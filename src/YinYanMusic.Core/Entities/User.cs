namespace YinYanMusic.Core.Entities;

public class User
{
    public long Id { get; set; }
    public string UserName { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string Gender { get; set; } = "保密";
    public DateOnly? Birthday { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Playlist> Playlists { get; set; } = [];
    public ICollection<LikedSong> LikedSongs { get; set; } = [];
    public ICollection<PlaylistCollection> PlaylistCollections { get; set; } = [];
    public ICollection<Follow> Following { get; set; } = [];
    public ICollection<Follow> Followers { get; set; } = [];
}