param(
    [string]$InstallRoot = "C:\Program Files\StoneMaster",
    [string]$CorelAddonsRoot = "C:\Program Files\Corel\CorelDRAW Graphics Suite\26\Programs64\Addons"
)

$ErrorActionPreference = "Stop"

if (Get-Process -Name "CorelDRW*" -ErrorAction SilentlyContinue) {
    throw "Önce CorelDRAW'ı kapatın; ardından bu betiği yönetici olarak yeniden çalıştırın."
}

$destination = Join-Path $CorelAddonsRoot "StoneMaster"
if (-not (Test-Path -LiteralPath $InstallRoot -PathType Container)) {
    throw "StoneMaster kurulum klasörü bulunamadı: $InstallRoot"
}
if (-not (Test-Path -LiteralPath $CorelAddonsRoot -PathType Container)) {
    throw "CorelDRAW Addons klasörü bulunamadı: $CorelAddonsRoot"
}

$files = @(
    @{ Source = "StoneMaster.Corel.dll"; Destination = "StoneMaster.Corel.dll" },
    @{ Source = "StoneMaster.Engine\StoneMaster.Engine.exe"; Destination = "StoneMaster.Engine\StoneMaster.Engine.exe" },
    @{ Source = "Addon\AppUI.xslt"; Destination = "AppUI.xslt" },
    @{ Source = "Addon\UserUI.xslt"; Destination = "UserUI.xslt" },
    @{ Source = "Addon\Coreldrw.addon"; Destination = "Coreldrw.addon" },
    @{ Source = "config\stones.json"; Destination = "config\stones.json" },
    @{ Source = "config\palette.json"; Destination = "config\palette.json" }
)

foreach ($file in $files) {
    $source = Join-Path $InstallRoot $file.Source
    $target = Join-Path $destination $file.Destination
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Kaynak dosya bulunamadı: $source"
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}

$missing = $files | Where-Object {
    -not (Test-Path -LiteralPath (Join-Path $destination $_.Destination) -PathType Leaf)
}
if ($missing) {
    throw "Onarım tamamlanamadı; eksik dosya sayısı: $($missing.Count)"
}

Write-Host "StoneMaster CorelDRAW eklentisi onarıldı: $destination"
