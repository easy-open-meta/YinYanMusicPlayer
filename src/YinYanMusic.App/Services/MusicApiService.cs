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
    Task<PlaylistDto?> CreatePlaylistAsync(string name, string? description);
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
    public async Task<AuthResponse> LoginAsync(LoginRequest req) =>
        await PostAsync<LoginRequest, AuthResponse>("api/auth/login", req);

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req) =>
        await PostAsync<RegisterRequest, AuthResponse>("api/auth/register", req);

    public async Task<UserDto?> MeAsync() =>
        await GetAsync<UserDto>("api/auth/me");

    public async Task<MeOverviewDto> GetOverviewAsync()
    {
        var r = await GetAsync<MeOverviewDto>("api/me/overview");
        return r ?? new MeOverviewDto(0, 0, 0, 0, 0, 0);
    }

    public Task<ArtistDto?> GetArtistAsync(long id) =>
        GetAsync<ArtistDto>($"api/catalog/artists/{id}");

    public async Task<PagedResult<AlbumDto>> GetAlbumsAsync(long? artistId = null, int page = 1, int pageSize = 20)
    {
        var url = $"api/catalog/albums?page={page}&pageSize={pageSize}";
        if (artistId.HasValue) url += $"&artistId={artistId}";
        return await GetAsync<PagedResult<AlbumDto>>(url) ?? new PagedResult<AlbumDto>([], 0, 1, pageSize);
    }

    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync() =>
        GetListAsync<CategoryDto>("api/catalog/categories");

    /// <summary>按专辑名模糊搜索专辑（搜索页"专辑"结果区）。</summary>
    public async Task<IReadOnlyList<AlbumDto>> SearchAlbumsAsync(string? keyword = null, int limit = 10)
    {
        var url = $"api/catalog/albums?page=1&pageSize={limit}";
        if (!string.IsNullOrWhiteSpace(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
        var result = await GetAsync<PagedResult<AlbumDto>>(url) ?? new PagedResult<AlbumDto>([], 0, 1, limit);
        return result.Items;
    }

    /// <summary>按用户名/昵称模糊搜索用户（搜索页"用户"结果区）。</summary>
    public async Task<IReadOnlyList<UserDto>> SearchUsersAsync(string? keyword = null, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(keyword)) return [];
        var url = $"api/users/search?limit={limit}&keyword={Uri.EscapeDataString(keyword)}";
        return await GetAsync<List<UserDto>>(url) ?? [];
    }

    /// <summary>用户详情页资料（粉丝/关注/歌单统计 + IsFollowing）。</summary>
    public Task<UserProfileDto?> GetUserProfileAsync(long userId) =>
        GetAsync<UserProfileDto>($"api/users/{userId}");

    /// <summary>该用户的公开歌单。</summary>
    public Task<IReadOnlyList<PlaylistDto>> GetUserPlaylistsAsync(long userId) =>
        GetListAsync<PlaylistDto>($"api/users/{userId}/playlists");

    public async Task<bool> FollowUserAsync(long userId) =>
        (await http.PutAsync($"api/me/following/{userId}", null)).IsSuccessStatusCode;

    public async Task<bool> UnfollowUserAsync(long userId) =>
        (await http.DeleteAsync($"api/me/following/{userId}")).IsSuccessStatusCode;

    /// <summary>当前用户关注的歌手完整信息（“我的”页关注列表）。注意与 GetFollowedArtistIdsAsync（仅 Id）区分。</summary>
    public Task<IReadOnlyList<ArtistDto>> GetFollowedArtistsAsync() =>
        GetListAsync<ArtistDto>("api/me/following-artists/details");

    /// <summary>当前用户关注的用户完整列表（“我的”页关注列表的用户区）。</summary>
    public Task<IReadOnlyList<UserDto>> GetFollowedUsersAsync() =>
        GetListAsync<UserDto>("api/me/following");

    public Task<IReadOnlyList<UserDto>> GetFollowersAsync() =>
        GetListAsync<UserDto>("api/me/followers");

    public async Task<PagedResult<SongDto>> SearchSongsAsync(string? keyword = null, long? artistId = null, long? albumId = null, int? categoryId = null, int page = 1, int pageSize = 20)
    {
        var url = $"api/songs?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
        if (artistId.HasValue) url += $"&artistId={artistId}";
        if (albumId.HasValue) url += $"&albumId={albumId}";
        if (categoryId.HasValue) url += $"&categoryId={categoryId}";
        return await GetAsync<PagedResult<SongDto>>(url) ?? new PagedResult<SongDto>([], 0, 1, pageSize);
    }

    public Task<IReadOnlyList<SongDto>> GetLikedSongsAsync() =>
        GetListAsync<SongDto>("api/songs/liked");

    public async Task<bool> LikeAsync(long songId) => (await http.PutAsync($"api/songs/{songId}/like", null)).IsSuccessStatusCode;

    public async Task<bool> UnlikeAsync(long songId) => (await http.DeleteAsync($"api/songs/{songId}/like")).IsSuccessStatusCode;

    public async Task<IReadOnlySet<long>> GetFollowedArtistIdsAsync()
    {
        var ids = await GetAsync<IReadOnlyList<long>>("api/me/following-artists");
        return ids is null ? new HashSet<long>() : ids.ToHashSet();
    }

    public async Task<bool> FollowArtistAsync(long artistId) =>
        (await http.PutAsync($"api/me/following-artists/{artistId}", null)).IsSuccessStatusCode;

    public async Task<bool> UnfollowArtistAsync(long artistId) =>
        (await http.DeleteAsync($"api/me/following-artists/{artistId}")).IsSuccessStatusCode;

    public async Task<PagedResult<PlaylistDto>> SearchPlaylistsAsync(string? keyword = null, int? categoryId = null, int page = 1, int pageSize = 20)
    {
        var url = $"api/playlists?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
        if (categoryId.HasValue) url += $"&categoryId={categoryId}";
        return await GetAsync<PagedResult<PlaylistDto>>(url) ?? new PagedResult<PlaylistDto>([], 0, 1, pageSize);
    }

    public Task<PlaylistDetailDto?> GetPlaylistAsync(long id) =>
        GetAsync<PlaylistDetailDto>($"api/playlists/{id}");

    public async Task<PlaylistDto?> CreatePlaylistAsync(string name, string? description) =>
        await PostAsync<CreatePlaylistRequest, PlaylistDto>("api/playlists", new CreatePlaylistRequest(name, description, null));

    public async Task<bool> AddSongsToPlaylistAsync(long playlistId, IReadOnlyList<long> songIds) =>
        (await http.PostAsJsonAsync($"api/playlists/{playlistId}/songs", new AddSongsToPlaylistRequest(songIds))).IsSuccessStatusCode;

    public async Task<bool> RemoveSongFromPlaylistAsync(long playlistId, long songId) =>
        (await http.DeleteAsync($"api/playlists/{playlistId}/songs/{songId}")).IsSuccessStatusCode;

    public async Task<bool> DeletePlaylistAsync(long playlistId) =>
        (await http.DeleteAsync($"api/playlists/{playlistId}")).IsSuccessStatusCode;

    public Task<IReadOnlyList<PlaylistDto>> GetMyPlaylistsAsync() =>
        GetListAsync<PlaylistDto>("api/me/playlists");

    public Task<IReadOnlyList<PlaylistDto>> GetCollectedPlaylistsAsync() =>
        GetListAsync<PlaylistDto>("api/playlists/collected");

    public async Task<IReadOnlyList<long>> GetPlaylistsContainingSongAsync(long songId) =>
        await GetAsync<List<long>>($"api/playlists/contains-song?songId={songId}") ?? [];

    public async Task<bool> CollectPlaylistAsync(long playlistId) =>
        (await http.PutAsync($"api/playlists/{playlistId}/collect", null)).IsSuccessStatusCode;

    public async Task<bool> UncollectPlaylistAsync(long playlistId) =>
        (await http.DeleteAsync($"api/playlists/{playlistId}/collect")).IsSuccessStatusCode;

    public async Task RecordPlayAsync(long songId) =>
        await http.PostAsync($"api/songs/{songId}/play", null);

    public async Task<MediaUploadResult?> UploadAsync(Stream content, string fileName, string kind)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        form.Add(streamContent, "file", fileName);
        var resp = await http.PostAsync($"api/media/upload?kind={kind}", form);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<MediaUploadResult>();
    }

    private async Task<T?> GetAsync<T>(string url)
    {
        try
        {
            return await http.GetFromJsonAsync<T>(url);
        }
        catch
        {
            return default;
        }
    }

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string url) where T : class =>
        await GetAsync<List<T>>(url) ?? [];

    private async Task<TResp> PostAsync<TReq, TResp>(string url, TReq body)
    {
        var resp = await http.PostAsJsonAsync(url, body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TResp>())!;
    }
}