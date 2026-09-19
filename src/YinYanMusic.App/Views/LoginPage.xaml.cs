using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class LoginPage : ContentPage
{
	private bool _navigatingToSettings;

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
}