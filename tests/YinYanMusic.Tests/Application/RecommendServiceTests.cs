using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Tests.Application;

/// <summary>
/// 推荐歌单专区（V2.12）的规则排序与人工干预语义。用 SQLite in-memory 跑真实 EF 管线，
/// 不 mock DbContext —— 这一版的风险全在"排序表达式能不能被正确翻译成 SQL"上，mock 掉就白测了。
///
/// <para>种子数据的形状（三条歌单的三种排序各不相同的顺序，避免用例之间"碰巧都通过"）：
/// <code>
/// 歌曲：1(华语,歌手甲,播放100) 2(华语,歌手乙,播放50) 3(欧美,歌手甲,播放10) 4(欧美,歌手乙,播放1)
/// 歌单：热门=[1,2] 播放150 收藏0  |  收藏多=[3] 播放10 收藏2  |  最新=[4] 播放1 收藏1
///       （创建时间 热门 &lt; 收藏多 &lt; 最新）
/// 于是：热播=[热门,收藏多,最新] 高分收藏=[收藏多,最新,热门] 最新上架=[最新,收藏多,热门]
/// </code></para>
/// </summary>
public class RecommendServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MusicDbContext _db;
    private readonly RecommendService _service;

    private long _playlistHot;
    private long _playlistCollected;
    private long _playlistNewest;
    private long _userWithTaste;

    private static readonly DateTime Day1 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day2 = new(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Day3 = new(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    public RecommendServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MusicDbContext>().UseSqlite(_connection).Options;
        _db = new MusicDbContext(options);
        _db.Database.EnsureCreated();

        Seed();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Media:ImageDirectory"] = Path.Combine(Path.GetTempPath(), "yinyan-test-images"),
        }).Build();
        _service = new RecommendService(_db, new AudioMetadataService(_db, config, new FakeEnv()));
    }

    private void Seed()
    {
        _db.Users.Add(new User { UserName = "owner", DisplayName = "歌单主", PasswordHash = "x", Role = "user" });
        _db.Users.Add(new User { UserName = "fan1", DisplayName = "收藏甲", PasswordHash = "x", Role = "user" });
        _db.Users.Add(new User { UserName = "fan2", DisplayName = "收藏乙", PasswordHash = "x", Role = "user" });
        _db.SaveChanges();

        // userWithTaste 用来测「猜你喜欢」：它喜欢的是欧美区（分类 2）的 song3
        var owner = _db.Users.First(u => u.UserName == "owner");
        var fan1 = _db.Users.First(u => u.UserName == "fan1");
        var fan2 = _db.Users.First(u => u.UserName == "fan2");
        _userWithTaste = fan1.Id;

        var chinese = new Category { Name = "华语" };
        var western = new Category { Name = "欧美" };
        _db.Categories.AddRange(chinese, western);
        _db.SaveChanges();

        var artistA = new Artist { Name = "歌手甲" };
        var artistB = new Artist { Name = "歌手乙" };
        _db.Artists.AddRange(artistA, artistB);
        _db.SaveChanges();

        var songs = new[]
        {
            new Song { Title = "华语甲", ArtistId = artistA.Id, CategoryId = chinese.Id, AudioUrl = "1.mp3", PlayCount = 100 },
            new Song { Title = "华语乙", ArtistId = artistB.Id, CategoryId = chinese.Id, AudioUrl = "2.mp3", PlayCount = 50 },
            new Song { Title = "欧美甲", ArtistId = artistA.Id, CategoryId = western.Id, AudioUrl = "3.mp3", PlayCount = 10 },
            new Song { Title = "欧美乙", ArtistId = artistB.Id, CategoryId = western.Id, AudioUrl = "4.mp3", PlayCount = 1 },
        };
        _db.Songs.AddRange(songs);
        _db.SaveChanges();

        // 歌单都显式给封面：免得 FillPlaylistCoversAsync 去探测音频文件（测试里没有真实文件，纯属白跑）
        // 分区刻意错开：『热门』是华语区（不然 for-you 会给它"整张歌单都在口味区"的额外加分，
        // 掩盖掉"播放量第一但在 for-you 里垫底"这个要测的对比）
        var hot = MakePlaylist(owner.Id, "热门", chinese.Id, Day1, [songs[0], songs[1]]);
        var collected = MakePlaylist(owner.Id, "收藏多", western.Id, Day2, [songs[2]]);
        var newest = MakePlaylist(owner.Id, "最新", chinese.Id, Day3, [songs[3]]);
        _db.Playlists.AddRange(hot, collected, newest);
        _db.SaveChanges();

        _playlistHot = hot.Id;
        _playlistCollected = collected.Id;
        _playlistNewest = newest.Id;

        // 收藏数：热门 0、收藏多 2、最新 1 —— 刻意与播放量排序相反
        _db.PlaylistCollections.AddRange(
            new PlaylistCollection { UserId = fan1.Id, PlaylistId = collected.Id },
            new PlaylistCollection { UserId = fan2.Id, PlaylistId = collected.Id },
            new PlaylistCollection { UserId = fan2.Id, PlaylistId = newest.Id });
        _db.SaveChanges();

        // 口味信号：喜欢欧美区的 song3（分类 = 欧美，歌手 = 歌手甲）
        _db.LikedSongs.Add(new LikedSong { UserId = fan1.Id, SongId = songs[2].Id });
        _db.SaveChanges();
    }

    private static Playlist MakePlaylist(long ownerId, string name, int categoryId, DateTime createdAt, Song[] songs)
    {
        var playlist = new Playlist
        {
            OwnerId = ownerId,
            Name = name,
            CategoryId = categoryId,
            CreatedAt = createdAt,
            CoverUrl = $"/media/image/{name}.jpg",
        };
        for (var i = 0; i < songs.Length; i++)
            playlist.Songs.Add(new PlaylistSong { SongId = songs[i].Id, Position = i, AddedAt = createdAt });
        return playlist;
    }

    private async Task<List<string>> OrderAsync(string? scene, int? categoryId = null, long? userId = null)
    {
        var result = await _service.GetPlaylistsAsync(scene, categoryId, userId, 1, 20);
        return result.Items.Select(x => x.Name).ToList();
    }

    // ── TC-2.12-01 热播排序 ────────────────────────────────────────────────
    [Fact]
    public async Task Hot_OrdersByPlaylistPlayCountDescending()
    {
        Assert.Equal(["热门", "收藏多", "最新"], await OrderAsync(RecommendScenes.Hot));
    }

    // ── TC-2.12-02 高分收藏排序 ────────────────────────────────────────────
    [Fact]
    public async Task Collected_OrdersByCollectorCountDescending()
    {
        Assert.Equal(["收藏多", "最新", "热门"], await OrderAsync(RecommendScenes.Collected));
    }

    // ── TC-2.12-03 最新上架 ────────────────────────────────────────────────
    [Fact]
    public async Task New_PutsLatestPlaylistFirst()
    {
        Assert.Equal(["最新", "收藏多", "热门"], await OrderAsync(RecommendScenes.New));
    }

    // ── TC-2.12-04 猜你喜欢（已登录）偏向用户喜欢的分类 ─────────────────────
    [Fact]
    public async Task ForYou_BiasesTowardLikedCategoryAndArtist()
    {
        // 口味信号 = 欧美区 + 歌手甲。真正的"热播第一名"是『热门』（播放 150），
        // 但它在 for-you 里应该被压到最后：它两首歌都属于用户不感兴趣的华语区、歌手也不匹配。
        Assert.Equal(["收藏多", "最新", "热门"], await OrderAsync(RecommendScenes.ForYou, userId: _userWithTaste));
    }

    // ── TC-2.12-05 猜你喜欢（未登录）退化为热播 ────────────────────────────
    [Fact]
    public async Task ForYou_Anonymous_FallsBackToHot()
    {
        Assert.Equal(await OrderAsync(RecommendScenes.Hot), await OrderAsync(RecommendScenes.ForYou));
    }

    // ── TC-2.12-05 补充：登录了但没有任何口味信号，同样退化为热播（冷启动） ─────
    [Fact]
    public async Task ForYou_LoggedInWithoutAnySignal_FallsBackToHot()
    {
        // 造一个"没喜欢过歌、也没收藏过歌单"的用户：这种人个性化无从谈起，给通用热播榜才对。
        // 注意不能用 fan2 —— 它收藏过歌单，收藏歌单的分区本身就算口味信号。
        _db.Users.Add(new User { UserName = "blank", DisplayName = "空白用户", PasswordHash = "x", Role = "user" });
        await _db.SaveChangesAsync();
        var blank = await _db.Users.SingleAsync(u => u.UserName == "blank");

        Assert.Equal(await OrderAsync(RecommendScenes.Hot), await OrderAsync(RecommendScenes.ForYou, userId: blank.Id));
    }

    // ── TC-2.12-07 人工置顶优先于规则排序 ──────────────────────────────────
    [Fact]
    public async Task Pinned_BeatsRuleScore()
    {
        // 『最新』播放量垫底，正常排第三；置顶后必须排第一
        var set = await _service.AdminSetAsync(_playlistNewest, new UpdateRecommendedRequest(SortOrder: 1));
        Assert.True(set.Success);

        Assert.Equal(["最新", "热门", "收藏多"], await OrderAsync(RecommendScenes.Hot));
    }

    // ── 人工加权：只挪名次，不改规则本身 ────────────────────────────────────
    [Fact]
    public async Task Weighted_ShiftsRankingWithoutPinning()
    {
        // 『最新』规则分 1（播放量），加 50 分后 = 51：反超『收藏多』（10），但仍排在『热门』（150）之后。
        // 这就是"加权"和"置顶"的区别 —— 置顶是不管分值多少都排第一，加权是有限度地挪名次。
        var set = await _service.AdminSetAsync(_playlistNewest, new UpdateRecommendedRequest(Weight: 50));
        Assert.True(set.Success);

        Assert.Equal(["热门", "最新", "收藏多"], await OrderAsync(RecommendScenes.Hot));
    }

    // ── 下线：从**全部**场景里消失，但不影响歌单本身 ────────────────────────
    [Fact]
    public async Task Hidden_DisappearsFromEveryScene()
    {
        var set = await _service.AdminSetAsync(_playlistHot, new UpdateRecommendedRequest(IsHidden: true));
        Assert.True(set.Success);

        foreach (var scene in new[] { RecommendScenes.Hot, RecommendScenes.Collected, RecommendScenes.New })
        {
            var names = await OrderAsync(scene);
            Assert.DoesNotContain("热门", names);
        }

        // 歌单本身还在（下线只影响推荐位）
        Assert.True(await _db.Playlists.AnyAsync(p => p.Id == _playlistHot));
    }

    // ── TC-2.12-08 去重：命中多个信号只出现一次 ────────────────────────────
    [Fact]
    public async Task ForYou_PlaylistMatchingManySignals_AppearsOnce()
    {
        var result = await _service.GetPlaylistsAsync(RecommendScenes.ForYou, null, _userWithTaste, 1, 20);

        // 『收藏多』[song3] 同时命中分类与歌手两条信号，『热门』[song1,song2] 一首都不命中
        Assert.Equal(result.Items.Count, result.Items.Select(x => x.Id).Distinct().Count());
        Assert.Single(result.Items.Where(x => x.Name == "收藏多"));
        Assert.Equal(3, result.Total);
    }

    // ── TC-2.12-09 空数据：返回空列表，不报错 ──────────────────────────────
    [Fact]
    public async Task EmptyCatalog_ReturnsEmptyPage()
    {
        _db.PlaylistSongs.RemoveRange(_db.PlaylistSongs);
        _db.Playlists.RemoveRange(_db.Playlists);
        await _db.SaveChangesAsync();

        foreach (var scene in new[] { RecommendScenes.Hot, RecommendScenes.Collected, RecommendScenes.New, RecommendScenes.Zone })
        {
            var result = await _service.GetPlaylistsAsync(scene, categoryId: 1, userId: _userWithTaste, page: 1, pageSize: 20);
            Assert.Empty(result.Items);
            Assert.Equal(0, result.Total);
        }
    }

    // ── TC-2.12-11 已删歌单不再出现，且不报错 ──────────────────────────────
    [Fact]
    public async Task DeletedPlaylist_DisappearsAndCascadesOverride()
    {
        await _service.AdminSetAsync(_playlistHot, new UpdateRecommendedRequest(SortOrder: 1));
        await _service.AdminSetAsync(_playlistCollected, new UpdateRecommendedRequest(SortOrder: 2));

        // 后台强删歌单（与 PlaylistsController 的 AdminDelete 同一路径：物理删行）
        _db.Playlists.Remove(await _db.Playlists.FirstAsync(p => p.Id == _playlistHot));
        await _db.SaveChangesAsync();

        var names = await OrderAsync(RecommendScenes.Hot);
        Assert.DoesNotContain("热门", names);
        Assert.Equal(["收藏多", "最新"], names);

        // 干预行随歌单一起级联删除：否则同一 Id 被新歌单复用时会出现"新歌单莫名被置顶"
        Assert.False(await _db.AdminRecommended.AnyAsync(r => r.PlaylistId == _playlistHot));
    }

    // ── system 歌单永不进推荐（"我喜欢的音乐"是私人的） ─────────────────────
    [Fact]
    public async Task SystemPlaylist_NeverRecommended()
    {
        var owner = _db.Users.First(u => u.UserName == "owner");
        _db.Playlists.Add(new Playlist { OwnerId = owner.Id, Name = "我喜欢的音乐", IsSystem = true, CoverUrl = "/x.jpg" });
        await _db.SaveChangesAsync();

        Assert.DoesNotContain("我喜欢的音乐", await OrderAsync(RecommendScenes.New));
        Assert.DoesNotContain("我喜欢的音乐", await OrderAsync(RecommendScenes.Hot));
    }

    // ── 场景名容错：未知值 / 大小写 / 空格都落到热播，不 400 ─────────────────
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" HOT ")]
    [InlineData("unknown-scene")]
    public async Task UnknownScene_FallsBackToHot(string? scene)
    {
        Assert.Equal(await OrderAsync(RecommendScenes.Hot), await OrderAsync(scene!));
    }

    // ── 分页稳定：翻页不重不漏 ─────────────────────────────────────────────
    [Fact]
    public async Task Paging_IsStableAndComplete()
    {
        var first = await _service.GetPlaylistsAsync(RecommendScenes.Collected, null, null, 1, 1);
        var second = await _service.GetPlaylistsAsync(RecommendScenes.Collected, null, null, 2, 1);
        var third = await _service.GetPlaylistsAsync(RecommendScenes.Collected, null, null, 3, 1);

        Assert.Equal(3, first.Total);
        Assert.Equal(["收藏多", "最新", "热门"], new[] { first, second, third }.Select(p => p.Items.Single().Name));
    }

    // ── 分区推荐（scene=zone）：区内按播放量降序 ────────────────────────────
    [Fact]
    public async Task Zone_OrdersByPlayCountWithinCategory()
    {
        var western = await _db.Categories.FirstAsync(c => c.Name == "欧美");
        var chinese = await _db.Categories.FirstAsync(c => c.Name == "华语");

        // 欧美区只有『收藏多』；华语区两个，按播放量 150 > 1 排
        Assert.Equal(["收藏多"], await OrderAsync(RecommendScenes.Zone, western.Id));
        Assert.Equal(["热门", "最新"], await OrderAsync(RecommendScenes.Zone, chinese.Id));
    }

    // ── 分区推荐缺 categoryId：返回空列表，不静默换场景 ──────────────────────
    [Fact]
    public async Task Zone_WithoutCategoryId_ReturnsEmpty()
    {
        var result = await _service.GetPlaylistsAsync(RecommendScenes.Zone, null, null, 1, 20);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.Total);
    }

    // ── 后台：列表带人工干预状态 ───────────────────────────────────────────
    [Fact]
    public async Task AdminList_ReportsOverrideState()
    {
        await _service.AdminSetAsync(_playlistNewest, new UpdateRecommendedRequest(SortOrder: 1, Weight: 7));

        var page = await _service.AdminListAsync(null, 1, 20);

        Assert.Equal(3, page.Total);
        // 已干预的排最前
        Assert.Equal("最新", page.Items[0].Name);
        Assert.True(page.Items[0].HasOverride);
        Assert.Equal(1, page.Items[0].SortOrder);
        Assert.Equal(7, page.Items[0].Weight);

        var untouched = page.Items.Single(x => x.Name == "热门");
        Assert.False(untouched.HasOverride);
        Assert.Equal(0, untouched.SortOrder);
        Assert.Equal(150, untouched.PlayCount);
        Assert.Equal(2, page.Items.Single(x => x.Name == "收藏多").CollectorCount);
    }

    /// <summary>
    /// 后台列表**不过滤**系统歌单：审核视角要看全量，列表里看不见反而让人以为漏了数据。
    /// （它们永远不会被推荐 —— 见 <see cref="SystemPlaylist_NeverRecommended"/>。）
    /// </summary>
    [Fact]
    public async Task AdminList_KeepsSystemPlaylistsVisible()
    {
        var owner = await _db.Users.SingleAsync(u => u.UserName == "owner");
        _db.Playlists.Add(new Playlist { OwnerId = owner.Id, Name = "我喜欢的音乐", IsSystem = true, CoverUrl = "/x.jpg" });
        await _db.SaveChangesAsync();

        var page = await _service.AdminListAsync(null, 1, 20);

        Assert.Equal(4, page.Total);
        Assert.True(page.Items.Single(x => x.Name == "我喜欢的音乐").IsSystem);
    }

    // 注：后台关键字搜索走 PostgreSQL 的 ILike，SQLite 上无法翻译，因此不在这里测
    // （与其它带 ILike 的既有实现一致，改由本地 API + PostgreSQL 端到端验证）。

    // ── 后台设置幂等：重复提交是覆盖，不会插出第二行 ─────────────────────────
    [Fact]
    public async Task AdminSet_IsIdempotentUpsert()
    {
        Assert.True((await _service.AdminSetAsync(_playlistHot, new UpdateRecommendedRequest(SortOrder: 3))).Success);
        Assert.True((await _service.AdminSetAsync(_playlistHot, new UpdateRecommendedRequest(SortOrder: 5, Weight: 2))).Success);

        var rows = await _db.AdminRecommended.Where(r => r.PlaylistId == _playlistHot).ToListAsync();
        Assert.Single(rows);
        Assert.Equal(5, rows[0].SortOrder);
        Assert.Equal(2, rows[0].Weight);
    }

    // ── 后台设置：负置顶序号夹到 0（不是报错），负权重保留（压低是正经用法） ──
    [Fact]
    public async Task AdminSet_ClampsNegativeSortOrderButKeepsNegativeWeight()
    {
        Assert.True((await _service.AdminSetAsync(_playlistHot, new UpdateRecommendedRequest(SortOrder: -9, Weight: -50))).Success);

        var row = await _db.AdminRecommended.SingleAsync(r => r.PlaylistId == _playlistHot);
        Assert.Equal(0, row.SortOrder);
        Assert.Equal(-50, row.Weight);
    }

    [Fact]
    public async Task AdminSet_UnknownPlaylist_Fails()
    {
        var result = await _service.AdminSetAsync(99999, new UpdateRecommendedRequest(SortOrder: 1));

        Assert.False(result.Success);
        Assert.Equal(RecommendService.PlaylistNotFound, result.Error);
    }

    [Fact]
    public async Task AdminRemove_ClearsOverrideAndIsNotIdempotent()
    {
        await _service.AdminSetAsync(_playlistHot, new UpdateRecommendedRequest(SortOrder: 1));

        Assert.True((await _service.AdminRemoveAsync(_playlistHot)).Success);
        Assert.Equal(["热门", "收藏多", "最新"], await OrderAsync(RecommendScenes.Hot));

        // 再删一次：如实报错（后台"移除干预"按钮在无记录的行上本不该出现）
        var again = await _service.AdminRemoveAsync(_playlistHot);
        Assert.False(again.Success);
        Assert.Equal(RecommendService.NoOverride, again.Error);
    }

    // ── 下线与移除干预是两回事：下线保留干预行，重新上线即可恢复 ─────────────
    [Fact]
    public async Task Hidden_CanBeBroughtBackWithoutLosingPin()
    {
        await _service.AdminSetAsync(_playlistHot, new UpdateRecommendedRequest(SortOrder: 1));
        await _service.AdminSetAsync(_playlistHot, new UpdateRecommendedRequest(SortOrder: 1, IsHidden: true));
        Assert.DoesNotContain("热门", await OrderAsync(RecommendScenes.Hot));

        // 后台"恢复"：把 IsHidden 关掉，置顶序号还在
        await _service.AdminSetAsync(_playlistHot, new UpdateRecommendedRequest(SortOrder: 1));
        Assert.Equal(["热门", "收藏多", "最新"], await OrderAsync(RecommendScenes.Hot));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
