; StoneMaster for CorelDRAW installer
#define AppName "StoneMaster"
#define AppVersion "1.0.0"
#define Publisher "StoneMaster"
#define CorelAddon "StoneMaster"

[Setup]
AppId={{7E8AA84B-1D80-43DC-8B9B-BB9E2B2F7A90}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\StoneMaster
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=StoneMaster
OutputDir=..\artifacts\installer
OutputBaseFilename=StoneMaster_Setup

[Files]
Source: "..\artifacts\StoneMaster.Corel\StoneMaster.Corel.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\artifacts\StoneMaster.Corel\StoneMaster.Engine.exe"; DestDir: "{app}\StoneMaster.Engine"; Flags: ignoreversion
Source: "..\corel\StoneMaster.gms.bas"; DestDir: "{app}\VBA"; Flags: ignoreversion
Source: "..\corel\StoneMaster.Corel\Manifest\AppUI.xslt"; DestDir: "{app}\Addon"; Flags: ignoreversion
Source: "..\corel\StoneMaster.Corel\Manifest\UserUI.xslt"; DestDir: "{app}\Addon"; Flags: ignoreversion
Source: "..\corel\StoneMaster.Corel\Manifest\Coreldrw.addon"; DestDir: "{app}\Addon"; Flags: ignoreversion
Source: "..\config\*.json"; DestDir: "{app}\config"; Flags: ignoreversion

[Code]
function FindCorelAddonRoot(): String;
var
  Base, Candidate: String;
begin
  Result := '';
  Base := ExpandConstant('{autopf}') + '\Corel';
  if not DirExists(Base) then Exit;

  Candidate := Base + '\CorelDRAW Graphics Suite\26\Programs64\Addons';
  if DirExists(Candidate) then begin Result := Candidate; Exit; end;

  Candidate := Base + '\CorelDRAW Graphics Suite 2026\Programs64\Addons';
  if DirExists(Candidate) then begin Result := Candidate; Exit; end;

  Candidate := Base + '\CorelDRAW Graphics Suite 2025\Programs64\Addons';
  if DirExists(Candidate) then begin Result := Candidate; Exit; end;

  Candidate := Base + '\CorelDRAW Graphics Suite 2024\Programs64\Addons';
  if DirExists(Candidate) then begin Result := Candidate; Exit; end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AddonRoot, Dest: String;
begin
  if CurStep = ssPostInstall then
  begin
    AddonRoot := FindCorelAddonRoot();
    if AddonRoot = '' then
    begin
      MsgBox('CorelDRAW 2024/2025/2026 Programs64\Addons klasörü otomatik bulunamadı. CorelDRAW yolunu kontrol edin; kaynak paket içindeki Addon klasörünü ilgili Programs64\Addons\StoneMaster konumuna kopyalayın.', mbInformation, MB_OK);
      Exit;
    end;

    Dest := AddonRoot + '\StoneMaster';
    ForceDirectories(Dest);
    CopyFile(ExpandConstant('{app}\StoneMaster.Corel.dll'), Dest + '\StoneMaster.Corel.dll', False);
    CopyFile(ExpandConstant('{app}\Addon\AppUI.xslt'), Dest + '\AppUI.xslt', False);
    CopyFile(ExpandConstant('{app}\Addon\UserUI.xslt'), Dest + '\UserUI.xslt', False);
    CopyFile(ExpandConstant('{app}\Addon\Coreldrw.addon'), Dest + '\Coreldrw.addon', False);

    ForceDirectories(Dest + '\StoneMaster.Engine');
    CopyFile(ExpandConstant('{app}\StoneMaster.Engine\StoneMaster.Engine.exe'), Dest + '\StoneMaster.Engine\StoneMaster.Engine.exe', True);
    ForceDirectories(Dest + '\config');
    CopyFile(ExpandConstant('{app}\config\stones.json'), Dest + '\config\stones.json', True);
    CopyFile(ExpandConstant('{app}\config\palette.json'), Dest + '\config\palette.json', True);
  end;
end;
