param(
    [string]$CorelInteropPath = "",
    [switch]$SkipEngineBuild = $false
)

$ErrorActionPreference = "Stop"

# Renkli çıktı için helper fonksiyon
function Write-Step {
    param([string]$Message, [string]$Color = "Cyan")
    Write-Host "`n=== $Message ===" -ForegroundColor $Color
}

Write-Step "StoneMaster Build Süreci Başlatılıyor"

# Engine build (isteğe bağlı olarak atlanabilir)
if (-not $SkipEngineBuild) {
    Write-Step "Engine Bileşenleri Derleniyor"
    & "$PSScriptRoot\build-engine.ps1"
} else {
    Write-Step "Engine Build Atlandı" -Color Yellow
}

# CorelInteropPath kontrolü
if ([string]::IsNullOrEmpty($CorelInteropPath)) {
    Write-Host "`n[BİLGİ] Corel.Interop.VGCore.dll aranıyor..." -ForegroundColor Cyan
    
    # Önce bilinen konumlara bak
    $knownPaths = @(
        "C:\Program Files\Corel\CorelDRAW Graphics Suite\26\Programs64\Corel.Interop.VGCore.dll",
        "C:\Program Files\Corel\CorelDRAW Graphics Suite 2024\Programs64\Corel.Interop.VGCore.dll",
        "C:\Program Files (x86)\Corel\CorelDRAW Graphics Suite 2023\Programs64\Corel.Interop.VGCore.dll",
        "C:\Program Files\Corel\CorelDRAW Graphics Suite 2023\Programs64\Corel.Interop.VGCore.dll",
        "${env:USERPROFILE}\Desktop\Corel.Interop.VGCore.dll",
        "${env:USERPROFILE}\OneDrive\Desktop\Corel.Interop.VGCore.dll",
        "${env:USERPROFILE}\OneDrive\Masaüstü\Corel.Interop.VGCore.dll"
    )
    
    foreach ($path in $knownPaths) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $CorelInteropPath = $path
            Write-Host "[BULUNDU] Standart yolda tespit edildi: $CorelInteropPath" -ForegroundColor Green
            break
        }
    }
    
    # Eğer bulunamadıysa tüm sistemde recursive arama yap
    if ([string]::IsNullOrEmpty($CorelInteropPath)) {
        Write-Host "[BİLGİ] Bilinen konumlarda bulunamadı. Tüm disklerde aranıyor..." -ForegroundColor Yellow
        
        $drives = Get-PSDrive -PSProvider FileSystem | Select-Object -ExpandProperty Root
        
        foreach ($drive in $drives) {
            Write-Host "  Aranıyor: $drive" -ForegroundColor Gray
            try {
                $foundFile = Get-ChildItem -Path $drive -Filter "Corel.Interop.VGCore.dll" -Recurse -ErrorAction SilentlyContinue -File | 
                    Where-Object { $_.FullName -notmatch '\\Recycle\\.Bin|\\\$Recycle\\.Bin|\\System Volume Information' } |
                    Select-Object -First 1 -ExpandProperty FullName
                
                if ($foundFile) {
                    $CorelInteropPath = $foundFile
                    Write-Host "[BULUNDU] DLL Tespit Edildi: $CorelInteropPath" -ForegroundColor Green
                    break
                }
            }
            catch {
                Write-Host "  Hata (atlanıyor): $_" -ForegroundColor DarkGray
                continue
            }
        }
    }
    
    if ([string]::IsNullOrEmpty($CorelInteropPath) -or -not (Test-Path -LiteralPath $CorelInteropPath -PathType Leaf)) {
        Write-Host "`nHATA: Corel.Interop.VGCore.dll bulunamadı." -ForegroundColor Red
        Write-Host "Lütfen şunlardan birini yapın:" -ForegroundColor Yellow
        Write-Host "  1. CorelDRAW'ı yükleyin" -ForegroundColor Yellow
        Write-Host "  2. Dosyanın yolunu belirtin: .\build.ps1 -CorelInteropPath 'C:\path\to\Corel.Interop.VGCore.dll'" -ForegroundColor Yellow
        Write-Host "  3. Veya sadece engine'i derleyin: .\build.ps1 -SkipEngineBuild" -ForegroundColor Yellow
        throw "Corel.Interop.VGCore.dll bulunamadı"
    }
} elseif (-not (Test-Path -LiteralPath $CorelInteropPath -PathType Leaf)) {
    throw "Belirtilen Corel.Interop.VGCore.dll bulunamadı: $CorelInteropPath"
}

# DLL'yi proje klasörüne kopyala
$dllDestDir = "$PSScriptRoot\..\build"
New-Item -ItemType Directory -Force -Path $dllDestDir | Out-Null
$dllDest = "$dllDestDir\Corel.Interop.VGCore.dll"

if ($CorelInteropPath -ne $dllDest) {
    Copy-Item -LiteralPath $CorelInteropPath -Destination $dllDest -Force
    Write-Host "[KOPYALANDI] DLL dosyası proje klasörüne kopyalandı: $dllDest" -ForegroundColor Green
}

# Corel Add-on build - .csproj dosyasını doğrudan kullan
$project = "$PSScriptRoot\..\corel\StoneMaster.Corel\StoneMaster.Corel.csproj"

if (-not (Test-Path $project)) {
    # Eğer .csproj bulunamazsa solution dosyasını dene
    $solution = "$PSScriptRoot\..\StoneMaster.sln"
    if (Test-Path $solution) {
        Write-Host "Proje dosyası bulunamadı, solution dosyası kullanılıyor: $solution" -ForegroundColor Yellow
        $project = $solution
    } else {
        throw "Ne proje (.csproj) ne de solution (.sln) dosyası bulunamadı!"
    }
}

Write-Step "Corel Add-on Derleniyor ($project)"

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$buildSuccess = $false

if ($null -ne $dotnetCommand) {
    Write-Host "dotnet MSBuild kullanılıyor..." -ForegroundColor Gray
    & $dotnetCommand.Source msbuild $project /t:Restore,Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath" /v:minimal
    $buildSuccess = ($LASTEXITCODE -eq 0)
}
else {
    $msbuildCommand = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($null -ne $msbuildCommand) {
        Write-Host "MSBuild kullanılıyor..." -ForegroundColor Gray
        & $msbuildCommand.Source $project /t:Restore,Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath" /v:minimal
        $buildSuccess = ($LASTEXITCODE -eq 0)
    }
    else {
        $vsBuildToolsMsbuild = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
        if (Test-Path -LiteralPath $vsBuildToolsMsbuild -PathType Leaf) {
            Write-Host "Visual Studio BuildTools MSBuild kullanılıyor..." -ForegroundColor Gray
            & $vsBuildToolsMsbuild $project /t:Restore,Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath" /v:minimal
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
    # Alternatif çıktı yollarını kontrol et
    $altPaths = @(
        "$PSScriptRoot\..\corel\StoneMaster.Corel\bin\Release\net48",
        "$PSScriptRoot\..\corel\StoneMaster.Corel\bin\x64\Release",
        "$PSScriptRoot\..\corel\StoneMaster.Corel\obj\x64\Release\net48"
    )
    
    $found = $false
    foreach ($altPath in $altPaths) {
        if (Test-Path "$altPath\StoneMaster.Corel.dll") {
            $dockerBuildOutput = $altPath
            $found = $true
            Write-Host "Alternatif çıktı yolu kullanılıyor: $dockerBuildOutput" -ForegroundColor Yellow
            break
        }
    }
    
    if (-not $found) {
        throw "Derleme çıktısı bulunamadı: $dockerBuildOutput\StoneMaster.Corel.dll"
    }
}

Copy-Item "$dockerBuildOutput\StoneMaster.Corel.dll" "$dockerDest\StoneMaster.Corel.dll" -Force

# Engine exe'sini kopyala (eğer varsa)
$engineExe = "$PSScriptRoot\..\artifacts\StoneMaster.Engine\StoneMaster.Engine.exe"
if (Test-Path $engineExe) {
    Copy-Item $engineExe "$dockerDest\StoneMaster.Engine.exe" -Force
} else {
    Write-Host "Engine exe bulunamadı, atlanıyor." -ForegroundColor Yellow
}

# Manifest dosyalarını kopyala
$manifestPath = "$PSScriptRoot\..\corel\StoneMaster.Corel\Manifest"
if (Test-Path $manifestPath) {
    Copy-Item "$manifestPath\AppUI.xslt" "$dockerDest\AppUI.xslt" -Force -ErrorAction SilentlyContinue
    Copy-Item "$manifestPath\UserUI.xslt" "$dockerDest\UserUI.xslt" -Force -ErrorAction SilentlyContinue
    Copy-Item "$manifestPath\Coreldrw.addon" "$dockerDest\Coreldrw.addon" -Force -ErrorAction SilentlyContinue
} else {
    Write-Host "Manifest klasörü bulunamadı, manuel kopyalama gerekebilir." -ForegroundColor Yellow
}

# Corel.Interop.VGCore.dll artifact'ta olmamalı
if (Test-Path -LiteralPath "$dockerDest\Corel.Interop.VGCore.dll") {
    Remove-Item "$dockerDest\Corel.Interop.VGCore.dll" -Force
    Write-Host "Corel.Interop.VGCore.dll artifact'tan kaldırıldı." -ForegroundColor Yellow
}

# Corel Plugins klasörüne otomatik kopyalama dene
$corelPluginDir = "C:\ProgramData\Corel\Plugins\GVAS\StoneMaster"
try {
    if (Test-Path "C:\ProgramData\Corel\Plugins\GVAS") {
        New-Item -ItemType Directory -Force -Path $corelPluginDir | Out-Null
        Get-ChildItem -Path $dockerDest -File | ForEach-Object {
            Copy-Item $_.FullName "$corelPluginDir\$($_.Name)" -Force
        }
        Write-Host "[KURULDU] Eklenti Corel Plugins klasörüne kopyalandı: $corelPluginDir" -ForegroundColor Green
    } else {
        Write-Host "Corel Plugins klasörü bulunamadı. Dosyaları manuel olarak kopyalamanız gerekebilir." -ForegroundColor Yellow
        Write-Host "Hedef: $corelPluginDir" -ForegroundColor Cyan
    }
}
catch {
    Write-Host "Corel Plugins klasörüne kopyalama başarısız. Yönetici yetkisi gerekebilir." -ForegroundColor Red
    Write-Host "Dosyaları manuel olarak kopyalayın: $dockerDest -> $corelPluginDir" -ForegroundColor Yellow
}

Write-Step "Build Başarıyla Tamamlandı!" -Color Green
Write-Host "Çıktı klasörü: $dockerDest" -ForegroundColor Green
if (Test-Path $corelPluginDir) {
    Write-Host "Eklenti otomatik olarak Corel'e yüklendi!" -ForegroundColor Green
} else {
    Write-Host "`nKurulum için: .\scripts\package.ps1 veya dosyaları manuel kopyalayın." -ForegroundColor Cyan
}
