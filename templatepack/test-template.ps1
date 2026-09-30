<#
.SYNOPSIS
Checks the repository still works as an app and as a dotnet new template.

.DESCRIPTION
  1. Builds the repository as it is and runs its tests (it must stay a working app).
  2. Packs the template and installs it into an isolated location (your installed templates are not touched).
  3. Creates apps with awkward names and different options, and checks each one:
     no 'blazwiz' left in it, a valid CloudFormation AppName, it builds, and its tests pass.
Exits with code 1 if anything fails. Output goes to a temporary folder, deleted afterwards unless -KeepOutput.

.EXAMPLE
./templatepack/test-template.ps1

.EXAMPLE
./templatepack/test-template.ps1 -SkipUiTests -KeepOutput
#>
[CmdletBinding()]
param(
    # Skip the Playwright UI tests (they need the Chromium browser installed, see README).
    [switch]$SkipUiTests,

    # Skip building and testing the repository itself.
    [switch]$SkipRepositoryTests,

    # Keep the generated apps and package for inspection.
    [switch]$KeepOutput
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$workRoot = Join-Path ([IO.Path]::GetTempPath()) "blazwiz-template-test-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$script:Failures = New-Object System.Collections.Generic.List[string]

# Name, extra options, and whether the generated app includes tests.
$cases = @(
    @{ Name = 'AcmePortal'; Options = @('--Title', 'Acme Portal', '--Description', 'Welcome to Acme.'); Tests = $true; UiTests = $true },
    @{ Name = 'my-app'; Options = @(); Tests = $true; UiTests = $false },
    @{ Name = '1stApp'; Options = @('--IncludeShowcase', 'false'); Tests = $true; UiTests = $false },
    @{ Name = 'NoNotes'; Options = @('--IncludeNotes', 'false'); Tests = $true; UiTests = $true },
    @{ Name = 'Minimal'; Options = @('--IncludeTests', 'false', '--IncludeShowcase', 'false', '--IncludeNotes', 'false'); Tests = $false; UiTests = $false }
)

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked([string]$Description, [scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) {
        $script:Failures.Add($Description)
        Write-Host "    [FAIL] $Description (exit code $LASTEXITCODE)" -ForegroundColor Red
        return $false
    }

    Write-Host "    [PASS] $Description" -ForegroundColor Green
    return $true
}

function Test-Project([string]$Root, [string]$Solution, [bool]$UiTests, [string]$Label) {
    if (-not (Invoke-Checked "$Label builds" { dotnet build (Join-Path $Root $Solution) --nologo -v q })) {
        return
    }

    $testProjects = Get-ChildItem -Path $Root -Filter '*Tests.csproj' -Recurse |
        Where-Object { $UiTests -or $_.BaseName -notlike '*.UITests' }
    foreach ($project in $testProjects) {
        Invoke-Checked "$Label $($project.BaseName) pass" { dotnet test $project.FullName --nologo --no-build -v q } | Out-Null
    }
}

try {
    New-Item -ItemType Directory -Path $workRoot | Out-Null

    if (-not $SkipRepositoryTests) {
        Write-Step 'Repository builds and passes its tests as it is'
        Test-Project -Root $repositoryRoot -Solution 'blazwiz.slnx' -UiTests (-not $SkipUiTests) -Label 'repository'
    }

    Write-Step 'Packing and installing the template (isolated)'
    $packageDirectory = Join-Path $workRoot 'package'
    $hive = Join-Path $workRoot 'hive'
    if (-not (Invoke-Checked 'template packs' { dotnet pack (Join-Path $PSScriptRoot 'Devoctomy.Blazwiz.Templates.csproj') -c Release -o $packageDirectory --nologo -v q })) {
        throw 'Could not pack the template.'
    }

    $package = Get-ChildItem -Path $packageDirectory -Filter '*.nupkg' | Select-Object -First 1
    if (-not (Invoke-Checked 'template installs' { dotnet new install $package.FullName --debug:custom-hive $hive })) {
        throw 'Could not install the template.'
    }

    foreach ($case in $cases) {
        $name = $case.Name
        Write-Step "App '$name' $($case.Options -join ' ')"
        $output = Join-Path $workRoot "apps/$name"

        if (-not (Invoke-Checked "'$name' is created" { dotnet new blazwiz -n $name -o $output @($case.Options) --debug:custom-hive $hive })) {
            continue
        }

        # Nothing should still be called blazwiz (ignoring restore output in obj/, which records this script's temp path).
        $leftovers = Get-ChildItem -Path $output -Recurse -File |
            Where-Object { $_.FullName.Substring($output.Length) -notmatch '[\\/](bin|obj)[\\/]' } |
            Where-Object { $_.FullName.Substring($output.Length) -match 'blazwiz' -or (Select-String -Path $_.FullName -Pattern 'blazwiz' -SimpleMatch -Quiet) }
        if ($leftovers) {
            $script:Failures.Add("'$name' still contains 'blazwiz'")
            Write-Host "    [FAIL] still contains 'blazwiz':" -ForegroundColor Red
            $leftovers | ForEach-Object { Write-Host "           $($_.FullName.Substring($output.Length + 1))" }
        }
        else {
            Write-Host "    [PASS] no 'blazwiz' left" -ForegroundColor Green
        }

        # The CloudFormation AppName must match the template's AllowedPattern.
        $deployScript = Get-Content (Join-Path $output 'cloudformation/deploy.ps1') -Raw
        $appNameMatch = [regex]::Match($deployScript, '\[string\]\$AppName = ''([^'']*)''')
        $appName = $appNameMatch.Groups[1].Value
        if ($appNameMatch.Success -and $appName -cmatch '^[a-z][a-z0-9]{1,20}$') {
            Write-Host "    [PASS] CloudFormation AppName '$appName' is valid" -ForegroundColor Green
        }
        else {
            $script:Failures.Add("'$name' has an invalid CloudFormation AppName '$appName'")
            Write-Host "    [FAIL] invalid CloudFormation AppName '$appName'" -ForegroundColor Red
        }

        $solution = (Get-ChildItem -Path $output -Filter '*.slnx' | Select-Object -First 1).Name
        $hasTestProjects = [bool](Get-ChildItem -Path $output -Filter '*Tests.csproj' -Recurse)
        if ($hasTestProjects -ne $case.Tests) {
            $script:Failures.Add("'$name' test projects present: $hasTestProjects, expected $($case.Tests)")
            Write-Host "    [FAIL] test projects present: $hasTestProjects, expected $($case.Tests)" -ForegroundColor Red
        }

        Test-Project -Root $output -Solution $solution -UiTests ($case.UiTests -and -not $SkipUiTests) -Label "'$name'"
    }
}
catch {
    $script:Failures.Add($_.Exception.Message)
    Write-Host "    [FAIL] $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    if ($KeepOutput) {
        Write-Host ''
        Write-Host "Output kept in $workRoot"
    }
    else {
        Remove-Item -Recurse -Force $workRoot -ErrorAction SilentlyContinue
    }
}

Write-Host ''
if ($script:Failures.Count -gt 0) {
    Write-Host "$($script:Failures.Count) check(s) failed:" -ForegroundColor Red
    $script:Failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'All checks passed.' -ForegroundColor Green
