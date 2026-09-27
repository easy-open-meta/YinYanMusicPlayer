using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class AccountEditPage : ContentPage
{
	private readonly AccountEditViewModel _vm;

	public AccountEditPage() : this(ServiceHelper.GetRequiredService<AccountEditViewModel>())
	{
	}

	public AccountEditPage(AccountEditViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		// 每次进入都重新拉一次资料：从安全中心返回时邮箱可能已变
		_ = _vm.LoadCommand.ExecuteAsync(null);
	}
}
