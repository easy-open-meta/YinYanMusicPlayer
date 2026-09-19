using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 「我的」页关注列表：与“关注”统计同口径 —— 用户区（Follow 表）+ 歌手区（ArtistFollow 表）。
/// </summary>
public partial class FollowArtistsViewModel(IMusicApi api) : ObservableObject
{
    public ObservableCollection<UserDto> Users { get; } = [];
    public ObservableCollection<ArtistDto> Artists { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    public bool HasUsers => Users.Count > 0;
    public bool HasArtists => Artists.Count > 0;
    public bool HasAny => HasUsers || HasArtists;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var usersTask = api.GetFollowedUsersAsync();
            var artistsTask = api.GetFollowedArtistsAsync();
            await Task.WhenAll(usersTask, artistsTask);

            Replace(Users, usersTask.Result);
            Replace(Artists, artistsTask.Result);
            OnPropertyChanged(nameof(HasUsers));
            OnPropertyChanged(nameof(HasArtists));
            OnPropertyChanged(nameof(HasAny));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private Task OpenArtistAsync(ArtistDto artist) => Shell.Current.GoToAsync($"artist?artistId={artist.Id}");

    [RelayCommand]
    private Task OpenUserAsync(UserDto user) => Shell.Current.GoToAsync($"userDetail?userId={user.Id}");

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}
