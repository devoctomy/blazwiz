<#
.SYNOPSIS
Deploys a blazwiz app to AWS: infrastructure (CloudFormation), portal (S3 + CloudFront) and a test user (Cognito).

.DESCRIPTION
The base stack and this script are released with the framework (Devoctomy.Blazwiz.Aws). Apps run it through
their own cloudformation/deploy.ps1, which passes the app's name, folder and portal project.

Safe to run repeatedly. An optional parameter that is not given (-CustomDomainName, -CertificateArn,
-LocalDevOrigins, -UserPoolDeletionProtection, -AuditLogging, -Events) keeps the value the stack already has, so an upgrade
run need not repeat them. Each run:
  1. Creates / updates the CloudFormation stack "<AppName>-<Environment>" through a change set. The changes
     are listed first, replacements need confirming, and replacing the user pool, identity pool, data bucket,
     audit log bucket, CloudFront distribution or app client is refused unless -AllowStatefulReplacement is
     given (see stack-policy.json). Then the app's own
     stack "<AppName>-<Environment>-app" (-AppStackTemplate), which imports the base stack's outputs.
  2. Writes the stack outputs into wwwroot/appsettings.Production.json, publishes the portal and adds its
     Content-Security-Policy (see Add-ContentSecurityPolicy in _common.ps1).
  3. Uploads the portal to S3 with sensible Cache-Control headers and invalidates CloudFront.
  4. Creates the test user (or resets their password) if -TestUserEmail is given.

Needs the AWS CLI v2 and the .NET 10 SDK. Credentials come from your AWS CLI configuration
(use -AwsProfile to pick a named profile).

.EXAMPLE
./cloudformation/deploy.ps1 -Region eu-west-2 -TestUserEmail me@example.com
(the app's wrapper, which runs this script)

.EXAMPLE
./cloudformation/deploy.ps1 -Environment test -AwsProfile work -SkipTestUser
#>
[CmdletBinding()]
param(
    # Short lowercase app name (letters and numbers), used to name resources.
    [Parameter(Mandatory)]
    [string]$AppName,

    # The app's folder (holding its solution).
    [Parameter(Mandatory)]
    [string]$AppRoot,

    # The portal project, relative to -AppRoot, e.g. MyApp.Portal/MyApp.Portal.csproj.
    [Parameter(Mandatory)]
    [string]$PortalProject,

    # The app's own CloudFormation template, deployed after the base stack as "<stack name>-app". Optional.
    [string]$AppStackTemplate,

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

    # Optional custom domain for the portal, e.g. app.example.com. Pass '' to remove one the stack has.
    [string]$CustomDomainName = '',

    # ACM certificate ARN (us-east-1) for the custom domain.
    [string]$CertificateArn = '',

    # Origins allowed to call the data bucket besides the portal, for running the portal locally against this
    # environment. Defaults to the portal's local development URLs for every environment but prod, and to none for prod.
    [string[]]$LocalDevOrigins,

    # Whether Cognito refuses to delete the user pool (and every user with it). auto: on for prod.
    [ValidateSet('auto', 'ACTIVE', 'INACTIVE')]
    [string]$UserPoolDeletionProtection = 'auto',

    # Whether every read, write and delete of an object in the data bucket is recorded by CloudTrail. auto: on for prod.
    [ValidateSet('auto', 'true', 'false')]
    [string]$AuditLogging = 'auto',

    # Whether to create the user events API (AppSync Events) that tells a user's open devices about a sign out
    # everywhere. Off unless asked for, so an app never gets a resource it does not use: apps created with user events
    # pass enabled through their own deploy.ps1.
    [ValidateSet('enabled', 'disabled')]
    [string]$Events = 'disabled',

    # Skip the CloudFormation deployment (just publish the portal / manage the user).
    [switch]$SkipInfrastructure,

    # Let the deployment replace or remove resources protected by stack-policy.json: the user pool, identity pool,
    # data bucket and audit log bucket (their users, data and logs would be lost), the CloudFront distribution (the
    # portal's address would change) and the app client (every session would end). Only when that is intended.
    [switch]$AllowStatefulReplacement,

    # Skip publishing and uploading the portal.
    [switch]$SkipPortal,

    # Publish the portal compiled ahead of time to WebAssembly, with SIMD (RunAOTCompilation, WasmEnableSIMD), instead
    # of running its .NET code in the WebAssembly interpreter. Much faster at run time (Argon2id key derivation most of
    # all), larger to download, and several minutes slower to publish. Needs the wasm-tools workload
    # (dotnet workload install wasm-tools). Setting both properties in the portal's project file does the same.
    [switch]$Aot,

    # Skip creating / updating the test user.
    [switch]$SkipTestUser,

    # Do not ask for confirmation before making changes.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$stackName = Get-StackName $AppName $Environment
$templatePath = Join-Path $PSScriptRoot 'stack.yaml'
$stackPolicyPath = Join-Path $PSScriptRoot 'stack-policy.json'
$appStackName = "$stackName-app"
$portalProject = Join-Path $AppRoot $PortalProject
$productionSettingsPath = Join-Path (Split-Path -Parent $portalProject) 'wwwroot/appsettings.Production.json'
$publishDirectory = Join-Path $AppRoot "publish/$stackName"

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

if ($null -eq $LocalDevOrigins) {
    $LocalDevOrigins = if ($Environment -eq 'prod') { @() } else { @('https://localhost:7090', 'http://localhost:5090') }
}

Write-Step "Checking AWS access"
$identity = Initialize-AwsContext -Region $Region -AwsProfile $AwsProfile
Confirm-Action -Force:$Force -Message "This will create / update stack '$stackName' and its resources in account $($identity.Account) ($script:AwsRegion)."

# 1. Infrastructure ---------------------------------------------------------------------------

if (-not $SkipInfrastructure) {
    # Applies stack-policy.json, naming only the protected resources the stack has: CloudFormation refuses a policy
    # that names a resource the stack lacks, such as the audit log bucket, which is conditional. On failure the policy
    # already on the stack stays, so it is a warning.
    function Set-StackPolicy {
        $resources = Invoke-Aws -Json -AllowFailure -Arguments @('cloudformation', 'describe-stack-resources', '--stack-name', $stackName)
        if ($null -eq $resources) {
            Write-Warn "Could not apply the stack policy: $script:LastAwsError"
            return
        }

        $present = @($resources.StackResources | ForEach-Object { $_.LogicalResourceId })
        $policy = Get-Content -Raw $stackPolicyPath | ConvertFrom-Json
        foreach ($statement in @($policy.Statement | Where-Object { $_.Effect -eq 'Deny' })) {
            $statement.Resource = @($statement.Resource | Where-Object { $present -contains ($_ -replace '^LogicalResourceId/', '') })
        }

        $policyFile = New-TempJsonFile $policy
        try {
            Invoke-Aws -AllowFailure -Arguments @('cloudformation', 'set-stack-policy', '--stack-name', $stackName, '--stack-policy-body', "file://$policyFile") | Out-Null
            if ($null -ne $script:LastAwsError) {
                Write-Warn "Could not apply the stack policy: $script:LastAwsError"
            }
        }
        finally {
            Remove-TempFile $policyFile
        }
    }

    $existingStack = Invoke-Aws -Json -AllowFailure -Arguments @('cloudformation', 'describe-stacks', '--stack-name', $stackName)
    if ($null -eq $existingStack -and $script:LastAwsError -notmatch 'does not exist') {
        throw "Could not describe stack $stackName`: $script:LastAwsError"
    }
    # A stack left in REVIEW_IN_PROGRESS by an earlier, unexecuted create change set is still created with CREATE.
    $changeSetType = if ($null -eq $existingStack -or $existingStack.Stacks[0].StackStatus -eq 'REVIEW_IN_PROGRESS') { 'CREATE' } else { 'UPDATE' }
    $changeSetName = "deploy-$(Get-Date -Format 'yyyyMMddHHmmss')"

    Write-Step "Working out changes to stack '$stackName'"
    # An optional parameter that is not given keeps the value the stack already has (e.g. the custom domain), so an
    # upgrade run does not have to repeat every setting. The first deployment, or a stack from before the parameter
    # existed, gets the default.
    $previousKeys = if ($changeSetType -eq 'UPDATE') { @($existingStack.Stacks[0].Parameters | ForEach-Object { $_.ParameterKey }) } else { @() }
    $kept = @()
    $parameters = @(
        @{ ParameterKey = 'AppName'; ParameterValue = $AppName }
        @{ ParameterKey = 'Environment'; ParameterValue = $Environment }
    )
    $optional = [ordered]@{
        CustomDomainName           = $CustomDomainName
        CertificateArn             = $CertificateArn
        LocalDevOrigins            = ($LocalDevOrigins -join ',')
        UserPoolDeletionProtection = $UserPoolDeletionProtection
        AuditLogging               = $AuditLogging
        Events                     = $Events
    }
    foreach ($key in $optional.Keys) {
        if (-not $PSBoundParameters.ContainsKey($key) -and $previousKeys -contains $key) {
            $parameters += @{ ParameterKey = $key; UsePreviousValue = $true }
            $kept += $key
        }
        else {
            $parameters += @{ ParameterKey = $key; ParameterValue = $optional[$key] }
        }
    }
    if ($kept.Count -gt 0) {
        Write-Info "Keeping the stack's current $($kept -join ', ')"
    }
    $parametersFile = New-TempJsonFile $parameters
    $tagsFile = New-TempJsonFile @(
        @{ Key = 'app'; Value = $AppName }
        @{ Key = 'environment'; Value = $Environment }
    )
    try {
        Invoke-Aws -Arguments @(
            'cloudformation', 'create-change-set',
            '--stack-name', $stackName,
            '--change-set-name', $changeSetName,
            '--change-set-type', $changeSetType,
            '--template-body', "file://$templatePath",
            '--capabilities', 'CAPABILITY_IAM',
            '--parameters', "file://$parametersFile",
            '--tags', "file://$tagsFile"
        ) | Out-Null
    }
    finally {
        Remove-Item -Force $parametersFile, $tagsFile -ErrorAction SilentlyContinue
    }

    # The waiter fails when there is nothing to change, so the change set's status is checked instead.
    Invoke-Aws -AllowFailure -Arguments @('cloudformation', 'wait', 'change-set-create-complete', '--stack-name', $stackName, '--change-set-name', $changeSetName) | Out-Null
    $changeSet = Invoke-Aws -Json -Arguments @('cloudformation', 'describe-change-set', '--stack-name', $stackName, '--change-set-name', $changeSetName)

    if ($changeSet.Status -eq 'FAILED') {
        $reason = "$(Get-OptionalProperty $changeSet 'StatusReason')"
        if ($reason -notmatch "didn't contain changes|No updates are to be performed") {
            throw "Could not create change set: $reason"
        }
        Invoke-Aws -Arguments @('cloudformation', 'delete-change-set', '--stack-name', $stackName, '--change-set-name', $changeSetName) | Out-Null
        Write-Ok "No infrastructure changes"
        # Still applied, in case an earlier run could not set it.
        Set-StackPolicy
    }
    else {
        # Replacement creates a new resource and deletes the old one, losing its data and changing its id.
        $protected = @((Get-Content -Raw $stackPolicyPath | ConvertFrom-Json).Statement |
            Where-Object { $_.Effect -eq 'Deny' } |
            ForEach-Object { $_.Resource } |
            ForEach-Object { $_ -replace '^LogicalResourceId/', '' })
        $replacing = @()
        foreach ($change in @($changeSet.Changes)) {
            $resource = $change.ResourceChange
            $replacement = "$(Get-OptionalProperty $resource 'Replacement')"
            $line = "$($resource.Action.PadRight(7)) $($resource.LogicalResourceId) ($($resource.ResourceType))"
            if ($replacement -eq 'True' -or $replacement -eq 'Conditional') {
                $replacing += $resource.LogicalResourceId
                Write-Warn "$line - REPLACEMENT ($replacement)"
            }
            elseif ($resource.Action -eq 'Remove' -and $protected -contains $resource.LogicalResourceId) {
                $replacing += $resource.LogicalResourceId
                Write-Warn $line
            }
            else {
                Write-Info $line
            }
        }

        $blocked = @($replacing | Where-Object { $protected -contains $_ })
        if ($blocked.Count -gt 0 -and -not $AllowStatefulReplacement) {
            Invoke-Aws -Arguments @('cloudformation', 'delete-change-set', '--stack-name', $stackName, '--change-set-name', $changeSetName) | Out-Null
            throw ("This deployment would replace or remove $($blocked -join ', '): users, data or logs would be lost, or the portal's address or app client would change. Nothing was changed. " +
                'Revert the template change, or re-run with -AllowStatefulReplacement if that is really intended.')
        }
        if ($replacing.Count -gt 0) {
            Confirm-Action -Force:$Force -Message "The resources marked REPLACEMENT above will be recreated with new ids. Anything stored in them is not copied across."
        }

        Write-Step "Applying changes (first run takes ~5 minutes, mostly CloudFront)"
        $overrideFile = $null
        try {
            if ($AllowStatefulReplacement -and $changeSetType -eq 'UPDATE') {
                # Stack policies cannot be overridden for a change set, so lift it for this update only.
                $overrideFile = New-TempJsonFile @{ Statement = @(@{ Effect = 'Allow'; Principal = '*'; Action = 'Update:*'; Resource = '*' }) }
                Invoke-Aws -Arguments @('cloudformation', 'set-stack-policy', '--stack-name', $stackName, '--stack-policy-body', "file://$overrideFile") | Out-Null
            }

            Invoke-Aws -Arguments @('cloudformation', 'execute-change-set', '--stack-name', $stackName, '--change-set-name', $changeSetName) | Out-Null
            $waiter = if ($changeSetType -eq 'CREATE') { 'stack-create-complete' } else { 'stack-update-complete' }
            # Invoke-Aws's result cannot tell success from failure here (no output either way), so check the error.
            Invoke-Aws -AllowFailure -Arguments @('cloudformation', 'wait', $waiter, '--stack-name', $stackName) | Out-Null
            if ($null -ne $script:LastAwsError) {
                $events = Invoke-Aws -Json -AllowFailure -Arguments @('cloudformation', 'describe-stack-events', '--stack-name', $stackName, '--max-items', '50')
                $failedEvents = if ($null -eq $events) { @() } else { @($events.StackEvents) }
                foreach ($stackEvent in @($failedEvents | Where-Object { $_.ResourceStatus -like '*FAILED' } | Select-Object -First 5)) {
                    Write-Warn "$($stackEvent.LogicalResourceId): $(Get-OptionalProperty $stackEvent 'ResourceStatusReason')"
                }
                throw "Stack '$stackName' did not deploy (see the events above, or the CloudFormation console)."
            }
        }
        finally {
            if ($overrideFile) { Remove-Item -Force $overrideFile -ErrorAction SilentlyContinue }
            # Applied on every run so new stacks get it and -AllowStatefulReplacement's override is undone.
            Set-StackPolicy
        }
    }
    Write-Ok "Stack is up to date"

    if ($AppStackTemplate) {
        # The app's own resources, built from the base stack's exported outputs.
        Write-Step "Deploying the app's stack '$appStackName'"
        Invoke-Aws -Arguments @(
            'cloudformation', 'deploy',
            '--stack-name', $appStackName,
            '--template-file', $AppStackTemplate,
            '--parameter-overrides', "BaseStackName=$stackName",
            '--capabilities', 'CAPABILITY_IAM', 'CAPABILITY_NAMED_IAM',
            '--tags', "app=$AppName", "environment=$Environment",
            '--no-fail-on-empty-changeset'
        ) | Out-Null
        Write-Ok "App stack is up to date"
    }
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

    # The user events API, when the stack has one (Events=enabled). Without it the app runs without events.
    $eventsHttp = $outputs['EventsHttpEndpoint']
    $eventsRealtime = $outputs['EventsRealtimeEndpoint']
    if ($eventsHttp -and $eventsRealtime) {
        $productionSettings['Events'] = [ordered]@{
            HttpEndpoint     = $eventsHttp
            RealtimeEndpoint = $eventsRealtime
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
        $publishArguments = @($portalProject, '-c', 'Release', '-o', $publishDirectory, '--nologo')
        if ($Aot) { $publishArguments += @('-p:RunAOTCompilation=true', '-p:WasmEnableSIMD=true') }
        & dotnet publish @publishArguments
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)" }
    }
    finally {
        Remove-Item -Force $productionSettingsPath -ErrorAction SilentlyContinue
    }

    $wwwroot = Join-Path $publishDirectory 'wwwroot'
    Add-ContentSecurityPolicy -WwwRoot $wwwroot -Region $outputs['Region'] -DataBucketName $outputs['DataBucketName'] -EventsHttpEndpoint $eventsHttp -EventsRealtimeEndpoint $eventsRealtime
    Write-Ok "Content-Security-Policy added to index.html"

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
            # Drop keys pressed during the (long) deployment so they are not read as the password.
            try { $Host.UI.RawUI.FlushInputBuffer() } catch { Write-Verbose "Could not flush the input buffer: $_" }
            $TestUserPassword = Read-Host "    Password for $TestUserEmail (min 12 chars, upper, lower, number)" -AsSecureString
        }

        # Check against the user pool's password policy (see stack.yaml) before creating the user,
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

        # Through a file only the current user can read, not the command line, so the password is not in process listings.
        $passwordFile = $null
        try {
            $passwordFile = New-TempJsonFile @{
                UserPoolId = $userPoolId
                Username   = $TestUserEmail
                Password   = (ConvertTo-PlainText $TestUserPassword)
                Permanent  = $true
            }
            Invoke-Aws -Arguments @('cognito-idp', 'admin-set-user-password', '--cli-input-json', "file://$passwordFile") | Out-Null
        }
        finally {
            Remove-TempFile $passwordFile
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
