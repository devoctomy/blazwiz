<#
.SYNOPSIS
Prints the version packages are validated against: the latest release on nuget.org that is not above the version in
Directory.Build.props, from 1.0 on. Prints nothing before 1.0, when any release may break (docs/versioning.md).

.DESCRIPTION
Pass the result to dotnet pack as -p:PackageValidationBaselineVersion when it is not empty. Package validation then
fails on a binary breaking change against that release unless it is recorded in the package's
CompatibilitySuppressions.xml.

During development the version in Directory.Build.props is the last release, so changes are checked against it. After
eng/bump-version.ps1 it is the next one, not yet released, so they are checked against the release before it.

.EXAMPLE
$baseline = ./eng/get-validation-baseline.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$props = [xml](Get-Content (Join-Path $root 'Directory.Build.props'))
$version = [version]($props.Project.PropertyGroup.VersionPrefix | Where-Object { $_ } | Select-Object -First 1)
if ($version.Major -lt 1) {
    return
}

# Every package is released together, so Core's versions are every package's.
$index = Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/devoctomy.blazwiz.core/index.json'
$released = $index.versions |
    Where-Object { $_ -match '^\d+\.\d+\.\d+$' } |
    ForEach-Object { [version]$_ } |
    Where-Object { $_.Major -ge 1 -and $_ -le $version } |
    Sort-Object |
    Select-Object -Last 1

if ($released) {
    return "$released"
}
