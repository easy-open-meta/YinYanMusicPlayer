namespace YinYanMusic.Core.Entities;

/// <summary>
/// 站内通知本身（V2.15）。一条通知 × 多个接收人；已读状态在 <see cref="NotificationRecipient"/>。
/// </summary>
public class Notification
{
    public long Id { get; set; }

    /// <summary>标题。1–100 字，由服务层校验。</summary>
    public string Title { get; set; } = default!;

    /// <summary>正文。纯文本，1–500 字。</summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 目标范围：<c>all</c> / <c>partial</c> / <c>single</c>，取值见 <see cref="NotificationTargets"/>。
    /// </summary>
    public string TargetType { get; set; } = default!;

    /// <summary>
    /// 部分/单个推送时的目标用户 Id 列表，JSON 数组（如 <c>"[1,2,3]"</c>）。
    /// 全部用户时为 null —— 接收行在发送时按当时用户列表物化到 <see cref="NotificationRecipients"/>。
    /// </summary>
    public string? RecipientUserIds { get; set; }

    /// <summary>可选关联歌曲 Id（点击跳转播放页）。无外键：歌删了通知仍在，跳转时再查。</summary>
    public long? RelatedSongId { get; set; }

    /// <summary>可选关联歌单 Id（点击跳转歌单页）。</summary>
    public long? RelatedPlaylistId { get; set; }

    /// <summary>可选关联专辑 Id（点击播放整张专辑）。无外键，同 <see cref="RelatedSongId"/>。</summary>
    public long? RelatedAlbumId { get; set; }

    /// <summary>发送状态：<c>sending</c> / <c>completed</c> / <c>failed</c>，取值见 <see cref="NotificationStatuses"/>。</summary>
    public string Status { get; set; } = default!;

    /// <summary>发送人（管理员）用户 Id。</summary>
    public long CreatedById { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>完成发送（接收行全部写入）的时间；失败时也可保留最后一次尝试时间。</summary>
    public DateTime? SentAtUtc { get; set; }

    public ICollection<NotificationRecipient> Recipients { get; set; } = [];
}

/// <summary>
/// 通知 × 用户 的接收与已读状态（V2.15）。唯一索引 <c>(NotificationId, UserId)</c>。
/// </summary>
public class NotificationRecipient
{
    public long Id { get; set; }

    public long NotificationId { get; set; }
    public Notification Notification { get; set; } = default!;

    public long UserId { get; set; }
    public User User { get; set; } = default!;

    public bool IsRead { get; set; }

    public DateTime? ReadAtUtc { get; set; }
}

/// <summary>通知目标范围取值（与 <see cref="Notification.TargetType"/> 对应）。</summary>
public static class NotificationTargets
{
    public const string All = "all";
    public const string Partial = "partial";
    public const string Single = "single";

    public static bool IsValid(string? targetType) =>
        targetType is All or Partial or Single;
}

/// <summary>通知发送状态取值。</summary>
public static class NotificationStatuses
{
    public const string Sending = "sending";
    public const string Completed = "completed";
    public const string Failed = "failed";

    public static bool IsValid(string? status) =>
        status is Sending or Completed or Failed;
}
