using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using YinYanMusic.App.Services;
using YinYanMusic.App.Services.LocalLibrary;
using YinYanMusic.App.ViewModels;
using YinYanMusic.App.Views;

namespace YinYanMusic.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()

			// UseMauiCommunityToolkitMediaElement(false)：关闭 MediaElement 自带的 Android 前台服务。
			// 它内部走 Media3 MediaSessionService，会注册一个默认通知渠道（MIUI 里显示为"1"），
			// 并与本项目自研的 MusicPlaybackService（音乐播放渠道）重复。播放通知已由
			// MusicPlaybackService 承载，这里必须关掉，避免出现多余的"1"通知类别。
			.UseMauiCommunityToolkitMediaElement(false)
			.ConfigureFonts(fonts =>
			{
			fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
			})

			// Windows 专属：拦截主窗口的关闭（✕ / Alt+F4 / 任务栏右键），按「偏好设置」里的开关决定
			// 是退出还是隐藏到托盘继续播。必须在窗口创建时挂——MAUI 的 Window 事件只能"知道窗口没了"，
			// 取消不了关闭。Android 没有窗口关闭这回事，整块不注册。
#if WINDOWS
			.ConfigureLifecycleEvents(events => events.AddWindows(
				windows => windows.OnWindowCreated(WindowsWindowCloseHook.Attach)))
#endif
		;

		Routing.RegisterRoute("playlist", typeof(PlaylistDetailPage));
		Routing.RegisterRoute("nowplaying", typeof(NowPlayingPage));
		Routing.RegisterRoute("artist", typeof(ArtistDetailPage));
		Routing.RegisterRoute("zones", typeof(ZonesPage));
		Routing.RegisterRoute("zone", typeof(ZonePage));
		Routing.RegisterRoute("followArtists", typeof(FollowArtistsPage));
		Routing.RegisterRoute("myPlaylists", typeof(MyPlaylistsPage));
		Routing.RegisterRoute("followers", typeof(FollowersPage));
		Routing.RegisterRoute("userDetail", typeof(UserPage));
		Routing.RegisterRoute("settings", typeof(SettingsPage));
		// 偏好设置：本机行为开关（目前只有「关闭窗口隐藏到托盘」，Windows 专属，Android 不显示入口）
		Routing.RegisterRoute("preferences", typeof(PreferencesPage));
		// V2.5 账号与安全
		Routing.RegisterRoute("account", typeof(AccountEditPage));
		Routing.RegisterRoute("security", typeof(SecurityCenterPage));
		// V2.5 三级页面：安全中心下的两个子页
		Routing.RegisterRoute("changePassword", typeof(ChangePasswordPage));
		Routing.RegisterRoute("bindEmail", typeof(BindEmailPage));
		// V2.15 关于 / 第三方开源组件引用
		Routing.RegisterRoute("about", typeof(AboutPage));
		Routing.RegisterRoute("thirdParty", typeof(ThirdPartyLicensesPage));
		// V2.15 消息通知
		Routing.RegisterRoute("notifications", typeof(NotificationsPage));
		// V2.6 本地音乐库
		Routing.RegisterRoute("localMusic", typeof(LocalMusicPage));
		// V2.7 歌曲缓存
		Routing.RegisterRoute("cacheManage", typeof(CacheManagePage));

		// API 地址解析器：单例。M0.5 让 API 地址可配置（应用内设置 > 环境变量 > api.json > 默认）。
		builder.Services.AddSingleton<ApiConfigStore>();

		// 基础设施
		builder.Services.AddSingleton<IAuthService, AuthService>();
		builder.Services.AddSingleton<IMusicApi, MusicApiService>();
		builder.Services.AddSingleton<PlayerService>();
		// V2.15 通知实时通道：登录后连 SignalR；单例保证跨页面同一连接
		builder.Services.AddSingleton<NotificationRealtimeService>();
		// V2.16 系统音量联动：Android 走官方 AudioManager（STREAM_MUSIC），其他平台保持 App 内软件音量
#if ANDROID
		builder.Services.AddSingleton<ISystemVolumeService, AndroidSystemVolumeService>();
#else
		builder.Services.AddSingleton<ISystemVolumeService, NoopSystemVolumeService>();
#endif
		// V2.16 网络策略：Android 检测省流量模式（Data Saver），其他平台不限制
#if ANDROID
		builder.Services.AddSingleton<INetworkPolicyService, AndroidNetworkPolicyService>();
#else
		builder.Services.AddSingleton<INetworkPolicyService, NoopNetworkPolicyService>();
#endif
		builder.Services.AddTransient<AuthTokenHandler>();
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<PreferencesViewModel>();
		builder.Services.AddTransient<PreferencesPage>();

		// V2.6 本地音乐库：SQLite 曲库 + 平台扫描器。
		// Store 必须是单例——连接与建表只做一次，扫描和 UI 共用同一个 SQLiteAsyncConnection。
		builder.Services.AddSingleton<LocalDatabase>();
		builder.Services.AddSingleton<LocalLibraryStore>();
#if ANDROID
		builder.Services.AddSingleton<ILocalMediaScanner, AndroidLocalMediaScanner>();
#elif WINDOWS
		builder.Services.AddSingleton<ILocalMediaScanner, WindowsLocalMediaScanner>();
#endif

		// V2.7 歌曲缓存：索引 / 下载 / 离线补报留痕 / 缓存历史。
		// 三者都必须单例：索引要跨页面共享状态，下载器要靠单例的并发闸门才能限制"同时最多 3 个"。
		builder.Services.AddSingleton<CacheStore>();
		builder.Services.AddSingleton<CacheDownloadService>();
		builder.Services.AddSingleton<PendingPlayReportStore>();
		builder.Services.AddSingleton<CacheHistoryStore>();
		// V2.11 离线播放补报调度器：启动 / 网络恢复 / 播放结束三处触发，内部自带指数退避
		builder.Services.AddSingleton<PlayReportFlusher>();
		// V2.12 全局离线红点：设备离线时当前页面右上角显示红点（点击展开提示）
		builder.Services.AddSingleton<OfflineIndicatorService>();

		// HttpClient（自动注入令牌）
		// ⚠️ 不设 BaseAddress：路径全部由 MusicApiService 内的 Abs() 拼绝对地址（见 ApiConfig.cs 注释）。
		// 这样 ApiConfig.BaseUrl 改了之后**下一次请求就生效**，不用重建 HttpClient。
		builder.Services.AddSingleton<HttpClient>(sp =>
		{
			var handler = new HttpClientHandler();
			return new HttpClient(new AuthTokenHandler(sp)
			{
				InnerHandler = handler
			})
			{
				Timeout = TimeSpan.FromSeconds(30)
			};
		});

		// 动态配色：接口共享，实现按平台注册
		// （IBlurService 走平台原生模糊，仅 Android 12+ 可用；IAcrylicImageService 是
		//   自己糊像素的亚克力底图，两端都实现，浮窗铺底用它）
#if ANDROID
		builder.Services.AddSingleton<IAccentColorService, AndroidAccentColorService>();
		builder.Services.AddSingleton<IBlurService, AndroidBlurService>();
		// V2.16 公平内存机制：官方 LruCache，按 MemoryClass 分配容量，onTrimMemory 分级清理
		builder.Services.AddSingleton<AndroidBitmapCache>();
		builder.Services.AddSingleton<IAcrylicImageService, AndroidAcrylicImageService>();
#elif WINDOWS
		builder.Services.AddSingleton<IAccentColorService, WindowsAccentColorService>();
		builder.Services.AddSingleton<IBlurService, WindowsBlurService>();
		builder.Services.AddSingleton<IAcrylicImageService, WindowsAcrylicImageService>();
#endif

		// ViewModel
		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<RegisterViewModel>();
		builder.Services.AddSingleton<HomeViewModel>();
		builder.Services.AddSingleton<LibraryViewModel>();
		builder.Services.AddSingleton<SearchViewModel>();
		builder.Services.AddTransient<PlaylistDetailViewModel>();
		builder.Services.AddTransient<NowPlayingViewModel>();
		builder.Services.AddTransient<ArtistDetailViewModel>();
		builder.Services.AddTransient<ZonesViewModel>();
		builder.Services.AddTransient<ZoneViewModel>();
		builder.Services.AddTransient<FollowArtistsViewModel>();
		builder.Services.AddTransient<MyPlaylistsViewModel>();
		builder.Services.AddTransient<FollowersViewModel>();
		builder.Services.AddTransient<UserDetailViewModel>();

		// View
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<RegisterPage>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<HomeView>();
		builder.Services.AddTransient<LibraryView>();
		builder.Services.AddTransient<SearchView>();
		builder.Services.AddTransient<PlaylistDetailPage>();
		builder.Services.AddTransient<NowPlayingPage>();
		builder.Services.AddTransient<ArtistDetailPage>();
		builder.Services.AddTransient<ZonesPage>();
		builder.Services.AddTransient<ZonePage>();
		builder.Services.AddTransient<FollowArtistsPage>();
		builder.Services.AddTransient<MyPlaylistsPage>();
		builder.Services.AddTransient<FollowersPage>();
		builder.Services.AddTransient<UserPage>();
		// V2.5 账号与安全
		builder.Services.AddTransient<AccountEditPage>();
		builder.Services.AddTransient<AccountEditViewModel>();
		builder.Services.AddTransient<SecurityCenterPage>();
		builder.Services.AddTransient<SecurityCenterViewModel>();
		builder.Services.AddTransient<ChangePasswordPage>();
		builder.Services.AddTransient<ChangePasswordViewModel>();
		builder.Services.AddTransient<BindEmailPage>();
		builder.Services.AddTransient<BindEmailViewModel>();
		// V2.6 本地音乐库
		builder.Services.AddTransient<LocalMusicPage>();
		builder.Services.AddTransient<LocalMusicViewModel>();
		// V2.7 歌曲缓存
		builder.Services.AddTransient<CacheManagePage>();
		builder.Services.AddTransient<CacheManageViewModel>();

		// V2.15 关于 / 第三方开源组件引用
		builder.Services.AddTransient<AboutPage>();
		builder.Services.AddTransient<AboutViewModel>();
		builder.Services.AddTransient<ThirdPartyLicensesPage>();
		builder.Services.AddTransient<ThirdPartyLicensesViewModel>();

		// V2.15 消息通知
		builder.Services.AddTransient<NotificationsPage>();
		builder.Services.AddTransient<NotificationsViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		ServiceHelper.Provider = app.Services;
		return app;
	}
}
