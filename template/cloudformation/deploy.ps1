<#
.SYNOPSIS
Deploys myapp to AWS: the blazwiz base stack, this app's own stack (app.yaml), the portal and a test user.

.DESCRIPTION
Runs deploy.ps1 from the blazwiz base stack released with the framework (Devoctomy.Blazwiz.Aws, at the app's
BlazwizVersion), for this app. Takes the same parameters, e.g. -Region, -Environment, -TestUserEmail, -SkipPortal, -Force; see that script's help
(Get-Help ./cloudformation/deploy.ps1 -Full, after finding it with ./cloudformation/base-stack.ps1).
#>
param(
    # Short lowercase app name (letters and numbers), used to name resources.
    [string]$AppName = 'myapp'
)

$ErrorActionPreference = 'Stop'
$base = & (Join-Path $PSScriptRoot 'base-stack.ps1')
& (Join-Path $base 'deploy.ps1') -AppName $AppName -AppRoot (Split-Path -Parent $PSScriptRoot) -PortalProject 'MyApp.Portal/MyApp.Portal.csproj' -AppStackTemplate (Join-Path $PSScriptRoot 'app.yaml') @args
