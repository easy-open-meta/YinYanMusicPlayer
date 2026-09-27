using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

public partial class HomeViewModel(IMusicApi api, PlayerService player) : ObservableObject
{
    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<SongDto> HotSongs { get; } = [];
    public ObservableCollection<PlaylistDto> Playlists { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    private bool _loaded;

    // ── V2.12 推荐歌单专区 ───────────────────────────────────────────────────

    /// <summary>
    /// 推荐场景标签。场景码 / 中文名一一配对（见 <see cref="RecommendSceneTab"/> 注释）。
    /// 「分区推荐」不在首页占标签位 —— 它需要先知道是哪个分区，入口在「音乐专区」那边，
    /// 首页给的是不依赖上下文的通用榜单。
    /// </summary>
    private static readonly RecommendSceneTab[] DefaultRecommendTabs =
    [
        new(RecommendScenes.Hot, "热播"),
        new(RecommendScenes.Collected, "高分收藏"),
        new(RecommendScenes.New, "最新上架"),
        new(RecommendScenes.ForYou, "猜你喜欢"),
    ];

    public ObservableCollection<RecommendSceneTab> RecommendTabs { get; } = [.. DefaultRecommendTabs];

    public ObservableCollection<PlaylistDto> RecommendedPlaylists { get; } = [];

    /// <summary>当前选中的场景标签。初始值写在这里（而不是构造里）：字段初始化器不会触发下面的 Changed 回调。</summary>
    [ObservableProperty]
    private RecommendSceneTab? selectedRecommendTab = DefaultRecommendTabs[0];

    /// <summary>推荐位自己的加载态。与整页的 <see cref="IsBusy"/> 分开：切标签只该让推荐位转圈，不该让整页消失。</summary>
    [ObservableProperty]
    private bool isLoadingRecommend;

    /// <summary>推荐位请求的自增序号，用于丢弃过期响应（见 <see cref="LoadRecommendedAsync"/>）。</summary>
    private int _recommendRequestId;

    /// <summary>切场景标签：只重拉推荐列表，不整页 Load（避免首页整块闪一下）。</summary>
    partial void OnSelectedRecommendTabChanged(RecommendSceneTab? value)
    {
        if (value is null) return;
        _ = LoadRecommendedAsync(value.Scene);
    }

    private async Task LoadRecommendedAsync(string scene)
    {
        // 连点标签会让多个请求并发在飞，晚到的旧响应会把新场景的结果盖掉，表现是"点了没反应/回的上一档"。
        // 用一个自增序号只认最后一次请求的结果 —— 与"继续播放"卡片用 CTS 接力防竞态是同一个思路，
        // 只是这里不需要取消 HTTP 请求，丢弃结果就够。
        var requestId = ++_recommendRequestId;
        IsLoadingRecommend = true;
        try
        {
            var result = await api.GetRecommendedPlaylistsAsync(scene, pageSize: 12);
            if (requestId != _recommendRequestId) return;   // 已经有更新的请求发出，本次结果作废

            // 系统歌单（如"我喜欢的音乐"）个人可见，不该出现在公开推荐位。
            // 服务端已排除，这里按 IsSystem 标志再兜一道 —— 与精选歌单列表同一套防御，不依赖歌单名。
            Replace(RecommendedPlaylists, result.Items.Where(p => !p.IsSystem));
        }
        catch
        {
            // 推荐位是锦上添花的区块：服务端未升级 / 离线 / 请求失败时保持空列表（走 XAML 空态文案），
            // 绝不能让首页的"热门歌曲""精选歌单"跟着一起失败
            if (requestId == _recommendRequestId) RecommendedPlaylists.Clear();
        }
        finally
        {
            if (requestId == _recommendRequestId) IsLoadingRecommend = false;
        }
    }

    [RelayCommand]
    private void SelectRecommendTab(RecommendSceneTab tab)
    {
        foreach (var item in RecommendTabs) item.IsSelected = ReferenceEquals(item, tab);
        // 值没变时 SelectedRecommendTab 不会触发 Changed 回调，那就手动拉一次
        // （用户重复点当前标签，期望的是"再刷一下"，而不是毫无反应）
        if (ReferenceEquals(SelectedRecommendTab, tab)) _ = LoadRecommendedAsync(tab.Scene);
        else SelectedRecommendTab = tab;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var cats = await api.GetCategoriesAsync();
            var songs = await api.SearchSongsAsync(pageSize: 20);
            var lists = await api.SearchPlaylistsAsync(pageSize: 10);

            Replace(Categories, cats);
            Replace(HotSongs, songs.Items);
            await SongMenuHelper.MarkLikedAsync(api, HotSongs);
            // 系统歌单（如“我喜欢的音乐”）个人可见，不应出现在公开精选歌单列表里
            // （服务端 SearchAsync 已排除，这里再按 IsSystem 标志兜底过滤，不依赖歌单名）
            Replace(Playlists, lists.Items.Where(p => !p.IsSystem));

            // V2.12 推荐位：跟着首页一起刷（Tab 来回切时数据会更新）。
            // 不走 SelectedRecommendTab 的 setter —— 值没变就不会触发 Changed，这里显式拉一次。
            await LoadRecommendedAsync(SelectedRecommendTab?.Scene ?? RecommendScenes.Hot);

            // V2.10：顺手拉一次"上次听到哪"。失败（未登录 / 离线）就静默不显示卡片，首页其余内容照常
            await LoadResumeAsync();

            _loaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── V2.10 断点续播：首页「继续播放」卡片 ──────────────────────────────────

    /// <summary>是否显示「继续播放」卡片。没有记录、或正在播的恰好就是那一首时不显示。</summary>
    [ObservableProperty]
    private bool hasResume;

    [ObservableProperty]
    private string resumeTitle = string.Empty;

    [ObservableProperty]
    private string resumeSubtitle = string.Empty;

    /// <summary>原样存服务端给的地址，由 XAML 的 AbsoluteUrl 转换器负责解析（data URI / 相对路径都能认）。</summary>
    [ObservableProperty]
    private string? resumeCoverUrl;

    /// <summary>上次听到的秒数，交给 SecondsToTime 转换器显示成 mm:ss。</summary>
    [ObservableProperty]
    private double resumeSeconds;

    private PlaybackProgressDto? _resume;

    private async Task LoadResumeAsync()
    {
        try
        {
            var last = await api.GetLastPlaybackAsync();

            // 正在播的就是这一首时不必再提示"继续播放" —— 入口和当下状态重复，卡片反而碍事
            var sameSongPlaying = last is not null && player.Current?.Id == last.SongId && player.IsPlaying;
            // V2.10 的语义是"**换设备**从上次的位置接着听"（设计文档 3.10.2）。进度若是本机自己报上去的，
            // 再弹一次"上次在「本机」听到"就是纯噪音，所以同设备不显示。
            var sameDevice = last is not null && IsSameDevice(last.DeviceName);
            if (last is null || sameSongPlaying || sameDevice)
            {
                _resume = null;
                HasResume = false;
                return;
            }

            _resume = last;
            ResumeTitle = last.Song.Title;
            // 设备来源（V2.12）：进度来自另一台设备时标注出来（"上次在 xxx 听到"）；
            // 老服务端 / 老记录没有设备名时退回"上次听到"。
            var source = string.IsNullOrWhiteSpace(last.DeviceName) ? null : $"上次在「{last.DeviceName}」听到";
            ResumeSubtitle = source is null
                ? $"{last.Song.ArtistsDisplay} · 上次听到"
                : $"{last.Song.ArtistsDisplay} · {source}";
            ResumeCoverUrl = last.Song.CoverUrl;
            ResumeSeconds = last.PositionSeconds;
            HasResume = true;

            // 5 秒后自动收起（V2.12）：卡片常驻首页顶部太占地方，提示到位就够了。
            // 只挂一次 —— 用 CancellationTokenSource 接力，连续 Load（Tab 来回切）时取消上一轮，
            // 不会出现"卡片刚重新显示就被上一轮的定时器收掉"的竞态。
            _autoDismissCts?.Cancel();
            _autoDismissCts = new CancellationTokenSource();
            var token = _autoDismissCts.Token;
            _ = AutoDismissAsync(token);
        }
        catch
        {
            _resume = null;
            HasResume = false;
        }
    }

    /// <summary>
    /// 进度记录里的设备名是否就是本机。两边用的都是上报时的同一个值
    /// （<see cref="IMusicApi.CurrentDeviceName"/>），不会出现"报的是 A、比的是 B"的漂移。
    /// <para>
    /// 空设备名（老记录 / 老服务端）一律当"不是本机"：判断不了就不能假定是同一台 ——
    /// 否则所有老记录都会把续播入口永远藏掉。
    /// </para>
    /// </summary>
    private bool IsSameDevice(string? recordedDeviceName)
        => !string.IsNullOrWhiteSpace(recordedDeviceName)
           && string.Equals(recordedDeviceName.Trim(), api.CurrentDeviceName.Trim(), StringComparison.OrdinalIgnoreCase);

    private CancellationTokenSource? _autoDismissCts;

    /// <summary>继续播放卡片的自动收起时长（V2.12）。</summary>
    private static readonly TimeSpan ResumeAutoDismiss = TimeSpan.FromSeconds(5);

    private async Task AutoDismissAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(ResumeAutoDismiss, token);
            if (token.IsCancellationRequested) return;
            HasResume = false;   // 到点收起；用户没点就是错过了，下次进首页会再次出现
        }
        catch (TaskCanceledException)
        {
            // 新一轮 Load 接管了计时，这轮作废 —— 正常路径
        }
    }

    /// <summary>
    /// 点「继续播放」：从服务端记的位置接着放。
    /// <para>
    /// 队列不能只放这一首 —— 全局约束 1.3 第 10 条：队列长度为 1 时"列表循环"的下一首等于它自己。
    /// 所以拿**这位歌手的歌**当上下文列表（续播那首必然在里面），拿不到就退化成单曲队列，
    /// 至少能续播，不至于点了没反应。
    /// </para>
    /// </summary>
    [RelayCommand]
    private async Task ResumeAsync()
    {
        var last = _resume;
        if (last is null) return;

        if (player.Current?.Id == last.SongId) { await GoNowPlayingAsync(); return; }

        var context = new List<SongDto> { last.Song };
        try
        {
            if (last.Song.ArtistId > 0)
            {
                var songs = await api.SearchSongsAsync(artistId: last.Song.ArtistId, pageSize: 50);
                if (songs.Items.Count > 0) context = [.. songs.Items];
            }
        }
        catch { /* 上下文拿不到不影响续播本身 */ }

        var index = context.FindIndex(s => s.Id == last.SongId);
        if (index < 0)
        {
            context.Insert(0, last.Song);
            index = 0;
        }

        player.PlayQueueAt(context, index, last.PositionSeconds, "继续播放");
        HasResume = false;   // 已经放到队列里了，卡片收起
        await GoNowPlayingAsync();
    }

    /// <summary>进入某个音乐专区的详情页。展示参数走查询串传，避免分区页二次请求分区信息。</summary>
    [RelayCommand]
    private Task OpenZoneAsync(CategoryDto zone) => Shell.Current.GoToAsync($"zone?{ZoneQuery.Build(zone)}");

    /// <summary>进入“全部专区”网格页。</summary>
    [RelayCommand]
    private Task OpenZonesAsync() => Shell.Current.GoToAsync("zones");

    [RelayCommand]
    private void PlayHot(SongDto song)
    {
        if (player.Current?.Id == song.Id) { _ = GoNowPlayingAsync(); return; }
        var index = HotSongs.IndexOf(song);
        player.PlayQueue(HotSongs, index >= 0 ? index : 0, "发现");
        _ = GoNowPlayingAsync();
    }

    [RelayCommand]
    private Task GoNowPlayingAsync() => Shell.Current.GoToAsync("nowplaying");

    [RelayCommand]
    private async Task OpenPlaylistAsync(PlaylistDto playlist) =>
        await Shell.Current.GoToAsync($"playlist?id={playlist.Id}");

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}