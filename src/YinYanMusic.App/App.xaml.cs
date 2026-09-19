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

	protected override Window CreateWindow(IActivationState? activationState)
	{
		_window = new Window(new ContentPage { BackgroundColor = Color.FromArgb("#F6F6F9") });
		return _window;
	}

	protected override async void OnStart()
	{
		base.OnStart();
		try
		{
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
}
