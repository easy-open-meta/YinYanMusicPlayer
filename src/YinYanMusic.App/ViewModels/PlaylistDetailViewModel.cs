using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

public partial class PlaylistDetailViewModel(IMusicApi api, PlayerService player) : ObservableObject
{
    private long _playlistId;
    private string? _originalCoverUrl;

    [ObservableProperty]
    private string name = "歌单";

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private string ownerName = string.Empty;

    [ObservableProperty]
    private bool isOwner;

    [ObservableProperty]
    private bool isCollected;

    public string CoverUrl { get; private set; } = string.Empty;

    public ObservableCollection<SongDto> Songs { get; } = [];

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task LoadAsync(string? idParam)
    {
        if (!long.TryParse(idParam, out var id)) return;
        _playlistId = id;

        if (id == 0)
        {
            Name = "我喜欢的音乐";
            Description = null;
            OwnerName = "我";
            IsOwner = true;
            IsCollected = false;
            var liked = await api.GetLikedSongsAsync();
            Songs.Clear();
            foreach (var song in liked) Songs.Add(song);
            CoverUrl = Songs.Count > 0 ? Services.ApiConfig.Absolute(Songs[0].CoverUrl) : string.Empty;
            OnPropertyChanged(nameof(CoverUrl));
            return;
        }

        var detail = await api.GetPlaylistAsync(id);
        if (detail is null) return;
        Name = detail.Name;
        Description = detail.Description;
        OwnerName = $"by {detail.OwnerName}";
        IsOwner = detail.IsOwner;
        IsCollected = detail.IsCollected;
        _originalCoverUrl = detail.CoverUrl;
        Songs.Clear();
        foreach (var song in detail.Songs) Songs.Add(song);
        await SongMenuHelper.MarkLikedAsync(api, Songs);

        CoverUrl = !string.IsNullOrWhiteSpace(detail.CoverUrl)
            ? Services.ApiConfig.Absolute(detail.CoverUrl)
            : (Songs.Count > 0 ? Services.ApiConfig.Absolute(Songs[0].CoverUrl) : string.Empty);
        OnPropertyChanged(nameof(CoverUrl));

    }

    [RelayCommand]
    private void PlayAll()
    {
        if (Songs.Count == 0) return;
        int startIndex = player.PlayMode == PlayMode.Random
            ? Random.Shared.Next(Songs.Count)
            : 0;
        player.PlayQueue(Songs, startIndex, Name);
        _ = Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private void PlaySong(SongDto song)
    {
        if (player.Current?.Id == song.Id) { _ = Shell.Current.GoToAsync("nowplaying"); return; }
        var index = Songs.IndexOf(song);
        player.PlayQueue(Songs, index >= 0 ? index : 0, Name);
        _ = Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private async Task ToggleCollectAsync()
    {
        var ok = IsCollected
            ? await api.UncollectPlaylistAsync(_playlistId)
            : await api.CollectPlaylistAsync(_playlistId);
        if (ok) IsCollected = !IsCollected;
    }

    [RelayCommand]
    private async Task RemoveSongAsync(SongDto song)
    {
        if (!IsOwner) return;
        var removed = false;
        if (_playlistId == 0)
        {
            if (await api.UnlikeAsync(song.Id))
                removed = Songs.Remove(song);
        }
        else
        {
            if (await api.RemoveSongFromPlaylistAsync(_playlistId, song.Id))
                removed = Songs.Remove(song);
        }

        if (removed && string.IsNullOrWhiteSpace(_originalCoverUrl))
        {
            CoverUrl = Songs.Count > 0 ? Services.ApiConfig.Absolute(Songs[0].CoverUrl) : string.Empty;
            OnPropertyChanged(nameof(CoverUrl));
        }
    }

    [RelayCommand]
    private async Task DeletePlaylistAsync()
    {
        if (!IsOwner) return;
        var ok = await api.DeletePlaylistAsync(_playlistId);
        if (ok) await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private Task GoNowPlayingAsync() => Shell.Current.GoToAsync("nowplaying");

    public async Task<bool> IsSongLikedAsync(long songId)
    {
        try
        {
            var liked = await api.GetLikedSongsAsync();
            return liked.Any(s => s.Id == songId);
        }
        catch { return false; }
    }

    public async Task<bool> LikeSongAsync(long songId) => await api.LikeAsync(songId);

    public async Task<bool> UnlikeSongAsync(long songId) => await api.UnlikeAsync(songId);

    public void InsertNextSong(SongDto song) => player.InsertNext(song);

    public Task GoToArtistAsync(long artistId) => Shell.Current.GoToAsync($"artist?artistId={artistId}");
}