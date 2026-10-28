# Re-renders the UI goldens for one plugin.
#
# The deliberate counterpart to scripts/check.ps1 step 4: that one compares, this one writes. Run it
# after an intended UI change, LOOK at the images it writes, then commit them (CONTRIBUTING.md T6).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\render-goldens.ps1 -Name FooBar

param(
    [Parameter(Mandatory = $true)]
    [string] $Name
)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$previews = Join-Path $root "src/$Name.Previews/$Name.Previews.csproj"
$goldens = Join-Path $root "src/$Name.Previews/goldens"
$dll = Join-Path $root "src/$Name.Previews/bin/Release/$Name.Previews.dll"

if (-not (Test-Path $previews)) {
    Write-Host "no previews project at $previews"
    exit 2
}

& dotnet build $previews -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "the previews project did not build"
    exit 1
}

if (-not (Test-Path $dll)) {
    Write-Host "no previews assembly at $dll"
    exit 1
}

$toolArgs = @('--assembly', $dll, '--compare', $goldens, '--update', '--warp')

# Same resolution as check.ps1: the repository pins the tool, and where a local manifest restore is
# refused (a Windows mark-of-the-web on dotnet-tools.json) the global install is used instead.
$globalTool = Join-Path $env:USERPROFILE '.dotnet/tools/glasswork-preview.exe'
if (Test-Path $globalTool) {
    & $globalTool @toolArgs
}
else {
    & dotnet glasswork-preview @toolArgs
}

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Goldens rewritten in $goldens. Look at each PNG, then commit it with the change."
}

exit $LASTEXITCODE
