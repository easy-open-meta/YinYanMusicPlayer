#if ANDROID
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Android.Views;
using AndroidX.Core.App;
using System.Net.Http;
using System.Threading;

namespace YinYanMusic.App;

public class MediaNotificationManager : MediaSessionCompat.Callback, IDisposable
{
    private const int NotificationId = 1001;
    private const string ChannelId = "music_player";

    private static readonly Lazy<MediaNotificationManager> _instance = new(() => new MediaNotificationManager());
    public static MediaNotificationManager Instance => _instance.Value;

    private MediaSessionCompat? _session;
    private NotificationManager? _notificationManager;
    private Context? _context;
    private bool _initialized;
    private bool _disposed;
    private string _title = "音言音乐";
    private string _artist = "";
    private Bitmap? _largeIcon;
    private int _coverRequestId;
    private long _metadataDurationMs;
    private long _lastPositionMs;
    private long _lastDurationMs;
    private bool _lastIsPlaying;

    public Action? PlayAction { get; set; }
    public Action? PauseAction { get; set; }
    public Action? NextAction { get; set; }
    public Action? PreviousAction { get; set; }
    public Action? StopAction { get; set; }
    public Action<long>? SeekAction { get; set; }

    public void Init()
    {
        if (_initialized) return;
        _context = Android.App.Application.Context;

        _notificationManager = (NotificationManager)_context.GetSystemService(Context.NotificationService)!;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "音乐播放", NotificationImportance.Low);
            _notificationManager.CreateNotificationChannel(channel);
        }

        _session = new MediaSessionCompat(_context, "YinYanMusicService");
        // Android 13+（含 HyperOS）系统媒体控件的按钮与进度条事件直接派发给 MediaSession 回调，
        // 不再走通知 action 的 PendingIntent；不注册回调，上一曲/下一曲/拖动进度条会被系统静默丢弃。
        _session.SetCallback(this, new Handler(Looper.MainLooper));
        _session.Active = true;
        _initialized = true;
        _disposed = false;
        Android.Util.Log.Info("YinYanMusic", "MediaNotificationManager.Init: MediaSessionCompat active");
    }

    public void UpdateMetadata(string title, string artist, long durationMs, string? coverUrl)
    {
        if (_session is null) return;
        _title = title;
        _artist = artist;
        _metadataDurationMs = durationMs;
        SetMetadata();
        _ = UpdateCoverAsync(coverUrl);
    }

    private void SetMetadata()
    {
        if (_session is null) return;
        Android.Util.Log.Info("YinYanMusic", $"SetMetadata: title={_title}, artist={_artist}, duration={_metadataDurationMs}");
        var builder = new MediaMetadataCompat.Builder()
            .PutString("android.media.metadata.DISPLAY_TITLE", _title)
            .PutString("android.media.metadata.DISPLAY_SUBTITLE", _artist)
            .PutString("android.media.metadata.TITLE", _title)
            .PutString("android.media.metadata.ARTIST", _artist)
            .PutString("android.media.metadata.ALBUM", _artist)
            .PutLong("android.media.metadata.DURATION", _metadataDurationMs);
        if (_largeIcon is not null)
            builder.PutBitmap("android.media.metadata.ART", _largeIcon);
        var metadata = builder.Build();
        var vDt = metadata.GetString("android.media.metadata.DISPLAY_TITLE");
        var vT = metadata.GetString("android.media.metadata.TITLE");
        Android.Util.Log.Info("YinYanMusic", $"SetMetadata verify: DT={vDt}, T={vT}");
        _session.SetMetadata(metadata);
    }

    private async Task UpdateCoverAsync(string? coverUrl)
    {
        // 每次请求领一个序号（先领号再去重/下载），快速切歌时旧封面的下载结果会被丢弃，
        // 避免"先发起、后完成"的旧封面覆盖当前歌曲的封面。
        var requestId = Interlocked.Increment(ref _coverRequestId);

        // ⚠️ 没有封面时必须**清掉并重建通知**，不能只把内存位图置空就 return。
        // 旧实现就是直接 return，于是"从在线歌切到没有封面的本地歌"时，
        // 通知栏会一直挂着上一首（在线歌）的封面 —— 真机反馈的"封面没及时刷新"就是这个。
        if (string.IsNullOrEmpty(coverUrl))
        {
            ClearCoverAndRefresh();
            return;
        }
        try
        {
            // ⚠️ 不能直接用 HttpClient：本地曲库的封面是磁盘路径（AppData/local-covers/x.jpg）
            // 或 content:// URI，HTTP 取不到 → 异常被静默吞掉 → 通知栏封面一直是空的
            //（真机反馈："通知栏封面与播放页不一致"）。
            // 统一走 ImageSourceFactory.ReadBytesAsync，它认得 http / 本地路径 / content:// / data:。
            // 用完全限定名：本文件在 YinYanMusic.App 命名空间下，而 Platforms/Android/Services
            // 里也有服务类，简写容易解析歧义。
            var bytes = await YinYanMusic.App.Services.ImageSourceFactory.ReadBytesAsync(coverUrl);
            if (requestId != Volatile.Read(ref _coverRequestId)) return;

            // 取不到封面（文件已被删、content:// 授权失效、网络抖动）：同样清空 + 重建，
            // 让通知栏落到"无封面"而不是"停留在上一首的封面"。
            if (bytes is null || bytes.Length == 0)
            {
                ClearCoverAndRefresh();
                return;
            }

            var decoded = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
            if (decoded is null)
            {
                ClearCoverAndRefresh();
                return;
            }

            try { _largeIcon?.Recycle(); } catch { }
            _largeIcon = decoded;
            SetMetadata();
            ShowNotification();
        }
        catch
        {
            ClearCoverAndRefresh();
        }
    }

    /// <summary>
    /// 清空通知大图并重建通知。换歌但"这首歌没有封面/封面取不到"时的统一出口 ——
    /// 目的是让通知栏立刻掉到"无封面"状态，而不是继续显示上一首的封面。
    /// </summary>
    private void ClearCoverAndRefresh()
    {
        if (_largeIcon is null && _session is null) return;
        try { _largeIcon?.Recycle(); } catch { }
        _largeIcon = null;
        SetMetadata();        // 新 metadata 里不再带 ART
        ShowNotification();   // 用新 builder 重建（_largeIcon 为 null 时不会 SetLargeIcon）
    }

    public void UpdatePlaybackState(bool isPlaying, long positionMs, long durationMs)
    {
        if (_session is null) return;
        if (!_session.Active)
            _session.Active = true;
        var stateChanged = _lastIsPlaying != isPlaying;
        _lastIsPlaying = isPlaying;
        _lastPositionMs = positionMs;
        _lastDurationMs = durationMs;
        var state = isPlaying ? PlaybackStateCompat.StatePlaying : PlaybackStateCompat.StatePaused;
        var builder = new PlaybackStateCompat.Builder()
            .SetActions(PlaybackStateCompat.ActionPlay | PlaybackStateCompat.ActionPause |
                        PlaybackStateCompat.ActionSkipToNext | PlaybackStateCompat.ActionSkipToPrevious |
                        PlaybackStateCompat.ActionStop | PlaybackStateCompat.ActionSeekTo)
            .SetState(state, positionMs, isPlaying ? 1.0f : 0.0f);
        _session.SetPlaybackState(builder.Build());
        if (stateChanged)
            ShowNotification();
    }

    public void UpdateProgress(bool isPlaying, long positionMs, long durationMs)
    {
        if (_session is null) return;
        var state = isPlaying ? PlaybackStateCompat.StatePlaying : PlaybackStateCompat.StatePaused;
        var builder = new PlaybackStateCompat.Builder()
            .SetActions(PlaybackStateCompat.ActionPlay | PlaybackStateCompat.ActionPause |
                        PlaybackStateCompat.ActionSkipToNext | PlaybackStateCompat.ActionSkipToPrevious |
                        PlaybackStateCompat.ActionStop | PlaybackStateCompat.ActionSeekTo)
            .SetState(state, positionMs, isPlaying ? 1.0f : 0.0f);
        _session.SetPlaybackState(builder.Build());
    }

    /// <summary>构建当前媒体通知（前台服务与普通刷新共用）。</summary>
    public Notification BuildNotification()
    {
        var builder = new NotificationCompat.Builder(_context, ChannelId)
            .SetSmallIcon(Resource.Mipmap.icon)
            .SetContentTitle(_title)
            .SetContentText(_artist)
            .SetContentIntent(BuildLaunchIntent())
            .SetVisibility(NotificationCompat.VisibilityPublic)
            .SetOngoing(_lastIsPlaying)
            .SetShowWhen(false)
            .SetStyle(new AndroidX.Media.App.NotificationCompat.MediaStyle()
                .SetMediaSession(_session!.SessionToken))
            .AddAction(BuildAction(PlayAction, "play", "播放", (int)BuildVersionCodes.Lollipop, !_lastIsPlaying, 1))
            .AddAction(BuildAction(PauseAction, "pause", "暂停", (int)BuildVersionCodes.Lollipop, _lastIsPlaying, 2))
            .AddAction(BuildAction(NextAction, "next", "下一首", (int)BuildVersionCodes.Lollipop, true, 3))
            .AddAction(BuildAction(PreviousAction, "prev", "上一首", (int)BuildVersionCodes.Lollipop, true, 4))
            .AddAction(BuildAction(StopAction, "stop", "关闭", (int)BuildVersionCodes.Lollipop, true, 5));

        if (_largeIcon is not null)
            builder.SetLargeIcon(_largeIcon);

        return builder.Build();
    }

    private void ShowNotification()
    {
        if (_context is null || _notificationManager is null || _session is null) return;
        try
        {
            // 只重建通知本身。PlaybackState 由 UpdatePlaybackState/UpdateProgress 统一维护，
            // 这里若再 SetPlaybackState 会用过期参数回写状态，切歌时进度条会闪回上一首的位置。
            // 通过前台服务 startForeground 发布，避免后台时被系统降级成普通"正在运行中"通知。
            MusicPlaybackService.Start(_context, BuildNotification());
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("YinYanMusic", $"ShowNotification failed: {ex.Message}");
        }
    }

    private PendingIntent BuildLaunchIntent()
    {
        var launch = _context!.PackageManager!.GetLaunchIntentForPackage(_context.PackageName!);
        return launch is null
            ? PendingIntent.GetActivity(_context, 0, new Intent(_context, typeof(Activity)), PendingIntentFlags.Immutable)
            : PendingIntent.GetActivity(_context, 0, launch, PendingIntentFlags.Immutable);
    }

    private NotificationCompat.Action BuildAction(Action? callback, string actionType, string title, int minSdk, bool show, int requestCode)
    {
        var intent = new Intent("com.yinyan.music.MEDIA_ACTION");
        intent.SetPackage(_context!.PackageName);
        intent.PutExtra(MediaActionReceiver.ActionExtra, actionType);
        var pi = PendingIntent.GetBroadcast(_context, requestCode, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var iconRes = actionType switch
        {
            "play" => Android.Resource.Drawable.IcMediaPlay,
            "pause" => Android.Resource.Drawable.IcMediaPause,
            "next" => Android.Resource.Drawable.IcMediaNext,
            "prev" => Android.Resource.Drawable.IcMediaPrevious,
            "stop" => Android.Resource.Drawable.IcMenuCloseClearCancel,
            _ => Android.Resource.Drawable.IcMediaPlay
        };
        return new NotificationCompat.Action.Builder(iconRes, title, pi).Build();
    }

    /// <summary>
    /// 隐藏通知并停用会话，但保留 MediaSessionCompat 实例：
    /// UpdateMetadata/UpdatePlaybackState 会再次把会话激活后继续复用，所以这里不能 Release，
    /// 终态的原生资源释放统一交给 Dispose。
    /// </summary>
    public void HideNotification()
    {
        if (_context is not null)
            MusicPlaybackService.Stop(_context);
        _notificationManager?.Cancel(NotificationId);
        if (_session is not null)
            _session.Active = false;
    }

    /// <summary>
    /// 释放 MediaSessionCompat、通知与封面位图等原生资源，并断开所有回调委托。
    /// 由 MainActivity.OnDestroy 在 Activity/应用销毁时调用；
    /// 释放后若再次调用 Init 会重新创建会话。
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_context is not null)
                MusicPlaybackService.Stop(_context);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("YinYanMusic", $"Dispose: stop playback service failed: {ex.Message}");
        }

        try
        {
            _notificationManager?.Cancel(NotificationId);
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("YinYanMusic", $"Dispose: cancel notification failed: {ex.Message}");
        }

        var session = _session;
        _session = null;
        if (session is not null)
        {
            try
            {
                session.Active = false;
                session.Release();
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("YinYanMusic", $"Dispose: release session failed: {ex.Message}");
            }
            try
            {
                session.Dispose();
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("YinYanMusic", $"Dispose: dispose session failed: {ex.Message}");
            }
        }

        try
        {
            _largeIcon?.Recycle();
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("YinYanMusic", $"Dispose: recycle largeIcon failed: {ex.Message}");
        }
        _largeIcon = null;

        PlayAction = null;
        PauseAction = null;
        NextAction = null;
        PreviousAction = null;
        StopAction = null;
        SeekAction = null;

        _initialized = false;
        Android.Util.Log.Info("YinYanMusic", "MediaNotificationManager.Dispose: MediaSessionCompat released");
        GC.SuppressFinalize(this);
    }

    public override void OnPlay() => PlayAction?.Invoke();
    public override void OnPause() => PauseAction?.Invoke();
    public override void OnSkipToNext() => NextAction?.Invoke();
    public override void OnSkipToPrevious() => PreviousAction?.Invoke();
    public override void OnSeekTo(long pos)
    {
        Android.Util.Log.Info("YinYan", $"OnSeekTo: pos={pos}");
        SeekAction?.Invoke(pos);
        // 拖动后立刻把新位置回写给系统面板的 PlaybackState，
        // 否则面板会按旧位置继续外推，进度条看起来像"弹回去"。
        UpdatePlaybackState(_lastIsPlaying, pos, _lastDurationMs);
    }
    public override void OnStop()
    {
        StopAction?.Invoke();
        HideNotification();
    }
}
#endif
