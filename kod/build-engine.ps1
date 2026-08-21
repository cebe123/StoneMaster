$ErrorActionPreference = "Stop"

Set-Location "$PSScriptRoot\..\engine"

python -m pip install -r requirements.txt
python -m pip install pyinstaller
python -m PyInstaller --noconfirm --clean --onefile --name StoneMaster.Engine --add-data "..\config;config" cli.py

$dest = "$PSScriptRoot\..\artifacts\StoneMaster.Engine"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item "dist\StoneMaster.Engine.exe" "$dest\StoneMaster.Engine.exe" -Force
Copy-Item "$PSScriptRoot\..\config" "$dest\config" -Recurse -Force
