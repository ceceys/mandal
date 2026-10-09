; Mandal kurulum paketi (Inno Setup 6)
; Derleme: ISCC.exe /DAppVersion=1.1.0 setup\Mandal.iss
; Önce: dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish-setup

#ifndef AppVersion
  #define AppVersion "1.3.1"
#endif
#define AppName "Mandal"
#define AppPublisher "Cuma Ali Dirik"
#define AppURL "https://github.com/ceceys/mandal"
#define AppExe "Mandal.exe"

[Setup]
AppId={{7D3C9B2E-5A41-4F0B-9C6E-2B1F8E4D7A10}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=Mandal-Setup-{#AppVersion}
SetupIconFile=..\Assets\mandal.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
CloseApplications=yes
RestartApplications=no
LicenseFile=..\LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
#ifexist "compiler:Languages\Turkish.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
#endif
#ifexist "compiler:Languages\German.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
#endif
#ifexist "compiler:Languages\French.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
#endif
#ifexist "compiler:Languages\Spanish.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
#endif
#ifexist "compiler:Languages\Italian.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
#endif
#ifexist "compiler:Languages\BrazilianPortuguese.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
#endif
#ifexist "compiler:Languages\Russian.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
#endif
#ifexist "compiler:Languages\Japanese.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
#endif
#ifexist "compiler:Languages\Korean.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "{cm:AutoStartProgram,{#AppName}}"; GroupDescription: "{cm:AutoStartProgramGroupDescription}"

[Files]
Source: "..\publish-setup\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/f /im {#AppExe}"; Flags: runhidden; RunOnceId: "KillMandal"
