; TextCase installer (Inno Setup 6), in English and French (picked from the Windows display language).
; Built by scripts\build.ps1, which prepares dist\app first:
;   ISCC.exe /DAppVersion=1.0.0 installer\TextCase.iss
; The app also runs it to install an update (src\TextCase\Services\Updater.cs): silent, closing the running app
; (its hidden window answers the Restart Manager's WM_CLOSE), /RELAUNCH=1.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

; Windows file versions are numbers only: 1.1.0 for a 1.1.0-beta.1 pre-release.
#if Pos("-", AppVersion) > 0
  #define AppFileVersion Copy(AppVersion, 1, Pos("-", AppVersion) - 1)
#else
  #define AppFileVersion AppVersion
#endif

#define AppName "TextCase"
#define AppExe "TextCase.exe"

[Setup]
AppId={{4D4C29C7-FEEA-4F84-8A74-1DB539E852D1}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=QuentinPerou
AppPublisherURL=https://github.com/quentinperou
AppSupportURL=https://github.com/quentinperou/TextCase
AppUpdatesURL=https://github.com/quentinperou/TextCase/releases
VersionInfoVersion={#AppFileVersion}
DefaultDirName={autopf}\TextCase
DisableProgramGroupPage=yes
; Admin rights: installs for every user of the PC and lets the .NET runtime be installed.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\dist
OutputBaseFilename=TextCase-Setup-{#AppVersion}
SetupIconFile=..\src\TextCase\Assets\TextCase.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
; Only ask for the language when Windows uses one the installer does not have.
ShowLanguageDialog=auto

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
english.RuntimeMissing=TextCase needs the Microsoft ".NET 10 Desktop Runtime", which is not installed on this PC.%nInstall it now?
french.RuntimeMissing=TextCase a besoin du « .NET 10 Desktop Runtime » (Microsoft), absent de ce PC.%nL'installer maintenant ?
english.RuntimeInstalling=Installing the .NET 10 Desktop Runtime...
french.RuntimeInstalling=Installation du .NET 10 Desktop Runtime…
english.RuntimeFailed=The automatic installation failed. The download page will open:%nchoose ".NET Desktop Runtime 10" (x64).
french.RuntimeFailed=L'installation automatique a échoué. La page de téléchargement va s'ouvrir :%nchoisissez « .NET Desktop Runtime 10 » (x64).
english.DeleteUserData=Also delete your TextCase settings (the shortcuts and the word list)?%n%nKeep them if you are going to install TextCase again.
french.DeleteUserData=Supprimer aussi vos réglages TextCase (les raccourcis et la liste de mots) ?%n%nGardez-les si vous allez réinstaller TextCase.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; "Start with Windows" is written by the app itself (Settings › General, on by default once installed): removed with it.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "TextCase"; Flags: uninsdeletevalue dontcreatekey
; "Change the case of the name" in the menu of File Explorer is written by the app too (Settings › General): removed with it.
Root: HKCU; Subkey: "Software\Classes\*\shell\TextCase"; Flags: uninsdeletekey dontcreatekey
Root: HKCU; Subkey: "Software\Classes\Directory\shell\TextCase"; Flags: uninsdeletekey dontcreatekey

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[UninstallDelete]
; Temporary files of the app: the last update downloaded.
Type: filesandordirs; Name: "{%TEMP}\TextCase-Update"

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser
; Update installed from the app (silent, /RELAUNCH=1): start the app again, as the user, not elevated, without
; opening its settings.
Filename: "{app}\{#AppExe}"; Parameters: "--background"; Flags: nowait runasoriginaluser skipifnotsilent; Check: RelaunchRequested

[Code]
{ Set by the app when it installs an update (Updater.InstallerArguments). }
function RelaunchRequested: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

{ The app is framework-dependent: it needs the .NET 10 Desktop Runtime. }
function IsDesktopRuntimeInstalled: Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

{ A silent install (winget, the updater) installs it without asking, and says nothing if that fails: a message box
  would wait for an answer nobody sees (MsgBox is not suppressed by /SUPPRESSMSGBOXES). }
procedure EnsureDesktopRuntime;
var
  ResultCode: Integer;
begin
  if IsDesktopRuntimeInstalled then
    Exit;
  if not WizardSilent and (MsgBox(CustomMessage('RuntimeMissing'), mbConfirmation, MB_YESNO) <> IDYES) then
    Exit;
  WizardForm.StatusLabel.Caption := CustomMessage('RuntimeInstalling');
  if Exec('winget.exe', 'install --id Microsoft.DotNet.DesktopRuntime.10 -e --silent --accept-source-agreements --accept-package-agreements',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and IsDesktopRuntimeInstalled then
    Exit;
  if WizardSilent then
    Exit;
  MsgBox(CustomMessage('RuntimeFailed'), mbInformation, MB_OK);
  ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/10.0', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    EnsureDesktopRuntime;
end;

{ TextCase runs in the notification area: close it before its files are removed. taskkill without /f sends WM_CLOSE
  to the windows of TextCase.exe only (its hidden window quits the app); a window title would also match a File
  Explorer window open on a folder named TextCase. /f for what is left. }
procedure CloseRunningApp;
var
  ResultCode: Integer;
begin
  if Exec('taskkill.exe', '/im {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
    Sleep(1500);
  Exec('taskkill.exe', '/f /im {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeUninstall: Boolean;
begin
  CloseRunningApp;
  Result := True;
end;

{ Settings (%LOCALAPPDATA%\TextCase, of the account that uninstalls): kept unless the user says otherwise, "No" being
  the default, so that a reinstall finds them. A silent uninstall keeps them. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent and
     (MsgBox(CustomMessage('DeleteUserData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
    DelTree(ExpandConstant('{localappdata}\TextCase'), True, True, True);
end;
