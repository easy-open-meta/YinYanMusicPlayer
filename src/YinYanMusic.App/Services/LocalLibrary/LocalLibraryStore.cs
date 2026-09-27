using SQLite;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Services.LocalLibrary;

/// <summary>一次 Upsert 的结果。</summary>
public enum UpsertOutcome
{
    /// <summary>库里原本没有，本次新插入。</summary>
    Inserted,
    /// <summary>库里已有，本次只是补齐（或无需改动）。</summary>
    Updated,
}

/// <summary>
/// 本地曲库存储（V2.6）：SQLite 落盘 + 幂等写入 + 计数/失效维护。
///
/// 库文件固定放在 <see cref="FileSystem.AppDataDirectory"/>（设备私有目录，两端都有写权限，
/// 且不会因为卸载重装以外的方式被清理）。所有对外方法都先 <c>await EnsureCreatedAsync()</c>，
/// 调用方不需要关心初始化时序。
///
/// 刻意用 <see cref="SQLiteAsyncConnection"/> 而非同步版本：扫描几十上百个文件时
/// 逐个同步写库会卡住 UI 线程。
/// </summary>
public class LocalLibraryStore
{
    /// <summary>库文件名。真正的常量定义在 <see cref="LocalDatabase"/>（V2.7 起缓存/补报表共用同一个库）。</summary>
    public const string DatabaseFileName = LocalDatabase.DatabaseFileName;

    /// <summary>
    /// 连接由 <see cref="LocalDatabase"/> 统一持有（V2.7）：本地曲库、缓存索引、补报队列
    /// 同处一个 yinyan-local.db，建表与连接管理收口在一个地方，避免多连接写同一文件的 SQLITE_BUSY
    /// 和"某张表忘了建"的故障。
    /// </summary>
    private readonly LocalDatabase _db;

    public LocalLibraryStore(LocalDatabase db) => _db = db;

    /// <summary>
    /// 曲库发生变更（新增/移除/清空）时触发。
    /// <para>存在的理由：<see cref="SongMenuHelper.ShowLocalMenuAsync"/> 里的「从本地曲库移除」
    /// 直接操作 Store，它拿不到页面的 ViewModel；没有这个事件的话，移除后列表不会刷新，
    /// 用户会以为没删掉（实测在真机上踩到）。</para>
    /// </summary>
    public event EventHandler? LibraryChanged;

    private void RaiseChanged() => LibraryChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>数据库文件的绝对路径。UI 上用来给用户看"曲库存在哪"。</summary>
    public static string DatabasePath => LocalDatabase.DatabasePath;

    /// <summary>内嵌封面抽出后的落盘目录。</summary>
    public static string CoverDirectory =>
        Path.Combine(FileSystem.AppDataDirectory, "local-covers");

    /// <summary>取共享连接（建表与初始化时序由 <see cref="LocalDatabase"/> 保证）。</summary>
    private Task<SQLiteAsyncConnection> GetConnectionAsync() => _db.GetConnectionAsync();

    /// <summary>当前库内曲目总数。</summary>
    public async Task<int> CountAsync()
    {
        var conn = await GetConnectionAsync();
        return await conn.Table<LocalSong>().CountAsync();
    }

    /// <summary>全部曲目，按"最近添加在前"排序（新扫到的文件排在前面，用户一眼能看到）。</summary>
    public async Task<List<LocalSong>> GetAllAsync()
    {
        var conn = await GetConnectionAsync();
        return await conn.Table<LocalSong>()
            .OrderByDescending(s => s.DateAddedUtc)
            .ThenBy(s => s.Title)
            .ToListAsync();
    }

    /// <summary>
    /// 幂等写入：<see cref="LocalSong.FilePath"/> 命中已有记录就跳过（不更新、不改 DateAddedUtc）。
    /// 这是 TC-2.6-02「重复扫描不产生重复记录」的实现点，靠唯一索引 + 先查后插双保险。
    /// </summary>
    /// <returns>true = 新入库；false = 已存在被跳过。</returns>
    public async Task<bool> AddIfAbsentAsync(LocalSong song)
    {
        var conn = await GetConnectionAsync();
        var exists = await conn.Table<LocalSong>()
            .Where(s => s.FilePath == song.FilePath)
            .FirstOrDefaultAsync();
        if (exists is not null) return false;

        await conn.InsertAsync(song);
        RaiseChanged();
        return true;
    }

    /// <summary>
    /// 插入或补齐已有记录。返回本次是新增还是命中已有。
    ///
    /// <para>为什么要 Upsert 而不是一直 AddIfAbsent：早期版本存在"封面抽取失效"的缺陷
    /// （Android 上 content:// 落地临时文件时拿不到扩展名，当时用的 TagLib 会整体解析失败），
    /// 那时入库的记录 <see cref="LocalSong.CoverPath"/> 是空的。用户重扫时应该把封面补上，
    /// 否则封面永远补不回来，只能清库重来。</para>
    ///
    /// <para>只补"元数据/封面"，**不动** <see cref="LocalSong.LocalPlayCount"/> 与
    /// <see cref="LocalSong.DateAddedUtc"/> —— 用户的播放统计不能被重扫抹掉。</para>
    /// </summary>
    public async Task<UpsertOutcome> UpsertAsync(LocalSong song)
    {
        var conn = await GetConnectionAsync();
        var exists = await conn.Table<LocalSong>()
            .Where(s => s.FilePath == song.FilePath)
            .FirstOrDefaultAsync();

        if (exists is null)
        {
            await conn.InsertAsync(song);
            RaiseChanged();
            return UpsertOutcome.Inserted;
        }

        var changed = false;

        // 补齐封面（这是重扫的主要目的）
        if (string.IsNullOrEmpty(exists.CoverPath) && !string.IsNullOrEmpty(song.CoverPath))
        {
            exists.CoverPath = song.CoverPath;
            changed = true;
        }

        // 补齐缺失的元数据；已有值不覆盖（避免把用户看惯的信息改掉）
        if (string.IsNullOrWhiteSpace(exists.ArtistName) && !string.IsNullOrWhiteSpace(song.ArtistName))
        {
            exists.ArtistName = song.ArtistName;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(exists.AlbumName) && !string.IsNullOrWhiteSpace(song.AlbumName))
        {
            exists.AlbumName = song.AlbumName;
            changed = true;
        }
        if (exists.DurationSeconds <= 0 && song.DurationSeconds > 0)
        {
            exists.DurationSeconds = song.DurationSeconds;
            changed = true;
        }

        if (changed)
        {
            await conn.UpdateAsync(exists);
            RaiseChanged();
        }

        return UpsertOutcome.Updated;
    }

    /// <summary>播放计数 +1，并刷新最后播放时间。**这是本地歌唯一的"上报"落点。**</summary>
    public async Task IncrementPlayCountAsync(int localId)
    {
        var conn = await GetConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE LocalSong SET LocalPlayCount = LocalPlayCount + 1, LastPlayedAtUtc = ? WHERE Id = ?",
            DateTime.UtcNow, localId);
    }

    /// <summary>按主键取一条（播放前校验文件是否还在）。</summary>
    public async Task<LocalSong?> GetAsync(int localId)
    {
        var conn = await GetConnectionAsync();
        return await conn.Table<LocalSong>().Where(s => s.Id == localId).FirstOrDefaultAsync();
    }

    /// <summary>
    /// 按文件路径（去重键）取一条。扫描时用来快速跳过已入库的文件——
    /// Android 侧尤其重要：命中就不必再把 <c>content://</c> 落地成临时文件去解析元数据。
    /// </summary>
    public async Task<LocalSong?> GetByFilePathAsync(string filePath)
    {
        var conn = await GetConnectionAsync();
        return await conn.Table<LocalSong>().Where(s => s.FilePath == filePath).FirstOrDefaultAsync();
    }

    /// <summary>移除一条记录（文件已删或用户手动移除）。连带删掉抽出的封面文件。</summary>
    public async Task RemoveAsync(int localId)
    {
        var conn = await GetConnectionAsync();
        var song = await conn.Table<LocalSong>().Where(s => s.Id == localId).FirstOrDefaultAsync();
        if (song is null) return;

        await conn.DeleteAsync(song);
        TryDeleteCover(song.CoverPath);
        RaiseChanged();
    }

    /// <summary>
    /// 清理所有文件已不存在的记录。扫描开始时调用一次，
    /// 避免"文件早就删了但列表里还留着"这种越积越多的脏数据。
    /// </summary>
    /// <returns>被清掉的条数。</returns>
    public async Task<int> PurgeMissingAsync()
    {
        var conn = await GetConnectionAsync();
        var all = await conn.Table<LocalSong>().ToListAsync();

        var removed = 0;
        foreach (var song in all)
        {
            if (LocalFileAccess.Exists(song.FilePath)) continue;
            await conn.DeleteAsync(song);
            TryDeleteCover(song.CoverPath);
            removed++;
        }
        if (removed > 0) RaiseChanged();
        return removed;
    }

    /// <summary>整体清空（设置页"清空本地曲库"用）。不动设备上的音乐文件。</summary>
    public async Task ClearAsync()
    {
        var conn = await GetConnectionAsync();
        var all = await conn.Table<LocalSong>().ToListAsync();
        await conn.DeleteAllAsync<LocalSong>();
        foreach (var song in all) TryDeleteCover(song.CoverPath);
        RaiseChanged();
    }

    private static void TryDeleteCover(string? coverPath)
    {
        if (string.IsNullOrWhiteSpace(coverPath)) return;
        try
        {
            if (File.Exists(coverPath)) File.Delete(coverPath);
        }
        catch
        {
            // 封面文件删不掉不影响主流程（记录已经从库里移除了）
        }
    }

    // ── 映射：LocalSong ⇄ SongDto ─────────────────────────────────────────────

    /// <summary>
    /// 转成 UI 通用的 <see cref="SongDto"/>，让本地歌能直接复用现有的列表模板、
    /// 播放页、迷你播放条与播放队列。
    ///
    /// <para><b>Id 取负数</b>：在线歌的 Id 是数据库自增正数，本地歌用 <c>-LocalSong.Id</c>，
    /// 两套 ID 空间天然不重叠，所有"按 Id 比较"的既有代码（IndexOfSong、菜单里的
    /// <c>player.Current?.Id == song.Id</c>）不用改就能正确区分。</para>
    ///
    /// <para><b>AudioUrl 放原始路径</b>：本地歌存盘符 / 裸绝对路径 / <c>content://</c>，
    /// 拼 BaseUrl 只会得到必然 404 的假地址。V2.13 之前靠 <see cref="ApiConfig.Absolute(string?)"/>
    /// 里的一张本地协议白名单逐个放行（<c>file:</c> / <c>content:</c>），
    /// 现在判据统一在 <c>Core/MediaAddress.IsLocalFile</c>（含裸绝对路径与盘符），
    /// 所以 PlayerService 的既有取值路径不需要为本地歌分叉。</para>
    /// </summary>
    public static SongDto ToDto(LocalSong s) => new()
    {
        Id = -s.Id,
        Title = string.IsNullOrWhiteSpace(s.Title) ? Path.GetFileName(s.FilePath) : s.Title,
        ArtistId = 0,                                   // 本地歌没有在线歌手，菜单据此隐藏"歌手/关注"
        ArtistName = s.ArtistName ?? string.Empty,
        AlbumId = null,
        AlbumName = s.AlbumName,
        CoverUrl = s.CoverPath,                         // 本地文件路径，AbsoluteUrlConverter 直接放行
        AudioUrl = s.FilePath,
        LyricUrl = null,
        DurationSeconds = s.DurationSeconds,
        PlayCount = s.LocalPlayCount,
        IsLiked = false,
        IsLocal = true,
    };

    /// <summary>本地歌的 SongDto.Id → LocalSong.Id（取绝对值还原）。</summary>
    public static int ToLocalId(long songDtoId) => (int)Math.Abs(songDtoId);
}
