# StoneMaster Installation

## End user

1. Close CorelDRAW.
2. Run `StoneMaster_Setup.exe` as a normal administrator-approved Windows installer.
3. Setup detects CorelDRAW 2024/2025/2026.
4. Restart CorelDRAW.
5. Open `Window > Dockers > StoneMaster`.

## Developer machine

Install:

- Visual Studio 2022
- .NET Framework 4.8 Developer Pack
- CorelDRAW 2024/2025/2026
- Python 3.x
- PyInstaller
- Inno Setup 6

Then:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

To produce the installer after a successful build:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

Both scripts default to the supplied `Corel.Interop.VGCore.dll`; another licensed SDK location can be passed with `-CorelInteropPath <path>`. The DLL is a compile-time COM reference only and is not included in `StoneMaster_Setup.exe`.

## If Docker is missing

Run CorelDRAW with workspace reset using F8 once, then restart. Corel documents this as the remedy when user UI XSLT changes are not reflected.

## CorelDRAW install path

Typical 64-bit layout:

```text
C:\Program Files\Corel\CorelDRAW Graphics Suite 2026\
  Programs64\
    Addons\
      StoneMaster\
```

The exact root can vary by edition/install language, so the installer discovers it rather than hard-coding a single directory.
