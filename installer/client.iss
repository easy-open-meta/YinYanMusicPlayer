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
;   （可选）公钥证书 yinyan.pem，以及把它写入本机受信任根证书颁发机构的选项
; ============================================================================

; ⚠️ 故意不给兜底默认值：版本号由 build-client-installer.ps1 读 csproj 的
;    ApplicationDisplayVersion，补成 4 段后通过 /DMyClientVersion=<x.y.z.w> 传入。
;    这里原本写死 "2.11.0.0"，在 csproj 推进到 2.12.0 之后就过期了，
;    结果是在 Inno Setup IDE 里直接编译会静默产出版本号写错的包，所以改成硬失败。
#ifndef MyClientVersion
  #error 未指定版本号。请用 build-client-installer.ps1 编译，或显式传 /DMyClientVersion=<x.y.z.w>
#endif

#define MyAppName      "音言音乐"
#define MyAppPublisher "YinYanMusic"
#define MyAppExe       "YinYanMusic.App.exe"
#define MyClientDir    "build\client"
#define MyCertDir      "certs"
#define MyCertFile     "yinyan.pem"
; 必须与 ISCC /S 参数里的名字一致（见文件头说明）
#define MySignTool     "yinyan"

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
; 用 {#MySignTool} 指定的签名工具签 setup.exe 与卸载器（SignedUninstaller 默认 yes）。
; 名字对应的命令来自 ISCC /S 参数；没有该参数时编译会直接失败，这是有意的
; ——宁可构建报错，也不要静默产出未签名的安装包。
SignTool={#MySignTool}
; 签名工具是控制台程序，不设这个开关时它会新开一个控制台窗口在屏幕上闪一下。
; 签名一共被调用两次：先卸载器 uninst.e32.tmp，最后才是 setup.exe 本身。
; 只影响窗口显示，不影响签名结果。
SignToolRunMinimized=yes

[Languages]
Name: "chinese"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[CustomMessages]
chinese.ServerPageCaption=连接设置
chinese.ServerPageDesc=填后端服务地址（客户端会连它）。留空则使用客户端内置的「设置」页里配置的地址。
chinese.ServerUrl=服务器地址
chinese.BadUrl=地址要以 http:// 或 https:// 开头，例如 http://192.168.1.10:5116
chinese.ServerHint=提示：地址会写成系统环境变量 YINYAN_API_BASEURL（优先级低于客户端「设置」页里手填的地址）。新登录的会话才会读到它，所以装完第一次启动若地址不对，请在「设置」页里改一次。
chinese.LaunchApp=启动 {#MyAppName}
chinese.SignGroup=签名与信任
chinese.TrustTask=信任「{#MyAppPublisher}」的签名证书（写入本机受信任的根证书颁发机构）
chinese.TrustStatusMsg=正在写入受信任的根证书颁发机构...

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
; 默认不勾：写入受信任的根属于系统信任链改动，让用户自己决定。
; 想默认勾选，把 Flags 去掉即可。
; 注意：它消除的是「未知发布者」，不会消除 SmartScreen 的云端信誉提示。
Name: "trustcert"; Description: "{cm:TrustTask}"; GroupDescription: "{cm:SignGroup}"; Flags: unchecked

[Files]
; 自包含客户端（约 280MB / 600+ 文件：.NET 运行时 + Windows App SDK 全在里面）
Source: "{#MyClientDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; 公钥证书（不含私钥）：随包附带，即使不勾选信任，用户或 IT 也能手动导入
; 私钥 yinyan.pfx 绝不能放进这里
Source: "{#MyCertDir}\{#MyCertFile}"; DestDir: "{app}"; Flags: ignoreversion

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
; 写本机受信任根需要管理员权限。本脚本 PrivilegesRequired=admin，
; 安装过程本身已提权，所以这里不需要额外的权限判断。
; 移除：certutil -delstore "Root" 证书指纹
Filename: "{sys}\certutil.exe"; Parameters: "-addstore -f Root ""{app}\{#MyCertFile}"""; Tasks: trustcert; Flags: runhidden waituntilterminated; StatusMsg: "{cm:TrustStatusMsg}"

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
