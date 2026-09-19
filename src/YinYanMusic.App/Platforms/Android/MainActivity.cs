using Android.App;
using Android.Content.PM;
using Android.OS;
using YinYanMusic.App.Services;

namespace YinYanMusic.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu &&
            CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            RequestPermissions(new[] { Android.Manifest.Permission.PostNotifications }, 1001);
        }
    }

    protected override void OnDestroy()
    {
        // 仅在 Activity 真正结束时释放（退出/划掉应用）。配置变更导致的重建（如系统字体缩放、
        // 语言切换）也会走 OnDestroy，此时进程内单例仍要继续可用，不能释放。
        if (IsFinishing)
        {
            try
            {
                // 解绑 MediaElement 事件、停止进度定时器，并释放 ExoPlayer 引用
                ServiceHelper.GetRequiredService<PlayerService>().DetachPlayer();
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("YinYanMusic", $"DetachPlayer failed: {ex.Message}");
            }

            MediaNotificationManager.Instance.Dispose();
        }

        base.OnDestroy();
    }
}
