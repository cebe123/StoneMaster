param(
    [ValidateNotNullOrEmpty()]
    [string]$CorelInteropPath = "C:\Users\adnan\OneDrive\Masaüstü\Corel.Interop.VGCore.dll"
)

$ErrorActionPreference = "Stop"

& "$PSScriptRoot\build.ps1" -CorelInteropPath $CorelInteropPath

$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    $defaultIscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path -LiteralPath $defaultIscc -PathType Leaf) {
        $isccPath = $defaultIscc
    }
    else {
        throw "Inno Setup 6 bulunamadı. ISCC.exe'yi PATH'e ekleyin veya Inno Setup 6'yı kurun."
    }
}
else {
    $isccPath = $iscc.Source
}

& $isccPath "$PSScriptRoot\..\installer\StoneMaster.iss"

$setup = Get-ChildItem "$PSScriptRoot\..\artifacts\installer\StoneMaster_Setup.exe" -ErrorAction Stop
Write-Host "Paket hazır: $($setup.FullName)"
