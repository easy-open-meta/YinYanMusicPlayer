<#
.SYNOPSIS
    一键产出「音言音乐」Windows 客户端安装包（win-x64 / amd64）

.DESCRIPTION
    四步：
      1) 发布自包含客户端（目标机不需要装 .NET 10 / Windows App Runtime）
      2) 用证书给发布的客户端 exe 签名
      3) 检查 Inno Setup 编译器与中文语言包
      4) 用 Inno Setup 打包成单个 setup.exe，并给安装包与卸载器签名

    产物：
        installer\build\client\                        客户端发布输出（约 280MB）
        installer\build\out\YinYanMusic-Client-Setup-*.exe

    版本号默认取 App.csproj 的 <ApplicationDisplayVersion>（如 2.2.0），
    会补成 4 段（2.2.0.0）给安装包用，同时透传给 dotnet publish -p:Version。

    签名说明：
      证书私钥（.pfx）不放进仓库。运行前设置这两个环境变量，或让脚本提示你输入密码：
        YINYAN_PFX      证书路径（默认 %USERPROFILE%\certs\yinyan.pfx）
        YINYAN_CERT_PW  证书密码（不设则交互式提示输入）
      签名动作由 installer\sign.cmd 完成，它不需要 Windows SDK —— 用的是
      PowerShell 自带的 Set-AuthenticodeSignature。

.PREREQUISITES
    - .NET 10 SDK + MAUI workload（dotnet 在 PATH）
    - Inno Setup 6（winget install --id JRSoftware.InnoSetup -e）
      中文语言包 ChineseSimplified.isl 缺失时脚本会自动下载
    - installer\certs\yinyan.pem（公钥证书，随包附带，已在仓库里）
    - installer\sign.ps1 + installer\sign.cmd（签名工具）

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File installer\build-client-installer.ps1
    powershell -ExecutionPolicy Bypass -File installer\build-client-installer.ps1 -Version 2.2.0
    powershell -ExecutionPolicy Bypass -File installer\build-client-installer.ps1 -SkipPublish
    powershell -ExecutionPolicy Bypass -File installer\build-client-installer.ps1 -NoSign
#>
[CmdletBinding()]
param(
    # 版本号覆盖（默认读 App.csproj 的 ApplicationDisplayVersion）
    [string]$Version,

    # 已有 installer\build\client 时跳过发布，只重新编译安装包
    [switch]$SkipPublish,

    # 签名证书（.pfx）路径。留空则取 $env:YINYAN_PFX，
    # 再退到 %USERPROFILE%\certs\yinyan.pfx。私钥不要放进仓库。
    [string]$PfxPath,

    # 跳过签名，产出未签名的安装包。仅本地测试用，不要用来发布。
    [switch]$NoSign
)

$ErrorActionPreference = 'Stop'

$RepoRoot  = Split-Path -Parent $PSScriptRoot
$ClientDir = Join-Path $PSScriptRoot 'build\client'
$OutDir    = Join-Path $PSScriptRoot 'build\out'
$AppCsproj = Join-Path $RepoRoot 'src\YinYanMusic.App\YinYanMusic.App.csproj'

# 必须与 client.iss 里的 #define MySignTool 一致，也要与下面 /S 参数里的名字一致
$SignToolName = 'yinyan'
$AppExeName   = 'YinYanMusic.App.exe'

function Fail($msg) { Write-Host "❌ $msg" -ForegroundColor Red; exit 1 }

# ── 0. 版本号（默认取 csproj 的 ApplicationDisplayVersion）────────────────────
if (-not $Version) {
    try {
        [xml]$xml = Get-Content $AppCsproj -Raw
        $Version = ($xml.Project.PropertyGroup.ApplicationDisplayVersion | Where-Object { $_ } | Select-Object -First 1)
    } catch { }
    if (-not $Version) { Fail "读不到 $AppCsproj 里的 ApplicationDisplayVersion，请用 -Version 指定" }
}
# 安装包的 VersionInfoVersion 要 4 段数字；把 2.2.0 补成 2.2.0.0
$installerVersion = $Version
while (($installerVersion -split '\.').Count -lt 4) { $installerVersion += '.0' }
if ($installerVersion -notmatch '^\d{1,5}(\.\d{1,5}){3}$') {
    Fail "版本号必须是数字段（如 2.2.0 或 2.2.0.0），收到：$Version"
}
Write-Host "== YinYanMusic Windows 客户端安装包 ==" -ForegroundColor Green
Write-Host "   客户端版本：$Version（安装包版本 $installerVersion）"

# ── 1. 发布客户端（自包含 win-x64）──────────────────────────────────────────
if ($SkipPublish) {
    Write-Host '=== 1/4 跳过发布（-SkipPublish）===' -ForegroundColor Cyan
    if (-not (Test-Path (Join-Path $ClientDir $AppExeName))) {
        Fail "找不到 $ClientDir\$AppExeName，先不加 -SkipPublish 跑一次"
    }
} else {
    Write-Host '=== 1/4 发布客户端（win-x64 自包含）===' -ForegroundColor Cyan
    # ⚠️ 不能用 `-r win-x64`：这是全局属性，会渗到 App 的 android TFM（NU1102 找
    #    Microsoft.NETCore.App.Runtime.Mono.win-x64）以及被引用的 net10.0 工程（NETSDK1005）。
    #    用 MAUI/Windows SDK 认的 RuntimeIdentifierOverride 才只影响 Windows 目标。
    dotnet publish $AppCsproj `
        -f net10.0-windows10.0.19041.0 `
        -c Release `
        -p:RuntimeIdentifierOverride=win-x64 `
        -p:SelfContained=true `
        -p:WindowsAppSDKSelfContained=true `
        -p:Version=$Version `
        -o $ClientDir
    if ($LASTEXITCODE -ne 0) { Fail "dotnet publish 失败（exit $LASTEXITCODE）" }
    if (-not (Test-Path (Join-Path $ClientDir $AppExeName))) {
        Fail "发布完成，但 $ClientDir\$AppExeName 不存在"
    }
}

# ── 2. 签名客户端可执行文件 ─────────────────────────────────────────────────
# ── 1.5 版本一致性校验 ──────────────────────────────────────────────────────
# -SkipPublish 时最容易踩：安装包版本号取自 csproj，载荷却是上一次发布留下的，
# 会出现「安装包标 2.12.0.0、里面装的却是旧 exe」的错配。
$appExePath     = Join-Path $ClientDir $AppExeName
$appFileVersion = (Get-Item $appExePath).VersionInfo.FileVersion
if (-not $appFileVersion) { Fail "$AppExeName 读不到 FileVersion，无法校验版本，拒绝继续" }
if ($appFileVersion -ne $installerVersion) {
    Fail "版本不一致：安装包将标 $installerVersion，但载荷 $AppExeName 的 FileVersion 是 $appFileVersion。`n   → 去掉 -SkipPublish 重新发布，或用 -Version 指定与载荷一致的版本。"
}
Write-Host "   版本校验通过：载荷 FileVersion = $appFileVersion" -ForegroundColor DarkGray

# 必须放在 ISCC 之前：签完安装包再改包内内容，外层签名立刻失效。
# 只签入口 exe。自包含输出里有 600+ 个文件，绝大多数是 .NET / Windows App SDK
# 自带的，不需要也不应该逐个签。
if ($NoSign) {
    Write-Host '=== 2/4 跳过签名（-NoSign）===' -ForegroundColor Yellow
} else {
    Write-Host '=== 2/4 签名客户端可执行文件 ===' -ForegroundColor Cyan

    if (-not $PfxPath) {
        $PfxPath = if ($env:YINYAN_PFX) { $env:YINYAN_PFX } else { Join-Path $env:USERPROFILE 'certs\yinyan.pfx' }
    }
    if (-not (Test-Path $PfxPath)) {
        Fail "找不到签名证书：$PfxPath`n   用 -PfxPath 指定，或设置环境变量 YINYAN_PFX。`n   ⚠️ 私钥不要放进仓库目录。"
    }
    # 交给 sign.cmd（→ sign.ps1）读取；不放进命令行，避免出现在进程参数里
    $env:YINYAN_PFX = $PfxPath

    if (-not $env:YINYAN_CERT_PW) {
        $sec = Read-Host -Prompt '证书密码（未设置 YINYAN_CERT_PW）' -AsSecureString
        $env:YINYAN_CERT_PW = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR(
            [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
    }
    if (-not $env:YINYAN_CERT_PW) { Fail '证书密码为空' }

    & (Join-Path $PSScriptRoot 'sign.cmd') (Join-Path $ClientDir $AppExeName)
    if ($LASTEXITCODE -ne 0) { Fail "客户端 exe 签名失败（exit $LASTEXITCODE）" }
}

# ── 3. 定位 ISCC + 中文语言包 ───────────────────────────────────────────────
Write-Host '=== 3/4 检查 Inno Setup 编译器 ===' -ForegroundColor Cyan
$probe = @()
if ($env:LOCALAPPDATA) { $probe += (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe') }
if ($env:ProgramFiles) { $probe += (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe') }
$pf86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
if ($pf86) { $probe += (Join-Path $pf86 'Inno Setup 6\ISCC.exe') }
$iscc = $probe | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) { Fail ' 找不到 ISCC.exe。安装：winget install --id JRSoftware.InnoSetup -e' }
Write-Host "   ISCC: $iscc"

$langFile = Join-Path (Split-Path $iscc) 'Languages\ChineseSimplified.isl'
if (-not (Test-Path $langFile)) {
    $url = 'https://raw.githubusercontent.com/jrsoftware/issrc/main/Files/Languages/ChineseSimplified.isl'
    Write-Host "   中文语言包缺失，正在下载：$url"
    try {
        Invoke-WebRequest -Uri $url -OutFile $langFile -UseBasicParsing
        Write-Host "   已写入 $langFile"
    } catch {
        Fail "下载中文语言包失败：$($_.Exception.Message)。手动放到 $langFile 即可"
    }
}

# ── 4. 编译安装包 ───────────────────────────────────────────────────────────
Write-Host '=== 4/4 编译安装包（ISCC）===' -ForegroundColor Cyan

# 清掉上次留下的旧 setup：产物名带版本号，版本一变就会在 out 里并存两个。
# 只删同前缀的，目录里其他文件（update.json 之类）不动。
# 放在编译前而不是脚本最开头：这样发布/签名失败时，上一次的安装包还在，不会把退路先清掉。
if (Test-Path $OutDir) {
    $stale = @(Get-ChildItem -Path $OutDir -Filter 'YinYanMusic-Client-Setup-*.exe' -File -ErrorAction SilentlyContinue)
    foreach ($s in $stale) {
        Write-Host "   清理旧产物：$($s.Name)" -ForegroundColor DarkGray
        Remove-Item $s.FullName -Force
    }
    if ($stale.Count -eq 0) { Write-Host '   无旧产物需要清理' -ForegroundColor DarkGray }
}

$isccArgs = @("/DMyClientVersion=$installerVersion")

if (-not $NoSign) {
    # ⚠️ Inno Setup 6.x 只接受 [Setup] 里的 SignTool=<名字>，签名工具的**命令不能
    #    写在 .iss 里**（写了会报 Value of [Setup] section directive "SignTool" is invalid）。
    #    命令只能通过这个 /S 参数传进去。
    #    末尾的 $f 是 Inno 的占位符（替换成待签文件路径，并自动加引号），
    #    必须留在单引号里——写成双引号会被 PowerShell 当变量插值成空字符串。
    $isccArgs += '/S' + $SignToolName + '=' + $env:ComSpec + ' /c ' + (Join-Path $PSScriptRoot 'sign.cmd') + ' $f'
}

$isccArgs += (Join-Path $PSScriptRoot 'client.iss')
& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) { Fail "ISCC 编译失败（exit $LASTEXITCODE）" }

$setup = Join-Path $OutDir "YinYanMusic-Client-Setup-$installerVersion.exe"
if (-not (Test-Path $setup)) { Fail "编译返回成功，但没找到 $setup" }

$size = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host ''
Write-Host '=== 完成 ===' -ForegroundColor Green
Write-Host "  安装包：$setup  ($size MB)"
Write-Host "  客户端：$ClientDir"

# 断言产物真的带签名：ISCC 只在签名失败时报错，但多一道校验不亏
if ($NoSign) {
    Write-Host '  签名：已跳过（-NoSign）—— 不要用这个包发布' -ForegroundColor Yellow
} else {
    $sig = Get-AuthenticodeSignature -FilePath $setup
    if (-not $sig.SignerCertificate) { Fail '安装包已产出，但没有签名，检查 sign.cmd 是否执行成功' }
    Write-Host "  签名者：$($sig.SignerCertificate.Subject)"
    Write-Host "  签名状态：$($sig.Status)（自签证书显示 UnknownError 属正常，表示签名有效但链未被本机信任）"
}
Write-Host ''
Write-Host '  安装后：程序 C:\Program Files\YinYanMusic\；快捷方式「音言音乐」；'
Write-Host '          服务器地址若要预置，安装向导里填（写成机器级环境变量 YINYAN_API_BASEURL）。'
