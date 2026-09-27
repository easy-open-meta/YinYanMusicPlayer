using Android.App;
using Android.Content;
using Android.Runtime;
using YinYanMusic.App.Services;

namespace YinYanMusic.App;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	/// <summary>
	/// 系统内存压力回调（V2.16 公平内存机制）：按压力级别清理位图内存缓存，
	/// 避免低内存场景下缓存占用导致 OOM。
	/// </summary>
	public override void OnTrimMemory(TrimMemory level)
	{
		base.OnTrimMemory(level);
		try
		{
			ServiceHelper.GetService<AndroidBitmapCache>()?.OnTrimMemory(level);
		}
		catch
		{
			// 缓存清理失败不影响应用继续运行
		}
	}
}