# Deterministic gate for one plugin. See CONTRIBUTING.md T3.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Name SlotYield
#
# Prints a JSON summary as the last line and exits 0 only when every gate passed.
# This checks compilation, tests and formatting. It cannot verify anything that needs
# the game running - that is the human's in-game checklist (CONTRIBUTING.md T2).

param(
    [Parameter(Mandatory = $true)]
    [string] $Name,

    [switch] $SkipFormat
)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$plugin = Join-Path $root "src/$Name/$Name.csproj"
$tests = Join-Path $root "src/$Name.Tests/$Name.Tests.csproj"

if (-not (Test-Path $plugin)) {
    Write-Host "no plugin project at $plugin"
    exit 2
}

$summary = [ordered]@{
    plugin = $Name
    build = $null
    tests = 'absent'
    format = 'skipped'
}

Write-Host "[1/3] build $Name (warnings are errors for strict projects)"
& dotnet build $plugin -c Release -v q --nologo
$summary.build = ($LASTEXITCODE -eq 0)

if (Test-Path $tests) {
    Write-Host "[2/3] tests $Name.Tests"
    # dotnet test cannot start here (no process handle, no named pipe). Running the xunit v3
    # executable directly is the portable path; CONTRIBUTING.md T4.
    & dotnet build $tests -c Debug -v q --nologo
    if ($LASTEXITCODE -eq 0) {
        & dotnet run --project $tests -c Debug --no-build
        $summary.tests = if ($LASTEXITCODE -eq 0) { 'passed' } else { 'failed' }
    }
    else {
        $summary.tests = 'failed-to-build'
    }
}
else {
    Write-Host "[2/3] tests: no test project at src/$Name.Tests"
}

if (-not $SkipFormat) {
    Write-Host "[3/3] format (scoped to this project only - never run repo-wide)"
    & dotnet format $plugin --verify-no-changes
    $summary.format = if ($LASTEXITCODE -eq 0) { 'clean' } else { 'dirty' }
}

$summary.ok = ($summary.build -eq $true) -and
              ($summary.tests -in @('passed', 'absent')) -and
              ($summary.format -in @('clean', 'skipped'))

Write-Host ""
$summary | ConvertTo-Json -Compress

if ($summary.ok) { exit 0 } else { exit 1 }
