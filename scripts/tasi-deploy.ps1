# ZaraGON: Veritabani ve ayarlari yollari koruyarak deploy klasorune kopyala
# Kullanim:
#   CD C:\ZaraGON
#   powershell -ExecutionPolicy Bypass -File "D:\Zoragon\scripts\tasi-deploy.ps1" -ZaragonRoot "C:\ZaraGON"
# veya ZaraGON icinde:  .\tasi-deploy.ps1 -ZaragonRoot "C:\ZaraGON"

param(
    [Parameter(Mandatory=$false)]
    [string]$ZaragonRoot = "",
    [string]$DeployRoot = ""
)

if ([string]::IsNullOrWhiteSpace($ZaragonRoot)) {
    $ZaragonRoot = (Get-Location).Path
}
if ([string]::IsNullOrWhiteSpace($DeployRoot)) {
    $DeployRoot = Join-Path $ZaragonRoot "deploy"
}

$ErrorActionPreference = "Stop"
Write-Host "ZaraGON tasi-deploy: DB + config + www yollari korunur." -ForegroundColor Cyan

# Tasinacak klasorler (yollar korunur: deploy\mariadb, deploy\config, ...)
$Klasorler = @(
    "mariadb",   # Veritabani dosyalari (DB burada)
    "config",    # zoragon.json, versions.json, apache/php/mariadb conf, sites-enabled, alias
    "www",       # Projeler, document root
    "apps",      # phpMyAdmin vb.
    "backups",   # Veritabani yedekleri
    "logs"       # Opsiyonel: hata loglari
)

Write-Host "ZaraGON kok: $ZaragonRoot" -ForegroundColor Cyan
Write-Host "Hedef (deploy): $DeployRoot" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $ZaragonRoot)) {
    Write-Error "ZaraGON kok bulunamadi: $ZaragonRoot"
}

New-Item -ItemType Directory -Path $DeployRoot -Force | Out-Null

foreach ($dir in $Klasorler) {
    $kaynak = Join-Path $ZaragonRoot $dir
    $hedef  = Join-Path $DeployRoot $dir

    if (Test-Path $kaynak) {
        Write-Host "Kopyalaniyor: $dir\ ..." -ForegroundColor Yellow
        if (Test-Path $hedef) {
            Remove-Item -Path $hedef -Recurse -Force
        }
        Copy-Item -Path $kaynak -Destination $hedef -Recurse -Force
        Write-Host "  -> $hedef" -ForegroundColor Green
    } else {
        Write-Host "Atlandi (yok): $dir" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "Tamamlandi. Yollar korundu: deploy\mariadb, deploy\config, deploy\www, ..." -ForegroundColor Green
Write-Host "Programi yeniden kurduktan sonra bu klasorleri C:\ZaraGON (veya yeni kurulum yoluna) geri kopyalayabilirsiniz." -ForegroundColor Cyan
