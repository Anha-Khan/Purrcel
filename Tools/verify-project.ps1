#Requires -Version 5.1
<#
.SYNOPSIS
    Day 6 local verification for Cat Courier: secret scan, JSON validation,
    Edit Mode tests, and an Android development build.

.DESCRIPTION
    Runs four checks in order against the project in this repository and prints a
    single pass/fail summary. Designed for repeated local use, so it is safe to
    run any number of times.

    Safety properties:
      * Unity is launched in batch mode through its absolute CLI path.
      * No secret value is ever printed; findings report path, line, rule, length.
      * No Git command is issued.

.PARAMETER SkipAndroidBuild
    Run the first three checks only. The Android build is the slow stage.

.PARAMETER SkipTests
    Run everything except the Edit Mode suite.

.EXAMPLE
    pwsh -File Tools/verify-project.ps1
    pwsh -File Tools/verify-project.ps1 -SkipAndroidBuild
#>
[CmdletBinding()]
param(
    [switch] $SkipAndroidBuild,
    [switch] $SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Common.ps1')

$root = Get-ProjectRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$started = Get-Date

Write-Host "Cat Courier - project verification" -ForegroundColor White
Write-Host "Root:    $root"
Write-Host "Unity:   $(Get-UnityCli)"
Write-Host "Started: $($started.ToString('yyyy-MM-dd HH:mm:ss'))"

# ---------------------------------------------------------------- 1. secrets --
Write-Stage '1/4  Secret scan'
$null = Invoke-SecretScan -Root $root

# ------------------------------------------------------------------- 2. json --
Write-Stage '2/4  JSON validation'
$jsonOk = Invoke-JsonValidation -Root $root
if ($jsonOk) { Write-Ok 'All authored JSON files parse.' }

# --------------------------------------------------------------- 3. EditMode --
Write-Stage '3/4  Edit Mode tests'
if ($SkipTests) {
    Write-Warn 'Skipped by -SkipTests.'
} else {
    $report = New-ArtifactFile -Root $root -SubDir 'TestResults' -Name "day6-verify-nunit-$stamp.xml"
    $log = New-ArtifactFile -Root $root -SubDir 'Logs' -Name "day6-verify-tests-$stamp.log"
    $null = Invoke-EditModeTests -Root $root -ReportPath $report -LogPath $log -Label 'Edit Mode tests'
}

# ------------------------------------------------------------------ 4. build --
Write-Stage '4/4  Android development build'
if ($SkipAndroidBuild) {
    Write-Warn 'Skipped by -SkipAndroidBuild.'
} else {
    $buildLog = New-ArtifactFile -Root $root -SubDir 'Logs' -Name "day6-verify-android-$stamp.log"
    $apk = Join-Path $root 'Builds\Android\CatCourier.apk'
    $apkBefore = if (Test-Path $apk) { (Get-Item $apk).LastWriteTimeUtc } else { [datetime]::MinValue }

    $build = Invoke-Unity -UnityArgs @('-projectPath', $root, '-executeMethod', $script:BuildAndroidMethod, '-quit') -LogFile $buildLog -Label 'Android build'

    if ($build.ExitCode -ne 0) {
        Add-Failure "Android build failed (Unity exit code $($build.ExitCode)). See $(Split-Path -Leaf $buildLog)."
    } elseif (-not (Test-Path $apk)) {
        Add-Failure 'Android build reported success but Builds\Android\CatCourier.apk was not produced.'
    } elseif ((Get-Item $apk).LastWriteTimeUtc -le $apkBefore) {
        Add-Failure 'Android build reported success but the APK was not rewritten.'
    } else {
        $sizeMb = [math]::Round((Get-Item $apk).Length / 1MB, 1)
        $hash = (Get-FileHash -Path $apk -Algorithm SHA256).Hash
        Write-Ok "APK rebuilt: Builds\Android\CatCourier.apk ($sizeMb MB)"
        Write-Info "SHA-256 $hash"
    }
}

$elapsed = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)
Write-Host "`nElapsed: $elapsed s" -ForegroundColor DarkGray
exit (Write-Summary)
