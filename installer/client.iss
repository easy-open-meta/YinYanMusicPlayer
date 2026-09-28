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
;         -c Release -p:RuntimeIdentifierOverride=win-x64 -p:SelfContained=false `
;         -p:WindowsAppSDKSelfContained=true -o installer\build\client
;     ISCC.exe installer\client.iss /DMyClientVersion=2.2.0.0
;
; 安装结果：
;   C:\Program Files\YinYanMusic\          客户端（框架依赖：不含 .NET 运行时；
;                                          Windows App SDK 自包含，无需装 Windows App Runtime）
;   .NET 桌面运行时 10 前置：安装时自动检测（见 PrepareToInstall），缺失时引导用户
;   从微软官方下载并安装（aka.ms 永久链，始终指向 10.0 最新补丁版），装完继续安装
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
; 例外：一键脚本的 -NoSign 本地快速路径传 /DMySkipSign=1 显式跳过签名，
; 此时卸载器签名也一并关掉（没有 SignTool 就签不了卸载器）。
#ifndef MySkipSign
  SignTool={#MySignTool}
#else
  SignedUninstaller=no
#endif
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
; ── .NET 桌面运行时 10 前置（框架依赖发布带来的检测与引导，见 PrepareToInstall）──
chinese.RuntimeAsk=本机未检测到音言音乐运行所需的「.NET 桌面运行时 10」。\n\n是否现在自动下载（约 55MB，微软官方）并安装？\n\n「是」= 自动下载并安装，完成后继续安装音言音乐；\n「否」= 打开微软官方下载页面，装好后请重新运行本安装程序。
chinese.RuntimeDownloading=正在下载 .NET 桌面运行时 10（约 55MB，微软官方）...
chinese.RuntimeInstalling=正在安装 .NET 桌面运行时 10...
chinese.RuntimeReboot=.NET 桌面运行时已安装完成。系统提示需要重启才能完全生效，建议稍后重启。\n\n现在继续安装音言音乐。
chinese.RuntimeAbortAuto=未能自动完成 .NET 桌面运行时 10 的安装（已为你打开微软官方下载页面）。\n请在官方页面下载并安装「.NET Desktop Runtime - Windows x64」后，重新运行本安装程序。
chinese.RuntimeAbortDecline=安装已取消：请先安装 .NET 桌面运行时 10（已为你打开微软官方下载页面），完成后重新运行本安装程序。

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
; 默认不勾：写入受信任的根属于系统信任链改动，让用户自己决定。
; 想默认勾选，把 Flags 去掉即可。
; 注意：它消除的是「未知发布者」，不会消除 SmartScreen 的云端信誉提示。
Name: "trustcert"; Description: "{cm:TrustTask}"; GroupDescription: "{cm:SignGroup}"; Flags: unchecked

[Files]
; 框架依赖客户端（不含 .NET 运行时；Windows App SDK 自包含随包）。
; .NET 桌面运行时 10 的检测与引导安装见 [Code] 的 PrepareToInstall。
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

// ── .NET 桌面运行时 10 前置：检测 + 引导安装 ──────────────────────────────────
// 客户端是框架依赖发布（减小安装包体积），目标机必须有 .NET 桌面运行时 10
// （Microsoft.WindowsDesktop.App 10.x）。Windows App SDK 已随包自包含，无需另装。
const
  // aka.ms 永久链：始终指向 10.0 的最新补丁版桌面运行时（win-x64）
  RuntimeDownloadUrl = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe';
  RuntimeDownloadPage = 'https://dotnet.microsoft.com/download/dotnet/10.0';
  RuntimeInstallerName = 'windowsdesktop-runtime-win-x64.exe';
  // 官方安装器"装好但需要重启"的退出码
  ERROR_SUCCESS_REBOOT_REQUIRED = 3010;

/// 检测本机是否装有 .NET 桌面运行时 10：枚举 <dotnet根>\shared\Microsoft.WindowsDesktop.App
/// 下的版本子目录，任一以 "10." 开头即视为可用。dotnet 根目录按 x64 机器级安装的
/// 默认位置取（{pf}\dotnet）；应用本身就是 win-x64，x64 运行时只会装在这里。
function DotNetDesktopRuntime10Installed(): Boolean;
var
  Dirs: String;
  Fr: TFindRec;
begin
  Result := False;
  Dirs := AddBackslash(ExpandConstant('{pf}')) + 'dotnet\shared\Microsoft.WindowsDesktop.App';
  if not DirExists(Dirs) then Exit;
  if FindFirst(Dirs + '\*', Fr) then
  begin
    try
      repeat
        if (Fr.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
          if Copy(Fr.Name, 1, 3) = '10.' then
          begin
            Result := True;
            Exit;
          end;
      until not FindNext(Fr);
    finally
      FindClose(Fr);
    end;
  end;
end;

/// 用系统自带 curl.exe（Win10 19041+ 必有，正是本应用支持的最低系统）把官方运行时
/// 安装器下载到临时目录。-f 让 HTTP 错误返回非 0，-L 跟随 aka.ms 的重定向。
/// 下完做一次最小体积校验（真实安装器约 55MB），防止把错误页当成 exe 存下来。
function DownloadRuntimeInstaller(var InstallerPath: String): Boolean;
var
  TmpFile: String;
  Rc: Integer;
  Fr: TFindRec;
begin
  Result := False;
  InstallerPath := ExpandConstant('{tmp}\' + RuntimeInstallerName);
  if not FileExists(ExpandConstant('{sys}\curl.exe')) then Exit;
  if FileExists(InstallerPath) then DeleteFile(InstallerPath);
  if not Exec(ExpandConstant('{sys}\curl.exe'),
       '-f -L --retry 2 --connect-timeout 20 -o "' + InstallerPath + '" "' + RuntimeDownloadUrl + '"',
       '', SW_HIDE, ewWaitUntilTerminated, Rc) then Exit;
  if Rc <> 0 then Exit;
  if not FindFirst(InstallerPath, Fr) then Exit;
  try
    Result := Fr.SizeLow > 10 * 1024 * 1024;   // 完整安装器约 55MB，明显小于它的一律当失败
  finally
    FindClose(Fr);
  end;
end;

/// 静默安装官方运行时。安装进程继承本安装器的管理员令牌（PrivilegesRequired=admin）。
/// /passive：显示进度条但不提问；/norestart：不自动重启。返回是否视为成功
/// （0 = 成功；3010 = 成功但需要重启，由调用方提示）。
function InstallRuntime(InstallerPath: String): Boolean;
var
  Rc: Integer;
begin
  WizardForm.StatusLabel.Caption := CustomMessage('RuntimeInstalling');
  WizardForm.Repaint;
  if Exec(InstallerPath, '/passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, Rc) then
  begin
    if Rc = ERROR_SUCCESS_REBOOT_REQUIRED then
      MsgBox(CustomMessage('RuntimeReboot'), mbInformation, MB_OK);
    Result := (Rc = 0) or (Rc = ERROR_SUCCESS_REBOOT_REQUIRED);
  end
  else
    Result := False;
end;

/// 安装正式开始前的最后一道闸：缺运行时就先补齐（自动，失败则打开官方下载页），
/// 返回非空字符串即中止安装并向用户展示该消息。
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstallerPath: String;
  Rc: Integer;
begin
  Result := '';
  if DotNetDesktopRuntime10Installed() then Exit;

  case MsgBox(CustomMessage('RuntimeAsk'), mbConfirmation, MB_YESNO) of
    IDYES:
      begin
        WizardForm.StatusLabel.Caption := CustomMessage('RuntimeDownloading');
        WizardForm.Repaint;
        if DownloadRuntimeInstaller(InstallerPath) then
        begin
          if InstallRuntime(InstallerPath) then
            Exit;   // 官方安装器返回 0/3010 即视为就绪，不再复查目录（避免自定义安装路径误判）
        end;
        ShellExec('open', RuntimeDownloadPage, '', '', SW_SHOWNORMAL, ewNoWait, Rc);
        Result := CustomMessage('RuntimeAbortAuto');
      end;
    IDNO:
      begin
        ShellExec('open', RuntimeDownloadPage, '', '', SW_SHOWNORMAL, ewNoWait, Rc);
        Result := CustomMessage('RuntimeAbortDecline');
      end;
  end;
end;

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
