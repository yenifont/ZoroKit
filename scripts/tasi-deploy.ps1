# ZoroKit: Veritabani ve ayarlari yollari koruyarak deploy klasorune kopyala
# Kullanim:
#   CD C:\ZoroKit
#   powershell -ExecutionPolicy Bypass -File "D:\Zoragon\scripts\tasi-deploy.ps1" -ZoroKitRoot "C:\ZoroKit"
# veya ZoroKit icinde:  .\tasi-deploy.ps1 -ZoroKitRoot "C:\ZoroKit"

param(
    [Parameter(Mandatory=$false)]
    [string]$ZoroKitRoot = "",
    [string]$DeployRoot = ""
)

if ([string]::IsNullOrWhiteSpace($ZoroKitRoot)) {
    $ZoroKitRoot = (Get-Location).Path
}
if ([string]::IsNullOrWhiteSpace($DeployRoot)) {
    $DeployRoot = Join-Path $ZoroKitRoot "deploy"
}

$ErrorActionPreference = "Stop"
Write-Host "ZoroKit tasi-deploy: DB + config + www yollari korunur." -ForegroundColor Cyan

# Tasinacak klasorler (yollar korunur: deploy\mariadb, deploy\config, ...)
$Klasorler = @(
    "mariadb",   # Veritabani dosyalari (DB burada)
    "config",    # zoragon.json, versions.json, apache/php/mariadb conf, sites-enabled, alias
    "www",       # Projeler, document root
    "apps",      # phpMyAdmin vb.
    "backups",   # Veritabani yedekleri
    "logs"       # Opsiyonel: hata loglari
)

Write-Host "ZoroKit kok: $ZoroKitRoot" -ForegroundColor Cyan
Write-Host "Hedef (deploy): $DeployRoot" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $ZoroKitRoot)) {
    Write-Error "ZoroKit kok bulunamadi: $ZoroKitRoot"
}

New-Item -ItemType Directory -Path $DeployRoot -Force | Out-Null

foreach ($dir in $Klasorler) {
    $kaynak = Join-Path $ZoroKitRoot $dir
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
Write-Host "Programi yeniden kurduktan sonra bu klasorleri C:\ZoroKit (veya yeni kurulum yoluna) geri kopyalayabilirsiniz." -ForegroundColor Cyan
