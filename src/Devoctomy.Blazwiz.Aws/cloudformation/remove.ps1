<#
.SYNOPSIS
Deletes a blazwiz app's environment: the app's own stack, the users' keys secrets, then the base stack's buckets
(emptied) and the base stack.

.DESCRIPTION
This permanently deletes the portal, all per-user data (the data bucket, every version in it, and every user's keys
secrets in Secrets Manager), the audit log if there is one, and all Cognito users for the environment. If the user
pool is protected from deletion (UserPoolDeletionProtection, on for prod), the protection is turned off first.
You will be asked to type the stack name to confirm unless -Force is given.

.EXAMPLE
./cloudformation/remove.ps1 -Region eu-west-2 -Environment dev
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$AppName,
    [string]$Environment = 'dev',
    [string]$Region,
    [string]$AwsProfile,

    # Do not ask for confirmation.
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$stackName = Get-StackName $AppName $Environment

Write-Step "Checking AWS access"
$identity = Initialize-AwsContext -Region $Region -AwsProfile $AwsProfile

$outputs = Get-StackOutputs $stackName
if ($null -eq $outputs) {
    Write-Ok "Stack '$stackName' does not exist in $script:AwsRegion, nothing to do."
    exit 0
}

if (-not $Force) {
    Write-Host ''
    Write-Host "    This PERMANENTLY deletes stack '$stackName' in account $($identity.Account) ($script:AwsRegion):" -ForegroundColor Yellow
    Write-Host "    the portal, every object in $($outputs['DataBucketName']), every user's keys secrets, and all Cognito users." -ForegroundColor Yellow
    $answer = Read-Host "    Type the stack name ($stackName) to confirm"
    if ($answer -ne $stackName) {
        Write-Host '    Cancelled, nothing was changed.'
        exit 1
    }
}

# The app's own stack imports the base stack's outputs, so it goes first.
$appStackName = "$stackName-app"
$appStack = Invoke-Aws -Json -AllowFailure -Arguments @('cloudformation', 'describe-stacks', '--stack-name', $appStackName)
if ($null -ne $appStack) {
    Write-Step "Deleting the app's stack '$appStackName'"
    Invoke-Aws -Arguments @('cloudformation', 'delete-stack', '--stack-name', $appStackName) | Out-Null
    Invoke-Aws -Arguments @('cloudformation', 'wait', 'stack-delete-complete', '--stack-name', $appStackName) | Out-Null
    Write-Ok "App stack deleted"
}

# The users' keys secrets are created by the portal, not the stack, so deleting the stack would leave them behind.
# Each is tagged with the app's name, which the deployer's policy requires to delete it.
if ($outputs['UserPoolId']) {
    Write-Step "Deleting the users' keys secrets"
    $deleted = 0
    $failed = 0
    $token = $null
    do {
        $listArguments = @('cognito-idp', 'list-users', '--user-pool-id', $outputs['UserPoolId'], '--attributes-to-get', 'sub')
        if ($token) { $listArguments += @('--pagination-token', $token) }
        $page = Invoke-Aws -Json -AllowFailure -Arguments $listArguments
        if ($null -eq $page) {
            Write-Warn "Could not list the users (their secrets are left as they are): $script:LastAwsError"
            break
        }

        foreach ($user in @($page.Users)) {
            $sub = ($user.Attributes | Where-Object { $_.Name -eq 'sub' } | Select-Object -First 1).Value
            if (-not $sub) { continue }
            $secrets = Invoke-Aws -Json -AllowFailure -Arguments @('secretsmanager', 'list-secrets', '--filters', "Key=name,Values=$sub/keys/", '--include-planned-deletion')
            foreach ($secret in @($secrets.SecretList)) {
                Invoke-Aws -AllowFailure -Arguments @('secretsmanager', 'delete-secret', '--secret-id', $secret.ARN, '--force-delete-without-recovery') | Out-Null
                if ($script:LastAwsError) {
                    $failed++
                    Write-Warn "Could not delete $($secret.Name): $script:LastAwsError"
                }
                else {
                    $deleted++
                }
            }
        }

        $token = Get-OptionalProperty $page 'PaginationToken'
    } while ($token)
    Write-Ok "$deleted secret(s) deleted$(if ($failed) { ", $failed left (untagged secrets from before this version must be deleted by hand)" })"
}

Write-Step "Emptying buckets"
foreach ($bucket in $outputs['PortalBucketName'], $outputs['DataBucketName'], $outputs['AuditLogBucketName']) {
    if ($bucket) {
        # Every version and delete marker too: the data bucket is versioned, so 's3 rm' alone would leave them.
        $versions = Invoke-Aws -Json -AllowFailure -Arguments @('s3api', 'list-object-versions', '--bucket', $bucket, '--query', '{Objects: [Versions[].{Key: Key, VersionId: VersionId}, DeleteMarkers[].{Key: Key, VersionId: VersionId}][] }')
        if ($null -eq $versions) {
            if ($script:LastAwsError -notmatch 'NoSuchBucket') {
                throw "Could not list $bucket`: $script:LastAwsError"
            }
            continue
        }

        $objects = @($versions.Objects | Where-Object { $null -ne $_ })
        while ($objects.Count -gt 0) {
            # delete-objects takes up to 1000 keys at a time.
            $batch = @($objects | Select-Object -First 1000)
            $objects = @($objects | Select-Object -Skip 1000)
            $deleteFile = New-TempJsonFile @{ Objects = $batch; Quiet = $true }
            try {
                Invoke-Aws -Arguments @('s3api', 'delete-objects', '--bucket', $bucket, '--delete', "file://$deleteFile") | Out-Null
            }
            finally {
                Remove-Item -Force $deleteFile -ErrorAction SilentlyContinue
            }
        }
        Write-Ok "Emptied $bucket"
    }
}

# Cognito refuses to delete a protected user pool, so the protection comes off first (an update of the stack: its
# policy only blocks replacing or removing the pool, not changing it).
$pool = Invoke-Aws -Json -AllowFailure -Arguments @('cognito-idp', 'describe-user-pool', '--user-pool-id', $outputs['UserPoolId'])
if ($null -ne $pool -and (Get-OptionalProperty $pool.UserPool 'DeletionProtection') -eq 'ACTIVE') {
    Write-Step "Turning the user pool's deletion protection off"
    $parametersFile = New-TempJsonFile @(
        @{ ParameterKey = 'UserPoolDeletionProtection'; ParameterValue = 'INACTIVE' }
        @{ ParameterKey = 'AppName'; UsePreviousValue = $true }
        @{ ParameterKey = 'Environment'; UsePreviousValue = $true }
        @{ ParameterKey = 'CustomDomainName'; UsePreviousValue = $true }
        @{ ParameterKey = 'CertificateArn'; UsePreviousValue = $true }
        @{ ParameterKey = 'LocalDevOrigins'; UsePreviousValue = $true }
        @{ ParameterKey = 'AuditLogging'; UsePreviousValue = $true }
    )
    try {
        Invoke-Aws -Arguments @(
            'cloudformation', 'update-stack',
            '--stack-name', $stackName,
            '--use-previous-template',
            '--capabilities', 'CAPABILITY_IAM',
            '--parameters', "file://$parametersFile"
        ) | Out-Null
        Invoke-Aws -Arguments @('cloudformation', 'wait', 'stack-update-complete', '--stack-name', $stackName) | Out-Null
    }
    finally {
        Remove-Item -Force $parametersFile -ErrorAction SilentlyContinue
    }
    Write-Ok "Deletion protection off"
}

Write-Step "Deleting stack '$stackName' (takes a few minutes, mostly CloudFront)"
Invoke-Aws -Arguments @('cloudformation', 'delete-stack', '--stack-name', $stackName) | Out-Null
Invoke-Aws -Arguments @('cloudformation', 'wait', 'stack-delete-complete', '--stack-name', $stackName) | Out-Null
Write-Ok "Stack deleted"
