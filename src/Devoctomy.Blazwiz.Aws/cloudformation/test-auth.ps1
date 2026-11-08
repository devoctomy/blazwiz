<#
.SYNOPSIS
Verifies a deployed blazwiz app's environment end to end, using the same flow the portal will use.

.DESCRIPTION
Checks, and reports PASS / FAIL for each:
  - The portal is served over HTTPS, deep links fall back to index.html, and the deployed
    appsettings.Production.json points at this stack's user pool.
  - The user can sign in to the Cognito user pool (with the admin flow, as your credentials: the portal's client
    only allows SRP from the browser).
  - The ID token can be exchanged for temporary AWS credentials via the identity pool.
  - Those credentials can read / write / list the user's own prefix in the data bucket, and are denied writing,
    reading and listing anyone else's prefix, and listing the whole bucket.
  - They can create (tagged, as the portal does), read and delete a profile's keys secret (<sub>/keys/<id>), read the
    index (<sub>/keys/index) if there is one, and are denied another user's secrets.

It creates a small test object under the user's own prefix and a test profile secret, and deletes both again. The
user's real index is only read, never written.
Exits with code 1 if any check fails.

.EXAMPLE
./cloudformation/test-auth.ps1 -Region eu-west-2 -Email me@example.com
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$AppName,
    [string]$Environment = 'dev',
    [string]$Region,
    [string]$AwsProfile,

    [Parameter(Mandatory = $true)]
    [string]$Email,

    # Prompted for if not supplied.
    [Security.SecureString]$Password
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$script:Failures = 0

function Get-TokenSubject([string]$Token) {
    $payload = $Token.Split('.')[1].Replace('-', '+').Replace('_', '/')
    $payload += '=' * ((4 - $payload.Length % 4) % 4)
    ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json).sub
}

function Test-Check([string]$Name, [scriptblock]$Check) {
    try {
        $detail = & $Check
        Write-Host "    [PASS] $Name" -ForegroundColor Green
        if ($detail) { Write-Host "           $detail" -ForegroundColor DarkGray }
        return $true
    }
    catch {
        $script:Failures++
        Write-Host "    [FAIL] $Name" -ForegroundColor Red
        Write-Host "           $($_.Exception.Message)" -ForegroundColor DarkGray
        return $false
    }
}

$stackName = Get-StackName $AppName $Environment

Write-Step "Checking AWS access"
Initialize-AwsContext -Region $Region -AwsProfile $AwsProfile | Out-Null

$outputs = Get-StackOutputs $stackName
if ($null -eq $outputs) {
    throw "Stack '$stackName' does not exist in $script:AwsRegion."
}

if ($null -eq $Password) {
    $Password = Read-Host "    Password for $Email" -AsSecureString
}

# Portal --------------------------------------------------------------------------------------

Write-Step "Portal ($($outputs['PortalUrl']))"

Test-Check 'Portal home page is served' {
    $response = Invoke-WebRequest -Uri $outputs['PortalUrl'] -UseBasicParsing
    if ($response.StatusCode -ne 200) { throw "Status $($response.StatusCode)" }
    if ($response.Content -notmatch 'blazor\.webassembly') { throw "Response does not look like the $AppName portal" }
    'HTTP 200'
} | Out-Null

Test-Check 'Deep links fall back to index.html (client side routing)' {
    $response = Invoke-WebRequest -Uri "$($outputs['PortalUrl'])/some/deep/link" -UseBasicParsing
    if ($response.StatusCode -ne 200) { throw "Status $($response.StatusCode)" }
    if ($response.Content -notmatch 'blazor\.webassembly') { throw 'Did not get index.html' }
    'HTTP 200'
} | Out-Null

Test-Check 'Deployed settings point at this user pool' {
    $settings = Invoke-RestMethod -Uri "$($outputs['PortalUrl'])/appsettings.Production.json"
    if ($settings.AwsCognito.UserPoolId -ne $outputs['UserPoolId']) {
        throw "Expected $($outputs['UserPoolId']) but found '$($settings.AwsCognito.UserPoolId)'"
    }
    $settings.AwsCognito.UserPoolId
} | Out-Null

# Cognito -------------------------------------------------------------------------------------

Write-Step "Cognito"

$tempFiles = New-Object System.Collections.Generic.List[string]
$idToken = $null
$identityId = $null
$credentials = $null
$loginsFile = $null

try {
    Test-Check "User pool sign-in as $Email" {
        # The portal's client only allows SRP from the browser, so the command line signs in with the admin flow, as
        # your credentials (cognito-idp:AdminInitiateAuth). The password goes through a file only you can read.
        $authFile = New-TempJsonFile @{ USERNAME = $Email; PASSWORD = (ConvertTo-PlainText $Password) }
        $tempFiles.Add($authFile)
        try {
            $auth = Invoke-Aws -Json -Arguments @(
                'cognito-idp', 'admin-initiate-auth',
                '--auth-flow', 'ADMIN_USER_PASSWORD_AUTH',
                '--user-pool-id', $outputs['UserPoolId'],
                '--client-id', $outputs['UserPoolClientId'],
                '--auth-parameters', "file://$authFile"
            )
        }
        finally {
            Remove-TempFile $authFile
        }

        $challenge = $auth.PSObject.Properties['ChallengeName']
        if ($null -ne $challenge -and $challenge.Value) {
            throw "Sign-in needs challenge '$($challenge.Value)'. Set a permanent password with deploy.ps1 -TestUserPassword."
        }

        $script:idToken = $auth.AuthenticationResult.IdToken
        'Received ID token'
    } | Out-Null

    if ($idToken) {
        $providerName = "cognito-idp.$($outputs['Region']).amazonaws.com/$($outputs['UserPoolId'])"
        $loginsFile = New-TempJsonFile @{ $providerName = $idToken }
        $tempFiles.Add($loginsFile)

        Test-Check 'Identity pool issues an identity id' {
            $result = Invoke-Aws -Json -NoProfile -Arguments @(
                'cognito-identity', 'get-id',
                '--identity-pool-id', $outputs['IdentityPoolId'],
                '--logins', "file://$loginsFile"
            )
            $script:identityId = $result.IdentityId
            $identityId
        } | Out-Null
    }

    if ($identityId) {
        Test-Check 'Identity pool issues temporary AWS credentials' {
            $result = Invoke-Aws -Json -NoProfile -Arguments @(
                'cognito-identity', 'get-credentials-for-identity',
                '--identity-id', $identityId,
                '--logins', "file://$loginsFile"
            )
            $script:credentials = $result.Credentials
            "Expires $($credentials.Expiration)"
        } | Out-Null
    }
}
finally {
    foreach ($file in $tempFiles) { Remove-Item -Force $file -ErrorAction SilentlyContinue }
}

# Per-user data access (as the signed-in user) ------------------------------------------------

if ($credentials) {
    Write-Step "Data bucket access as the signed-in user"

    $bucket = $outputs['DataBucketName']
    $ownKey = "$identityId/$AppName-test-auth.txt"
    $otherKey = "someone-else/$AppName-test-auth.txt"
    $localFile = Join-Path ([IO.Path]::GetTempPath()) "$AppName-test-auth-$([Guid]::NewGuid().ToString('N')).txt"
    Set-Content -Path $localFile -Value "$AppName test-auth $(Get-Date -Format o)"

    # Use the user's temporary credentials (not your own) for these calls.
    $savedEnvironment = @{}
    foreach ($name in 'AWS_ACCESS_KEY_ID', 'AWS_SECRET_ACCESS_KEY', 'AWS_SESSION_TOKEN', 'AWS_PROFILE') {
        $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    try {
        $env:AWS_ACCESS_KEY_ID = $credentials.AccessKeyId
        $env:AWS_SECRET_ACCESS_KEY = $credentials.SecretKey
        $env:AWS_SESSION_TOKEN = $credentials.SessionToken
        Remove-Item Env:AWS_PROFILE -ErrorAction SilentlyContinue

        Test-Check 'Can write to own prefix' {
            Invoke-Aws -NoProfile -Arguments @('s3', 'cp', $localFile, "s3://$bucket/$ownKey", '--only-show-errors') | Out-Null
            "s3://$bucket/$ownKey"
        } | Out-Null

        Test-Check 'Can read from own prefix' {
            $content = Invoke-Aws -NoProfile -Arguments @('s3', 'cp', "s3://$bucket/$ownKey", '-')
            if (-not ($content -join '' -match "$AppName test-auth")) { throw 'Unexpected content' }
        } | Out-Null

        Test-Check 'Can list own prefix' {
            $listing = Invoke-Aws -NoProfile -Json -Arguments @('s3api', 'list-objects-v2', '--bucket', $bucket, '--prefix', "$identityId/")
            if (@($listing.Contents).Count -lt 1) { throw 'Test object not listed' }
        } | Out-Null

        Test-Check "Is denied access to another user's prefix" {
            $result = Invoke-Aws -NoProfile -AllowFailure -Arguments @('s3', 'cp', $localFile, "s3://$bucket/$otherKey", '--only-show-errors')
            if ($null -ne $result -or $script:LastAwsError -notmatch 'AccessDenied|Forbidden|403') {
                throw "Expected AccessDenied writing to $otherKey"
            }
            $result = Invoke-Aws -NoProfile -AllowFailure -Arguments @('s3api', 'get-object', '--bucket', $bucket, '--key', $otherKey, (Join-Path ([IO.Path]::GetTempPath()) "$AppName-test-auth-other-$([Guid]::NewGuid().ToString('N')).txt"))
            if ($null -ne $result -or $script:LastAwsError -notmatch 'AccessDenied|Forbidden|403') {
                throw "Expected AccessDenied reading $otherKey"
            }
            $result = Invoke-Aws -NoProfile -AllowFailure -Arguments @('s3api', 'list-objects-v2', '--bucket', $bucket, '--prefix', 'someone-else/')
            if ($null -ne $result -or $script:LastAwsError -notmatch 'AccessDenied|Forbidden|403') {
                throw "Expected AccessDenied listing someone-else/"
            }
            $result = Invoke-Aws -NoProfile -AllowFailure -Arguments @('s3api', 'list-objects-v2', '--bucket', $bucket)
            if ($null -ne $result -or $script:LastAwsError -notmatch 'AccessDenied|Forbidden|403') {
                throw 'Expected AccessDenied listing the whole bucket'
            }
            'AccessDenied for writing, reading and listing, as expected'
        } | Out-Null

        Test-Check 'Can delete from own prefix (clean up)' {
            Invoke-Aws -NoProfile -Arguments @('s3', 'rm', "s3://$bucket/$ownKey", '--only-show-errors') | Out-Null
        } | Out-Null

        Write-Step "Keys secrets as the signed-in user"

        $sub = Get-TokenSubject $idToken
        $profileSecret = "$sub/keys/$([Guid]::NewGuid().ToString('D'))"
        $indexSecret = "$sub/keys/index"
        $secretText = "$AppName test-auth"

        Test-Check 'Can create a profile secret, tagged with the app' {
            # Tagged as the portal tags them (secretsmanager:TagResource), so remove.ps1 can find them.
            Invoke-Aws -NoProfile -Arguments @('secretsmanager', 'create-secret', '--name', $profileSecret, '--secret-string', $secretText, '--tags', "Key=app,Value=$AppName") | Out-Null
            $profileSecret
        } | Out-Null

        Test-Check 'Can read a profile secret' {
            $secret = Invoke-Aws -NoProfile -Json -Arguments @('secretsmanager', 'get-secret-value', '--secret-id', $profileSecret)
            if ($secret.SecretString -ne $secretText) { throw 'Unexpected content' }
        } | Out-Null

        Test-Check 'Can delete a profile secret (clean up)' {
            Invoke-Aws -NoProfile -Arguments @('secretsmanager', 'delete-secret', '--secret-id', $profileSecret, '--force-delete-without-recovery') | Out-Null
        } | Out-Null

        # The index lists the user's real profiles, so it is only read: writing is already shown by the profile secret.
        Test-Check 'Can read the index secret' {
            $index = Invoke-Aws -NoProfile -Json -AllowFailure -Arguments @('secretsmanager', 'get-secret-value', '--secret-id', $indexSecret)
            if ($null -ne $index) {
                'Existing index read'
            }
            elseif ($script:LastAwsError -match 'ResourceNotFoundException') {
                'No index yet (the user has no protected profiles); reading it is allowed'
            }
            else {
                throw $script:LastAwsError
            }
        } | Out-Null

        Test-Check "Is denied another user's secrets" {
            foreach ($otherSecret in @("someone-else/keys/$([Guid]::NewGuid().ToString('D'))", 'someone-else/keys/index')) {
                $result = Invoke-Aws -NoProfile -AllowFailure -Arguments @('secretsmanager', 'create-secret', '--name', $otherSecret, '--secret-string', $secretText)
                if ($null -ne $result -or $script:LastAwsError -notmatch 'AccessDenied') {
                    throw "Expected AccessDenied creating $otherSecret"
                }
                $result = Invoke-Aws -NoProfile -AllowFailure -Arguments @('secretsmanager', 'get-secret-value', '--secret-id', $otherSecret)
                if ($null -ne $result -or $script:LastAwsError -notmatch 'AccessDenied') {
                    throw "Expected AccessDenied reading $otherSecret"
                }
            }
            'AccessDenied, as expected'
        } | Out-Null
    }
    finally {
        foreach ($name in $savedEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
        }
        Remove-Item -Force $localFile -ErrorAction SilentlyContinue
    }
}

Write-Host ''
if ($script:Failures -gt 0) {
    Write-Host "$($script:Failures) check(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host 'All checks passed.' -ForegroundColor Green
