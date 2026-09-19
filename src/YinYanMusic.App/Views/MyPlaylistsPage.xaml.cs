using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class MyPlaylistsPage : ContentPage
{
	private readonly MyPlaylistsViewModel _vm;

	public MyPlaylistsPage() : this(ServiceHelper.GetRequiredService<MyPlaylistsViewModel>())
	{
	}

	public MyPlaylistsPage(MyPlaylistsViewModel vm)
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
