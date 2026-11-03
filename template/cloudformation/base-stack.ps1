<#
.SYNOPSIS
Finds the blazwiz base stack and scripts for this app: the Devoctomy.Blazwiz.Aws package at the app's BlazwizVersion
(restored with the app), or the framework's source when the app is inside a blazwiz repository checkout. Prints the
folder.
#>
$ErrorActionPreference = 'Stop'
$appRoot = Split-Path -Parent $PSScriptRoot

$source = Join-Path $appRoot '../src/Devoctomy.Blazwiz.Aws/cloudformation'
if (Test-Path (Join-Path $source 'stack.yaml')) {
    return (Resolve-Path $source).Path
}

$version = ([xml](Get-Content (Join-Path $appRoot 'Directory.Packages.props'))).Project.PropertyGroup.BlazwizVersion | Where-Object { $_ } | Select-Object -First 1
$packages = ((dotnet nuget locals global-packages --list) -replace '^global-packages:\s*', '').Trim()
$folder = Join-Path $packages "devoctomy.blazwiz.aws/$($version.ToLowerInvariant())/tools/cloudformation"
if (-not (Test-Path (Join-Path $folder 'stack.yaml'))) {
    dotnet restore $appRoot --nologo -v q | Out-Host
}

if (-not (Test-Path (Join-Path $folder 'stack.yaml'))) {
    throw "Could not find the blazwiz base stack ($folder). Restore the app and check BlazwizVersion in Directory.Packages.props."
}

return $folder
