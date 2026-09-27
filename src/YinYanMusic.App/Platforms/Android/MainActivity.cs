using Android.App;
using Android.Content;
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

    /// <summary>
    /// 把运行时权限结果转发给等待中的扫描器（V2.6 本地音乐库）。
    /// MAUI 的 Permissions API 对 API 33+ 的 READ_MEDIA_AUDIO 判断不可靠，
    /// 所以本地扫描自己走原生 <c>RequestPermissions</c>，结果在这里桥接回去。
    /// </summary>
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (requestCode == AndroidLocalMediaScanner.PermissionRequestHandler.RequestCode &&
            AndroidLocalMediaScanner.PermissionRequestHandler.Current is { } handler)
        {
            handler.OnResult(permissions, grantResults);
        }

        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
    }

    /// <summary>
    /// 把 SAF 目录选择结果转发给等待中的扫描器（V2.6 自选文件夹扫描）。
    /// 走 <c>StartActivityForResult</c> 而不是 MAUI 的 FilePicker：目录选择
    /// （<c>ACTION_OPEN_DOCUMENT_TREE</c>）只有原生 API 能做，且要拿持久授权。
    /// </summary>
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (AndroidLocalMediaScanner.FolderPickResultHandler.Current is { } handler &&
            handler.Handles(requestCode))
        {
            handler.OnResult(resultCode, data);
        }

        base.OnActivityResult(requestCode, resultCode, data);
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
