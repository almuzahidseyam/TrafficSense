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
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TrafficSense"; Filename: "{app}\TrafficSense.exe"
Name: "{group}\Uninstall TrafficSense"; Filename: "{uninstallexe}"
Name: "{autodesktop}\TrafficSense"; Filename: "{app}\TrafficSense.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\TrafficSense.exe"; Description: "Launch TrafficSense"; Flags: nowait postinstall skipifsilent unchecked

[Code]
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
