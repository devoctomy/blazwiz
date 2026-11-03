<#
.SYNOPSIS
Checks a deployed myapp environment end to end: the portal, sign in, AWS credentials and per-user S3 access.

.DESCRIPTION
Runs test-auth.ps1 from the blazwiz base stack released with the framework (Devoctomy.Blazwiz.Aws, at the app's
BlazwizVersion), for this app. Takes the same parameters, e.g. -Region, -Environment, -Email; see that script's help
(Get-Help ./cloudformation/test-auth.ps1 -Full, after finding it with ./cloudformation/base-stack.ps1).
#>
param(
    # Short lowercase app name (letters and numbers), used to name resources.
    [string]$AppName = 'myapp'
)

$ErrorActionPreference = 'Stop'
$base = & (Join-Path $PSScriptRoot 'base-stack.ps1')
& (Join-Path $base 'test-auth.ps1') -AppName $AppName @args
