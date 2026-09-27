using System.ComponentModel;
using System.Text.Json.Serialization;

namespace YinYanMusic.Core.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>
/// 分区（音乐专区）。
/// <para>
/// <paramref name="ContentMode"/> 是**可空**的：读接口（列表/详情）返回库里存的实际值（非空）；
/// 写接口沿用本项目的局部更新约定 —— <c>null</c> 表示"这次不改"（新建时表示用默认 <c>both</c>），
/// 所以别给它设非空默认值，否则"没传这个字段"会被当成"改回 both"。
/// </para>
/// </summary>
public record CategoryDto(int Id, string Name, string? Slogan = null, string? ColorHex = null, string? IconGlyph = null, string? ContentMode = null);

public record ArtistDto(long Id, string Name, string? Region, string? Kind, string? AvatarUrl, string? Bio, long FollowerCount, long SongCount, long AlbumCount);

/// <summary>
/// 专辑。<paramref name="TrackCount"/> 是**读接口才有值**的统计字段（创建/更新的响应里恒为 0，
/// 因为那一刻确实还没有歌），列表页要靠它显示"这张专辑有几首"。
/// </summary>
public record AlbumDto(long Id, string Name, string? CoverUrl, DateOnly? ReleaseDate, string? Description, ArtistDto? Artist, int TrackCount = 0)
{
    /// <summary>全部歌手（V2.12 联合创作专辑，按 Position 排，首位 = 主歌手）。读接口返回；写接口不收这个字段。</summary>
    public IReadOnlyList<SongArtistRef>? Artists { get; init; }
}

/// <summary>
/// 一首歌署名里的一位歌手（联合创作时有多位，按 Position 排，第一位是主歌手）。
/// <para>
/// 带 <see cref="Id"/> 而不是只给名字：界面上"点歌手名进详情页""关注这位歌手"都要 ID，
/// 只有名字就只能靠名字反查（同名歌手会认错人）。本地歌与离线缓存的索引里没有歌手 ID，
/// 那里 <see cref="Id"/> 为 0 —— 表示"知道有这么一位，但跳不过去"，界面据此隐藏跳转/关注动作。
/// </para>
/// </summary>
public record SongArtistRef(long Id, string Name);

public class SongDto : INotifyPropertyChanged
{
    public long Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public long ArtistId { get; init; }
    public string ArtistName { get; init; } = string.Empty;

    /// <summary>
    /// **全部**歌手（带 ID），按标签里的顺序（第一位是主歌手，其 <see cref="SongArtistRef.Name"/> 与
    /// <see cref="ArtistName"/> 相同）。联合创作的歌在这里能看到所有合作者；单歌手时是一个元素的列表；
    /// 本地歌（<see cref="IsLocal"/>）为 null。
    /// <para>
    /// 刻意给**列表**而不是拼好的一行：用得着它的地方（播放页点歌手跳转、关注某一位、长按逐个复制）
    /// 都要逐个人，别让服务端替客户端决定分隔符。想在界面上显示成一行就绑 <see cref="ArtistsDisplay"/>。
    /// </para>
    /// </summary>
    public IReadOnlyList<SongArtistRef>? Artists { get; init; }

    /// <summary>
    /// 界面上那一行歌手文案 = 全部歌手拼成 <c>Aimer / EGOIST</c>；没有列表（本地歌、旧数据）时退回主歌手。
    /// <para>
    /// 给界面**直接绑**用：列表模板、队列浮窗、封面另存的文件名都取它 —— 分隔符只有这一处定义，
    /// 每个页面不必各 join 一遍，也不必为"本地歌没有 <see cref="Artists"/>"单开一条分支。
    /// </para>
    /// <para>
    /// <b>不参与序列化</b>：拼成一行是客户端的观感决定，不该固化进接口契约 ——
    /// 线上传的仍然是 <see cref="Artists"/> 列表。
    /// </para>
    /// </summary>
    [JsonIgnore]
    public string ArtistsDisplay =>
        Artists is { Count: > 0 } ? string.Join(" / ", Artists.Select(a => a.Name)) : ArtistName;

    public long? AlbumId { get; init; }
    public string? AlbumName { get; init; }

    /// <summary>
    /// 所属分区（音乐专区）。可空 —— 歌曲不必属于任何分区。
    /// ⚠️ 后台「编辑歌曲」对话框靠它回显当前分类；列表/详情接口漏了它，下拉框就永远
    /// 显示占位符（选完保存成功，再打开还是空的）。
    /// </summary>
    public int? CategoryId { get; init; }

    /// <summary>分区名（展示用）。顺带带出，省得前端为了一列名字再查一次分类表。</summary>
    public string? CategoryName { get; init; }

    public string? CoverUrl { get; set; }
    public string AudioUrl { get; init; } = string.Empty;
    public string? LyricUrl { get; init; }
    private int _durationSeconds;
    public int DurationSeconds
    {
        get => _durationSeconds;
        set
        {
            if (_durationSeconds == value) return;
            _durationSeconds = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DurationSeconds)));
        }
    }
    private long _playCount;
    /// <summary>
    /// 播放次数。在线歌来自服务端统计；本地歌（<see cref="IsLocal"/>）是本地
    /// <c>LocalPlayCount</c> 的映射，播放后由 <c>PlayerService</c> 就地 +1，
    /// 所以必须是可写属性并带变更通知（列表上的次数列要立刻刷新）。
    /// </summary>
    public long PlayCount
    {
        get => _playCount;
        set
        {
            if (_playCount == value) return;
            _playCount = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PlayCount)));
        }
    }

    /// <summary>
    /// 是否为本地曲库的歌（V2.6）。true 时：
    /// <list type="bullet">
    /// <item><see cref="Id"/> 是负数（<c>-LocalSong.Id</c>），与在线自增正数天然隔离；</item>
    /// <item><see cref="AudioUrl"/> 是设备文件路径或 <c>content://</c> URI，不走 BaseUrl 拼接；</item>
    /// <item>播放**不上报**服务端，只累加本地计数；</item>
    /// <item>菜单隐藏「喜欢 / 添加到歌单 / 歌手 / 关注歌手」（没有 songId，也没有在线歌手）。</item>
    /// </list>
    /// </summary>
    public bool IsLocal { get; init; }

    private bool _isLiked;
    public bool IsLiked
    {
        get => _isLiked;
        set
        {
            if (_isLiked == value) return;
            _isLiked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLiked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public record PlaylistDto(
    long Id,
    string Name,
    string? Description,
    string? CoverUrl,
    int? CategoryId,
    string? CategoryName,
    long OwnerId,
    string OwnerName,
    int TrackCount,
    int CollectorCount,
    DateTime CreatedAt,
    bool IsSystem = false,
    /// <summary>
    /// 歌单播放次数 = 歌单内全部歌曲的 <see cref="Song.PlayCount"/> 之和（V2.4）。
    /// 聚合查询实时算出，不做冗余列，避免增删歌曲/删歌导致统计失准。
    /// </summary>
    long PlayCount = 0);

public record PlaylistDetailDto(
    long Id,
    string Name,
    string? Description,
    string? CoverUrl,
    long OwnerId,
    string OwnerName,
    bool IsOwner,
    bool IsCollected,
    IReadOnlyList<SongDto> Songs,
    // 编辑歌单（改名 / 选标签）需要的字段：
    int? CategoryId = null,
    string? CategoryName = null,
    /// <summary>系统歌单（如“我喜欢的音乐”）：不可改名 / 改标签 / 删除，前端据此隐藏按钮。</summary>
    bool IsSystem = false,
    /// <summary>歌单播放次数 = 歌单内全部歌曲 PlayCount 之和（V2.4）。</summary>
    long PlayCount = 0);

public record CreatePlaylistRequest(string Name, string? Description, int? CategoryId);

/// <summary>
/// 编辑歌单。<paramref name="CategoryId"/> 为 null 时表示"不改标签"；
/// 想把标签清空则置 <paramref name="ClearCategory"/> = true
/// （只靠 CategoryId=null 无法区分"没传"和"要清空"，所以单独给一个开关）。
/// </summary>
public record UpdatePlaylistRequest(string? Name, string? Description, string? CoverUrl, int? CategoryId, bool ClearCategory = false);

public record AddSongsToPlaylistRequest(IReadOnlyList<long> SongIds);

public record CreateSongRequest(
    string Title,
    long ArtistId,
    long? AlbumId,
    int? CategoryId,
    string AudioUrl,
    string? LyricUrl,
    int DurationSeconds);

public record CreateArtistRequest(string Name, string? Region, string? Kind, string? AvatarUrl, string? Bio);

public record CreateAlbumRequest(long ArtistId, string Name, DateOnly? ReleaseDate, string? Description, string? CoverUrl)
{
    /// <summary>
    /// 完整歌手列表（V2.12 联合创作专辑）：按顺序，首位 = 主歌手。传了（非空）以它为准，
    /// <see cref="ArtistId"/> 被忽略并由列表首位同步；不传保持旧行为（单歌手）。
    /// </summary>
    public IReadOnlyList<long>? ArtistIds { get; init; }
}

public record MediaUploadResult(string Url);

// ── M2: Update / Delete ──────────────────────────────────────────────────────

/// <summary>编辑歌曲（所有字段均可选，不传的保持原值）。</summary>
public record UpdateSongRequest(
    string? Title,
    long? ArtistId,
    long? AlbumId,
    int? CategoryId,
    string? AudioUrl,
    string? LyricUrl,
    int? DurationSeconds,
    /// <summary>传 true 表示把 CategoryId 清为空。</summary>
    bool ClearCategory = false,
    /// <summary>传 true 表示把 AlbumId 清为空（与 <see cref="ClearCategory"/> 同理：null 在这层只表示"不改"）。</summary>
    bool ClearAlbum = false,
    /// <summary>
    /// 完整歌手列表（V2.12 联合创作编辑）：**按顺序**，首位 = 主歌手，其余 = 合作歌手。
    /// 传了（非 null 且非空）就整体重建 SongArtists 关联；不传保持原值（向后兼容旧调用方）。
    /// 传了列表时 <see cref="ArtistId"/> 被忽略，以列表首位为准。
    /// </summary>
    IReadOnlyList<long>? ArtistIds = null);

/// <summary>编辑歌手。</summary>
public record UpdateArtistRequest(
    string? Name,
    string? Region,
    string? Kind,
    string? AvatarUrl,
    string? Bio,
    /// <summary>传 true 表示把 AvatarUrl 清为空（局部更新里 null 只表示"这次不改"）。</summary>
    bool ClearAvatar = false);

/// <summary>编辑专辑。</summary>
public record UpdateAlbumRequest(
    string? Name,
    DateOnly? ReleaseDate,
    string? Description,
    string? CoverUrl,
    /// <summary>传 true 表示把 ReleaseDate 清为空。</summary>
    bool ClearReleaseDate = false,
    /// <summary>传 true 表示把 CoverUrl 清为空（同上：null 只表示"这次不改"）。</summary>
    bool ClearCover = false,
    /// <summary>
    /// 完整歌手列表（V2.12 联合创作专辑）：按顺序，首位 = 主歌手。传了（非空）就整体重建关联
    /// 并同步 <c>Album.ArtistId</c>；不传保持原值（向后兼容旧调用方）。
    /// </summary>
    IReadOnlyList<long>? ArtistIds = null);