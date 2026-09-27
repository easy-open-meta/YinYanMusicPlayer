using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;

namespace YinYanMusic.Core;

public static class MappingExtensions
{
    public static CategoryDto ToDto(this Category c)
        => new(c.Id, c.Name, c.Slogan, c.ColorHex, c.IconGlyph, c.ContentMode);

    public static ArtistDto ToDto(this Artist a, long followerCount = 0, long songCount = 0, long albumCount = 0)
        => new(a.Id, a.Name, a.Region, a.Kind, a.AvatarUrl, a.Bio, followerCount, songCount, albumCount);

    /// <summary>
    /// 专辑 → DTO。<paramref name="trackCount"/> 由调用方按需查好传进来（读接口传真实数量，
    /// 创建/更新响应传默认的 0）—— 映射器里不查库，避免隐藏的 N+1。
    /// </summary>
    public static AlbumDto ToDto(this Album a, int trackCount = 0) =>
        new(a.Id, a.Name, a.CoverUrl, a.ReleaseDate, a.Description, a.Artist is null ? null : a.Artist.ToDto(), trackCount);

    public static SongDto ToDto(this Song s) => new()
    {
        Id = s.Id,
        Title = s.Title,
        ArtistId = s.ArtistId,
        ArtistName = s.Artist?.Name ?? string.Empty,
        // 关联表没 Include 时（如歌单详情）回退成只有主歌手的单元素列表，别让调用方拿到 null 就少一段信息。
        // ⚠️ Count > 0 不代表 Artist 已加载：SongService.UpdateAsync 换主歌手时，SyncPrimaryArtistLinkAsync
        //    把关联行查进追踪上下文，EF 关系修正会自动把它们挂进 s.SongArtists（Artist 导航是空的，
        //    只与新主歌手同 Id 的行会被 Reference.LoadAsync 补上）——所以 Select 里必须判空，
        //    否则"编辑歌曲 + 换主歌手 + 有合作歌手"必炸 NullReferenceException（线上实测）。
        Artists = s.SongArtists.Count > 0
            ? s.SongArtists.OrderBy(sa => sa.Position)
                .Select(sa => new SongArtistRef(sa.ArtistId, sa.Artist?.Name ?? string.Empty)).ToList()
            : (s.Artist?.Name is { Length: > 0 } primary ? [new SongArtistRef(s.ArtistId, primary)] : null),
        AlbumId = s.AlbumId,
        AlbumName = s.Album?.Name,
        CategoryId = s.CategoryId,
        CategoryName = s.Category?.Name,
        CoverUrl = s.CoverUrl ?? s.Album?.CoverUrl,
        AudioUrl = s.AudioUrl,
        LyricUrl = s.LyricUrl,
        DurationSeconds = s.DurationSeconds,
        PlayCount = s.PlayCount
    };
}