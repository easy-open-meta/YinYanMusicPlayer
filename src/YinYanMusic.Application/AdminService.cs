using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using YinYanMusic.Data;

namespace YinYanMusic.Application;

/// <summary>
/// 后台的维护类系统任务。
/// <para>
/// ⚠️ **"导入音乐"已不在这里**：它收口到了 <see cref="IDirectoryScanService"/>
/// （读 ATL 标签 + 校验时长 + 递归 + 按相对路径去重），与定时扫描同一条路径。
/// 历史教训：这里曾有一套"只按文件名拆「歌手 - 歌名」"的导入实现，同一个目录换个入口进来
/// 就会得到不同质量的记录（无专辑、无发行年份、时长恒为 0、坏文件照收），2026-09-23 已删除。
/// </para>
/// </summary>
public interface IAdminService
{
    Task<int> DeleteSeedSongsAsync();
    Task<(int Total, int Updated, string? Error)> ScanDurationsAsync();
}

public class AdminService(MusicDbContext db, IConfiguration config) : IAdminService
{
    public async Task<int> DeleteSeedSongsAsync()
    {
        var seedSongs = await db.Songs
            .Where(s => s.AudioUrl.StartsWith("/media/audio/sample_"))
            .ToListAsync();
        var count = seedSongs.Count;
        db.Songs.RemoveRange(seedSongs);
        await db.SaveChangesAsync();
        return count;
    }

    public async Task<(int Total, int Updated, string? Error)> ScanDurationsAsync()
    {
        var musicDir = config["Media:MusicDirectory"];
        if (string.IsNullOrWhiteSpace(musicDir) || !Directory.Exists(musicDir))
            return (0, 0, $"目录不存在: {musicDir}");

        var songs = await db.Songs.ToListAsync();
        var updated = 0;
        foreach (var song in songs)
        {
            var fileName = Uri.UnescapeDataString(song.AudioUrl.Replace("/media/audio/", ""));
            var filePath = Path.Combine(musicDir, fileName);
            if (!File.Exists(filePath)) continue;
            var duration = Mp3DurationReader.GetDuration(filePath);
            if (duration <= 0) continue;

            // 向下取整，与 Mp3DurationReader 的口径一致：这里曾经用 Math.Round，
            // 会让列表显示比播放页多 1 秒（历史上用估算式时长 + 四舍五入就是这么分叉的）。
            var seconds = (int)duration;
            if (song.DurationSeconds == seconds) continue;   // 没变就不算"更新"，也不产生 UPDATE

            song.DurationSeconds = seconds;
            updated++;
        }
        await db.SaveChangesAsync();
        return (songs.Count, updated, null);
    }
}
