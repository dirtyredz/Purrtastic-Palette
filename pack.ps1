<#
    Builds a release archive laid out the way Nexus and Vortex expect:

        BepInEx/plugins/PurrtasticPalette/PurrtasticPalette.dll

    Deliberately not the dev deploy path (plugins/MoonlightPeaksMods/PurrtasticPalette), which
    only exists to keep hand-built DLLs clear of Vortex during development.

    There is no test project. Every code path reads Unity and game types (the wardrobe screen,
    the customization preview rig, the inventory), so a console runner could not exercise
    anything meaningful. Verification is the in-game checklist in RELEASING.md.
#>

$ErrorActionPreference = 'Stop'

# This mod is its own repo, so the mod root and the repo root are the same directory.
$modRoot  = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = $modRoot
$project  = Join-Path $modRoot 'src\PurrtasticPalette.csproj'

# Single source of truth for the version, so the archive can never disagree with the DLL.
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ }
if (-not $version) { throw "Could not read <Version> from $project" }

Write-Host "Packing Purrtastic Palette $version"

# SkipDeploy keeps a release build from overwriting the copy under test in the game folder.
dotnet build $project -c Release -p:SkipDeploy=true
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$dll = Join-Path $modRoot 'src\bin\Release\netstandard2.1\PurrtasticPalette.dll'
if (-not (Test-Path $dll)) { throw "Built DLL not found at $dll" }

$staging = Join-Path $env:TEMP "PurrtasticPalette-pack-$([guid]::NewGuid().ToString('N'))"
$target  = Join-Path $staging 'BepInEx\plugins\PurrtasticPalette'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item $dll $target

$dist = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$archive = Join-Path $dist "PurrtasticPalette-$version.zip"
if (Test-Path $archive) { Remove-Item $archive }

Compress-Archive -Path (Join-Path $staging 'BepInEx') -DestinationPath $archive
Remove-Item $staging -Recurse -Force

Write-Host "Created $archive"
Write-Host 'Extract it over the game folder to install.'
