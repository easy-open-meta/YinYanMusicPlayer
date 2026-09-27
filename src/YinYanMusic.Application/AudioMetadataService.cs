using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public class AudioMetadataService(MusicDbContext db, IConfiguration config, IWebHostEnvironment env)
{
    private static readonly ConcurrentDictionary<long, int> Cache = new();
    private static readonly ConcurrentDictionary<long, bool> CoverAttempted = new();
    /// <summary>专辑 ID → 从文件标签读到的年份（null = 文件里没有年份）。每进程每张专辑只读一次。</summary>
    private static readonly ConcurrentDictionary<long, int?> AlbumTagYearCache = new();
    private readonly string? _musicDir = config["Media:MusicDirectory"];
    private readonly string _imageDir = ResolveImageDir(config, env);

    /// <summary>封面落盘目录：优先 <c>Media:ImageDirectory</c>（Docker bind mount 用），回退 <c>wwwroot/media/image</c>。</summary>
    private static string ResolveImageDir(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config["Media:ImageDirectory"];
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, "wwwroot", "media", "image");
    }

    /// <summary>
    /// 专辑发行时间：**以音频文件标签为准**（ATL），文件里没有年份才沿用库里的值。
    /// <para>
    /// 为什么放在读接口：库里的 <c>Album.ReleaseDate</c> 是**导入那一刻**写下的快照 ——
    /// 之后重打了标签、或专辑是从别的入口（后台手工建）进来的，库里的值都不会跟着变。
    /// 发行时间的出处应该是文件本身。
    /// </para>
    /// <para>
    /// 与 <see cref="FillDurationsAsync"/> 同一套做法：发现不一致就顺手把库里的值修正过来（自愈），
    /// 这样 App 与后台看到的是同一个值，不会出现"列表一个样、编辑框另一个样"。
    /// ⚠️ 只在**年份**不同时才覆盖 —— 后台手工填过的精确日期（如 2020-05-15）年份一致就保留，
    /// 不会被文件里"只有年份"的信息降级成 2020-01-01。
    /// </para>
    /// <para>
    /// ⚠️ 局限：标签里能拿到的精度就是"年"（ATL 的 Date/PublishingDate 在缺失时返回 0001-01-01，见 AudioTagReader），
    /// 所以这里只能定位到年、存成该年 1 月 1 日 —— 与导入时（Scanner）的口径一致。
    /// </para>
    /// </summary>
    public async Task FillAlbumReleaseDatesAsync(IList<AlbumDto> albums)
    {
        if (albums.Count == 0) return;

        var toUpdate = new List<(long AlbumId, DateOnly Date)>();
        for (var i = 0; i < albums.Count; i++)
        {
            var album = albums[i];
            if (!AlbumTagYearCache.TryGetValue(album.Id, out var cached))
            {
                cached = await ReadAlbumTagYearAsync(album.Id);
                AlbumTagYearCache[album.Id] = cached;
            }

            if (cached is not int year) continue;              // 文件里没年份 → 回退库里的值（可能也是 null）
            if (album.ReleaseDate?.Year == year) continue;      // 已是同一年 → 保持不动（含更精确的手工日期）

            var date = new DateOnly(year, 1, 1);
            albums[i] = album with { ReleaseDate = date };      // record：整体替换，所以入参是可写集合
            toUpdate.Add((album.Id, date));
        }

        foreach (var (albumId, date) in toUpdate)
            await db.Albums.Where(a => a.Id == albumId)
                .ExecuteUpdateAsync(u => u.SetProperty(a => a.ReleaseDate, date));
    }

    /// <summary>读该专辑下**第一个带年份标签**的文件年份；都没有则 null（= 交给库里的值）。</summary>
    private async Task<int?> ReadAlbumTagYearAsync(long albumId)
    {
        if (string.IsNullOrEmpty(_musicDir) || !Directory.Exists(_musicDir)) return null;

        var urls = await db.Songs.AsNoTracking()
            .Where(s => s.AlbumId == albumId)
            .Select(s => s.AudioUrl)
            .ToListAsync();

        foreach (var url in urls)
        {
            var filePath = Path.Combine(_musicDir, Uri.UnescapeDataString(url.Replace("/media/audio/", "")));
            if (!File.Exists(filePath)) continue;
            var year = AudioTagReader.TryReadYear(filePath);
            if (year is int y) return y;   // 坏文件/无年份返回 null → 继续看这张专辑的下一首
        }
        return null;
    }

    public async Task FillDurationsAsync(IReadOnlyList<SongDto> songs)
    {
        if (string.IsNullOrEmpty(_musicDir) || !Directory.Exists(_musicDir)) return;
        var toUpdate = new List<(long Id, int Seconds)>();
        foreach (var song in songs)
        {
            // 命中进程内缓存：以缓存值（与客户端进度条一致的向下取整口径）为准
            if (Cache.TryGetValue(song.Id, out var cached))
            {
                if (song.DurationSeconds != cached) song.DurationSeconds = cached;
                continue;
            }

            // 库里已经有值就不再逐文件探测 —— 读接口的职责是"把**未知**的补上"。
            // 原先对每首没进过缓存的歌都探一次，冷启动后打开一个 36 首的歌手页要 ~0.7s
            // （每个文件都要读帧），这才是"歌手页加载慢"的主因；暖机后又是 20ms 级的请求，
            // 于是快慢全看服务端进程刚不刚重启过 —— 用户体验上最难解释的那种慢。
            // 至于"库里存的值不对"（旧代码 Math.Round 造成的 1 秒偏差 / 换过音频文件）：
            // 写入端已统一成向下取整，扫描（DirectoryScanService / 后台「执行扫描」）会把库里
            // 的值改成与文件一致，不需要读路径反复兜。
            if (song.DurationSeconds > 0) continue;

            var fileName = Uri.UnescapeDataString(song.AudioUrl.Replace("/media/audio/", ""));
            var filePath = Path.Combine(_musicDir, fileName);
            if (!File.Exists(filePath)) continue;
            var duration = Mp3DurationReader.GetDuration(filePath);
            if (duration <= 0) continue;
            // 向下取整（旧数据用 Math.Round，会出现列表 3:01 / 播放页 3:00 的 1 秒偏差）
            var seconds = (int)duration;
            Cache[song.Id] = seconds;
            if (song.DurationSeconds != seconds)
            {
                song.DurationSeconds = seconds;
                toUpdate.Add((song.Id, seconds));
            }
        }
        if (toUpdate.Count > 0)
        {
            foreach (var (id, seconds) in toUpdate)
                await db.Songs.Where(s => s.Id == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.DurationSeconds, seconds));
        }
    }

    public async Task FillCoversAsync(IReadOnlyList<SongDto> songs)
    {
        if (string.IsNullOrEmpty(_musicDir) || !Directory.Exists(_musicDir)) return;

        var candidates = songs
            .Where(s => string.IsNullOrEmpty(s.CoverUrl))
            .Where(s => CoverAttempted.TryAdd(s.Id, true))
            .ToList();
        if (candidates.Count == 0) return;

        var songUpdates = new List<(long SongId, string Url)>();
        var albumUpdates = new List<(long AlbumId, string Url)>();
        foreach (var song in candidates)
        {
            var url = await ExtractCoverAsync(song.Id, song.AudioUrl);
            if (url is null) continue;
            song.CoverUrl = url;
            songUpdates.Add((song.Id, url));
            if (song.AlbumId.HasValue) albumUpdates.Add((song.AlbumId.Value, url));
        }
        if (songUpdates.Count == 0) return;

        foreach (var (songId, url) in songUpdates)
        {
            await db.Songs.Where(s => s.Id == songId && s.CoverUrl == null)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.CoverUrl, url));
        }
        foreach (var (albumId, url) in albumUpdates)
        {
            await db.Albums.Where(a => a.Id == albumId && a.CoverUrl == null)
                .ExecuteUpdateAsync(u => u.SetProperty(a => a.CoverUrl, url));
        }
    }

    /// <summary>
    /// 歌单封面回退：没有封面的歌单取「歌单内第一首歌」的封面
    /// （歌曲封面 → 专辑封面 → 内嵌图提取落盘）。首页精选歌单 / 我的歌单 / 收藏歌单 / 歌单详情头图统一走这里。
    /// </summary>
    public async Task FillPlaylistCoversAsync(IList<PlaylistDto> playlists)
    {
        for (var i = 0; i < playlists.Count; i++)
        {
            if (playlists[i].CoverUrl is not null) continue;

            var first = await db.PlaylistSongs.AsNoTracking()
                .Where(ps => ps.PlaylistId == playlists[i].Id)
                // 与歌单详情页展示顺序一致（最新添加在前）：点进去看到的第一首是谁，外面列表的封面就是谁
                .OrderByDescending(ps => ps.AddedAt).ThenByDescending(ps => ps.Position)
                .Select(ps => new { SongId = ps.Song.Id, SongCover = ps.Song.CoverUrl, ps.Song.AudioUrl, AlbumCover = ps.Song.Album.CoverUrl })
                .FirstOrDefaultAsync();
            if (first is null) continue;   // 空歌单保持占位

            var cover = first.SongCover ?? first.AlbumCover
                ?? await ExtractCoverAsync(first.SongId, first.AudioUrl);
            if (cover is null) continue;

            playlists[i] = playlists[i] with { CoverUrl = cover };
            // 提取出来的内嵌封面顺手固化到歌曲行，后续 FillCoversAsync 不再重复提取
            if (first.SongCover is null)
                await db.Songs.Where(s => s.Id == first.SongId && s.CoverUrl == null)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.CoverUrl, cover));
        }
    }

    public async Task<string?> ExtractCoverAsync(long songId, string audioUrl)
    {
        try
        {
            var fileName = Uri.UnescapeDataString(audioUrl.Replace("/media/audio/", ""));
            var filePath = Path.Combine(_musicDir!, fileName);
            if (!File.Exists(filePath)) return null;

            // 标签与内嵌封面统一走 AudioTagReader（底层 ATL）—— 全项目只有这一个实现
            var tags = AudioTagReader.TryRead(filePath);
            if (tags?.Cover is not { Length: > 0 } data) return null;
            var ext = tags.CoverExtension;

            Directory.CreateDirectory(_imageDir);
            var coverFileName = $"song-{songId}{ext}";
            var coverPath = Path.Combine(_imageDir, coverFileName);
            if (!File.Exists(coverPath))
                await File.WriteAllBytesAsync(coverPath, data);

            var baseUrl = config["Media:BaseUrl"];
            return string.IsNullOrWhiteSpace(baseUrl)
                ? $"/media/image/{coverFileName}"
                : $"{baseUrl.TrimEnd('/')}/media/image/{coverFileName}";
        }
        catch
        {
            return null;
        }
    }

}
