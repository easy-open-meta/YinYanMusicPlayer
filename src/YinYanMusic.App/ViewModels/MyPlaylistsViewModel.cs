using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 「我的」页歌单列表：api/me/playlists。
/// 与「我的」页口径保持一致：置顶合成一个「我喜欢的音乐」条目（点击进 playlist?id=0 的喜欢列表），
/// 其下为真实歌单（系统歌单由 IsSystem 过滤，不依赖名称硬编码）。这样列表条数与个人卡的“歌单”统计一致。
/// </summary>
public partial class MyPlaylistsViewModel(IMusicApi api) : ObservableObject
{
    public ObservableCollection<PlaylistDto> Playlists { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var playlists = await api.GetMyPlaylistsAsync();
            var liked = await api.GetLikedSongsAsync();

            Replace(Playlists, playlists.Where(p => !p.IsSystem));

            // 置顶「我喜欢的音乐」（与 LibraryViewModel 同款合成条目；Id=0 由歌单详情页特判渲染喜欢的歌曲）
            var firstCover = liked.Count > 0 ? liked[0].CoverUrl : null;
            var likedPlaylist = new PlaylistDto(0, "我喜欢的音乐", null, firstCover, null, null, 0, "我", liked.Count, 0, DateTime.MinValue, IsSystem: true);
            Playlists.Insert(0, likedPlaylist);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private Task OpenPlaylistAsync(PlaylistDto playlist) => Shell.Current.GoToAsync($"playlist?id={playlist.Id}");

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}
