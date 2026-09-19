<#
.SYNOPSIS
    一键产出「音言音乐」Windows 客户端安装包（win-x64 / amd64）

.DESCRIPTION
    两步：
      1) 发布自包含客户端（目标机不需要装 .NET 10 / Windows App Runtime）
      2) 用 Inno Setup 打包成单个 setup.exe

    产物：
        installer\build\client\                        客户端发布输出（约 280MB）
        installer\build\out\YinYanMusic-Client-Setup-*.exe

    版本号默认取 App.csproj 的 <ApplicationDisplayVersion>（如 2.2.0），
    会补成 4 段（2.2.0.0）给安装包用，同时透传给 dotnet publish -p:Version。

.PREREQUISITES
    - .NET 10 SDK + MAUI workload（dotnet 在 PATH）
    - Inno Setup 6（winget install --id JRSoftware.InnoSetup -e）
      中文语言包 ChineseSimplified.isl 缺失时脚本会自动下载

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File installer\build-client-installer.ps1
    powershell -ExecutionPolicy Bypass -File installer\build-client-installer.ps1 -Version 2.2.0
    powershell -ExecutionPolicy Bypass -File installer\build-client-installer.ps1 -SkipPublish
#>
[CmdletBinding()]
param(
    # 版本号覆盖（默认读 App.csproj 的 ApplicationDisplayVersion）
    [string]$Version,

    # 已有 installer\build\client 时跳过发布，只重新编译安装包
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'

$RepoRoot  = Split-Path -Parent $PSScriptRoot
$ClientDir = Join-Path $PSScriptRoot 'build\client'
$OutDir    = Join-Path $PSScriptRoot 'build\out'
$AppCsproj = Join-Path $RepoRoot 'src\YinYanMusic.App\YinYanMusic.App.csproj'

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
    Write-Host '=== 1/3 跳过发布（-SkipPublish）===' -ForegroundColor Cyan
    if (-not (Test-Path (Join-Path $ClientDir 'YinYanMusic.App.exe'))) {
        Fail "找不到 $ClientDir\YinYanMusic.App.exe，先不加 -SkipPublish 跑一次"
    }
} else {
    Write-Host '=== 1/3 发布客户端（win-x64 自包含）===' -ForegroundColor Cyan
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
    if (-not (Test-Path (Join-Path $ClientDir 'YinYanMusic.App.exe'))) {
        Fail "发布完成，但 $ClientDir\YinYanMusic.App.exe 不存在"
    }
}

# ── 2. 定位 ISCC + 中文语言包 ───────────────────────────────────────────────
Write-Host '=== 2/3 检查 Inno Setup 编译器 ===' -ForegroundColor Cyan
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

# ── 3. 编译安装包 ───────────────────────────────────────────────────────────
Write-Host '=== 3/3 编译安装包（ISCC）===' -ForegroundColor Cyan
& $iscc "/DMyClientVersion=$installerVersion" (Join-Path $PSScriptRoot 'client.iss')
if ($LASTEXITCODE -ne 0) { Fail "ISCC 编译失败（exit $LASTEXITCODE）" }

$setup = Join-Path $OutDir "YinYanMusic-Client-Setup-$installerVersion.exe"
if (-not (Test-Path $setup)) { Fail "编译返回成功，但没找到 $setup" }

$size = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host ''
Write-Host '=== 完成 ===' -ForegroundColor Green
Write-Host "  安装包：$setup  ($size MB)"
Write-Host "  客户端：$ClientDir"
Write-Host ''
Write-Host '  安装后：程序 C:\Program Files\YinYanMusic\；快捷方式「音言音乐」；'
Write-Host '          服务器地址若要预置，安装向导里填（写成机器级环境变量 YINYAN_API_BASEURL）。'
