using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class FollowArtistsPage : ContentPage
{
	private readonly FollowArtistsViewModel _vm;

	public FollowArtistsPage() : this(ServiceHelper.GetRequiredService<FollowArtistsViewModel>())
	{
	}

	public FollowArtistsPage(FollowArtistsViewModel vm)
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
