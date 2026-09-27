using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class BindEmailPage : ContentPage
{
	private readonly BindEmailViewModel _vm;

	public BindEmailPage() : this(ServiceHelper.GetRequiredService<BindEmailViewModel>())
	{
	}

	public BindEmailPage(BindEmailViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = _vm.LoadCommand.ExecuteAsync(null);
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// 停掉验证码倒计时，避免离开页面后还在后台空转
		_vm.StopCountdown();
	}
}
