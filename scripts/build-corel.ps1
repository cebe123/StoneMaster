param(
    [string]$CorelInteropPath = "",
    [switch]$SkipRestore = $false
)

$ErrorActionPreference = "Stop"

# Renkli çıktı için helper fonksiyon
function Write-Step {
    param([string]$Message, [string]$Color = "Cyan")
    Write-Host "`n=== $Message ===" -ForegroundColor $Color
}

Write-Step "StoneMaster Corel Add-on Derlemesi Başlatılıyor"

# CorelInteropPath kontrolü
if ([string]::IsNullOrEmpty($CorelInteropPath)) {
    # Varsayılan yolları dene
    $defaultPaths = @(
        "C:\Program Files\Corel\CorelDRAW Graphics Suite 2024\Programs64\Corel.Interop.VGCore.dll",
        "C:\Program Files (x86)\Corel\CorelDRAW Graphics Suite 2023\Programs64\Corel.Interop.VGCore.dll",
        "C:\Program Files\Corel\CorelDRAW Graphics Suite 2023\Programs64\Corel.Interop.VGCore.dll",
        "${env:ProgramFiles}\Corel\CorelDRAW Graphics Suite 2024\Programs64\Corel.Interop.VGCore.dll",
        "${env:ProgramFiles(x86)}\Corel\CorelDRAW Graphics Suite 2023\Programs64\Corel.Interop.VGCore.dll",
        "$env:USERPROFILE\Desktop\Corel.Interop.VGCore.dll",
        "$env:USERPROFILE\OneDrive\Desktop\Corel.Interop.VGCore.dll",
        "$env:USERPROFILE\OneDrive\Masaüstü\Corel.Interop.VGCore.dll"
    )
    
    foreach ($path in $defaultPaths) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $CorelInteropPath = $path
            Write-Host "Corel.Interop.VGCore.dll bulundu: $CorelInteropPath" -ForegroundColor Green
            break
        }
    }
    
    if ([string]::IsNullOrEmpty($CorelInteropPath) -or -not (Test-Path -LiteralPath $CorelInteropPath -PathType Leaf)) {
        Write-Host "`nHATA: Corel.Interop.VGCore.dll bulunamadı." -ForegroundColor Red
        Write-Host "Lütfen şunlardan birini yapın:" -ForegroundColor Yellow
        Write-Host "  1. CorelDRAW'ı yükleyin" -ForegroundColor Yellow
        Write-Host "  2. Dosyanın yolunu belirtin: .\build-corel.ps1 -CorelInteropPath 'C:\path\to\Corel.Interop.VGCore.dll'" -ForegroundColor Yellow
        Write-Host "`nÖrnek kullanım:" -ForegroundColor Cyan
        Write-Host "  .\scripts\build-corel.ps1 -CorelInteropPath 'C:\Users\adnan\OneDrive\Masaüstü\Corel.Interop.VGCore.dll'" -ForegroundColor White
        throw "Corel.Interop.VGCore.dll bulunamadı"
    }
} elseif (-not (Test-Path -LiteralPath $CorelInteropPath -PathType Leaf)) {
    throw "Belirtilen Corel.Interop.VGCore.dll bulunamadı: $CorelInteropPath"
}

# Corel Add-on build
$solution = "$PSScriptRoot\..\corel\StoneMaster.Corel\StoneMaster.Corel.csproj"

if (-not (Test-Path $solution)) {
    throw "Corel solution dosyası bulunamadı: $solution"
}

Write-Step "Corel Add-on Derleniyor"

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$buildSuccess = $false

$restoreFlag = ""
if ($SkipRestore) {
    $restoreFlag = "/p:RestorePackages=false"
    Write-Host "Paket geri yükleme atlandı." -ForegroundColor Yellow
}

if ($null -ne $dotnetCommand) {
    Write-Host "dotnet MSBuild kullanılıyor..." -ForegroundColor Gray
    if ($SkipRestore) {
        & $dotnetCommand.Source msbuild $solution /t:Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath" $restoreFlag /v:minimal
    } else {
        & $dotnetCommand.Source msbuild $solution /t:Restore,Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath" /v:minimal
    }
    $buildSuccess = ($LASTEXITCODE -eq 0)
}
else {
    $msbuildCommand = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($null -ne $msbuildCommand) {
        Write-Host "MSBuild kullanılıyor..." -ForegroundColor Gray
        if ($SkipRestore) {
            & $msbuildCommand.Source $solution /t:Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath" $restoreFlag /v:minimal
        } else {
            & $msbuildCommand.Source $solution /t:Restore,Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath" /v:minimal
        }
        $buildSuccess = ($LASTEXITCODE -eq 0)
    }
    else {
        $vsBuildToolsMsbuild = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
        if (Test-Path -LiteralPath $vsBuildToolsMsbuild -PathType Leaf) {
            Write-Host "Visual Studio BuildTools MSBuild kullanılıyor..." -ForegroundColor Gray
            if ($SkipRestore) {
                & $vsBuildToolsMsbuild $solution /t:Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath" $restoreFlag /v:minimal
            } else {
                & $vsBuildToolsMsbuild $solution /t:Restore,Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath" /v:minimal
            }
            $buildSuccess = ($LASTEXITCODE -eq 0)
        }
        else {
            throw "MSBuild bulunamadı. Visual Studio 2022 Build Tools kurun veya dotnet SDK yükleyin."
        }
    }
}

if (-not $buildSuccess) {
    throw "Corel add-on derlemesi başarısız oldu."
}

# Artifact klasörünü hazırla
Write-Step "Artifact'lar Hazırlanıyor"
$dockerDest = "$PSScriptRoot\..\artifacts\StoneMaster.Corel"
New-Item -ItemType Directory -Force -Path $dockerDest | Out-Null

$dockerBuildOutput = "$PSScriptRoot\..\corel\StoneMaster.Corel\bin\x64\Release\net48"

if (-not (Test-Path "$dockerBuildOutput\StoneMaster.Corel.dll")) {
    throw "Derleme çıktısı bulunamadı: $dockerBuildOutput\StoneMaster.Corel.dll"
}

Copy-Item "$dockerBuildOutput\StoneMaster.Corel.dll" "$dockerDest\StoneMaster.Corel.dll" -Force

# Manifest dosyalarını kopyala
$manifestPath = "$PSScriptRoot\..\corel\StoneMaster.Corel\Manifest"
if (Test-Path $manifestPath) {
    Copy-Item "$manifestPath\AppUI.xslt" "$dockerDest\AppUI.xslt" -Force -ErrorAction SilentlyContinue
    Copy-Item "$manifestPath\UserUI.xslt" "$dockerDest\UserUI.xslt" -Force -ErrorAction SilentlyContinue
    Copy-Item "$manifestPath\Coreldrw.addon" "$dockerDest\Coreldrw.addon" -Force -ErrorAction SilentlyContinue
}

# Corel.Interop.VGCore.dll artifact'ta olmamalı
if (Test-Path -LiteralPath "$dockerDest\Corel.Interop.VGCore.dll") {
    Remove-Item "$dockerDest\Corel.Interop.VGCore.dll" -Force
    Write-Host "Corel.Interop.VGCore.dll artifact'tan kaldırıldı." -ForegroundColor Yellow
}

Write-Step "Corel Build Başarıyla Tamamlandı!" -Color Green
Write-Host "Çıktı klasörü: $dockerDest" -ForegroundColor Green
Write-Host "`nNot: Engine bileşeni bu script ile derlenmez. Tüm proje için build.ps1 kullanın." -ForegroundColor Cyan
