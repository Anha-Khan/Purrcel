#Requires -Version 5.1
<#
.SYNOPSIS
    Day 6 fresh-import check for Cat Courier: proves the authored tree alone is
    enough to open, self-heal, and pass tests.

.DESCRIPTION
    Copies the project into a temporary folder while leaving behind every
    generated Unity directory (Library, Temp, obj, Builds, Logs, TestResults,
    UserSettings), then in that copy runs Apply All followed by the full Edit Mode
    suite. Nothing in the real project is modified.

    A clean Library forces a genuine first import, so this is the check that
    catches missing .meta files, an unlisted package, and a scene or asset that
    only existed because of local state.

    Safety properties:
      * The copy is discarded on exit unless -KeepPath is passed.
      * No secret value is ever printed.
      * No Git command is issued.

.PARAMETER KeepPath
    Leave the temporary copy on disk and print its path.

.PARAMETER TempRoot
    Parent folder for the temporary copy. Defaults to the system temp folder.

.EXAMPLE
    pwsh -File Tools/verify-fresh-import.ps1
    pwsh -File Tools/verify-fresh-import.ps1 -KeepPath
#>
[CmdletBinding()]
param(
    [switch] $KeepPath,
    [string] $TempRoot = [System.IO.Path]::GetTempPath()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Common.ps1')

$root = Get-ProjectRoot
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if (-not (Test-Path $TempRoot)) {
    New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null
}

$copy = Join-Path (Resolve-Path $TempRoot).Path "CatCourier-FreshImport-$stamp"

# Generated state that must never be carried into a fresh import.
$excluded = @('Library', 'Temp', 'obj', 'Builds', 'Logs', 'TestResults', 'UserSettings')
$excludedPaths = @($excluded | ForEach-Object { Join-Path $root $_ })

Write-Host "Cat Courier - fresh import verification" -ForegroundColor White
Write-Host "Source: $root"
Write-Host "Copy:   $copy"
Write-Host "Unity:  $(Get-UnityCli)"

if (Test-Path $copy) {
    throw "Refusing to overwrite an existing copy: $copy"
}

$started = Get-Date

try {
    # ------------------------------------------------------------------ copy --
    Write-Stage "1/3  Copy authored tree (excluding: $($excluded -join ', '))"
    New-Item -ItemType Directory -Force -Path $copy | Out-Null

    # robocopy is the native tool for this; exit codes below 8 all mean success.
    # /XD takes a space-separated list, so it must be passed as separate
    # arguments after every other switch (a later /switch ends the list).
    $roboArgs = @($root, $copy, '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:1', '/W:1', '/XD') + $excludedPaths
    $null = & robocopy @roboArgs
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit code $LASTEXITCODE."
    }

    $copied = (Get-ChildItem -LiteralPath $copy -Recurse -File -ErrorAction SilentlyContinue).Count
    Write-Ok "Copied $copied file(s)."

    $leaked = @()
    foreach ($dir in $excluded) {
        if (Test-Path (Join-Path $copy $dir)) { $leaked += $dir }
    }
    if ($leaked.Count -gt 0) {
        Add-Failure "Generated state leaked into the fresh copy: $($leaked -join ', ')"
    } else {
        Write-Ok 'No generated Unity state present in the copy.'
    }

    # ---------------------------------------------------------------- ApplyAll --
    Write-Stage '2/3  Apply All on the fresh copy (cold import, no Library cache)'
    $setupLog = New-ArtifactFile -Root $root -SubDir 'Logs' -Name "day6-fresh-import-setup-$stamp.log"
    $setup = Invoke-Unity -UnityArgs @('-projectPath', $copy, '-executeMethod', $script:ApplyAllMethod, '-quit') -LogFile $setupLog -Label 'Apply All'
    if ($setup.ExitCode -ne 0) {
        Add-Failure "Apply All failed (Unity exit code $($setup.ExitCode)). See $(Split-Path -Leaf $setupLog)."
        Write-Bad 'Skipping Edit Mode tests because the fresh copy is not in a trustworthy state.'
    } else {
        Write-Ok 'Apply All completed.'

        # ------------------------------------------------------------ EditMode --
        Write-Stage '3/3  Edit Mode tests on the fresh copy'
        $report = New-ArtifactFile -Root $root -SubDir 'TestResults' -Name "day6-fresh-import-nunit-$stamp.xml"
        $testLog = New-ArtifactFile -Root $root -SubDir 'Logs' -Name "day6-fresh-import-tests-$stamp.log"
        $null = Invoke-EditModeTests -Root $copy -ReportPath $report -LogPath $testLog -Label 'Edit Mode tests (fresh import)'
    }
} finally {
    if ($KeepPath) {
        Write-Host "`nCopy kept at: $copy" -ForegroundColor Yellow
    } elseif (Test-Path $copy) {
        Write-Host "`nRemoving temporary copy..." -ForegroundColor DarkGray
        Remove-Item -LiteralPath $copy -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$elapsed = [math]::Round(((Get-Date) - $started).TotalSeconds, 1)
Write-Host "`nElapsed: $elapsed s" -ForegroundColor DarkGray
exit (Write-Summary)
