<#
.SYNOPSIS
Checks the framework packages and the dotnet new template work together, as a user of them would see them.

.DESCRIPTION
  1. Builds the repository and runs its tests (unless -SkipRepositoryTests).
  2. Packs the framework packages (Devoctomy.Blazwiz.*) to a local feed, with a version unique to this run so the NuGet
     cache never serves an older build of them.
  3. Packs the template and installs it into an isolated location (your installed templates are not touched).
  4. Creates apps with awkward names and different options, and checks each one: no template placeholder (MyApp) left
     in it, a new app ID, a valid CloudFormation AppName, it builds against the packages from the local feed (not the
     framework's source), and its tests pass.
Exits with code 1 if anything fails. Output goes to a temporary folder, deleted afterwards unless -KeepOutput.

.EXAMPLE
./templatepack/test-template.ps1

.EXAMPLE
./templatepack/test-template.ps1 -SkipUiTests -SkipRepositoryTests -KeepOutput
#>
[CmdletBinding()]
param(
    # Skip the Playwright UI tests (they need the Chromium browser installed, see README).
    [switch]$SkipUiTests,

    # Skip building and testing the repository itself.
    [switch]$SkipRepositoryTests,

    # Keep the generated apps and packages for inspection.
    [switch]$KeepOutput,

    # Only create these apps (by name, e.g. -Only NoNotes), not all of them.
    [string[]]$Only
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$templateRoot = Join-Path $repositoryRoot 'template'
$runId = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
$workRoot = Join-Path ([IO.Path]::GetTempPath()) "blazwiz-template-test-$runId"
$script:Failures = New-Object System.Collections.Generic.List[string]

# Name, extra options, and whether the generated app includes tests.
$cases = @(
    @{ Name = 'AcmePortal'; Options = @('--Title', 'Acme Portal', '--Description', 'Welcome to Acme.'); Tests = $true; UiTests = $true },
    @{ Name = 'my-site'; Options = @(); Tests = $true; UiTests = $false },
    @{ Name = '1stApp'; Options = @('--IncludeShowcase', 'false'); Tests = $true; UiTests = $false },
    @{ Name = 'NoNotes'; Options = @('--Notes', 'false'); Tests = $true; UiTests = $true },
    @{ Name = 'NoSync'; Options = @('--Sync', 'false'); Tests = $true; UiTests = $true },
    @{ Name = 'Minimal'; Options = @('--IncludeTests', 'false', '--IncludeShowcase', 'false', '--Sync', 'false'); Tests = $false; UiTests = $false }
)

if ($Only) {
    $cases = @($cases | Where-Object { $Only -contains $_.Name })
}

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked([string]$Description, [scriptblock]$Command) {
    # The command's output goes to the console, not into this function's result.
    & $Command | Out-Host
    if ($LASTEXITCODE -ne 0) {
        $script:Failures.Add($Description)
        Write-Host "    [FAIL] $Description (exit code $LASTEXITCODE)" -ForegroundColor Red
        return $false
    }

    Write-Host "    [PASS] $Description" -ForegroundColor Green
    return $true
}

function Test-Project([string]$Root, [string]$Solution, [bool]$UiTests, [string]$Label, [string[]]$BuildArguments) {
    if (-not (Invoke-Checked "$Label builds" { dotnet build (Join-Path $Root $Solution) --nologo -v q -m:1 @BuildArguments })) {
        return
    }

    $testProjects = Get-ChildItem -Path $Root -Filter '*Tests.csproj' -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Where-Object { $UiTests -or $_.BaseName -notlike '*.UITests' }
    foreach ($project in $testProjects) {
        Invoke-Checked "$Label $($project.BaseName) pass" { dotnet test $project.FullName --nologo --no-build -v q @BuildArguments } | Out-Null
    }
}

try {
    New-Item -ItemType Directory -Path $workRoot | Out-Null

    if (-not $SkipRepositoryTests) {
        Write-Step 'Repository builds and passes its tests'
        Test-Project -Root $repositoryRoot -Solution 'blazwiz.slnx' -UiTests (-not $SkipUiTests) -Label 'repository' -BuildArguments @()
    }

    # A version unique to this run: the NuGet cache keeps a package once it has a version, so reusing one would test an
    # older build of the framework.
    $versionPrefix = ([xml](Get-Content (Join-Path $repositoryRoot 'Directory.Build.props'))).Project.PropertyGroup.VersionPrefix | Where-Object { $_ } | Select-Object -First 1
    $versionSuffix = "test.$runId"
    $version = "$versionPrefix-$versionSuffix"
    $feed = Join-Path $workRoot 'feed'

    Write-Step "Packing the framework packages ($version)"
    if (-not (Invoke-Checked 'framework packs' { dotnet pack (Join-Path $repositoryRoot 'blazwiz.slnx') -c Release -o $feed --nologo -v q -m:1 -p:VersionSuffix=$versionSuffix })) {
        throw 'Could not pack the framework.'
    }

    Write-Step 'Packing and installing the template (isolated)'
    $hive = Join-Path $workRoot 'hive'
    if (-not (Invoke-Checked 'template packs' { dotnet pack (Join-Path $PSScriptRoot 'Devoctomy.Blazwiz.Templates.csproj') -c Release -o $feed --nologo -v q -p:VersionSuffix=$versionSuffix })) {
        throw 'Could not pack the template.'
    }

    $package = Get-ChildItem -Path $feed -Filter 'Devoctomy.Blazwiz.Templates.*.nupkg' | Select-Object -First 1
    if (-not (Invoke-Checked 'template installs' { dotnet new install $package.FullName --debug:custom-hive $hive })) {
        throw 'Could not install the template.'
    }

    $templateAppId = [regex]::Match((Get-Content (Join-Path $templateRoot 'MyApp.Portal.Application/AppIdentity.cs') -Raw), 'Id = "([^"]+)"').Groups[1].Value

    foreach ($case in $cases) {
        $name = $case.Name
        Write-Step "App '$name' $($case.Options -join ' ')"
        $output = Join-Path $workRoot "apps/$name"

        if (-not (Invoke-Checked "'$name' is created" { dotnet new blazwiz -n $name -o $output @($case.Options) --debug:custom-hive $hive })) {
            continue
        }

        # Use the packages from the local feed, at this run's version, as an app upgrading to them would: the version in
        # Directory.Packages.props, and the feed in nuget.config (as well as nuget.org).
        $packagesProps = Join-Path $output 'Directory.Packages.props'
        (Get-Content $packagesProps -Raw) -replace '<BlazwizVersion>[^<]*</BlazwizVersion>', "<BlazwizVersion>$version</BlazwizVersion>" |
            Set-Content -Path $packagesProps -NoNewline
        @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="blazwiz-test" value="$feed" />
  </packageSources>
</configuration>
"@ | Set-Content -Path (Join-Path $output 'nuget.config')

        # Nothing should still have the template's placeholder name (ignoring build output).
        $leftovers = Get-ChildItem -Path $output -Recurse -File |
            Where-Object { $_.FullName.Substring($output.Length) -notmatch '[\\/](bin|obj)[\\/]' } |
            Where-Object { $_.FullName.Substring($output.Length) -match 'MyApp' -or (Select-String -Path $_.FullName -Pattern 'MyApp|myapp|MYAPP' -CaseSensitive -Quiet) }
        if ($leftovers) {
            $script:Failures.Add("'$name' still contains 'MyApp'")
            Write-Host "    [FAIL] still contains 'MyApp':" -ForegroundColor Red
            $leftovers | ForEach-Object { Write-Host "           $($_.FullName.Substring($output.Length + 1))" }
        }
        else {
            Write-Host "    [PASS] no 'MyApp' left" -ForegroundColor Green
        }

        # The app builds against the packages, not the framework's source.
        if (Test-Path (Join-Path $output 'Directory.Build.targets')) {
            $script:Failures.Add("'$name' has the repository's Directory.Build.targets")
            Write-Host "    [FAIL] has the repository's Directory.Build.targets" -ForegroundColor Red
        }

        # Each app gets its own ID (the 'guids' in template.json), so its WebAuthn derived keys differ from every other app's.
        $appIdentityFile = Get-ChildItem -Path $output -Filter 'AppIdentity.cs' -Recurse | Select-Object -First 1
        $appId = [regex]::Match((Get-Content $appIdentityFile.FullName -Raw), 'Id = "([^"]+)"').Groups[1].Value
        $parsedAppId = [Guid]::Empty
        if ($templateAppId -and [Guid]::TryParse($appId, [ref]$parsedAppId) -and $parsedAppId -ne [Guid]::Empty -and $appId -ne $templateAppId) {
            Write-Host "    [PASS] app ID '$appId' is new" -ForegroundColor Green
        }
        else {
            $script:Failures.Add("'$name' app ID '$appId' is not a new GUID (template's is '$templateAppId')")
            Write-Host "    [FAIL] app ID '$appId' is not a new GUID (template's is '$templateAppId')" -ForegroundColor Red
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

        Test-Project -Root $output -Solution $solution -UiTests ($case.UiTests -and -not $SkipUiTests) -Label "'$name'" -BuildArguments @()
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
