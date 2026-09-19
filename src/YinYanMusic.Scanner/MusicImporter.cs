using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Scanner;

public sealed record ImportSettings(string? ImageDirectory, string? BaseUrl, bool Force, bool DryRun, int BatchSize);

public sealed class ImportSummary
{
    public int Scanned { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public int Covers { get; set; }
    public TimeSpan Elapsed { get; set; }
    public List<string> Errors { get; } = [];

    public void Print()
    {
        Console.WriteLine();
        Console.WriteLine("──────── 导入结果 ────────");
        Console.WriteLine($"  扫描文件 : {Scanned}");
        Console.WriteLine($"  新增     : {Added}");
        Console.WriteLine($"  更新     : {Updated}");
        Console.WriteLine($"  跳过     : {Skipped}");
        Console.WriteLine($"  失败     : {Failed}");
        Console.WriteLine($"  封面     : {Covers}");
        Console.WriteLine($"  耗时     : {Elapsed.TotalSeconds:F1} 秒");
        if (Errors.Count == 0) return;
        Console.WriteLine("  失败明细（最多 10 条）:");
        foreach (var error in Errors.Take(10))
            Console.WriteLine("    - " + error);
    }
}

/// <summary>
/// 扫描本地音乐目录，用 TagLib 解析元数据后写库。
/// URL 约定与 Api 端保持一致：音频 <c>/media/audio/&lt;相对路径&gt;</c>、封面 <c>/media/image/song-{id}{ext}</c>
/// （见 YinYanMusic.Application 的 AudioMetadataService / MediaService）。
/// </summary>
public sealed class MusicImporter(MusicDbContext db, ImportSettings settings)
{
    private static readonly string[] AudioExtensions = [".mp3", ".flac", ".m4a", ".wav", ".ogg"];

    private const int MaxTitleLength = 128;
    private const int MaxArtistNameLength = 64;
    private const int MaxAlbumNameLength = 128;
    private const int MaxUrlLength = 2000;
    private const string UnknownArtist = "未知艺术家";
    private const string AudioUrlPrefix = "/media/audio/";
    private const string ImageUrlPrefix = "/media/image/";

    private readonly Dictionary<string, Song> _songsByUrl = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Artist> _artistsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<long, string> _artistNamesById = [];
    private readonly Dictionary<string, Album> _albumsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PendingCover> _pendingCovers = [];

    private sealed record PendingCover(Song Song, Album? Album, byte[] Data, string Extension);

    private sealed record Metadata(
        string Title,
        string Artist,
        string? Album,
        int? Year,
        int DurationSeconds,
        byte[]? Cover,
        string CoverExtension);

    public async Task<ImportSummary> RunAsync(string root)
    {
        var summary = new ImportSummary();
        var watch = Stopwatch.StartNew();

        var files = EnumerateAudioFiles(root);
        summary.Scanned = files.Count;
        Console.WriteLine($"扫描到 {summary.Scanned} 个音频文件。");

        if (files.Count > 0)
        {
            try
            {
                await LoadExistingAsync();
            }
            catch (Exception ex) when (settings.DryRun)
            {
                Console.WriteLine($"[预演] 读取现有数据失败，全部按新增处理：{ex.Message}");
            }

            var index = 0;
            foreach (var file in files)
            {
                index++;
                var relative = Path.GetRelativePath(root, file);
                try
                {
                    ImportFile(root, file, relative, summary);
                }
                catch (Exception ex)
                {
                    summary.Failed++;
                    summary.Errors.Add($"{relative}: {ex.Message}");
                    Console.WriteLine($"  [失败] {relative}: {ex.Message}");
                }

                if (index % settings.BatchSize == 0)
                    await FlushAsync(summary);
            }
            await FlushAsync(summary);
        }

        watch.Stop();
        summary.Elapsed = watch.Elapsed;
        return summary;
    }

    private static List<string> EnumerateAudioFiles(string root)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive
        };
        return Directory.EnumerateFiles(root, "*", options)
            .Where(f => AudioExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task LoadExistingAsync()
    {
        foreach (var song in await db.Songs.ToListAsync())
            _songsByUrl[song.AudioUrl] = song;

        foreach (var artist in await db.Artists.ToListAsync())
        {
            _artistsByName.TryAdd(artist.Name, artist);
            _artistNamesById[artist.Id] = artist.Name;
        }

        foreach (var album in await db.Albums.ToListAsync())
        {
            if (_artistNamesById.TryGetValue(album.ArtistId, out var artistName))
                _albumsByKey.TryAdd(AlbumKey(artistName, album.Name), album);
        }

        Console.WriteLine($"数据库现有：{_songsByUrl.Count} 首歌 / {_artistsByName.Count} 位艺术家 / {_albumsByKey.Count} 张专辑。");
    }

    private void ImportFile(string root, string file, string relative, ImportSummary summary)
    {
        var audioUrl = BuildUrl(AudioUrlPrefix, Path.GetRelativePath(root, file));
        if (audioUrl.Length > MaxUrlLength)
        {
            summary.Failed++;
            summary.Errors.Add($"{relative}: 路径过长（超过 {MaxUrlLength} 字符）");
            return;
        }

        _songsByUrl.TryGetValue(audioUrl, out var existing);
        if (existing is not null && !settings.Force)
        {
            summary.Skipped++;
            Console.WriteLine($"  [跳过] {relative}");
            return;
        }

        var meta = ReadMetadata(file);

        // ATL 校验（与 Api 端时长唯一来源一致）：从音频数据逐帧解析时长，失败（损坏/不支持的流）不允许导入。
        // 同时时长入库统一取 ATL 向下取整值（不再用 TagLib 的 Math.Round，避免与播放页差 1 秒）。
        if (!TryGetAtlDurationSeconds(file, out var atlSeconds))
        {
            summary.Failed++;
            summary.Errors.Add($"{relative}: ATL 解析失败（无法从音频数据计算时长），拒绝导入");
            Console.WriteLine($"  [拒绝] {relative}: ATL 解析失败");
            return;
        }
        meta = meta with { DurationSeconds = atlSeconds };

        var lyricUrl = BuildLyricUrl(root, file);
        var artist = GetOrCreateArtist(meta.Artist);
        var album = meta.Album is null ? null : GetOrCreateAlbum(artist, meta.Album, meta.Year);

        if (existing is not null)
        {
            existing.Title = meta.Title;
            existing.Artist = artist;
            existing.Album = album;
            existing.DurationSeconds = meta.DurationSeconds;
            if (lyricUrl is not null) existing.LyricUrl = lyricUrl;
            summary.Updated++;
            Console.WriteLine($"  [更新] {meta.Title} - {artist.Name}");
            AddPendingCover(existing, album, meta);
            return;
        }

        var song = new Song
        {
            Title = meta.Title,
            Artist = artist,
            Album = album,
            AudioUrl = audioUrl,
            LyricUrl = lyricUrl,
            DurationSeconds = meta.DurationSeconds
        };
        db.Songs.Add(song);
        _songsByUrl[audioUrl] = song;
        summary.Added++;
        var duration = TimeSpan.FromSeconds(meta.DurationSeconds);
        var albumText = album is null ? string.Empty : $" / {album.Name}";
        Console.WriteLine($"  [新增] {meta.Title} - {artist.Name}{albumText}  ({duration:mm\\:ss})");
        AddPendingCover(song, album, meta);
    }

    /// <summary>
    /// ATL 时长解析（时长唯一来源，与 Api 端 Mp3DurationReader 同款）：从音频数据逐帧解析，
    /// 向下取整为整秒。解析失败/时长为 0 返回 false。
    /// </summary>
    private static bool TryGetAtlDurationSeconds(string file, out int seconds)
    {
        try
        {
            var track = new ATL.Track(file);
            if (track.DurationMs > 0)
            {
                seconds = (int)(track.DurationMs / 1000.0);
                return true;
            }
        }
        catch { }
        seconds = 0;
        return false;
    }

    private static Metadata ReadMetadata(string file)
    {
        using var tagFile = TagLib.File.Create(file);

        var title = tagFile.Tag.Title?.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = Path.GetFileNameWithoutExtension(file);

        var artist = (tagFile.Tag.FirstPerformer ?? tagFile.Tag.FirstAlbumArtist)?.Trim();
        if (string.IsNullOrWhiteSpace(artist))
            artist = UnknownArtist;

        var album = tagFile.Tag.Album?.Trim();
        if (string.IsNullOrWhiteSpace(album))
            album = null;

        var year = tagFile.Tag.Year is > 0 and < 3000 ? (int)tagFile.Tag.Year : (int?)null;

        // 时长不在 TagLib 里取：由 TryGetAtlDurationSeconds 统一提供（ATL 逐帧解析 + 向下取整）

        byte[]? cover = null;
        var coverExtension = ".jpg";
        if (tagFile.Tag.Pictures is { Length: > 0 } pictures
            && pictures[0].Data?.Data is { Length: > 0 } data)
        {
            cover = data;
            coverExtension = (pictures[0].MimeType ?? string.Empty).ToLowerInvariant() switch
            {
                var mime when mime.Contains("png") => ".png",
                var mime when mime.Contains("webp") => ".webp",
                _ => ".jpg"
            };
        }

        return new Metadata(
            Truncate(title, MaxTitleLength)!,
            Truncate(artist, MaxArtistNameLength)!,
            Truncate(album, MaxAlbumNameLength),
            year,
            0,   // DurationSeconds 由 TryGetAtlDurationSeconds 统一提供（with 表达式覆盖）
            cover,
            coverExtension);
    }

    private Artist GetOrCreateArtist(string name)
    {
        if (_artistsByName.TryGetValue(name, out var artist)) return artist;
        artist = new Artist { Name = name };
        _artistsByName[name] = artist;
        db.Artists.Add(artist);
        return artist;
    }

    private Album GetOrCreateAlbum(Artist artist, string name, int? year)
    {
        var key = AlbumKey(artist.Name, name);
        if (_albumsByKey.TryGetValue(key, out var album)) return album;

        album = new Album { Name = name, Artist = artist };
        if (year is > 0) album.ReleaseDate = new DateOnly(year.Value, 1, 1);
        _albumsByKey[key] = album;
        db.Albums.Add(album);
        return album;
    }

    private void AddPendingCover(Song song, Album? album, Metadata meta)
    {
        if (meta.Cover is { Length: > 0 })
            _pendingCovers.Add(new PendingCover(song, album, meta.Cover, meta.CoverExtension));
    }

    /// <summary>
    /// 提交当前批次：先保存拿到自增 Id，再按 song-{id}{ext} 落盘封面并回填 URL。
    /// 预演模式只统计不做任何写入。
    /// </summary>
    private async Task FlushAsync(ImportSummary summary)
    {
        if (!settings.DryRun)
            await db.SaveChangesAsync();

        if (_pendingCovers.Count > 0)
        {
            if (settings.DryRun || string.IsNullOrWhiteSpace(settings.ImageDirectory))
            {
                summary.Covers += _pendingCovers.Count;
            }
            else
            {
                Directory.CreateDirectory(settings.ImageDirectory!);
                foreach (var item in _pendingCovers)
                {
                    var fileName = $"song-{item.Song.Id}{item.Extension}";
                    var url = settings.BaseUrl is { Length: > 0 } baseUrl
                        ? $"{baseUrl.TrimEnd('/')}{ImageUrlPrefix}{fileName}"
                        : $"{ImageUrlPrefix}{fileName}";

                    if (string.IsNullOrEmpty(item.Song.CoverUrl))
                    {
                        await File.WriteAllBytesAsync(Path.Combine(settings.ImageDirectory!, fileName), item.Data);
                        item.Song.CoverUrl = url;
                        summary.Covers++;
                    }

                    if (item.Album is not null && string.IsNullOrEmpty(item.Album.CoverUrl))
                        item.Album.CoverUrl = url;
                }
                await db.SaveChangesAsync();
            }
            _pendingCovers.Clear();
        }
    }

    private static string? BuildLyricUrl(string root, string file)
    {
        var sidecar = Path.ChangeExtension(file, ".lrc");
        if (!File.Exists(sidecar)) return null;
        var url = BuildUrl(AudioUrlPrefix, Path.GetRelativePath(root, sidecar));
        return url.Length > MaxUrlLength ? null : url;
    }

    /// <summary>
    /// 生成服务端相对 URL。路径按段做 URI 转义（AudioMetadataService 读取时用
    /// Uri.UnescapeDataString 反解回磁盘路径，含中文/空格/括号的文件名都能正确往返）。
    /// </summary>
    private static string BuildUrl(string prefix, string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var escaped = string.Join('/', normalized.Split('/').Select(Uri.EscapeDataString));
        return prefix + escaped;
    }

    private static string AlbumKey(string artistName, string albumName) => artistName + "\u0000" + albumName;

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
