using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Views;

/// <summary>
/// 分区详情页。分区展示信息通过导航查询串带入（categoryId/name/slogan/colorHex/icon），
/// QueryProperty 收到后转喂 ZoneViewModel.Apply；categoryId 到齐即触发该分区的歌曲与歌单加载。
/// </summary>
[QueryProperty(nameof(CategoryId), "categoryId")]
[QueryProperty(nameof(ZoneName), "name")]
[QueryProperty(nameof(Slogan), "slogan")]
[QueryProperty(nameof(ColorHex), "colorHex")]
[QueryProperty(nameof(Icon), "icon")]
[QueryProperty(nameof(ContentMode), "mode")]
public partial class ZonePage : ContentPage
{
	private readonly ZoneViewModel _vm;
	private readonly IMusicApi _api;
	private readonly PlayerService _player;

	public ZonePage() : this(ServiceHelper.GetRequiredService<ZoneViewModel>())
	{
	}

	public ZonePage(ZoneViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
		_api = ServiceHelper.GetRequiredService<IMusicApi>();
		_player = ServiceHelper.GetRequiredService<PlayerService>();
	}

	// 注意：属性名 Name 与 ContentPage.Name 冲突，故用 ZoneName 承接查询参数 "name"。
	public string CategoryId { set => _vm.Apply("categoryId", value); }
	public string ZoneName { set => _vm.Apply("name", value); }
	public string Slogan { set => _vm.Apply("slogan", value); }
	public string ColorHex { set => _vm.Apply("colorHex", value); }
	public string Icon { set => _vm.Apply("icon", value); }

	/// <summary>内容类型（both/songs/playlists）。注意：它在导航串里排在最后，所以比 categoryId 晚到，
	/// ZoneViewModel 会等这一轮参数喂完再发请求。</summary>
	public string ContentMode { set => _vm.Apply("mode", value); }

	/// <summary>歌曲行的「更多」菜单。带上分区歌曲整张列表，保证「列表循环」能走到下一首。</summary>
	private async void OnMoreClicked(object? sender, EventArgs e)
	{
		if (sender is not Button btn || btn.CommandParameter is not SongDto song) return;
		await SongMenuHelper.ShowMenuAsync(song, _api, _player, _vm.Songs, _vm.Name);
	}
}
