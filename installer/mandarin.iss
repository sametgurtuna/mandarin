; Mandarin installer (Inno Setup 6).
;
; Per-user install, no admin rights: same philosophy as the app itself (per-user
; registry only, see CLAUDE.md). Installs to %LocalAppData%\Programs\Mandarin,
; so no UAC prompt and no elevation is ever needed.
;
; Build with the publish output already produced (see Commands in CLAUDE.md):
;   dotnet publish src/Mandarin.App/Mandarin.App.csproj -c Release -r win-x64 ^
;     --self-contained true -p:PublishSingleFile=true ^
;     -p:IncludeNativeLibrariesForSelfExtract=true -o publish
; then:
;   "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" installer\mandarin.iss

#define MyAppName "Mandarin"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Mandarin"
#define MyAppURL "https://github.com/sametgurtuna/mandarin"
#define MyAppExeName "Mandarin.App.exe"

[Setup]
; Fixed AppId: keep this the same across versions so upgrades replace in place
; instead of installing side by side.
AppId={{8F2E6B2F-6B7B-4B7C-9B0B-1C9E9E7B7A61}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={localappdata}\Programs\Mandarin
DefaultGroupName=Mandarin
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\installer-output
OutputBaseFilename=MandarinSetup
SetupIconFile=..\assets\mandarin.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "Start Mandarin automatically when Windows starts"; GroupDescription: "Startup:"; Flags: checkedonce

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Mandarin"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,Mandarin}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Mandarin"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Same key and value format Settings > Start Mandarin when Windows starts writes
; at runtime (Mandarin.Shell.StartupIntegration), so the checkbox there reflects
; this correctly after install and toggling it later just edits the same value.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Mandarin"; ValueData: """{app}\{#MyAppExeName}"" --startup"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Mandarin}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Explorer's "Convert with Mandarin" verb is registered by the app itself
; (Settings > right-click menu), not by this installer — but if it was turned
; on, leaving it pointing at a now-deleted exe would break Explorer's menu.
Type: dirifempty; Name: "{app}"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\*\shell\Mandarin.ConvertWithMandarin');
  end;
end;
