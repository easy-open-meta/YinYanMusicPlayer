using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class ChangePasswordPage : ContentPage
{
	public ChangePasswordPage() : this(ServiceHelper.GetRequiredService<ChangePasswordViewModel>())
	{
	}

	public ChangePasswordPage(ChangePasswordViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}
