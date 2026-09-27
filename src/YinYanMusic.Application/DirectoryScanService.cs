using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

/// <summary>
/// 目录扫描服务（V2.8）：扫 <c>Media:MusicDirectory</c>，用 ATL 读标签与解析时长，
/// 按 <c>AudioUrl</c> 去重后把新歌写进曲库，并为已入库但时长为 0 的歌补时长。
/// <para>
/// **所有"扫描入库"入口都走这里**：定时任务、后台「立即扫描」、后台「执行导入」——
/// 区别只在触发方式与能否指定目录，解析规则完全一套（读标签 + 校验时长 +
/// 递归 + 按相对路径去重）。历史上后台导入曾是另一套"按文件名拆「歌手 - 歌名」"的实现，
/// 同一个目录换个入口导入就会得到不同质量的记录（无专辑、无发行年份、时长恒为 0、坏文件照收），
/// 2026-09-23 已收口到这里。
/// </para>
/// <para>唯一没走这里的是 Scanner CLI（独立工程，额外做封面落盘、<c>--force</c>/<c>--dry-run</c>）。</para>
/// </summary>
public interface IDirectoryScanService
{
    /// <summary>
    /// 扫一轮并增量入库。<paramref name="dir"/> 留空 = 配置里的 <c>Media:MusicDirectory</c>；
    /// 传了则扫那个目录（后台「执行导入」的可选目录）。
    /// </summary>
    Task<ScanSnapshot> ScanAsync(string trigger, string? dir = null, CancellationToken ct = default);
}

public class DirectoryScanService(
    MusicDbContext db,
    IConfiguration config,
    ScanOptions options,
    ScanState state,
    IWebHostEnvironment env,
    ILogger<DirectoryScanService> logger) : IDirectoryScanService
{
    private static readonly string[] AudioExts = [".mp3", ".flac", ".m4a", ".wav", ".ogg"];

    private const int MaxTitleLength = 128;
    private const int MaxArtistNameLength = 64;
    private const int MaxAlbumNameLength = 128;
    private const int MaxUrlLength = 2000;
    private const string AudioUrlPrefix = "/media/audio/";
    private const string ImageUrlPrefix = "/media/image/";

    /// <summary>封面落盘目录（与 AudioMetadataService 同一解析：<c>Media:ImageDirectory</c> 优先，回退 wwwroot）。</summary>
    private readonly string _imageDir = ResolveImageDir(config, env);

    private static string ResolveImageDir(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config["Media:ImageDirectory"];
        return !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, "wwwroot", "media", "image");
    }

    /// <summary>单轮最多处理的文件数。防止运维误把整个盘挂进来时一轮跑到天荒地老。</summary>
    private const int MaxFilesPerRun = 20000;

    /// <summary>每多少条写一次库：控制单事务大小，也让中断时已提交的批次保持完整。</summary>
    private const int BatchSize = 100;

    public async Task<ScanSnapshot> ScanAsync(string trigger, string? dir = null, CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();

        // 扫描目录：显式传入优先（后台「执行导入」的目录框），否则用配置里的音乐目录
        var musicDir = string.IsNullOrWhiteSpace(dir) ? config["Media:MusicDirectory"] : dir.Trim();
        if (string.IsNullOrWhiteSpace(musicDir) || !Directory.Exists(musicDir))
        {
            // 目录不存在是**可预期的配置状态**（容器忘了挂卷 / 路径写错），不是异常：
            // 记录一次错误、让状态接口能回显，但绝不抛出 —— 否则定时器每轮都会炸（TC-2.8-10）。
            var msg = $"音乐目录不存在或不可读: {musicDir}";
            logger.LogWarning("[目录扫描] {Message}", msg);
            return Finish(state, startedAt, trigger, 0, 0, 0, 0, msg);
        }

        // 「存在但没权限列目录」必须单独探一次，不能只靠上面那个 Directory.Exists：
        // 实测给目录加一条拒绝 Read/Execute 的 ACL 后，Directory.Exists 仍返回 true，
        // 而下面的枚举开了 IgnoreInaccessible（为了容忍个别读不了的子目录），会把
        // "列目录被拒"静默变成"空目录" —— 于是没权限被伪装成成功 + 0 个文件，运维看不出任何异常。
        try
        {
            _ = Directory.EnumerateFileSystemEntries(musicDir).Take(1).ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            var msg = $"音乐目录没有读取权限: {musicDir}";
            logger.LogWarning(ex, "[目录扫描] {Message}", msg);
            return Finish(state, startedAt, trigger, 0, 0, 0, 0, msg);
        }

        // 静态托管的音乐目录（URL 基准），与扫描目录可能是两个值 —— 见循环里的 urlRoot
        var servedRoot = config["Media:MusicDirectory"];

        int total = 0, imported = 0, updated = 0, failed = 0;
        string? error = null;
        try
        {
            var files = EnumerateAudioFiles(musicDir);
            total = files.Count;
            if (total > MaxFilesPerRun)
                logger.LogWarning("[目录扫描] 目录内音频文件 {Count} 个，超过单轮上限 {Max}，只处理前 {Max} 个。",
                    total, MaxFilesPerRun, MaxFilesPerRun);

            if (total == 0)
            {
                logger.LogInformation("[目录扫描] {Dir} 下没有音频文件，本轮跳过。", musicDir);
                return Finish(state, startedAt, trigger, 0, 0, 0, 0, null);
            }

            // 只取去重 / 补时长 / 修歌手关联要用的几列：整表读完整实体在大曲库下是纯浪费的内存。
            var existingSongs = await db.Songs.AsNoTracking()
                .Select(s => new { s.Id, s.AudioUrl, s.DurationSeconds, ArtistName = s.Artist.Name })
                .ToListAsync(ct);
            // URL → (Id, 时长, 主歌手名)；Id = 0 表示"本轮刚新增、库里还没有这一行"。
            var songsByUrl = new Dictionary<string, (long Id, int DurationSeconds, string ArtistName)>(StringComparer.Ordinal);
            foreach (var s in existingSongs) songsByUrl[s.AudioUrl] = (s.Id, s.DurationSeconds, s.ArtistName);

            var artists = await db.Artists.ToDictionaryAsync(a => a.Name, ct);
            // 专辑按「歌手 + 专辑名」去重（库里没有唯一索引，只能靠内存字典挡住并发建重名专辑）。
            var albumsByKey = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
            foreach (var album in await db.Albums.Include(a => a.Artist).ToListAsync(ct))
                albumsByKey.TryAdd(AlbumKey(album.Artist.Name, album.Name), album);

            var pending = 0;
            // 封面不能在 SaveChanges 前落盘（那时 Song.Id 还是 0），先攒着，批次提交后再统一写。
            var pendingCovers = new List<PendingCover>();
            // 分批提交：既控制单事务大小，也让"优雅停止/被强杀"时已处理的部分不丢（TC-2.8-07）。
            async Task FlushIfNeededAsync()
            {
                if (++pending < BatchSize) return;
                await db.SaveChangesAsync(ct);
                await WriteCoversAsync(pendingCovers, ct);
                pending = 0;
            }

            foreach (var file in files.Take(MaxFilesPerRun))
            {
                ct.ThrowIfCancellationRequested();

                // URL 基准：静态托管挂的是 Media:MusicDirectory，所以只要文件在它下面，
                // URL 就必须相对**它**生成 —— 否则"指定子目录导入"会写出少一层（或错一层）的路径，
                // 表现为"导入成功却播不出声"。扫的是音乐目录之外的目录时退回按扫描目录算
                // （那种文件本来就不由 /media/audio 提供，URL 只是个标识）。
                var urlRoot = IsUnder(servedRoot, file) ? servedRoot! : musicDir;
                var relative = Path.GetRelativePath(urlRoot, file);
                var audioUrl = BuildUrl(relative);
                if (audioUrl.Length > MaxUrlLength)
                {
                    failed++;
                    logger.LogWarning("[目录扫描] 跳过（路径过长）: {Relative}", relative);
                    continue;
                }

                var known = songsByUrl.TryGetValue(audioUrl, out var existing);

                // 本轮刚新增过同一路径（占位记录的 Id = 0）→ 什么都不用做
                if (known && existing.Id == 0) continue;

                // ── 库里已有：补时长 / 修歌手关联 ──────────────────────────────
                // 不读标签（省一次文件解析），也不动标题/歌手/专辑 —— 运维可能在后台手工
                // 修正过，定时扫描不该把人工修的成果覆盖回去。
                if (known)
                {
                    // 存量修复（不读文件，只看已存的歌手名）：早期导入把多歌手塞进了一个名字
                    // （"陈小春、陈国坤、…"），这里拆开、逐个建/找歌手、重新挂载并修正主歌手。
                    if (existing.Id != 0 && AudioTagReader.HasArtistSeparator(existing.ArtistName))
                    {
                        await RepairMergedArtistAsync(existing.Id, existing.ArtistName, artists, ct);
                        updated++;
                    }

                    if (existing.DurationSeconds > 0) continue;

                    if (!TryGetDurationSeconds(file, out var existingSeconds))
                    {
                        failed++;
                        logger.LogWarning("[目录扫描] 补时长失败（ATL 解析不出）: {Relative}", relative);
                        continue;
                    }

                    // 只更新 DurationSeconds 这一列。不要 db.Songs.Update(整实体)：那会把
                    // 读取时的全部列一起写回去，从而覆盖掉本轮扫描期间后台对这首歌的编辑（丢更新）。
                    await db.Songs.Where(s => s.Id == existing.Id)
                        .ExecuteUpdateAsync(u => u.SetProperty(s => s.DurationSeconds, existingSeconds), ct);
                    updated++;
                    continue;
                }

                // ── 新文件：读标签 → 定时长 → 入库 ───────────────────────────
                Meta meta;
                try
                {
                    meta = ReadMetadata(file);
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogWarning(ex, "[目录扫描] 元数据读取失败: {Relative}", relative);
                    continue;
                }

                // ATL 逐帧解析是时长的唯一来源（与 Scanner / AudioMetadataService 同一口径）。
                // 解析不出来（损坏 / 不支持的流）一律不入库 —— 否则歌单不渲染、播放自动跳过，
                // 表现为"导入成功却听不了"。
                if (!TryGetDurationSeconds(file, out var seconds))
                {
                    failed++;
                    logger.LogWarning("[目录扫描] 拒绝入库（ATL 解析失败）: {Relative}", relative);
                    continue;
                }

                var artist = GetOrCreateArtist(meta.Artists[0], artists);
                var album = meta.Album is null ? null : GetOrCreateAlbum(artist, meta.Album, meta.Year, albumsByKey);

                var song = new Song
                {
                    Title = meta.Title,
                    Artist = artist,
                    Album = album,
                    AudioUrl = audioUrl,
                    LyricUrl = BuildLyricUrl(urlRoot, file),
                    DurationSeconds = seconds
                };
                // 联合创作：把标签里的**所有**歌手都挂上（主歌手 Position = 0）。
                // 用导航属性而不是裸 ArtistId：本轮新建的歌手此时还没有自增 Id，
                // 交给 EF 在 SaveChanges 时按依赖顺序（歌手 → 歌曲 → 关联）解析。
                for (var i = 0; i < meta.Artists.Count; i++)
                    song.SongArtists.Add(new SongArtist
                    {
                        Song = song,
                        Artist = i == 0 ? artist : GetOrCreateArtist(meta.Artists[i], artists),
                        Position = i
                    });

                db.Songs.Add(song);
                // 登记占位（Id = 0）：同一轮里重复扫到同一路径时不会被当成"库里已有的行"去补时长
                songsByUrl[audioUrl] = (0, seconds, meta.Artists[0]);
                // 封面元数据随歌入库：批次提交拿到自增 Id 后落盘 song-{id}{ext} 并回填 CoverUrl
                if (meta.Cover is { Length: > 0 })
                    pendingCovers.Add(new PendingCover(song, album, meta.Cover, meta.CoverExtension));
                imported++;
                await FlushIfNeededAsync();
            }

            if (pending > 0)
            {
                await db.SaveChangesAsync(ct);
                await WriteCoversAsync(pendingCovers, ct);
            }

            logger.LogInformation("[目录扫描] 完成：扫描 {Total} 个文件，新入库 {Imported} 首，补时长 {Updated} 首，失败 {Failed} 个，耗时 {Elapsed:F1}s。",
                total, imported, updated, failed, watch.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            // 进程停止（docker stop / 服务停止）：不算失败，已提交批次的数据保持完整。
            logger.LogInformation("[目录扫描] 被取消（进程正在停止），已入库数据保持完整。");
            error = "扫描被取消（进程正在停止）";
        }
        catch (Exception ex)
        {
            error = $"{ex.Message} | {ex.InnerException?.Message}";
            logger.LogError(ex, "[目录扫描] 失败：{Message}", error);
        }

        return Finish(state, startedAt, trigger, total, imported, updated, failed, error);
    }

    private static ScanSnapshot Finish(
        ScanState state, DateTimeOffset startedAt, string trigger,
        int total, int imported, int updated, int failed, string? error)
    {
        var snapshot = new ScanSnapshot(
            StartedAtUtc: startedAt,
            FinishedAtUtc: DateTimeOffset.UtcNow,
            Trigger: trigger,
            Total: total,
            Imported: imported,
            Updated: updated,
            Failed: failed,
            Error: error,
            Succeeded: error is null,
            RunCount: state.RunCount + 1,
            Enabled: state.Enabled,
            IntervalMinutes: state.IntervalMinutes,
            DurationMs: 0);
        return state.Complete(snapshot with { DurationMs = (long)(snapshot.FinishedAtUtc!.Value - startedAt).TotalMilliseconds });
    }

    private List<string> EnumerateAudioFiles(string root)
    {
        var enumeration = new EnumerationOptions
        {
            RecurseSubdirectories = options.ScanRecursive,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive
        };
        return Directory.EnumerateFiles(root, "*", enumeration)
            .Where(f => AudioExts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryGetDurationSeconds(string file, out int seconds)
    {
        var duration = Mp3DurationReader.GetDuration(file);
        if (duration > 0)
        {
            // 向下取整：与 Scanner、播放页口径一致（Math.Round 会让列表比播放页多 1 秒）。
            seconds = (int)duration;
            return true;
        }
        seconds = 0;
        return false;
    }

    private static Meta ReadMetadata(string file)
    {
        // 标签统一走 AudioTagReader（底层 ATL）：标题/歌手（多值已拆好）/专辑/年份/内嵌封面都在里面处理好了。
        // 读不出来 → 抛出去由调用方计入 failed（与迁移前一致，坏文件不静默入库）。
        var tags = AudioTagReader.TryRead(file)
            ?? throw new InvalidOperationException("标签读取失败（文件损坏或格式不支持）");

        var title = tags.Title ?? Path.GetFileNameWithoutExtension(file);

        return new Meta(
            Truncate(title, MaxTitleLength)!,
            tags.Artists.Select(a => Truncate(a, MaxArtistNameLength)!).ToList(),
            Truncate(tags.Album, MaxAlbumNameLength),
            tags.Year,
            0,
            tags.Cover,
            tags.CoverExtension);
    }

    /// <summary>
    /// 封面落盘并回填 URL。必须在 <see cref="Song"/> 保存拿到自增 Id 之后调用；
    /// 失败只记日志、不阻断入库（封面缺失由读接口的 FillCoversAsync 兜底）。
    /// </summary>
    private async Task WriteCoversAsync(List<PendingCover> pendingCovers, CancellationToken ct)
    {
        if (pendingCovers.Count == 0) return;

        try
        {
            Directory.CreateDirectory(_imageDir);
            foreach (var item in pendingCovers)
            {
                var fileName = $"song-{item.Song.Id}{item.Extension}";
                var url = $"{ImageUrlPrefix}{fileName}";

                if (string.IsNullOrEmpty(item.Song.CoverUrl))
                {
                    var path = Path.Combine(_imageDir, fileName);
                    if (!File.Exists(path))
                        await File.WriteAllBytesAsync(path, item.Data, ct);
                    item.Song.CoverUrl = url;
                }

                if (item.Album is not null && string.IsNullOrEmpty(item.Album.CoverUrl))
                    item.Album.CoverUrl = url;
            }
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 进程停止：让取消异常继续向上传播，已入库的歌曲保持完整
            pendingCovers.Clear();
            throw;
        }
        catch (Exception ex)
        {
            // 封面写失败（目录只读/磁盘满等）不能把整轮扫描带崩：歌已入库，封面下次读接口会再尝试
            logger.LogWarning(ex, "[目录扫描] 封面落盘失败（不影响入库）：{Count} 首", pendingCovers.Count);
        }
        finally
        {
            pendingCovers.Clear();
        }
    }

    /// <summary>
    /// 存量修复：把"多歌手挤在一个名字里"的老记录拆开。
    /// 做法与新增路径一致 —— 逐个 get-or-create 歌手、补齐关联行、主歌手改成第一个拆分出来的名字，
    /// 并把原来那个合并名（如 <c>"陈小春、陈国坤、…"</c>）的挂载摘掉（它拆完就没有歌了，可在后台删除）。
    /// </summary>
    private async Task RepairMergedArtistAsync(long songId, string mergedName, Dictionary<string, Artist> artists, CancellationToken ct)
    {
        var names = AudioTagReader.SplitArtists(mergedName);
        if (names.Count <= 1) return;   // 没有分隔符（理论上不会走到这里）

        var song = await db.Songs.FirstOrDefaultAsync(s => s.Id == songId, ct);
        if (song is null) return;

        var oldArtistId = song.ArtistId;
        var linked = await db.SongArtists.Where(sa => sa.SongId == songId)
            .Select(sa => sa.ArtistId).ToListAsync(ct);
        var resolved = names.Select(n => GetOrCreateArtist(n, artists)).ToList();

        // ⚠️ 用导航属性赋值：本轮新建的歌手此时 Id 还是 0，交给 EF 在 SaveChanges 时解析外键
        song.Artist = resolved[0];

        // 旧的合并歌手（整串名字那一行）卸下来，否则它仍会以"合作歌手"身份显示这首歌
        if (oldArtistId != resolved[0].Id)
        {
            var stale = await db.SongArtists.FirstOrDefaultAsync(sa => sa.SongId == songId && sa.ArtistId == oldArtistId, ct);
            if (stale is not null) db.SongArtists.Remove(stale);
        }

        for (var i = 0; i < resolved.Count; i++)
        {
            var artist = resolved[i];
            if (artist.Id != 0 && linked.Contains(artist.Id)) continue;   // 已挂过，别重复插（复合主键会炸）
            db.SongArtists.Add(new SongArtist { SongId = songId, Artist = artist, Position = i });
        }

        await db.SaveChangesAsync(ct);

        // 拆分完了再收拾那个合并名：它从来不是一个真歌手（是把多个人塞进一行的产物），
        // 现在又一首歌都不挂 → 直接删掉，免得它继续在歌手列表和搜索里占位
        // （有人关注过、或还挂着专辑就不动，交给人处理）。
        if (oldArtistId != resolved[0].Id)
        {
            var orphan = await db.Artists.FirstOrDefaultAsync(a => a.Id == oldArtistId, ct);
            var stillUsed = await db.SongArtists.AnyAsync(sa => sa.ArtistId == oldArtistId, ct)
                || await db.Albums.AnyAsync(al => al.ArtistId == oldArtistId, ct)
                || await db.ArtistFollows.AnyAsync(f => f.ArtistId == oldArtistId, ct);
            if (orphan is not null && !stillUsed)
            {
                db.Artists.Remove(orphan);
                await db.SaveChangesAsync(ct);
            }
        }

        logger.LogInformation("[目录扫描] 拆分多歌手：歌曲 {SongId}「{Merged}」→ {Split}",
            songId, mergedName, string.Join(" / ", names));
    }

    private Artist GetOrCreateArtist(string name, Dictionary<string, Artist> cache)
    {
        if (cache.TryGetValue(name, out var artist)) return artist;
        artist = new Artist { Name = name };
        cache[name] = artist;
        db.Artists.Add(artist);
        return artist;
    }

    private Album GetOrCreateAlbum(Artist artist, string name, int? year, Dictionary<string, Album> cache)
    {
        var key = AlbumKey(artist.Name, name);
        if (cache.TryGetValue(key, out var existing)) return existing;

        var album = new Album { Name = name, Artist = artist };
        if (year is > 0) album.ReleaseDate = new DateOnly(year.Value, 1, 1);
        cache[key] = album;
        db.Albums.Add(album);
        return album;
    }

    private static string? BuildLyricUrl(string root, string file)
    {
        var sidecar = Path.ChangeExtension(file, ".lrc");
        if (!File.Exists(sidecar)) return null;
        var url = BuildUrl(Path.GetRelativePath(root, sidecar));
        return url.Length > MaxUrlLength ? null : url;
    }

    /// <summary>
    /// 生成服务端相对 URL：按路径段做 URI 转义（AudioMetadataService 用
    /// <c>Uri.UnescapeDataString</c> 反解回磁盘路径，中文/空格/括号的文件名都能正确往返）。
    /// </summary>
    private static string BuildUrl(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        var escaped = string.Join('/', normalized.Split('/').Select(Uri.EscapeDataString));
        return AudioUrlPrefix + escaped;
    }

    /// <summary>file 是否位于 root 之下（大小写不敏感，两端都取全路径再比）。</summary>
    private static bool IsUnder(string? root, string file)
    {
        if (string.IsNullOrWhiteSpace(root)) return false;
        var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var f = Path.GetFullPath(file);
        return f.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string AlbumKey(string artistName, string albumName) => artistName + "\u0000" + albumName;

    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    /// <param name="Artists">全部歌手（已拆分，第一个是主歌手）。</param>
    /// <param name="Cover">内嵌封面原始字节（没有则为 null）。</param>
    /// <param name="CoverExtension">封面扩展名（按 MIME 推断，默认 .jpg）。</param>
    private sealed record Meta(
        string Title,
        IReadOnlyList<string> Artists,
        string? Album,
        int? Year,
        int DurationSeconds,
        byte[]? Cover,
        string CoverExtension);

    private sealed record PendingCover(Song Song, Album? Album, byte[] Data, string Extension);
}
