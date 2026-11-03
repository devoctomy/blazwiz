# Shared helpers for the blazwiz deployment scripts. Dot-source this file; do not run it directly.

Set-StrictMode -Version Latest

$script:AwsRegion = $null
$script:AwsProfile = $null
$script:LastAwsError = $null

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Write-Info([string]$Message) {
    Write-Host "    $Message"
}

function Write-Ok([string]$Message) {
    Write-Host "    [ok] $Message" -ForegroundColor Green
}

function Write-Warn([string]$Message) {
    Write-Host "    [warn] $Message" -ForegroundColor Yellow
}

function Get-StackName([string]$AppName, [string]$Environment) {
    return "$AppName-$Environment"
}

function Initialize-AwsContext {
    <#
    .SYNOPSIS
    Checks the AWS CLI is available, resolves the region and shows which account will be used.
    #>
    param(
        [string]$Region,
        [string]$AwsProfile
    )

    if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
        throw 'The AWS CLI (v2) is required: https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html'
    }

    $script:AwsProfile = if ([string]::IsNullOrWhiteSpace($AwsProfile)) { $null } else { $AwsProfile }

    if ([string]::IsNullOrWhiteSpace($Region)) {
        $Region = $env:AWS_REGION
    }

    if ([string]::IsNullOrWhiteSpace($Region)) {
        $Region = $env:AWS_DEFAULT_REGION
    }

    if ([string]::IsNullOrWhiteSpace($Region)) {
        # Reads local CLI configuration only, no AWS call. Exits non-zero when no region is configured.
        $configureArgs = @('configure', 'get', 'region')
        if ($script:AwsProfile) { $configureArgs += @('--profile', $script:AwsProfile) }
        $previousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $Region = @(& aws @configureArgs 2>$null) -join ''
        }
        finally {
            $ErrorActionPreference = $previousPreference
        }
    }

    if ([string]::IsNullOrWhiteSpace($Region)) {
        throw 'No AWS region. Pass -Region (e.g. -Region eu-west-2) or set a default with "aws configure".'
    }

    $script:AwsRegion = "$Region".Trim()

    $identity = Invoke-Aws -Arguments @('sts', 'get-caller-identity') -Json
    Write-Info "Account : $($identity.Account)"
    Write-Info "Caller  : $($identity.Arn)"
    Write-Info "Region  : $script:AwsRegion"
    if ($script:AwsProfile) {
        Write-Info "Profile : $script:AwsProfile"
    }
    elseif ($env:AWS_ACCESS_KEY_ID) {
        Write-Warn 'Using credentials from the AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY environment variables.'
    }

    return $identity
}

function Confirm-Action {
    <#
    .SYNOPSIS
    Asks the user to type "yes" before continuing, unless -Force was given. Exits if they decline.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Message,
        [switch]$Force
    )

    if ($Force) {
        return
    }

    Write-Host ''
    Write-Host "    $Message" -ForegroundColor Yellow
    $answer = Read-Host '    Type "yes" to continue'
    if ($answer -ne 'yes') {
        Write-Host '    Cancelled, nothing was changed.'
        exit 1
    }
}

function Invoke-Aws {
    <#
    .SYNOPSIS
    Runs the AWS CLI with the configured region / profile and fails loudly on a non-zero exit code.

    .PARAMETER Json
    Parse the output as JSON.

    .PARAMETER Stream
    Write output straight to the console (for long running commands) instead of returning it.

    .PARAMETER AllowFailure
    Return $null instead of throwing on failure. The error text is left in $script:LastAwsError.

    .PARAMETER NoProfile
    Do not pass --profile, so credentials come from environment variables instead.
    #>
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [switch]$Json,
        [switch]$Stream,
        [switch]$AllowFailure,
        [switch]$NoProfile
    )

    $all = @($Arguments) + @('--region', $script:AwsRegion)
    if ($script:AwsProfile -and -not $NoProfile) { $all += @('--profile', $script:AwsProfile) }
    if ($Json) { $all += @('--output', 'json') }

    # Native stderr must not be turned into terminating errors (Windows PowerShell 5.1 does this
    # when $ErrorActionPreference is Stop), so relax it just for the call and check the exit code.
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        if ($Stream) {
            & aws @all 2>&1 | ForEach-Object { Write-Host "    $_" }
            $raw = @()
        }
        else {
            $raw = @(& aws @all 2>&1)
        }
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    $stdout = @($raw | Where-Object { $_ -isnot [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_" })
    $stderr = @($raw | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_" })

    if ($exitCode -ne 0) {
        $script:LastAwsError = ($stderr + $stdout) -join [Environment]::NewLine
        if ($AllowFailure) {
            return $null
        }

        throw "aws $($Arguments[0..1] -join ' ') failed (exit code $exitCode):$([Environment]::NewLine)$script:LastAwsError"
    }

    $script:LastAwsError = $null
    if ($Stream) {
        return
    }

    if ($Json) {
        $text = $stdout -join [Environment]::NewLine
        if ([string]::IsNullOrWhiteSpace($text)) { return $null }
        return $text | ConvertFrom-Json
    }

    return $stdout
}

function Get-OptionalProperty($InputObject, [string]$Name) {
    <#
    .SYNOPSIS
    Returns a property's value, or $null if the object does not have it (the AWS CLI omits empty fields).
    #>
    if ($null -eq $InputObject) { return $null }
    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-StackOutputs([string]$StackName) {
    <#
    .SYNOPSIS
    Returns the stack outputs as a hashtable, or $null if the stack does not exist.
    #>
    $stacks = Invoke-Aws -Arguments @('cloudformation', 'describe-stacks', '--stack-name', $StackName) -Json -AllowFailure
    if ($null -eq $stacks) {
        if ($script:LastAwsError -match 'does not exist') { return $null }
        throw "Could not describe stack $StackName`: $script:LastAwsError"
    }

    # A stack that is still being created has no Outputs property yet.
    $outputs = @{}
    $outputsProperty = $stacks.Stacks[0].PSObject.Properties['Outputs']
    if ($null -ne $outputsProperty) {
        foreach ($output in @($outputsProperty.Value)) {
            if ($null -ne $output) { $outputs[$output.OutputKey] = $output.OutputValue }
        }
    }

    return $outputs
}

function ConvertTo-PlainText([Security.SecureString]$Secure) {
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function New-TempJsonFile($Value) {
    <#
    .SYNOPSIS
    Writes a value as JSON to a temporary file and returns its path, for use with file:// arguments.
    Avoids quoting problems when passing JSON on the command line. Delete the file when done.
    #>
    $path = Join-Path ([IO.Path]::GetTempPath()) "blazwiz-$([Guid]::NewGuid().ToString('N')).json"
    $json = $Value | ConvertTo-Json -Depth 10 -Compress
    [IO.File]::WriteAllText($path, $json, (New-Object Text.UTF8Encoding($false)))
    return $path
}

function Add-ContentSecurityPolicy {
    <#
    .SYNOPSIS
    Adds a Content-Security-Policy <meta> tag to a published portal's index.html.

    .DESCRIPTION
    Done after publishing because the inline import map that Blazor writes into index.html changes with every build,
    and the policy must allow it by its hash. Doing it here keeps the policy and the page it protects in one file, so
    they always match. Local runs are not published, so they have no policy.

    The service worker checks every cached file against service-worker-assets.js, so the index.html hash there is
    updated to match. The pre-compressed copies of index.html are deleted, as they no longer match (deploy.ps1 does
    not upload them anyway).

    What the policy allows:
      - scripts: the app's own files, WebAssembly, and the inline import map (by hash). No other inline script or
        event handler attributes (onerror=... etc.), so markup injected into a page cannot run.
      - connections: the app itself, and only the AWS endpoints the portal calls in this region and data bucket.
      - styles: the app's own files, plus inline style attributes (Blazor sets them on elements).
      - images: the app's own files and data: URIs only. Remote images in notes are not loaded.
      - workers: the app's own files, and blob: for the OPFS worker.
    Framing is already refused by the X-Frame-Options header from CloudFront's managed SecurityHeadersPolicy
    (frame-ancestors cannot be set in a <meta> policy).
    #>
    param(
        [Parameter(Mandatory)][string]$WwwRoot,
        [Parameter(Mandatory)][string]$Region,
        [Parameter(Mandatory)][string]$DataBucketName
    )

    $indexPath = Join-Path $WwwRoot 'index.html'
    $utf8 = New-Object Text.UTF8Encoding($false)
    $html = [IO.File]::ReadAllText($indexPath, $utf8)
    if ($html -match 'http-equiv="Content-Security-Policy"') {
        throw "$indexPath already has a Content-Security-Policy."
    }

    $scriptHashes = @()
    foreach ($match in [regex]::Matches($html, '<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)</script>')) {
        # Browsers hash the script text after turning CRLF (and lone CR) line breaks into LF.
        $content = $match.Groups[1].Value -replace "`r`n?", "`n"
        if ($content.Length -gt 0) {
            $hash = [Security.Cryptography.SHA256]::Create().ComputeHash($utf8.GetBytes($content))
            $scriptHashes += "'sha256-$([Convert]::ToBase64String($hash))'"
        }
    }

    $directives = @(
        "default-src 'self'"
        "script-src 'self' 'wasm-unsafe-eval' $($scriptHashes -join ' ')".TrimEnd()
        "style-src 'self' 'unsafe-inline'"
        "img-src 'self' data:"
        "font-src 'self'"
        "connect-src 'self' https://cognito-idp.$Region.amazonaws.com https://cognito-identity.$Region.amazonaws.com https://secretsmanager.$Region.amazonaws.com https://$DataBucketName.s3.$Region.amazonaws.com"
        "worker-src 'self' blob:"
        "manifest-src 'self'"
        "object-src 'none'"
        "base-uri 'self'"
        "form-action 'self'"
    )
    $meta = "<meta http-equiv=""Content-Security-Policy"" content=""$($directives -join '; ')"" />"

    # First in <head> (after the charset), so it covers every script and stylesheet after it.
    $charset = [regex]::Match($html, '<meta charset="[^"]*"\s*/?>')
    if (-not $charset.Success) {
        throw "Could not find <meta charset> in $indexPath."
    }
    $html = $html.Insert($charset.Index + $charset.Length, "`n    $meta")
    [IO.File]::WriteAllText($indexPath, $html, $utf8)

    $assetsPath = Join-Path $WwwRoot 'service-worker-assets.js'
    if (Test-Path $assetsPath) {
        $indexHash = [Convert]::ToBase64String([Security.Cryptography.SHA256]::Create().ComputeHash([IO.File]::ReadAllBytes($indexPath)))
        $assets = [IO.File]::ReadAllText($assetsPath, $utf8)
        $pattern = '("hash":\s*")sha256-[^"]*(",\s*"url":\s*"index\.html")'
        if (-not [regex]::IsMatch($assets, $pattern)) {
            throw "Could not find index.html in $assetsPath."
        }
        $assets = [regex]::Replace($assets, $pattern, "`${1}sha256-$indexHash`${2}")
        [IO.File]::WriteAllText($assetsPath, $assets, $utf8)
    }

    Remove-Item -Force (Join-Path $WwwRoot 'index.html.br'), (Join-Path $WwwRoot 'index.html.gz') -ErrorAction SilentlyContinue
}
