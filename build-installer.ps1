# ============================================================
#  CaniveteSuico — Build + Package installer with Velopack
# ============================================================
#
#  Usage (from the repo root):
#    .\build-installer.ps1
#    .\build-installer.ps1 -Version 1.2.0
#
#  Prerequisites:
#    dotnet tool install -g vpk
#
#  Output:
#    ./releases/
#      CaniveteSuico-Setup.exe           ← installer for first-time users
#      CaniveteSuico-1.x.x-*.nupkg      ← delta/full update packages
#      RELEASES-win-x64                  ← update manifest
#
#  To publish an update on GitHub:
#    1. Bump -Version below (or pass it as a parameter)
#    2. Run this script
#    3. Create a GitHub Release and upload all files from ./releases/
# ============================================================

param(
    [string]$Version = "",
    [string]$Icon    = ""      # optional: path to .ico file
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Resolve paths ────────────────────────────────────────────────────────────
$Root      = $PSScriptRoot
$AppDir    = Join-Path $Root "CaniveteSuico.App"
$PublishDir = Join-Path $Root "publish"
$ReleasesDir = Join-Path $Root "releases"
$Csproj    = Join-Path $AppDir "CaniveteSuico.App.csproj"

# ── Read version from .csproj if not passed ──────────────────────────────────
if (-not $Version) {
    [xml]$proj = Get-Content $Csproj
    $Version = $proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if (-not $Version) {
        Write-Error 'Could not read Version from .csproj. Use: .\build-installer.ps1 -Version 1.0.0'
        exit 1
    }
}

Write-Host ""
Write-Host "==== CaniveteSuico - installer build ====" -ForegroundColor Cyan
Write-Host ('  Version : ' + $Version) -ForegroundColor White
Write-Host ('  Project : ' + $AppDir) -ForegroundColor Gray
Write-Host ('  Output  : ' + $ReleasesDir) -ForegroundColor Gray
Write-Host ""

# -- Clean previous publish ---------------------------------------------------
if (Test-Path $PublishDir) {
    Write-Host "[*] Cleaning publish folder..." -ForegroundColor Yellow
    Remove-Item $PublishDir -Recurse -Force
}

# -- dotnet publish (self-contained, win-x64) --------------------------------
Write-Host "[*] dotnet publish (Release, win-x64, self-contained)..." -ForegroundColor Cyan

dotnet publish "$Csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output "$PublishDir" `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    Write-Error ("dotnet publish failed (exit " + $LASTEXITCODE + ").")
    exit $LASTEXITCODE
}

# Copy optional runtime tools (yt-dlp, ffmpeg, pandoc) into publish output when present.
$ToolsSrc = Join-Path $AppDir "tools"
$ToolsDst = Join-Path $PublishDir "tools"
if (Test-Path $ToolsSrc) {
    Write-Host "[*] Copying tools/ into publish output..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $ToolsDst -Force | Out-Null
    Copy-Item -Path (Join-Path $ToolsSrc '*') -Destination $ToolsDst -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "  [ok] Publish done." -ForegroundColor Green

# -- Ensure releases dir exists -----------------------------------------------
if (-not (Test-Path $ReleasesDir)) {
    New-Item -ItemType Directory -Path $ReleasesDir | Out-Null
}

# Clean previous Velopack artifacts so the same version can be rebuilt locally
if (Test-Path $ReleasesDir) {
    Remove-Item (Join-Path $ReleasesDir '*') -Force -Recurse -ErrorAction SilentlyContinue
}

# -- vpk pack -----------------------------------------------------------------
Write-Host "[*] vpk pack (Velopack)..." -ForegroundColor Cyan

$vpkArgs = @(
    "pack"
    "--packId",    "CaniveteSuico"
    "--packVersion", $Version
    "--packDir",   $PublishDir
    "--mainExe",   "CaniveteSuico.App.exe"
    "--outputDir", $ReleasesDir
    "--packTitle", "Canivete Suíço"
)

# Velopack setup shortcut icon (defaults to Assets\app.ico when present)
if (-not $Icon) {
    $Icon = Join-Path $AppDir 'Assets\app.ico'
}
if ($Icon -and (Test-Path $Icon)) {
    $vpkArgs += '--icon', $Icon
    Write-Host ('  Icon: ' + $Icon) -ForegroundColor Gray
}

& vpk @vpkArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error ("vpk pack failed (exit " + $LASTEXITCODE + ").")
    exit $LASTEXITCODE
}

# -- Summary ----------------------------------------------------------------
Write-Host ""
Write-Host "=== Build OK ===" -ForegroundColor Green
Write-Host ""
Write-Host ('  Folder: ' + $ReleasesDir) -ForegroundColor White
Write-Host ""
Get-ChildItem $ReleasesDir | ForEach-Object {
    $size = "{0:N0} KB" -f ($_.Length / 1KB)
    Write-Host ("  {0,-50} {1,10}" -f $_.Name, $size) -ForegroundColor Gray
}
Write-Host ""
Write-Host '  Next steps:' -ForegroundColor Yellow
Write-Host ('    1. GitHub Release tag: v' + $Version)
Write-Host '    2. Upload ALL files from releases/ folder to the release assets'
Write-Host '    3. Give users the generated Setup.exe'
Write-Host ""
