using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using YinYanMusic.Api.Auth;
using YinYanMusic.Application;
using YinYanMusic.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((doc, ctx, ct) =>
{
    doc.Info = new OpenApiInfo
    {
        Title = "音言音乐 API",
        Version = "v1",
        Description = "音言音乐播放器后端服务（.NET 10 + PostgreSQL）"
    };
    return Task.CompletedTask;
}));

builder.Services.AddDbContext<MusicDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

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
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MusicDbContext>();
    if (Environment.GetEnvironmentVariable("DROP_DB") == "1")
        await db.Database.EnsureDeletedAsync();
    if (db.Database.GetMigrations().Any())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Songs\" ADD COLUMN IF NOT EXISTS \"CoverUrl\" text NULL");
    // 关注歌手表：项目尚未使用 EF 迁移（EnsureCreated 只在库不存在时才建表），新表按同样方式幂等补建。
    // 如果以后引入 EF 迁移，这段可以删掉。
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
    // 歌单系统标志列：项目尚未使用 EF 迁移（EnsureCreated 只在库不存在时才建表），老库幂等补列。
    // 存量数据按系统歌单的固定名称回填一次（仅服务端数据迁移用，客户端逻辑一律以 IsSystem 标志为准）。
    await db.Database.ExecuteSqlRawAsync(
        "ALTER TABLE \"Playlists\" ADD COLUMN IF NOT EXISTS \"IsSystem\" boolean NOT NULL DEFAULT false");
    await db.Database.ExecuteSqlRawAsync(
        "UPDATE \"Playlists\" SET \"IsSystem\" = true WHERE \"Name\" = '我喜欢的音乐' AND \"IsSystem\" = false");

    // 分区（音乐专区）：Categories 补 3 个展示字段（宣传语/卡片底色/图标字符），老库幂等补列。
    // IconGlyph 存 MaterialIcons 的实际 Unicode 字符（chr(码点十进制)），客户端 Text 可直接绑定。
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Categories\" ADD COLUMN IF NOT EXISTS \"Slogan\" text NULL");
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Categories\" ADD COLUMN IF NOT EXISTS \"ColorHex\" text NULL");
    await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"Categories\" ADD COLUMN IF NOT EXISTS \"IconGlyph\" text NULL");
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

// .flac / .m4a 不在 ASP.NET Core 默认的 MIME 映射表里，静态文件中间件对未知扩展名默认直接返回 404
// （且不打日志）。必须显式注册，否则 /media/audio 下的无损/封装音频永远取不到，前端表现为"点了没反应"。
var audioContentTypes = new FileExtensionContentTypeProvider();
audioContentTypes.Mappings[".flac"] = "audio/flac";
audioContentTypes.Mappings[".m4a"] = "audio/mp4";
audioContentTypes.Mappings[".wav"] = "audio/wav";
audioContentTypes.Mappings[".ogg"] = "audio/ogg";
audioContentTypes.Mappings[".aac"] = "audio/aac";

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
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.Run();
