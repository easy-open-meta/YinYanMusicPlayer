#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace YinYanMusic.App;

/// <summary>
/// 媒体播放前台服务：用 <c>startForeground</c> 承载 MediaStyle 通知。
/// <para>
/// 为什么必须要有它：Android 要求后台媒体播放使用前台服务，否则系统会把 MediaStyle
/// 通知降级成普通"正在运行中"通知（表现为通知栏丢媒体按钮、丢封面）。Manifest 里
/// <c>FOREGROUND_SERVICE</c> / <c>FOREGROUND_SERVICE_MEDIA_PLAYBACK</c> 权限早已声明，
/// 这里补上实际的服务实现。
/// </para>
/// </summary>
[Service(
    Name = "com.yinyan.music.MusicPlaybackService",
    ForegroundServiceType = Android.Content.PM.ForegroundService.TypeMediaPlayback,
    Exported = false)]
public class MusicPlaybackService : Service
{
    /// <summary>与 <see cref="MediaNotificationManager"/> 的通知 ID 保持一致。</summary>
    public const int NotificationId = 1001;

    private static MusicPlaybackService? _instance;

    /// <summary>当前运行的服务实例（<see cref="MediaNotificationManager"/> 用它即时刷新前台通知）。</summary>
    public static MusicPlaybackService? Instance => _instance;

    /// <summary>启动前台服务并（如果已在运行）立即刷新通知。</summary>
    public static void Start(Context context, Notification notification)
    {
        var intent = new Intent(context, typeof(MusicPlaybackService));
        ContextCompat.StartForegroundService(context, intent);
        // 服务实例已注册（说明服务已运行）时，直接更新前台通知
        _instance?.StartForeground(NotificationId, notification);
    }

    /// <summary>停止前台服务并移除媒体通知。</summary>
    public static void Stop(Context context)
    {
        context.StopService(new Intent(context, typeof(MusicPlaybackService)));
    }

    public override void OnCreate()
    {
        base.OnCreate();
        _instance = this;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // 前台服务必须在启动后立刻 startForeground，否则系统会 ANR
        var notification = MediaNotificationManager.Instance.BuildNotification();
        StartForeground(NotificationId, notification);
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        if (ReferenceEquals(_instance, this)) _instance = null;
        base.OnDestroy();
    }

    public override IBinder? OnBind(Intent? intent) => null;
}
#endif