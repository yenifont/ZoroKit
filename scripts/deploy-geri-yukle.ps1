# ZoroKit: deploy klasorundeki verileri kurulum kokune geri kopyala
# Kullanim: .\deploy-geri-yukle.ps1 -DeployRoot "C:\ZoroKit\deploy" -ZoroKitRoot "C:\ZoroKit"

param(
    [string]$DeployRoot = (Join-Path (Get-Location).Path "deploy"),
    [string]$ZoroKitRoot = "C:\ZoroKit"
)

$ErrorActionPreference = "Stop"

$Klasorler = @("mariadb", "config", "www", "apps", "backups", "logs")

Write-Host "Deploy (kaynak): $DeployRoot" -ForegroundColor Cyan
Write-Host "ZoroKit (hedef): $ZoroKitRoot" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $DeployRoot)) {
    Write-Error "Deploy klasoru bulunamadi: $DeployRoot"
}

foreach ($dir in $Klasorler) {
    $kaynak = Join-Path $DeployRoot $dir
    $hedef  = Join-Path $ZoroKitRoot $dir

    if (Test-Path $kaynak) {
        Write-Host "Geri yukleniyor: $dir\ ..." -ForegroundColor Yellow
        if (-not (Test-Path (Split-Path $hedef -Parent))) {
            New-Item -ItemType Directory -Path (Split-Path $hedef -Parent) -Force | Out-Null
        }
        Copy-Item -Path $kaynak -Destination $hedef -Recurse -Force
        Write-Host "  -> $hedef" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Geri yukleme tamamlandi." -ForegroundColor Green
