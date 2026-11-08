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
  5. With -UpgradeFrom, simulates an upgrade as a user would do it, for testing a change that affects apps: installs
     the template of a release from nuget.org, creates and customises an app with it, checks it on that release's
     packages, then upgrades it to this run's packages and checks it again. Bumping the package version itself is
     ordinary .NET and needs no test; what matters is the change.
Exits with code 1 if anything fails. Output goes to a temporary folder, deleted afterwards unless -KeepOutput.

.EXAMPLE
./templatepack/test-template.ps1

.EXAMPLE
./templatepack/test-template.ps1 -SkipUiTests -SkipRepositoryTests -KeepOutput

.EXAMPLE
./templatepack/test-template.ps1 -UpgradeFrom latest -Only AcmePortal

.EXAMPLE
./templatepack/test-template.ps1 -UpgradeFrom 0.2.0-beta.8 -Only AcmePortal
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
    [string[]]$Only,

    # Where to put the packages, generated apps and their test results. Defaults to a new folder in the temp folder.
    [string]$WorkRoot,

    # What to upgrade an app from, to simulate an upgrade: a version released on nuget.org (e.g. 0.1.0 or
    # 0.2.0-beta.8), or 'latest' for the newest release that is not a pre-release.
    [string]$UpgradeFrom
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$templateRoot = Join-Path $repositoryRoot 'template'
$runId = [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
$workRoot = if ($WorkRoot) { $WorkRoot } else { Join-Path ([IO.Path]::GetTempPath()) "blazwiz-template-test-$runId" }
$script:Failures = New-Object System.Collections.Generic.List[string]

# Name, extra options, and whether the generated app includes tests.
$cases = @(
    @{ Name = 'AcmePortal'; Options = @('--title', 'Acme Portal', '--description', 'Welcome to Acme.'); Tests = $true; UiTests = $true },
    # Characters that would break the JSON, HTML and C# strings the title and description go into are removed.
    @{ Name = 'Quoted'; Options = @('--title', 'Acme "Portal" <Beta> & C\o', '--description', 'Say "hi" <now> & \leave'); Tests = $true; UiTests = $true },
    @{ Name = 'my-site'; Options = @(); Tests = $true; UiTests = $false },
    @{ Name = '1stApp'; Options = @('--showcase', 'false'); Tests = $true; UiTests = $false },
    @{ Name = 'NoNotes'; Options = @('--notes', 'false'); Tests = $true; UiTests = $true },
    @{ Name = 'NoSync'; Options = @('--sync', 'false'); Tests = $true; UiTests = $true },
    @{ Name = 'Minimal'; Options = @('--tests', 'false', '--showcase', 'false', '--sync', 'false'); Tests = $false; UiTests = $false }
)

if ($Only) {
    $cases = @($cases | Where-Object { $Only -contains $_.Name })
}

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

# Windows PowerShell (5.1) hands arguments to native commands without escaping the quotes inside them, so a quote in
# a title would end the argument there. PowerShell 7 escapes them itself.
function ConvertTo-NativeArguments([string[]]$Arguments) {
    if ($PSVersionTable.PSVersion.Major -ge 7) {
        return $Arguments
    }

    return @($Arguments | ForEach-Object { $_ -replace '"', '\"' })
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

# Points an app at the packages in the local feed, at a version, as an app upgrading to them would: the version in
# Directory.Packages.props, and the feed in nuget.config (as well as nuget.org).
function Use-Packages([string]$App, [string]$Version, [string]$Feed) {
    $packagesProps = Join-Path $App 'Directory.Packages.props'
    (Get-Content $packagesProps -Raw) -replace '<BlazwizVersion>[^<]*</BlazwizVersion>', "<BlazwizVersion>$Version</BlazwizVersion>" |
        Set-Content -Path $packagesProps -NoNewline
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="blazwiz-test" value="$Feed" />
  </packageSources>
</configuration>
"@ | Set-Content -Path (Join-Path $App 'nuget.config')
}

function Test-Project([string]$Root, [string]$Solution, [bool]$UiTests, [string]$Label, [string[]]$BuildArguments) {
    # Build each app with fresh build servers, as on a machine that has only that app. On Linux, an app built with the
    # compiler server left by the apps before it intermittently failed in the Razor source generator (its .razor files
    # read as malformed), and passed when built again.
    dotnet build-server shutdown | Out-Null

    if (-not (Invoke-Checked "$Label builds" { dotnet build (Join-Path $Root $Solution) --nologo -v q @BuildArguments })) {
        return
    }

    $testProjects = Get-ChildItem -Path $Root -Filter '*Tests.csproj' -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Where-Object { $UiTests -or $_.BaseName -notlike '*.UITests' }
    foreach ($project in $testProjects) {
        Invoke-Checked "$Label $($project.BaseName) pass" { dotnet test $project.FullName --nologo --no-build -v q @BuildArguments } | Out-Null
    }
}

if (Test-Path $workRoot) {
    # Never run in, or afterwards delete, a folder this run did not create.
    throw "The work folder '$workRoot' already exists. Use a new folder, or delete it first."
}

$createdWorkRoot = $false
try {
    New-Item -ItemType Directory -Path $workRoot | Out-Null
    $createdWorkRoot = $true

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

    # From 1.0, checked against the latest release for binary breaking changes, as the release workflow does.
    $validation = @()
    $baseline = & (Join-Path $repositoryRoot 'eng/get-validation-baseline.ps1')
    if ($baseline) {
        $validation = @("-p:PackageValidationBaselineVersion=$baseline")
    }

    Write-Step "Packing the framework packages ($version)"
    if (-not (Invoke-Checked 'framework packs' { dotnet pack (Join-Path $repositoryRoot 'blazwiz.slnx') -c Release -o $feed --nologo -v q -p:VersionSuffix=$versionSuffix @validation })) {
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

        $options = ConvertTo-NativeArguments $case.Options
        if (-not (Invoke-Checked "'$name' is created" { dotnet new blazwiz -n $name -o $output @options --no-restore --debug:custom-hive $hive })) {
            continue
        }

        Use-Packages -App $output -Version $version -Feed $feed

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

        # The AWS base stack comes from the Devoctomy.Blazwiz.Aws package at the app's version, not copied into the app.
        $baseStack = & (Join-Path $output 'cloudformation/base-stack.ps1') 2>&1 | Select-Object -Last 1
        if ("$baseStack" -match "devoctomy\.blazwiz\.aws[\\/]$([regex]::Escape($version.ToLowerInvariant()))[\\/]tools[\\/]cloudformation$" -and
            (Test-Path (Join-Path $baseStack 'stack.yaml')) -and (Test-Path (Join-Path $baseStack 'deploy.ps1'))) {
            Write-Host "    [PASS] AWS base stack found in the package ($version)" -ForegroundColor Green
        }
        else {
            $script:Failures.Add("'$name' AWS base stack not found in the package: $baseStack")
            Write-Host "    [FAIL] AWS base stack not found in the package: $baseStack" -ForegroundColor Red
        }

        $solution = (Get-ChildItem -Path $output -Filter '*.slnx' | Select-Object -First 1).Name
        $hasTestProjects = [bool](Get-ChildItem -Path $output -Filter '*Tests.csproj' -Recurse)
        if ($hasTestProjects -ne $case.Tests) {
            $script:Failures.Add("'$name' test projects present: $hasTestProjects, expected $($case.Tests)")
            Write-Host "    [FAIL] test projects present: $hasTestProjects, expected $($case.Tests)" -ForegroundColor Red
        }

        Test-Project -Root $output -Solution $solution -UiTests ($case.UiTests -and -not $SkipUiTests) -Label "'$name'" -BuildArguments @()
    }

    if ($UpgradeFrom) {
        # The release to upgrade from, as published on nuget.org: its template, and the packages its apps use.
        $released = @((Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/devoctomy.blazwiz.templates/index.json').versions)
        $baseVersion = if ($UpgradeFrom -eq 'latest') {
            $released | Where-Object { $_ -match '^\d+\.\d+\.\d+$' } | Sort-Object { [version]$_ } | Select-Object -Last 1
        }
        else {
            $released | Where-Object { $_ -eq $UpgradeFrom }
        }

        if (-not $baseVersion) {
            throw "No release '$UpgradeFrom' of Devoctomy.Blazwiz.Templates on nuget.org (released: $($released -join ', '))."
        }

        Write-Step "Upgrade path: an app from release $baseVersion (nuget.org), upgraded to $version"
        $baseHive = Join-Path $workRoot 'upgrade-hive'
        $upgraded = Join-Path $workRoot 'apps/Upgraded'
        if ((Invoke-Checked "template $baseVersion installs from nuget.org" { dotnet new install "Devoctomy.Blazwiz.Templates@$baseVersion" --debug:custom-hive $baseHive }) -and
            (Invoke-Checked "'Upgraded' is created from $baseVersion" { dotnet new blazwiz -n Upgraded -o $upgraded --no-restore --debug:custom-hive $baseHive })) {
            # Created as a user's app is: on the packages released with that template, from nuget.org.

            # Customise it as an app would: a page of its own, and the theme re-branded through the framework's tokens.
            @'
@page "/custom"

<PageHeader Title="Custom" Icon="bi-star" />
<p id="custom-page">Added by the app.</p>
'@ | Set-Content -Path (Join-Path $upgraded 'Upgraded.Portal/Pages/Custom.razor')
            Add-Content -Path (Join-Path $upgraded 'Upgraded.Portal/wwwroot/css/app.css') -Value "`n:root { --bw-accent: #0d9488; }"

            Test-Project -Root $upgraded -Solution 'Upgraded.slnx' -UiTests $false -Label "'Upgraded' at $baseVersion"

            Use-Packages -App $upgraded -Version $version -Feed $feed
            Test-Project -Root $upgraded -Solution 'Upgraded.slnx' -UiTests (-not $SkipUiTests) -Label "'Upgraded' upgraded to $version"
        }
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
    elseif ($createdWorkRoot) {
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
