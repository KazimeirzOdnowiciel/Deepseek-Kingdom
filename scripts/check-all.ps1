# Runs the deterministic gate over every plugin in src/ and fails if any of them fails.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-all.ps1
#
# Used by CI and worth running before a push. Each plugin's result is printed as JSON, then a
# summary line, which is the last line of output.

param(
    [switch] $SkipFormat
)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src'
$check = Join-Path $PSScriptRoot 'check.ps1'

if (-not (Test-Path $src)) {
    Write-Host "no src/ directory yet - nothing to check"
    '{"plugins":0,"failed":0,"ok":true}'
    exit 0
}

$plugins = Get-ChildItem $src -Directory |
    Where-Object { $_.Name -notlike '*.Tests' -and (Test-Path (Join-Path $_.FullName "$($_.Name).csproj")) } |
    Sort-Object Name

if (-not $plugins) {
    Write-Host "no plugins in src/ - nothing to check"
    '{"plugins":0,"failed":0,"ok":true}'
    exit 0
}

# Re-invoke this same host so check.ps1's `exit` codes are reported per plugin instead of
# terminating the whole sweep.
$exe = (Get-Process -Id $PID).Path
$failed = 0

foreach ($plugin in $plugins) {
    Write-Host ""
    Write-Host "=== $($plugin.Name) ==="

    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $check, '-Name', $plugin.Name)
    if ($SkipFormat) { $arguments += '-SkipFormat' }

    $output = & $exe @arguments 2>&1
    $output | ForEach-Object { Write-Host $_ }

    if ($LASTEXITCODE -ne 0) { $failed++ }
}

Write-Host ""
"{`"plugins`":$($plugins.Count),`"failed`":$failed,`"ok`":$(if ($failed -eq 0) { 'true' } else { 'false' })}"

if ($failed -eq 0) { exit 0 } else { exit 1 }
