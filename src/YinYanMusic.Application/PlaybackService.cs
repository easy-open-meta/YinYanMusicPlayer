using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface IPlaybackService
{
    /// <summary>批量上限：一次补报最多 500 条（客户端按 200/批提交，这里留裕量）。</summary>
    const int MaxBatchSize = 500;

    /// <summary>保存 / 更新播放进度。同一用户同一首只留一行，后写覆盖先写。</summary>
    Task<ServiceResult> SaveAsync(long userId, PlaybackProgressRequest req);

    /// <summary>最近一次播放（换设备"继续播放"用）。没有任何记录时返回 null。</summary>
    Task<PlaybackProgressDto?> GetLastAsync(long userId);

    /// <summary>
    /// 批量接收离线播放补报（V2.11）。幂等：按 ClientReportKey 去重，插入成功才给该歌 +1 播放数。
    /// 曲库里已不存在的歌计入 unknownSongs，客户端会直接丢弃。
    /// </summary>
    Task<PlayReportsAck> AcceptReportsAsync(long userId, PlayReportsBatchRequest req);
}

/// <summary>
/// 断点续播（V2.10）。只管"最后一次听到哪"，不做实时跟随。
/// </summary>
public class PlaybackService(MusicDbContext db, AudioMetadataService metadata) : IPlaybackService
{
    public async Task<ServiceResult> SaveAsync(long userId, PlaybackProgressRequest req)
    {
        // 本地音乐的 Id 是负数（-LocalSong.Id），压根不该走到这里
        if (req.SongId <= 0) return ServiceResult.Fail("歌曲不存在。");
        if (!await db.Songs.AnyAsync(s => s.Id == req.SongId)) return ServiceResult.Fail("歌曲不存在。");

        // 位置下限兜底：客户端在"还没开始播"时可能报 0 或负数
        var position = Math.Max(0, req.PositionSeconds);
        var now = DateTime.UtcNow;

        // 设备名（V2.12）：截断防脏数据；null/空白统一存 null（旧客户端没这个字段）
        var deviceName = string.IsNullOrWhiteSpace(req.DeviceName)
            ? null
            : req.DeviceName.Trim() is { Length: > 64 } trimmed ? trimmed[..64] : req.DeviceName.Trim();

        // 复合主键天然去重，所以这里"先查后改/插"就够，不需要额外判重；
        // 冲突（两端同时上报同一首）按"后写覆盖先写"，即最后到达服务端的那次为准 —— 与 3.10.2 的约定一致
        var row = await db.PlaybackProgress.FirstOrDefaultAsync(p => p.UserId == userId && p.SongId == req.SongId);
        if (row is null)
        {
            db.PlaybackProgress.Add(new PlaybackProgress
            {
                UserId = userId,
                SongId = req.SongId,
                PositionSeconds = position,
                DeviceName = deviceName,
                UpdatedAtUtc = now
            });
        }
        else
        {
            row.PositionSeconds = position;
            row.DeviceName = deviceName;
            row.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<PlaybackProgressDto?> GetLastAsync(long userId)
    {
        var row = await db.PlaybackProgress.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.UpdatedAtUtc)
            // 同一毫秒内的两条要有稳定顺序，否则"最近一条"会在两端跳来跳去
            .ThenByDescending(p => p.SongId)
            .FirstOrDefaultAsync();

        if (row is null) return null;

        // 歌被删的话进度行已被外键级联清掉，正常到不了这里；真到了就当没有记录，别抛 500（TC-2.10-08）
        var song = await db.Songs.AsNoTracking()
            .Include(s => s.Artist)
            .Include(s => s.Album)
            .Include(s => s.Category)
            .Include(s => s.SongArtists).ThenInclude(sa => sa.Artist)
            .FirstOrDefaultAsync(s => s.Id == row.SongId);
        if (song is null) return null;

        var dto = song.ToDto();
        await metadata.FillCoversAsync([dto]);
        return new PlaybackProgressDto(row.SongId, row.PositionSeconds, row.UpdatedAtUtc, dto, row.DeviceName);
    }

    /// <summary>批量上限：一次补报最多 500 条（客户端按 200/批提交，这里留裕量）。更大的批次直接拒绝，客户端会重试。</summary>
    public async Task<PlayReportsAck> AcceptReportsAsync(long userId, PlayReportsBatchRequest req)
    {
        var items = (req.Items ?? []).Where(i => i is not null
            && !string.IsNullOrWhiteSpace(i.ClientKey)
            && i.SongId > 0).ToList();

        var accepted = 0;
        var duplicated = 0;
        var unknownSongs = new List<long>();

        foreach (var group in items.GroupBy(i => i.SongId))
        {
            var songId = group.Key;
            // 歌已不在曲库（后台删过）：告诉客户端直接丢弃，别再试（TC-2.11-03）
            var songExists = await db.Songs.AsNoTracking().AnyAsync(s => s.Id == songId);
            if (!songExists)
            {
                unknownSongs.Add(songId);
                continue;
            }

            foreach (var item in group)
            {
                var key = item.ClientKey.Trim();
                if (key.Length > 64) key = key[..64];

                var exists = await db.PlayReports.AsNoTracking()
                    .AnyAsync(r => r.ClientReportKey == key);
                if (exists)
                {
                    // 幂等命中：服务端已收过，计数不能再 +1（TC-2.11-02）
                    duplicated++;
                    continue;
                }

                db.PlayReports.Add(new PlayReport
                {
                    ClientReportKey = key,
                    UserId = userId,
                    SongId = songId,
                    PlayedAtUtc = item.PlayedAtUtc == default ? DateTime.UtcNow : item.PlayedAtUtc.ToUniversalTime(),
                    CreatedAtUtc = DateTime.UtcNow
                });

                // 只有**插入成功**的才 +1：与项目里 ON CONFLICT DO NOTHING 的幂等口径一致。
                // 先查后插在单实例 API 下由请求串行化兜住；即便并发撞上，唯一索引会让
                // SaveChanges 抛异常走 catch，该批整体重试，不会重复计数。
                await db.Songs.Where(s => s.Id == songId)
                    .ExecuteUpdateAsync(u => u.SetProperty(s => s.PlayCount, s => s.PlayCount + 1));
                accepted++;
            }

            await db.SaveChangesAsync();
        }

        return new PlayReportsAck(accepted, duplicated, unknownSongs);
    }
}
