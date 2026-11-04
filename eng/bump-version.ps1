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
if ($Version) {
    $next = ConvertTo-Version $Version '-Version'
}
else {
    $next = New-Object System.Version($current.Major, ($current.Minor + 1), 0)
}

if ($next -le $current) {
    throw "The new version ($next) must be higher than the current one ($current)."
}

# 2. The changelog. Checked before anything is written, so a failure leaves every file as it was.
$changelogPath = Join-Path $root 'CHANGELOG.md'
$changelog = Read-Text $changelogPath
$unreleased = [regex]::Match($changelog, '(?m)^## Unreleased[^\r\n]*\r?\n')
if (-not $unreleased.Success) {
    throw 'CHANGELOG.md has no "## Unreleased" section to release.'
}

if ([regex]::IsMatch($changelog, "(?m)^## $([regex]::Escape("$next"))\s*$")) {
    throw "CHANGELOG.md already has a section for $next."
}

$sectionStart = $unreleased.Index + $unreleased.Length
$nextSection = [regex]::Match($changelog.Substring($sectionStart), '(?m)^## ')
$sectionBody = if ($nextSection.Success) { $changelog.Substring($sectionStart, $nextSection.Index) } else { $changelog.Substring($sectionStart) }
if (-not $sectionBody.Trim()) {
    Write-Warning 'The Unreleased section of CHANGELOG.md is empty: the release will have no notes. Add them before releasing.'
}

Write-Text $propsPath ($props.Substring(0, $match.Index) + "<VersionPrefix>$next</VersionPrefix>" + $props.Substring($match.Index + $match.Length)) $props
Write-Host "Version: $current -> $next"

$changelogText = $changelog.Substring(0, $unreleased.Index) + "## Unreleased`n`n## $next`n" + $changelog.Substring($sectionStart)
Write-Text $changelogPath $changelogText $changelog
Write-Host "CHANGELOG.md: Unreleased is now $next."

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
