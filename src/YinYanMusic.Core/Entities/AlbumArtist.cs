namespace YinYanMusic.Core.Entities;

/// <summary>
/// 专辑 ↔ 歌手 的关联（V2.12 联合创作专辑）。一张专辑可以由多位歌手共同发行，
/// 出现在每位歌手的专辑列表里。与 <see cref="SongArtist"/> 完全同构：Position 0 = 主歌手。
///
/// <para>⚠️ <see cref="Album.ArtistId"/>（单值列）**保留**且始终等于 Position=0 那一行的 ArtistId：
/// 歌手页"专辑数"统计、目录扫描的归属判断、歌手详情页过滤等读路径都走它，改掉代价远大于收益。
/// 写入端（CatalogService）负责两处同步，不依赖数据库触发器。</para>
/// </summary>
public class AlbumArtist
{
    public long AlbumId { get; set; }
    public Album Album { get; set; } = default!;

    public long ArtistId { get; set; }
    public Artist Artist { get; set; } = default!;

    /// <summary>排序位置：0 = 主歌手（与 <see cref="Album.ArtistId"/> 一致），其余按贡献顺序。</summary>
    public int Position { get; set; }
}
