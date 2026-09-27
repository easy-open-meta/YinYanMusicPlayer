using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
// Windows 服务形态要用到（UseWindowsService / WindowsServiceLifetimeOptions）。
// Linux/Docker 上这个包不会被调用（下面有 OperatingSystem.IsWindows() 守卫），引用它是安全可移植的。
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using YinYanMusic.Api.Auth;
using YinYanMusic.Api.Hubs;
using YinYanMusic.Api.Jobs;
using YinYanMusic.Application;
using YinYanMusic.Data;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// Windows 安装形态（Inno Setup 装的机子走这里）：
//   - 服务模式：由 SCM 以 Windows 服务启动。UseWindowsService 会顺带把 ContentRoot
//     设为 exe 所在目录；只在 Windows 上调，Linux/Docker 上是彻底的 no-op。
//   - 配置覆盖层：%ProgramData%\YinYanMusic\appsettings.json（安装向导按用户填写生成，
//     与程序目录分离，升级覆盖程序目录时不会丢配置）。
//   - 监听端口优先级见下方 ConfigureKestrel。
// ============================================================================
if (OperatingSystem.IsWindows())
{
    // 显式指定服务名：`sc create` / 安装脚本里注册的就是这个名字（默认名会用程序集名，改名易踩空）。
    builder.Host.UseWindowsService(o => o.ServiceName = "YinYanMusic.Api");

    var sharedConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "YinYanMusic", "appsettings.json");
    if (File.Exists(sharedConfigPath))
        builder.Configuration.AddJsonFile(sharedConfigPath, optional: true, reloadOnChange: true);
}

// ============================================================================
// 监听地址：默认 HTTP 0.0.0.0:5116（必须 0.0.0.0，真机/容器才能从外部连进来）。
//   端口优先级：环境变量 YINYAN_HTTP_PORT > 配置 Api:HttpPort > 5116
//     （Docker 用环境变量；Windows 安装包把端口写进 appsettings.json 的 Api:HttpPort）
//   HTTPS 不在进程内自建（2026-09-17 定案）：生产由 Nginx 反代终结 TLS，
//   Windows 形态则由安装包同源托管前端，局域网内走明文 http。
//   注意：一旦调用 ConfigureKestrel 注册端点，ASPNETCORE_URLS 就会被忽略，
//   所以老启动脚本不设 ASPNETCORE_URLS 也能拿到 5116。
// ============================================================================
builder.WebHost.ConfigureKestrel(opts =>
{
    var httpPort = int.TryParse(Environment.GetEnvironmentVariable("YINYAN_HTTP_PORT"), out var envPort) && envPort > 0
        ? envPort
        : int.TryParse(builder.Configuration["Api:HttpPort"], out var cfgPort) && cfgPort > 0
            ? cfgPort
            : 5116;
    opts.ListenAnyIP(httpPort);

    // 开发环境（Vite 代理 / 直连调试）下显式打开 keep-alive 并放宽空闲超时。
    // 背景：代理复用连接时，Kestrel 默认 130s 空闲超时 + 并发下的连接关闭，
    // 与代理侧取用旧连接之间存在竞态，表现为偶发 ECONNRESET（前端"列表偶尔刷不出来"）。
    // 这里把空闲超时拉长，减少服务端主动断连的概率；生产保持默认（由 Nginx 终结）。
    if (builder.Environment.IsDevelopment())
    {
        opts.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
        opts.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(60);
        opts.Limits.MaxConcurrentConnections = null; // 不限制，避免开发期连接被拒
    }
});

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((doc, ctx, ct) =>
    {
        doc.Info = new OpenApiInfo
        {
            Title = "音言音乐 API",
            Version = "v1",
            Description = "音言音乐播放器后端服务（.NET 10 + PostgreSQL）"
        };
        return Task.CompletedTask;
    });
    // Bearer 安全方案：Scalar UI 的 Authorize 里填 token 即可调 [Authorize] 接口（含 admin）
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

builder.Services.AddDbContext<MusicDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
    // V2.12：手写迁移期间快照与模型可能出现细微表述差异（列顺序/注记），EF 10 起
    // PendingModelChangesWarning 默认抛异常阻断启动。这里降级为告警，避免开发期被
    // 误判卡死；迁移本身仍由 Migrate() 正常应用，结构正确性由联调实测保证。
    .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// 账号相关开关（App 是否可注册 / 后台建超管是否要引导码）：绑一次、单例注入，
// 不给 Application 层添 IOptions 依赖。配置节 Auth（CI 里用 Auth__* 环境变量覆盖）。
var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton<IBootstrapCodeService, BootstrapCodeService>();

// SMTP（V2.5 邮箱验证码）：同样是"绑一次、单例注入"的写法，不给 Application 加 IOptions 依赖。
// ⚠️ 未配置是合法状态：IsConfigured 为 false 时邮箱绑定接口返回明确提示，
//    客户端据此隐藏入口 —— 不能让缺 SMTP 阻断 API 启动（部署形态多样）。
var smtpOptions = builder.Configuration.GetSection(SmtpOptions.SectionName).Get<SmtpOptions>() ?? new SmtpOptions();
builder.Services.AddSingleton(smtpOptions);
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// V2.8 定时扫描目录入库：配置绑一次、单例注入（同 AuthOptions / SmtpOptions 的写法）。
// 环境变量 Media__ScanIntervalMinutes / Media__ScanOnStartup / Media__ScanRecursive 可覆盖。
var scanOptions = builder.Configuration.GetSection("Media").Get<ScanOptions>() ?? new ScanOptions();
builder.Services.AddSingleton(scanOptions);
// 运行状态（上次结果 / 下次时间 / 是否启用）：进程内单例，重启归零是刻意设计。
builder.Services.AddSingleton(sp => new ScanState(scanOptions.IsEnabled, scanOptions.ScanIntervalMinutes));
builder.Services.AddScoped<IDirectoryScanService, DirectoryScanService>();
// 执行入口（定时 / 手动共用）
builder.Services.AddSingleton<IScanGate, ScanGate>();
builder.Services.AddSingleton<IScanRunner, ScanRunner>();
// 宿主服务按单例注册，同时暴露具体类型给 AdminController（后台的定时器开关要唤醒它重排）
builder.Services.AddSingleton<DirectoryScanJob>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<DirectoryScanJob>());

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<AudioMetadataService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISongService, SongService>();
builder.Services.AddScoped<IPlaylistService, PlaylistService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IMeService, MeService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IDictService, DictService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IPlaybackService, PlaybackService>();
builder.Services.AddScoped<IRecommendService, RecommendService>();

// V2.15 站内通知：服务层 + SignalR 实时广播（Hub 见 Hubs/NotificationsHub.cs）
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<INotificationBroadcaster, SignalRNotificationBroadcaster>();
builder.Services.AddSignalR();

var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        // V2.5：比对令牌里的 tv 声明与库中 TokenVersion，改密后旧 token 立即失效
        TokenVersionValidator.Attach(o);

        // V2.15 SignalR：WebSocket 握手不会自动带 Authorization 头，标准做法是
        // Hub 连接串上的 ?access_token=<JWT>。只对 /hubs/notifications 生效。
        // ⚠️ TokenVersionValidator.Attach 会整表替换 o.Events —— 必须在它之后再包一层，
        // 把已挂上的 OnTokenValidated 原样保留。
        var validated = o.Events?.OnTokenValidated;
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnTokenValidated = validated ?? (_ => Task.CompletedTask)
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// InitialCreate 的 MigrationId（P0 基线缝合用）。后续新增迁移不影响此处；若重建基线需同步更新。
const string P0BaselineMigrationId = "20260920155441_InitialCreate";

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
    var logger = app.Logger;

    // DROP_DB=1：本地/CI 快速重置（先删库，随后按"全新库"流程 Migrate() 建表）。生产环境严禁设置。
    // 库不存在时 Npgsql 的 EnsureDeleted 会抛 3D000，所以先 CanConnect 再删（不存在则直接走全新库建表）。
    var canConnect = await db.Database.CanConnectAsync();
    if (Environment.GetEnvironmentVariable("DROP_DB") == "1" && canConnect)
    {
        await db.Database.EnsureDeletedAsync();
        canConnect = false; // 删完即全新库
    }

    // ============================================================================
    // 数据库结构变更（P0 起）：一律走 EF Core 迁移，启动时 Migrate() 应用。
    // 存量库都是 EnsureCreated 建的，没有 __EFMigrationsHistory，直接 Migrate 会因
    // "表已存在"而失败 —— 所以启动时做三态识别：
    //   ① 全新库（库不存在 / Songs 不存在）   → 直接 Migrate() 建库建表；
    //   ② 老库未基线（Songs 存在、历史表没有） → 先跑幂等 SQL 对齐结构 → 建历史表并
    //      插入 InitialCreate 记录（"基线缝合"）→ 再 Migrate()（此时应无待应用迁移）；
    //   ③ 已基线（历史表存在）                → 直接 Migrate()。
    // ⚠️ 过渡完成（确认所有环境的历史表都已存在）后：删除"②基线缝合"分支与全部手写 DDL。
    //    按文档约定放在 P0 上线后的下一个版本再删，留一个观察窗口，不要和首次上线同版删除。
    // ============================================================================
    // 全新库（库不存在）连不上，跳过表级识别，交给 Migrate() 建库建表；能连上才做缝合判断。
    if (canConnect)
    {
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        try
        {
            var songsTableExists = await TableExistsAsync(conn, "Songs");
            var historyTableExists = await TableExistsAsync(conn, "__EFMigrationsHistory");

            if (songsTableExists && !historyTableExists)
            {
                logger.LogInformation("检测到 EnsureCreated 老库（无 __EFMigrationsHistory），开始一次性基线缝合…");

                // ---- ① 幂等 DDL：把老库结构对齐到 InitialCreate 的模型（逐段迁移自原启动流程，语义不变）----
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Songs\" ADD COLUMN IF NOT EXISTS \"CoverUrl\" text NULL");

                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE IF NOT EXISTS "ArtistFollows" (
                        "UserId" bigint NOT NULL,
                        "ArtistId" bigint NOT NULL,
                        "CreatedAt" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_ArtistFollows" PRIMARY KEY ("UserId", "ArtistId"),
                        CONSTRAINT "FK_ArtistFollows_Artists_ArtistId" FOREIGN KEY ("ArtistId") REFERENCES "Artists" ("Id") ON DELETE CASCADE,
                        CONSTRAINT "FK_ArtistFollows_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
                    );
                    """);

                // 歌单系统标志列：存量数据按系统歌单固定名称回填一次（仅服务端数据迁移用，客户端逻辑以 IsSystem 为准）。
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Playlists\" ADD COLUMN IF NOT EXISTS \"IsSystem\" boolean NOT NULL DEFAULT false");
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE \"Playlists\" SET \"IsSystem\" = true WHERE \"Name\" = '我喜欢的音乐' AND \"IsSystem\" = false");

                // 账号体系：角色 + 停用。默认 'user'：存量账号一律普通用户，超管只能由后台注册页产出（见 AuthService）。
                // 提管理员：UPDATE "Users" SET "Role"='admin' WHERE "UserName"='xxx';（改完必须重新登录换 token）
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"Role\" text NOT NULL DEFAULT 'user'");
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"Users\" ADD COLUMN IF NOT EXISTS \"IsDisabled\" boolean NOT NULL DEFAULT false");

                // 分区（音乐专区）：Categories 补 3 个展示字段（宣传语/卡片底色/图标字符）。
                // IconGlyph 存 MaterialIcons 的实际 Unicode 字符（chr(码点十进制)），客户端 Text 可直接绑定。
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Categories\" ADD COLUMN IF NOT EXISTS \"Slogan\" text NULL");
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Categories\" ADD COLUMN IF NOT EXISTS \"ColorHex\" text NULL");
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Categories\" ADD COLUMN IF NOT EXISTS \"IconGlyph\" text NULL");

                // ---- ② 建迁移历史表并写入 InitialCreate 基线记录（与生成迁移的 MigrationId 严格一致）----
                // 表结构与 Npgsql HistoryRepository 建的一致；ProductVersion 仅作记录，不参与待应用判定。
                await db.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                        "MigrationId" character varying(150) NOT NULL,
                        "ProductVersion" character varying(32) NOT NULL,
                        CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                    );
                    """);
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1}) ON CONFLICT (\"MigrationId\") DO NOTHING",
                    P0BaselineMigrationId, "10.0.4");

                logger.LogInformation("基线缝合完成：已写入 {MigrationId} 历史记录，老库结构与 InitialCreate 对齐。",
                    P0BaselineMigrationId);
            }
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    // ---- ③ 应用迁移：全新库建库建全表；已缝合/已基线的库幂等跳过（重复启动无副作用）----
    await db.Database.MigrateAsync();

    // 超管引导码：还没有超管 → 确保码存在；已经有超管 → 清掉残留文件
    var hasAdmin = await db.Users.AnyAsync(u => u.Role == "admin");
    scope.ServiceProvider.GetRequiredService<IBootstrapCodeService>().Ensure(hasAdmin);

    // 分区种子：5 条分类，ON CONFLICT DO NOTHING —— 人工改名不会被覆盖。
    // 刻意不进迁移的 HasData（线上分类可能已被改名，HasData 的覆盖/删除语义风险大于收益）。
    await db.Database.ExecuteSqlRawAsync(
        """
        INSERT INTO "Categories" ("Name", "Slogan", "ColorHex", "IconGlyph")
        VALUES
            ('华语专区',   '华语流行 · 音乐坐标',   '#E53935', chr(58373)),
            ('粤语专区',   '探索大湾区优质好音乐', '#EC407A', chr(57405)),
            ('K-Pop专区', '热歌唤醒你的 DNA',      '#8E24AA', chr(59517)),
            ('欧美专区',   '环球热单一站收听',      '#1E88E5', chr(59403)),
            ('日语专区',   '捕捉最新日系风潮',      '#00897B', chr(59618))
        ON CONFLICT ("Name") DO NOTHING;
        """);
}

// 用 information_schema 判断表是否存在（三态识别）。参数化传表名，避免拼接。
static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection conn, string tableName)
{
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = $1)";
    var p = cmd.CreateParameter();
    p.Value = tableName;
    cmd.Parameters.Add(p);
    return await cmd.ExecuteScalarAsync() is bool exists && exists;
}

// .flac / .m4a 不在 ASP.NET Core 默认的 MIME 映射表里，静态文件中间件对未知扩展名默认直接返回 404
// （且不打日志）。必须显式注册，否则 /media/audio 下的无损/封装音频永远取不到，前端表现为"点了没反应"。
var audioContentTypes = new FileExtensionContentTypeProvider();
audioContentTypes.Mappings[".flac"] = "audio/flac";
audioContentTypes.Mappings[".m4a"] = "audio/mp4";
audioContentTypes.Mappings[".wav"] = "audio/wav";
audioContentTypes.Mappings[".ogg"] = "audio/ogg";
audioContentTypes.Mappings[".aac"] = "audio/aac";

app.UseDefaultFiles();  // 让 / 自动落到 wwwroot/index.html（API 同源托管前端）
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = audioContentTypes });

var musicDir = builder.Configuration["Media:MusicDirectory"];
if (!string.IsNullOrWhiteSpace(musicDir) && Directory.Exists(musicDir))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(musicDir),
        RequestPath = "/media/audio",
        ContentTypeProvider = audioContentTypes
    });
}
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// V2.15 通知实时通道：App 连 /hubs/notifications?access_token=<JWT>
app.MapHub<NotificationsHub>("/hubs/notifications");
// SPA 兜底：必须放在 MapControllers 之后，否则会把 /api/* 也回退到 index.html。
// 条件：仅当 wwwroot 存在（生产部署：后台前端由 API 同源托管）才启用；
// 开发环境（dotnet run）wwwroot 不存在，跳过兜底，避免遮蔽 OpenAPI / 真实 404。
// ⚠️ 生产若改由 Nginx 托管前端 wwwroot 是空的，这段自然不生效，不影响那条部署形态。
var webRoot = app.Environment.WebRootPath;
if (!string.IsNullOrEmpty(webRoot) && Directory.Exists(webRoot))
    app.MapFallbackToFile("index.html", new StaticFileOptions { ContentTypeProvider = audioContentTypes });
// ============================================================================
// API 文档（P1）：MapOpenApi（出 JSON，/openapi/v1.json）与 MapScalarApiReference
// （出可视化 UI，/scalar/v1）必须成对出现，只调后者会 404。
// 由配置开关 OpenApi:Enabled 控制（默认 Development 开、Production 关），
// 生产需要时设环境变量 OpenApi__Enabled=true 按需打开，并由 Nginx 加 IP 白名单 /
// Basic Auth 限制，不要裸奔到公网（CORS 全开 + 0.0.0.0 绑定）。
// /openapi/v1.json 路径保持不变：web/admin 的 npm run gen:api 抓的就是它。
// ============================================================================
if (app.Configuration.GetValue("OpenApi:Enabled", app.Environment.IsDevelopment()))
{
    app.MapOpenApi();
    app.MapScalarApiReference(o =>
    {
        o.Title = "音言音乐 API";
        o.Theme = ScalarTheme.DeepSpace; // 深色主题
        // 2.12 起 DefaultHttpClient 是 (ScalarTarget, ScalarClient) 元组，默认客户端选 C# HttpClient
        o.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
    // 根路径默认跳到 API 文档页。注意：上面的 UseDefaultFiles/UseStaticFiles 在端点路由
    // 之前执行，wwwroot 存在（生产同源托管后台前端）时 index.html 优先，这里不会生效，
    // 所以不会抢掉 SPA 的入口；只有没有前端的形态（开发环境 / Nginx 托管形态）才跳转。
    app.MapGet("/", () => Results.Redirect("/scalar/v1"));
}

app.Run();
