#define MyAppName "Auto Manual Tester"
#define MyAppVersion GetEnv("APP_VERSION")
#define MyAppPublisher "Auto Manual Tester"
#define MyAppExeName "AutoManualTester.exe"

[Setup]
AppId={{B2D39B62-7B0D-4D55-A57D-7A7BFD442D8D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\AutoManualTester
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=AutoManualTester-Setup
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "..\artifacts\AutoManualTester-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Auto Manual Tester"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Auto Manual Tester"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"
