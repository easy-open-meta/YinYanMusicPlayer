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

    /// <summary>
    /// 专区内容类型：<c>both</c>（歌曲+歌单，默认）/ <c>songs</c>（仅歌曲）/ <c>playlists</c>（仅歌单）。
    /// 取值见 <see cref="CategoryContentModes"/>，用来做"纯歌曲专区"这类只放一种内容的专区。
    /// </summary>
    public string ContentMode { get; set; } = CategoryContentModes.Both;

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

    /// <summary>
    /// 全部歌手（V2.12 联合创作专辑）。首位（<see cref="AlbumArtist.Position"/> = 0）即 <see cref="Artist"/>，
    /// 由服务层保证两处一致；没加载关联表时可能为空集合。
    /// </summary>
    public ICollection<AlbumArtist> AlbumArtists { get; set; } = [];
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

    /// <summary>
    /// 主歌手。**保留**（不是唯一歌手了）—— 列表显示、专辑归属、播放页等仍以它为准；
    /// 完整归属看 <see cref="SongArtists"/>。改这里必须同步关联表，见 SongService.SyncPrimaryArtist。
    /// </summary>
    public Artist Artist { get; set; } = default!;

    /// <summary>
    /// 全部歌手（含主歌手，主歌手 <see cref="SongArtist.Position"/> = 0）。
    /// 一首歌可以是联合创作，同时出现在每位歌手的页面上。
    /// </summary>
    public ICollection<SongArtist> SongArtists { get; set; } = [];

    public Album? Album { get; set; }
    public Category? Category { get; set; }
    public ICollection<PlaylistSong> PlaylistSongs { get; set; } = [];
    public ICollection<LikedSong> LikedBy { get; set; } = [];
}

/// <summary>
/// 歌曲 ↔ 歌手 的关联（联合创作）。一首歌可以挂多个歌手；主歌手那条 <see cref="Position"/> = 0，
/// 与 <see cref="Song.ArtistId"/> 始终指向同一个人。
/// <para>
/// 为什么要这张表：标签里的歌手字段本来是**多值**（ATL 用 <c>;</c> 连接，实测中文标签里也有用 <c>、</c> 的），
/// 早先只取第一个存进 <see cref="Song.ArtistId"/>，于是"联合创作"的歌只在第一位歌手名下出现。
/// </para>
/// </summary>
public class SongArtist
{
    public long SongId { get; set; }
    public Song Song { get; set; } = default!;

    public long ArtistId { get; set; }
    public Artist Artist { get; set; } = default!;

    /// <summary>排序位置：0 = 主歌手（与 <see cref="Song.ArtistId"/> 一致），其余按标签里的先后顺序。</summary>
    public int Position { get; set; }
}