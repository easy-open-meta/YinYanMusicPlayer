using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;

namespace YinYanMusic.Core;

public static class MappingExtensions
{
    public static CategoryDto ToDto(this Category c)
        => new(c.Id, c.Name, c.Slogan, c.ColorHex, c.IconGlyph);

    public static ArtistDto ToDto(this Artist a, long followerCount = 0, long songCount = 0, long albumCount = 0)
        => new(a.Id, a.Name, a.Region, a.Kind, a.AvatarUrl, a.Bio, followerCount, songCount, albumCount);

    public static AlbumDto ToDto(this Album a) =>
        new(a.Id, a.Name, a.CoverUrl, a.ReleaseDate, a.Description, a.Artist is null ? null : a.Artist.ToDto());

    public static SongDto ToDto(this Song s) => new()
    {
        Id = s.Id,
        Title = s.Title,
        ArtistId = s.ArtistId,
        ArtistName = s.Artist?.Name ?? string.Empty,
        AlbumId = s.AlbumId,
        AlbumName = s.Album?.Name,
        CoverUrl = s.CoverUrl ?? s.Album?.CoverUrl,
        AudioUrl = s.AudioUrl,
        LyricUrl = s.LyricUrl,
        DurationSeconds = s.DurationSeconds,
        PlayCount = s.PlayCount
    };
}