<#
.SYNOPSIS
Deploys blazwiz to AWS: infrastructure (CloudFormation), portal (S3 + CloudFront) and a test user (Cognito).

.DESCRIPTION
Safe to run repeatedly. Each run:
  1. Creates / updates the CloudFormation stack "<AppName>-<Environment>".
  2. Writes the stack outputs into wwwroot/appsettings.Production.json and publishes the portal.
  3. Uploads the portal to S3 with sensible Cache-Control headers and invalidates CloudFront.
  4. Creates the test user (or resets their password) if -TestUserEmail is given.

Needs the AWS CLI v2 and the .NET 10 SDK. Credentials come from your AWS CLI configuration
(use -AwsProfile to pick a named profile).

.EXAMPLE
./cloudformation/deploy.ps1 -Region eu-west-2 -TestUserEmail me@example.com

.EXAMPLE
./cloudformation/deploy.ps1 -Environment test -AwsProfile work -SkipTestUser
#>
[CmdletBinding()]
param(
    # Short lowercase app name (letters and numbers), used to name resources.
    [string]$AppName = 'blazwiz',

    # Environment name, e.g. dev, test, prod.
    [string]$Environment = 'dev',

    # AWS region. Defaults to the region configured for the AWS CLI profile.
    [string]$Region,

    # Named AWS CLI profile. Defaults to the default credential chain.
    [string]$AwsProfile,

    # Email address of a test user to create. The user is created with a permanent password (no reset on first sign-in).
    [string]$TestUserEmail,

    # Password for the test user (min 12 chars, upper, lower and a number). Prompted for if needed and not supplied.
    [Security.SecureString]$TestUserPassword,

    # Optional custom domain for the portal, e.g. app.example.com.
    [string]$CustomDomainName = '',

    # ACM certificate ARN (us-east-1) for the custom domain.
    [string]$CertificateArn = '',

    # Skip the CloudFormation deployment (just publish the portal / manage the user).
    [switch]$SkipInfrastructure,

    # Skip publishing and uploading the portal.
    [switch]$SkipPortal,

    # Skip creating / updating the test user.
    [switch]$SkipTestUser,

    # Do not ask for confirmation before making changes.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$stackName = Get-StackName $AppName $Environment
$templatePath = Join-Path $PSScriptRoot 'blazwiz-aws.yaml'
$portalProject = Join-Path $script:RepoRoot 'blazwiz.Portal/blazwiz.Portal.csproj'
$productionSettingsPath = Join-Path $script:RepoRoot 'blazwiz.Portal/wwwroot/appsettings.Production.json'
$publishDirectory = Join-Path $script:RepoRoot "publish/$stackName"

# Files that must always be revalidated so a new deployment is picked up straight away.
# Everything else is cached for a day (framework files are fingerprinted, the service worker checks hashes).
$noCacheFiles = [ordered]@{
    'index.html'                  = 'text/html; charset=utf-8'
    'service-worker.js'           = 'text/javascript; charset=utf-8'
    'service-worker-assets.js'    = 'text/javascript; charset=utf-8'
    'manifest.webmanifest'        = 'application/manifest+json'
    'appsettings.json'            = 'application/json; charset=utf-8'
    'appsettings.Production.json' = 'application/json; charset=utf-8'
}

if ($CustomDomainName -and -not $CertificateArn) {
    throw '-CertificateArn (an ACM certificate in us-east-1) is required when -CustomDomainName is set.'
}

Write-Step "Checking AWS access"
$identity = Initialize-AwsContext -Region $Region -AwsProfile $AwsProfile
Confirm-Action -Force:$Force -Message "This will create / update stack '$stackName' and its resources in account $($identity.Account) ($script:AwsRegion)."

# 1. Infrastructure ---------------------------------------------------------------------------

if (-not $SkipInfrastructure) {
    Write-Step "Deploying CloudFormation stack '$stackName' (first run takes ~5 minutes, mostly CloudFront)"
    Invoke-Aws -Stream -Arguments @(
        'cloudformation', 'deploy',
        '--stack-name', $stackName,
        '--template-file', $templatePath,
        '--capabilities', 'CAPABILITY_IAM',
        '--no-fail-on-empty-changeset',
        '--parameter-overrides',
        "AppName=$AppName",
        "Environment=$Environment",
        "CustomDomainName=$CustomDomainName",
        "CertificateArn=$CertificateArn",
        '--tags',
        "app=$AppName",
        "environment=$Environment"
    )
    Write-Ok "Stack is up to date"
}

$outputs = Get-StackOutputs $stackName
if ($null -eq $outputs) {
    throw "Stack '$stackName' does not exist in $script:AwsRegion. Run without -SkipInfrastructure first."
}

# 2 + 3. Portal -------------------------------------------------------------------------------

if (-not $SkipPortal) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'The .NET SDK is required to publish the portal.'
    }

    Write-Step "Publishing portal"
    $productionSettings = [ordered]@{
        AwsCognito = [ordered]@{
            Region           = $outputs['Region']
            UserPoolId       = $outputs['UserPoolId']
            UserPoolClientId = $outputs['UserPoolClientId']
            IdentityPoolId   = $outputs['IdentityPoolId']
        }
        AwsS3      = [ordered]@{
            Region         = $outputs['Region']
            DataBucketName = $outputs['DataBucketName']
        }
    }

    # Written before publishing (not patched afterwards) so the service worker's asset hashes stay valid.
    # Blazor loads appsettings.Production.json on top of appsettings.json when hosted (Production environment).
    [IO.File]::WriteAllText(
        $productionSettingsPath,
        ($productionSettings | ConvertTo-Json -Depth 5),
        (New-Object Text.UTF8Encoding($false)))

    try {
        if (Test-Path $publishDirectory) { Remove-Item -Recurse -Force $publishDirectory }
        & dotnet publish $portalProject -c Release -o $publishDirectory --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)" }
    }
    finally {
        Remove-Item -Force $productionSettingsPath -ErrorAction SilentlyContinue
    }

    $wwwroot = Join-Path $publishDirectory 'wwwroot'
    $bucketUri = "s3://$($outputs['PortalBucketName'])"

    Write-Step "Uploading portal to $bucketUri"
    $syncArgs = @(
        's3', 'sync', $wwwroot, $bucketUri,
        '--delete',
        '--only-show-errors',
        '--cache-control', 'public, max-age=86400',
        # Pre-compressed copies are not needed, CloudFront compresses on the fly.
        '--exclude', '*.br',
        '--exclude', '*.gz'
    )
    foreach ($file in $noCacheFiles.Keys) { $syncArgs += @('--exclude', $file) }
    Invoke-Aws -Arguments $syncArgs | Out-Null

    foreach ($file in $noCacheFiles.Keys) {
        $localPath = Join-Path $wwwroot $file
        if (Test-Path $localPath) {
            Invoke-Aws -Arguments @(
                's3', 'cp', $localPath, "$bucketUri/$file",
                '--only-show-errors',
                '--cache-control', 'no-cache',
                '--content-type', $noCacheFiles[$file]
            ) | Out-Null
        }
    }
    Write-Ok "Uploaded"

    Write-Step "Invalidating CloudFront cache"
    $invalidation = Invoke-Aws -Json -Arguments @(
        'cloudfront', 'create-invalidation',
        '--distribution-id', $outputs['DistributionId'],
        '--paths', '/*'
    )
    Write-Ok "Invalidation $($invalidation.Invalidation.Id) started (usually completes within a minute or two)"
}

# 4. Test user --------------------------------------------------------------------------------

if (-not $SkipTestUser -and $TestUserEmail) {
    Write-Step "Setting up test user $TestUserEmail"
    $userPoolId = $outputs['UserPoolId']

    $existing = Invoke-Aws -Json -AllowFailure -Arguments @(
        'cognito-idp', 'admin-get-user',
        '--user-pool-id', $userPoolId,
        '--username', $TestUserEmail
    )
    if ($null -eq $existing -and $script:LastAwsError -notmatch 'UserNotFoundException') {
        throw "Could not look up user: $script:LastAwsError"
    }

    # A user left with a temporary password (e.g. by an earlier failed run) cannot sign in, so set one.
    if ($null -ne $existing -and $null -eq $TestUserPassword -and $existing.UserStatus -ne 'FORCE_CHANGE_PASSWORD') {
        Write-Ok "User already exists, password left unchanged (pass -TestUserPassword to reset it)"
    }
    else {
        if ($null -eq $TestUserPassword) {
            $TestUserPassword = Read-Host "    Password for $TestUserEmail (min 12 chars, upper, lower, number)" -AsSecureString
        }

        # Check against the user pool's password policy (see blazwiz-aws.yaml) before creating the user,
        # so a rejected password does not leave behind a user with no usable password.
        $plainPassword = ConvertTo-PlainText $TestUserPassword
        $passwordOk = $plainPassword.Length -ge 12 -and $plainPassword -cmatch '[a-z]' -and $plainPassword -cmatch '[A-Z]' -and $plainPassword -match '[0-9]'
        $plainPassword = $null
        if (-not $passwordOk) {
            throw 'The test user password must be at least 12 characters and contain an upper case letter, a lower case letter and a number.'
        }

        if ($null -eq $existing) {
            Invoke-Aws -Arguments @(
                'cognito-idp', 'admin-create-user',
                '--user-pool-id', $userPoolId,
                '--username', $TestUserEmail,
                '--user-attributes', "Name=email,Value=$TestUserEmail", 'Name=email_verified,Value=true',
                '--message-action', 'SUPPRESS'
            ) | Out-Null
            Write-Ok "User created"
        }

        $plainPassword = ConvertTo-PlainText $TestUserPassword
        try {
            Invoke-Aws -Arguments @(
                'cognito-idp', 'admin-set-user-password',
                '--user-pool-id', $userPoolId,
                '--username', $TestUserEmail,
                '--password', $plainPassword,
                '--permanent'
            ) | Out-Null
        }
        finally {
            $plainPassword = $null
        }
        Write-Ok "Password set (permanent)"
    }
}

# Summary -------------------------------------------------------------------------------------

Write-Step "Done"
Write-Info "Portal        : $($outputs['PortalUrl'])"
Write-Info "User pool     : $($outputs['UserPoolId'])"
Write-Info "App client    : $($outputs['UserPoolClientId'])"
Write-Info "Identity pool : $($outputs['IdentityPoolId'])"
Write-Info "Data bucket   : $($outputs['DataBucketName'])"
Write-Host ''
Write-Info "Verify sign-in and per-user AWS access with:"
Write-Info "  ./cloudformation/test-auth.ps1 -Environment $Environment -Region $script:AwsRegion -Email <user>"
