using YinYanMusic.App.Services;
using YinYanMusic.App.Services.LocalLibrary;
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

		// 标题：Windows 标题栏一直显示空白（MAUI 默认取 Window.Title，没人设过它）。
		// 用应用显示名而不是写死字符串，与安装包/关于页保持同一来源。
		_window = new Window(new ContentPage { BackgroundColor = Color.FromArgb(PlaceholderBackground) })
		{
			Title = AppInfo.Current.Name,
		};
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

			// V2.7 歌曲缓存：**后台**预热索引并对账（清掉"文件已丢"的条目、修正容量统计，TC-2.7-10）。
			// 刻意不 await：对账要读 SQLite，挂在启动关键路径上会拖慢进主页；
			// 而缓存只影响播放与设置页，晚一两秒就绪没有任何影响。
			_ = Task.Run(async () =>
			{
				try
				{
					var cache = ServiceHelper.GetRequiredService<CacheStore>();
					await cache.EnsureIndexAsync();
					await cache.ReconcileAsync();
				}
				catch (Exception ex)
				{
					LogInfo($"[Cache] 启动预热失败: {ex.Message}");
				}
			});

			// ⚠️ 先把 AppShell 挂上，**再**去恢复登录态。
			// 原来这里是先 await TryRestoreSessionAsync() 再挂 Shell，而该方法内部要
			// 调 api.MeAsync() 走网络 —— 网络慢或不可达时整个启动就卡在启动占位页上，
			// 用户看到的是一片空白（真机实测：弱网下白屏 30 秒以上）。
			// 本地音乐是纯本地能力、不依赖登录态，所以绝不能让它被网络恢复流程挡住。
			var shell = new AppShell();
			_window!.Page = shell;

			var auth = ServiceHelper.GetRequiredService<IAuthService>();
			var isLoggedIn = await auth.TryRestoreSessionAsync();

			if (isLoggedIn)
			{
				var player = ServiceHelper.GetRequiredService<PlayerService>();
				// 启动即清空上次的播放状态，迷你播放条默认隐藏，由用户自行重新点歌。
				player.ResetForNewSession();

				// V2.15 通知实时通道：登录恢复后连 SignalR（失败只记日志）。
				// 新通知到达时弹轻 Toast；角标由 LibraryViewModel 订阅 UnreadCountChanged。
				try
				{
					var realtime = ServiceHelper.GetRequiredService<NotificationRealtimeService>();
					realtime.NotificationReceived += (n, _) =>
					{
						MainThread.BeginInvokeOnMainThread(async () =>
						{
							try { await SongMenuHelper.ShowToastAsync($"新通知：{n.Title}"); }
							catch { /* Toast 失败不影响收通知 */ }
						});
					};
					await realtime.EnsureConnectedAsync();
				}
				catch (Exception ex)
				{
					LogInfo($"[Notify] 启动连接失败: {ex.Message}");
				}

				await Shell.Current.GoToAsync("///main");
			}
			// 未登录就停在 AppShell 的初始路由（login），用户可以登录或直接进本地音乐

			// V2.11 离线播放补报：挂网络恢复监听 + 启动即试一轮（登录态在 TryRestoreSession 里已恢复）。
			// flusher 内部自带退避与异常兜底，fire-and-forget 即可，不拖启动。
			var flusher = ServiceHelper.GetRequiredService<PlayReportFlusher>();
			flusher.Start();
			_ = flusher.FlushAsync("启动");

			// V2.12 全局离线红点：离线时当前页右上角挂 3px 红点（点击展开说明），联网自动消失
			ServiceHelper.GetRequiredService<OfflineIndicatorService>().Start();
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
			player.ReportProgressOnExit();
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
