<#
.SYNOPSIS
Deletes a blazwiz environment: empties both buckets and deletes the CloudFormation stack.

.DESCRIPTION
This permanently deletes the portal, all per-user data and all Cognito users for the environment.
You will be asked to type the stack name to confirm unless -Force is given.

.EXAMPLE
./cloudformation/remove.ps1 -Region eu-west-2 -Environment dev
#>
[CmdletBinding()]
param(
    [string]$AppName = 'blazwiz',
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
    Write-Host "    the portal, every object in $($outputs['DataBucketName']) and all Cognito users." -ForegroundColor Yellow
    $answer = Read-Host "    Type the stack name ($stackName) to confirm"
    if ($answer -ne $stackName) {
        Write-Host '    Cancelled, nothing was changed.'
        exit 1
    }
}

Write-Step "Emptying buckets"
foreach ($bucket in $outputs['PortalBucketName'], $outputs['DataBucketName']) {
    if ($bucket) {
        Invoke-Aws -AllowFailure -Arguments @('s3', 'rm', "s3://$bucket", '--recursive', '--only-show-errors') | Out-Null
        if ($script:LastAwsError -and $script:LastAwsError -notmatch 'NoSuchBucket') {
            throw "Could not empty $bucket`: $script:LastAwsError"
        }
        Write-Ok "Emptied $bucket"
    }
}

Write-Step "Deleting stack '$stackName' (takes a few minutes, mostly CloudFront)"
Invoke-Aws -Arguments @('cloudformation', 'delete-stack', '--stack-name', $stackName) | Out-Null
Invoke-Aws -Arguments @('cloudformation', 'wait', 'stack-delete-complete', '--stack-name', $stackName) | Out-Null
Write-Ok "Stack deleted"
