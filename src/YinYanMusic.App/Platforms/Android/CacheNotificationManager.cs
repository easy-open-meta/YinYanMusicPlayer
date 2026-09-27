#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;            // Build.VERSION / BuildVersionCodes（漏了它会在 Android 目标编译失败）
using AndroidX.Core.App;

namespace YinYanMusic.App;

/// <summary>
/// 缓存下载的**通知栏进度**（V2.7 增强）。
///
/// <para>与 <see cref="MediaNotificationManager"/> 分开是刻意的：播放通知是 MediaStyle
/// 且常驻带控制按钮，缓存通知是普通进度通知、任务结束就该消失。混在一条通知里会导致
/// "暂停/播放"和"下载进度"互相顶掉。</para>
///
/// <para>通知栏只放一条：显示第一首的歌名 + **总体进度**（多首在队列时按字节数加权），
/// 并注明"共 N 首"。全部完成即取消通知 —— 不留"下载完了还挂着"的尾巴。</para>
/// </summary>
public sealed class CacheNotificationManager
{
    /// <summary>与播放通知（1001）刻意错开，两条可以同时存在。</summary>
    private const int NotificationId = 2001;
    private const string ChannelId = "cache_download";

    private static readonly Lazy<CacheNotificationManager> _instance = new(() => new CacheNotificationManager());
    public static CacheNotificationManager Instance => _instance.Value;

    private Context? _context;
    private NotificationManager? _notificationManager;
    private bool _initialized;

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _context = Android.App.Application.Context;
        _notificationManager = (NotificationManager)_context.GetSystemService(Context.NotificationService)!;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            // Low：缓存是背景动作，不该响铃/弹横幅打扰正在听歌的用户
            var channel = new NotificationChannel(ChannelId, "歌曲缓存", NotificationImportance.Low)
            {
                Description = "显示在线歌曲的缓存下载进度",
            };
            _notificationManager.CreateNotificationChannel(channel);
        }

        _initialized = true;
    }

    /// <summary>
    /// 刷新通知：<paramref name="activeCount"/> 首在下载，当前这首是 <paramref name="title"/>。
    /// <paramref name="indeterminate"/>=true 时用不确定进度条（服务器没给文件大小）。
    /// </summary>
    public void Update(int activeCount, string title, double percent, bool indeterminate = false)
    {
        try
        {
            EnsureInitialized();
            if (_context is null || _notificationManager is null) return;

            var clamped = Math.Clamp(percent, 0, 100);
            var text = activeCount > 1
                ? $"共 {activeCount} 首 · {clamped:F0}%"
                : $"{clamped:F0}%";

            var builder = new NotificationCompat.Builder(_context, ChannelId)
                .SetSmallIcon(Resource.Mipmap.icon)
                .SetContentTitle(activeCount > 1 ? $"正在缓存 {activeCount} 首" : "正在缓存")
                .SetContentText(string.IsNullOrWhiteSpace(title) ? text : $"{title} · {text}")
                .SetContentIntent(BuildLaunchIntent())
                .SetOnlyAlertOnce(true)      // 进度刷新不该反复提醒
                .SetOngoing(true)            // 用户不该用滑动误删进度
                .SetShowWhen(false)
                .SetPriority(NotificationCompat.PriorityLow)
                .SetProgress(100, (int)Math.Round(clamped), indeterminate);

            _notificationManager.Notify(NotificationId, builder.Build());
            // 落一行日志：通知栏是"用户看得见、开发者抓不到"的地方，
            // 出问题时（通知不出现/不消失）只能靠这行判断到底有没有发出去。
            Android.Util.Log.Info("YinYan", $"[CacheNotify] notify {activeCount} 首 {clamped:F0}% title={title}");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("YinYan", $"CacheNotificationManager.Update failed: {ex.Message}");
        }
    }

    /// <summary>取消通知（没有进行中的下载时调用）。</summary>
    public void Clear()
    {
        try
        {
            EnsureInitialized();
            _notificationManager?.Cancel(NotificationId);
            Android.Util.Log.Info("YinYan", "[CacheNotify] cancel (没有进行中的下载)");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Warn("YinYan", $"CacheNotificationManager.Clear failed: {ex.Message}");
        }
    }

    private PendingIntent BuildLaunchIntent()
    {
        var launch = _context!.PackageManager!.GetLaunchIntentForPackage(_context.PackageName!);
        return launch is null
            ? PendingIntent.GetActivity(_context, 0, new Intent(_context, typeof(Activity)), PendingIntentFlags.Immutable)
            : PendingIntent.GetActivity(_context, 0, launch, PendingIntentFlags.Immutable);
    }
}
#endif
