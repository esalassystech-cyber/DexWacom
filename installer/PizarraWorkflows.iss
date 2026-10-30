#define AppName "Pizarra Workflows"
#define AppVersion "0.2.2"
#define AppPublisher "Eric Salas"
#define AppExeName "Pizarra.App.exe"

[Setup]
AppId={{C2668132-94E8-4BA5-B2C5-1B8B5382D0A2}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoVersion=0.2.2.0
VersionInfoProductName={#AppName}
VersionInfoDescription=Actualización de diagnóstico de Pizarra Workflows
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\Wacom Workboard
UsePreviousAppDir=yes
DefaultGroupName={#AppName}
SetupIconFile=..\assets\pizarra-workflows.ico
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=Pizarra-Workflows-Diagnostics-0.2.2
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Abrir {#AppName}"; Flags: postinstall nowait skipifsilent

[UninstallDelete]
; Los tableros en Documentos\Wacom Workboard se conservan al desinstalar.
