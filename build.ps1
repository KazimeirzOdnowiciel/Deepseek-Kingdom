param(
    [string] $BaseUrl = "https://raw.githubusercontent.com/KazimeirzOdnowiciel/Deepseek-Kingdom/main",
    [switch] $NoBuild
)

# Rebuilds every listed plugin, copies its release zip into dist/, and regenerates repo.json.
# Run from anywhere: paths are relative to this script.
#
# A plugin ships only because it appears in $plugins below. Being in src/ does not publish it,
# which is the whole point: a plugin under construction is invisible to users until the line is
# added here deliberately (CONTRIBUTING.md P1).

$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$dist = Join-Path $root "dist"

New-Item -ItemType Directory -Force -Path $dist | Out-Null

# Folder = directory under src/. Project = its csproj. Add one line per plugin to publish it.
#
# Written as += on purpose: a one-element array literal written as @( item, ) is a PARSE ERROR in
# PowerShell, so the "add a line, leave the comma" shape breaks the moment the first plugin is
# added and nothing has been published yet to notice.
$plugins = @()
# $plugins += @{ Folder = "FooBar"; Project = "FooBar.csproj" }

$parent = Join-Path $root "src"
$entries = @()

foreach ($plugin in $plugins)
{
    $folder = Join-Path $parent $plugin.Folder
    $project = Join-Path $folder $plugin.Project
    $name = [System.IO.Path]::GetFileNameWithoutExtension($plugin.Project)

    if (-not (Test-Path $project))
    {
        Write-Warning "missing project: $project"
        continue
    }

    if (-not $NoBuild)
    {
        Write-Host "building $name..."
        & dotnet build $project -c Release -v q | Out-Null

        if ($LASTEXITCODE -ne 0)
        {
            throw "build failed for $name"
        }
    }

    $output = Join-Path $folder "bin\Release\$name"
    $zip = Join-Path $output "latest.zip"
    $manifestPath = Join-Path $output "$name.json"

    if (-not (Test-Path $zip) -or -not (Test-Path $manifestPath))
    {
        Write-Warning "no package output for $name"
        continue
    }

    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $target = Join-Path $dist "$($manifest.InternalName)-$($manifest.AssemblyVersion).zip"

    Copy-Item $zip $target -Force
    Write-Host "  packaged $($manifest.InternalName).zip ($([math]::Round((Get-Item $target).Length / 1kb)) KB)"

    # Version in the path: a CDN cannot serve a previous release for a new version.
    $tag = $manifest.AssemblyVersion
    $link = "$BaseUrl/dist/$($manifest.InternalName)-$tag.zip"

    $iconPath = Join-Path $root "icons/$($manifest.InternalName).png"
    $iconUrl = ""

    if (Test-Path $iconPath)
    {
        New-Item -ItemType Directory -Force -Path (Join-Path $dist "icons") | Out-Null
        Copy-Item $iconPath (Join-Path $dist "icons/$($manifest.InternalName)-$tag.png") -Force
        $iconUrl = "$BaseUrl/dist/icons/$($manifest.InternalName)-$tag.png"
    }

    # The installed-plugin list reads the manifest inside the zip, not the repo entry, so the icon
    # has to live in there too or it vanishes the moment the plugin is installed.
    if ($iconUrl -ne "")
    {
        Add-Type -AssemblyName System.IO.Compression.FileSystem

        $staging = Join-Path ([System.IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString("N"))
        New-Item -ItemType Directory -Path $staging | Out-Null
        [System.IO.Compression.ZipFile]::ExtractToDirectory($target, $staging)

        $innerManifest = Join-Path $staging "$($manifest.InternalName).json"

        if (Test-Path $innerManifest)
        {
            $inner = Get-Content $innerManifest -Raw | ConvertFrom-Json
            $inner | Add-Member -NotePropertyName IconUrl -NotePropertyValue $iconUrl -Force
            $innerJson = ConvertTo-Json -InputObject $inner -Depth 6
            [System.IO.File]::WriteAllText($innerManifest, $innerJson, (New-Object System.Text.UTF8Encoding($false)))

            Remove-Item $target -Force
            [System.IO.Compression.ZipFile]::CreateFromDirectory(
                $staging, $target, [System.IO.Compression.CompressionLevel]::Optimal, $false)
        }

        Remove-Item $staging -Recurse -Force
    }

    $entries += [ordered]@{
        Author             = $manifest.Author
        Name               = $manifest.Name
        InternalName       = $manifest.InternalName
        AssemblyVersion    = $manifest.AssemblyVersion
        Description        = $manifest.Description
        ApplicableVersion  = $manifest.ApplicableVersion
        RepoUrl            = $manifest.RepoUrl
        Tags               = $manifest.Tags
        DalamudApiLevel    = $manifest.DalamudApiLevel
        LoadRequiredState  = 0
        LoadSync           = $false
        CanUnloadAsync     = $false
        LoadPriority       = 0
        Punchline          = $manifest.Punchline
        AcceptsFeedback    = $true
        DownloadLinkInstall = $link
        DownloadLinkUpdate  = $link
        DownloadLinkTesting = $link
        IconUrl            = $iconUrl
        IsHide             = $false
        IsTestingExclusive = $false
        LastUpdate         = [int][DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    }
}

if ($plugins.Count -gt 0 -and $entries.Count -eq 0)
{
    throw "nothing was packaged"
}

# An empty repository is a valid state: it publishes an empty index rather than failing, so the
# first plugin does not have to be committed together with the automation.
$json = if ($entries.Count -eq 0) { "[]" } else { ConvertTo-Json -InputObject $entries -Depth 6 }
[System.IO.File]::WriteAllText((Join-Path $root "repo.json"), $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ""
Write-Host "repo.json written with $($entries.Count) plugin(s), links pointing at $BaseUrl"
