; ============================================================================
; 音言音乐 —— Windows 客户端安装包（Inno Setup 6.x，win-x64 / amd64）
;
; 打包的是 **客户端**（YinYanMusic.App），不是服务端 API：
; 客户端连你服务器上的 API（Docker + Nginx 那套），所以安装包里不含 API。
;
; 用法（先发布客户端，或用一键脚本）：
;   installer\build-client-installer.ps1
;   等价于：
;     dotnet publish src\YinYanMusic.App\YinYanMusic.App.csproj -f net10.0-windows10.0.19041.0 `
;         -c Release -p:RuntimeIdentifierOverride=win-x64 -p:SelfContained=true `
;         -p:WindowsAppSDKSelfContained=true -o installer\build\client
;     ISCC.exe installer\client.iss /DMyClientVersion=2.2.0.0
;
; 安装结果：
;   C:\Program Files\YinYanMusic\          客户端（自包含，目标机不需要装 .NET / Windows App Runtime）
;   开始菜单 / 桌面快捷方式「音言音乐」
;   （可选）机器级环境变量 YINYAN_API_BASEURL = 安装时填的服务器地址
; ============================================================================

#ifndef MyClientVersion
  #define MyClientVersion "2.2.0.0"
#endif

#define MyAppName      "音言音乐"
#define MyAppPublisher "YinYanMusic"
#define MyAppExe       "YinYanMusic.App.exe"
#define MyClientDir    "build\client"

[Setup]
AppId=YinYanMusicClient
AppName={#MyAppName}
AppVersion={#MyClientVersion}
AppVerName={#MyAppName} {#MyClientVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\YinYanMusic
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; 装到 Program Files + 写机器级环境变量，需要提权
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=build\out
OutputBaseFilename=YinYanMusic-Client-Setup-{#MyClientVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
; icon.ico 由 publish 输出（build\client\icon.ico）——所以必须先发布再编译本脚本
SetupIconFile={#MyClientDir}\icon.ico
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExe}
; AppVersion 只管向导文案，这几行才写进 setup.exe 的版本资源（否则显示 0.0.0.0）
VersionInfoVersion={#MyClientVersion}
VersionInfoProductVersion={#MyClientVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} 客户端 安装程序
VersionInfoCopyright=Copyright (C) 2026 {#MyAppPublisher}

[Languages]
Name: "chinese"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[CustomMessages]
chinese.ServerPageCaption=连接设置
chinese.ServerPageDesc=填后端服务地址（客户端会连它）。留空则使用客户端内置的「设置」页里配置的地址。
chinese.ServerUrl=服务器地址
chinese.BadUrl=地址要以 http:// 或 https:// 开头，例如 http://192.168.1.10:5116
chinese.ServerHint=提示：地址会写成系统环境变量 YINYAN_API_BASEURL（优先级低于客户端「设置」页里手填的地址）。新登录的会话才会读到它，所以装完第一次启动若地址不对，请在「设置」页里改一次。
chinese.LaunchApp=启动 {#MyAppName}

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; 自包含客户端（约 280MB / 600+ 文件：.NET 运行时 + Windows App SDK 全在里面）
Source: "{#MyClientDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Registry]
; 服务器地址（可选）：写机器级环境变量，客户端启动时按「环境变量」优先级读取
Root: HKLM; \
  Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; \
  ValueType: string; ValueName: "YINYAN_API_BASEURL"; ValueData: "{code:GetServerUrl}"; \
  Flags: uninsdeletevalue; Check: ShouldWriteServerUrl

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchApp}"; Flags: postinstall nowait skipifsilent

[Code]
var
  ServerPage: TInputQueryWizardPage;

function GetServerUrl(Param: String): String;
begin
  Result := Trim(ServerPage.Values[0]);
end;

function ShouldWriteServerUrl: Boolean;
begin
  Result := GetServerUrl('') <> '';
end;

procedure InitializeWizard;
begin
  ServerPage := CreateInputQueryPage(wpSelectDir,
    CustomMessage('ServerPageCaption'),
    CustomMessage('ServerPageDesc'),
    CustomMessage('ServerHint'));
  { ⚠️ Add(APrompt, APassword: Boolean)：第二个参数是「是否密码框」，**不是默认值**！
     传字符串编译能过、运行时直接抛 "Type Mismatch"。
     默认值必须用 Values[Index] 赋（见下一行）。 }
  ServerPage.Add(CustomMessage('ServerUrl'), False);
  ServerPage.Values[0] := '';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Url: String;
begin
  Result := True;
  if CurPageID = ServerPage.ID then
  begin
    Url := Trim(ServerPage.Values[0]);
    if (Url <> '') and (Pos('://', Url) = 0) then
    begin
      MsgBox(CustomMessage('BadUrl'), mbError, MB_OK);
      Result := False;
    end;
  end;
end;
