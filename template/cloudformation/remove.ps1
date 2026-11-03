<#
.SYNOPSIS
Deletes a myapp environment: this app's own stack, then the base stack and its buckets.

.DESCRIPTION
Runs remove.ps1 from the blazwiz base stack released with the framework (Devoctomy.Blazwiz.Aws, at the app's
BlazwizVersion), for this app. Takes the same parameters, e.g. -Region, -Environment, -Force; see that script's help
(Get-Help ./cloudformation/remove.ps1 -Full, after finding it with ./cloudformation/base-stack.ps1).
#>
param(
    # Short lowercase app name (letters and numbers), used to name resources.
    [string]$AppName = 'myapp'
)

$ErrorActionPreference = 'Stop'
$base = & (Join-Path $PSScriptRoot 'base-stack.ps1')
& (Join-Path $base 'remove.ps1') -AppName $AppName @args
