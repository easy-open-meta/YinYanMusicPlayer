namespace YinYanMusic.Core.Entities;

public class Playlist
{
    public long Id { get; set; }
    public long OwnerId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public int? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 系统自动创建的歌单（如注册时生成的“我喜欢的音乐”），个人可见，
    /// 不出现在公开的精选/搜索歌单列表里。按数据标志判断，而不是按歌单名硬编码。
    /// </summary>
    public bool IsSystem { get; set; }

    public User Owner { get; set; } = default!;
    public Category? Category { get; set; }
    public ICollection<PlaylistSong> Songs { get; set; } = [];
    public ICollection<PlaylistCollection> CollectedBy { get; set; } = [];
}

public class PlaylistSong
{
    public long PlaylistId { get; set; }
    public long SongId { get; set; }
    public int Position { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Playlist Playlist { get; set; } = default!;
    public Song Song { get; set; } = default!;
}

public class LikedSong
{
    public long UserId { get; set; }
    public long SongId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = default!;
    public Song Song { get; set; } = default!;
}

public class PlaylistCollection
{
    public long UserId { get; set; }
    public long PlaylistId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = default!;
    public Playlist Playlist { get; set; } = default!;
}

public class Follow
{
    public long FollowerId { get; set; }
    public long FolloweeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User Follower { get; set; } = default!;
    public User Followee { get; set; } = default!;
}

/// <summary>
/// 用户关注的歌手。注意与 <see cref="Follow"/> 区分：那个是"用户关注用户"（两个外键都指向 Users），
/// 这个才是"关注歌手"，所以单独建表，避免把歌手 Id 塞进用户外键里。
/// </summary>
public class ArtistFollow
{
    public long UserId { get; set; }
    public long ArtistId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = default!;
    public Artist Artist { get; set; } = default!;
}