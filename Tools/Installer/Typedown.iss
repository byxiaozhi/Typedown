; MyAppName, MyAppPublisher, MyAppExeName, MyAppId: the edition's names.
#include "brand.iss"
#ifndef MyArch
  #define MyArch "x64"
#endif
#define MyAppVersion "1.3.9"
#define MyAppAssocName MyAppName + " Markdown"
#define MyAppAssocKey MyAppName + ".Markdown"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename={#MyAppName}-windows-{#MyArch}-v{#MyAppVersion}
SetupIconFile=..\..\Dev\Typedown\Assets\logo.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
#if MyArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
PrivilegesRequired=admin
ChangesAssociations=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
; The app outlives its window when "keep running in the background" is on, and a window is what the
; restart manager sends its close request to — so it finds the app, asks it to close, and nothing
; happens. Checking the mutex says so before a single file is touched, instead of failing partway
; through with a list of files in use and an uninstall that then cannot finish either.
; Config.InstanceName + ".Mutex" (Dev/Typedown/App.cs)
AppMutex={#MyAppName}.App.Mutex
RestartApplications=no
MinVersion=10.0
VersionInfoVersion={#MyAppVersion}.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "..\..\Dev\Typedown\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not IsWebView2RuntimeInstalled

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Markdown Editor"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCR; Subkey: ".md"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".markdown"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".mdown"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".mkdn"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".mkd"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".mdwn"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".mdtxt"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".mdtext"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".rmd"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletevalue
; .txt / .text: offer the app in "Open with" without taking over the default handler (upstream #3)
Root: HKCR; Subkey: ".txt\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".text\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: "{#MyAppAssocKey}"; ValueType: string; ValueName: ""; ValueData: "{#MyAppAssocName}"; Flags: uninsdeletekey
Root: HKCR; Subkey: "{#MyAppAssocKey}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCR; Subkey: "{#MyAppAssocKey}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Installing WebView2 Runtime..."; Flags: waituntilterminated; Check: not IsWebView2RuntimeInstalled
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
function IsWebView2RuntimeInstalled: Boolean;
var
  Version: String;
begin
  Result := False;
  if RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version)
     or RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) then
  begin
    Result := (Version <> '') and (Version <> '0.0.0.0');
  end;
end;
