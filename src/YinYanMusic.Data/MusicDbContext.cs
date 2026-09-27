using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core;
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
    public DbSet<EmailVerification> EmailVerifications => Set<EmailVerification>();
    public DbSet<SongArtist> SongArtists => Set<SongArtist>();
    public DbSet<DictType> DictTypes => Set<DictType>();
    public DbSet<DictItem> DictItems => Set<DictItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentLike> CommentLikes => Set<CommentLike>();
    public DbSet<PlaybackProgress> PlaybackProgress => Set<PlaybackProgress>();
    public DbSet<PlayReport> PlayReports => Set<PlayReport>();
    public DbSet<AlbumArtist> AlbumArtists => Set<AlbumArtist>();
    public DbSet<AdminRecommended> AdminRecommended => Set<AdminRecommended>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.UserName).HasMaxLength(32);
            e.Property(x => x.DisplayName).HasMaxLength(32);
            e.Property(x => x.Bio).HasMaxLength(200);
            e.Property(x => x.Gender).HasMaxLength(8);
            // 邮箱（V2.5）：唯一索引 + 过滤条件 —— 未绑定的用户 Email 是 null，
            // 不加过滤条件会因多个 null 而互相冲突（PostgreSQL 里 null 互不相等，
            // 实际不会冲突，但显式过滤语义更清楚，也避免其它库的差异）。
            e.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            e.Property(x => x.Email).HasMaxLength(256);
        });

        b.Entity<EmailVerification>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Code).HasMaxLength(8);
            // 按用户 + 签发时间查"最近 5 分钟发了几次"，这条索引覆盖该查询
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasOne(x => x.User).WithMany(u => u.EmailVerifications)
             .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Category>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(32);
            // 内容类型（both / songs / playlists）：默认值必须有 —— 迁移给**存量分区**补这一列时，
            // 非空表加 NOT NULL 列不给默认值会直接失败；有了它，老数据自动落 both（历史行为）。
            e.Property(x => x.ContentMode).HasMaxLength(16).HasDefaultValue(CategoryContentModes.Both);
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

        // 歌曲 ↔ 歌手（联合创作）。复合主键天然挡住重复挂载；再给 ArtistId 单列索引，
        // 因为"按歌手列歌 / 数歌"是高频查询（歌手页、歌手列表的歌曲数）。
        b.Entity<SongArtist>(e =>
        {
            e.HasKey(x => new { x.SongId, x.ArtistId });
            e.HasOne(x => x.Song).WithMany(x => x.SongArtists).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Artist).WithMany().OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ArtistId);
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

        // 数据字典（V2.16）：类型编码唯一；项按 (类型, 启用, 排序) 走覆盖索引。
        // DictItem.DictType 冗余存类型编码而不建外键：删类型时由服务层先删项，
        // 字典项本身与歌手表只是"文本约定"，没有硬引用。
        b.Entity<DictType>(e =>
        {
            e.HasIndex(x => x.Type).IsUnique();
            e.Property(x => x.Type).HasMaxLength(32);
            e.Property(x => x.Name).HasMaxLength(32);
            e.Property(x => x.IsEnabled).HasDefaultValue(true);
        });

        b.Entity<DictItem>(e =>
        {
            // 同一类型下选项文本唯一：既是业务约束（服务层也校验，给友好错误），
            // 也让种子 SQL 能用 ON CONFLICT ("DictType", "Label") DO NOTHING 幂等插入。
            e.HasIndex(x => new { x.DictType, x.Label }).IsUnique();
            e.HasIndex(x => new { x.DictType, x.IsEnabled, x.SortOrder });
            e.Property(x => x.DictType).HasMaxLength(32);
            e.Property(x => x.Label).HasMaxLength(32);
            e.Property(x => x.SortOrder).HasDefaultValue(0);
            e.Property(x => x.IsEnabled).HasDefaultValue(true);
        });

        // 评论（V2.9）。TargetType + TargetId 是多态挂载，**刻意不建外键**（见 CommentTargets 注释）：
        // 删歌曲/歌单时的评论清理由服务层负责。
        b.Entity<Comment>(e =>
        {
            e.Property(x => x.TargetType).HasMaxLength(16);
            // 与 CommentService 的 500 字上限一致：上限写在两处，但服务层才是给用户报错的那道
            e.Property(x => x.Content).HasMaxLength(500);
            // 列表查询的固定形态：按对象筛 + 按时间排
            e.HasIndex(x => new { x.TargetType, x.TargetId, x.CreatedAt });
            // "同一用户 10 秒内发过评论吗"的限流统计走这条
            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            // 取"这几条主评论各自的整棵子树"走这条：RootId 是物化好的所属主评论，不必递归查询。
            // 带 Id 是为了让 ORDER BY Id（父节点必先于子节点）也走索引。
            e.HasIndex(x => new { x.RootId, x.Id });
            e.HasOne(x => x.User).WithMany(u => u.Comments).OnDelete(DeleteBehavior.Cascade);
            // 自引用：删主评论时回复一并删（TC-2.9-06 要求"子回复一并处理"）
            e.HasOne(x => x.Parent).WithMany(x => x.Replies)
             .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CommentLike>(e =>
        {
            e.HasKey(x => new { x.CommentId, x.UserId });
            e.HasOne(x => x.Comment).WithMany(c => c.Likes).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        // 播放进度（V2.10 断点续播）：每用户每歌一行。
        b.Entity<PlaybackProgress>(e =>
        {
            e.HasKey(x => new { x.UserId, x.SongId });
            // 设备名长度上限（V2.12）。这里补声明是**对齐既有库结构**，不是新增约束：
            // 手写迁移 20260926140000_AddPlaybackProgressDevice 建的就是 varchar(64)，
            // 当时手写的快照里也记着 MaxLength(64)，唯独**模型配置漏写了这一句** ——
            // 于是之后每生成一次迁移，EF 都会按模型（无长度 = text）把这一列"改回 text"，
            // 附带一句"可能丢数据"的警告，而它跟本次要加的改动毫无关系。
            // 补上之后模型 = 快照 = 库，漂移消失（实体注释也写的"截断到 64 字符防脏数据撑爆列宽"）。
            e.Property(x => x.DeviceName).HasMaxLength(64);
            e.HasOne(x => x.User).WithMany().OnDelete(DeleteBehavior.Cascade);
            // 歌被删 → 该歌的进度一并清掉，否则"继续播放"会指向一首已经不在的歌（TC-2.10-08）
            e.HasOne(x => x.Song).WithMany().OnDelete(DeleteBehavior.Cascade);
            // "最近一条"查询：按用户筛 + 按时间倒序取 1
            e.HasIndex(x => new { x.UserId, x.UpdatedAtUtc });
        });

        // 专辑 ↔ 歌手（V2.12 联合创作专辑）。与 SongArtist 同构：复合主键天然挡重复挂载，
        // ArtistId 单列索引给"按歌手列专辑"。Album.ArtistId（单值列）保留，两处一致性由服务层保证。
        b.Entity<AlbumArtist>(e =>
        {
            e.HasKey(x => new { x.AlbumId, x.ArtistId });
            e.HasOne(x => x.Album).WithMany(x => x.AlbumArtists).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Artist).WithMany().OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ArtistId);
        });

        // 离线播放补报（V2.11）。幂等键 ClientReportKey 唯一 —— 冲突 = 服务端已收过（TC-2.11-02）。
        // User / Song 都是 Cascade：用户注销或歌被删时行随主表走，不会留孤儿行。
        // PlayedAtUtc 索引给"按时间统计"（将来播放历史）预留，现在只有写入。
        b.Entity<PlayReport>(e =>
        {
            e.HasKey(x => x.ClientReportKey);
            e.Property(x => x.ClientReportKey).HasMaxLength(64);
            e.HasOne(x => x.User).WithMany().OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Song).WithMany().OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.PlayedAtUtc });
        });

        // 后台人工干预推荐位（V2.12）。PlaylistId 既是主键又是外键 —— 见实体注释。
        // WithOne() 不给 Playlist 加反向导航：查询侧统一走显式左连接（RecommendService），
        // 不想让"有没有人工干预"这件事渗进 Playlist 这个被到处引用的实体。
        // Cascade：歌单被物理删除（后台强删/用户自删）时，干预行必须跟着走，否则留下孤儿行，
        // 下次同一个 Id 被新歌单复用时会出现"新歌单莫名被置顶"（TC-2.12-11）。
        b.Entity<AdminRecommended>(e =>
        {
            e.HasKey(x => x.PlaylistId);
            e.HasOne(x => x.Playlist).WithOne().HasForeignKey<AdminRecommended>(x => x.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // 站内通知（V2.15）。Notifications 本体无外键指向 User（CreatedById 只是审计字段，
        // 发送人被删也不该连带删掉通知）；NotificationRecipients 对 User Cascade，
        // 账号注销时接收行一并清掉。
        b.Entity<Notification>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(100);
            e.Property(x => x.Content).HasMaxLength(500);
            e.Property(x => x.TargetType).HasMaxLength(16);
            e.Property(x => x.Status).HasMaxLength(16).HasDefaultValue(NotificationStatuses.Sending);
            e.Property(x => x.RecipientUserIds).HasColumnType("text");
            // 后台已发送列表：按时间倒序一页页翻
            e.HasIndex(x => new { x.CreatedAtUtc, x.Id });
            // 状态筛选（发送中/已完成/失败）
            e.HasIndex(x => x.Status);
            e.HasMany(x => x.Recipients)
             .WithOne(r => r.Notification)
             .HasForeignKey(r => r.NotificationId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<NotificationRecipient>(e =>
        {
            // 同一通知对同一用户只一行（防重复推送/重复建行）
            e.HasIndex(x => new { x.NotificationId, x.UserId }).IsUnique();
            // 用户列表 / 未读数：按用户筛 + 未读过滤
            e.HasIndex(x => new { x.UserId, x.IsRead });
            e.HasOne(x => x.User).WithMany(u => u.NotificationRecipients).OnDelete(DeleteBehavior.Cascade);
        });
    }
}