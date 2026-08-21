param(
    [ValidateNotNullOrEmpty()]
    [string]$CorelInteropPath = "C:\Users\adnan\OneDrive\Masaüstü\Corel.Interop.VGCore.dll"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $CorelInteropPath -PathType Leaf)) {
    throw "Corel.Interop.VGCore.dll bulunamadı: $CorelInteropPath"
}

& "$PSScriptRoot\build-engine.ps1"

$solution = "$PSScriptRoot\..\corel\StoneMaster.Corel\StoneMaster.Corel.csproj"

Write-Host "Build Corel Docker with Visual Studio MSBuild."
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -ne $dotnetCommand) {
    & $dotnetCommand.Source msbuild $solution /t:Restore,Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath"
}
else {
    $msbuildCommand = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($null -ne $msbuildCommand) {
        & $msbuildCommand.Source $solution /t:Restore,Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath"
    }
    else {
        $vsBuildToolsMsbuild = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
        if (Test-Path -LiteralPath $vsBuildToolsMsbuild -PathType Leaf) {
            & $vsBuildToolsMsbuild $solution /t:Restore,Build /p:Configuration=Release /p:Platform=x64 "/p:CorelInteropPath=$CorelInteropPath"
        }
        else {
            throw "MSBuild bulunamadı. Visual Studio 2022 .NET desktop build tools kurun veya msbuild'i PATH'e ekleyin."
        }
    }
}

$dockerDest = "$PSScriptRoot\..\artifacts\StoneMaster.Corel"
New-Item -ItemType Directory -Force -Path $dockerDest | Out-Null

$dockerBuildOutput = "$PSScriptRoot\..\corel\StoneMaster.Corel\bin\x64\Release\net48"
Copy-Item "$dockerBuildOutput\StoneMaster.Corel.dll" "$dockerDest\StoneMaster.Corel.dll" -Force
Copy-Item "$PSScriptRoot\..\artifacts\StoneMaster.Engine\StoneMaster.Engine.exe" "$dockerDest\StoneMaster.Engine.exe" -Force
Copy-Item "$PSScriptRoot\..\corel\StoneMaster.Corel\Manifest\AppUI.xslt" "$dockerDest\AppUI.xslt" -Force
Copy-Item "$PSScriptRoot\..\corel\StoneMaster.Corel\Manifest\UserUI.xslt" "$dockerDest\UserUI.xslt" -Force
Copy-Item "$PSScriptRoot\..\corel\StoneMaster.Corel\Manifest\Coreldrw.addon" "$dockerDest\Coreldrw.addon" -Force

if (Test-Path -LiteralPath "$dockerDest\Corel.Interop.VGCore.dll") {
    throw "Corel.Interop.VGCore.dll paketlenemez; artifacts klasöründen kaldırın."
}

Write-Host "Artifacts ready: $dockerDest"
