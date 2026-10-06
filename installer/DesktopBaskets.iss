#ifndef AppVersion
  #define AppVersion "0.4.15"
#endif
#ifndef PayloadDir
  #define PayloadDir "..\build\stage"
#endif
#ifndef OutputPath
  #define OutputPath "..\dist"
#endif

[Setup]
#ifdef BuildValidation
AppId={{BA73A83B-47D6-4EBA-A11C-94AEEBB8D63A}
AppName=Desktop Baskets Validation
AppMutex=Local\DesktopBaskets_Validation_{username}
OutputBaseFilename=DesktopBaskets-Validation-{#AppVersion}
#else
AppId={{4E45B6B5-149D-46DB-BB03-10368C7A9FA8}
AppName=Desktop Baskets
AppMutex=Local\DesktopBaskets_{username}
OutputBaseFilename=DesktopBaskets-Setup-{#AppVersion}-x64
#endif
AppVersion={#AppVersion}
AppPublisher=OverGreen996
AppPublisherURL=https://github.com/OverGreen996/DesktopBaskets
AppSupportURL=https://github.com/OverGreen996/DesktopBaskets/issues
AppUpdatesURL=https://github.com/OverGreen996/DesktopBaskets/releases
DefaultDirName={localappdata}\Programs\DesktopBaskets
DefaultGroupName=Desktop Baskets
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputPath}
SetupIconFile=..\src\DesktopBaskets\Assets\DesktopBaskets.ico
UninstallDisplayIcon={app}\DesktopBaskets.exe
UninstallDisplayName=Desktop Baskets {#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern dark hidebevels includetitlebar
WizardSizePercent=110
CloseApplications=no
RestartApplications=no
DisableWelcomePage=no
DisableDirPage=no
Uninstallable=yes
VersionInfoVersion={#AppVersion}

[Languages]
Name: "chinesetraditional"; MessagesFile: "Languages\ChineseTraditional.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
chinesetraditional.SetupAppRunningError=Desktop Baskets 正在執行。%n%n請先在程式或系統匣選「退出並還原圖示」，再繼續安裝。關閉管理視窗只會藏到系統匣。
chinesetraditional.UninstallAppRunningError=Desktop Baskets 正在執行。%n%n請先選「退出並還原圖示」，再繼續解除安裝。

[CustomMessages]
chinesetraditional.DesktopShortcut=建立桌面捷徑
chinesetraditional.Autostart=登入 Windows 時啟動
chinesetraditional.Launch=開啟 Desktop Baskets
chinesetraditional.CloseFirst=請先在 Desktop Baskets 選「退出並還原圖示」，再執行安裝或解除安裝。你的分類設定與原始檔案會保留。
chinesetraditional.NeedFramework=需要 .NET Framework 4.8 或更新版本。請先從 Microsoft 安裝此元件：https://dotnet.microsoft.com/download/dotnet-framework/net48
english.DesktopShortcut=Create a desktop shortcut
english.Autostart=Start when signing in to Windows
english.Launch=Launch Desktop Baskets
english.CloseFirst=Choose Exit and restore icons in Desktop Baskets before installing or uninstalling. Your settings and original files are preserved.
english.NeedFramework=.NET Framework 4.8 or later is required. Install it from Microsoft: https://dotnet.microsoft.com/download/dotnet-framework/net48

#ifndef BuildValidation
[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; Flags: unchecked
Name: "autostart"; Description: "{cm:Autostart}"; Flags: unchecked

[Icons]
Name: "{group}\Desktop Baskets"; Filename: "{app}\DesktopBaskets.exe"
Name: "{group}\解除安裝 Desktop Baskets"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Desktop Baskets"; Filename: "{app}\DesktopBaskets.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DesktopBaskets"; ValueData: """{app}\DesktopBaskets.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\DesktopBaskets.exe"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent
#endif

[Files]
Source: "{#PayloadDir}\DesktopBaskets.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\DesktopBaskets.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\Assets\*"; DestDir: "{app}\Assets"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\使用說明.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PayloadDir}\Newtonsoft.Json-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Code]
function CheckRunning(IsSilent: Boolean): Boolean;
begin
#ifdef BuildValidation
  Result := not CheckForMutexes(ExpandConstant('Local\DesktopBaskets_Validation_{username}'));
#else
  Result := not CheckForMutexes(ExpandConstant('Local\DesktopBaskets_{username}'));
#endif
  if not Result then begin
    Log(CustomMessage('CloseFirst'));
    if not IsSilent then MsgBox(CustomMessage('CloseFirst'), mbInformation, MB_OK);
  end;
end;

function InitializeSetup: Boolean;
var Release: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040);
  if not Result then begin
    Log(CustomMessage('NeedFramework'));
    if not WizardSilent then MsgBox(CustomMessage('NeedFramework'), mbError, MB_OK);
    exit;
  end;
  Result := CheckRunning(WizardSilent);
end;

function InitializeUninstall: Boolean;
begin
  Result := CheckRunning(UninstallSilent);
end;
