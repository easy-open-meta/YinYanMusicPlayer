using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Views;

[QueryProperty(nameof(PlaylistId), "id")]
public partial class PlaylistDetailPage : ContentPage
{
	private readonly PlaylistDetailViewModel _vm;

	public PlaylistDetailPage() : this(ServiceHelper.GetRequiredService<PlaylistDetailViewModel>())
	{
	}

	public PlaylistDetailPage(PlaylistDetailViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
	}

	public string PlaylistId
	{
		set => _ = _vm.LoadCommand.ExecuteAsync(value);
	}

	private async void OnMoreClicked(object? sender, EventArgs e)
	{
		if (sender is not Button btn || btn.CommandParameter is not SongDto song) return;

		try
		{
			var isLiked = await _vm.IsSongLikedAsync(song.Id);
			var artistLabel = $"歌手：{song.ArtistName}";
			var likeLabel = isLiked ? "取消喜欢" : "喜欢";

			var options = new List<string> { "下一首播放", artistLabel, likeLabel, "添加到歌单..." };
			if (_vm.IsOwner) options.Add("删除");

		var choice = await SongMenuHelper.ShowBottomSheetAsync(song.Title, options);
		if (choice is null) return;

			if (choice == "下一首播放")
			{
				_vm.InsertNextSong(song);
			}
			else if (choice == artistLabel)
			{
				if (song.ArtistId > 0) await _vm.GoToArtistAsync(song.ArtistId);
			}
			else if (choice == likeLabel)
			{
				if (isLiked) { await _vm.UnlikeSongAsync(song.Id); song.IsLiked = false; }
				else { await _vm.LikeSongAsync(song.Id); song.IsLiked = true; }
			}
			else if (choice == "添加到歌单...")
			{
				// 复用 SongMenuHelper 的添加流程：选目标歌单、已含该歌的歌单禁选、完成后刷新歌单列表
				await SongMenuHelper.ShowAddToPlaylistAsync(song, ServiceHelper.GetRequiredService<IMusicApi>());
			}
			else if (choice == "删除")
			{
				await _vm.RemoveSongCommand.ExecuteAsync(song);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[菜单异常] {ex.Message}");
		}
	}
}
