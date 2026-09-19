using System.ComponentModel;

namespace YinYanMusic.Core.Dtos;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public record CategoryDto(int Id, string Name, string? Slogan = null, string? ColorHex = null, string? IconGlyph = null);

public record ArtistDto(long Id, string Name, string? Region, string? Kind, string? AvatarUrl, string? Bio, long FollowerCount, long SongCount, long AlbumCount);

public record AlbumDto(long Id, string Name, string? CoverUrl, DateOnly? ReleaseDate, string? Description, ArtistDto? Artist);

public class SongDto : INotifyPropertyChanged
{
    public long Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public long ArtistId { get; init; }
    public string ArtistName { get; init; } = string.Empty;
    public long? AlbumId { get; init; }
    public string? AlbumName { get; init; }
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
    public long PlayCount { get; init; }

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
    bool IsSystem = false);

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
    bool IsSystem = false);

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

public record CreateAlbumRequest(long ArtistId, string Name, DateOnly? ReleaseDate, string? Description, string? CoverUrl);

public record MediaUploadResult(string Url);