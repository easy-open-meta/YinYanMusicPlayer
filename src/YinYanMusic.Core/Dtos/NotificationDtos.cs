using System.Text.Json.Serialization;

namespace YinYanMusic.Core.Dtos;

/// <summary>
/// 一条站内通知（用户侧列表项）。未读在前由服务端排序。
/// </summary>
public record NotificationDto(
    long Id,
    string Title,
    string Content,
    bool IsRead,
    long? RelatedSongId,
    long? RelatedPlaylistId,
    DateTime CreatedAt,
    DateTime? ReadAtUtc = null,
    long? RelatedAlbumId = null)
{
    /// <summary>
    /// 是否带可跳转的关联对象（决定列表行末尾要不要显示 ">" 箭头）。
    /// <para>
    /// <b>不参与序列化</b>：这是"图标显不显示"的观感判断，不该固化进接口契约 ——
    /// 线上传的仍然是三个可空 Id（哪个非空由客户端自己判）。与
    /// <c>SongDto.ArtistsDisplay</c> 同一做法。
    /// </para>
    /// </summary>
    [JsonIgnore]
    public bool HasRelatedTarget =>
        RelatedSongId is > 0 || RelatedPlaylistId is > 0 || RelatedAlbumId is > 0;
}

/// <summary>
/// 未读数（角标）。<see cref="UnreadCount"/> 供轮询/初始值；SignalR 上另有 <c>UnreadCountChanged</c> 事件。
/// </summary>
public record UnreadCountDto(int UnreadCount);

/// <summary>
/// 后台发送通知。
/// <para>
/// <paramref name="TargetType"/> 取 <c>all</c> / <c>partial</c> / <c>single</c>；
/// partial/single 时 <paramref name="RecipientUserIds"/> 必填且非空。
/// </para>
/// </summary>
public record SendNotificationRequest(
    string Title,
    string Content,
    string TargetType,
    IReadOnlyList<long>? RecipientUserIds = null,
    long? RelatedSongId = null,
    long? RelatedPlaylistId = null,
    long? RelatedAlbumId = null);

/// <summary>
/// 后台已发送列表项：发送时间、目标范围、接收人数、状态。
/// </summary>
public record AdminNotificationDto(
    long Id,
    string Title,
    string Content,
    string TargetType,
    IReadOnlyList<long> RecipientUserIds,
    long? RelatedSongId,
    long? RelatedPlaylistId,
    long? RelatedAlbumId,
    string Status,
    long CreatedById,
    string CreatedByName,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    int RecipientCount,
    int ReadCount);
