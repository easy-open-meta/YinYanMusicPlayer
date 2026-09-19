using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class FollowersPage : ContentPage
{
	private readonly FollowersViewModel _vm;

	public FollowersPage() : this(ServiceHelper.GetRequiredService<FollowersViewModel>())
	{
	}

	public FollowersPage(FollowersViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
	}

	protected override void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		_ = _vm.LoadCommand.ExecuteAsync(null);
	}
}
