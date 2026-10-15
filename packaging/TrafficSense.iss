#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef SourceDir
  #error SourceDir is required
#endif
#ifndef OutputDir
  #error OutputDir is required
#endif
#ifndef AppId
  #define AppId "TrafficSense.27C48081-DB56-4C92-9F87-66EB1C88B94F"
#endif
#ifndef OutputName
  #define OutputName "TrafficSense-Setup"
#endif
#ifndef StartupValue
  #define StartupValue "TrafficSense"
#endif
#ifndef MutexSuffix
  #define MutexSuffix ""
#endif

[Setup]
AppId={#AppId}
AppName=TrafficSense
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher=Muhammad Al-Muzahid
AppPublisherURL=https://github.com/almuzahidseyam/TrafficSense
AppUpdatesURL=https://github.com/almuzahidseyam/TrafficSense/releases
DefaultDirName={localappdata}\Programs\TrafficSense
DefaultGroupName=TrafficSense
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename={#OutputName}
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\Assets\TrafficSense.ico
UninstallDisplayIcon={app}\TrafficSense.exe
LicenseFile=..\LICENSE
AppMutex={code:AppMutexName}
CloseApplications=no
RestartApplications=no
AllowNoIcons=yes
DisableProgramGroupPage=yes
SetupLogging=yes

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
#ifdef Lite
Source: "{#RuntimeCheck}"; DestName: "TrafficSense.RuntimeCheck.exe"; Flags: dontcopy
#endif
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TrafficSense"; Filename: "{app}\TrafficSense.exe"
Name: "{group}\Uninstall TrafficSense"; Filename: "{uninstallexe}"
Name: "{autodesktop}\TrafficSense"; Filename: "{app}\TrafficSense.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\TrafficSense.exe"; Description: "Launch TrafficSense"; Flags: nowait postinstall skipifsilent unchecked

[Code]
#ifdef Lite
var
  RuntimePage: TOutputMsgWizardPage;
  RuntimeDownload: TNewButton;

function SetEnvironmentVariable(Name, Value: String): Boolean;
  external 'SetEnvironmentVariableW@kernel32.dll stdcall';

procedure OpenRuntimeDownload(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure InitializeWizard;
begin
  RuntimePage := CreateOutputMsgPage(wpSelectTasks, 'TrafficSense Lite', '.NET 8 Desktop Runtime (x64) required',
    'Lite includes all TrafficSense features and uses your existing .NET runtime.' + #13#10#13#10 +
    'Setup checks the runtime before installing or upgrading. If it is missing, install the latest .NET 8 Desktop Runtime for Windows x64, then retry Setup.' + #13#10#13#10 +
    '.NET Framework, the plain .NET Runtime, x86-only runtimes, or .NET 9/10 alone do not satisfy this requirement. You can also choose the full TrafficSense-Setup.exe with .NET included.');
  RuntimeDownload := TNewButton.Create(WizardForm);
  RuntimeDownload.Parent := RuntimePage.Surface;
  RuntimeDownload.SetBounds(0, ScaleY(180), ScaleX(270), ScaleY(30));
  RuntimeDownload.Caption := 'Get .NET 8 Desktop Runtime (x64)';
  RuntimeDownload.OnClick := @OpenRuntimeDownload;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
  Output: TExecOutput;
  PreviousGuiSetting: String;
  Ready: Boolean;
begin
  Result := '';
  Ready := False;
  PreviousGuiSetting := GetEnv('DOTNET_DISABLE_GUI_ERRORS');
  try
    try
      SetEnvironmentVariable('DOTNET_DISABLE_GUI_ERRORS', '1');
      ExtractTemporaryFile('TrafficSense.RuntimeCheck.exe');
      if ExecAndCaptureOutput(ExpandConstant('{tmp}\TrafficSense.RuntimeCheck.exe'), '', '', SW_HIDE, ewWaitUntilTerminated, ExitCode, Output) then
        Ready := (ExitCode = 0) and not Output.Error;
    except
      Log('Lite runtime check failed: ' + GetExceptionMessage);
    end;
  finally
    SetEnvironmentVariable('DOTNET_DISABLE_GUI_ERRORS', PreviousGuiSetting);
  end;
  if not Ready then begin
    Result := 'TrafficSense Lite needs .NET 8 Desktop Runtime (x64). Install its latest 8.0 patch from https://dotnet.microsoft.com/en-us/download/dotnet/8.0 and retry Setup, or use the full TrafficSense-Setup.exe. No application files have been changed.';
    Log(Result);
  end else Log('PASS: Lite runtime prerequisite verified by x64 Desktop apphost.');
end;
#endif

function AppMutexName(Param: String): String;
begin
  Result := 'Local\TrafficSense.Installer.' + Lowercase(GetUserNameString) + '{#MutexSuffix}';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#StartupValue}', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\TrafficSense.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#StartupValue}');
end;
