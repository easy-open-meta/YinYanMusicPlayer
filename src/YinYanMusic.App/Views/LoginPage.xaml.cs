using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class LoginPage : ContentPage
{
	private bool _navigatingToSettings;
	private bool _navigatingToLocalMusic;

	public LoginPage() : this(ServiceHelper.GetRequiredService<LoginViewModel>())
	{
	}

	public LoginPage(LoginViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}

	/// <summary>M0.5：登录页底部「服务器设置」入口。</summary>
	private async void OnOpenSettings(object? sender, EventArgs e)
	{
		if (_navigatingToSettings) return;
		_navigatingToSettings = true;
		try
		{
			// 全局路由页用相对路径压栈导航；`///` 绝对路由仅限 Shell 顶层路由（main/login/register）。
			await Shell.Current.GoToAsync("settings");
		}
		catch (Exception ex)
		{
#if ANDROID
			Android.Util.Log.Warn("YinYan", $"navigate to settings failed: {ex.Message}");
#else
			System.Diagnostics.Debug.WriteLine($"[LoginPage] navigate to settings failed: {ex.Message}");
#endif
		}
		finally
		{
			_navigatingToSettings = false;
		}
	}

	/// <summary>
	/// V2.6：登录页「本地音乐」入口。**无需登录**即可进入 ——
	/// 恢复登录态需要联网（MeAsync 失败会登出并停在本页），
	/// 而本地音乐恰恰是给"没网/后端不可达"准备的，所以入口不能卡在登录后面。
	/// 本地音乐页本身不调用任何服务端接口，因此未登录状态下功能完整。
	/// </summary>
	private async void OnOpenLocalMusic(object? sender, EventArgs e)
	{
		if (_navigatingToLocalMusic) return;
		_navigatingToLocalMusic = true;
		try
		{
			await Shell.Current.GoToAsync("localMusic");
		}
		catch (Exception ex)
		{
#if ANDROID
			Android.Util.Log.Warn("YinYan", $"navigate to localMusic failed: {ex.Message}");
#else
			System.Diagnostics.Debug.WriteLine($"[LoginPage] navigate to localMusic failed: {ex.Message}");
#endif
		}
		finally
		{
			_navigatingToLocalMusic = false;
		}
	}
}