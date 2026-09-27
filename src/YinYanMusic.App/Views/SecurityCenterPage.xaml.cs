using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class SecurityCenterPage : ContentPage
{
	private readonly SecurityCenterViewModel _vm;

	public SecurityCenterPage() : this(ServiceHelper.GetRequiredService<SecurityCenterViewModel>())
	{
	}

	public SecurityCenterPage(SecurityCenterViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		// 每次返回本页都重新拉一次：从「邮箱绑定」子页回来时邮箱可能已更新
		_ = _vm.LoadCommand.ExecuteAsync(null);
	}
}
