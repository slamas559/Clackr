#define AppName "Clackr"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppExeName "Clackr.exe"
#define PublishDir "..\KeySonic.UI\bin\Release\net10.0-windows\win-x64\publish"

[Setup]
AppId={{fda6fc37-7cc6-4030-adac-16cb0de9d38e}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\Clackr
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
CloseApplications=yes
OutputDir=..\artifacts
OutputBaseFilename=Clackr-Setup-{#AppVersion}-win-x64
SetupIconFile=..\KeySonic.UI\Assets\clackr.ico
UninstallDisplayIcon={app}\Clackr.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch Clackr"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'KeySonic');
end;