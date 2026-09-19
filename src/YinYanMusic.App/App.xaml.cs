using YinYanMusic.App.Services;
using YinYanMusic.App.Views;

namespace YinYanMusic.App;

public partial class App : Application
{
	private Window? _window;

	public App()
	{
		InitializeComponent();
	}

	/// <summary>启动时的空白占位页背景，与 MainPage 的 PageBackground 同色。</summary>
	private const string PlaceholderBackground = "#F6F6F9";

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// 坑：Android 上 App 从后台回来时本方法可能再次被调用。
		// 早期实现每次都 new 一个「空占位页 + 新 Window」，会把已经挂好的 AppShell 盖掉，
		// 表现就是「放到后台再点开 = 白屏」（进程没死、Activity 也没重建，只是窗口内容被换成了空白页）。
		// 所以窗口一旦建好就必须复用，不能重建。
		if (_window is not null)
		{
			LogInfo("CreateWindow: reuse existing window, Page=" + _window.Page?.GetType().Name);
			return _window;
		}

		_window = new Window(new ContentPage { BackgroundColor = Color.FromArgb(PlaceholderBackground) });
		LogInfo("CreateWindow: created placeholder window");
		return _window;
	}

	protected override async void OnStart()
	{
		base.OnStart();
		try
		{
			// M0.5：先把 API 地址按 ①设置 ②环境变量 ③api.json ④默认 解析出来，
			// 这样之后任何用 IMusicApi 的调用都拿到正确 URL。
			ServiceHelper.GetRequiredService<ApiConfigStore>().Load();

			// 应用已保存的主题色（无记录则用默认紫色）。
			ThemeService.Instance.Initialize();
			var auth = ServiceHelper.GetRequiredService<IAuthService>();
			var isLoggedIn = await auth.TryRestoreSessionAsync();

			var shell = new AppShell();
			_window!.Page = shell;
			if (isLoggedIn)
			{
				var player = ServiceHelper.GetRequiredService<PlayerService>();
				// 启动即清空上次的播放状态，迷你播放条默认隐藏，由用户自行重新点歌。
				player.ResetForNewSession();
				await Shell.Current.GoToAsync("///main");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[启动恢复异常] {ex.Message}");
			try { _window!.Page = new AppShell(); } catch { }
		}
	}

	protected override void OnSleep()
	{
		base.OnSleep();
		try
		{
			var player = ServiceHelper.GetRequiredService<PlayerService>();
			player.SaveState();
		}
		catch { }
	}

	protected override void OnResume()
	{
		base.OnResume();
		// 兜底：万一窗口内容还停在启动时的空占位页（OnStart 的异步替换没跑完或被跳过），
		// 这里补上 AppShell，避免用户看到一片空白。已挂载 AppShell 时不会做任何事。
		try
		{
			if (_window is null) return;
			if (_window.Page is AppShell)
			{
				LogInfo("OnResume: Page is AppShell, no-op");
				return;
			}
			LogInfo("OnResume: Page=" + _window.Page?.GetType().Name + " -> replace with AppShell");
			_window.Page = new AppShell();
		}
		catch (Exception ex)
		{
			LogInfo("OnResume failed: " + ex.Message);
		}
	}

	private static void LogInfo(string message)
	{
#if ANDROID
		Android.Util.Log.Info("YinYan", message);
#else
		System.Diagnostics.Debug.WriteLine(message);
#endif
	}
}
