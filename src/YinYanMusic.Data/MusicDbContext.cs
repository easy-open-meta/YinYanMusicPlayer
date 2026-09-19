using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core.Entities;

namespace YinYanMusic.Data;

public class MusicDbContext(DbContextOptions<MusicDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Song> Songs => Set<Song>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistSong> PlaylistSongs => Set<PlaylistSong>();
    public DbSet<LikedSong> LikedSongs => Set<LikedSong>();
    public DbSet<PlaylistCollection> PlaylistCollections => Set<PlaylistCollection>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<ArtistFollow> ArtistFollows => Set<ArtistFollow>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.UserName).HasMaxLength(32);
            e.Property(x => x.DisplayName).HasMaxLength(32);
            e.Property(x => x.Bio).HasMaxLength(200);
            e.Property(x => x.Gender).HasMaxLength(8);
        });

        b.Entity<Category>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(32);
        });

        b.Entity<Artist>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(64);
            e.Property(x => x.Region).HasMaxLength(16);
            e.Property(x => x.Kind).HasMaxLength(16);
            e.Property(x => x.Bio).HasMaxLength(500);
        });

        b.Entity<Album>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.HasOne(x => x.Artist).WithMany(x => x.Albums).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Song>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(128);
            e.Property(x => x.AudioUrl).HasMaxLength(2000);
            e.Property(x => x.LyricUrl).HasMaxLength(500);
            e.HasOne(x => x.Artist).WithMany(x => x.Songs).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Album).WithMany(x => x.Songs).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Category).WithMany(x => x.Songs).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Title);
        });

        b.Entity<Playlist>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasOne(x => x.Owner).WithMany(x => x.Playlists).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Category).WithMany(x => x.Playlists).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<PlaylistSong>(e =>
        {
            e.HasKey(x => new { x.PlaylistId, x.SongId });
            e.HasOne(x => x.Playlist).WithMany(x => x.Songs).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Song).WithMany(x => x.PlaylistSongs).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LikedSong>(e =>
        {
            e.HasKey(x => new { x.UserId, x.SongId });
            e.HasOne(x => x.User).WithMany(x => x.LikedSongs).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Song).WithMany(x => x.LikedBy).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PlaylistCollection>(e =>
        {
            e.HasKey(x => new { x.UserId, x.PlaylistId });
            e.HasOne(x => x.User).WithMany(x => x.PlaylistCollections).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Playlist).WithMany(x => x.CollectedBy).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Follow>(e =>
        {
            e.HasKey(x => new { x.FollowerId, x.FolloweeId });
            e.HasOne(x => x.Follower).WithMany(x => x.Following).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Followee).WithMany(x => x.Followers).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ArtistFollow>(e =>
        {
            e.HasKey(x => new { x.UserId, x.ArtistId });
            e.HasOne(x => x.User).WithMany().OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Artist).WithMany().OnDelete(DeleteBehavior.Cascade);
        });
    }
}