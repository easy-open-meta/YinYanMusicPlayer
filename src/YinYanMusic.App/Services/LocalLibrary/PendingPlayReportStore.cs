namespace YinYanMusic.App.Services.LocalLibrary;

/// <summary>
/// 离线播放补报队列（V2.7 留痕，V2.11 消费）。
///
/// <para><b>谁能进队列</b>（口径已定，别再放宽）：只有「服务端曲库里有 songId」+
/// 「已缓存到本地」+「离线播放」三条同时满足的播放才入队。
/// 本地曲库的歌（<c>IsLocal</c>，没有 songId）永远不入队——它压根不属于服务端统计。
/// 在线播放走 <c>POST api/songs/{id}/play</c> 实时上报，也不入队。</para>
///
/// <para><b>账号维度</b>（V2.11，修复 3.11.6 已知问题）：每行记录入队时登录的 UserId，
/// flush 只提交当前账号的行——A 离线攒的播放不能记到后来登录的 B 名下（TC-2.11-11）。
/// 其它账号的行原样留在队列里，等那个账号再登录。</para>
/// </summary>
public sealed class PendingPlayReportStore
{
    /// <summary>队列长度上限。离线很久（比如出差两周）也不至于把库撑爆；超出丢最旧的。</summary>
    public const int MaxRows = 5000;

    private readonly LocalDatabase _db;

    public PendingPlayReportStore(LocalDatabase db) => _db = db;

    /// <summary>
    /// 记一条待补报的播放。幂等键是本次调用生成的 GUID（服务端按它去重），
    /// **不做时间桶去重**——同一分钟内切歌重播是两次真实播放，不该被合并。
    /// </summary>
    public async Task EnqueueAsync(long songId, long userId, int positionSeconds = 0, DateTime? playedAtUtc = null)
    {
        if (songId <= 0) return;   // 防御：负数/0 是本地歌的 Id 空间，不该走到这里

        try
        {
            var conn = await _db.GetConnectionAsync();
            var now = DateTime.UtcNow;

            await conn.InsertAsync(new PendingPlayReport
            {
                ClientReportKey = Guid.NewGuid().ToString("N"),
                SongId = songId,
                UserId = userId,
                PlayedAtUtc = playedAtUtc ?? now,
                PositionSeconds = Math.Max(0, positionSeconds),
                EnqueuedAtUtc = now,
            });

            await TrimAsync(conn);
        }
        catch (Exception ex)
        {
            // 留痕失败不能影响播放本身：补报是"锦上添花"，播放才是主线
            AppLog.Warn($"[PendingReport] 入队失败 songId={songId}: {ex.Message}");
        }
    }

    /// <summary>当前待补报条数（全账号合计）。缓存管理页展示它，用户能直观看到"离线听的已经记下来了"。</summary>
    public async Task<int> CountAsync()
    {
        try
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.Table<PendingPlayReport>().CountAsync();
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[PendingReport] 统计失败: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 取一批待上报的行（V2.11）。只取**属于当前账号**的行（其余账号的行留在队列里等它登录）；
    /// 老版本写入的 UserId=0 行无归属可查，一并按当前账号处理。
    /// 按 EnqueuedAtUtc 升序取——补报顺序与实际听歌顺序一致，服务端统计的时间线才自然。
    /// </summary>
    public async Task<List<PendingPlayReport>> TakeBatchAsync(long userId, int maxCount)
    {
        try
        {
            var conn = await _db.GetConnectionAsync();
            var rows = await conn.QueryAsync<PendingPlayReport>(
                "SELECT * FROM PendingPlayReport WHERE UserId = ? OR UserId = 0 " +
                "ORDER BY EnqueuedAtUtc ASC LIMIT ?", userId, maxCount);
            return rows.ToList();
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[PendingReport] 取批失败: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// 按幂等键删除已处置（成功 / 重复 / 歌已不存在）的行。三类结果都删、都不重试：
    /// duplicated 重交只会永远得到重复；unknown 的歌已从曲库消失（TC-2.11-03）。
    /// </summary>
    public async Task DeleteByKeysAsync(IEnumerable<string> clientKeys)
    {
        var keys = clientKeys as IReadOnlyList<string> ?? clientKeys.ToList();
        if (keys.Count == 0) return;

        try
        {
            var conn = await _db.GetConnectionAsync();
            // 参数上限保险：分片拼 IN 子句
            foreach (var chunk in keys.Chunk(200))
            {
                var placeholders = string.Join(',', chunk.Select(_ => "?"));
                await conn.ExecuteAsync(
                    $"DELETE FROM PendingPlayReport WHERE ClientReportKey IN ({placeholders})",
                    chunk.Cast<object>().ToArray());
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[PendingReport] 删除已处置行失败: {ex.Message}");
        }
    }

    /// <summary>超出上限时丢掉最旧的若干条（保留最近的 <see cref="MaxRows"/> 条）。</summary>
    private static async Task TrimAsync(SQLite.SQLiteAsyncConnection conn)
    {
        var count = await conn.Table<PendingPlayReport>().CountAsync();
        if (count <= MaxRows) return;

        await conn.ExecuteAsync(
            "DELETE FROM PendingPlayReport WHERE ClientReportKey IN (" +
            "  SELECT ClientReportKey FROM PendingPlayReport " +
            "  ORDER BY EnqueuedAtUtc DESC LIMIT -1 OFFSET ?)",
            MaxRows);

        AppLog.Warn($"[PendingReport] 队列超过 {MaxRows} 条，已丢弃最旧的记录");
    }
}
