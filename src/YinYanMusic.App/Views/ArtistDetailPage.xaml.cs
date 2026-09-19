using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Views;

[QueryProperty(nameof(ArtistId), "artistId")]
public partial class ArtistDetailPage : ContentPage
{
	private readonly ArtistDetailViewModel _vm;
	private readonly IMusicApi _api;
	private readonly PlayerService _player;

	public ArtistDetailPage() : this(ServiceHelper.GetRequiredService<ArtistDetailViewModel>())
	{
	}

	public ArtistDetailPage(ArtistDetailViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
		_api = ServiceHelper.GetRequiredService<IMusicApi>();
		_player = ServiceHelper.GetRequiredService<PlayerService>();
	}

	public string ArtistId
	{
		set => _ = _vm.LoadCommand.ExecuteAsync(value);
	}

	private async void OnMoreClicked(object? sender, EventArgs e)
	{
		if (sender is not Button btn || btn.CommandParameter is not SongDto song) return;
		// 带上歌手页的歌曲列表，保证「列表循环」能走到下一首
		await SongMenuHelper.ShowMenuAsync(song, _api, _player, _vm.Songs, _vm.ArtistName);
	}
}
