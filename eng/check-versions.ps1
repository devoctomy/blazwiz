<#
.SYNOPSIS
Checks the framework's version is the same everywhere it is written, and optionally matches a release tag.

.DESCRIPTION
Every Devoctomy.Blazwiz package and the template share one version (docs/versioning.md). It is written in two places:
VersionPrefix and VersionSuffix in Directory.Build.props (the packages), and BlazwizVersion in
template/Directory.Packages.props (what new apps use). Exits with code 1 if they differ, or if -Tag is given and is
not "v" followed by that version.

.EXAMPLE
./eng/check-versions.ps1

.EXAMPLE
./eng/check-versions.ps1 -Tag v0.1.0-preview.1
#>
[CmdletBinding()]
param(
    # The release tag, e.g. v1.2.0.
    [string]$Tag
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$props = [xml](Get-Content (Join-Path $root 'Directory.Build.props'))
$prefix = $props.Project.PropertyGroup.VersionPrefix | Where-Object { $_ } | Select-Object -First 1
$suffix = $props.Project.PropertyGroup.VersionSuffix | Where-Object { $_ } | Select-Object -First 1
$version = if ($suffix) { "$prefix-$suffix" } else { $prefix }

$templateProps = [xml](Get-Content (Join-Path $root 'template/Directory.Packages.props'))
$templateVersion = $templateProps.Project.PropertyGroup.BlazwizVersion | Where-Object { $_ } | Select-Object -First 1

$failed = $false
Write-Host "Packages (Directory.Build.props): $version"
Write-Host "Template (template/Directory.Packages.props BlazwizVersion): $templateVersion"
if ($templateVersion -ne $version) {
    Write-Host "The template's BlazwizVersion must be $version." -ForegroundColor Red
    $failed = $true
}

if ($Tag) {
    Write-Host "Tag: $Tag"
    if ($Tag -ne "v$version") {
        Write-Host "The tag must be v$version." -ForegroundColor Red
        $failed = $true
    }
}

if ($failed) {
    exit 1
}

Write-Host 'Versions match.' -ForegroundColor Green
