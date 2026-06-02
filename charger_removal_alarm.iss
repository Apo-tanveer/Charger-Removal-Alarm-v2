; Inno Setup Script - Charger Removal Alarm

#define MyAppName      "Charger Removal Alarm"
#define MyAppVersion   "1.0"
#define MyAppPublisher "Tanveer"
#define MyAppExeName   "ChargerRemovalAlarm.exe"

[Setup]
AppId={{C2D3E4F5-A6B7-8901-CDEF-012345678901}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=installer_output
OutputBaseFilename=ChargerRemovalAlarm_Setup
SetupIconFile=icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";  Description: "Create a &desktop shortcut";        GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startupicon";  Description: "Start automatically with &Windows"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "publish\ChargerRemovalAlarm.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\icon.ico";               DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";           Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}";     Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#MyAppName}"; \
  ValueData: """{app}\{#MyAppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
