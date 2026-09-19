using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

public partial class ArtistDetailViewModel(IMusicApi api, PlayerService player) : ObservableObject
{
    private long _artistId;

    [ObservableProperty]
    private string artistName = "歌手";

    [ObservableProperty]
    private int songCount;

    [ObservableProperty]
    private int followerCount;

    [ObservableProperty]
    private int albumCount;

    [ObservableProperty]
    private string avatarUrl = string.Empty;

    public ObservableCollection<SongDto> Songs { get; } = [];

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task LoadAsync(string? idParam)
    {
        if (!long.TryParse(idParam, out var id) || id == 0) return;
        _artistId = id;

        var detail = await api.GetArtistAsync(id);
        if (detail is not null)
        {
            ArtistName = detail.Name;
            FollowerCount = (int)detail.FollowerCount;
            AlbumCount = (int)detail.AlbumCount;
        }

        var result = await api.SearchSongsAsync(artistId: id, pageSize: 100);
        Songs.Clear();
        foreach (var song in result.Items) Songs.Add(song);
        SongCount = (int)result.Total;
        AvatarUrl = Songs.Count > 0 ? Songs[0].CoverUrl ?? string.Empty : string.Empty;
        if (string.IsNullOrEmpty(ArtistName) && Songs.Count > 0)
            ArtistName = Songs[0].ArtistName;
    }

    [RelayCommand]
    private void PlayAll()
    {
        if (Songs.Count == 0) return;
        int startIndex = player.PlayMode == PlayMode.Random
            ? Random.Shared.Next(Songs.Count)
            : 0;
        player.PlayQueue(Songs, startIndex, ArtistName);
        _ = Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private void PlaySong(SongDto song)
    {
        if (player.Current?.Id == song.Id) { _ = Shell.Current.GoToAsync("nowplaying"); return; }
        var index = Songs.IndexOf(song);
        player.PlayQueue(Songs, index >= 0 ? index : 0, ArtistName);
        _ = Shell.Current.GoToAsync("nowplaying");
    }
}