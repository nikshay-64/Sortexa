; Sortexa Windows Desktop File Organizer
; Inno Setup 6 Installer Script

#define MyAppName "Sortexa"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Nikshay Kumar"
#define MyAppExeName "Sortexa.exe"
#define MyAppId "{{C28D7B48-8314-411B-B750-84E37A6A09FA}"

[Setup]
; Application Identity
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=Sortexa Windows Desktop File Organizer
AppCopyright=Copyright (C) 2026 Nikshay Kumar
AppContact=Nikshay Kumar

; Installation Directory and Scope (Per-User Scope: HKCU, non-administrative)
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
UsePreviousPrivileges=no

; Explicit Uninstall Registration (Guarantees registration in Windows Installed apps & Programs and Features)
Uninstallable=yes
CreateUninstallRegKey=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}

; Architecture and Target Compatibility (x64 Windows 10 & 11)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Appearance and Packaging
OutputBaseFilename=Sortexa-Setup
OutputDir=..\release
SetupIconFile=..\Sortexa.App\Assets\Sortexa.ico
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
CloseApplications=yes
RestartApplications=no

; Windows Binary Version Information
VersionInfoVersion=1.0.0.0
VersionInfoDescription=Sortexa Setup
VersionInfoCompany=Nikshay Kumar
VersionInfoCopyright=Copyright (C) 2026 Nikshay Kumar
VersionInfoProductName=Sortexa
VersionInfoProductVersion=1.0.0

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\Sortexa-win-x64\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Sortexa.App\Assets\Sortexa.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
