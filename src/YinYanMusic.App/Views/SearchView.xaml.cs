using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Views;

public partial class SearchView : ContentView
{
	private readonly IMusicApi _api;
	private readonly PlayerService _player;

	public SearchView() : this(ServiceHelper.GetRequiredService<SearchViewModel>())
	{
	}

	public SearchView(SearchViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
		_api = ServiceHelper.GetRequiredService<IMusicApi>();
		_player = ServiceHelper.GetRequiredService<PlayerService>();
	}

	private async void OnMoreClicked(object? sender, EventArgs e)
	{
		if (sender is not Button btn || btn.CommandParameter is not SongDto song) return;

		// 搜索结果和热门歌曲共用同一个「更多」按钮，按 Id 判断它属于哪张列表，
		// 把那张列表作为播放上下文传下去（否则队列只有一首，列表循环会原地重播）
		var vm = BindingContext as SearchViewModel;
		IReadOnlyList<SongDto>? context = null;
		if (vm is not null)
		{
			if (ContainsSong(vm.SearchResults, song)) context = vm.SearchResults;
			else if (ContainsSong(vm.HotSongs, song)) context = vm.HotSongs;
		}
		await SongMenuHelper.ShowMenuAsync(song, _api, _player, context, "搜索");
	}

	private static bool ContainsSong(IEnumerable<SongDto> list, SongDto song)
	{
		foreach (var item in list)
			if (ReferenceEquals(item, song) || item.Id == song.Id) return true;
		return false;
	}
}
