#Requires -Version 5.1
<#
.SYNOPSIS
    Removes the generated verification artifacts under Logs/ and TestResults/.

.DESCRIPTION
    Deliberately narrow. It touches only files inside <project>/Logs and
    <project>/TestResults, and only ones this repo's own scripts generate
    (day*-*.log and day*-*.xml / *-nunit*.xml). The two root folders are kept so
    Unity does not have to recreate them.

    It will not touch Assets, ProjectSettings, Packages, docs, Builds, Library,
    or anything under Tools. Nothing is committed or staged, because no Git
    command is ever issued.

    Prints a preview and requires an explicit confirmation before deleting
    anything.

.PARAMETER Force
    Skip the confirmation prompt. Intended for scripted cleanup only.

.EXAMPLE
    pwsh -File Tools/clean-generated-artifacts.ps1
    pwsh -File Tools/clean-generated-artifacts.ps1 -Force
#>
[CmdletBinding()]
param(
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Common.ps1')

$root = Get-ProjectRoot

Write-Host "Cat Courier - clean generated artifacts" -ForegroundColor White
Write-Host "Root: $root"

# Only these two folders, and only artifacts this repo's scripts produce.
$targets = @(
    @{ Folder = 'Logs';        Pattern = '^day.*\.log$' }
    @{ Folder = 'TestResults'; Pattern = '^(day|editmode).*(nunit|junit).*\.xml$' }
)

$planned = New-Object System.Collections.Generic.List[object]
foreach ($target in $targets) {
    $dir = Join-Path $root $target.Folder
    if (-not (Test-Path $dir)) {
        Write-Warn "$($target.Folder)/ does not exist, skipping."
        continue
    }
    Get-ChildItem -LiteralPath $dir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match $target.Pattern } |
        ForEach-Object {
            $planned.Add([pscustomobject]@{
                Folder = $target.Folder
                Name   = $_.Name
                SizeKb = [math]::Round($_.Length / 1KB, 1)
                Path   = $_.FullName
            })
        }
}

Write-Host ''
if ($planned.Count -eq 0) {
    Write-Ok 'Nothing to clean. No generated artifacts matched.'
    exit 0
}

Write-Host 'Files that will be deleted:' -ForegroundColor White
$planned | Sort-Object Folder, Name | ForEach-Object {
    Write-Host ('  {0,-13} {1,10} KB  {2}' -f $_.Folder, $_.SizeKb, $_.Name)
}
$totalKb = [math]::Round((($planned | Measure-Object -Property SizeKb -Sum).Sum), 1)
Write-Host ''
Write-Host "Total: $($planned.Count) file(s), $totalKb KB" -ForegroundColor White

# Nothing outside Logs/ and TestResults/ may ever be removed.
$stray = $planned | Where-Object { $_.Folder -notin @('Logs', 'TestResults') }
if ($stray) {
    throw 'Internal error: a deletion target escaped Logs/ and TestResults/. Aborting.'
}

if (-not $Force) {
    Write-Host ''
    Write-Host 'This cannot be undone.' -ForegroundColor Yellow
    $answer = Read-Host 'Type yes to delete these files'
    if ($answer -cne 'yes') {
        Write-Host 'Aborted. Nothing was deleted.' -ForegroundColor Yellow
        exit 0
    }
}

$failed = 0
foreach ($item in $planned) {
    try {
        Remove-Item -LiteralPath $item.Path -Force -ErrorAction Stop
    } catch {
        Write-Bad "Could not delete $($item.Folder)/$($item.Name): $($_.Exception.Message)"
        $failed++
    }
}

# Drop subdirectories that just became empty, but keep Logs/ and TestResults/.
foreach ($target in $targets) {
    $dir = Join-Path $root $target.Folder
    if (-not (Test-Path $dir)) { continue }
    Get-ChildItem -LiteralPath $dir -Recurse -Directory -ErrorAction SilentlyContinue |
        Sort-Object -Property FullName -Descending |
        ForEach-Object {
            if (-not (Get-ChildItem -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue)) {
                Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
            }
        }
}

Write-Host ''
if ($failed -eq 0) {
    Write-Ok "Deleted $($planned.Count) file(s). Library/, Builds/, and all authored content are untouched."
    exit 0
}

Add-Failure "$failed file(s) could not be deleted (likely still open in the editor or another process)."
exit (Write-Summary)
