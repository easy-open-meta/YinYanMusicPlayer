using System.Net.Http.Json;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Services;

public interface IMusicApi
{
    Task<AuthResponse> LoginAsync(LoginRequest req);
    Task<AuthResponse> RegisterAsync(RegisterRequest req);
    Task<UserDto?> MeAsync();

    // ── V2.5 账号与安全中心 ──────────────────────────────────────────────────

    /// <summary>更新资料（昵称 / 简介 / 性别 / 头像 URL）。</summary>
    Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest req);
    /// <summary>
    /// 上传头像（V2.5 调整）：提交 base64 图片，服务端校验 2MB 上限后
    /// 以 data URI 存进 AvatarUrl。返回更新后的用户资料。
    /// </summary>
    Task<UserDto?> UpdateAvatarAsync(string imageBase64, string contentType);
    /// <summary>修改密码。成功后服务端 TokenVersion 自增，旧 token 全部失效 → 需重新登录。</summary>
    Task<(bool Ok, string? Error)> ChangePasswordAsync(string oldPassword, string newPassword);
    /// <summary>发送邮箱验证码。SMTP 未配置时返回 Sent=false + 提示文案。</summary>
    Task<SendEmailCodeResult?> SendEmailCodeAsync(string email);
    /// <summary>绑定邮箱。</summary>
    Task<(bool Ok, string? Error)> BindEmailAsync(string email, string code);
    /// <summary>安全中心能力探测：邮箱绑定是否可用（SMTP 是否配置）。</summary>
    Task<bool> IsEmailBindingAvailableAsync();
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
    /// <summary>歌单内搜索歌曲（V2.4）：关键词命中标题/歌手/专辑，空关键词返回全部。</summary>
    Task<PagedResult<SongDto>> SearchPlaylistSongsAsync(long playlistId, string? keyword = null, int page = 1, int pageSize = 50);
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

    // ── V2.9 评论 ────────────────────────────────────────────────────────────
    /// <summary>评论列表（未登录也可读）。失败时返回 <c>null</c>，调用方据此提示。</summary>
    Task<PagedResult<CommentDto>?> GetCommentsAsync(string targetType, long targetId, int page = 1, int pageSize = 20);

    /// <summary>发表评论 / 回复（<paramref name="parentId"/> 非空即回复）。失败时带回后端文案（空内容、超长、限流都由服务端判定）。</summary>
    Task<(CommentDto? Comment, string? Error)> CreateCommentAsync(string targetType, long targetId, long? parentId, string content);

    /// <summary>删除自己的评论。</summary>
    Task<(bool Ok, string? Error)> DeleteCommentAsync(long commentId);

    /// <summary>点赞 / 取消赞，返回服务端算好的最新状态（点赞数 + 是否已赞）。</summary>
    Task<CommentLikeState?> SetCommentLikeAsync(long commentId, bool like);

    /// <summary>
    /// 某条主评论下**全部回复**的扁平分页（"展开全部"用）。列表接口每条主评论只带回
    /// 200 条子孙，超出的部分靠它逐页追加。返回项按 Id 升序、带 ParentId 与 Depth，
    /// 调用方按 ParentId 自建层级。
    /// </summary>
    Task<PagedResult<CommentDto>?> GetCommentRepliesAsync(long commentId, int page = 1, int pageSize = 200);

    /// <summary>
    /// 可见评论**总数（主评论 + 回复）**。菜单里那条「查看评论：(N)」与面板标题都用它 ——
    /// 列表接口的 <c>Total</c> 只数主评论，当"评论数"用会和用户直觉对不上。
    /// 请求失败返回 <c>null</c>，调用方退化成不带数字的文案。
    /// </summary>
    Task<int?> GetCommentCountAsync(string targetType, long targetId);

    // ── V2.10 断点续播 ──────────────────────────────────────────────────────
    /// <summary>上报播放进度。失败静默（进度丢一条不影响播放本身，TC-2.10-04）。</summary>
    Task<bool> SavePlaybackProgressAsync(long songId, double positionSeconds);

    /// <summary>最近一次播放（换设备“继续播放”用）。没有记录返回 null。</summary>
    Task<PlaybackProgressDto?> GetLastPlaybackAsync();

    /// <summary>
    /// 本机上报用的设备名（V2.12）。与上报进度时写进库的**是同一个值**，
    /// 所以调用方拿它判定"这份进度是不是本机自己报的"不会两边漂移。
    /// </summary>
    string CurrentDeviceName { get; }

    // ── V2.11 离线播放补报 ──────────────────────────────────────────────────
    /// <summary>
    /// 批量补报离线播放。网络/服务器失败会**抛异常**（与 Get 系的静默吞不同）——
    /// 补报调度器要靠异常区分"这批没发出去，队列保留"与"发出去了，按结果删"。
    /// </summary>
    Task<PlayReportsAck> ReportPlaysAsync(IReadOnlyList<PlayReportRequest> items);

    // ── V2.12 推荐歌单专区 ──────────────────────────────────────────────────
    /// <summary>
    /// 推荐歌单。<paramref name="scene"/> 取 <see cref="RecommendScenes"/> 之一；
    /// <paramref name="categoryId"/> 只有 <see cref="RecommendScenes.Zone"/> 场景需要。
    /// <para>接口匿名可访问，"未登录"这件事由服务端处理（for-you 退化成热播），
    /// 客户端不做登录判断 —— 游客打开首页也该看到推荐位。</para>
    /// </summary>
    Task<PagedResult<PlaylistDto>> GetRecommendedPlaylistsAsync(string scene = RecommendScenes.Hot, int? categoryId = null, int page = 1, int pageSize = 20);

    // ── V2.15 站内通知 ────────────────────────────────────────────────────────
    /// <summary>分页拉取自己的通知（未读在前）。失败返回空页。</summary>
    Task<PagedResult<NotificationDto>> GetNotificationsAsync(int page = 1, int pageSize = 20);

    /// <summary>未读数（角标初始值 / 静默兜底）。失败返回 -1，调用方保留现有角标。</summary>
    Task<int> GetNotificationUnreadCountAsync();

    /// <summary>标记单条已读。非本人接收行服务端 404。</summary>
    Task<(bool Ok, string? Error)> MarkNotificationReadAsync(long notificationId);

    /// <summary>一键全部已读。</summary>
    Task<(bool Ok, string? Error)> MarkAllNotificationsReadAsync();

    /// <summary>按 Id 取单曲（通知关联跳转播放用）。404 / 失败返回 null。</summary>
    Task<SongDto?> GetSongAsync(long songId);
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

    // ── V2.9 评论 ────────────────────────────────────────────────────────────

    public async Task<PagedResult<CommentDto>?> GetCommentsAsync(string targetType, long targetId, int page = 1, int pageSize = 20)
    {
        var url = Abs($"api/comments?targetType={Uri.EscapeDataString(targetType)}&targetId={targetId}&page={page}&pageSize={pageSize}");
        return await GetAsync<PagedResult<CommentDto>>(url);
    }

    public async Task<(CommentDto? Comment, string? Error)> CreateCommentAsync(string targetType, long targetId, long? parentId, string content)
    {
        // 400（内容/限流/目标不存在）要拿到后端文案给用户看，所以不走 PostAsync 的 EnsureSuccessStatusCode
        var resp = await http.PostAsJsonAsync(Abs("api/comments"), new CreateCommentRequest(targetType, targetId, parentId, content));
        if (resp.IsSuccessStatusCode) return (await resp.Content.ReadFromJsonAsync<CommentDto>(), null);
        return (null, await ReadErrorMessageAsync(resp));
    }

    public async Task<(bool Ok, string? Error)> DeleteCommentAsync(long commentId)
    {
        var resp = await http.DeleteAsync(Abs($"api/comments/{commentId}"));
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorMessageAsync(resp));
    }

    public async Task<CommentLikeState?> SetCommentLikeAsync(long commentId, bool like)
    {
        var resp = like
            ? await http.PostAsync(Abs($"api/comments/{commentId}/like"), null)
            : await http.DeleteAsync(Abs($"api/comments/{commentId}/like"));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<CommentLikeState>() : null;
    }

    public async Task<PagedResult<CommentDto>?> GetCommentRepliesAsync(long commentId, int page = 1, int pageSize = 200)
        => await GetAsync<PagedResult<CommentDto>>(Abs($"api/comments/{commentId}/replies?page={page}&pageSize={pageSize}"));

    public async Task<int?> GetCommentCountAsync(string targetType, long targetId)
    {
        var result = await GetAsync<CommentCountDto>(
            Abs($"api/comments/count?targetType={Uri.EscapeDataString(targetType)}&targetId={targetId}"));
        return result?.Count;
    }

    /// <summary>GET api/comments/count 的返回：只为一个计数，不值得进 Core.Dtos 给两端共用。</summary>
    private sealed record CommentCountDto(int Count);

    // ── V2.10 断点续播 ──────────────────────────────────────────────────────

    /// <summary>
    /// 上报设备名（V2.12）：Android 是机型（如 "2201123C"），Windows 是主机名。
    /// 存进静态字段而不是每次读 —— DeviceInfo.Name 在 Android 上要跨 JNI，进度上报每 30 秒一次，
    /// 没必要反复取；且设备名在一次运行里不会变。
    /// </summary>
    private static string? _deviceName;
    private static string DeviceName =>
        _deviceName ??= DeviceInfo.Current.Name is { Length: > 0 } name ? name : DeviceInfo.Current.Platform.ToString();

    /// <summary>见 <see cref="IMusicApi.CurrentDeviceName"/> —— 与上报用的是同一个值。</summary>
    public string CurrentDeviceName => DeviceName;

    public async Task<bool> SavePlaybackProgressAsync(long songId, double positionSeconds)
    {
        try
        {
            var resp = await http.PutAsJsonAsync(Abs("api/me/playback/progress"),
                new PlaybackProgressRequest(songId, positionSeconds, DeviceName));
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            // 断网 / 超时都当失败：调用方是 fire-and-forget，不能把异常抛到播放路径上
            return false;
        }
    }

    public Task<PlaybackProgressDto?> GetLastPlaybackAsync() =>
        GetAsync<PlaybackProgressDto>(Abs("api/me/playback/last"));

    public async Task<AuthResponse> LoginAsync(LoginRequest req) =>
        await PostAsync<LoginRequest, AuthResponse>(Abs("api/auth/login"), req);

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req) =>
        await PostAsync<RegisterRequest, AuthResponse>(Abs("api/auth/register"), req);

    public async Task<UserDto?> MeAsync() =>
        await GetAsync<UserDto>(Abs("api/auth/me"));

    // ── V2.5 账号与安全中心 ──────────────────────────────────────────────────

    public async Task<UserDto?> UpdateProfileAsync(UpdateProfileRequest req)
    {
        // 沿用项目既有写法（直接 http.PutAsJsonAsync + 手动读响应），不额外引入 PutAsync 辅助方法
        var resp = await http.PutAsJsonAsync(Abs("api/auth/me"), req);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<UserDto>() : null;
    }

    public async Task<UserDto?> UpdateAvatarAsync(string imageBase64, string contentType)
    {
        var resp = await http.PutAsJsonAsync(Abs("api/auth/me/avatar"),
            new UpdateAvatarRequest(imageBase64, contentType));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<UserDto>() : null;
    }

    public async Task<(bool Ok, string? Error)> ChangePasswordAsync(string oldPassword, string newPassword)
    {
        var resp = await http.PutAsJsonAsync(Abs("api/auth/password"), new ChangePasswordRequest(oldPassword, newPassword));
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorMessageAsync(resp));
    }

    public async Task<SendEmailCodeResult?> SendEmailCodeAsync(string email)
        => await PostAsync<SendEmailCodeRequest, SendEmailCodeResult>(Abs("api/auth/email/code"), new SendEmailCodeRequest(email));

    public async Task<(bool Ok, string? Error)> BindEmailAsync(string email, string code)
    {
        var resp = await http.PutAsJsonAsync(Abs("api/auth/email"), new BindEmailRequest(email, code));
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorMessageAsync(resp));
    }

    public async Task<bool> IsEmailBindingAvailableAsync()
    {
        try
        {
            var r = await GetAsync<CapabilitiesDto>(Abs("api/auth/capabilities"));
            return r?.EmailBinding ?? false;
        }
        catch
        {
            // 探测失败按"不可用"处理：宁可少显示一个入口，也不要让用户点了没反应
            return false;
        }
    }

    /// <summary>读后端统一错误体 <c>{ "message": "..." }</c>；取不到就回 HTTP 状态码。</summary>
    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage resp)
    {
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
            if (!string.IsNullOrWhiteSpace(body?.Message)) return body!.Message!;
        }
        catch { /* 非 JSON 响应，走下面的兜底 */ }
        return $"操作失败（HTTP {(int)resp.StatusCode}）";
    }

    private sealed record ErrorBody(string? Message);
    private sealed record CapabilitiesDto(bool EmailBinding);

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

    /// <summary>
    /// 推荐歌单（V2.12）。场景名走查询串透传，**不在客户端做白名单校验** ——
    /// 服务端对未知名一律兜底成热播，所以将来加场景只要改服务端 + 客户端标签表两处，
    /// 不必再改这一层（反过来在客户端拦一道，只会变成"服务端上了新场景、客户端还是空列表"）。
    /// </summary>
    public async Task<PagedResult<PlaylistDto>> GetRecommendedPlaylistsAsync(string scene = RecommendScenes.Hot, int? categoryId = null, int page = 1, int pageSize = 20)
    {
        var url = $"api/recommend/playlists?scene={Uri.EscapeDataString(scene)}&page={page}&pageSize={pageSize}";
        if (categoryId.HasValue) url += $"&categoryId={categoryId}";
        return await GetAsync<PagedResult<PlaylistDto>>(Abs(url)) ?? new PagedResult<PlaylistDto>([], 0, 1, pageSize);
    }

    /// <summary>
    /// 歌单内搜索歌曲（V2.4）。关键词命中标题/歌手/专辑，空关键词返回全部。
    /// 走 Abs(...) 拼接，保证改服务器地址后下一次请求即生效。
    /// </summary>
    public async Task<PagedResult<SongDto>> SearchPlaylistSongsAsync(long playlistId, string? keyword = null, int page = 1, int pageSize = 50)
    {
        var url = $"api/playlists/{playlistId}/songs?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
        return await GetAsync<PagedResult<SongDto>>(Abs(url)) ?? new PagedResult<SongDto>([], 0, 1, pageSize);
    }

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

    public async Task<PlayReportsAck> ReportPlaysAsync(IReadOnlyList<PlayReportRequest> items)
    {
        var resp = await http.PostAsJsonAsync(Abs("api/me/playback/reports"), new PlayReportsBatchRequest(items));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PlayReportsAck>())!;
    }

    public async Task<MediaUploadResult?> UploadAsync(Stream content, string fileName, string kind)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        form.Add(streamContent, "file", fileName);
        var resp = await http.PostAsync(Abs($"api/media/upload?kind={kind}"), form);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<MediaUploadResult>();
    }

    // ── V2.15 站内通知 ────────────────────────────────────────────────────────

    public async Task<PagedResult<NotificationDto>> GetNotificationsAsync(int page = 1, int pageSize = 20)
    {
        var r = await GetAsync<PagedResult<NotificationDto>>(Abs($"api/notifications?page={page}&pageSize={pageSize}"));
        return r ?? new PagedResult<NotificationDto>([], 0, page, pageSize);
    }

    public async Task<int> GetNotificationUnreadCountAsync()
    {
        try
        {
            var resp = await http.GetAsync(Abs("api/notifications/unread-count"));
            if (!resp.IsSuccessStatusCode) return -1;
            var body = await resp.Content.ReadFromJsonAsync<UnreadCountDto>();
            return body?.UnreadCount ?? -1;
        }
        catch
        {
            return -1;
        }
    }

    public async Task<(bool Ok, string? Error)> MarkNotificationReadAsync(long notificationId)
    {
        var resp = await http.PostAsync(Abs($"api/notifications/{notificationId}/read"), null);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorMessageAsync(resp));
    }

    public async Task<(bool Ok, string? Error)> MarkAllNotificationsReadAsync()
    {
        var resp = await http.PostAsync(Abs("api/notifications/read-all"), null);
        if (resp.IsSuccessStatusCode) return (true, null);
        return (false, await ReadErrorMessageAsync(resp));
    }

    public async Task<SongDto?> GetSongAsync(long songId)
    {
        try
        {
            return await http.GetFromJsonAsync<SongDto>(Abs($"api/songs/{songId}"));
        }
        catch
        {
            return null;
        }
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