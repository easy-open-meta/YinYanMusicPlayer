using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Tests.Application;

/// <summary>
/// 离线播放补报服务层（V2.11）的核心语义：幂等去重、计数只对 accepted 生效、unknown 歌上报、
/// 时长与落库归一。用 SQLite in-memory 跑真实 EF 管线，不 mock DbContext。
/// </summary>
public class PlaybackServiceAcceptReportsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MusicDbContext _db;
    private readonly PlaybackService _service;

    public PlaybackServiceAcceptReportsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new MusicDbContext(options);
        _db.Database.EnsureCreated();

        // 种子：两个用户 + 歌手 + 两首歌（song 7 播放数 10，song 8 播放数 0）
        _db.Users.Add(new User { UserName = "a", DisplayName = "A", PasswordHash = "x", Role = "user" });
        _db.Users.Add(new User { UserName = "b", DisplayName = "B", PasswordHash = "x", Role = "user" });
        _db.SaveChanges();

        var artist = new Artist { Name = "测试歌手" };
        _db.Artists.Add(artist);
        _db.SaveChanges();

        _db.Songs.Add(new Song { Id = 7, Title = "歌七", ArtistId = artist.Id, AudioUrl = "a.mp3", PlayCount = 10 });
        _db.Songs.Add(new Song { Id = 8, Title = "歌八", ArtistId = artist.Id, AudioUrl = "b.mp3", PlayCount = 0 });
        _db.SaveChanges();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Media:ImageDirectory"] = Path.Combine(Path.GetTempPath(), "yinyan-test-images"),
        }).Build();
        var env = HostingEnvironment();
        _service = new PlaybackService(_db, new AudioMetadataService(_db, config, env));
    }

    private static IWebHostEnvironment HostingEnvironment() => new FakeEnv();

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string ContentRootFileProviderPath => ContentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task Accept_NewReports_CountsPlayAndStoresRows()
    {
        var playedAt = DateTime.UtcNow.AddHours(-1);
        var ack = await _service.AcceptReportsAsync(1, new PlayReportsBatchRequest(
        [
            new PlayReportRequest(7, "k1", playedAt, 30),
            new PlayReportRequest(8, "k2", playedAt, 0),
        ]));

        Assert.Equal(2, ack.Accepted);
        Assert.Equal(0, ack.Duplicated);
        Assert.Empty(ack.UnknownSongs);

        var song7 = await _db.Songs.AsNoTracking().SingleAsync(s => s.Id == 7);
        var song8 = await _db.Songs.AsNoTracking().SingleAsync(s => s.Id == 8);
        Assert.Equal(11, song7.PlayCount);   // 10 + 1（TC-2.11-01）
        Assert.Equal(1, song8.PlayCount);

        var rows = await _db.PlayReports.AsNoTracking().ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(1, r.UserId));   // 归属当前账号
    }

    [Fact]
    public async Task Accept_DuplicateKeys_NotCountedTwice()
    {
        var items = new List<PlayReportRequest> { new(7, "dup-1", DateTime.UtcNow.AddHours(-2), 0) };
        await _service.AcceptReportsAsync(1, new PlayReportsBatchRequest(items));
        await _service.AcceptReportsAsync(1, new PlayReportsBatchRequest(items));   // 同一批重交

        var song = await _db.Songs.AsNoTracking().SingleAsync(s => s.Id == 7);
        Assert.Equal(11, song.PlayCount);   // 仍然 10+1，不重复计数（TC-2.11-02）

        var ack2 = await _service.AcceptReportsAsync(1, new PlayReportsBatchRequest(items));
        Assert.Equal(0, ack2.Accepted);
        Assert.Equal(1, ack2.Duplicated);
    }

    [Fact]
    public async Task Accept_UnknownSongs_ReportedAndNotCounted()
    {
        var ack = await _service.AcceptReportsAsync(1, new PlayReportsBatchRequest(
        [
            new PlayReportRequest(999, "ghost-1", DateTime.UtcNow, 0),
        ]));

        Assert.Equal(1, ack.UnknownSongs.Count);   // 歌已删 → unknownSongs（TC-2.11-03）
        Assert.Equal(999, ack.UnknownSongs.Single());
        Assert.Equal(0, ack.Accepted);
        Assert.False(await _db.PlayReports.AnyAsync());   // 不落库
    }

    [Fact]
    public async Task Accept_ReportsBelongToSubmittingUser()
    {
        var items = new List<PlayReportRequest> { new(7, "k-a", DateTime.UtcNow, 0) };
        await _service.AcceptReportsAsync(1, new PlayReportsBatchRequest(items));
        await _service.AcceptReportsAsync(2, new PlayReportsBatchRequest(
        [
            new PlayReportRequest(7, "k-b", DateTime.UtcNow, 0),   // 另一个账号补自己的
        ]));

        var rowA = await _db.PlayReports.AsNoTracking().SingleAsync(r => r.ClientReportKey == "k-a");
        var rowB = await _db.PlayReports.AsNoTracking().SingleAsync(r => r.ClientReportKey == "k-b");
        Assert.Equal(1, rowA.UserId);
        Assert.Equal(2, rowB.UserId);   // 各归各账号（TC-2.11-11）
    }

    [Fact]
    public async Task Accept_PlayedAtUtcNormalizedToUtc()
    {
        var local = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Local);
        await _service.AcceptReportsAsync(1, new PlayReportsBatchRequest(
        [
            new PlayReportRequest(7, "tz-1", local, 0),
        ]));

        var row = await _db.PlayReports.AsNoTracking().SingleAsync(r => r.ClientReportKey == "tz-1");
        // SQLite 存取不保留 DateTimeKind：校验归一化语义（UTC 瞬间），而不是 Kind 枚举
        Assert.Equal(local.ToUniversalTime(), row.PlayedAtUtc);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
