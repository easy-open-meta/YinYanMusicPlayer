using System.Net.Http.Json;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Services;

public interface IMusicApi
{
    Task<AuthResponse> LoginAsync(LoginRequest req);
    Task<AuthResponse> RegisterAsync(RegisterRequest req);
    Task<UserDto?> MeAsync();
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync();
    Task<IReadOnlyList<ArtistDto>> GetFollowedArtistsAsync();
    Task<IReadOnlyList<UserDto>> GetFollowedUsersAsync();
    Task<IReadOnlyList<UserDto>> GetFollowersAsync();
    Task<PagedResult<SongDto>> SearchSongsAsync(string? keyword = null, long? artistId = null, long? albumId = null, int? categoryId = null, int page = 1, int pageSize = 20);
    Task<IReadOnlyList<SongDto>> GetLikedSongsAsync();
    Task<bool> LikeAsync(long songId);
    Task<bool> UnlikeAsync(long songId);
    Task<IReadOnlySet<long>> GetFollowedArtistIdsAsync();
    Task<bool> FollowArtistAsync(long artistId);
    Task<bool> UnfollowArtistAsync(long artistId);
    Task<PagedResult<PlaylistDto>> SearchPlaylistsAsync(string? keyword = null, int? categoryId = null, int page = 1, int pageSize = 20);
    Task<IReadOnlyList<AlbumDto>> SearchAlbumsAsync(string? keyword = null, int limit = 10);
    Task<IReadOnlyList<UserDto>> SearchUsersAsync(string? keyword = null, int limit = 10);
    Task<UserProfileDto?> GetUserProfileAsync(long userId);
    Task<IReadOnlyList<PlaylistDto>> GetUserPlaylistsAsync(long userId);
    Task<bool> FollowUserAsync(long userId);
    Task<bool> UnfollowUserAsync(long userId);
    Task<PlaylistDetailDto?> GetPlaylistAsync(long id);
    /// <summary>新建歌单。<paramref name="categoryId"/> 传 null 即不带标签。</summary>
    Task<PlaylistDto?> CreatePlaylistAsync(string name, string? description, int? categoryId = null);
    /// <summary>编辑歌单：改名 / 换标签。<paramref name="categoryId"/> 为 null 且 clearCategory 为 false 时不改标签。</summary>
    Task<bool> UpdatePlaylistAsync(long playlistId, string name, int? categoryId, bool clearCategory = false);
    Task<bool> AddSongsToPlaylistAsync(long playlistId, IReadOnlyList<long> songIds);
    Task<bool> RemoveSongFromPlaylistAsync(long playlistId, long songId);
    Task<bool> DeletePlaylistAsync(long playlistId);
    Task<IReadOnlyList<PlaylistDto>> GetMyPlaylistsAsync();
    Task<IReadOnlyList<PlaylistDto>> GetCollectedPlaylistsAsync();
    Task<IReadOnlyList<long>> GetPlaylistsContainingSongAsync(long songId);
    Task<bool> CollectPlaylistAsync(long playlistId);
    Task<bool> UncollectPlaylistAsync(long playlistId);
    Task RecordPlayAsync(long songId);
    Task<MediaUploadResult?> UploadAsync(Stream content, string fileName, string kind);
    Task<MeOverviewDto> GetOverviewAsync();
    Task<ArtistDto?> GetArtistAsync(long id);
    Task<PagedResult<AlbumDto>> GetAlbumsAsync(long? artistId = null, int page = 1, int pageSize = 20);
}

/// <summary>GET api/me/overview 的返回：liked 喜欢的歌曲数、myPlaylists 我的歌单数、
/// collected 收藏的歌单数、following 我关注的用户数、followers 我的粉丝数、
/// artistFollowing 我关注的歌手数（即“我的”页显示的“关注”）。</summary>
public record MeOverviewDto(long Liked, long MyPlaylists, long Collected, long Following, long Followers, long ArtistFollowing);

public class MusicApiService(HttpClient http) : IMusicApi
{
    /// <summary>
    /// 所有路径都走 <see cref="ApiConfig.Absolute(string?)"/> —— HttpClient 不设 BaseAddress，
    /// 所以改了 ApiConfig.BaseUrl <b>下一次请求就生效</b>，不用重建 HttpClient。
    /// </summary>
    private static string Abs(string relative) => ApiConfig.Absolute(relative);

    public async Task<AuthResponse> LoginAsync(LoginRequest req) =>
        await PostAsync<LoginRequest, AuthResponse>(Abs("api/auth/login"), req);

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req) =>
        await PostAsync<RegisterRequest, AuthResponse>(Abs("api/auth/register"), req);

    public async Task<UserDto?> MeAsync() =>
        await GetAsync<UserDto>(Abs("api/auth/me"));

    public async Task<MeOverviewDto> GetOverviewAsync()
    {
        var r = await GetAsync<MeOverviewDto>(Abs("api/me/overview"));
        return r ?? new MeOverviewDto(0, 0, 0, 0, 0, 0);
    }

    public Task<ArtistDto?> GetArtistAsync(long id) =>
        GetAsync<ArtistDto>(Abs($"api/catalog/artists/{id}"));

    public async Task<PagedResult<AlbumDto>> GetAlbumsAsync(long? artistId = null, int page = 1, int pageSize = 20)
    {
        var url = $"api/catalog/albums?page={page}&pageSize={pageSize}";
        if (artistId.HasValue) url += $"&artistId={artistId}";
        return await GetAsync<PagedResult<AlbumDto>>(Abs(url)) ?? new PagedResult<AlbumDto>([], 0, 1, pageSize);
    }

    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync() =>
        GetListAsync<CategoryDto>(Abs("api/catalog/categories"));

    /// <summary>按专辑名模糊搜索专辑（搜索页"专辑"结果区）。</summary>
    public async Task<IReadOnlyList<AlbumDto>> SearchAlbumsAsync(string? keyword = null, int limit = 10)
    {
        var url = $"api/catalog/albums?page=1&pageSize={limit}";
        if (!string.IsNullOrWhiteSpace(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
        var result = await GetAsync<PagedResult<AlbumDto>>(Abs(url)) ?? new PagedResult<AlbumDto>([], 0, 1, limit);
        return result.Items;
    }

    /// <summary>按用户名/昵称模糊搜索用户（搜索页"用户"结果区）。</summary>
    public async Task<IReadOnlyList<UserDto>> SearchUsersAsync(string? keyword = null, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return [];
        var url = $"api/users/search?limit={limit}&keyword={Uri.EscapeDataString(keyword)}";
        return await GetAsync<List<UserDto>>(Abs(url)) ?? [];
    }

    /// <summary>用户详情页资料（粉丝/关注/歌单统计 + IsFollowing）。</summary>
    public Task<UserProfileDto?> GetUserProfileAsync(long userId) =>
        GetAsync<UserProfileDto>(Abs($"api/users/{userId}"));

    /// <summary>该用户的公开歌单。</summary>
    public Task<IReadOnlyList<PlaylistDto>> GetUserPlaylistsAsync(long userId) =>
        GetListAsync<PlaylistDto>(Abs($"api/users/{userId}/playlists"));

    public async Task<bool> FollowUserAsync(long userId) =>
        (await http.PutAsync(Abs($"api/me/following/{userId}"), null)).IsSuccessStatusCode;

    public async Task<bool> UnfollowUserAsync(long userId) =>
        (await http.DeleteAsync(Abs($"api/me/following/{userId}"))).IsSuccessStatusCode;

    /// <summary>当前用户关注的歌手完整信息（“我的”页关注列表）。注意与 GetFollowedArtistIdsAsync（仅 Id）区分。</summary>
    public Task<IReadOnlyList<ArtistDto>> GetFollowedArtistsAsync() =>
        GetListAsync<ArtistDto>(Abs("api/me/following-artists/details"));

    /// <summary>当前用户关注的用户完整列表（"我的"页关注列表的用户区）。</summary>
    public Task<IReadOnlyList<UserDto>> GetFollowedUsersAsync() =>
        GetListAsync<UserDto>(Abs("api/me/following"));

    public Task<IReadOnlyList<UserDto>> GetFollowersAsync() =>
        GetListAsync<UserDto>(Abs("api/me/followers"));

    public async Task<PagedResult<SongDto>> SearchSongsAsync(string? keyword = null, long? artistId = null, long? albumId = null, int? categoryId = null, int page = 1, int pageSize = 20)
    {
        var url = $"api/songs?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
        if (artistId.HasValue) url += $"&artistId={artistId}";
        if (albumId.HasValue) url += $"&albumId={albumId}";
        if (categoryId.HasValue) url += $"&categoryId={categoryId}";
        return await GetAsync<PagedResult<SongDto>>(Abs(url)) ?? new PagedResult<SongDto>([], 0, 1, pageSize);
    }

    public Task<IReadOnlyList<SongDto>> GetLikedSongsAsync() =>
        GetListAsync<SongDto>(Abs("api/songs/liked"));

    public async Task<bool> LikeAsync(long songId) => (await http.PutAsync(Abs($"api/songs/{songId}/like"), null)).IsSuccessStatusCode;
    public async Task<bool> UnlikeAsync(long songId) => (await http.DeleteAsync(Abs($"api/songs/{songId}/like"))).IsSuccessStatusCode;

    public async Task<IReadOnlySet<long>> GetFollowedArtistIdsAsync()
    {
        var ids = await GetAsync<IReadOnlyList<long>>(Abs("api/me/following-artists"));
        return ids is null ? new HashSet<long>() : ids.ToHashSet();
    }

    public async Task<bool> FollowArtistAsync(long artistId) =>
        (await http.PutAsync(Abs($"api/me/following-artists/{artistId}"), null)).IsSuccessStatusCode;
    public async Task<bool> UnfollowArtistAsync(long artistId) =>
        (await http.DeleteAsync(Abs($"api/me/following-artists/{artistId}"))).IsSuccessStatusCode;

    public async Task<PagedResult<PlaylistDto>> SearchPlaylistsAsync(string? keyword = null, int? categoryId = null, int page = 1, int pageSize = 20)
    {
        var url = $"api/playlists?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
        if (categoryId.HasValue) url += $"&categoryId={categoryId}";
        return await GetAsync<PagedResult<PlaylistDto>>(Abs(url)) ?? new PagedResult<PlaylistDto>([], 0, 1, pageSize);
    }

    public Task<PlaylistDetailDto?> GetPlaylistAsync(long id) =>
        GetAsync<PlaylistDetailDto>(Abs($"api/playlists/{id}"));

    public async Task<PlaylistDto?> CreatePlaylistAsync(string name, string? description, int? categoryId = null) =>
        await PostAsync<CreatePlaylistRequest, PlaylistDto>(Abs("api/playlists"), new CreatePlaylistRequest(name, description, categoryId));

    /// <summary>编辑歌单（改名 + 换标签）。Description / CoverUrl 传 null = 保持原值。</summary>
    public async Task<bool> UpdatePlaylistAsync(long playlistId, string name, int? categoryId, bool clearCategory = false) =>
        (await http.PutAsJsonAsync(Abs($"api/playlists/{playlistId}"),
            new UpdatePlaylistRequest(name, null, null, categoryId, clearCategory))).IsSuccessStatusCode;

    public async Task<bool> AddSongsToPlaylistAsync(long playlistId, IReadOnlyList<long> songIds) =>
        (await http.PostAsJsonAsync(Abs($"api/playlists/{playlistId}/songs"), new AddSongsToPlaylistRequest(songIds))).IsSuccessStatusCode;

    public async Task<bool> RemoveSongFromPlaylistAsync(long playlistId, long songId) =>
        (await http.DeleteAsync(Abs($"api/playlists/{playlistId}/songs/{songId}"))).IsSuccessStatusCode;

    public async Task<bool> DeletePlaylistAsync(long playlistId) =>
        (await http.DeleteAsync(Abs($"api/playlists/{playlistId}"))).IsSuccessStatusCode;

    public Task<IReadOnlyList<PlaylistDto>> GetMyPlaylistsAsync() =>
        GetListAsync<PlaylistDto>(Abs("api/me/playlists"));

    public Task<IReadOnlyList<PlaylistDto>> GetCollectedPlaylistsAsync() =>
        GetListAsync<PlaylistDto>(Abs("api/playlists/collected"));

    public async Task<IReadOnlyList<long>> GetPlaylistsContainingSongAsync(long songId) =>
        await GetAsync<List<long>>(Abs($"api/playlists/contains-song?songId={songId}")) ?? [];

    public async Task<bool> CollectPlaylistAsync(long playlistId) =>
        (await http.PutAsync(Abs($"api/playlists/{playlistId}/collect"), null)).IsSuccessStatusCode;
    public async Task<bool> UncollectPlaylistAsync(long playlistId) =>
        (await http.DeleteAsync(Abs($"api/playlists/{playlistId}/collect"))).IsSuccessStatusCode;

    public async Task RecordPlayAsync(long songId) =>
        await http.PostAsync(Abs($"api/songs/{songId}/play"), null);

    public async Task<MediaUploadResult?> UploadAsync(Stream content, string fileName, string kind)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        form.Add(streamContent, "file", fileName);
        var resp = await http.PostAsync(Abs($"api/media/upload?kind={kind}"), form);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<MediaUploadResult>();
    }

    private async Task<T?> GetAsync<T>(string absoluteUrl)
    {
        try
        {
            return await http.GetFromJsonAsync<T>(absoluteUrl);
        }
        catch
        {
            return default;
        }
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string absoluteUrl) where T : class =>
        await GetAsync<List<T>>(absoluteUrl) ?? [];

    private async Task<TResp> PostAsync<TReq, TResp>(string absoluteUrl, TReq body)
    {
        var resp = await http.PostAsJsonAsync(absoluteUrl, body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TResp>())!;
    }
}