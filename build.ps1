# ZaraGON Build & Package Script
# Usage: powershell -ExecutionPolicy Bypass -File build.ps1

param(
    [switch]$SkipInstaller,
    [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$PublishDir = Join-Path $Root "publish"
$ProjectPath = Join-Path $Root "src\ZaraGON.UI\ZaraGON.UI.csproj"

Write-Host ""
Write-Host "=== ZaraGON Build ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean
Write-Host "[1/4] Temizleniyor..." -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
dotnet clean $ProjectPath -c Release --nologo -v q 2>$null

# Step 2: Publish
Write-Host "[2/4] Derleniyor ve yayinlaniyor (self-contained, single-file)..." -ForegroundColor Yellow
dotnet publish $ProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $PublishDir `
    --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host "HATA: Derleme basarisiz!" -ForegroundColor Red
    exit 1
}

# Step 3: Show output
$exe = Join-Path $PublishDir "ZaraGON.exe"
if (Test-Path $exe) {
    $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "[3/4] Derleme basarili! ZaraGON.exe = ${size} MB" -ForegroundColor Green
} else {
    Write-Host "HATA: ZaraGON.exe bulunamadi!" -ForegroundColor Red
    exit 1
}

# Step 4: Download VC++ Redistributable for bundling in installer
$VcRedistDir = Join-Path $Root "installer\deps"
$VcRedistExe = Join-Path $VcRedistDir "vc_redist.x64.exe"
if (-not (Test-Path $VcRedistExe)) {
    Write-Host "[4/5] VC++ Redistributable indiriliyor..." -ForegroundColor Yellow
    if (-not (Test-Path $VcRedistDir)) { New-Item -ItemType Directory -Path $VcRedistDir -Force | Out-Null }
    try {
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri "https://aka.ms/vs/17/release/vc_redist.x64.exe" -OutFile $VcRedistExe -UseBasicParsing
        $vcSize = [math]::Round((Get-Item $VcRedistExe).Length / 1MB, 1)
        Write-Host "  VC++ Redistributable indirildi (${vcSize} MB)" -ForegroundColor Green
    } catch {
        Write-Host "  UYARI: VC++ Redistributable indirilemedi: $_" -ForegroundColor Yellow
        Write-Host "  Manuel olarak $VcRedistExe yoluna koyabilirsiniz." -ForegroundColor Yellow
    }
} else {
    Write-Host "[4/5] VC++ Redistributable mevcut, atlaniyor." -ForegroundColor Green
}

# Step 5: Build installer (optional)
if (-not $SkipInstaller) {
    if (Test-Path $InnoSetupPath) {
        Write-Host "[5/5] Installer olusturuluyor..." -ForegroundColor Yellow
        $issPath = Join-Path $Root "installer\ZaraGON.iss"
        & $InnoSetupPath $issPath
        if ($LASTEXITCODE -eq 0) {
            $setupExe = Get-ChildItem (Join-Path $Root "installer\Output") -Filter "*.exe" | Select-Object -First 1
            if ($setupExe) {
                $setupSize = [math]::Round($setupExe.Length / 1MB, 1)
                Write-Host ""
                Write-Host "=== Tamamlandi ===" -ForegroundColor Green
                Write-Host "Installer: $($setupExe.FullName) (${setupSize} MB)" -ForegroundColor Green
            }
        } else {
            Write-Host "UYARI: Installer olusturulamadi." -ForegroundColor Yellow
        }
    } else {
        Write-Host "[5/5] Inno Setup bulunamadi: $InnoSetupPath" -ForegroundColor Yellow
        Write-Host "  Inno Setup 6 yukleyin: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
        Write-Host "  Veya -SkipInstaller ile calistin." -ForegroundColor Yellow
    }
} else {
    Write-Host "[5/5] Installer atlandi (-SkipInstaller)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Publish dizini: $PublishDir" -ForegroundColor Cyan
Write-Host ""
