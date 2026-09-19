using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class RegisterPage : ContentPage
{
	public RegisterPage() : this(ServiceHelper.GetRequiredService<RegisterViewModel>())
	{
	}

	public RegisterPage(RegisterViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}