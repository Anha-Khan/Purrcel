#Requires -Version 5.1
<#
    Shared helpers for the Cat Courier local verification scripts.

    Contract for every script that dot-sources this file:
      * Unity is invoked through the absolute CLI path below (or $env:CAT_COURIER_UNITY).
      * No secret value is ever written to the console or a log.
      * No Git command is ever issued.
#>

Set-StrictMode -Version Latest

# Absolute path to the editor already installed and used to build this project.
$script:UnityCli = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe'

# Fully qualified entry points that already exist in Assets/_Project/Editor.
$script:ApplyAllMethod = 'CatCourier.Editor.CatCourierProjectSetup.ApplyAll'
$script:BuildAndroidMethod = 'CatCourier.Editor.CatCourierProjectSetup.BuildAndroid'

# Directories that hold generated Unity state. Never scanned, never validated.
$script:GeneratedDirs = @('Library', 'Temp', 'obj', 'Builds', 'Logs', 'TestResults', 'UserSettings')

# Locally generated + .gitignored. Key material here is expected, so findings in
# these files are reported as warnings with values redacted instead of failing.
$script:LocalOnlyFiles = @(
    'Assets/_Project/Config/RevenueCatConfig.asset',
    'Assets/_Project/Config/RevenueCatConfig.asset.meta',
    'ProjectSettings/PurchasingSettings.asset'
)

# Directories that are part of the authored project and are safe to scan/validate.
$script:SourceDirs = @('Assets', 'Packages', 'ProjectSettings', 'docs')

$script:Failures = New-Object System.Collections.Generic.List[string]
$script:Warnings = New-Object System.Collections.Generic.List[string]

function Get-ProjectRoot {
    <#  Walk up from this file until the folder that holds Assets/ + ProjectSettings/. #>
    $dir = Split-Path -Parent $PSCommandPath
    while ($dir) {
        if ((Test-Path (Join-Path $dir 'Assets')) -and (Test-Path (Join-Path $dir 'ProjectSettings'))) {
            return (Resolve-Path $dir).Path
        }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    throw 'Could not locate the Cat Courier project root (a folder containing Assets/ and ProjectSettings/).'
}

function Get-UnityCli {
    if ($env:CAT_COURIER_UNITY -and (Test-Path $env:CAT_COURIER_UNITY)) { return $env:CAT_COURIER_UNITY }
    if (Test-Path $script:UnityCli) { return $script:UnityCli }
    throw "Unity CLI not found at $($script:UnityCli). Set CAT_COURIER_UNITY to the Unity.exe path."
}

function Write-Stage { param([string] $Message) Write-Host "`n=== $Message ===" -ForegroundColor Cyan }
function Write-Ok    { param([string] $Message) Write-Host "  [ok]   $Message" -ForegroundColor Green }
function Write-Warn  { param([string] $Message) Write-Host "  [warn] $Message" -ForegroundColor Yellow }
function Write-Info  { param([string] $Message) Write-Host "  ..    $Message" -ForegroundColor DarkGray }
function Write-Bad   { param([string] $Message) Write-Host "  [FAIL] $Message" -ForegroundColor Red }

function Add-Failure { param([string] $Message) $script:Failures.Add($Message) | Out-Null }
function Add-Warning { param([string] $Message) $script:Warnings.Add($Message) | Out-Null }

function Write-Summary {
    <#  Print the aggregate result and return the process exit code. #>
    Write-Host ''
    Write-Host '--- Summary ---' -ForegroundColor Cyan
    if ($script:Warnings.Count -gt 0) { Write-Host "Warnings: $($script:Warnings.Count)" -ForegroundColor Yellow }
    if ($script:Failures.Count -eq 0) {
        Write-Host 'Result:  PASS' -ForegroundColor Green
        return 0
    }
    foreach ($f in $script:Failures) { Write-Bad $f }
    Write-Host "Result:  FAIL ($($script:Failures.Count) failed check(s))" -ForegroundColor Red
    return 1
}

function New-ArtifactFile {
    <#  Absolute path inside <root>/<subdir>, creating the directory. #>
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [ValidateSet('Logs', 'TestResults')] [string] $SubDir,
        [Parameter(Mandatory)] [string] $Name
    )
    $dir = Join-Path $Root $SubDir
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    return (Join-Path (Resolve-Path $dir).Path $Name)
}

function ConvertTo-ProcessArgument {
    <#  Quote one argument so Start-Process round-trips paths containing spaces. #>
    param([Parameter(Mandatory)] [string] $Value)
    if ($Value -match '[\s"]') { return '"' + ($Value -replace '"', '\"') + '"' }
    return $Value
}

function Invoke-Unity {
    <#
        Run the editor in batch mode and return @{ ExitCode; LogFile }.

        The editor is always given -logFile so its own output never lands in the
        console. -quit is deliberately omitted for -runTests invocations: the test
        runner terminates the process itself, and -quit can race it.
    #>
    param(
        [Parameter(Mandatory)] [string[]] $UnityArgs,
        [Parameter(Mandatory)] [string] $LogFile,
        [Parameter(Mandatory)] [string] $Label,
        [int] $TimeoutSeconds = 5400
    )

    $exe = Get-UnityCli
    $all = @('-batchmode', '-nographics', '-logFile', $LogFile) + $UnityArgs
    $quoted = ($all | ForEach-Object { ConvertTo-ProcessArgument -Value $_ }) -join ' '

    Write-Info "$Label (log: $(Split-Path -Leaf $LogFile))"
    $proc = Start-Process -FilePath $exe -ArgumentList $quoted -NoNewWindow -PassThru
    $proc | Wait-Process -Timeout $TimeoutSeconds -ErrorAction SilentlyContinue
    if (-not $proc.HasExited) {
        Write-Info 'Timed out, terminating the editor.'
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        Add-Failure "$Label timed out after $TimeoutSeconds s. See $(Split-Path -Leaf $LogFile)."
        return @{ ExitCode = 124; LogFile = $LogFile }
    }
    return @{ ExitCode = $proc.ExitCode; LogFile = $LogFile }
}

function Get-TestSummary {
    <#
        Read total/passed/failed out of a Unity NUnit results file.
        Handles both the NUnit (<test-run>) and JUnit (<testsuites>) layouts, and
        returns $null for a missing, malformed, or unrecognised report.
    #>
    param([Parameter(Mandatory)] [string] $Path)
    if (-not (Test-Path $Path)) { return $null }
    try {
        [xml]$xml = Get-Content -LiteralPath $Path -Raw
        $nunit = $xml.SelectSingleNode('/test-run')
        $junit = $xml.SelectSingleNode('/testsuites')
        if ($null -ne $nunit) {
            return [pscustomobject]@{
                Total   = [int] $nunit.GetAttribute('total')
                Passed  = [int] $nunit.GetAttribute('passed')
                Failed  = [int] $nunit.GetAttribute('failed')
                Skipped = [int] $nunit.GetAttribute('skipped')
                Result  = $nunit.GetAttribute('result')
            }
        }
        if ($null -ne $junit) {
            # JUnit reports failures + errors together.
            $failures = [int] $junit.GetAttribute('failures') + [int] $junit.GetAttribute('errors')
            $total = [int] $junit.GetAttribute('tests')
            $skipped = [int] $junit.GetAttribute('skipped')
            return [pscustomobject]@{
                Total   = $total
                Passed  = $total - $failures - $skipped
                Failed  = $failures
                Skipped = $skipped
                Result  = $(if ($failures -gt 0) { 'Failed' } else { 'Passed' })
            }
        }
        return $null
    } catch {
        return $null
    }
}

function Get-EditModeTestFailure {
    <#
        Unity exits 2 when tests run but some fail, and 0 when they all pass.
        Anything else means the editor itself broke before the suite completed.
    #>
    param([int] $ExitCode)
    if ($ExitCode -eq 0) { return $null }
    if ($ExitCode -eq 2) { return 'One or more Edit Mode tests failed (Unity exit code 2).' }
    return "Unity exited with code $ExitCode before the Edit Mode suite could be trusted."
}

function Invoke-EditModeTests {
    <#  Run the Edit Mode suite in <Root> and report pass/fail. Returns $true when green. #>
    param(
        [Parameter(Mandatory)] [string] $Root,
        [Parameter(Mandatory)] [string] $ReportPath,
        [Parameter(Mandatory)] [string] $LogPath,
        [string] $Label = 'Edit Mode tests'
    )

    if (Test-Path $ReportPath) { Remove-Item -LiteralPath $ReportPath -Force }
    $result = Invoke-Unity -UnityArgs @('-projectPath', $Root, '-runTests', '-testPlatform', 'EditMode', '-testResults', $ReportPath) -LogFile $LogPath -Label $Label
    $summary = Get-TestSummary -Path $ReportPath
    $problem = Get-EditModeTestFailure -ExitCode $result.ExitCode

    if ($null -eq $summary) {
        Add-Failure "$Label produced no readable NUnit report ($(Split-Path -Leaf $ReportPath))."
        if ($problem) { Add-Failure $problem }
        return $false
    }

    $counts = "$($summary.Passed)/$($summary.Total) passed, $($summary.Failed) failed, $($summary.Skipped) skipped"
    if ($problem) { Add-Failure "${Label}: $counts. $problem" } else { Write-Ok "${Label}: $counts" }
    return (-not $problem)
}

function Get-SourceFile {
    <#  Authored text files, excluding generated Unity state and the local secrets. #>
    param([Parameter(Mandatory)] [string] $Root)

    $files = New-Object System.Collections.Generic.List[string]
    $excludedDirs = @($script:GeneratedDirs + @('.git', '.vs', '.idea', '.vscode', 'Tools'))
    $excludedFiles = @($script:LocalOnlyFiles)

    foreach ($dir in $script:SourceDirs) {
        $full = Join-Path $Root $dir
        if (-not (Test-Path $full)) { continue }
        Get-ChildItem -LiteralPath $full -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            $rel = $_.FullName.Substring($Root.Length).TrimStart('\', '/') -replace '\\', '/'
            $skip = $false
            foreach ($e in $excludedDirs) { if ($rel -match "(^|/)$([regex]::Escape($e))(/|$)") { $skip = $true; break } }
            if (-not $skip) { foreach ($e in $excludedFiles) { if ($rel -eq $e) { $skip = $true; break } } }
            if (-not $skip) { $files.Add($_.FullName) }
        }
    }

    # Root-level authored files (README, LICENSE, HANDOFF, .gitignore, ...).
    Get-ChildItem -LiteralPath $Root -File -ErrorAction SilentlyContinue | ForEach-Object { $files.Add($_.FullName) }
    return $files
}

function Test-JsonFile {
    <#  Validate one JSON file. Returns $true when it parses. #>
    param([Parameter(Mandatory)] [string] $Path, [Parameter(Mandatory)] [string] $Root)
    $rel = $Path.Substring($Root.Length).TrimStart('\', '/') -replace '\\', '/'
    try {
        $null = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -ErrorAction Stop
        return $true
    } catch {
        Add-Failure "Invalid JSON: $rel -> $($_.Exception.Message)"
        return $false
    }
}

function Invoke-SecretScan {
    <#
        Flag committed credential-shaped content in the authored tree.

        Only the file path, line number, rule name and a length are reported. The
        matched text is never read into the output, so this script is safe to run
        with a terminal recorder or a shared log.
    #>
    param([Parameter(Mandatory)] [string] $Root)

    $rules = @(
        @{ Name = 'private-key-block';      Pattern = '-----BEGIN (RSA |EC |DSA |OPENSSH |PGP )?PRIVATE KEY-----' }
        @{ Name = 'google-api-key';         Pattern = 'AIza[0-9A-Za-z_\-]{35}' }
        @{ Name = 'aws-access-key-id';      Pattern = 'AKIA[0-9A-Z]{16}' }
        @{ Name = 'revenuecat-sdk-key';     Pattern = 'appl_[A-Za-z0-9]{20,}' }
        @{ Name = 'json-web-token';         Pattern = 'eyJ[A-Za-z0-9_\-]{8,}\.eyJ[A-Za-z0-9_\-]{8,}' }
        @{ Name = 'google-services-plist';  Pattern = 'GOOGLE_APP_ID' }
        @{ Name = 'assigned-credential';    Pattern = '(?i)\b(api[_-]?key|api[_-]?secret|client[_-]?secret|access[_-]?token|auth[_-]?token|secret[_-]?key|password|passwd)\b\s*[:=]\s*["''`]?[A-Za-z0-9_\-\/\.\+]{16,}' }
    )

    $bannedExtensions = @('.keystore', '.jks', '.p12', '.pfx', '.pem', '.key', '.ppk')
    $bannedNames = @('.env', '.env.local', '.env.production')

    $hardFindings = New-Object System.Collections.Generic.List[string]
    $softFindings = New-Object System.Collections.Generic.List[string]

    $files = Get-SourceFile -Root $Root
    $textExtensions = @('.cs', '.json', '.md', '.txt', '.asmdef', '.xml', '.yml', '.yaml', '.asset', '.meta', '.cfg', '.ini', '.gradle', '.properties', '.gitignore', '.gitattributes')

    foreach ($file in $files) {
        $rel = $file.Substring($Root.Length).TrimStart('\', '/') -replace '\\', '/'
        $ext = [System.IO.Path]::GetExtension($file).ToLowerInvariant()
        $name = [System.IO.Path]::GetFileName($file).ToLowerInvariant()

        if ($bannedNames -contains $name) { $hardFindings.Add("$rel [banned-env-file] present") ; continue }
        if ($bannedExtensions -contains $ext) { $hardFindings.Add("$rel [banned-key-file] present"); continue }
        if ($textExtensions -notcontains $ext -and $name -notmatch '^\.(gitignore|gitattributes)$') { continue }

        $lines = Get-Content -LiteralPath $file -ErrorAction SilentlyContinue
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            foreach ($rule in $rules) {
                $m = [regex]::Match($line, $rule.Pattern)
                if ($m.Success) {
                    $entry = ('{0}:{1} [{2}] matched, {3} chars (value redacted)' -f $rel, ($i + 1), $rule.Name, $m.Value.Length)
                    if ($script:LocalOnlyFiles -contains $rel) { $softFindings.Add($entry) } else { $hardFindings.Add($entry) }
                }
            }
        }
    }

    Write-Info "Scanned $($files.Count) authored file(s)."

    foreach ($s in $softFindings) {
        Add-Warning "Local ignored file holds key material: $s"
        Write-Warn "Local ignored file holds key material: $s"
    }
    if ($softFindings.Count -eq 0) { Write-Ok 'No key material in the local .gitignored config.' }

    foreach ($h in $hardFindings) {
        Add-Failure "Secret scan: $h"
        Write-Bad "Secret scan: $h"
    }
    if ($hardFindings.Count -eq 0) {
        Write-Ok 'No secret-shaped content in the authored tree.'
    }

    return ($hardFindings.Count -eq 0)
}

function Invoke-JsonValidation {
    <#  Parse every authored .json file. Returns $true when all of them are valid. #>
    param([Parameter(Mandatory)] [string] $Root)

    $files = Get-SourceFile -Root $Root |
        Where-Object { [System.IO.Path]::GetExtension($_).ToLowerInvariant() -eq '.json' } |
        Sort-Object

    if (-not $files -or $files.Count -eq 0) {
        Add-Failure 'JSON validation found no .json files to check.'
        return $false
    }

    $ok = $true
    foreach ($file in $files) {
        if (Test-JsonFile -Path $file -Root $Root) {
            Write-Ok "Valid JSON: $($file.Substring($Root.Length).TrimStart('\', '/') -replace '\\', '/')"
        } else {
            $ok = $false
        }
    }
    return $ok
}
