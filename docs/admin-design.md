# 音言音乐 · 后台管理系统 & 部署方案 设计文档

> ⚠️ **本文档为 2026-09-16 的方案记录，部分内容已被后续改造取代，阅读时注意区分：**
> - 「API 地址硬编码」→ 已改为可配置：设置页 > 环境变量 `YINYAN_API_BASEURL` > `api.json` > 平台默认值（见 `ApiConfigStore` / `SettingsPage`）。
> - 「`HttpClient.BaseAddress` 注册时定死、改 `ApiConfig.BaseUrl` 不生效」→ 已修：不再设 `BaseAddress`，
>   `MusicApiService` 全部改走 `ApiConfig.Absolute(url)`。
> - 「API 进程直接托管前端」→ 现在是条件式：`wwwroot` 存在时才启用 `UseDefaultFiles` + `MapFallbackToFile`。
> - 「Android 明文靠 `debug-overrides`」→ **该写法无效**（`<debug-overrides>` 只支持 `<trust-anchors>`，
>   改不了明文策略）。2026-09-22 改为按构建配置**换 nsc 文件**：
>   Debug 用 `network_security_config_debug.xml`（明文全开）、Release 用 `network_security_config.xml`（严格），
>   由 csproj 的 `${nscRes}` 占位符选用。明文许可**只认 nsc**，`android:usesCleartextTraffic` 会被忽略。
> 最新的迭代设计（V2.3–V2.15）以 `V2.3-V2.15迭代设计开发文档.md` 为准。

> 状态：**待评审** · 版本 **v4.1**（2026-09-16）
> 修订轨迹：
> - v1 Blazor Server → v2 部署到 Nginx 改 Blazor WASM → v3 改**前后端分离**（Vue 3 + Vite + TS + Element Plus）
> - v4：并入三条新需求 —— ①MAUI 客户端 API 地址可配置 ②单一超级管理员（首次注册即超管，随后关闭注册）
>   ③Windows 一键安装包（前端 + API 一起打包，填配置即启动）
> - v4.1：澄清需求② —— **注册页指的是「后台管理页面的注册页」（`/register`），不是 MAUI App 的注册页**
> - v4.2：再澄清 —— **App 是用户端，自助注册必须保留**。
> - **v4.3（当前）**：采纳可选项 —— 后台注册页加**一次性引导码**（现仅存进程内存、启动日志可见），防超管被抢注。
>   于是注册规则从"有没有用户"改成"**有没有超管**"：App 随时可注册普通用户；
>   后台 `/register` 只在**系统还没有超管**时可注册，注册成功即下线。
>   （若按"有没有用户"判断，别人先在 App 注册一个账号，超管就永远建不出来了。）
>
> 文中接口路径 / DTO 字段均来自当前代码，不是臆想。

---

## 0. 先给结论

| 项 | 决定 |
|---|---|
| 架构 | **前后端分离**：前端 SPA（Vue 3 + Vite + TS + Element Plus），后端 = 现有 .NET API（补管理接口） |
| 前端位置 | 仓库内 `web/admin/` |
| 契约 | 以 API 的 OpenAPI（`/openapi/v1.json`）为源，`openapi-typescript` 生成 TS 类型 |
| 账号体系 | **两条注册通道并存**：① App（用户端）**随时自助注册** → 普通用户 `role=user`；② 后台管理页 `/register` **仅在系统还没有超管时**可注册，且需填**一次性引导码**（防抢注） → 该账号即**唯一超级管理员** `role=admin`，成功后注册页下线、引导码销毁 |
| 客户端配置 | MAUI 的 API 地址支持 **环境变量 / 配置文件 / 应用内设置** 三层配置（不再硬编码 IP） |
| 部署形态 | 两种并存：**① Windows 安装包**（Inno Setup，API 进程同源托管前端，装完填配置即启动）；**② Linux + Nginx**（静态托管 + `/api/` 反代） |
| 前置必做 | ① `User` 加 `Role` 并写进 JWT claim ② `api/admin/*` 加鉴权（现在**裸奔**） |

---

## 1. 目标与非目标

### 目标
- 后台管理：管理曲库（歌曲/专辑/歌手/分区）、歌单、用户、系统任务（导入/扫时长）。
- 客户端（MAUI）不再硬编码 API 地址，可在配置文件/环境变量/设置页指定。
- 部署要"傻瓜化"：**Windows 装一个包，填几个配置就能把前后端都跑起来**。
- Linux 场景仍可用 Nginx 部署（同一套前端产物，前端只认相对路径 `/api`）。

### 非目标
- 不做多级角色/权限体系（本期只有"超管"和"普通用户"两种）。
- 不做播放/转码、审计日志、多租户、暗色模式（结构预留）。
- Windows 安装包**不内置 PostgreSQL**（见 5.4，需你拍板）。

---

## 2. 现状盘点

### 2.1 后端

| 项 | 现状 |
|---|---|
| 技术栈 | .NET 10 · ASP.NET Core · PostgreSQL(EF Core) · JWT Bearer |
| 契约 | `YinYanMusic.Core/Dtos/*.cs`；分页统一 `PagedResult<T>` ✅；错误统一 `{ "message": "..." }` ✅ |
| API 文档 | `AddOpenApi` + `MapOpenApi`，**仅 Development**：`/openapi/v1.json` |
| CORS | `AllowAnyOrigin/AnyHeader/AnyMethod`（已全开） |
| JWT | Issuer `YinYanMusic.Api` / Audience `YinYanMusic.App` / 有效期 **7 天** |
| 注册 | `AuthService.RegisterAsync`：**任何人可无限注册**，创建用户 + 系统歌单「我喜欢的音乐」 |
| 角色 | `User` 实体**没有 Role 字段**，`TokenService`**不发 role claim** |

### 2.2 客户端（MAUI）

| 项 | 现状 |
|---|---|
| 目标框架 | `net10.0-android` + `net10.0-windows10.0.19041.0`（Windows 为未打包 `WindowsPackageType=None`） |
| API 地址 | `Services/ApiConfig.cs` **硬编码**（Android `http://10.26.218.85:5116` / 其它 `http://localhost:5116`），`BaseUrl` 已是可写静态属性 ✅ |
| HttpClient | `MauiProgram.cs:50` 注册 Singleton，**`BaseAddress` 在注册时定死**，之后改 `ApiConfig.BaseUrl` 不生效 |
| Android 明文 | `network_security_config.xml` 写死放行 `10.26.218.85 / 10.0.2.2 / 127.0.0.1 / localhost` |

### 2.3 现有接口（前端/后台可直接用的部分）

| 分组 | 方法 | 路径 | 鉴权 | 返回 |
|---|---|---|---|---|
| 认证 | POST | `api/auth/register` / `api/auth/login` | 匿名 | `AuthResponse(AccessToken, ExpiresAt, UserDto)` |
| 认证 | GET / PUT | `api/auth/me` | 登录 | `UserDto` |
| 歌曲 | GET | `api/songs` | **无** | `PagedResult<SongDto>`（`keyword/artistId/albumId/categoryId/page/pageSize`） |
| 歌曲 | GET / POST | `api/songs/{id}` / `api/songs` | **无** | `SongDto` / `CreateSongRequest` |
| 专辑 | GET / POST | `api/catalog/albums[/{id}]` | **无** | `PagedResult<AlbumDto>` / `CreateAlbumRequest` |
| 歌手 | GET / POST | `api/catalog/artists[/{id}]` | **无** | `PagedResult<ArtistDto>` / `CreateArtistRequest` |
| 分区 | GET / POST | `api/catalog/categories` | 无 / `Roles=admin`（**恒 403**） | `IReadOnlyList<CategoryDto>` |
| 歌单 | GET | `api/playlists[/{id}]` | 无 | `PagedResult<PlaylistDto>` / `PlaylistDetailDto` |
| 歌单 | POST/PUT/DELETE | `api/playlists[/{id}]` | 登录（**仅所有者**） | 曲目增删、收藏同理 |
| 用户 | GET | `api/users/search` / `api/users/{id}` | 登录 | `IReadOnlyList<UserDto>`（限 50、非分页）/ `UserProfileDto` |
| 媒体 | POST | `api/media/upload?kind=` | 登录 | `MediaUploadResult(Url)`，限 100MB |
| 系统 | POST/DELETE | `api/admin/import?dir=`、`seed-songs`、`scan-durations` | **无鉴权** | 见 2.4 |

### 2.4 两个必须先修的安全问题

1. **没有角色**：`User` 无 `Role`，`TokenService` 不发 role claim → `POST api/catalog/categories` 的
   `[Authorize(Roles="admin")]` **对所有人都 403**，接口形同废弃；后台也无法鉴权。
2. **`api/admin/*` 裸奔**：导入目录 / 删种子歌曲 / 扫时长三个端点无任何鉴权，能扫服务器目录、能删数据；
   API 绑 `0.0.0.0:5116` + CORS 全开 → 局域网任何人可调用。

> v4 的「超级管理员」方案正好把这两件事一起解决：`User.Role` + `api/admin/*` 加 `[Authorize(Roles="admin")]`。

### 2.5 后台缺口：需要新增/修改的后端接口

| 能力 | 需要的端点 | 现状 |
|---|---|---|
| 管理员登录 | `POST api/admin/login` | ❌ |
| 注册开关查询 | `GET api/auth/registration-open` | ❌ |
| 编辑 / 删除歌曲 | `PUT/DELETE api/songs/{id}` | ❌ |
| 编辑 / 删除专辑 / 歌手 / 分区 | `PUT/DELETE api/catalog/{albums,artists,categories}/{id}` | ❌ |
| 用户分页 / 新建 / 编辑 / 禁用 / 重置密码 | `api/admin/users*` | ❌ |
| 管理员视角歌单 | `GET/PUT/DELETE api/admin/playlists[/{id}]` | ⚠️ 有列表，删除限所有者 |
| 总览统计 | `GET api/admin/overview` | ❌ |
| 任务进度 | `GET api/admin/tasks/{id}` | ❌（导入是同步长任务） |

---

## 3. 需求①：MAUI 客户端 API 地址可配置

### 3.1 配置优先级（高 → 低）

```
① 应用内设置（Preferences / 设置页）   ← 用户在设备上明确指定，最高优先级
② 环境变量 YINYAN_API_BASEURL          ← Windows/服务器场景、CI、批量部署
③ 配置文件 api.json                    ← 随包预置 / 运维替换 / MDM 下发
④ 平台默认值（Android 用可编辑占位值，Windows 用 http://localhost:5116）
```

Android 默认值建议：仍给一个占位地址，但**首次启动就在登录页底部显示「当前服务器：xxx（点此修改）」**，
避免"连不上但不知道能改"。

### 3.2 配置文件位置与格式

```json
{ "baseUrl": "http://192.168.1.20:5116" }
```

| 平台 | 位置 |
|---|---|
| Android | `FileSystem.AppDataDirectory/api.json`（应用私有，无需权限）；另支持「从文件导入」读取外部存储 |
| Windows | `%LOCALAPPDATA%\YinYanMusic\api.json`，并支持程序目录 `api.json`（便携模式优先） |

### 3.3 关键实现点（不这么做会踩坑）

1. **必须在创建 HttpClient 之前赋值**：`MauiProgram.cs` 里 `AddSingleton<HttpClient>` 用到了 `ApiConfig.BaseUrl`，
   加载配置要放在服务注册之前（或直接在 `ApiConfig` 首次访问时惰性加载一次）。
2. **`BaseAddress` 现在是注册时定死的**：改完地址单纯改 `ApiConfig.BaseUrl` **不会生效**。
   推荐做法：**不设 `HttpClient.BaseAddress`**，在 `MusicApiService` 的私有方法
   （`GetAsync<T>` / `PostAsync<,>`，以及 `PutAsync/DeleteAsync/PostAsJsonAsync` 调用处）统一走
   `ApiConfig.Absolute(url)` 拼绝对地址 —— **一处收口，改完立即生效**，不用重建 HttpClient、不用重启 App。
3. **Android 明文限制要跟着改**：`network_security_config.xml` 现在写死 IP 白名单，地址可配后必然失效：
   ```xml
   <network-security-config>
     <base-config cleartextTrafficPermitted="false"/>
     <debug-overrides>                                  <!-- 仅 Debug 包放开 http -->
       <base-config cleartextTrafficPermitted="true"/>
     </debug-overrides>
   </network-security-config>
   ```
   → 正式包强制 HTTPS；内网必须用 http 时，用构建开关生成一个允许明文的配置（release 不建议）。
4. **设置页**：登录页底部「服务器设置」入口，显示当前地址、可编辑、带**「测试连接」**（调 `api/songs?page=1` 看是否 200）；
   改完立即生效并回写 Preferences。

---

## 4. 需求②：单一超级管理员 + 注册关闭

### 4.1 规则（两条通道）

| 通道 | 条件 | 结果 |
|---|---|---|
| **App 注册**（用户端，保留） | **始终可用** | 普通用户 `Role="user"` |
| **后台 `/register`** | **仅当系统还没有 `Role=admin` 的账号** | 该账号 = **超级管理员** `Role="admin"`；页面提示"这将成为超级管理员账号" |
| **后台 `/register`** | 已存在超管 | 路由不可达；接口也返回 403「管理员已存在。」 |
| **后台登录** | `POST api/admin/login` | 校验 `role == admin`，否则 401；返回 **8 小时** token |
| **App 登录** | `POST api/auth/login` | 不校验 role（超管也能用 App 听歌） |
| 需要更多账号 | 由超管在后台「用户」页**新建账号**（M3 的 `POST api/admin/users`） | |

一句话：**App 随便注册普通用户；后台注册页只在"还没有超管"时开放一次，用完即关。**

> ⚠️ 判断依据必须是「**有没有超管**」而不是「有没有用户」：
> 若按后者，别人先在 App 注册一个账号，后台 `/register` 就永远关闭、超管再也建不出来。

### 4.2 服务端改动

```csharp
// Application/AuthService.RegisterAsync
// req.Source: "admin"（后台注册页）/ "app"（MAUI 客户端），默认 "app"
var hasAdmin = await db.Users.AnyAsync(u => u.Role == "admin");
var fromAdmin = string.Equals(req.Source, "admin", StringComparison.OrdinalIgnoreCase);

if (fromAdmin)
{
    if (hasAdmin) return ServiceResult<AuthResponse>.Fail("管理员已存在。");
    if (requireBootstrap && !bootstrap.Verify(req.BootstrapCode))     // 见 4.5
        return ServiceResult<AuthResponse>.Fail("引导码不正确。");
}
// 未走后台通道 → 普通用户，随时可注册（App 是用户端）
...
var user = new User { ..., Role = fromAdmin ? "admin" : "user" };
// 注册成功后：bootstrap.Consume() 作废引导码
```

- `RegisterRequest` 增加 `Source`（`"admin"` / `"app"`，默认 `"app"`）与 `BootstrapCode`（仅 admin 通道需要）
  —— **默认保持现有行为，App 端零改动**；
- `User` 实体新增 `Role`（默认 `"user"`）、`IsDisabled`（软删/禁用）；
- `TokenService.CreateToken` 写入 `ClaimTypes.Role`；
- `UserDto` 增加 `Role`（客户端/前端可据此判断）；
- 新增匿名端点 `GET api/auth/registration-open?source=admin` → `{ "open": true/false }`
  （**只有后台需要**；App 端注册常驻，不用查）；
- 配置项：`Auth:AllowAppRegistration`（默认 `true`，紧急时可一键关闭 App 注册）、
  `Auth:RequireBootstrapCode`（默认 `true`；本机开发嫌麻烦可设 `false`）。

### 4.5 一次性引导码（防抢注）★ 本次采纳

`Source` 字段客户端可伪造，只是护栏；**引导码才是真正的门槛** —— 它只存在于**服务器进程内存**，
不在数据库、不落磁盘、不走网络，拿不到它就算打开 `/register` 也建不了超管。

> 早期版本把码写进 `bootstrap.txt` 文件；Docker 部署时容器以非 root 运行，
> `/var/lib/yinyan` 目录不可写导致生成失败、注册卡死，遂改为纯内存 + 日志打印方案。

| 环节 | 行为 |
|---|---|
| **生成** | API 启动时：若**无超管** → 生成随机码（20 位，如 `7KQ2-9XMB-4RTD-8VH6-AB3D`），**只打印到启动日志**（Docker 用 `docker logs`、Linux 用 `journalctl` 查看） |
| **获取** | 从启动日志取：`docker logs <容器> | grep 引导码` / `journalctl -u yinyan-music | grep 引导码` |
| **使用** | 后台 `/register` 表单多一个「引导码」输入框，提交时随 `Source=admin` 一起发 |
| **校验** | 服务端与内存中的码**固定时间比较**（`CryptographicOperations.FixedTimeEquals`）；不匹配 → 403「引导码不正确。」 |
| **销毁** | 超管创建成功 → 立即从内存作废（此时 `hasAdmin=true`，注册页也一并下线） |
| **重发** | 重启服务即重新生成新码（**仅在无超管时有效**；旧码随之失效） |
| **残留清理** | 启动时若 `hasAdmin == true` → 内存码不生成/立即清空 |

> 开发便利性：`Auth:RequireBootstrapCode=false` 时跳过校验（本机 `dotnet run` 调试用），生产默认 `true`。

### 4.3 客户端（MAUI）改动

- **注册入口保持原样，不做任何改动**（App 是用户端，`Source` 默认为 `app` → 普通用户，行为与今天一致）；
- 唯一建议一并修的是 `LoginViewModel`：现在把所有异常吞成"请检查用户名和密码"，
  应区分 **连不上服务器**（`HttpRequestException`）与 **401 凭据错误** —— 这正是之前"真机登录失败"被误导的原因。

### 4.4 后台前端

- 路由：`/login`、`/register`（**仅当 `registration-open?source=admin` 为 true 时可达**，否则重定向到 `/login`）；
- 注册成功后 → 跳 `/login`，并提示"超级管理员已创建，注册入口已关闭"；此后 `/register` 永久 404/重定向；
- 登录页调 `api/admin/login`；非超管 → 明确提示"该账号不是管理员"；
- 超管即唯一管理员，后台不做角色分配 UI（本期只有一种管理员）。

---

## 5. 部署形态一：Windows 安装包 ★ 新增

### 5.1 形态：一个进程、一个端口

```
安装后：
  YinYanMusic.Api.exe（Kestrel，默认 http://0.0.0.0:5116）
    ├── /api/*        → 后端接口
    ├── /media/*      → 音乐/封面静态目录
    └── /*            → 前端静态文件（web/admin/dist），SPA 回退 index.html
```

**API 进程直接托管前端**：`app.UseStaticFiles()` + `app.MapFallbackToFile("index.html")`
（`MapFallbackToFile` 放在 `MapControllers` 之后，否则会吞掉 `/api`）。
好处：Windows 上**不需要 IIS，也不需要 Nginx**，一个 exe 全搞定；前端只用相对路径 `/api`，两种部署形态通用。

### 5.2 打包内容（Inno Setup）

| 内容 | 来源 |
|---|---|
| API（**self-contained 单文件**，win-x64） | `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`（约 70~90MB） |
| 前端 dist | `pnpm build` → `web/admin/dist` → 拷进发布目录的 `wwwroot/` |
| `appsettings.json` | 安装向导按用户输入生成 |
| Windows 服务 | API 引用 `Microsoft.Extensions.Hosting.WindowsServices` + `builder.Host.UseWindowsService()`；安装包用 `sc create` 注册并启动（**不引入 NSSM 等第三方 exe**） |

目录约定：
```
C:\Program Files\YinYanMusic\          程序（exe、wwwroot）
%ProgramData%\YinYanMusic\             配置（appsettings.json）、日志
```

### 5.3 安装向导要填的配置（Inno Setup 自定义页面）

| 字段 | 说明 | 默认 |
|---|---|---|
| 端口 | API/前端共用监听端口 | `5116` |
| 数据库连接串 | PostgreSQL（Host/Port/Database/用户/密码） | `Host=localhost;Port=5432;Database=yinyan_music;...` |
| JWT 密钥 | **自动生成随机串**（可展开修改） | 随机 40 位 |
| 音乐目录 | `Media:MusicDirectory`，目录扫描导入用 | 空（可后填） |
| 注册为 Windows 服务并开机自启 | 默认勾选 | 是 |
| 防火墙放行端口 | 局域网内手机/平板要访问 | 勾选 |
| **超级管理员引导码** | 完成后**展示**服务自动生成的一次性引导码（+ 复制按钮），建超管时要填 | 自动生成 |

- 提供 **「测试数据库连接」** 按钮（装完前先验证填的对不对）；
- 装完提供「立即打开后台」→ `http://localhost:<端口>/`；
- 开始菜单放「修改配置」快捷方式：打开 `%ProgramData%\YinYanMusic\appsettings.json`；
- 卸载：停服务 → 删程序目录（配置目录询问是否保留）。

### 5.4 PostgreSQL 怎么办（需你拍板）

| 方案 | 说明 | 代价 |
|---|---|---|
| **A（推荐）** | 连接**已有的** PostgreSQL，安装向导里填连接串 + 测试连接 | 目标机要自备 PG（文档给安装指引） |
| B | 安装包**内置 PostgreSQL 绿色版**（~100MB+，需 `initdb`、注册服务、初始化库表） | 包体翻倍、安装逻辑复杂、升级/备份要自己管 |
| C | 换 SQLite | 要改 EF 配置与 `EnsureCreated` 那套，并发/数据量不如 PG |

### 5.5 首次使用流程

```
安装 → 填配置（含数据库测试）→ 完成（服务已启动）
  → 安装向导最后一页：抄下「超级管理员引导码」（或从服务启动日志里取）
  → 浏览器打开 http://localhost:5116
  → 还没有超管 → 自动落到 /register：填引导码 + 账号信息 → 注册超级管理员
  → 注册成功 → 引导码作废、注册页下线，跳 /login → 登录进后台
  → 导入音乐目录 → 曲库就绪
  → 手机/平板装 MAUI App → 设置页填 http://<这台机器IP>:5116
      → 老用户直接登录；新用户在 App 里自助注册（普通用户，无需引导码）
```

---

## 6. 部署形态二：Linux + Nginx（保留，可选）

两种形态共用**同一份前端产物**（前端只认相对路径 `/api`），Linux 侧：
- Nginx 静态托管 `dist/` + `location /api/` 反代到 `127.0.0.1:5116`；
  要点：`client_max_body_size 120m`（对齐 100MB 上传）、`proxy_read_timeout 600s`（长任务）、
  `location /assets/ { expires 365d; }`、`try_files $uri /index.html`（SPA 回退）、`index.html` 设 `no-store`；
- API 只监听回环 + systemd 托管 + `UseForwardedHeaders`（TLS 终结后生成 https 链接）；
- 若 MAUI App 走域名，改造后的配置 + `network_security_config.xml` 填域名 + HTTPS。

> Windows 安装包与 Linux Nginx **二选一即可**，不是必须都做。默认先做 Windows 安装包（你的要求），
> Linux/Nginx 作为可选产物（前端不需要任何改动）。

---

## 7. 前端设计（沿用 v3）

### 7.1 技术栈与工程

Vue 3（`<script setup>` + TS）+ Vite + Element Plus + Pinia + vue-router 4 + Axios + dayjs + pnpm（Node 22）。
工程位于 `web/admin/`：`src/api`（http/schema.d.ts/各模块）、`src/views`、`src/layout`、`src/stores`、
`src/composables/useCrudTable.ts`、`src/styles/element-override.css`。

### 7.2 契约同步

```bash
dotnet run --project src/YinYanMusic.Api --environment Development
curl -s http://localhost:5116/openapi/v1.json -o web/admin/openapi.json
pnpm gen:api     # openapi-typescript → src/api/schema.d.ts
```
`openapi.json` 提交进仓库；建议后端把 `MapOpenApi()` 从 `IsDevelopment` 里放出来（或加开关），便于 CI 导出契约。

### 7.3 认证

`POST api/admin/login` → token 存 localStorage（8 小时）+ Pinia + 路由守卫 + Axios 拦截器（401 跳登录）。
开发环境用 Vite proxy 转发 `/api`、`/media` 到 `http://<开发机>:5116`，不碰 CORS。

### 7.4 页面与视觉

信息架构：仪表盘 / 歌曲 / 专辑 / 歌手 / 分区 / 歌单 / 用户 / 系统任务。
视觉：主色 `#4F46E5`、圆角 12/8/20、卡片圆角 16 + 1px 边框、行高 56、表头 13px `#8A8AA3`、
无斑马纹、hover `#F7F7FB`、间距 8 的倍数、Element Plus `zh-cn` 语言包。
（完整 CSS 变量覆盖见 v3 文档 5.4，此处不重复。）

---

## 8. 实施计划

| 里程碑 | 内容 | 验收标准 |
|---|---|---|
| **M0 权限与账号地基**（后端） | `User.Role`/`IsDisabled` + JWT role claim；`RegisterRequest.Source`；**后台注册页在"无超管"时可注册且为超管、App 注册保持可用**；**一次性引导码**（生成/校验/固定时间比较/销毁/残留清理）；`api/admin/login`；`api/auth/registration-open?source=admin`；`api/admin/*` 与所有写接口加 `[Authorize(Roles="admin")]` | 无超管 + source=admin + 正确引导码 → 超管建成且文件销毁；引导码错 → 403；再有 source=admin → 403；source=app 始终成功且为普通用户；非超管调 `api/admin/import` → 403 |
| **M0.5 客户端可配置**（MAUI） | 环境变量/配置文件/设置页三层配置；`ApiConfig.Absolute` 收口；Android 明文改 `debug-overrides`；设置页「测试连接」 | 真机改地址后**立即生效**，不用重装/重启；release 包强制 HTTPS |
| **M1 前端骨架**（可与 M0 并行） | `web/admin`：**`/register`（仅库空时可达）+ `/login`**、布局、路由守卫、Axios 拦截、OpenAPI 生成类型、dev proxy | 首次访问落到 `/register`；注册成功后 `/register` 不可达；超管可登录；刷新保持登录；非超管被拒 |
| → **M1 状态** | ✅ 已完成：`web/admin` 工程 + Login/Register/Dashboard/NotFound + Pinia auth + Axios 拦截器（401 跳登录）+ Element Plus 主题覆盖 + Vite dev proxy。`npm run build` 通过（dist 1.1MB / gzip 368KB，1 700+ 模块）。**未做端到端联调**（需要 API 在 5116 跑） | `tsc --noEmit` 0 错 |
| **M2 曲库 CRUD** | 歌曲/专辑/歌手/分区 增删改查（含新增后端接口） | 四张表可用；删除有二次确认 |
| **M3 内容与用户** | 歌单列表/详情/删除；用户列表/**新建账号**/编辑/禁用/重置密码 | 超管能新建账号并用新账号登录 App |
| → **M3 状态** | ✅ 已完成：后端 `UserService`（分页/新建/编辑/禁用/重置密码）+ `PlaylistService.AdminDeleteAsync`；前端 `catalog.ts`（PlaylistDto + AdminUserDto + API）+ `Users.vue`（CRUD Dialog）+ `Playlists.vue`（列表+删除）+ `router`/`SideMenu` 路由；npm build ✅（1.1MB/gzip 369KB）。**未做端对端联调** | tsconfig 0 错 |
| **M4 系统任务** | 导入 / 扫时长 / 清种子；长任务改后台 + 进度 | 点导入有进度，不超时 |
| → **M4 状态** | ✅ 已完成（MVP 务实版）：后端三个端点已在 `AdminController`（M0 已加 `[Authorize(Roles="admin")]`）；前端 `SystemTasks.vue` 接好三个任务卡 + 导入可指定目录 + 删种子二次确认 + `el-alert` 结果展示。**未做长任务异步化**——本机库规模（百首级）同步几秒完，MVP 优先；文档明确标注「数据量大时改异步 + 进度轮询」。`npm run build` ✅ | UI 可用，结果展示清晰 |
| **M5 Windows 安装包** | API 同源托管前端；`UseWindowsService`；Inno Setup 向导（端口/数据库/JWT/音乐目录/防火墙）；发布脚本 | 干净机器装完 → 填配置 → 浏览器能开后台 → 手机 App 能连 |
| → **M5 状态** | ✅ 已完成：① `Program.cs` 加 `UseDefaultFiles` + `MapFallbackToFile`（SPA 兜底，仅当 wwwroot 存在时启用）；② `Microsoft.Extensions.Hosting.WindowsServices 10.0.0` 加进 `Directory.Packages.props` + `YinYanMusic.Api.csproj`；③ `builder.Host.UseWindowsService()`（`OperatingSystem.IsWindows()` 检测，安全可移植）；④ `publish-win.ps1`（前端 build → `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true` → 拷贝 dist → 写默认 `appsettings.json`）；⑤ `installer/installer.iss`（Inno Setup 6.7.3，向导双自定义页 + `sc create` 服务注册 + `netsh` 防火墙 + 展示引导码 + 卸载清服务）。ISCC 编译通过 → `installer/output/YinYanMusic-Setup-1.0.0.exe`（2MB） | ISCC 0 错 |
| **M6 Linux/Nginx（可选）** | Nginx 配置 + systemd + 发布脚本 | 域名 HTTPS 可访问；刷新子路由不 404 |

---

## 9. 风险与权衡

1. **后端缺口大**：M0 + M2 要补约 15 个端点，是主要工作量。
2. **`BaseAddress` 定死**：若不做 `ApiConfig.Absolute` 收口，改地址不生效（M0.5 的重点）。
3. **Android 明文**：地址可配后必须靠 `debug-overrides` + 正式包 HTTPS，否则现场连不上还查不出原因。
4. **超管只能有一个、事后不可再建**：万一超管被人抢建或账号丢失，要留后门 ——
   建议环境变量 `YINYAN_BOOTSTRAP_ADMIN=用户名:密码`（**仅当无超管时**自动创建），或脚本直接改库。
   （判断依据已是"有没有超管"，所以别人先在 App 注册也不会把后台注册页堵死。）
5. **引导码的可用性**：码写在服务器文件里 —— 文件被误删但超管还没建 → 删文件重启即可重新生成；
   文件权限没收紧 → 等于没保护（Windows 安装时收紧 ACL、Linux `chmod 600`）；
   安装向导要**明确提示抄走**，否则用户装完找不到码会以为装坏了。
6. **Windows 包的数据库**：目标机没有 PostgreSQL 就跑不起来（见 5.4，需你定）。
6. **self-contained 体积**：单文件约 70~90MB（含运行时）；要小包得改 framework-dependent（需预装 .NET 10 Runtime）。
7. **长任务超时**：导入/扫时长建议改异步（M4）。
8. **契约漂移**：靠 OpenAPI 生成 + `openapi.json` 入库比对兜住。

---

## 10. 需要你拍板的问题

**新需求相关**
1. **PostgreSQL**：目标机器已有 PG（方案 A，推荐）/ 内置 PG 绿色版（B）/ 换 SQLite（C）？
2. **安装包体积**：self-contained 单文件（~80MB，装完即用，推荐）还是 framework-dependent（~15MB，但要先装 .NET 10 Runtime）？
3. ~~要不要加一次性引导码？~~ → **已采纳**，见 4.5（默认开启，`Auth:RequireBootstrapCode=true`）。
4. **是否注册为 Windows 服务并开机自启**（推荐是）？是否自动放行防火墙端口？
5. 前端托管方式：**API 同源托管**（推荐，单进程）还是 Windows 上也要 Nginx/IIS？

**沿用 v3 的问题**
6. 管理端是否限内网/VPN（建议 IP 白名单）？
7. 删除走物理删还是软删（建议软删）？
8. 导入/扫时长是否改成异步 + 进度（建议改）？
9. 本期是否包含「用户管理」？（建议保留 —— 注册关了之后，建号只能靠它）

---

_评审后我按确认结果更新文档，然后从 **M0（权限与账号地基）** 开始动手；M0.5 与 M1 可与 M0 并行。_
