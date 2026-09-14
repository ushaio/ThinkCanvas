; ThinkCanvas 0.0.4 安装包脚本（Inno Setup 6）
; 使用：安装 Inno Setup 后执行 ISCC packaging\ThinkCanvas.iss，
; 产物输出到 packaging\Output\ThinkCanvas-0.0.4-setup.exe
; 说明：采用 xcopy 产物打包，目标机需已安装 .NET 10 Desktop Runtime。

#define MyAppName "ThinkCanvas"
#define MyAppVersion "0.0.4"
#define MyAppPublisher "ThinkCanvas"
#define MyAppExeName "ThinkCanvas.exe"
#define ReleaseDir "..\bin\Release\net10.0-windows"

[Setup]
AppId={{8E4A6C52-9B7D-4F3A-A1E2-ThinkCanvas00}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=ThinkCanvas-{#MyAppVersion}-setup
SetupIconFile=..\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "开机自动启动 ThinkCanvas"; Flags: unchecked

[Files]
Source: "{#ReleaseDir}\ThinkCanvas.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ReleaseDir}\ThinkCanvas.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ReleaseDir}\ThinkCanvas.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#ReleaseDir}\ThinkCanvas.deps.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; 勾选“开机自动启动”任务时写入当前用户自启动项
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ThinkCanvas"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
