using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.App.Services.LocalLibrary;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

public partial class LibraryViewModel(IMusicApi api, IAuthService auth, PlayerService player,
    LocalLibraryStore localLibrary, NotificationRealtimeService realtime) : ObservableObject
{
    /// <summary>未读通知数（V2.15「我的」页角标）。</summary>
    [ObservableProperty]
    private int unreadCount;

    /// <summary>角标是否显示（0 隐藏）。</summary>
    public bool UnreadBadgeVisible => UnreadCount > 0;

    /// <summary>角标文案：>9 收成 9+（避免撑开卡片）。</summary>
    public string UnreadBadgeText => UnreadCount > 9 ? "9+" : UnreadCount.ToString();

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(UnreadBadgeVisible));
        OnPropertyChanged(nameof(UnreadBadgeText));
    }

    /// <summary>
    /// 订阅实时通道的未读变化。本 VM 是单例，挂一次就够（用 <c>_realtimeHooked</c> 防重复）。
    /// 事件可能在 SignalR 后台线程抛出，角标只能在主线程改。
    /// </summary>
    private bool _realtimeHooked;

    private void HookRealtimeOnce()
    {
        if (_realtimeHooked) return;
        _realtimeHooked = true;
        realtime.UnreadCountChanged += count =>
            MainThread.BeginInvokeOnMainThread(() => UnreadCount = count);
    }

    public ObservableCollection<PlaylistDto> MyPlaylists { get; } = [];
    public ObservableCollection<PlaylistDto> CollectedPlaylists { get; } = [];
    public ObservableCollection<SongDto> LikedSongs { get; } = [];

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string avatarUrl = string.Empty;

    [ObservableProperty]
    private string gender = string.Empty;

    /// <summary>个人简介（V2.5 调整）。卡片上最多显示 25 字，超出走 Dialog 看全文。</summary>
    [ObservableProperty]
    private string bio = string.Empty;

    /// <summary>卡片上展示的简介（超出 25 字截断加省略号）。</summary>
    public string BioPreview => string.IsNullOrEmpty(Bio)
        ? string.Empty
        : (Bio.Length <= BioPreviewLimit ? Bio : Bio[..BioPreviewLimit] + "…");

    /// <summary>是否有简介（控制简介行与分隔线的可见性）。</summary>
    public bool HasBio => !string.IsNullOrWhiteSpace(Bio);

    /// <summary>简介是否超出预览长度（决定是否显示可点的 ">" 箭头）。</summary>
    public bool BioOverflows => !string.IsNullOrEmpty(Bio) && Bio.Length > BioPreviewLimit;

    /// <summary>卡片上简介的最大字符数；超出则截断 + 弹窗看全文。</summary>
    private const int BioPreviewLimit = 25;

    partial void OnBioChanged(string value)
    {
        OnPropertyChanged(nameof(BioPreview));
        OnPropertyChanged(nameof(HasBio));
        OnPropertyChanged(nameof(BioOverflows));
    }

    [ObservableProperty]
    private string stats = string.Empty;

    /// <summary>关注数量（订阅者）。</summary>
    [ObservableProperty]
    private int followingCount;

    /// <summary>粉丝数量（跟踪者）。</summary>
    [ObservableProperty]
    private int followersCount;

    [ObservableProperty]
    private int myPlaylistCount;

    [ObservableProperty]
    private int collectedPlaylistCount;

    [ObservableProperty]
    private bool isBusy;

    /// <summary>本地音乐入口（V2.9 从抽屉迁来）：本地曲库总数（"本地音乐 (3)"）。</summary>
    [ObservableProperty]
    private int localSongCount;

    /// <summary>「本地音乐」标题 + 括号总数；没有扫描过时只显示标题不加括号。</summary>
    public string LocalMusicTitle => LocalSongCount > 0 ? $"本地音乐 ({LocalSongCount})" : "本地音乐";

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            HookRealtimeOnce();

            var me = await api.MeAsync();
            DisplayName = me?.DisplayName ?? auth.CurrentUser?.DisplayName ?? "未登录";
            AvatarUrl = me?.AvatarUrl ?? string.Empty;
            Gender = me?.Gender ?? string.Empty;
            Bio = me?.Bio ?? string.Empty;

            var overview = await api.GetOverviewAsync();
            // “关注”统计 = 关注的用户（Follow 表）+ 关注的歌手（ArtistFollow 表），两者都算关注对象。
            FollowingCount = (int)(overview.ArtistFollowing + overview.Following);
            FollowersCount = (int)overview.Followers;

            // V2.15 未读角标：进「我的」页拉一次真值（-1 = 拉取失败，保留现有角标不清零）
            try
            {
                var unread = await api.GetNotificationUnreadCountAsync();
                if (unread >= 0) UnreadCount = unread;
                await realtime.EnsureConnectedAsync();
            }
            catch (Exception ex)
            {
                AppLog.Warn($"[我的] 未读通知拉取失败: {ex.Message}");
            }

            var my = await api.GetMyPlaylistsAsync();
            var collected = (await api.GetCollectedPlaylistsAsync()).ToList();
            var liked = await api.GetLikedSongsAsync();
            var myFiltered = my.Where(p => !p.IsSystem).ToList();
            // 歌单封面回退已由服务端统一处理（无封面时取歌单内第一首歌的封面）
            Replace(MyPlaylists, myFiltered);
            Replace(CollectedPlaylists, collected);
            Replace(LikedSongs, liked);
            var firstCover = liked.Count > 0 ? liked[0].CoverUrl : null;
            var likedPlaylist = new PlaylistDto(0, "我喜欢的音乐", null, firstCover, null, null, 0, "我", liked.Count, 0, DateTime.MinValue, IsSystem: true);
            MyPlaylists.Insert(0, likedPlaylist);
            MyPlaylistCount = MyPlaylists.Count;
            CollectedPlaylistCount = collected.Count;
            Stats = $"{liked.Count} 首喜欢的音乐 · {MyPlaylists.Count} 个歌单 · {collected.Count} 个收藏";

            // 本地音乐入口的总数：纯本地 SQLite，接口失败也不影响本页其它内容
            try
            {
                LocalSongCount = await localLibrary.CountAsync();
                OnPropertyChanged(nameof(LocalMusicTitle));
            }
            catch (Exception ex)
            {
                AppLog.Warn($"[我的] 本地曲库计数失败: {ex.Message}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>个人卡三个统计位的跳转：关注（歌手）/ 歌单 / 粉丝。</summary>
    [RelayCommand]
    private Task OpenFollowingAsync() => Shell.Current.GoToAsync("followArtists");

    [RelayCommand]
    private Task OpenMyPlaylistsAsync() => Shell.Current.GoToAsync("myPlaylists");

    [RelayCommand]
    private Task OpenFollowersAsync() => Shell.Current.GoToAsync("followers");

    /// <summary>账号信息编辑（V2.5）：点个人信息卡进入。</summary>
    [RelayCommand]
    private Task OpenAccountAsync() => Shell.Current.GoToAsync("account");

    /// <summary>本地音乐（V2.9 从抽屉迁来）：纯本地能力，不依赖登录态与网络。</summary>
    [RelayCommand]
    private Task OpenLocalMusicAsync() => Shell.Current.GoToAsync("localMusic");

    /// <summary>消息通知（V2.15）：进通知列表页。</summary>
    [RelayCommand]
    private Task OpenNotificationsAsync() => Shell.Current.GoToAsync("notifications");

    /// <summary>
    /// 查看完整个人简介（V2.5）。卡片上只显示前 25 字，超出部分用现有 Dialog 弹全文
    /// —— 复用全站统一的居中卡片，不再造第二套弹窗。
    /// </summary>
    [RelayCommand]
    private async Task ShowFullBioAsync()
    {
        if (string.IsNullOrWhiteSpace(Bio)) return;
        await SongMenuHelper.ShowMessageDialogAsync("个人介绍", Bio);
    }

    [RelayCommand]
    private async Task PlayLikedAsync()
    {
        if (LikedSongs.Count == 0)
        {
            await SongMenuHelper.ShowMessageDialogAsync("提示", "还没有喜欢的歌曲。", "好的");
            return;
        }
        player.PlayQueue(LikedSongs, 0, "我喜欢的音乐");
        await Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private async Task OpenPlaylistAsync(PlaylistDto playlist)
    {

        await Shell.Current.GoToAsync($"playlist?id={playlist.Id}");
    }

    [RelayCommand]
    private async Task CreatePlaylistAsync()
    {
        // 与「编辑歌单」用同一个居中圆角对话框：一次填名称 + 选标签
        //（返回 null 表示用户取消；名称已由对话框 Trim 过，空名会就地报错不关闭）
        var categories = await api.GetCategoriesAsync();
        var draft = await SongMenuHelper.ShowEditPlaylistAsync("新建歌单", "我的歌单", categories, null, "创建");
        if (draft is null) return;

        var trimmedName = draft.Name;
        if (trimmedName == "我喜欢的音乐")
        {
            await SongMenuHelper.ShowMessageDialogAsync("提示", "该名称为系统保留，请换个名字", "确定");
            return;
        }
        var existing = await api.GetMyPlaylistsAsync();
        if (existing.Any(p => string.Equals(p.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            await SongMenuHelper.ShowMessageDialogAsync("提示", $"已存在名为「{trimmedName}」的歌单，请换个名字", "确定");
            return;
        }
        var created = await api.CreatePlaylistAsync(trimmedName, null, draft.CategoryId);
        if (created is not null) await LoadAsync();
    }

    [RelayCommand]
    private async Task AddToPlaylistAsync(SongDto song)
    {
        var playlists = await api.GetMyPlaylistsAsync();
        if (playlists.Count == 0)
        {
            await SongMenuHelper.ShowMessageDialogAsync("提示", "还没有自己的歌单，先创建一个吧。", "好的");
            return;
        }
        var names = playlists.Select(p => p.Name).ToList();
        var picked = await SongMenuHelper.ShowBottomSheetAsync($"收藏「{song.Title}」到歌单", names);
        var target = playlists.FirstOrDefault(p => p.Name == picked);
        if (target is null) return;
        await api.AddSongsToPlaylistAsync(target.Id, [song.Id]);
        await SongMenuHelper.ShowMessageDialogAsync("提示", $"已加入歌单「{target.Name}」。", "好的");
        await LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ToggleLikeAsync(SongDto song)
    {
        var ok = await api.LikeAsync(song.Id);
        if (ok) await SongMenuHelper.ShowMessageDialogAsync("提示", $"已喜欢「{song.Title}」。", "好的");
    }

    [RelayCommand]
    private void PlaySong(SongDto song)
    {
        if (player.Current?.Id == song.Id) { _ = Shell.Current.GoToAsync("nowplaying"); return; }
        var index = LikedSongs.IndexOf(song);
        player.PlayQueue(LikedSongs, index >= 0 ? index : 0, "我喜欢的音乐");
        _ = Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        // 与删除歌单同一套居中圆角确认框（退出是不可逆操作，确认键用危险色）
        var confirm = await SongMenuHelper.ShowRoundedConfirmAsync(
            "退出登录", "确定要退出当前账号吗？", "退出", "取消", destructive: true);
        if (!confirm) return;
        await auth.LogoutAsync();
        await Shell.Current.GoToAsync("///login");
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}

public partial class NowPlayingViewModel(PlayerService player, IMusicApi api, IAccentColorService accent) : ObservableObject
{
    public PlayerService Player { get; } = player;

    /// <summary>
    /// 当前封面的主色，播放页背景用它做动态着色。
    /// 取不到（无封面 / 图里没可用色彩 / 网络失败）时为 null，页面降级到默认深色。
    /// </summary>
    [ObservableProperty]
    private Color? accentColor;

    [ObservableProperty]
    private bool isLiked;

    /// <summary>
    /// 歌手关注状态（播放页歌手名旁的 + / ✓ 按钮）。
    /// **语义随歌手数量而变**：单歌手 = 那位是否已关注；联合创作 = **全部**都已关注（少一位就是 +）。
    /// </summary>
    [ObservableProperty]
    private bool isArtistFollowed;

    /// <summary>当前歌没有可操作的歌手（本地歌、离线缓存：歌手 Id 都是 0）时隐藏关注按钮。</summary>
    [ObservableProperty]
    private bool canFollowArtist;

    /// <summary>
    /// 当前歌里**有 ID 的**歌手（能跳详情页、能关注的那几位）。
    /// 每次切歌由 <see cref="RefreshArtistFollowedAsync"/> 重算；本地歌 / 离线缓存为空 → 按钮隐藏。
    /// </summary>
    private List<SongArtistRef> _followableArtists = [];

    /// <summary>当前用户已关注的歌手 ID 集合。与上面的列表配合判断每一位的状态，切歌/关注后更新。</summary>
    private HashSet<long> _followedArtistIds = [];

    /// <summary>
    /// 是否显示收藏按钮。本地歌（V2.6）**不显示** ——
    /// 它没有 songId，收藏要 PUT api/songs/{负数id}/like，服务端不认识必然失败；
    /// 而且本地歌本身就只在设备上，没有"收藏到账号"的语义。
    /// </summary>
    [ObservableProperty]
    private bool canLike;

    [RelayCommand]
    private async Task ToggleLikeAsync()
    {
        if (Player.Current is null) return;
        // 兜底拦截：按钮已隐藏，但命令仍可能被其他路径触发（如快捷键）。
        // 本地歌的 Id 是负数，发请求必然 404，且本地播放本来就不该碰服务端。
        if (Player.Current.IsLocal) return;
        if (IsLiked)
        {
            await api.UnlikeAsync(Player.Current.Id);
            IsLiked = false;
        }
        else
        {
            await api.LikeAsync(Player.Current.Id);
            IsLiked = true;
        }
    }

    public async Task RefreshIsLikedAsync()
    {
        if (Player.Current is null)
        {
            IsLiked = false;
            CanLike = false;
            return;
        }

        // 本地歌：直接隐藏收藏，不做任何网络请求（离线场景下请求也必然失败）
        if (Player.Current.IsLocal)
        {
            CanLike = false;
            IsLiked = false;
            return;
        }

        CanLike = true;
        try
        {
            var liked = await api.GetLikedSongsAsync();
            IsLiked = liked.Any(s => s.Id == Player.Current.Id);
        }
        catch { IsLiked = false; }
    }

    /// <summary>
    /// 播放页的快速关注按钮。
    /// <para>
    /// 单歌手：点一下直接关注 / 取消（图标切换即是反馈，不再弹提示）。
    /// 联合创作：弹出「关注歌手」列表 —— 关注哪位是用户的真实选择，一刀切"全都关注"会把不想看的歌手塞进他的关注列表。
    /// </para>
    /// <para>
    /// 弹层与请求都在 <see cref="SongArtistSheets.FollowAsync"/> 里（歌曲「更多」菜单用的是同一处实现），
    /// 这里只负责把结果同步到按钮状态。
    /// </para>
    /// </summary>
    [RelayCommand]
    private async Task ToggleFollowArtistAsync()
    {
        var credits = _followableArtists;
        if (credits.Count == 0) return;

        var outcome = await SongArtistSheets.FollowAsync(api, credits);
        if (outcome is null) return;   // 用户直接关掉了弹层

        // 播放页不弹提示：图标（✓ / +）就是反馈。这里只把"到底哪位关注上了"同步进本地缓存。
        var followed = outcome.FollowedIds;
        foreach (var artist in credits) _followedArtistIds.Remove(artist.Id);
        foreach (var id in followed) _followedArtistIds.Add(id);
        UpdateFollowState();
    }

    /// <summary>按钮图标的唯一判据：可操作歌手全都被关注才是 ✓。</summary>
    private void UpdateFollowState() =>
        IsArtistFollowed = _followableArtists.Count > 0
                           && _followableArtists.All(a => _followedArtistIds.Contains(a.Id));

    /// <summary>
    /// 按当前封面重算主色。由播放页在「进入页面」和「切歌」时调用，
    /// 不放构造函数里是因为主构造函数类没法写订阅语句，时机交给页面更清楚。
    /// </summary>
    public async Task RefreshAccentAsync()
    {
        try
        {
            AccentColor = await accent.ExtractAsync(Player.CoverUrl);
        }
        catch
        {
            AccentColor = null;      // 取色失败不影响播放页，降级到默认深色
        }
    }

    /// <summary>
    /// 切歌 / 进页面时重算"这首歌的歌手能不能关注、关注到什么程度"。
    /// <para>
    /// 只认有 ID 的歌手：本地歌（V2.6）与离线缓存索引里没有歌手 ID，按钮直接隐藏 ——
    /// 拿 Id = 0 去关注只会 404，不如不给这个动作。
    /// </para>
    /// </summary>
    public async Task RefreshArtistFollowedAsync()
    {
        _followableArtists = [.. SongArtistSheets.Followable(Player.Current)];
        CanFollowArtist = _followableArtists.Count > 0;

        if (_followableArtists.Count == 0)
        {
            _followedArtistIds = [];
            IsArtistFollowed = false;
            return;
        }

        try
        {
            _followedArtistIds = [.. await api.GetFollowedArtistIdsAsync()];
            UpdateFollowState();
        }
        catch
        {
            // 拿不到关注列表：按钮保持可点（用户点下去仍可能成功），状态先按"未关注"显示
            _followedArtistIds = [];
            UpdateFollowState();
        }
    }

    [RelayCommand]
    private void Toggle() => player.TogglePlayPause();

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private Task NextAsync() => player.NextAsync();

    [RelayCommand]
    private Task PreviousAsync() => player.PreviousAsync();

    [RelayCommand]
    private void SliderChanged(double value) => player.SeekTo(value);

    [RelayCommand]
    private void CyclePlayMode() => player.CyclePlayMode();
}