# 音言音乐播放器（YinYan Music Player）

一款现代化跨平台音乐播放器，支持 Windows 和 Android 双端，后端基于 ASP.NET Core Web API + PostgreSQL。

> 前身由 Enrico 捐献，原项目为 .NET Framework 4.6.1 WinForms 实现，现已升级为 .NET 10 MAUI 跨平台架构。

## 技术栈

| 层 | 技术 |
|---|---|
| 客户端 | .NET MAUI (net10.0-android, net10.0-windows10.0.19041.0) |
| 服务端 | ASP.NET Core Web API (.NET 10) |
| 数据库 | PostgreSQL (EF Core 10) |
| 认证 | JWT Bearer Token |
| MVVM | CommunityToolkit.Mvvm |
| 媒体播放 | CommunityToolkit.Maui.MediaElement（Android 端接入 ExoPlayer / 系统媒体会话通知栏） |
| 音频元数据 | ATL (z440.atl.core，逐帧解析时长) + TagLibSharp（标签/封面） |
| 包管理 | 中央包管理 (CPM, Directory.Packages.props) |

## 项目结构

```
YinYanMusic.slnx
├── src/
│   ├── YinYanMusic.Core/          # 共享类库（实体 + DTO + 映射扩展）
│   ├── YinYanMusic.Data/          # EF Core DbContext
│   ├── YinYanMusic.Application/   # 业务服务（歌曲/歌单/目录/账号/元数据/时长探测）
│   ├── YinYanMusic.Api/           # ASP.NET Core Web API
│   ├── YinYanMusic.App/           # MAUI 客户端（Windows + Android）
│   └── YinYanMusic.Scanner/       # 本地音乐导入 CLI 工具
├── tests/
│   └── YinYanMusic.Tests/         # xUnit 单元测试
├── docs/                          # 设计文档（如：分区功能方案）
├── Directory.Build.props          # 统一编译属性
└── Directory.Packages.props       # 中央包版本管理
```

## 构建与运行

### 前置条件

- .NET 10 SDK (10.0.300+)
- MAUI 工作负载：`dotnet workload install maui`
- PostgreSQL 16+
- （Android 构建）Android SDK

### 构建服务端

```bash
dotnet build src/YinYanMusic.Api/YinYanMusic.Api.csproj
```

### 运行服务端

```bash
dotnet run --project src/YinYanMusic.Api/YinYanMusic.Api.csproj
```

API 默认监听 `http://localhost:5000`，Swagger 文档位于 `/swagger`。

### 构建客户端（Windows）

```bash
dotnet build src/YinYanMusic.App/YinYanMusic.App.csproj -f net10.0-windows10.0.19041.0
```

### 构建客户端（Android）

```bash
dotnet build src/YinYanMusic.App/YinYanMusic.App.csproj -f net10.0-android
```

### 本地音乐导入（Scanner CLI）

```bash
dotnet run --project src/YinYanMusic.Scanner/YinYanMusic.Scanner.csproj -- --path "E:\CloudMusic" [--dry-run] [--force]
```

- 递归扫描 MP3 / FLAC / M4A / WAV / OGG，TagLib 读取标签与内嵌封面（自动落盘 `song-{id}.jpg`），同名 `.lrc` 自动关联歌词
- 时长由 ATL 从音频数据逐帧解析（向下取整）；解析失败的文件拒绝导入，损坏音频不会进入曲库

### 运行测试

```bash
dotnet test
```

## 配置

### 服务端 (`appsettings.json`，开发配置放 `appsettings.Development.json`)

- `ConnectionStrings:Default` — PostgreSQL 连接字符串
- `Jwt:SecretKey` — JWT 签名密钥（≥32 字符）、`Jwt:Issuer` / `Jwt:Audience` / `Jwt:ExpiryMinutes`
- `Media:MusicDirectory` — 本地音乐目录（Scanner 与 Api 的静态音频源）
- `Media:ImageDirectory` / `Media:BaseUrl` — 封面落盘目录与外链前缀（可选）

> 数据库结构变更不走 EF 迁移：新库由 `EnsureCreated` 建表，老库由 `Program.cs` 启动时的幂等 raw SQL 补齐（补列/补表/种子数据），改表结构记得同步维护。

### 客户端 (`Services/ApiConfig.cs`)

- `BaseUrl` — API 服务地址（默认 `http://localhost:5000`）

## 功能

### 账号与个人主页
- 注册 / 登录（JWT），「我的」页个人信息卡（头像 / 昵称 / 性别）
- 粉丝 / 关注 / 歌单三项统计**可点击**，分别进入粉丝列表、我的歌单列表、关注列表（关注对象含用户与歌手）
- 用户主页：搜索用户后可进入，查看资料与公开歌单，**关注 / 取关**该用户

### 发现页与音乐分区
- 热门歌曲、精选歌单横滑区（自动排除系统歌单）
- **音乐分区**：由 `Categories` 表完全数据驱动（华语专区、粤语专区、K-Pop 专区……），发现页横滑卡片 + 全部专区网格页 + 分区详情页；新增分区只需插入一行数据
- 每首歌/歌单可挂载分区（`CategoryId`），歌曲与歌单搜索接口均支持按分区过滤

### 搜索
- 关键词模糊匹配**歌曲**（歌名 / 歌手名 / 专辑名）、**专辑**、**用户**，三路并行、结果分区展示
- 专辑结果点击即播放整张；用户结果点击进入用户主页
- 搜索历史（本地持久化，支持单条删除 / 一键清空）

### 歌单
- 创建（歌单名系统保留校验）/ 收藏 / 取消收藏 / 删除
- 歌单内歌曲支持「下一首播放 / 收藏进其他歌单 / 喜欢 / 查看歌手 / 关注歌手」等更多菜单

### 播放
- 播放 / 暂停 / 上一首 / 下一首 / 进度拖拽 / 音量调节，迷你播放条常驻
- 四种播放模式：顺序播放 / 列表循环 / 单曲循环 / 随机播放，**本地持久化**（重启保留）
- 后台持续播放；Android 通知栏媒体控制面板（播放/暂停/上下曲/进度，与播放页同源时长）
- 播放页：唱片旋转动画、封面长按预览与保存、色彩亚克力背景、音质标签（FLAC / MP3 / M4A …随文件扩展名自动显示）
- 歌曲时长全链路统一：ATL 逐帧解析 + 向下取整（导入拒收解析失败文件 → 歌单不渲染 → 播放自动跳过）
- 歌词：同名 `.lrc` 自动关联

### 喜欢与关注
- 喜欢歌曲：播放页爱心 / 菜单项，「我喜欢的音乐」个人歌单置顶展示
- 关注歌手：歌曲菜单 / 播放页快捷关注，关注列表分区展示
- 关注用户：用户主页一键关注，关注数与关注列表实时同步

### 主题
- 五套主题色（紫 / 红 / 橙 / 蓝 / 纯白），设置抽屉一键切换，实时生效并本地持久化
- 全局 DynamicResource 换肤：按钮 / 滑块 / 标签 / 抽屉 / 底部导航等全量跟随；播放页深色亚克力背景保持不变，进度条 / 播放按钮 / 音质标签等强调元素随主题

## 参与贡献

1. Fork 本仓库
2. 新建 Feat_xxx 分支
3. 提交代码
4. 新建 Pull Request
