using SQLite;

namespace YinYanMusic.App.Services.LocalLibrary;

/// <summary>
/// 客户端本地 SQLite 库的**唯一连接入口**（V2.7 从 <see cref="LocalLibraryStore"/> 里抽出）。
///
/// <para>为什么必须抽出：V2.6 只有本地曲库一张表，连接自然跟着 Store 走；V2.7 起
/// 缓存索引（<see cref="CachedSong"/>）与补报队列（<see cref="PendingPlayReport"/>）
/// 都要落在**同一个库文件**里（设计文档 §3.7：与本地曲库共用 yinyan-local.db），
/// 再各自 new 一个 <see cref="SQLiteAsyncConnection"/> 会带来两个问题：
/// 一是 WAL 下多连接写同一文件更容易 SQLITE_BUSY；二是「建表」这件事散在三处，
/// 迟早出现某张表没被创建的低级故障。所以连接与建表收口到这里，各 Store 只负责自己的语义。</para>
///
/// <para>库文件固定放 <see cref="FileSystem.AppDataDirectory"/>（设备私有目录，两端都有写权限，
/// 卸载重装以外的清理动作碰不到它）。</para>
/// </summary>
public sealed class LocalDatabase
{
    /// <summary>库文件名。刻意不带版本号——表结构演进走 sqlite-net 的 CreateTable 增量加列。</summary>
    public const string DatabaseFileName = "yinyan-local.db";

    private readonly SemaphoreSlim _initGate = new(1, 1);
    private SQLiteAsyncConnection? _conn;

    /// <summary>数据库文件的绝对路径。UI 上用来给用户看"数据存在哪"。</summary>
    public static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);

    /// <summary>
    /// 建连接 + 建齐所有表。可重入：第一次真正干活，后续直接返回已有连接。
    /// 用 SemaphoreSlim 而不是 <c>Lazy&lt;Task&gt;</c>，因为要保证"连接创建失败后还能重试"
    /// （Lazy 缓存异常后会把失败永久固化）。
    /// </summary>
    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_conn is not null) return _conn;

        await _initGate.WaitAsync();
        try
        {
            if (_conn is not null) return _conn;

            Directory.CreateDirectory(FileSystem.AppDataDirectory);
            var conn = new SQLiteAsyncConnection(
                DatabasePath,
                // 读写并发：扫描/下载在后台线程写，UI 线程同时读列表
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            // CreateTable 是幂等的（表在就补缺列），所有表在这里一次建齐。
            await conn.CreateTableAsync<LocalSong>();
            await conn.CreateTableAsync<CachedSong>();
            await conn.CreateTableAsync<PendingPlayReport>();
            await conn.CreateTableAsync<CacheHistoryEntry>();

            _conn = conn;
            return conn;
        }
        finally
        {
            _initGate.Release();
        }
    }
}
