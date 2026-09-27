using SQLite;

namespace YinYanMusic.App.Services.LocalLibrary;

/// <summary>
/// 歌曲缓存（V2.7）：缓存索引的读写、容量统计、上限校验、启动对账。
///
/// <para><b>它在体系里的位置</b>：<see cref="CacheDownloadService"/> 负责"把文件拉下来"，
/// 本类负责"记住哪些歌已经在本机、占了多少空间、还能不能再存"。播放侧
/// （<see cref="PlayerService"/>）只通过本类判断"这首歌有没有缓存"。</para>
///
/// <para><b>内存索引</b>：菜单要在同步上下文里立刻知道"这首缓存了吗"，所以整表常驻一个
/// <c>Dictionary&lt;long, CachedSong&gt;</c>。量级只有几百行，比每次查 SQLite 省事且不卡 UI。
/// 索引由 <see cref="EnsureIndexAsync"/> 懒加载，所有写操作都会同步维护它。</para>
///
/// <para><b>达到上限的行为（已定案，别再改）</b>：达到上限就**停止缓存并提示**，
/// <b>绝不自动删除旧缓存</b>——用户以为还在的歌突然没了是最糟的体验。</para>
/// </summary>
public sealed class CacheStore
{
    /// <summary>上限下限（GB）。0 会让缓存功能事实上不可用，所以不允许填 0 或负数（TC-2.7-06）。</summary>
    public const double MinLimitGb = 0.1;

    /// <summary>上限硬顶（GB）。需求明确"最大 10GB"。</summary>
    public const double MaxLimitGb = 10.0;

    /// <summary>用户没设置过时的默认上限。取中间值：既不至于一首就满，也不会悄悄吃掉 10GB。</summary>
    public const double DefaultLimitGb = 5.0;

    private const string LimitPreferenceKey = "yinyan.cache.limitGb";

    /// <summary>「播完自动缓存」开关（默认开，设置页可关）。</summary>
    private const string AutoCacheEnabledKey = "yinyan.cache.autoCacheEnabled";

    /// <summary>"因为超过上限而没能自动缓存"的累计条数，缓存管理页展示（清空缓存时归零）。</summary>
    private const string AutoCacheSkippedKey = "yinyan.cache.autoCacheSkipped";

    private const string CacheFolderName = "media-cache";

    /// <summary>未完成的下载残留（<c>&lt;songId&gt;.part</c>）超过这个时长就当作垃圾清掉。</summary>
    private static readonly TimeSpan PartialFileTtl = TimeSpan.FromDays(1);

    private readonly LocalDatabase _db;

    /// <summary>缓存历史（「缓存历史」标签页的数据源）：移除/清空时往它写一条。</summary>
    private readonly CacheHistoryStore _history;

    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private readonly Dictionary<long, CachedSong> _index = [];
    private bool _indexLoaded;

    public CacheStore(LocalDatabase db, CacheHistoryStore history)
    {
        _db = db;
        _history = history;
    }

    /// <summary>
    /// 缓存内容变化（新增/移除/清空/对账纠正）时触发。
    /// <para>UI 侧靠它刷新「已缓存」标记与容量显示：缓存可能在别的页面被触发
    /// （菜单里点"移除本地缓存"、下载后台完成），订阅方拿不到那次操作的返回值。</para>
    /// </summary>
    public event EventHandler? CacheChanged;

    private void RaiseChanged() => CacheChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>缓存文件的落盘目录。</summary>
    public static string CacheDirectory => Path.Combine(FileSystem.AppDataDirectory, CacheFolderName);

    // ── 容量上限（Preferences 持久化，单一配置值不必入库） ────────────────────

    /// <summary>当前生效的上限（GB）。非法或未设置时回落到 <see cref="DefaultLimitGb"/>。</summary>
    public double LimitGb
    {
        get
        {
            var saved = Preferences.Default.Get(LimitPreferenceKey, DefaultLimitGb);
            return IsValidLimitGb(saved) ? saved : DefaultLimitGb;
        }
    }

    /// <summary>当前生效的上限（字节）。</summary>
    public long LimitBytes => (long)(LimitGb * 1024d * 1024d * 1024d);

    /// <summary>校验范围：<c>0 &lt; x ≤ 10</c>（GB）。</summary>
    public static bool IsValidLimitGb(double gb) =>
        !double.IsNaN(gb) && gb > 0 && gb >= MinLimitGb && gb <= MaxLimitGb;

    /// <summary>保存上限。非法值原样返回 false，由调用方给出提示（TC-2.7-06）。</summary>
    public bool TrySetLimitGb(double gb)
    {
        if (!IsValidLimitGb(gb)) return false;
        Preferences.Default.Set(LimitPreferenceKey, gb);
        RaiseChanged();
        return true;
    }

    /// <summary>把上限恢复成默认值。</summary>
    public void ResetLimit() => Preferences.Default.Remove(LimitPreferenceKey);

    // ── 播完自动缓存（2026-09-22 用户定案） ─────────────────────────────────

    /// <summary>
    /// 是否允许"整首听完后自动缓存"。**默认开**，设置页可关。
    /// <para>关掉之后只剩手动缓存（歌曲菜单里的「缓存到本地」），适合在意流量/空间的用户。</para>
    /// </summary>
    public bool AutoCacheEnabled
    {
        get => Preferences.Default.Get(AutoCacheEnabledKey, true);
        set
        {
            Preferences.Default.Set(AutoCacheEnabledKey, value);
            RaiseChanged();
        }
    }

    /// <summary>
    /// 因超过上限而**没能自动缓存**的累计条数。缓存管理页据此提示用户
    /// "有 N 首因为超过上限没有自动缓存" —— 而不是让自动缓存无声无息地什么都不做。
    /// </summary>
    public int AutoCacheSkippedCount => Preferences.Default.Get(AutoCacheSkippedKey, 0);

    /// <summary>自动缓存被上限拒绝时 +1。</summary>
    public void IncrementAutoCacheSkipped() =>
        Preferences.Default.Set(AutoCacheSkippedKey, AutoCacheSkippedCount + 1);

    /// <summary>清空缓存时归零：空间已经腾出来了，历史计数不再有意义。</summary>
    public void ResetAutoCacheSkipped() => Preferences.Default.Remove(AutoCacheSkippedKey);

    // ── 索引读写 ────────────────────────────────────────────────────────────

    /// <summary>已缓存歌曲数（内存索引，同步）。索引尚未加载时返回 0，UI 应在加载后再读。</summary>
    public int CachedCount => _index.Count;

    /// <summary>这首歌是否已缓存（同步，供菜单/列表判定）。索引未加载时会漏判，先 <see cref="EnsureIndexAsync"/>。</summary>
    public bool IsCached(long songId) => _index.ContainsKey(songId);

    /// <summary>取缓存条目（同步）。没有则返回 null。</summary>
    public CachedSong? TryGetCached(long songId) =>
        _index.TryGetValue(songId, out var entry) ? entry : null;

    /// <summary>把整张缓存索引读进内存。可重入、幂等。</summary>
    public async Task EnsureIndexAsync()
    {
        if (_indexLoaded) return;

        await _indexGate.WaitAsync();
        try
        {
            if (_indexLoaded) return;

            var conn = await _db.GetConnectionAsync();
            var all = await conn.Table<CachedSong>().ToListAsync();

            _index.Clear();
            foreach (var entry in all) _index[entry.SongId] = entry;
            _indexLoaded = true;
        }
        finally
        {
            _indexGate.Release();
        }

        // 索引加载完成才通知一次：在此之前 IsCached / TryGetCached 都是"不认识的歌"，
        // 播放页的「已缓存」标记、缓存管理页的列表都可能因此少显示内容。
        // 放在锁外抛，避免订阅方同步回调回来又撞上初始化闸门。
        RaiseChanged();
    }

    /// <summary>全部缓存条目，按缓存时间倒序（最近缓存的在最上面）。</summary>
    public async Task<List<CachedSong>> GetAllAsync()
    {
        await EnsureIndexAsync();
        return [.. _index.Values.OrderByDescending(c => c.CachedAtUtc).ThenBy(c => c.Title)];
    }

    /// <summary>已用容量（字节）。走 SQLite 聚合，避免依赖内存索引的完整性。</summary>
    public async Task<long> TotalBytesAsync()
    {
        var conn = await _db.GetConnectionAsync();
        var rows = await conn.QueryScalarsAsync<long>("SELECT IFNULL(SUM(SizeBytes), 0) FROM CachedSong");
        return rows.Count > 0 ? rows[0] : 0;
    }

    /// <summary>新增/覆盖一条缓存索引（下载完成后调用）。</summary>
    public async Task AddAsync(CachedSong entry)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.InsertOrReplaceAsync(entry);

        _index[entry.SongId] = entry;
        RaiseChanged();
    }

    /// <summary>
    /// 移除一条缓存：**先删索引再删文件**。
    /// 顺序很重要——中途失败时宁可留下一个孤儿文件（下次对账/清空会顺手带走），
    /// 也不能留下"索引说有、文件已经没了"的坏状态（那会让离线播放直接失败）。
    /// </summary>
    public async Task<bool> RemoveAsync(long songId)
    {
        var conn = await _db.GetConnectionAsync();
        var entry = await conn.Table<CachedSong>().Where(c => c.SongId == songId).FirstOrDefaultAsync();
        if (entry is null)
        {
            _index.Remove(songId);
            return false;
        }

        await conn.DeleteAsync(entry);
        _index.Remove(songId);
        TryDeleteFile(entry.FilePath);
        TryDeleteFile(PartialPath(songId));
        RaiseChanged();

        // 记一条历史：用户之后在「缓存历史」里能看到"这首是被移除的"（而不是"莫名其妙没了"）
        await _history.RecordAsync(new CacheHistoryEntry
        {
            SongId = entry.SongId,
            Title = string.IsNullOrWhiteSpace(entry.Title) ? $"歌曲 {entry.SongId}" : entry.Title,
            ArtistName = entry.ArtistsDisplay,   // 全部歌手：与「缓存中 / 已缓存」两张卡片同一口径
            CoverUrl = entry.CoverUrl,
            Event = CacheHistoryEvent.Removed,
            SizeBytes = entry.SizeBytes,
        });
        return true;
    }

    /// <summary>清空全部缓存（文件 + 索引）。不动本地曲库。</summary>
    public async Task ClearAsync()
    {
        var conn = await _db.GetConnectionAsync();
        var all = await conn.Table<CachedSong>().ToListAsync();
        await conn.DeleteAllAsync<CachedSong>();

        foreach (var entry in all) TryDeleteFile(entry.FilePath);
        _index.Clear();
        _indexLoaded = true;

        // 目录里可能还有下载中断留下的 .part，一并清掉
        foreach (var part in EnumeratePartialFiles()) TryDeleteFile(part);

        // 空间已经腾空，"因超上限跳过 N 首"的历史计数没有意义了（自动缓存会重新开始工作）
        ResetAutoCacheSkipped();

        RaiseChanged();

        // 历史记一条**汇总**：清空 200 首就写 200 条会把历史刷爆，一条带总数与总大小足够说明问题
        if (all.Count > 0)
        {
            await _history.RecordAsync(new CacheHistoryEntry
            {
                SongId = 0,
                Title = $"清空缓存（{all.Count} 首）",
                Event = CacheHistoryEvent.Cleared,
                SizeBytes = all.Sum(c => c.SizeBytes),
            });
        }
    }

    /// <summary>更新最近访问时间（播放缓存歌时调用）。失败不影响播放。</summary>
    public async Task TouchAsync(long songId)
    {
        try
        {
            var conn = await _db.GetConnectionAsync();
            var now = DateTime.UtcNow;
            await conn.ExecuteAsync("UPDATE CachedSong SET LastAccessUtc = ? WHERE SongId = ?", now, songId);
            if (_index.TryGetValue(songId, out var entry)) entry.LastAccessUtc = now;
        }
        catch
        {
            // 只是统计性字段，写不进去不值得打扰用户
        }
    }

    /// <summary>
    /// 启动对账（TC-2.7-10）：把"索引里有、文件已经不在"的条目清掉，并修正被外部改动过的大小。
    /// <para>用户在文件管理器/清理工具里删掉缓存文件后，索引若不纠正，容量统计会虚高、
    /// 离线播放会失败。对账后容量与条数立刻恢复准确。</para>
    /// </summary>
    /// <returns>被清掉的条目数。</returns>
    public async Task<int> ReconcileAsync()
    {
        try
        {
            // ⚠️ 必须先确保索引已加载：对账要同时更新 SQLite 与内存索引，
            // 而 _indexLoaded 是"内存索引可用"的标志位。若在这里把它置真却从没填过 _index，
            // 后续 IsCached / GetAllAsync 都会拿到空结果（缓存管理页会显示成"一首都没缓存"）。
            await EnsureIndexAsync();

            var conn = await _db.GetConnectionAsync();
            var all = await conn.Table<CachedSong>().ToListAsync();

            var removed = 0;
            foreach (var entry in all)
            {
                var exists = LocalFileAccess.Exists(entry.FilePath);
                long actualSize = 0;
                if (exists)
                {
                    try { actualSize = new FileInfo(entry.FilePath).Length; } catch { actualSize = entry.SizeBytes; }
                }

                if (!exists)
                {
                    await conn.DeleteAsync(entry);
                    _index.Remove(entry.SongId);
                    removed++;
                }
                else if (actualSize > 0 && actualSize != entry.SizeBytes)
                {
                    entry.SizeBytes = actualSize;
                    await conn.UpdateAsync(entry);
                    _index[entry.SongId] = entry;
                }
            }

            // 顺手清掉超期未完成的下载残留（前一天中断的下载不放着占空间）
            var cutoff = DateTime.UtcNow - PartialFileTtl;
            foreach (var part in EnumeratePartialFiles())
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(part) < cutoff) File.Delete(part);
                }
                catch { }
            }

            if (removed > 0) RaiseChanged();
            return removed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CacheStore] 对账失败: {ex.Message}");
            return 0;
        }
    }

    // ── 路径工具（下载服务与播放器共用） ────────────────────────────────────

    /// <summary>下载中的临时文件路径（<c>&lt;songId&gt;.part</c>）。断点续传靠它的长度续下去。</summary>
    public static string PartialPath(long songId) =>
        Path.Combine(CacheDirectory, $"{songId}.part");

    /// <summary>缓存文件的最终路径。<paramref name="extension"/> 形如 <c>.flac</c>（含点）。</summary>
    public static string TargetPath(long songId, string extension) =>
        Path.Combine(CacheDirectory, $"{songId}{extension}");

    /// <summary>确保缓存目录存在。</summary>
    public static void EnsureDirectory() => Directory.CreateDirectory(CacheDirectory);

    private static IEnumerable<string> EnumeratePartialFiles()
    {
        try
        {
            if (!Directory.Exists(CacheDirectory)) return [];
            return Directory.EnumerateFiles(CacheDirectory, "*.part");
        }
        catch
        {
            return [];
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // 文件被占用/无权限：留着不影响主流程（索引已经删了，最多留个孤儿文件）
        }
    }
}
