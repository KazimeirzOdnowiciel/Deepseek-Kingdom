# Runs the deterministic gate over everything in the repository and fails if any of it fails.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-all.ps1
#
# It covers every plugin in src/ AND the template, because the template is the artifact everything
# else is copied from and it lives outside src/ - without this it would be the one thing in the
# repository that nothing ever builds.
#
# Each target prints its own JSON, then a summary line, which is the last line of output.

param(
    [switch] $SkipFormat,
    [switch] $SkipTemplate
)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root 'src'
$check = Join-Path $PSScriptRoot 'check.ps1'

$plugins = @()
if (Test-Path $src) {
    $plugins = @(Get-ChildItem $src -Directory |
        Where-Object { $_.Name -notlike '*.Tests' -and $_.Name -notlike '*.Previews' -and (Test-Path (Join-Path $_.FullName "$($_.Name).csproj")) } |
        Sort-Object Name)
}

# Re-invoke this same host so check.ps1's `exit` codes are reported per target instead of
# terminating the whole sweep.
$exe = (Get-Process -Id $PID).Path
$failed = 0
$checked = 0

function Invoke-Gate([string] $targetName, [string] $base) {
    Write-Host ""
    Write-Host "=== $targetName ==="

    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $check, '-Name', $targetName, '-Base', $base)
    if ($SkipFormat) { $arguments += '-SkipFormat' }

    & $exe @arguments 2>&1 | ForEach-Object { Write-Host $_ }
    return ($LASTEXITCODE -eq 0)
}

foreach ($plugin in $plugins) {
    $checked++
    if (-not (Invoke-Gate $plugin.Name 'src')) { $failed++ }
}

if (-not $SkipTemplate) {
    $templateBase = 'templates/new-plugin'
    if (Test-Path (Join-Path $root "$templateBase/PluginName/PluginName.csproj")) {
        $checked++
        Write-Host ""
        Write-Host "(the template, so a broken scaffold cannot ship)"
        if (-not (Invoke-Gate 'PluginName' $templateBase)) { $failed++ }
    }
}

if ($checked -eq 0) {
    Write-Host "nothing to check: no plugins in src/ and no template"
}

Write-Host ""
"{`"targets`":$checked,`"failed`":$failed,`"ok`":$(if ($failed -eq 0) { 'true' } else { 'false' })}"

if ($failed -eq 0) { exit 0 } else { exit 1 }
