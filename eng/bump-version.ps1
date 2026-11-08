<#
.SYNOPSIS
Sets the version of the next release, and gets everything that goes with it ready.

.DESCRIPTION
Run on a branch before releasing; then push, merge to main and run the Release workflow (docs/releasing.md).

The version is set in one place, VersionPrefix in Directory.Build.props: every package and the template take it from
there. The script:

  1. Sets VersionPrefix to the new version: -Version, or the next minor version (0.1.0 -> 0.2.0).
  2. Turns the Unreleased section of CHANGELOG.md into the new version's section, and starts an empty Unreleased.
  3. Moves each package's public API from PublicAPI.Unshipped.txt to PublicAPI.Shipped.txt (applying *REMOVED* lines).
Package validation needs nothing here: packing finds the latest release itself (eng/get-validation-baseline.ps1).

If the current version has not been released (it is not on nuget.org as a plain version; betas do not count), no bump
is needed, and the script offers to add the Unreleased changes to it instead: the version stays, the Unreleased notes
join its changelog section, and the public API is moved. Answer n to bump anyway. -Version always bumps.

It only changes files; review them with git diff, then commit.

.EXAMPLE
./eng/bump-version.ps1

.EXAMPLE
./eng/bump-version.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    # The new version, MAJOR.MINOR.PATCH. Defaults to the next minor version.
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Read-Text([string]$Path) {
    return [System.IO.File]::ReadAllText($Path)
}

# Writes text with the line endings the file already had (CRLF or LF), as UTF-8 without a byte order mark.
function Write-Text([string]$Path, [string]$Text, [string]$Original) {
    $newline = if ($Original -match "`r`n") { "`r`n" } else { "`n" }
    $Text = ($Text -replace "`r`n", "`n") -replace "`n", $newline
    [System.IO.File]::WriteAllText($Path, $Text, $utf8)
}

function ConvertTo-Version([string]$Text, [string]$What) {
    if ($Text -notmatch '^\d+\.\d+\.\d+$') {
        throw "$What must be MAJOR.MINOR.PATCH, e.g. 1.2.0 (no suffix: betas are published from branches). Got '$Text'."
    }

    return [version]$Text
}

# 1. The version.
$propsPath = Join-Path $root 'Directory.Build.props'
$props = Read-Text $propsPath
$match = [regex]::Match($props, '<VersionPrefix>([^<]*)</VersionPrefix>')
if (-not $match.Success) {
    throw 'Directory.Build.props has no VersionPrefix.'
}

$current = ConvertTo-Version $match.Groups[1].Value 'VersionPrefix in Directory.Build.props'

# A version is released once it is on nuget.org as a plain version (betas are <version>-beta.<run>).
function Test-Released([version]$Version) {
    try {
        $index = Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/devoctomy.blazwiz.core/index.json'
    }
    catch {
        throw "Could not read nuget.org to see whether $Version has been released ($($_.Exception.Message)). Try again, or pass -Version to bump regardless."
    }

    return @($index.versions) -contains "$Version"
}

$addToCurrent = $false
if ($Version) {
    $next = ConvertTo-Version $Version '-Version'
}
elseif (-not (Test-Released $current)) {
    Write-Host "No bump needed: $current has not been released yet."
    $answer = Read-Host "Add the Unreleased changes to $current instead of bumping? [Y/n]"
    if (-not $answer -or $answer -match '^[Yy]') {
        $addToCurrent = $true
        $next = $current
    }
    else {
        $next = New-Object System.Version($current.Major, ($current.Minor + 1), 0)
    }
}
else {
    $next = New-Object System.Version($current.Major, ($current.Minor + 1), 0)
}

if (-not $addToCurrent -and $next -le $current) {
    throw "The new version ($next) must be higher than the current one ($current)."
}

# 2. The changelog. Checked before anything is written, so a failure leaves every file as it was.
$changelogPath = Join-Path $root 'CHANGELOG.md'
$changelog = Read-Text $changelogPath
$unreleased = [regex]::Match($changelog, '(?m)^## Unreleased[^\r\n]*\r?\n')
if (-not $unreleased.Success) {
    throw 'CHANGELOG.md has no "## Unreleased" section to release.'
}

$versionHeading = [regex]::Match($changelog, "(?m)^## $([regex]::Escape("$next"))[^\r\n]*\r?\n")
if ($versionHeading.Success -and -not $addToCurrent) {
    throw "CHANGELOG.md already has a section for $next."
}

if ($addToCurrent -and -not $versionHeading.Success) {
    throw "CHANGELOG.md has no section for $current to add the Unreleased changes to."
}

# The body of the section whose heading ends at $Start: up to the next "## " heading or the end.
function Get-SectionBody([string]$Text, [int]$Start) {
    $following = [regex]::Match($Text.Substring($Start), '(?m)^## ')
    return $(if ($following.Success) { $Text.Substring($Start, $following.Index) } else { $Text.Substring($Start) })
}

$sectionStart = $unreleased.Index + $unreleased.Length
$sectionBody = Get-SectionBody $changelog $sectionStart
if (-not $sectionBody.Trim() -and -not $addToCurrent) {
    Write-Warning 'The Unreleased section of CHANGELOG.md is empty: the release will have no notes. Add them before releasing.'
}

if ($addToCurrent) {
    # Each "### Added" etc. of Unreleased joins the same heading of the version's section, or is added at its end.
    $versionStart = $versionHeading.Index + $versionHeading.Length
    $versionBody = Get-SectionBody $changelog $versionStart
    $merged = $versionBody.TrimEnd() + "`n"
    $parts = [regex]::Split($sectionBody.Trim(), '(?m)^(?=### )') | Where-Object { $_.Trim() }
    foreach ($part in $parts) {
        $heading = [regex]::Match($part, '^### [^\r\n]*').Value
        $items = $part.Substring($heading.Length).Trim()
        $existing = if ($heading) { [regex]::Match($merged, "(?m)^$([regex]::Escape($heading))[^\r\n]*\r?\n") } else { $null }
        if ($existing -and $existing.Success) {
            $after = $existing.Index + $existing.Length
            $body = [regex]::Match($merged.Substring($after), '(?m)^### |\z')
            $merged = $merged.Substring(0, $after + $body.Index).TrimEnd() + "`n" + $items + "`n`n" + $merged.Substring($after + $body.Index)
        }
        else {
            $merged = $merged.TrimEnd() + "`n`n" + $part.Trim() + "`n"
        }
    }

    $changelogText = $changelog.Substring(0, $sectionStart) + "`n" + $changelog.Substring($sectionStart + $sectionBody.Length, $versionStart - $sectionStart - $sectionBody.Length) + $merged.TrimEnd() + "`n`n" + $changelog.Substring($versionStart + $versionBody.Length)
    Write-Text $changelogPath $changelogText $changelog
    Write-Host "Version: $current (unchanged)"
    Write-Host "CHANGELOG.md: the Unreleased changes are now under $current."
}
else {
    Write-Text $propsPath ($props.Substring(0, $match.Index) + "<VersionPrefix>$next</VersionPrefix>" + $props.Substring($match.Index + $match.Length)) $props
    Write-Host "Version: $current -> $next"

    $changelogText = $changelog.Substring(0, $unreleased.Index) + "## Unreleased`n`n## $next`n" + $changelog.Substring($sectionStart)
    Write-Text $changelogPath $changelogText $changelog
    Write-Host "CHANGELOG.md: Unreleased is now $next."
}

# 3. The public API.
foreach ($package in Get-ChildItem -Path (Join-Path $root 'src') -Directory) {
    $unshippedPath = Join-Path $package.FullName 'PublicAPI.Unshipped.txt'
    $shippedPath = Join-Path $package.FullName 'PublicAPI.Shipped.txt'
    if (-not (Test-Path $unshippedPath)) {
        continue
    }

    $unshippedText = Read-Text $unshippedPath
    $shippedText = if (Test-Path $shippedPath) { Read-Text $shippedPath } else { '' }
    $header = '#nullable enable'
    $lines = @($unshippedText -split "\r?\n" | Where-Object { $_ -and $_ -ne $header })
    if ($lines.Count -eq 0) {
        continue
    }

    $shipped = New-Object 'System.Collections.Generic.List[string]'
    $shippedText -split "\r?\n" | Where-Object { $_ -and $_ -ne $header } | ForEach-Object { $shipped.Add($_) }
    foreach ($line in $lines) {
        if ($line.StartsWith('*REMOVED*')) {
            [void]$shipped.Remove($line.Substring('*REMOVED*'.Length))
        }
        else {
            $shipped.Add($line)
        }
    }

    $sorted = [string[]]$shipped.ToArray()
    [System.Array]::Sort($sorted, [System.StringComparer]::Ordinal)
    $original = if ($shippedText) { $shippedText } else { $unshippedText }
    Write-Text $shippedPath ((@($header) + $sorted) -join "`n") $original
    Write-Text $unshippedPath $header $unshippedText
    Write-Host "$($package.Name): $($lines.Count) public API lines shipped."
}

Write-Host ''
Write-Host "Ready for $next. Review the changes (git diff), commit, push, merge to main, then run the Release workflow on main."
