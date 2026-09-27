using System.ComponentModel;
using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core;
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

		// 展开搜索框时自动聚焦、弹出软键盘 —— 少一次点击
		_vm.PropertyChanged += OnViewModelPropertyChanged;
	}

	public string PlaylistId
	{
		set => _ = _vm.LoadCommand.ExecuteAsync(value);
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(PlaylistDetailViewModel.IsSearchVisible)) return;

		if (_vm.IsSearchVisible)
		{
			// 等布局把 Border 显示出来再聚焦，否则刚设为 Visible 的元素还拿不到焦点
			Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120), () =>
			{
				try { PlaylistSearchEntry?.Focus(); }
				catch { /* 焦点失败不影响功能 */ }
			});
		}
		else
		{
			try { PlaylistSearchEntry?.Unfocus(); }
			catch { /* 忽略 */ }
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		// 离开页面时收起搜索框并清关键词：下次进来应是干净的完整列表，
		// 而不是"列表被上次的关键词过滤着、但搜索框已隐藏"的状态。
		_vm.CollapseSearch();
		_vm.PropertyChanged -= OnViewModelPropertyChanged;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		// 返回本页时重新挂上（OnDisappearing 里解绑过）
		_vm.PropertyChanged -= OnViewModelPropertyChanged;
		_vm.PropertyChanged += OnViewModelPropertyChanged;
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
			// V2.9 评论：与歌曲「更多」菜单同一套文案与语义
			options.Add(await SongMenuHelper.GetCommentMenuLabelAsync(
				ServiceHelper.GetRequiredService<IMusicApi>(), CommentTargets.Song, song.Id));
			if (_vm.IsOwner) options.Add("删除");

			// V2.7 缓存：与歌曲菜单同一套文案与语义（在线歌才有）
			var cacheLabel = await SongMenuHelper.GetCacheMenuLabelAsync(song);
			if (cacheLabel is not null) options.Add(cacheLabel);

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
			else if (SongMenuHelper.IsCommentOption(choice))
			{
				await SongMenuHelper.ShowCommentsAsync(song.Title, CommentTargets.Song, song.Id);
			}
			else if (choice == "删除")
			{
				await _vm.RemoveSongCommand.ExecuteAsync(song);
			}
			else if (cacheLabel is not null && choice == cacheLabel)
			{
				await SongMenuHelper.RunCacheMenuActionAsync(cacheLabel, song);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[菜单异常] {ex.Message}");
		}
	}

	/// <summary>
	/// 打开歌单评论面板（V2.9）。
	/// id=0 是「我喜欢的音乐」这类虚拟歌单 —— 它没有服务端行，评论没有可挂的对象，直接不响应。
	/// </summary>
	private async void OnCommentsClicked(object? sender, EventArgs e)
	{
		if (_vm.PlaylistId <= 0) return;
		await SongMenuHelper.ShowCommentsAsync(_vm.Name, CommentTargets.Playlist, _vm.PlaylistId);
	}
}
