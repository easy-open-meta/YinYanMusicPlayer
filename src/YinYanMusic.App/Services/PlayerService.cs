using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Core.Views;   // FileMediaSource：Windows 上走 StorageFile/SetFileSource 路径，避开 URI 转义坑
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Maui.Views;
using System.Globalization;
using System.Text.Json;
using YinYanMusic.App.Services.LocalLibrary;
using YinYanMusic.Core.Dtos;
#if ANDROID
using CommunityToolkit.Maui.Core.Handlers;
using AndroidX.Media3.Common;
#endif

namespace YinYanMusic.App.Services;

public enum PlayMode { Sequential, ListLoop, SingleLoop, Random }

public partial class PlayerService(IMusicApi api) : ObservableObject
{
    /// <summary>
    /// 本地曲库（V2.6）。**故意用可选注入 + 延迟解析**：PlayerService 在 MauiProgram 里
    /// 是最早注册的单例之一，而 LocalLibraryStore 属于 V2.6 新增能力；
    /// 用 <see cref="ServiceHelper"/> 延迟取，避免构造顺序耦合导致启动期解析失败。
    /// 拿不到时（理论上不会）本地歌的计数静默跳过，播放本身不受影响。
    /// </summary>
    private LocalLibraryStore? LocalStore => _localStore ??= ServiceHelper.GetService<LocalLibraryStore>();
    private LocalLibraryStore? _localStore;

    /// <summary>
    /// 歌曲缓存（V2.7）。同样是**可选注入 + 延迟解析**：播放器在 MauiProgram 里注册得很早，
    /// 缓存属于 V2.7 新增能力，用 <see cref="ServiceHelper"/> 延迟取最稳。
    /// 第一次取到时会顺手把索引读进内存并订阅变更事件（决定"已缓存"标记与离线音源）。
    /// </summary>
    private CacheStore? Cache
    {
        get
        {
            if (_cache is null)
            {
                _cache = ServiceHelper.GetService<CacheStore>();
                if (_cache is not null && !_cacheSubscribed)
                {
                    _cacheSubscribed = true;
                    // 事件可能从下载线程/对账线程抛出，UI 标记只能在主线程改
                    _cache.CacheChanged += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshCurrentCachedFlag);
                    // 索引是同步判断（TryGetCached）的前提，启动后后台读一次
                    _ = _cache.EnsureIndexAsync();
                }
            }
            return _cache;
        }
    }
    private CacheStore? _cache;
    private bool _cacheSubscribed;

    private MediaElement? _player;

    // 已被 ReleaseMediaSession 释放过会话的备用内核。Media3 会话 release 后不可复用：
    // 这些元素再被接管时 BindPlayer 会跳过 set_Player（避免抛异常），播放走 MediaElement 照常。
    private readonly HashSet<MediaElement> _sessionReleased = [];
    private List<SongDto> _queue = [];
    private int _index = -1;
    private readonly Random _rng = new();
    private bool _mediaReady;
    // 播放意图：PlayAt 里的 Play() 在 Windows 上切 Source 时会丢失（新源打开后停在暂停态），
    // 需要在 MediaOpened 后补一次 Play。用户手动暂停/播放失败时清除，避免与用户意图打架。
    private bool _playPending;
    // 串行化守卫：上一轮切歌的 OnMediaOpened 还没回来时（_switching=true），
    // 新的切歌请求不立即重设 Source（否则 ExoPlayer 在异步换源途中被覆盖，半数源打不开、
    // 媒体会话在 PLAYING/PAUSED 间乱翻 —— 表现成"切歌后一会播放一会停止"）。
    // 只记最后想切的目标，等当前轮 OnMediaOpened 完成后再接着切，连点再快也严格串行。
    private bool _switching;
    private int? _pendingIndex;
    private bool _seeking;
    // V2.7：本首歌是否已经尝试过"远程失败 → 回退缓存"。一首歌只回退一次，避免
    // 缓存文件本身也坏掉时陷入「失败→回退→失败→回退」的无限循环。
    private bool _cacheFallbackTried;

    /// <summary>
    /// 长期存活的播放内核（`MainPage` 的 MediaElement）。页面级元素只在它为 null 时才接管 ——
    /// 页面元素会随页面销毁，若让它长期霸占播放器就会出现"进度条在动但没声音"（见 AttachPrimaryPlayer）。
    /// </summary>
    private MediaElement? _primaryPlayer;

    /// <summary>换内核（迁移播放）后待恢复的位置与播放状态，由 <see cref="OnMediaOpened"/> 消费一次。</summary>
    private ResumeState? _resumeAfterOpen;

    /// <summary>迁移播放用的状态快照。</summary>
    private sealed record ResumeState(double PositionSeconds, bool WasPlaying);
    private DateTime _lastPlayAtTime = DateTime.MinValue;
    private CancellationTokenSource? _timerCts;

    /// <summary>V2.10 心跳计数：500ms 的进度定时器每跳 60 次（30 秒）上报一次播放进度。</summary>
    private int _progressHeartbeatTicks;

    /// <summary>
    /// 登录态（V2.10 上报进度前先判断）。与 LocalStore / Cache 同一套**可选注入 + 延迟解析**：
    /// PlayerService 注册得早，直接构造注入 IAuthService 会把启动顺序搞得很脆。
    /// </summary>
    private IAuthService? Auth => _auth ??= ServiceHelper.GetService<IAuthService>();
    private IAuthService? _auth;

#if ANDROID
    private IPlayer? _exoPlayer;
    private CustomForwardingPlayer? _forwardingPlayer;
#endif

    static void LogInfo(string msg)
    {
#if ANDROID
        Android.Util.Log.Info("YinYan", msg);
#else
        // 非 Android 走统一出口：写了 YINYAN_LOG_FILE 时这条会落盘，Windows 端排障靠它
        AppLog.Info(msg);
#endif
    }

    static void LogWarn(string msg)
    {
#if ANDROID
        Android.Util.Log.Warn("YinYan", msg);
#else
        AppLog.Warn(msg);
#endif
    }

    public double VinylRotation { get; set; }

    /// <summary>
    /// 本地歌文件已失效（被删/被移走/外接盘不在）时触发（V2.6）。
    /// 参数是那首歌的 <see cref="SongDto"/>（<c>Id</c> 为负数）。
    /// 本地音乐页订阅它弹"移除记录"提示；播放器自身不删库。
    /// </summary>
    public event EventHandler<SongDto>? LocalFileMissing;

    private static string VolumeFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YinYanMusic", "volume.txt");
    private static string PlayModeFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YinYanMusic", "playmode.txt");
    private static string StateFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YinYanMusic", "player_state.json");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrent))]
    [NotifyPropertyChangedFor(nameof(CurrentTitle))]
    [NotifyPropertyChangedFor(nameof(CurrentArtist))]
    [NotifyPropertyChangedFor(nameof(CurrentArtists))]
    [NotifyPropertyChangedFor(nameof(CurrentArtistDisplay))]
    [NotifyPropertyChangedFor(nameof(CoverUrl))]
    [NotifyPropertyChangedFor(nameof(QualityLabel))]
    private SongDto? current;

    /// <summary>
    /// 当前这首歌在本机是否有缓存（V2.7）—— 播放页据此显示「已缓存」徽标（TC-2.7-02）。
    /// <para>语义是"本机存了这份音频"，与"这次播放用的是远程还是本地文件"无关：
    /// 有网时仍然优先播远程（TC-2.7-03），但徽标照常显示，用户才知道离线也听得到。</para>
    /// </summary>
    [ObservableProperty]
    private bool isCurrentCached;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private double positionSeconds;

    [ObservableProperty]
    private double durationSeconds = 1;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private double volume = LoadInitialVolume();

    /// <summary>
    /// 初始音量：Android 上从系统媒体音量读取（进度条一开始就反映系统音量）；
    /// 其他平台继续读 volume.txt（App 内软件音量）。
    /// </summary>
    private static double LoadInitialVolume()
    {
#if ANDROID
        try
        {
            var svc = ServiceHelper.GetService<ISystemVolumeService>();
            if (svc is not null) return svc.Volume;
        }
        catch { }
#endif
        return LoadSavedVolume();
    }

    // 系统音量联动（V2.16）：Android 走官方 AudioManager，见 ISystemVolumeService。
    // _systemVolumeUpdating 用于区分「用户拖进度条」与「系统音量变化回调」，防止回环。
    private ISystemVolumeService? _systemVolume;
    private bool _systemVolumeSubscribed;
    private bool _systemVolumeUpdating;

    private static double LoadSavedVolume()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YinYanMusic", "volume.txt");
            if (File.Exists(path)) return double.Parse(File.ReadAllText(path), CultureInfo.InvariantCulture);
        }
        catch { }
        return 1.0;
    }

    private static PlayMode LoadSavedPlayMode()
    {
        try
        {
            if (File.Exists(PlayModeFilePath) &&
                Enum.TryParse<PlayMode>(File.ReadAllText(PlayModeFilePath), out var saved))
                return saved;
        }
        catch { }
        return PlayMode.ListLoop;
    }

    [ObservableProperty]
    private PlayMode playMode = LoadSavedPlayMode();

    [ObservableProperty]
    private string sourceName = string.Empty;

    public bool HasCurrent => Current is not null;
    public string CurrentTitle => Current?.Title ?? "未在播放";
    public string CurrentArtist => Current?.ArtistName ?? string.Empty;

    /// <summary>
    /// 当前歌的**全部**歌手（带 ID，联合创作时不止一个；第一位是主歌手）。
    /// 播放页的"点歌手名 → 选哪一位""关注某一位"都靠它；只有名字是选不出正确的人的（同名歌手会认错）。
    /// <para>
    /// 本地歌（V2.6）与离线缓存的索引里没有歌手 ID，此时 <see cref="SongArtistRef.Id"/> 为 0 ——
    /// 界面应当**只显示、不提供跳转/关注**（离线本来也打不开歌手页）。
    /// </para>
    /// </summary>
    public IReadOnlyList<SongArtistRef> CurrentArtistCredits =>
        Current?.Artists is { Count: > 0 } list
            ? list
            : (string.IsNullOrEmpty(CurrentArtist) ? [] : [new SongArtistRef(Current?.ArtistId ?? 0, CurrentArtist)]);

    /// <summary>
    /// 当前歌的逐个**歌手名**（长按复制菜单要的）：复制整串拿去搜是搜不到的，用户要的通常是某一位。
    /// </summary>
    public IReadOnlyList<string> CurrentArtists => [.. CurrentArtistCredits.Select(a => a.Name)];

    /// <summary>
    /// 播放页 / 迷你条歌手那一行显示的内容：联合创作拼成 <c>Aimer / EGOIST</c>，单歌手就是那个名字。
    /// 刻意把"显示文案"和"逐个名字"分开 —— 长按复制菜单要的是后者（见 <see cref="CurrentArtists"/>），
    /// 这里只是给界面一行字（拼法与分隔符见 <see cref="SongDto.ArtistsDisplay"/>，只有那一处定义）。
    /// 太长时由播放页自行跑马灯滚动，迷你条与列表则截断。
    /// </summary>
    public string CurrentArtistDisplay => Current?.ArtistsDisplay ?? string.Empty;

    /// <summary>
    /// 当前歌曲所属专辑名。没有专辑（`AlbumId` 为空）时返回空字符串，
    /// 调用方据此隐藏「复制专辑名」这类选项（播放页的长按复制菜单）。
    /// </summary>
    public string CurrentAlbum => Current?.AlbumName ?? string.Empty;
    /// <summary>
    /// 当前封面地址。**原样返回**，不做 BaseUrl 拼接 ——
    /// 拼接交给 XAML 的 <c>AbsoluteUrl</c> 转换器，它认得本地路径（file://、content://、
    /// 绝对路径）并分别走 FromFile/FromStream，也认得 data URI（V2.5 头像）。
    /// 这里若先拼一次，本地歌封面会被拼成 <c>http://host/data/user/...</c> 这种假 URL，
    /// 转换器就再也救不回来了（真机实测：播放页封面空白）。
    /// </summary>
    public string CoverUrl => Current?.CoverUrl ?? string.Empty;

    /// <summary>
    /// 当前歌曲的音质标签：取音频文件扩展名（FLAC / MP3 / M4A …）。
    /// 播放页用来显示小标签；没有音频地址或扩展名异常时返回空字符串，标签会自动隐藏。
    ///
    /// <para>V2.6：本地歌的 <c>content://</c> URI 末段未必带扩展名，取不到就回退空串
    /// （标签自动隐藏），不会显示乱码。</para>
    /// </summary>
    public string QualityLabel
    {
        get
        {
            var url = Current?.AudioUrl;
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            // 去掉可能的 query/fragment 再取扩展名（URL 可能是转义过的，不影响扩展名）
            var ext = Path.GetExtension(url.Split('?', '#')[0]).TrimStart('.');
            return ext.Length is > 0 and <= 5 ? ext.ToUpperInvariant() : string.Empty;
        }
    }
    public IReadOnlyList<SongDto> Queue => _queue;
    public int CurrentIndex => _index;
    public string PlayModeIcon => PlayMode switch
    {
        PlayMode.Sequential => "\uE05F",
        PlayMode.ListLoop => "\uE028",
        PlayMode.SingleLoop => "\uE041",
        PlayMode.Random => "\uE043",
        _ => "\uE028"
    };

    partial void OnVolumeChanged(double value)
    {
        // 系统音量变化回调触发的属性更新不再回写系统，否则 ContentObserver → Volume →
        // OnVolumeChanged → SetStreamVolume 会形成无限循环。
        if (_systemVolumeUpdating) return;
        ApplyVolumeToPlayer();
        try
        {
            var dir = Path.GetDirectoryName(VolumeFilePath);
            if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(VolumeFilePath, value.ToString(CultureInfo.InvariantCulture));
        }
        catch { }
        _systemVolume?.SetVolume(value);
    }

    /// <summary>
    /// 把当前音量应用到播放器。**必须在 Handler 就绪后调用**才有效
    /// （见 <see cref="AttachPlayer"/> 里的说明）。切主线程：HandlerChanged 可能来自非 UI 线程。
    /// </summary>
    private void ApplyVolumeToPlayer()
    {
        var player = _player;
        if (player is null) return;
        try
        {
            var volume = GetPlayerVolume();
            if (MainThread.IsMainThread) player.Volume = volume;
            else MainThread.BeginInvokeOnMainThread(() => { try { player.Volume = volume; } catch { } });
        }
        catch { }
    }

    /// <summary>
    /// 实际写入 MediaElement 的增益：Android 上音量交给系统媒体音量（播放器固定 100%）；
    /// 其他平台仍用 App 内软件音量。
    /// </summary>
    private double GetPlayerVolume()
    {
#if ANDROID
        return 1.0;
#else
        return Volume;
#endif
    }

    /// <summary>
    /// 订阅系统音量变化（幂等）。放在播放器挂载路径上：App 启动后首次挂载播放内核即订阅，
    /// 之后按物理音量键 / 系统音量条，App 音量条都会实时跟随。
    /// </summary>
    private void EnsureSystemVolumeSubscribed()
    {
        if (_systemVolumeSubscribed) return;
        var svc = ServiceHelper.GetService<ISystemVolumeService>();
        if (svc is null) return;
        _systemVolume = svc;
        _systemVolumeSubscribed = true;
        svc.VolumeChanged += OnSystemVolumeChanged;
    }

    private void OnSystemVolumeChanged(object? sender, double value)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _systemVolumeUpdating = true;
            Volume = value;
            _systemVolumeUpdating = false;
        });
    }

    partial void OnPlayModeChanged(PlayMode value)
    {
        OnPropertyChanged(nameof(PlayModeIcon));
#if ANDROID
        ApplyPlayModeToExoPlayer();
#endif
        try
        {
            var dir = Path.GetDirectoryName(PlayModeFilePath);
            if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(PlayModeFilePath, value.ToString());
        }
        catch { }
    }

#if ANDROID
    private void ApplyPlayModeToExoPlayer()
    {
        if (_exoPlayer is null) return;
        try
        {
            // ExoPlayer 的时间线里永远只有当前一首歌（队列在 _queue 里，切歌靠 PlayAt 换源），
            // 因此这里不能用 REPEAT_ALL——"REPEAT_ALL + 单元素列表"会让同一首无限重播。
            // 只有单曲循环交给 ExoPlayer（REPEAT_ONE）；其余模式 REPEAT_OFF，播完停住，
            // 由 MediaEnded 事件驱动 NextAsync（该事件后台照常派发，不依赖被冻结的 UI 定时器）。
            _exoPlayer.RepeatMode = PlayMode == PlayMode.SingleLoop ? 1 : 0;
            _exoPlayer.ShuffleModeEnabled = PlayMode == PlayMode.Random;
            Android.Util.Log.Info("YinYan", $"ApplyPlayModeToExoPlayer: mode={PlayMode}, repeat={_exoPlayer.RepeatMode}, shuffle={_exoPlayer.ShuffleModeEnabled}");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Info("YinYan", $"ApplyPlayModeToExoPlayer failed: {ex.Message}");
        }
    }
#endif

    partial void OnIsPlayingChanged(bool value)
    {
        // V2.10：一旦从“播放中”转为“非播放”（暂停、被系统打断、报错停住），把进度报一次 ——
        // 跨设备续播靠的就是这一条，只靠心跳最多会差 30 秒。
        // ⚠️ “待定位”窗口里不报：那一刻 PositionSeconds 已被 PlayAt 清成 0，
        // 报上去会把刚从服务端取回来的续播点覆盖成 0（PlayQueueAt 与换内核迁移都走这个窗口）。
        if (!value && _resumeAfterOpen is null)
            _ = ReportPlaybackProgressAsync(Current, PositionSeconds);

#if ANDROID
        if (Current is null) return;
        // 切歌窗口（_mediaReady=false，新源尚未打开）里 PositionSeconds 可能仍是定时器
        // 轮询到的上一首位置，照实上报会让系统面板进度条残留旧歌进度，此窗口一律上报 0。
        var positionMs = _mediaReady ? (long)(PositionSeconds * 1000) : 0;
        MediaNotificationManager.Instance.UpdatePlaybackState(
            value, positionMs, (long)(DurationSeconds * 1000));
#endif
    }


    /// <summary>
    /// 登记**长期存活**的播放内核（`MainPage` 里那个 1x1 的 MediaElement）。
    ///
    /// <para><b>为什么要区分"内核"与"页面元素"</b>：二级页面自带 MediaElement 时，
    /// 页面一退出它就会被销毁、底层 ExoPlayer 被 Release，而 PlayerService 是单例、
    /// 手里还攥着那个已释放的实例 —— 之后每次播放都落到已销毁的元素上：
    /// **通知栏进度条照走，却完全没声音**（2026-09-22 真机实测踩到，触发路径就是
    /// "进过一次带 MediaElement 的二级页面（本地音乐 / 缓存管理）再返回，
    /// 之后播放在线歌只有进度没有声音"，日志里能看到
    /// <c>ExoPlayerImpl: Release</c> 之后紧跟 <c>Cannot access disposed object … IExoPlayerInvoker</c>）。
    /// 所以主内核一旦登记，页面元素就不再抢它。</para>
    /// </summary>
    public void AttachPrimaryPlayer(MediaElement player)
    {
        _primaryPlayer = player;
        if (ReferenceEquals(_player, player)) return;
        LogInfo("AttachPrimaryPlayer: 登记长期播放内核");
        BindPlayer(player, migrate: true);
    }

    /// <summary>
    /// 页面级接管：**只有当前已经有生效播放器时**才不接管（覆盖"未登录直达本地音乐/缓存管理"场景）。
    ///
    /// <para>⚠️ 判据必须是 <c>_player is not null</c>（当前生效的播放器），**不能**用
    /// <c>_primaryPlayer is not null</c>（内核登记标记）。2026-09-22 踩过：Activity 销毁重建时
    /// <see cref="DetachPlayer"/> 会把 <c>_player</c> 清成 null，而主内核元素未必重新 Load，
    /// 于是"登记标记还在、生效播放器没了"→ 页面元素被拒绝接管、主内核又不会自己回来，
    /// 结果就是**点什么歌都播不了**（日志：<c>PlayAt: _player 为空…未开始播放</c>）。</para>
    ///
    /// <para>⚠️ 不接管的分支必须**立刻释放该元素的 Media3 媒体会话**（见 <see cref="ReleaseMediaSession"/>）：
    /// 备用内核的空会话（state=NONE、metadata=null）与主内核的正会话并存时，
    /// Android 13+ 的系统媒体面板偶尔会选中空会话，通知栏面板随即丢封面、丢进度条、丢切歌按钮。</para>
    /// </summary>
    public void AttachPlayer(MediaElement player)
    {
        if (_player is not null)
        {
            if (!ReferenceEquals(_player, player))
                ReleaseMediaSession(player);
            LogInfo("AttachPlayer: 已有生效的播放内核，页面元素不接管（避免退出页面后播放器被销毁）");
            return;
        }

        // 没有生效播放器时才由页面补位（迁移播放状态，换内核不该打断听歌）
        BindPlayer(player, migrate: true);
    }

    /// <summary>
    /// 页面退出时归还播放器：若长期内核可用就把播放迁回内核并**续播**
    /// （同一首、同一位置、同一播放状态）；没有内核可还（未登录场景）时保持不动。
    /// </summary>
    public void ReleasePlayer(MediaElement player)
    {
        if (!ReferenceEquals(_player, player)) return;
        if (_primaryPlayer is null) return;   // 没有内核可还，只能继续用页面元素
        LogInfo("ReleasePlayer: 页面退出，播放迁回长期内核");
        BindPlayer(_primaryPlayer, migrate: true);
    }

    /// <summary>
    /// 真正接管一个 MediaElement：解绑旧的、绑事件、抓 ExoPlayer、起进度定时器、挂通知栏动作。
    /// <paramref name="migrate"/>=true 时把当前歌曲与进度搬到新元素上继续播
    /// （换内核必然要重新设 Source，所以只能"重载 + 定位 + 恢复播放状态"）。
    /// </summary>
    private void BindPlayer(MediaElement player, bool migrate)
    {
        // 同一个元素重复调用直接返回：否则事件会被重复绑定，MediaEnded 等会被触发多次
        if (ReferenceEquals(_player, player)) return;

        // V2.16 系统音量联动：首次挂载播放内核时订阅系统音量变化
        EnsureSystemVolumeSubscribed();

        // 迁移所需的状态必须在停掉旧播放器**之前**取出来
        var migrateSong = migrate && Current is not null && _queue.Count > 0;
        var migratePosition = PositionSeconds;
        var migrateWasPlaying = IsPlaying;

        if (_player is not null)
        {
            try { _player.Stop(); } catch { }
            UnbindPlayerEvents(_player);
            // 换内核后旧元素沦为备用内核，它的 Media3 会话必须跟着释放：
            // 留着就是一个带旧 metadata 的"僵尸会话"，同样会跟新内核的正会话抢系统媒体面板。
            var oldPlayer = _player;
            if (!ReferenceEquals(oldPlayer, _primaryPlayer))
                ReleaseMediaSession(oldPlayer);
        }

        _player = player;

#if ANDROID
        // Reflection is required to access MediaElementHandler's internal MediaManager,
        // which holds the ExoPlayer and Media3 MediaSession instances.
        // CommunityToolkit.Maui.MediaElement does not expose these as public API.
        // If a toolkit upgrade breaks this, update the member names below.
        // Failure degrades gracefully: notification next/prev buttons won't show,
        // but core playback via MediaElement continues to work.
        try
        {
            if (player.Handler is MediaElementHandler meh)
            {
                var mmProp = typeof(MediaElementHandler).GetProperty("MediaManager",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mmProp?.GetValue(meh) is { } mm)
                {
                    var playerProp = mm.GetType().GetProperty("Player",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    _exoPlayer = playerProp?.GetValue(mm) as IPlayer;
                    Android.Util.Log.Info("YinYan", $"AttachPlayer: _exoPlayer = {(_exoPlayer is null ? "null" : "OK")}, type={_exoPlayer?.GetType().FullName}");

                    var sessionField = mm.GetType().GetField("session",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (sessionField?.GetValue(mm) is { } sessionObj && _exoPlayer is not null)
                    {
                        if (_sessionReleased.Contains(player))
                        {
                            // 这个元素曾被当备用内核、其 Media3 会话已被 Release（Media3 会话
                            // release 后不可复用）。重新挂 ForwardingPlayer 会抛异常（被外层
                            // catch 吃掉，播放不受影响，但系统面板会退化成无切歌按钮）。
                            // 所以这里干脆明确跳过并留日志，别让异常混进正常路径。
                            Android.Util.Log.Info("YinYan",
                                "AttachPlayer: this element's Media3 session was released earlier (stale backup kernel); skipping set_Player");
                        }
                        else
                        {
                            var onNext = new Action(() => MainThread.BeginInvokeOnMainThread(async () => { try { await NextAsync(); } catch { } }));
                            var onPrev = new Action(() => MainThread.BeginInvokeOnMainThread(async () => { try { await PreviousAsync(); } catch { } }));
                            _forwardingPlayer = new CustomForwardingPlayer(_exoPlayer, onNext, onPrev);

                            var setPlayerMethod = sessionObj.GetType().GetMethod("set_Player",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            setPlayerMethod?.Invoke(sessionObj, new object[] { _forwardingPlayer });
                            Android.Util.Log.Info("YinYan", $"AttachPlayer: ForwardingPlayer set on session, setPlayer={setPlayerMethod?.Name}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("YinYan", $"AttachPlayer: reflection failed, degrading without ExoPlayer/ForwardingPlayer: {ex.Message}");
        }
#endif

        player.Volume = GetPlayerVolume();
        // ⚠️ 上面这行在 Handler 还没创建时是**无效**的：MediaElement 的音量最终要落到
        // ExoPlayer 的 audio attributes / 原生播放器上，而那时原生对象尚不存在，
        // 赋值被静默丢弃。结果就是"保存的音量永远不生效"，用户会以为播放器坏了（没声音）。
        // 真机实测：volume.txt 里存着 0.10，播放时输出被压到 10%，听起来完全无声。
        // 这里在 Handler 就绪后补一次；HandlerChanged 在 Android 上可能触发多次，
        // 每次重新赋值是幂等的，无副作用。
        player.HandlerChanged += (_, _) =>
        {
            ApplyVolumeToPlayer();
            // 元素脱离视觉树（Handler 变 null）= 这个内核已经销毁。必须立刻放掉对它的引用，
            // 否则后续播放会写到一个已释放的播放器上 —— 表现正是"进度条在动但没声音"。
            if (player.Handler is null) OnPlayerElementGone(player);
        };

        player.MediaOpened += OnMediaOpened;
        player.StateChanged += OnStateChanged;
        player.MediaFailed += OnMediaFailed;
        player.MediaEnded += OnMediaEnded;

        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = new CancellationTokenSource();
        var cts = _timerCts;
        Device.StartTimer(TimeSpan.FromMilliseconds(500), () =>
        {
            if (cts.IsCancellationRequested) return false;
            if (_player is not null)
            {
                try
                {
                    PositionSeconds = _player.Position.TotalSeconds;
                    var dur = _player.Duration.TotalSeconds;
                    if (dur > 1 && (Current is null || Current.DurationSeconds <= 0))
                    {
                        DurationSeconds = dur;
                    }
                    // 此处曾有一段"位置跳回开头即手动 NextAsync"的循环检测 hack，
                    // 用于抵消 ExoPlayer REPEAT_ALL 在单元素列表上的无限重播。
                    // 现在非单曲循环统一 REPEAT_OFF，播完由 MediaEnded 事件驱动切歌
                    //（事件不依赖本定时器，后台照常派发），hack 已删除。
                }
                catch { }
            }
            // V2.10：播放中每 30 秒（60 跳）上报一次进度；暂停期间不累计
            if (IsPlaying)
            {
                _progressHeartbeatTicks++;
                if (_progressHeartbeatTicks >= 60)
                {
                    _progressHeartbeatTicks = 0;
                    _ = ReportPlaybackProgressAsync(Current, PositionSeconds);
                }
            }
            else _progressHeartbeatTicks = 0;
            return !cts.IsCancellationRequested;
        });

#if ANDROID
        MediaNotificationManager.Instance.Init();
        MediaNotificationManager.Instance.PlayAction = () => MainThread.BeginInvokeOnMainThread(TogglePlayPause);
        MediaNotificationManager.Instance.PauseAction = () => MainThread.BeginInvokeOnMainThread(TogglePlayPause);
        MediaNotificationManager.Instance.NextAction = () => MainThread.BeginInvokeOnMainThread(async () => { try { await NextAsync(); } catch { } });
        MediaNotificationManager.Instance.PreviousAction = () => MainThread.BeginInvokeOnMainThread(async () => { try { await PreviousAsync(); } catch { } });
        MediaNotificationManager.Instance.StopAction = () => MainThread.BeginInvokeOnMainThread(() => { try { _player?.Pause(); } catch { } });
        MediaNotificationManager.Instance.SeekAction = (posMs) => MainThread.BeginInvokeOnMainThread(() => SeekTo(posMs / 1000.0));
#endif

        // 迁移：把正在播的歌重新装到新内核上（位置与播放/暂停状态由 OnMediaOpened 恢复）。
        // PlayAt 会走一遍正常的音源决策（本地 / 缓存 / 远程），所以换内核后依然遵守
        // "有网优先远程、无网走缓存"的规则。
        if (migrateSong)
        {
            _resumeAfterOpen = new ResumeState(migratePosition, migrateWasPlaying);
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // skipReport：重载同一首歌不算新的一次播放，避免把播放次数刷高
                try { PlayAt(_index, skipReport: true); }
                catch (Exception ex) { LogWarn($"BindPlayer: 迁移播放失败 {ex.Message}"); }
            });
        }
    }

    /// <summary>
    /// 释放一个 MediaElement 的 Media3 媒体会话（反射取 Toolkit MediaManager 的 session 字段）。
    ///
    /// <para><b>为什么要释放</b>：CommunityToolkit 的 MediaElement 在 Handler 就绪时就会注册一个
    /// 系统级 Media3 MediaSession。页面级"备用内核"（本地音乐页 / 缓存管理页，已登录时从不接管播放）
    /// 的会话永远停留在 <c>state=NONE、metadata=null</c> 的空壳状态 —— 它与主内核的正会话并存时
    /// （真机 <c>dumpsys media_session</c> 抓到三个会话并存），Android 13+ 的系统媒体面板偶尔会
    /// 选中这个空会话，通知栏媒体面板随即丢封面、丢进度条、丢切歌按钮（2026-09-22 用户真机反馈，
    /// 触发路径就是"整首听完自动切下一首"时面板对会话的重新评估）。</para>
    ///
    /// <para><b>代价</b>：Media3 会话 release 后不可复用。这些元素若日后真的被接管
    /// （未登录直达 → 主内核失效的极端链路），<see cref="BindPlayer"/> 会跳过 set_Player、
    /// 降级为"系统面板无切歌按钮"，播放本身不受影响 —— 比"面板随机显示空壳"值得。</para>
    /// </summary>
    private void ReleaseMediaSession(MediaElement player)
    {
#if ANDROID
        try
        {
            if (_sessionReleased.Contains(player)) return;

            if (player.Handler is null)
            {
                // Handler 还没就绪（OnNavigatedTo 时通常已就绪，这里防御一下）：
                // 空会话正是 Handler 就绪那一刻产生的，所以挂一次性回调等它就绪再释放。
                void OnHandlerReady(object? s, EventArgs e)
                {
                    player.HandlerChanged -= OnHandlerReady;
                    if (player.Handler is not null && !ReferenceEquals(_player, player))
                        ReleaseMediaSession(player);
                }
                player.HandlerChanged += OnHandlerReady;
                return;
            }

            if (player.Handler is not MediaElementHandler meh) return;
            var mm = typeof(MediaElementHandler).GetProperty("MediaManager",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(meh);
            if (mm is null) return;

            if (mm.GetType().GetField("session",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(mm) is not AndroidX.Media3.Session.MediaSession session) return;

            session.Release();
            _sessionReleased.Add(player);
            Android.Util.Log.Info("YinYan", "ReleaseMediaSession: released stale Media3 session of backup kernel");
        }
        catch (Exception ex)
        {
            // 释放失败只是"多留一个空会话"（回到旧行为），绝不能影响播放
            Android.Util.Log.Warn("YinYan", $"ReleaseMediaSession failed: {ex.Message}");
        }
#else
        _ = player;
#endif
    }

    /// <summary>
    /// 播放元素已销毁（Handler 变 null）时的清理：放掉对它的引用并清空"已就绪"状态。
    ///
    /// <para>做这件事的理由：旧代码在元素销毁后仍把 <c>_player</c> / <c>_exoPlayer</c>
    /// 留在字段里，于是后续播放写到一个**已释放**的播放器上——不报错、不出声，
    /// 只有系统通知栏还在按旧状态画进度。清空引用后，行为至少是可解释的
    /// （要么由下次 <see cref="AttachPrimaryPlayer"/> / <see cref="AttachPlayer"/> 重新接管，
    /// 要么明确报"播放器未初始化"，而不是静默无声）。</para>
    /// </summary>
    private void OnPlayerElementGone(MediaElement player)
    {
        if (ReferenceEquals(_primaryPlayer, player)) _primaryPlayer = null;

        // 元素已销毁，别再让它挂在"会话已释放"集合里（单例服务会把它一直攥着不放）
        _sessionReleased.Remove(player);

        if (!ReferenceEquals(_player, player)) return;

        LogWarn("OnPlayerElementGone: 当前播放元素已销毁，已释放引用（等待下次接管）");
        _player = null;
        _mediaReady = false;
        _playPending = false;
        IsPlaying = false;
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;
#if ANDROID
        _forwardingPlayer = null;
        _exoPlayer = null;
#endif
    }

    /// <summary>
    /// 解绑 MediaElement 事件、停止进度定时器，并释放对播放器与 ExoPlayer 的引用。
    /// 只在播放器确实不再需要时调用（例如应用退出）；导航离开页面时不能调用，因为播放要继续。
    /// 调用后仍可再次 <see cref="AttachPlayer"/> 重新接管。
    /// </summary>
    public void DetachPlayer()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;

        if (_player is not null)
        {
            UnbindPlayerEvents(_player);
            _player = null;
        }

        // ⚠️ 这里的 Detach 意味着"内核不再可用"，登记标记必须一起清掉：
        // 留着它会让 AttachPlayer 误判"内核还在"而拒绝页面补位，变成谁都播不了的死锁
        // （2026-09-22 真机回归：Activity 销毁 → DetachPlayer → 之后点歌全部 _player 为空）。
        _primaryPlayer = null;

        _mediaReady = false;
        _playPending = false;
#if ANDROID
        _forwardingPlayer = null;
        _exoPlayer = null;
#endif
        LogInfo("DetachPlayer: 已解绑事件并停止进度定时器");
    }

    private void UnbindPlayerEvents(MediaElement player)
    {
        player.MediaOpened -= OnMediaOpened;
        player.StateChanged -= OnStateChanged;
        player.MediaFailed -= OnMediaFailed;
        player.MediaEnded -= OnMediaEnded;
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        // "点了没反应"的现场基本靠这条判断：Source 有没有真的打开、打开后的时长是多少
        // （时长 0 的常见原因是格式不被平台解码器支持，Windows 上 FLAC 尤其容易踩到）。
        LogInfo($"OnMediaOpened: {Current?.Title ?? "?"} duration={(sender as MediaElement)?.Duration.TotalSeconds ?? -1}s");
        _mediaReady = true;
        // Windows 平台切歌自动播放的关键：Source 切换时紧跟的 Play() 会丢，这里在新源打开后补放。
        // 若已在播放（其他平台/首次播放），Play() 是幂等的，无副作用。
        if (_playPending)
        {
            _playPending = false;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { _player?.Play(); } catch { }
            });
        }

        // 换内核后的续播：设完 Source 再定位回原位，并恢复原来的播放/暂停状态。
        // 放在 _playPending 之后入队，保证"迁移前是暂停态"时最终停在暂停态。
        var resume = _resumeAfterOpen;
        if (resume is not null)
        {
            _resumeAfterOpen = null;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (resume.PositionSeconds > 1)
                        _player?.SeekTo(TimeSpan.FromSeconds(resume.PositionSeconds), CancellationToken.None);
                    if (resume.WasPlaying) _player?.Play();
                    else _player?.Pause();
                    LogInfo($"OnMediaOpened: 换内核续播 position={resume.PositionSeconds:F1}s playing={resume.WasPlaying}");
                }
                catch (Exception ex)
                {
                    LogWarn($"OnMediaOpened: 续播失败 {ex.Message}");
                }
            });
        }
        try
        {
            var dur = _player?.Duration.TotalSeconds ?? 0;
            if (dur > 0)
            {
                // 偏差日志：API 有效且与实际偏差>5s 时记录，供后端元数据治理
                if (Current is not null && Current.DurationSeconds > 0 && Math.Abs(dur - Current.DurationSeconds) > 5)
                    LogWarn($"时长偏差: API={Current.DurationSeconds}s, 实际={dur:F0}s, 歌曲={Current.Title}");
                // 守卫：仅 API 时长缺失(<=0)才用实际时长回填，API 有效时保持权威值
                if (Current is null || Current.DurationSeconds <= 0)
                {
                    DurationSeconds = dur;
                    if (Current is not null)
                        // 与进度条显示同口径：向下取整，避免列表 3:01 / 播放页 3:00 的 1 秒偏差
                        Current.DurationSeconds = (int)dur;
                }
            }
        }
        catch { }

        // 串行化收尾：本轮切歌已打开，解除"切换中"。若有被推迟的目标，立刻接着切——
        // 连点时只保留最后一个目标，所以连点多快都严格串行、且最终结果一定生效。
        _switching = false;
        if (_pendingIndex.HasValue)
        {
            var next = _pendingIndex.Value;
            _pendingIndex = null;
            LogInfo($"PlayAt: 续切推迟的目标 index={next}");
            PlayAt(next);
        }
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        IsPlaying = e.NewState == MediaElementState.Playing;
        if (e.NewState == MediaElementState.Playing)
        {
            _mediaReady = true;
            _playPending = false;
            try
            {
                var dur = _player?.Duration.TotalSeconds ?? 0;
                if (dur > 1)
                {
                    if (Current is not null && Current.DurationSeconds > 0 && Math.Abs(dur - Current.DurationSeconds) > 5)
                        LogWarn($"时长偏差: API={Current.DurationSeconds}s, 实际={dur:F0}s, 歌曲={Current.Title}");
                    if (Current is null || Current.DurationSeconds <= 0)
                    {
                        DurationSeconds = dur;
                        if (Current is not null)
                            // 与进度条显示同口径：向下取整
                            Current.DurationSeconds = (int)dur;
                    }
                }
            }
            catch { }
        }
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        IsPlaying = false;
        _mediaReady = false;
        _playPending = false;
        ErrorMessage = e.ErrorMessage;
        // 之前这里不落日志，导致"点了没反应"完全无从排查（ErrorMessage 也没在 UI 上显示）
        LogWarn($"MediaFailed: {e.ErrorMessage}");

        // 串行化守卫收尾：源打开失败时 OnMediaOpened 不会触发，若不清掉 _switching，
        // 后续所有 PlayAt 都会被"推迟"进 _pendingIndex 而永久卡死（点了没反应）。
        // 这里必须手动解除切换中；推迟的目标通常也会同样失败，直接丢弃，避免无意义的连锁重试。
        _switching = false;
        _pendingIndex = null;

        // V2.7：远程源失败 + 本机有缓存 → 回退到缓存文件再播一次（每次失败只回退一次）
        TryFallbackToCache();
    }

    // async void 事件处理器：异常若逃逸会直接崩溃应用，必须在这里兜住
    private async void OnMediaEnded(object? sender, EventArgs e)
    {
        try
        {
            await HandleMediaEndedAsync();
        }
        catch (Exception ex)
        {
            LogWarn($"OnMediaEnded failed: {ex.Message}");
        }
    }

    private async Task HandleMediaEndedAsync()
    {
        var elapsedSincePlayAt = DateTime.UtcNow - _lastPlayAtTime;
        if (elapsedSincePlayAt < TimeSpan.FromSeconds(1))
        {
            LogWarn($"HandleMediaEndedAsync: spurious MediaEnded {elapsedSincePlayAt.TotalMilliseconds:F0}ms after PlayAt, recovering");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { _player?.SeekTo(TimeSpan.Zero, CancellationToken.None); _player?.Play(); } catch { }
            });
            return;
        }
        _mediaReady = false;
        LogInfo($"HandleMediaEndedAsync: PlayMode={PlayMode}, _index={_index}, queueCount={_queue.Count}");

        // V2.7 增强：整首听完 → 后台自动缓存（仅 WiFi、撞上限静默跳过、绝不打断播放）。
        // 必须在切歌之前调用：下面 NextAsync/PlayAt 会改掉 Current，那时就不知道是哪首歌了。
        TryAutoCacheFinishedSong();

        // V2.11：播放结束也是补报触发点之一（听完整首往往意味着刚离线听完一批，
        // 顺手试一轮；flusher 内部有退避节流，联网状态下它自己会跳过空队列）。
        _ = ServiceHelper.GetService<PlayReportFlusher>()?.FlushAsync("播放结束");

        switch (PlayMode)
        {
            case PlayMode.SingleLoop:
                if (Current is not null)
                    await MainThread.InvokeOnMainThreadAsync(() => PlayAt(_index));
                break;
            default:
                await NextAsync();
                break;
        }
    }


    public void PlayQueue(IReadOnlyList<SongDto> songs, int startIndex, string? sourceName = null)
    {
        if (songs.Count == 0) return;
        _queue = [.. songs];
        if (sourceName is not null) SourceName = sourceName;
        PlayAt(startIndex);
    }

    /// <summary>
    /// 播放指定歌曲。ATL 解析失败（时长未知）的歌曲自动跳过：
    /// skipStep=+1 时向后跳（下一首/点选），-1 时向前跳（上一首）；整队都无效则放弃播放。
    /// </summary>
    /// <param name="skipReport">
    /// 跳过播放计数与上报。**只给"换播放内核后重载同一首歌"用**（见 <see cref="BindPlayer"/>）：
    /// 那不是一次新的播放，若照常上报会把播放次数刷高（V2.4 的统计口径就失真了）。
    /// </param>
    public void PlayAt(int index, int skipStep = 1, bool skipReport = false)
    {
        if (_queue.Count == 0) return;
        index = ((index % _queue.Count) + _queue.Count) % _queue.Count;

        // 跳过无时长的歌（时长为 0 = 服务端 ATL 解析失败，播放必然无声）
        var visited = 0;
        while (_queue[index].DurationSeconds <= 0 && visited < _queue.Count)
        {
            LogWarn($"PlayAt: 跳过无时长的歌曲 index={index} ({_queue[index].Title})");
            index = ((index + skipStep) % _queue.Count + _queue.Count) % _queue.Count;
            visited++;
        }
        if (visited >= _queue.Count)
        {
            LogWarn($"PlayAt: 队列中所有歌曲均无时长，放弃播放");
            return;
        }

        // 串行化：上一轮切歌还在切换中（OnMediaOpened 未回），先把目标记下，
        // 不重设 Source —— 避免 ExoPlayer 换源竞态导致的播放/暂停抖动。最后的目标一定生效。
        if (_switching)
        {
            _pendingIndex = index;
            LogInfo($"PlayAt: 切换中，推迟到 index={index}（当前 _index={_index}）");
            return;
        }

        // V2.10：切歌前先把“上一首听到哪”报一次 —— 不报的话那首歌的进度就丢在这次切换里了
        var outgoing = Current;
        if (outgoing is not null && outgoing.Id != _queue[index].Id)
            _ = ReportPlaybackProgressAsync(outgoing, PositionSeconds);

        _lastPlayAtTime = DateTime.UtcNow;
        LogInfo($"PlayAt: called index={index}, queueCount={_queue.Count}");
        _index = index;

        // 兜底：二级页面的 MediaElement 销毁后（OnPlayerElementGone 清空了 _player），
        // 若此时没人来补位（用户还停在首页），播放会一直报"_player 为空"。
        // 这里在真正播放前把内核挂回 MainPage 那个长期存活的元素 —— 它才是 App 生命周期内的主内核。
        if (_player is null && _primaryPlayer is not null)
        {
            LogWarn("PlayAt: 播放内核缺失，尝试挂回主内核");
            BindPlayer(_primaryPlayer, migrate: false);
        }

        var song = _queue[index];
        Current = song;
        PositionSeconds = 0;
        _mediaReady = false;
        DurationSeconds = song.DurationSeconds > 0 ? song.DurationSeconds : 1;
        ErrorMessage = string.Empty;

        // 本地歌（V2.6）：播放前先确认文件还在。
        // 用户在文件管理器里删了歌、或 SD 卡拔了，都会走到这里——
        // 明确报"文件不存在"比静默失败好（TC-2.6-08）。
        if (song.IsLocal && !LocalFileAccess.Exists(song.AudioUrl))
        {
            IsPlaying = false;
            _playPending = false;
            ErrorMessage = "文件不存在，可能已被移动或删除";
            LogWarn($"PlayAt: 本地文件已失效，跳过播放 id={song.Id} ({song.Title})");
            // 抛给 UI 层（本地音乐页订阅后弹"移除"提示）。
            // 播放器自身**不擅自删库**——用户可能只是外接盘没插上。
            LocalFileMissing?.Invoke(this, song);
            return;
        }

        // ── V2.7 音源决策：有网优先远程，无网走本地缓存 ─────────────────────
        // 语义（设计文档 §3.7.2）：
        //   · 有网 → 一律播远程，保证听到的是服务端最新版本（TC-2.7-03）；
        //   · 无网 + 有缓存 → 播本地缓存文件，离线也能听（TC-2.7-02）；
        //   · 无网 + 没缓存 → 照旧播远程，失败由 MediaFailed 报出来（用户会看到提示）。
        // 注意判的是"当前网络不可用"，不是"远程能不能通"——探测远程可达性要发请求，
        // 放在播放路径上会拖慢点歌；真的连不上时 OnMediaFailed 的回退会兜住（见 TryFallbackToCache）。
        var cachedEntry = song.IsLocal ? null : Cache?.TryGetCached(song.Id);
        var playFromCache = cachedEntry is not null && IsOffline() && LocalFileAccess.Exists(cachedEntry.FilePath);
        _cacheFallbackTried = false;
        IsCurrentCached = cachedEntry is not null;

        if (cachedEntry is not null && IsOffline() && !playFromCache)
            LogWarn($"PlayAt: 离线且缓存文件已丢失 id={song.Id}，退回远程播放");

        if (_player is null)
        {
            ErrorMessage = "播放器未初始化";
            _playPending = false;
            LogWarn("PlayAt: _player 为空（MediaElement 未绑定），未开始播放");
            return;
        }

        // 走到真正换源这一步才标记"切换中"：前面的早返回（无时长/文件失效/内核未初始化）
        // 都不应留下 _switching=true，否则会卡死后续所有 PlayAt。换源期间由串行化守卫保护竞态。
        _switching = true;

        try
        {
            _player.MetadataTitle = song.Title;
            _player.MetadataArtist = song.ArtistsDisplay;
            // ⚠️ 无封面时必须显式赋空串：跳过赋值会让 Toolkit 内部残留上一首的
            // ArtworkUrl，Media3 会话（系统控制中心绑定的会话）就一直是上一首的封面
            // （真机复现：有封面→无封面，面板封面不刷新）。空串会触发 Toolkit
            // 用空元数据重建，封面正确清空。
            // ApiConfig.Absolute 对 null/空白返回空串，所以这里直接赋它就同时满足了这一条。
            //
            // 这里原先有个 `song.IsLocal ? 原样 : Absolute(...)` 的补丁分支，用来绕开
            // "本地封面是裸 Unix 路径、会被拼成假 URL"。V2.13 把判据做进了 Absolute 本身
            // （见 Core/MediaAddress），本地路径原样返回，补丁分支已无必要 —— 一并删掉，
            // 免得读者以为本地封面还得靠这处特判。
            _player.MetadataArtworkUrl = ApiConfig.Absolute(song.CoverUrl);
            var url = ApiConfig.Absolute(song.AudioUrl);
#if ANDROID
            MediaNotificationManager.Instance.UpdateMetadata(
                // 歌手用"全部歌手"（联合创作 → "Aimer / EGOIST"），与播放页保持一致；
                // 系统面板/锁屏自己会截断，不会因为名字长而排版错乱。
                song.Title,
                song.ArtistsDisplay,
                (long)(DurationSeconds * 1000),
                // 原样传，**不要** ApiConfig.Absolute：本地封面会被拼成
                // http://host/data/user/... 这种假 URL，通知栏必然加载失败。
                // MediaNotificationManager 内部用 ImageSourceFactory.ReadBytesAsync 处理各形态。
                string.IsNullOrEmpty(song.CoverUrl) ? null : song.CoverUrl);
            MediaNotificationManager.Instance.UpdatePlaybackState(
                true, 0, (long)(DurationSeconds * 1000));
            // 同步触发 ExoPlayer position=0 discontinuity，经 ForwardingPlayer 基类事件转发
            // 传播给 Media3 MediaSession（Android 13+ 系统面板实际绑定的会话），
            // 系统面板进度条立即归零。不依赖换源事件的异步传播时序（spec 3.3 根因）。
            // 不调 Pause：避免 PAUSED→PLAYING 状态闪烁；旧歌 position=0 的瞬态由紧随的
            // 换源立即 Stop 覆盖，无感知。不调 base.SeekToNext：单元素列表有 STATE_ENDED 副作用。
            try { _exoPlayer?.SeekTo(0); }
            catch (Exception ex) { Android.Util.Log.Warn("YinYan", $"PlayAt: SeekTo(0) discontinuity failed: {ex.Message}"); }
#endif
            System.Diagnostics.Debug.WriteLine($"[播放] {(playFromCache ? cachedEntry!.FilePath : url)}");
            // 换源前必须先 Stop：Toolkit 的 Android 实现给 Source 赋新值时要经历
            // "异步初始化新数据源 → 才销毁旧播放器" 的过程，期间旧解码缓冲继续出声
            // （真机实测旧歌多播 ~2.4s，通知栏切歌时尤其明显）。显式 Stop 立即
            // pause+flush 旧 AudioTrack，旧声当场掐断。Stop 后 MediaElement 回到
            // 初始态，随后的 Source 赋值等价于全新装载，MediaOpened 照常回调。
            try { _player.Stop(); } catch (Exception ex) { LogWarn($"PlayAt: Stop before switch failed: {ex.Message}"); }
            _playPending = true;   // 播放意图：若紧随的 Play() 在切源时被 Windows 丢弃，MediaOpened 后会补放

            // 本地源（file:// / 绝对路径 / Android content://）必须走 Uri：
            // 这些路径由 ApiConfig.Absolute 原样放行，再由 LocalFileAccess 规范成可播放 Uri。
            // （绝对文件路径直接 new Uri(path) 会自动补 file:// 前缀。）
            // V2.7：缓存歌走的是同一条本地分支，只是文件来自缓存目录而非用户设备曲库。
            if (song.IsLocal || playFromCache)
            {
                var localPath = song.IsLocal ? song.AudioUrl : cachedEntry!.FilePath;
                var localUri = LocalFileAccess.ToPlayableUri(localPath);
                if (localUri is null)
                {
                    IsPlaying = false;
                    _playPending = false;
                    ErrorMessage = "无法解析本地文件路径";
                    LogWarn($"PlayAt: 本地路径无法解析为 Uri: {localPath}");
                    // 与 catch 分支同理：本轮切歌已失败，必须解除"切换中"，
                    // 否则后续 PlayAt 全部被推迟进 _pendingIndex，切歌永久卡死。
                    _switching = false;
                    _pendingIndex = null;
                    return;
                }
                LogInfo($"PlayAt: 本地音源 path={localPath} → uri={localUri}");
                // Windows 上必须走 FileMediaSource：CommunityToolkit MediaElement 在 Windows 上对
                // UriMediaSource 只会走 MediaPlayer.SetUriSource(file://...)，而 .NET Uri 对本地路径
                // 里的空格/括号是保留不转义的，MediaPlayer 解不开这种 URI 就静默失败（无 MediaFailed、
                // 无 MediaOpened，"点了不播"）。FileMediaSource → StorageFile.GetFileFromPathAsync
                // → SetFileSource，用 Win32 路径直接打开，不踩 URI 转义坑。
                // Android / iOS 没有 SetUriSource 这个坑，保持 Uri 走原逻辑。
#if WINDOWS
                _player.Source = new FileMediaSource { Path = localPath };
#else
                _player.Source = localUri;
#endif
            }
         else
        {
            LogInfo($"PlayAt: 远程音源 url={url}");
            _player.Source = new Uri(url);
        }
#if WINDOWS
            // Windows：Source 切换时紧跟的 Play() 会丢，靠 MediaOpened 后的 _playPending 补放。
            _player.Play();
#else
            // Android：换源是异步的（ExoPlayer 先走 IDLE→BUFFERING→READY），此刻 Play() 要么被丢、
            // 要么在缓冲完成前就处于错误时序，最终仍要等 OnMediaOpened 补放——实测这个"立刻 Play"
            // 只会把首次出声拖慢（真机 +1.7s）。Android 统一走 MediaOpened 补放，这里不调。
#endif
#if ANDROID
            if (_exoPlayer is not null)
            {
                try
                {
                    _exoPlayer.RepeatMode = PlayMode == PlayMode.SingleLoop ? 1 : 0;
                    _exoPlayer.ShuffleModeEnabled = PlayMode == PlayMode.Random;
                    Android.Util.Log.Info("YinYan", $"PlayAt: RepeatMode={_exoPlayer.RepeatMode}, Shuffle={_exoPlayer.ShuffleModeEnabled}");
                }
                catch { }
            }
#endif
            // ⚠️ V2.6/V2.7 关键分支：三条上报路径，别搞混
            //   ① 本地曲库的歌（IsLocal，Id 为负）：**绝不上报**，只累加 LocalPlayCount
            //      —— 服务端不认识它，上报只会在统计里制造脏数据（TC-2.6-07）。
            //   ② 缓存歌离线播放：服务端认得（有 songId），但此刻发不出去 →
            //      写进待补报队列，联网后由 V2.11 批量补报（TC-2.7-12）。
            //   ③ 其余在线播放：照旧实时上报。
            if (skipReport)
            {
                // 换内核（迁移）时重新装载同一首：这不是"新的一次播放"，不计数、不上报
                LogInfo("PlayAt: 内核迁移重载，跳过播放计数与上报");
            }
            else if (song.IsLocal)
                _ = IncrementLocalPlayCountAsync(song.Id);
            else if (playFromCache)
                _ = TraceCachePlayAsync(song);
            else
                _ = ReportOnlinePlayAsync(song);

            // V2.10：切到新歌时把这首歌的进度报一次，位置取**实际起播位置**：
            // 普通切歌是 0（换设备续播要从"刚切的这首"开始，TC-2.10-02）；
            // ⚠️ 但"继续播放"（PlayQueueAt）是带着位置起播的 —— 这里若硬写 0，
            // 会把服务端刚记下的续播点覆盖成 0，用户再换设备就变成"从头发"了。
            _ = ReportPlaybackProgressAsync(song, _resumeAfterOpen?.PositionSeconds ?? 0);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            // 换源抛异常也属于"本轮切歌没成功打开"，必须解除切换中，避免卡死后续 PlayAt。
            _switching = false;
            _pendingIndex = null;
            System.Diagnostics.Debug.WriteLine($"[播放异常] {ex.Message}");
        }
    }

    /// <summary>
    /// 从指定位置起播（V2.10 断点续播）。用法与 <see cref="PlayQueue"/> 一致，
    /// 只是“定位”交给 <see cref="OnMediaOpened"/> 去做 —— 与“换内核续播”走同一条路，
    /// 避免在源还没打开时 Seek 被丢掉。
    /// </summary>
    public void PlayQueueAt(IReadOnlyList<SongDto> songs, int startIndex, double startPositionSeconds, string? sourceName = null)
    {
        if (songs.Count == 0) return;
        startPositionSeconds = Math.Max(0, startPositionSeconds);
        // 1 秒以内的位置不值得定位（还会多一次 Seek），直接从头放
        if (startPositionSeconds > 1)
            _resumeAfterOpen = new ResumeState(startPositionSeconds, WasPlaying: true);
        PlayQueue(songs, startIndex, sourceName);
    }

    /// <summary>
    /// 退出 App 时尽力把进度报一次（V2.10）。fire-and-forget：进程可能在请求发完前就被挂起，
    /// 所以它只是兜底，真正的覆盖靠暂停/切歌/30 秒心跳。
    /// <para>
    /// ⚠️ 这一刻位置很可能已经不可信：播放元素被释放、正在换源、媒体已停 ——
    /// <c>PositionSeconds</c> 都可能已被清成 0。这种情况下**什么都不报**，把服务端已有的那条记录留着；
    /// 报 0 等于把用户的续播点抹掉。也就是"抓不到最新的，就保住上一次记录的"。
    /// </para>
    /// </summary>
    public void ReportProgressOnExit()
    {
        var song = Current;
        if (song is null) return;

        // 待定位窗口里真实起播位置在 _resumeAfterOpen（PositionSeconds 已被 PlayAt 清 0）
        var position = _resumeAfterOpen?.PositionSeconds ?? PositionSeconds;

        // 1 秒以内不值得记（用户刚点开就退了），而且这类"看着像 0"的值最可能是不准的
        if (position <= 1) return;

        _ = ReportPlaybackProgressAsync(song, position);
    }

    /// <summary>
    /// V2.10：上报播放进度。fire-and-forget —— 进度丢一条不影响播放本身，失败只落日志。
    /// 本地歌（Id 为负）与未登录都直接跳过：前者服务端不认识，后者白跑一趟还多一次 401。
    /// </summary>
    private async Task ReportPlaybackProgressAsync(SongDto? song, double position)
    {
        try
        {
            if (song is null || song.IsLocal || song.Id <= 0) return;
            if (Auth?.IsLoggedIn != true) return;
            await api.SavePlaybackProgressAsync(song.Id, Math.Max(0, position));
        }
        catch (Exception ex)
        {
            LogWarn($"上报播放进度失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 本地歌播放计数 +1（V2.6）。这是本地歌**唯一**的计数落点，与在线
    /// <c>api.RecordPlayAsync</c> 在同一时机（开始播放）触发，保证两端"播放次数"口径一致。
    ///
    /// 走 fire-and-forget：计数失败不能影响播放（比如库被别的进程锁住）。
    /// 更新完通知 UI 刷新列表里的次数列。
    /// </summary>
    private async Task IncrementLocalPlayCountAsync(long songDtoId)
    {
        try
        {
            var store = LocalStore;
            if (store is null) return;

            var localId = LocalLibraryStore.ToLocalId(songDtoId);
            await store.IncrementPlayCountAsync(localId);

            // 同步内存里的 Current，让播放页/列表立刻看到新次数，不用等下次整表重载
            if (Current is not null && Current.Id == songDtoId)
                Current.PlayCount = Current.PlayCount + 1;
        }
        catch (Exception ex)
        {
            LogWarn($"IncrementLocalPlayCountAsync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 整首听完 → 交给缓存服务后台自动缓存（V2.7 增强）。
    ///
    /// <para><b>只在这里调用</b>：<c>MediaEnded</c> 只在自然播完时触发，手动切歌不会走到这里，
    /// 所以"只缓存完整听完的歌"这条策略由调用时机天然保证（不需要再比对进度）。</para>
    /// </summary>
    private void TryAutoCacheFinishedSong()
    {
        var song = Current;
        if (song is null || song.IsLocal || song.Id <= 0) return;

        var downloader = ServiceHelper.GetService<CacheDownloadService>();
        if (downloader is null) return;

        // fire-and-forget：自动缓存（WiFi 判断、容量预检、下载）绝不影响切歌与播放
        _ = downloader.TryAutoCacheAsync(song);
    }

    // ── V2.7 缓存播放的上报与回退 ────────────────────────────────────────────

    /// <summary>
    /// 在线播放的实时上报。**必须自己兜异常**：原来这里是裸的
    /// <c>_ = api.RecordPlayAsync(...)</c>，断网时会抛出未观察的任务异常（.NET 下不会崩进程，
    /// 但异常被静默吞掉、日志里什么都没有）。播放本身不该被上报失败影响。
    /// </summary>
    private async Task ReportOnlinePlayAsync(SongDto song)    {
        try
        {
            await api.RecordPlayAsync(song.Id);
        }
        catch (Exception ex)
        {
            LogWarn($"RecordPlayAsync failed id={song.Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// 缓存歌**离线播放**的留痕（V2.7 写队列，V2.11 消费补报）。
    /// 同时刷新缓存的最近访问时间，供将来做 LRU 清理时参考。
    /// </summary>
    private async Task TraceCachePlayAsync(SongDto song)
    {
        try
        {
            var reports = ServiceHelper.GetService<PendingPlayReportStore>();
            if (reports is not null)
            {
                // 账号维度（V2.11）：记下当时登录的 UserId，flush 时只提交属于当前账号的行
                var userId = Auth?.CurrentUser?.Id ?? 0;
                await reports.EnqueueAsync(song.Id, userId, (int)Math.Max(0, PositionSeconds));
            }

            LogInfo($"TraceCachePlayAsync: 已记录离线播放待补报 id={song.Id} ({song.Title})");
        }
        catch (Exception ex)
        {
            LogWarn($"TraceCachePlayAsync failed id={song.Id}: {ex.Message}");
        }

        try
        {
            var cache = Cache;
            if (cache is not null) await cache.TouchAsync(song.Id);
        }
        catch { }
    }

    /// <summary>
    /// 当前网络是否不可用。取不到网络状态（平台未实现/权限异常）时**按有网处理**：
    /// 宁可去试远程（失败还有回退），也不要因为一次探测异常把在线播放整体降级到缓存。
    /// </summary>
    private static bool IsOffline()
    {
        try
        {
            return Connectivity.Current.NetworkAccess != NetworkAccess.Internet;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>重新计算"当前歌曲是否已缓存"（缓存增删后由 CacheChanged 事件触发）。</summary>
    private void RefreshCurrentCachedFlag()
    {
        var song = Current;
        IsCurrentCached = song is not null && !song.IsLocal && (Cache?.IsCached(song.Id) ?? false);
    }

    /// <summary>
    /// 远程源播放失败时的兜底：本机有缓存就改用缓存文件重播一次。
    ///
    /// <para>覆盖的典型场景：写着"有网"但实际连不上服务器、服务端音频被后台删了/改名了、
    /// CDN 抖动。用户此刻的感受是"点了没反应"，回退到缓存能把这一下救回来。</para>
    ///
    /// <para>一首歌只回退一次（<see cref="_cacheFallbackTried"/>）：缓存文件本身也可能损坏，
    /// 不加这道闸就会在"失败→回退→失败"之间反复切源。</para>
    /// </summary>
    private void TryFallbackToCache()
    {
        var song = Current;
        if (song is null || song.IsLocal || _cacheFallbackTried) return;

        var cache = Cache;
        var entry = cache?.TryGetCached(song.Id);
        if (entry is null || !LocalFileAccess.Exists(entry.FilePath)) return;
        if (_player is null) return;

        _cacheFallbackTried = true;
        try
        {
            var uri = LocalFileAccess.ToPlayableUri(entry.FilePath);
            if (uri is null) return;

            LogWarn($"MediaFailed: 远程播放失败，回退到本地缓存 id={song.Id} ({song.Title})");
            _player.Source = uri;
            _playPending = true;
            _player.Play();
            ErrorMessage = string.Empty;

            // 这一次播放没能打成在线上报 → 补报留痕，别把服务端统计丢掉
            _ = TraceCachePlayAsync(song);
        }
        catch (Exception ex)
        {
            LogWarn($"TryFallbackToCache failed id={song.Id}: {ex.Message}");
        }
    }

    public async Task NextAsync()
    {
        if (_queue.Count == 0) return;
        LogInfo($"NextAsync: _index={_index}, queueCount={_queue.Count}, PlayMode={PlayMode}");
        int next;
        switch (PlayMode)
        {
            case PlayMode.Random:
                if (_queue.Count == 1) { await ReplayCurrentAsync(); return; }
                do { next = _rng.Next(_queue.Count); } while (next == _index);
                break;
            case PlayMode.Sequential:
                next = _index + 1;
                if (next >= _queue.Count) return;
                break;
            default:
                next = (_index + 1) % _queue.Count;
                break;
        }
        await MainThread.InvokeOnMainThreadAsync(() => PlayAt(next, skipStep: 1));
    }

    public async Task PreviousAsync()
    {
        if (_queue.Count == 0) return;
        int prev;
        switch (PlayMode)
        {
            case PlayMode.Random:
                if (_queue.Count == 1) { await ReplayCurrentAsync(); return; }
                do { prev = _rng.Next(_queue.Count); } while (prev == _index);
                break;
            case PlayMode.Sequential:
                prev = _index - 1;
                if (prev < 0) return;
                break;
            default:
                prev = (_index - 1 + _queue.Count) % _queue.Count;
                break;
        }
        await MainThread.InvokeOnMainThreadAsync(() => PlayAt(prev, skipStep: -1));
    }

    private async Task ReplayCurrentAsync()
    {
        if (Current is null) return;
        await MainThread.InvokeOnMainThreadAsync(() => PlayAt(_index));
    }

    public void TogglePlayPause()
    {
        if (_player is null || Current is null) return;
        try
        {
            if (IsPlaying) { _playPending = false; _player.Pause(); }  // 用户手动暂停，撤销自动补放
            else _player.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Toggle异常] {ex.Message}");
        }
    }

    public void SeekTo(double seconds)
    {
        if (_player is null) return;
        try
        {
            var clamped = Math.Max(0, Math.Min(seconds, DurationSeconds));
            PositionSeconds = clamped;
            _player.SeekTo(TimeSpan.FromSeconds(clamped), CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Seek异常] {ex.Message}");
        }
    }


	public void CyclePlayMode()
	{
		PlayMode = PlayMode switch
		{
			PlayMode.Sequential => PlayMode.ListLoop,
			PlayMode.ListLoop => PlayMode.SingleLoop,
			PlayMode.SingleLoop => PlayMode.Random,
			_ => PlayMode.Sequential
		};
	}

	public void InsertNext(SongDto song)
	{
		if (_queue.Count == 0 || _index < 0)
		{
			_queue = [song];
			PlayAt(0);
			return;
		}
		_queue.Insert(_index + 1, song);

	}

	/// <summary>
	/// 把后取到的歌补到队列尾部。给"列表懒加载 + 点歌要立刻出声"用：
	/// 点歌时先用已加载的那部分入队（不等网络、当场切音源），剩下的页取回来再补进来 ——
	/// 否则队列只有前 20 首时，听完这批"下一首"就没了。
	/// <para>
	/// <paramref name="expectedSourceName"/> 是入队时那批歌的来源（歌手名 / 歌单名）：
	/// 队列已经被换成别的来源（用户去点了别的列表）就丢弃这次结果 ——
	/// 不挡住的话，上一批的歌会被追加进新队列，用户会听到莫名其妙的歌。
	/// 同一首歌按 Id 去重（翻页期间有人播放会让服务端按播放量的排序变化，可能跨页重复）。
	/// </para>
	/// <para>必须在 UI 线程调用（队列浮窗直接读 <see cref="_queue"/>）。</para>
	/// </summary>
	public void AppendToQueue(IReadOnlyList<SongDto> songs, string? expectedSourceName)
	{
		if (songs.Count == 0) return;
		if (expectedSourceName is not null && !string.Equals(SourceName, expectedSourceName, StringComparison.Ordinal))
		{
			LogInfo($"AppendToQueue: 队列来源已变（现在是「{SourceName}」），丢弃这次补齐");
			return;
		}

		var known = _queue.Select(s => s.Id).ToHashSet();
		var added = songs.Where(s => known.Add(s.Id)).ToList();
		if (added.Count == 0) return;

		_queue.AddRange(added);
		LogInfo($"AppendToQueue: 队列 {_queue.Count - added.Count} → {_queue.Count} 首（来源「{SourceName}」）");
	}

	public void SaveState()
	{
		try
		{
			if (_queue.Count == 0 || _index < 0) return;
			var state = new PlayerState
			{
				Queue = _queue,
				Index = _index,
				PositionSeconds = PositionSeconds,
				SourceName = SourceName,
				PlayMode = PlayMode
			};
			var dir = Path.GetDirectoryName(StateFilePath);
			if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
			var json = JsonSerializer.Serialize(state);
			File.WriteAllText(StateFilePath, json);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[保存状态异常] {ex.Message}");
		}
	}

	public async Task RestoreStateAsync()
	{
		try
		{
			if (!File.Exists(StateFilePath)) return;
			var json = File.ReadAllText(StateFilePath);
			var state = JsonSerializer.Deserialize<PlayerState>(json);
			if (state is null || state.Queue.Count == 0) return;

			_queue = [.. state.Queue];
			_index = state.Index;
			Current = _queue[_index];
			PositionSeconds = state.PositionSeconds;
			SourceName = state.SourceName;
			PlayMode = state.PlayMode;
			DurationSeconds = Current.DurationSeconds > 0 ? Current.DurationSeconds : 1;
			RefreshCurrentCachedFlag();   // 恢复出来的歌也要能显示「已缓存」标记
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[恢复状态异常] {ex.Message}");
		}
		await Task.CompletedTask;
	}

	/// <summary>
	/// **会话边界**调用（冷启动清理 / 退出登录）：先把声音真正停下来，再清空播放状态；
	/// 迷你播放条因此保持隐藏，由用户自己从任意列表重新点歌。
	/// 同时删除磁盘上的 player_state.json，避免下次误恢复。
	/// </summary>
	public void ResetForNewSession()
	{
		// ⚠️ 必须先停内核：IsPlaying 只是个可观察属性（Android 上仅用于刷通知栏），
		// 把它清成 false 并不会让 MediaElement 停止播放 ——
		// "退出登录回到登录页、歌还在响"就是这么来的（2026-09-25 用户实测）。
		StopPlayback();

		_queue = [];
		_index = -1;
		Current = null;
		IsPlaying = false;
		IsCurrentCached = false;
		PositionSeconds = 0;
		DurationSeconds = 1;
		_mediaReady = false;
		_playPending = false;
		// 切歌窗口的两个遗留标记也一并清掉，否则下一次点歌会被"上一轮切换中"的状态推迟
		_switching = false;
		_pendingIndex = null;
		try
		{
			if (File.Exists(StateFilePath)) File.Delete(StateFilePath);
		}
		catch { }
	}

	/// <summary>
	/// 停掉播放内核并收起 Android 的媒体通知。
	/// **只用于会话结束**（冷启动清理、退出登录）—— 导航离开页面时绝不能调，那时播放要继续。
	/// </summary>
	private void StopPlayback()
	{
		try { _player?.Stop(); }
		catch (Exception ex) { LogWarn($"StopPlayback: 停止播放内核失败 {ex.Message}"); }

#if ANDROID
		// 通知栏那条也属于这次会话：不收起的话，退出登录后它会一直挂着，
		// 而播放内核已经停了 —— 点一下还能"续播"上一首。
		try { MediaNotificationManager.Instance.HideNotification(); }
		catch (Exception ex) { LogWarn($"StopPlayback: 收起媒体通知失败 {ex.Message}"); }
#endif
	}

	private class PlayerState
	{
		public List<SongDto> Queue { get; set; } = [];
		public int Index { get; set; }
		public double PositionSeconds { get; set; }
		public string SourceName { get; set; } = string.Empty;
		public PlayMode PlayMode { get; set; }
	}
}




