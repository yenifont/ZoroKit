# ZaraGON: deploy klasorundeki verileri kurulum kokune geri kopyala
# Kullanim: .\deploy-geri-yukle.ps1 -DeployRoot "C:\ZaraGON\deploy" -ZaragonRoot "C:\ZaraGON"

param(
    [string]$DeployRoot = (Join-Path (Get-Location).Path "deploy"),
    [string]$ZaragonRoot = "C:\ZaraGON"
)

$ErrorActionPreference = "Stop"

$Klasorler = @("mariadb", "config", "www", "apps", "backups", "logs")

Write-Host "Deploy (kaynak): $DeployRoot" -ForegroundColor Cyan
Write-Host "ZaraGON (hedef): $ZaragonRoot" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $DeployRoot)) {
    Write-Error "Deploy klasoru bulunamadi: $DeployRoot"
}

foreach ($dir in $Klasorler) {
    $kaynak = Join-Path $DeployRoot $dir
    $hedef  = Join-Path $ZaragonRoot $dir

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
