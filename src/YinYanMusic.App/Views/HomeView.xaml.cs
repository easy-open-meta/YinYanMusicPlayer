using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Views;

public partial class HomeView : ContentView
{
	private readonly IMusicApi _api;
	private readonly PlayerService _player;

	public HomeView() : this(ServiceHelper.GetRequiredService<HomeViewModel>())
	{
	}

	public HomeView(HomeViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
		_api = ServiceHelper.GetRequiredService<IMusicApi>();
		_player = ServiceHelper.GetRequiredService<PlayerService>();
	}

	private async void OnMoreClicked(object? sender, EventArgs e)
	{
		if (sender is not Button btn || btn.CommandParameter is not SongDto song) return;
		await SongMenuHelper.ShowMenuAsync(song, _api, _player);
	}
}
