# 音言音乐 · 构建、部署与安装使用指南

> 本文档覆盖从零搭建开发环境、生产构建（Windows 一键安装包）、安装流程、首次使用引导与常见问题排查。
>
> **推荐首次部署路线**：下载 `YinYanMusic-Setup-1.0.0.exe` → 安装 → 填配置 → 建超管 → 导入音乐 → App 填地址。连 PostgreSQL 都不用手动建表，API 首次启动自动完成。

---

## 目录

1. [前置条件](#1-前置条件)
2. [开发环境搭建](#2-开发环境搭建)
3. [开发构建与运行](#3-开发构建与运行)
4. [生产构建：Windows 一键安装包](#4-生产构建windows-一键安装包)
5. [安装流程](#5-安装流程)
6. [首次使用引导](#6-首次使用引导)
7. [PostgreSQL 环境配置与数据库迁移](#7-postgresql-环境配置)
8. [Android 客户端配置](#8-android-客户端配置)
9. [常见问题排查](#9-常见问题排查)

---

## 1. 前置条件

| 工具 | 版本 | 说明 |
|---|---|---|
| .NET SDK | ≥ 10.0.300 | [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download) |
| Node.js | ≥ 22 | [nodejs.org](https://nodejs.org)（前端构建用） |
| pnpm | ≥ 9 | `npm i -g pnpm` |
| PostgreSQL | ≥ 16 | [postgresql.org/download](https://www.postgresql.org/download/) |
| Inno Setup | 6.7.x | 仅打包用，[jrsoftware.org/isdl.php](https://jrsoftware.org/isdl.php)（非必须，开发可跳过） |

**Windows 安装包用户**仅需：PostgreSQL + 下载安装包（包体不含 .NET 运行时；安装器检测缺失时自动引导安装「.NET 桌面运行时 10」）。

---

## 2. 开发环境搭建

### 2.1 安装 .NET SDK

```powershell
# winget 安装（推荐）
winget install Microsoft.DotNet.SDK.10

# 或手动下载：https://dotnet.microsoft.com/download/dotnet/10.0
# 验证
dotnet --version   # 应显示 10.x.x
```

### 2.2 安装 PostgreSQL

```powershell
# Windows winget
winget install PostgreSQL.PostgreSQL --interactive

# 安装过程中记住：
#   - 端口：默认 5432
#   - 超级用户密码（后续要用）
```

**创建数据库**（用 pgAdmin 或 psql）：

```sql
CREATE DATABASE yinyan_music;
-- 用户已存在则跳过：
-- CREATE USER postgres WITH PASSWORD 'your_password';
-- GRANT ALL PRIVILEGES ON DATABASE yinyan_music TO postgres;
```

> 数据库不存在时 API 首次启动会自动建表（`EnsureCreated`），
> 表结构变更由 `Program.cs` 启动时的幂等 raw SQL 完成，**无需手动维护迁移脚本**。

### 2.3 克隆代码并安装前端依赖

```bash
git clone <your-repo-url>
cd yin-yan-music-player

# 安装前端依赖（Node 22 + pnpm）
cd web/admin
pnpm install
```

### 2.4 配置开发连接串

开发连接串在 `src/YinYanMusic.Api/bin/Debug/net10.0/appsettings.Development.json` 中。
**不要修改源码目录**下的文件，改用环境变量或 `dotnet user-secrets`：

```powershell
# 方式 A：环境变量（推荐，写入 PowerShell 配置文件使其持久化）
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=yinyan_music;Username=postgres;Password=你的密码"

# 方式 B：用户密钥（仅本机有效）
dotnet user-secrets set ConnectionStrings:Default "Host=localhost;Port=5432;Database=yinyan_music;Username=postgres;Password=你的密码" --project src/YinYanMusic.Api
```

### 2.5 安装 MAUI 工作负载（可选，仅构建 Android / Windows 客户端需要）

```powershell
dotnet workload install maui
```

---

## 3. 开发构建与运行

### 3.1 启动 API（开发模式）

```powershell
# 进入仓库根目录
cd D:\Repos\yin-yan-music-player

# 开发模式：监听 localhost:5116 + 暴露 OpenAPI 文档
dotnet run --project src/YinYanMusic.Api --environment Development

# 真机调试：监听所有网卡（手机/平板可访问）
dotnet run --project src/YinYanMusic.Api --environment Development --urls "http://0.0.0.0:5116"
```

**验证 API 是否正常**：

```powershell
# 本机验证
curl http://localhost:5116/api/songs?page=1

# 真机验证（手机与电脑同一局域网）
curl http://<电脑IPv4>:5116/api/songs?page=1
```

**API 关键端口与路径**：

| 用途 | 地址 |
|---|---|
| API 基址 | `http://localhost:5116` |
| 音乐资源 | `/media/audio/...` |
| API 文档 UI | `/scalar/v1`（开发默认开；生产需 `OpenApi__Enabled=true`） |
| OpenAPI 契约 | `/openapi/v1.json`（同上开关；`gen:api` 依赖此路径） |
| 引导码 | API 启动日志（搜「引导码」） |

### 3.2 启动前端开发服务器

```powershell
cd web/admin

# 开发服务器（Vite proxy 自动转发 /api 到 localhost:5116）
pnpm dev

# 构建生产产物
pnpm build
# 产物在 web/admin/dist/
```

> Vite proxy 配置：将 `/api` 与 `/media` 反代到 `http://localhost:5116`，
> 开发时无需配置 CORS、不用改 API 监听地址。

### 3.3 打开后台管理页

```
浏览器访问：http://localhost:5116/
```

- **无超管时**：自动落到 `/register`，用引导码创建超管
- **已有超管时**：重定向到 `/login`
- 后台管理页面在 `/` 下，完整路径：`http://localhost:5116/`（SPA，前端路由无刷新跳转）

### 3.4 构建 Android 客户端（可选）

```powershell
# 设置 Android SDK 环境变量（如果不在 PATH）
$env:ANDROID_HOME = "D:\Android\android-sdk"
$env:JAVA_HOME = "D:\Android\openjdk\jdk-17.0.8.101-hotspot"

# 构建 Debug APK
dotnet build src/YinYanMusic.App/YinYanMusic.App.csproj -f net10.0-android -c Debug

# 安装到真机（确保 adb 可用）
dotnet build src/YinYanMusic.App/YinYanMusic.App.csproj -f net10.0-android -c Debug -t:Install -p:AdbTarget="-s 3e534fe0"
```

### 3.5 构建 Windows 客户端（可选）

```powershell
dotnet build src/YinYanMusic.App/YinYanMusic.App.csproj -f net10.0-windows10.0.19041.0 -c Debug
```

---

## 4. 生产构建：Windows 一键安装包

### 4.1 构建步骤

```powershell
# 1. 进入仓库根目录
cd D:\Repos\yin-yan-music-player

# 2. 跑发布脚本（全自动）
#    - 构建前端 dist
#    - dotnet publish self-contained win-x64 单文件
#    - 拷贝 dist → wwwroot/
#    - 生成默认 appsettings.json
./publish-win.ps1

# 3. 用 Inno Setup 编译安装包
#    ISCC.exe 通常在这里：
$iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
Set-Location installer
& $iscc installer.iss
```

> 步骤 2 产物目录：`installer/build/api/`
>
> 步骤 3 产物：`installer/output/YinYanMusic-Setup-1.0.0.exe`

### 4.2 构建产物说明

```
installer/build/api/
├── YinYanMusic.Api.exe       ← self-contained 单文件（含 .NET 运行时，~80MB）
├── wwwroot/                  ← 前端静态文件
│   ├── index.html
│   └── assets/               ← JS / CSS chunks（带 hash，可长缓存）
└── appsettings.json          ← 默认占位配置（安装向导会替换）

installer/output/
└── YinYanMusic-Setup-1.0.0.exe  ← 可分发的安装包
```

### 4.3 publish-win.ps1 详细说明

```powershell
# 如需自定义配置，可在脚本中修改默认连接串/JWT 密钥，或在跑完后编辑
# installer/build/api/appsettings.json

# 跳过前端构建（已有 dist）
# → 直接跑 dotnet publish + 拷贝（节省时间）
```

### 4.4 Linux 部署构建（脚本方案）

无安装包，直接用脚本打包：

```bash
# 1. 在构建机上跑 publish-linux.sh
chmod +x publish-linux.sh
./publish-linux.sh

# 产物目录：installer/build/linux/
#   ├── yinyan-music      ← self-contained 二进制
#   ├── wwwroot/          ← 前端静态文件
#   ├── appsettings.json  ← 配置模板
#   └── setup.sh          ← 一键安装脚本
```

发布版本：`linux-x64` self-contained，包含 .NET 运行时，目标机无需预装 .NET。

---

## 5. 安装流程

### 5.1 运行安装包

1. 下载 `YinYanMusic-Setup-1.0.0.exe`
2. **右键 → 以管理员身份运行**（注册 Windows 服务需要 admin 权限）
3. 向导语言选择「简体中文」

### 5.2 配置向导页

**第一页：服务器配置**

| 字段 | 说明 | 推荐值 |
|---|---|---|
| 监听端口 | API 和前端共用端口 | `5116`（默认） |
| PostgreSQL 连接串 | 请替换 `Password=` 后的真实密码 | `Host=localhost;Port=5432;Database=yinyan_music;Username=postgres;Password=你的密码` |
| JWT 签名密钥 | 默认已自动生成 40 位随机字符串 | 保持默认即可（生产必须改） |
| 音乐目录 | 导入音乐用，可后填 | 空（安装后可在 `%ProgramData%\YinYanMusic\appsettings.json` 中补） |

**第二页：附加选项**

- ✅ **注册为 Windows 服务并开机自启**（推荐，保持勾选）
- ✅ **允许防火墙放行该端口**（局域网手机/平板能访问，保持勾选）

### 5.3 安装完成后

安装程序会：
1. 将配置文件写入 `%ProgramData%\YinYanMusic\appsettings.json`
2. 注册 Windows 服务 `YinYanMusic.Api` 并自动启动
3. 防火墙放行该端口
4. **弹出完成对话框，展示超级管理员引导码**

> 若未看到引导码提示，重启服务后在服务日志里查（引导码随每次启动重新生成，见 Q4）。

### 5.4 配置文件位置

| 用途 | 路径 |
|---|---|
| API 配置 | `%ProgramData%\YinYanMusic\appsettings.json` |
| 服务日志 | `%ProgramData%\YinYanMusic\logs\`（如果有） |
| 程序目录 | `C:\Program Files\YinYanMusic\` |

### 5.5 Linux 安装（setup.sh）

**发布 Linux 部署包**（在 Linux 构建机或 WSL 上）：

```bash
chmod +x publish-linux.sh
./publish-linux.sh
```

产物在 `installer/build/linux/`，将整个目录 `scp` 到目标 Linux 服务器，然后运行：

```bash
chmod +x setup.sh
sudo ./setup.sh
```

`setup.sh` 会自动：

| 步骤 | 说明 |
|---|---|
| 检测 PostgreSQL | 运行时检查 `pg_isready`，不存在则警告 |
| 询问配置 | 端口、连接串（默认 localhost 5432）、音乐目录 |
| 自动创建数据库 | 若不存在，用连接串凭据 `psql` 建 `yinyan_music` |
| 安装文件 | 二进制 → `/opt/yinyan-music/`，配置 → `/etc/yinyan-music/` |
| 创建系统用户 | `yinyan`（无登录 shell，仅运行服务） |
| 生成 JWT | 自动生成 40 位随机密钥 |
| systemd 服务 | 注册 `yinyan-music.service` → 开机自启 + 立即启动 |
| 展示引导码 | 服务启动日志会打印引导码，`journalctl -u yinyan-music` 查看 |

**Linux 关键路径**：

| 用途 | 路径 |
|---|---|
| 程序目录 | `/opt/yinyan-music/` |
| 配置文件 | `/etc/yinyan-music/appsettings.json` |
| 服务日志 | `journalctl -u yinyan-music -f` |
| systemd 服务名 | `yinyan-music` |
| 卸载脚本 | `sudo /opt/yinyan-music/uninstall.sh` |

**查看服务状态**：

```bash
systemctl status yinyan-music
journalctl -u yinyan-music -f   # 实时日志
```

**手动重启**：

```bash
sudo systemctl restart yinyan-music
```

---

## 6. 首次使用引导

### 6.1 创建超级管理员

1. 浏览器打开：`http://localhost:5116/`
2. 系统自动检测无超管，落到 `/register`
3. 填写超级管理员引导码（安装完成对话框里显示的码，或到服务启动日志里查看，见 Q4）
4. 设置用户名和密码
5. 点「注册」→ 超管创建成功 → 引导码立即作废 → 页面跳到 `/login`
6. 用超管账号登录后台

### 6.2 导入音乐

1. 后台菜单 → **系统任务**
2. 「导入音乐」卡片：留空使用配置文件中的音乐目录，或填入服务器上的音乐文件夹路径（如 `E:\Music`）
3. 点「执行导入」
4. 导入完成后歌曲出现在「歌曲」列表

> 音乐目录格式要求：`艺术家 - 歌曲名.mp3`（支持 .mp3 / .flac / .m4a / .wav / .ogg）
> 示例：`周杰伦 - 晴天.mp3`、`Taylor Swift - Blank Space.flac`

### 6.3 配置 MAUI App（Android）

1. 在手机上安装 MAUI App（Debug APK 或自行构建）
2. 打开 App → 登录页底部或设置页
3. 在「服务器地址」填：`http://<电脑IPv4>:5116`
4. 点「测试连接」确认连通
5. 返回登录页，用超管账号或普通用户账号登录

> **Android 明文访问说明**（2026-09-22 修正）：明文许可**只认** `networkSecurityConfig` ——
> 声明了它之后，manifest 的 `android:usesCleartextTraffic` 会被系统忽略。
> - **Debug 包**：引用 `xml/network_security_config_debug.xml`（`<base-config cleartextTrafficPermitted="true">`），
>   可连任意明文 http 地址。选用哪个文件由 csproj 的 `AndroidManifestPlaceholders`（`${nscRes}`）按构建配置决定。
> - **Release 包**：引用 `xml/network_security_config.xml`，默认只走 HTTPS，仅 `10.0.2.2 / 127.0.0.1 / localhost` 放行明文。
>   要放行其它地址，把 IP 加进它的 `<domain-config cleartextTrafficPermitted="true">` 后重新构建。
> - 更省事的联调方式：USB 连接后执行 `adb reverse tcp:5116 tcp:5116`，App 里填 `http://127.0.0.1:5116`
>   （127.0.0.1 在严格版白名单里，不受开发机 IP 变化影响）。
> - ⚠️ 别再往 nsc 里写 `<debug-overrides><base-config cleartextTrafficPermitted="true"/>`：它只支持
>   `<trust-anchors>`，那条"Debug 全开"的配置从未生效，表现是 App 内只有一句含糊的 `Connection failure`。

### 6.4 普通用户注册

App 自助注册始终开放。用户在 App 内注册 → 普通用户身份（role=user），
可正常使用音乐播放、收藏、关注等功能。超管可在后台「用户」页面管理这些账号。

---

## 7. PostgreSQL 环境配置

### 7.1 默认配置

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=yinyan_music;Username=postgres;Password=你的密码"
  }
}
```

### 7.2 修改连接串

编辑 `%ProgramData%\YinYanMusic\appsettings.json`，改 `ConnectionStrings:Default` 的值，
然后**重启服务**：

```powershell
sc stop YinYanMusic.Api
sc start YinYanMusic.Api
```

### 7.3 迁移数据库（P0 起走 EF 迁移）

结构变更已改为 **EF Core 迁移**，API 启动时自动 `Migrate()`，**无需手动执行 SQL**：

- **全新库**：自动建全表 + 历史表。
- **老库（EnsureCreated 建的，无 `__EFMigrationsHistory`）**：首次启动自动"基线缝合"——
  先跑幂等 SQL 对齐结构（补 `CoverUrl` / `IsSystem` / `Role` / `IsDisabled` / `Slogan` 等列、
  补 `ArtistFollows` 表），再写入 `InitialCreate` 基线记录，然后应用迁移。
- **种子数据**：5 条分区（华语/粤语/K-Pop/欧美/日语专区）仍在启动时 `ON CONFLICT DO NOTHING` 写入，
  人工改过的分类名不会被覆盖。

**升级前请先备份数据库**。手工操作（可选）：

```bash
# 查看待应用迁移
dotnet tool run dotnet-ef migrations list --project src/YinYanMusic.Data --startup-project src/YinYanMusic.Api

# 手工应用 / 回滚到指定迁移
dotnet tool run dotnet-ef database update --project src/YinYanMusic.Data --startup-project src/YinYanMusic.Api
dotnet tool run dotnet-ef database update <上一个迁移名> --project src/YinYanMusic.Data --startup-project src/YinYanMusic.Api
```

### 7.4 API 文档访问与限制（P1）

API 文档由配置开关 `OpenApi:Enabled` 控制：**开发默认开、生产默认关**。

| 环境 | 行为 |
|---|---|
| Development | `/scalar/v1` 与 `/openapi/v1.json` 可直接访问，根路径 `/` 302 跳转到 `/scalar/v1` |
| Production（默认） | 两个端点均 404 |
| Production + `OpenApi__Enabled=true` | 两个端点可访问 |

生产如需开放文档，**必须由 Nginx 限制访问**（API 绑 `0.0.0.0` 且 CORS 全开，
完整接口清单直接暴露到公网不合适）。二选一：

```nginx
# 方式 A：内网 IP 白名单
location /scalar {
    allow 192.168.0.0/16;
    allow 10.0.0.0/8;
    deny all;
    proxy_pass http://127.0.0.1:5116;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
}
location /openapi {
    allow 192.168.0.0/16;
    allow 10.0.0.0/8;
    deny all;
    proxy_pass http://127.0.0.1:5116;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
}
```

```nginx
# 方式 B：Basic Auth（需要 htpasswd 生成密码文件）
location /scalar {
    auth_basic "API Docs";
    auth_basic_user_file /etc/nginx/.htpasswd;
    proxy_pass http://127.0.0.1:5116;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
}
location /openapi {
    auth_basic "API Docs";
    auth_basic_user_file /etc/nginx/.htpasswd;
    proxy_pass http://127.0.0.1:5116;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
}
```

**UI 内调试 admin 接口**：打开 `/scalar/v1`，点右上角认证入口，粘贴登录接口返回的 JWT
（**直接贴 token，不要带 `Bearer ` 前缀**）；admin 接口需要 admin 角色账号的 token。

---

## 8. Android 客户端配置

### 8.1 API 地址配置（三层优先级）

```
① 应用内设置（用户手动指定，最高优先级）
② 环境变量 YINYAN_API_BASEURL
③ 配置文件 api.json
④ 平台默认值
```

### 8.2 配置文件路径

| 平台 | 路径 |
|---|---|
| Android | `FileSystem.AppDataDirectory/api.json` |
| Windows | `%LOCALAPPDATA%\YinYanMusic\api.json`（便携模式也支持程序目录 `api.json`） |

### 8.3 修改默认 LAN IP

**已经不需要再改代码里的 IP 了**（2026-09-22 起）：开发机 IP 由 DHCP 分配、会变，
把 IP 写进 nsc 只会变成过期的假白名单。现在：
- **调试**：用 Debug 包（明文全开）—— 设置页填任意 `http://<IP>:5116` 都能连；
  或用 `adb reverse tcp:5116 tcp:5116` + `http://127.0.0.1:5116`（推荐，IP 变了也不用管）。
- **Release 包要连内网**：编辑 `.../xml/network_security_config.xml`，把 IP 加进
  `<domain-config cleartextTrafficPermitted="true">` 再重新构建（Release 有意保持严格）。

---

## 9. 常见问题排查

### Q1：安装完成后浏览器打不开 `http://localhost:5116/`

```powershell
# 检查服务是否在运行
sc query YinYanMusic.Api

# 查看服务日志（如果有）
Get-Content "$env:ProgramData\YinYanMusic\logs\*.log" -Tail 50
```

### Q2：服务启动时报数据库连接错误

1. 确认 PostgreSQL 在运行（服务管理器或 `pg_isready`）
2. 确认连接串中的密码正确
3. 确认数据库 `yinyan_music` 已创建
4. 确认 pg_hba.conf 允许本机免密或 md5 认证

```powershell
# 快速验证连接
psql -h localhost -U postgres -d yinyan_music -c "SELECT 1"
```

### Q3：安装时「注册 Windows 服务失败」

手动注册：

```powershell
sc create YinYanMusic.Api binPath= "C:\Program Files\YinYanMusic\YinYanMusic.Api.exe --environment=Production --urls=http://0.0.0.0:5116 --contentRoot=C:\Program Files\YinYanMusic" DisplayName= "音言音乐 API" start= auto
sc description YinYanMusic.Api "音言音乐 API + 后台前端"
sc start YinYanMusic.Api
```

### Q4：超级管理员引导码在哪里

引导码**不再写文件**，只在 API 启动时打印到日志（超管建成即作废）：

```
Windows（服务）:  事件查看器 / 服务日志，或重启服务观察控制台输出
Windows（手动）:  启动 API 时的控制台窗口
Linux systemd:    journalctl -u yinyan-music | grep 引导码
Docker:           docker logs <容器名> | grep 引导码
```

> 日志行形如：`[引导码] 系统还没有超级管理员。创建超管需要这个一次性引导码：XXXXX-XXXXX-XXXXX-XXXXX-XXXXX`
> 引导码格式：`XXXX-XXXX-XXXX-XXXX-XXXX`（共 20 位字母数字）。
> 码只存在于进程内存中：超管创建成功后立即作废；重启服务会重新生成新码（旧码随之失效）。

### Q5：引导码校验失败 403

1. 确认码是从正确文件读取的（无多余空格或换行）
2. 确认系统时间正确（FixedTimeEquals 有时间窗口要求）
3. 如仍不行，临时关闭引导码校验：

编辑 `%ProgramData%\YinYanMusic\appsettings.json`，加入：

```json
"Auth": {
  "AllowAppRegistration": true,
  "RequireBootstrapCode": false
}
```

然后重启服务。之后**立即重置**为 `true`。

### Q6：Android App 连不上 API

1. 确认手机和服务器在同一局域网（`adb shell ping <电脑IP>`），或改用 USB 反向：
   `adb reverse tcp:5116 tcp:5116` + 地址填 `http://127.0.0.1:5116`（最稳，绕开 IP 与系统策略）
2. 确认防火墙已放行端口（安装时勾选了「允许防火墙放行」）
3. 确认 App「服务器地址」填的是电脑的**局域网 IP**（不是 `localhost`，除非用了上面的 adb reverse）
4. 明文策略：Debug 包不限明文；Release 包要 `network_security_config.xml` 放过对应 IP
5. ⚠️ 如果只有一句含糊的 `连不上：Connection failure`：那是 Android **明文策略**拦下的典型症状
   （.NET 把 Java 层的 `IOException: Cleartext HTTP traffic to x not permitted` 包成了这句话）。
   设置页「测试连接」会同时用默认栈与托管栈各试一次并显示完整异常链 —— 看到 `Cleartext ... not permitted`
   就按第 4 条处理；看到 `Connection refused` 才是地址/端口不对。

```powershell
# 查本机局域网 IP
ipconfig | findstr "IPv4"
# 通常是 192.168.x.x
```

### Q7：Windows 服务启动后立即停止

通常是 `appsettings.json` 语法错误（JSON 格式要求严格）或连接串无效。
检查：

```powershell
# 验证 JSON 格式
Get-Content "$env:ProgramData\YinYanMusic\appsettings.json" | ConvertFrom-Json
```

### Q8：数据库已存在但 API 报列不存在

API 每次启动自动执行幂等补列/补表 SQL。
如果某次更新引入了新列但启动失败，先确认 `appsettings.json` 语法正确，再查看日志。
**不要手动 ALTER TABLE**——所有结构变更都通过 `Program.cs` 的 raw SQL 进行。

### Q9：如何彻底卸载

1. **方式 A**：在「程序和功能」里找到「音言音乐 · 后台管理系统」，卸载
   - 卸载程序会停并删除服务、删除防火墙规则
   - 询问是否删除配置目录（建议保留）

2. **方式 B**：手动卸载

```powershell
# 停服务
sc stop YinYanMusic.Api
sc delete YinYanMusic.Api

# 删防火墙规则
netsh advfirewall firewall delete rule name="YinYanMusic.Api"

# 删程序目录
Remove-Item "C:\Program Files\YinYanMusic" -Recurse -Force

# 可选：删配置目录（会丢失所有配置和日志）
Remove-Item "$env:ProgramData\YinYanMusic" -Recurse -Force

# 可选：删数据库（会丢失所有数据）
# psql -U postgres -c "DROP DATABASE yinyan_music;"
```

---

## 附录：关键文件路径速查

| 场景 | 路径 |
|---|---|
| **Windows 安装包** | `installer/output/YinYanMusic-Setup-1.0.0.exe` |
| **Linux 构建脚本** | `publish-linux.sh`（仓库根） |
| **Linux 安装脚本** | `setup.sh`（产物目录内） |
| **Windows API 配置** | `%ProgramData%\YinYanMusic\appsettings.json` |
| **Linux API 配置** | `/etc/yinyan-music/appsettings.json` |
| **引导码** | 服务启动日志（Windows 服务日志 / `journalctl` / `docker logs`，搜「引导码」） |
| **Windows 服务名** | `YinYanMusic.Api` |
| **Linux systemd 服务名** | `yinyan-music` |
| **API 默认端口** | `5116` |
| **数据库名** | `yinyan_music` |
| **前台访问地址** | `http://localhost:5116/`（开发环境根路径 302 跳转到 `/scalar/v1`） |
| **API 文档 UI** | `http://localhost:5116/scalar/v1`（`OpenApi:Enabled` 控制，开发默认开） |
| **OpenAPI 契约** | `http://localhost:5116/openapi/v1.json`（同上开关，路径保持不变） |
| **MAUI 安卓明文配置** | `.../Resources/xml/network_security_config.xml`（严格，Release）/ `network_security_config_debug.xml`（明文全开，Debug）—— 由 csproj 的 `${nscRes}` 占位符按构建配置选用 |
| **前端源码** | `web/admin/` |
| **前端产物** | `web/admin/dist/` |
| **发布脚本（Windows）** | `publish-win.ps1`（仓库根） |
| **发布脚本（Linux）** | `publish-linux.sh`（仓库根） |
| **Inno Setup 脚本** | `installer/installer.iss` |
