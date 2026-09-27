using SQLite;

namespace YinYanMusic.App.Services.LocalLibrary;

/// <summary>
/// 本地音乐库的一条记录（V2.6）。对应设备上真实存在的一个音频文件。
///
/// 与在线 <c>Song</c> 的本质区别：**没有 songId**，服务端完全不知道它的存在。
/// 因此它的播放计数只写在本表的 <see cref="LocalPlayCount"/> 上，
/// 永远不会走 <c>POST /api/songs/{id}/play</c>。
/// </summary>
[Table("LocalSong")]
public class LocalSong
{
    /// <summary>本地自增主键。对外暴露成 <c>SongDto.Id = -Id</c>（负数），与在线正数 ID 天然隔离。</summary>
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    /// <summary>
    /// 去重键：同一个文件重复扫描不入库。
    /// Windows 是绝对路径；Android 是 <c>content://media/external/audio/media/{id}</c>
    /// （MediaStore 的稳定 URI，比 <c>_data</c> 路径更可靠，分区存储下也能直接播放）。
    /// </summary>
    [Indexed(Name = "IX_LocalSong_FilePath", Order = 1, Unique = true)]
    public string FilePath { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? ArtistName { get; set; }

    public string? AlbumName { get; set; }

    /// <summary>时长（秒）。取自 ATL 逐帧解析（向下取整），与服务端 Mp3DurationReader 同一口径。</summary>
    public int DurationSeconds { get; set; }

    /// <summary>
    /// 内嵌封面抽出后落盘的**本地文件绝对路径**（不是 URL）。
    /// 空表示该文件没有内嵌封面，UI 回退到占位图标。
    /// </summary>
    public string? CoverPath { get; set; }

    public DateTime DateAddedUtc { get; set; }

    public DateTime? LastPlayedAtUtc { get; set; }

    /// <summary>本地播放次数。**只记本地，永不上报**（TC-2.6-07）。</summary>
    public int LocalPlayCount { get; set; }

    // ── 非持久化辅助字段（sqlite-net 会忽略没有 getter/setter 映射的只读属性） ──

    /// <summary>播放时发现文件已不存在 → 置位，UI 上给出"移除"提示。</summary>
    [Ignore]
    public bool IsMissing { get; set; }
}
