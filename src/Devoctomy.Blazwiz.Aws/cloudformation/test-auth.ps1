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
  - They can create a profile's keys document (<identity id>/keys/<id>) only if there is none, replace it only at the
    version read (If-Match), delete it (which only adds a delete marker) but not delete any version of it, read the
    index (<identity id>/keys/index) if there is one, and are denied another user's keys. Then, as you, the test
    document's two versions and delete marker are checked and removed.
  - When the stack has the user events API: the user can publish to their own channel (/users/<sub>) and is refused
    another user's.
  - Last, signing out everywhere (Cognito GlobalSignOut) revokes the session: the access token is refused afterwards.
    This signs the test user out on every device; -SkipSignOutEverywhere leaves it out.

It creates a small test object under the user's own prefix and a test keys document, and deletes both again. The
user's real index is only read, never written. The event it publishes is a test event, which the portal ignores.
Subscriptions are not tested from here: the UI tests cover the browser side against the local channel.
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
    [Security.SecureString]$Password,

    # Leave out the last check, which signs the test user out on every device.
    [switch]$SkipSignOutEverywhere
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

function Write-ConditionalObject([string]$Key, [hashtable]$Condition) {
    # Writes the test file to a key on a condition (IfMatch or IfNoneMatch), as the portal writes keys documents, with
    # the signed-in user's credentials. The condition is passed in a file: ETags are quoted, and quotes do not survive
    # every shell's command line. Returns the response, or null with the error in $script:LastAwsError.
    $conditionFile = New-TempJsonFile $Condition
    try {
        return Invoke-Aws -NoProfile -Json -AllowFailure -Arguments @('s3api', 'put-object', '--bucket', $bucket, '--key', $Key, '--body', $localFile, '--cli-input-json', "file://$conditionFile")
    }
    finally {
        Remove-TempFile $conditionFile
    }
}

$stackName = Get-StackName $AppName $Environment
$keysKey = $null

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
$accessToken = $null
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
        $script:accessToken = $auth.AuthenticationResult.AccessToken
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

        Write-Step "Keys documents as the signed-in user"

        # The portal keeps each protected profile's keys document at <identity id>/keys/<profile id> and the index of
        # them at <identity id>/keys/index, and writes them only conditionally. A made-up profile id is used here, so
        # the user's real keys and index are only read.
        $script:keysKey = "$identityId/keys/$([Guid]::NewGuid().ToString('D'))"
        $indexKey = "$identityId/keys/index"

        Test-Check 'Can create a keys document only if there is none (If-None-Match: *)' {
            $created = Write-ConditionalObject $keysKey @{ IfNoneMatch = '*' }
            if ($null -eq $created) { throw $script:LastAwsError }
            $script:keysETag = $created.ETag
            $script:keysFirstVersion = $created.VersionId
            $keysKey
        } | Out-Null

        Test-Check 'Cannot create it again (412)' {
            $result = Write-ConditionalObject $keysKey @{ IfNoneMatch = '*' }
            if ($null -ne $result -or $script:LastAwsError -notmatch 'PreconditionFailed|412') {
                throw 'Expected PreconditionFailed creating an existing keys document'
            }
            'PreconditionFailed, as expected'
        } | Out-Null

        Test-Check 'Can replace it at the version read (If-Match)' {
            $replaced = Write-ConditionalObject $keysKey @{ IfMatch = $keysETag }
            if ($null -eq $replaced) { throw $script:LastAwsError }
            if ($replaced.ETag -eq $keysETag) { throw 'Expected a new ETag' }
        } | Out-Null

        Test-Check 'Cannot replace it at an older version (412)' {
            $result = Write-ConditionalObject $keysKey @{ IfMatch = $keysETag }
            if ($null -ne $result -or $script:LastAwsError -notmatch 'PreconditionFailed|412') {
                throw 'Expected PreconditionFailed replacing with a stale ETag'
            }
            'PreconditionFailed, as expected'
        } | Out-Null

        Test-Check 'Can delete it, which only adds a delete marker' {
            $deleted = Invoke-Aws -NoProfile -Json -Arguments @('s3api', 'delete-object', '--bucket', $bucket, '--key', $keysKey)
            if (-not (Get-OptionalProperty $deleted 'DeleteMarker')) { throw 'Expected a delete marker' }
        } | Out-Null

        Test-Check 'Cannot delete a version of it' {
            $result = Invoke-Aws -NoProfile -AllowFailure -Arguments @('s3api', 'delete-object', '--bucket', $bucket, '--key', $keysKey, '--version-id', $keysFirstVersion)
            if ($null -ne $result -or $script:LastAwsError -notmatch 'AccessDenied|Forbidden|403') {
                throw 'Expected AccessDenied deleting a version'
            }
            'AccessDenied, as expected'
        } | Out-Null

        Test-Check 'Can read the index' {
            $indexFile = Join-Path ([IO.Path]::GetTempPath()) "$AppName-test-auth-index-$([Guid]::NewGuid().ToString('N')).json"
            try {
                $index = Invoke-Aws -NoProfile -Json -AllowFailure -Arguments @('s3api', 'get-object', '--bucket', $bucket, '--key', $indexKey, $indexFile)
                if ($null -ne $index) {
                    'Existing index read'
                }
                elseif ($script:LastAwsError -match 'NoSuchKey|404') {
                    'No index yet (the user has no protected profiles); reading it is allowed'
                }
                else {
                    throw $script:LastAwsError
                }
            }
            finally {
                Remove-TempFile $indexFile
            }
        } | Out-Null

        Test-Check "Is denied another user's keys" {
            foreach ($otherKeys in @("someone-else/keys/$([Guid]::NewGuid().ToString('D'))", 'someone-else/keys/index')) {
                $result = Write-ConditionalObject $otherKeys @{ IfNoneMatch = '*' }
                if ($null -ne $result -or $script:LastAwsError -notmatch 'AccessDenied|Forbidden|403') {
                    throw "Expected AccessDenied creating $otherKeys"
                }
                $result = Invoke-Aws -NoProfile -AllowFailure -Arguments @('s3api', 'get-object', '--bucket', $bucket, '--key', $otherKeys, (Join-Path ([IO.Path]::GetTempPath()) "$AppName-test-auth-other-$([Guid]::NewGuid().ToString('N')).json"))
                if ($null -ne $result -or $script:LastAwsError -notmatch 'AccessDenied|Forbidden|403') {
                    throw "Expected AccessDenied reading $otherKeys"
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

    if ($keysKey) {
        Write-Step "The test keys document's versions, as you"

        Test-Check 'Its versions and delete marker are kept (then removed, to clean up)' {
            $versions = Invoke-Aws -Json -Arguments @('s3api', 'list-object-versions', '--bucket', $bucket, '--prefix', $keysKey)
            $kept = @(Get-OptionalProperty $versions 'Versions' | Where-Object { $_ -and $_.Key -eq $keysKey })
            $markers = @(Get-OptionalProperty $versions 'DeleteMarkers' | Where-Object { $_ -and $_.Key -eq $keysKey })
            foreach ($version in $kept + $markers) {
                Invoke-Aws -AllowFailure -Arguments @('s3api', 'delete-object', '--bucket', $bucket, '--key', $keysKey, '--version-id', $version.VersionId) | Out-Null
            }
            if ($kept.Count -ne 2 -or $markers.Count -ne 1) {
                throw "Expected 2 versions and 1 delete marker, found $($kept.Count) and $($markers.Count)"
            }
            '2 versions and a delete marker'
        } | Out-Null
    }
}

# User events (as the signed-in user) ---------------------------------------------------------

function Send-UserEvent([string]$Channel) {
    # Publishes one test event over HTTPS, as the portal does. Returns the HTTP status, or 0 when the API refused it in
    # the response body.
    $body = @{ channel = $Channel; events = @((@{ type = 'test-auth'; from = 'test-auth' } | ConvertTo-Json -Compress)) } | ConvertTo-Json -Compress
    try {
        $response = Invoke-WebRequest -Method Post -Uri "https://$($outputs['EventsHttpEndpoint'])/event" `
            -Headers @{ Authorization = $accessToken } -ContentType 'application/json' -Body $body -UseBasicParsing
        $result = $response.Content | ConvertFrom-Json
        $failed = $result.PSObject.Properties['failed']
        if ($null -ne $failed -and @($failed.Value).Count -gt 0) { return 0 }
        return [int]$response.StatusCode
    }
    catch {
        $status = $_.Exception.Response.StatusCode
        if ($null -ne $status) { return [int]$status }
        throw
    }
}

if ($accessToken -and $outputs['EventsHttpEndpoint']) {
    Write-Step "User events as the signed-in user"
    $subject = Get-TokenSubject $accessToken

    Test-Check "Publish to own channel /users/$subject" {
        $status = Send-UserEvent "/users/$subject"
        if ($status -ne 200) { throw "Expected HTTP 200, got $status" }
        'HTTP 200'
    } | Out-Null

    Test-Check 'Refused publishing to another user''s channel' {
        $status = Send-UserEvent '/users/someone-else'
        if ($status -eq 200) { throw 'Expected the channel handler to refuse it' }
        "Refused (HTTP $status)"
    } | Out-Null
}
elseif (-not $outputs['EventsHttpEndpoint']) {
    Write-Info 'The stack has no user events API (Events=disabled): skipping its checks.'
}

if ($accessToken -and -not $SkipSignOutEverywhere) {
    Write-Step "Sign out everywhere"
    Test-Check 'Global sign-out revokes the access token' {
        Invoke-Aws -NoProfile -Arguments @('cognito-idp', 'global-sign-out', '--access-token', $accessToken) | Out-Null
        $result = Invoke-Aws -NoProfile -AllowFailure -Arguments @('cognito-idp', 'get-user', '--access-token', $accessToken)
        if ($null -ne $result -or $script:LastAwsError -notmatch 'NotAuthorized') {
            throw 'Expected NotAuthorizedException for the revoked access token'
        }
        'Refused afterwards (NotAuthorizedException), as expected'
    } | Out-Null
}

Write-Host ''
if ($script:Failures -gt 0) {
    Write-Host "$($script:Failures) check(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host 'All checks passed.' -ForegroundColor Green
