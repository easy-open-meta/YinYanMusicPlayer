using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class LoginPage : ContentPage
{
	public LoginPage() : this(ServiceHelper.GetRequiredService<LoginViewModel>())
	{
	}

	public LoginPage(LoginViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}