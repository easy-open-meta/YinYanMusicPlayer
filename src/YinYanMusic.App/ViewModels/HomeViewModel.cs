using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

public partial class HomeViewModel(IMusicApi api, PlayerService player) : ObservableObject
{
    public ObservableCollection<CategoryDto> Categories { get; } = [];
    public ObservableCollection<SongDto> HotSongs { get; } = [];
    public ObservableCollection<PlaylistDto> Playlists { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    private bool _loaded;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var cats = await api.GetCategoriesAsync();
            var songs = await api.SearchSongsAsync(pageSize: 20);
            var lists = await api.SearchPlaylistsAsync(pageSize: 10);

            Replace(Categories, cats);
            Replace(HotSongs, songs.Items);
            await SongMenuHelper.MarkLikedAsync(api, HotSongs);
            // 系统歌单（如“我喜欢的音乐”）个人可见，不应出现在公开精选歌单列表里
            // （服务端 SearchAsync 已排除，这里再按 IsSystem 标志兜底过滤，不依赖歌单名）
            Replace(Playlists, lists.Items.Where(p => !p.IsSystem));
            _loaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>进入某个音乐专区的详情页。展示参数走查询串传，避免分区页二次请求分区信息。</summary>
    [RelayCommand]
    private Task OpenZoneAsync(CategoryDto zone) => Shell.Current.GoToAsync($"zone?{ZoneQuery.Build(zone)}");

    /// <summary>进入“全部专区”网格页。</summary>
    [RelayCommand]
    private Task OpenZonesAsync() => Shell.Current.GoToAsync("zones");

    [RelayCommand]
    private void PlayHot(SongDto song)
    {
        if (player.Current?.Id == song.Id) { _ = GoNowPlayingAsync(); return; }
        var index = HotSongs.IndexOf(song);
        player.PlayQueue(HotSongs, index >= 0 ? index : 0, "发现");
        _ = GoNowPlayingAsync();
    }

    [RelayCommand]
    private Task GoNowPlayingAsync() => Shell.Current.GoToAsync("nowplaying");

    [RelayCommand]
    private async Task OpenPlaylistAsync(PlaylistDto playlist) =>
        await Shell.Current.GoToAsync($"playlist?id={playlist.Id}");

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}