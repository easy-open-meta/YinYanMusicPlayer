namespace YinYanMusic.App.Services.LocalLibrary;

/// <summary>
/// 缓存历史（V2.7 增强）：记录"缓存这件事发生过什么"，供缓存管理页的「缓存历史」标签页展示。
///
/// <para><b>保留上限</b>：只留最近 <see cref="MaxRows"/> 条（超出丢最旧的）。
/// 它是给人看的日志，不是审计账本，没必要无限增长。</para>
/// </summary>
public sealed class CacheHistoryStore(LocalDatabase db)
{
    /// <summary>历史条数上限。</summary>
    public const int MaxRows = 500;

    /// <summary>历史有变化（新增/清空）时触发，UI 据此刷新。</summary>
    public event EventHandler? HistoryChanged;

    /// <summary>记一条历史。<b>绝不抛异常</b>：历史是附属信息，不能因为它失败影响缓存主流程。</summary>
    public async Task RecordAsync(CacheHistoryEntry entry)
    {
        try
        {
            var conn = await db.GetConnectionAsync();
            if (entry.AtUtc == default) entry.AtUtc = DateTime.UtcNow;
            await conn.InsertAsync(entry);
            await TrimAsync(conn);
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[CacheHistory] 记录失败：{ex.Message}");
        }
    }

    /// <summary>最近的历史（新的在前）。</summary>
    public async Task<List<CacheHistoryEntry>> GetRecentAsync(int limit = 200)
    {
        try
        {
            var conn = await db.GetConnectionAsync();
            return await conn.Table<CacheHistoryEntry>()
                .OrderByDescending(e => e.AtUtc)
                .Take(Math.Max(1, limit))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[CacheHistory] 读取失败：{ex.Message}");
            return [];
        }
    }

    /// <summary>条数（缓存管理页标签上显示）。</summary>
    public async Task<int> CountAsync()
    {
        try
        {
            var conn = await db.GetConnectionAsync();
            return await conn.Table<CacheHistoryEntry>().CountAsync();
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>清空历史（用户手动清，与"清空缓存"分开：清缓存不该顺手把历史也抹了）。</summary>
    public async Task ClearAsync()
    {
        try
        {
            var conn = await db.GetConnectionAsync();
            await conn.DeleteAllAsync<CacheHistoryEntry>();
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[CacheHistory] 清空失败：{ex.Message}");
        }
    }

    /// <summary>超出上限时丢掉最旧的。</summary>
    private static async Task TrimAsync(SQLite.SQLiteAsyncConnection conn)
    {
        var count = await conn.Table<CacheHistoryEntry>().CountAsync();
        if (count <= MaxRows) return;

        await conn.ExecuteAsync(
            "DELETE FROM CacheHistory WHERE Id IN (" +
            "  SELECT Id FROM CacheHistory ORDER BY AtUtc DESC LIMIT -1 OFFSET ?)",
            MaxRows);
    }
}
