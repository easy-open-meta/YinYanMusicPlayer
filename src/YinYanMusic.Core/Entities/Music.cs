namespace YinYanMusic.Core.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>
    /// 分区展示字段（App 的“音乐专区”）：宣传语、卡片底色（#RRGGBB）、MaterialIcons 图标字符。
    /// 均可空；纯数据驱动，加分区/改样式只动表数据，不改代码。
    /// </summary>
    public string? Slogan { get; set; }
    public string? ColorHex { get; set; }
    public string? IconGlyph { get; set; }

    public ICollection<Playlist> Playlists { get; set; } = [];
    public ICollection<Song> Songs { get; set; } = [];
}

public class Artist
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Region { get; set; }
    public string? Kind { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Album> Albums { get; set; } = [];
    public ICollection<Song> Songs { get; set; } = [];
}

public class Album
{
    public long Id { get; set; }
    public long ArtistId { get; set; }
    public string Name { get; set; } = default!;
    public DateOnly? ReleaseDate { get; set; }
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Artist Artist { get; set; } = default!;
    public ICollection<Song> Songs { get; set; } = [];
}

public class Song
{
    public long Id { get; set; }
    public string Title { get; set; } = default!;
    public long ArtistId { get; set; }
    public long? AlbumId { get; set; }
    public int? CategoryId { get; set; }
    public string AudioUrl { get; set; } = default!;
    public string? LyricUrl { get; set; }
    public string? CoverUrl { get; set; }
    public int DurationSeconds { get; set; }
    public long PlayCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Artist Artist { get; set; } = default!;
    public Album? Album { get; set; }
    public Category? Category { get; set; }
    public ICollection<PlaylistSong> PlaylistSongs { get; set; } = [];
    public ICollection<LikedSong> LikedBy { get; set; } = [];
}