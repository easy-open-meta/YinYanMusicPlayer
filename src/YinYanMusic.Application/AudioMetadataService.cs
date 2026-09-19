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
    private readonly string? _musicDir = config["Media:MusicDirectory"];
    private readonly string _imageDir = Path.Combine(env.ContentRootPath, "wwwroot", "media", "image");

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
            var fileName = Uri.UnescapeDataString(song.AudioUrl.Replace("/media/audio/", ""));
            var filePath = Path.Combine(_musicDir, fileName);
            if (!File.Exists(filePath)) continue;
            var duration = Mp3DurationReader.GetDuration(filePath);
            if (duration <= 0) continue;
            // 向下取整（旧数据用 Math.Round，会出现列表 3:01 / 播放页 3:00 的 1 秒偏差）。
            // 对已有时长的歌也校正一次：Cache 保证每首歌每进程只探测一次，避免重复开销。
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

            byte[] data;
            var ext = ".jpg";
            using (var tfile = TagLib.File.Create(filePath))
            {
                var pics = tfile.Tag.Pictures;
                if (pics is not { Length: > 0 }) return null;
                data = pics[0].Data.Data;
                if (data.Length == 0) return null;
                ext = (pics[0].MimeType ?? "").ToLowerInvariant() switch
                {
                    var m when m.Contains("png") => ".png",
                    var m when m.Contains("webp") => ".webp",
                    _ => ".jpg"
                };
            }

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
