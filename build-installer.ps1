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
        Write-Error "Não foi possível ler <Version> do .csproj. Passe -Version 1.x.x."
        exit 1
    }
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  CaniveteSuico  •  Build do instalador   ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host "  Versão  : $Version" -ForegroundColor White
Write-Host "  Projeto : $AppDir" -ForegroundColor Gray
Write-Host "  Saída   : $ReleasesDir" -ForegroundColor Gray
Write-Host ""

# ── Clean previous publish ───────────────────────────────────────────────────
if (Test-Path $PublishDir) {
    Write-Host "→ Limpando publish anterior…" -ForegroundColor Yellow
    Remove-Item $PublishDir -Recurse -Force
}

# ── dotnet publish (self-contained, win-x64) ─────────────────────────────────
Write-Host "→ Publicando .NET app (self-contained, win-x64)…" -ForegroundColor Cyan

dotnet publish "$Csproj" `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output "$PublishDir" `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish falhou (exit $LASTEXITCODE)."
    exit $LASTEXITCODE
}

Write-Host "  ✓ Publicação concluída." -ForegroundColor Green

# ── Ensure releases dir exists ───────────────────────────────────────────────
if (-not (Test-Path $ReleasesDir)) {
    New-Item -ItemType Directory -Path $ReleasesDir | Out-Null
}

# ── vpk pack ─────────────────────────────────────────────────────────────────
Write-Host "→ Empacotando com Velopack…" -ForegroundColor Cyan

$vpkArgs = @(
    "pack"
    "--packId",    "CaniveteSuico"
    "--packVersion", $Version
    "--packDir",   $PublishDir
    "--mainExe",   "CaniveteSuico.App.exe"
    "--outputDir", $ReleasesDir
    "--packTitle", "CaniveteSuico"
)

# Add icon if provided and exists
if ($Icon -and (Test-Path $Icon)) {
    $vpkArgs += "--icon", $Icon
    Write-Host "  Ícone    : $Icon" -ForegroundColor Gray
}

& vpk @vpkArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "vpk pack falhou (exit $LASTEXITCODE)."
    exit $LASTEXITCODE
}

# ── Summary ───────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║            Build concluído! ✓            ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  Arquivos gerados em: $ReleasesDir" -ForegroundColor White
Write-Host ""
Get-ChildItem $ReleasesDir | ForEach-Object {
    $size = "{0:N0} KB" -f ($_.Length / 1KB)
    Write-Host ("  {0,-50} {1,10}" -f $_.Name, $size) -ForegroundColor Gray
}
Write-Host ""
Write-Host "  Próximos passos:" -ForegroundColor Yellow
Write-Host "    1. Crie uma Release no GitHub com a tag v$Version"
Write-Host "    2. Faça upload de TODOS os arquivos da pasta releases/"
Write-Host "    3. Distribua o CaniveteSuico-Setup.exe para novos usuários"
Write-Host ""
