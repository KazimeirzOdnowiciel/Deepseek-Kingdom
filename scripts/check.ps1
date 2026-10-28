# Deterministic gate for one plugin. See CONTRIBUTING.md T3.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Name FooBar
#
# Prints a JSON summary as the last line and exits 0 only when every gate passed: build, unit
# tests, formatting, and - when the plugin has a previews project - the UI golden images.
# It cannot verify what needs the game running; that is the human's checklist (CONTRIBUTING.md T2).

param(
    [Parameter(Mandatory = $true)]
    [string] $Name,

    [switch] $SkipFormat,
    [switch] $SkipPreviews
)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$plugin = Join-Path $root "src/$Name/$Name.csproj"
$tests = Join-Path $root "src/$Name.Tests/$Name.Tests.csproj"
$previews = Join-Path $root "src/$Name.Previews/$Name.Previews.csproj"
$goldens = Join-Path $root "src/$Name.Previews/goldens"

if (-not (Test-Path $plugin)) {
    Write-Host "no plugin project at $plugin"
    exit 2
}

$summary = [ordered]@{
    plugin = $Name
    build = $null
    tests = 'absent'
    format = 'skipped'
    previews = 'absent'
}

Write-Host "[1/4] build $Name (warnings are errors for strict projects)"
& dotnet build $plugin -c Release -v q --nologo
$summary.build = ($LASTEXITCODE -eq 0)

if (Test-Path $tests) {
    Write-Host "[2/4] tests $Name.Tests"
    # dotnet test cannot start in every environment (no process handle, no named pipe for the
    # test host). Running the xunit v3 executable directly is the portable path; CONTRIBUTING.md T4.
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
    Write-Host "[2/4] tests: no test project at src/$Name.Tests"
}

if (-not $SkipFormat) {
    Write-Host "[3/4] format (scoped to this project only - never run repo-wide)"
    & dotnet format $plugin --verify-no-changes
    $summary.format = if ($LASTEXITCODE -eq 0) { 'clean' } else { 'dirty' }
}

if ($SkipPreviews) {
    Write-Host "[4/4] previews: skipped"
}
elseif (-not (Test-Path $previews)) {
    Write-Host "[4/4] previews: no previews project at src/$Name.Previews"
}
elseif (-not (Test-Path $goldens)) {
    Write-Host "[4/4] previews: no goldens in src/$Name.Previews/goldens"
    Write-Host "      render them once with --update, look at each, and commit them (CONTRIBUTING.md T6)"
    $summary.previews = 'missing-goldens'
}
else {
    Write-Host "[4/4] previews $Name.Previews against goldens (WARP, software rasterizer)"

    $built = $true
    & dotnet build $previews -c Release -v q --nologo
    if ($LASTEXITCODE -ne 0) { $built = $false }

    $dll = Join-Path $root "src/$Name.Previews/bin/Release/$Name.Previews.dll"
    if (-not (Test-Path $dll)) { $built = $false }

    if (-not $built) {
        $summary.previews = 'failed-to-build'
    }
    else {
        $out = Join-Path $root "artifacts/previews/$Name"
        $toolArgs = @('--assembly', $dll, '--compare', $goldens, '--out', $out, '--warp')

        # The repository pins the tool in dotnet-tools.json, so CI gets it from `dotnet tool
        # restore`. Where a local manifest restore is refused, the global install is used instead.
        $globalTool = Join-Path $env:USERPROFILE '.dotnet/tools/glasswork-preview.exe'
        if (Test-Path $globalTool) {
            & $globalTool @toolArgs
        }
        else {
            & dotnet glasswork-preview @toolArgs
        }

        $summary.previews = if ($LASTEXITCODE -eq 0) { 'passed' } else { 'mismatch' }

        if ($summary.previews -ne 'passed') {
            Write-Host ""
            Write-Host "Preview mismatch. The red pixels in $out/*.diff.png are what moved."
            Write-Host "If the change was intended, re-render and commit the new goldens:"
            Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File scripts/render-goldens.ps1 -Name $Name"
            Write-Host "Two other causes: the tool is missing (dotnet tool restore), or a Dalamud"
            Write-Host "update shifted cimgui and every pixel moved at once."
        }
    }
}

$summary.ok = ($summary.build -eq $true) -and
              ($summary.tests -in @('passed', 'absent')) -and
              ($summary.format -in @('clean', 'skipped')) -and
              ($summary.previews -in @('passed', 'absent'))

Write-Host ""
$summary | ConvertTo-Json -Compress

if ($summary.ok) { exit 0 } else { exit 1 }
