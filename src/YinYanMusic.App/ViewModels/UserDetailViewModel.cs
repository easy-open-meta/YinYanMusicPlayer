using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 用户详情页（搜索用户结果点入）：资料 + 粉丝/关注/歌单统计 + 关注/取关 + 公开歌单列表。
/// 查看自己时隐藏关注按钮（与"我的"页职责重复）。
/// </summary>
public partial class UserDetailViewModel(IMusicApi api, IAuthService auth) : ObservableObject
{
    public long UserId { get; private set; }

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string avatarUrl = string.Empty;

    [ObservableProperty]
    private long followersCount;

    [ObservableProperty]
    private long followingCount;

    [ObservableProperty]
    private long playlistCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FollowButtonText))]
    private bool isFollowing;

    /// <summary>查看自己时不显示关注按钮。</summary>
    [ObservableProperty]
    private bool canFollow;

    public string FollowButtonText => IsFollowing ? "已关注" : "关注";

    public ObservableCollection<PlaylistDto> Playlists { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private Task OpenPlaylistAsync(PlaylistDto playlist) => Shell.Current.GoToAsync($"playlist?id={playlist.Id}");

    /// <summary>由 UserPage 的 QueryProperty 回调喂参数；userId 到齐即加载资料与歌单。</summary>
    public void Apply(string key, string value)
    {
        if (key == "userId" && long.TryParse(value, out var id))
        {
            UserId = id;
            _ = LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        if (UserId <= 0) return;
        IsBusy = true;
        try
        {
            var profile = await api.GetUserProfileAsync(UserId);
            if (profile is null) return;

            DisplayName = profile.DisplayName;
            UserName = profile.UserName;
            AvatarUrl = profile.AvatarUrl ?? string.Empty;
            FollowersCount = profile.FollowersCount;
            FollowingCount = profile.FollowingCount;
            PlaylistCount = profile.PlaylistCount;
            IsFollowing = profile.IsFollowing;
            CanFollow = auth.CurrentUser is null || auth.CurrentUser.Id != UserId;

            var lists = await api.GetUserPlaylistsAsync(UserId);
            Replace(Playlists, lists);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleFollowAsync()
    {
        var ok = IsFollowing
            ? await api.UnfollowUserAsync(UserId)
            : await api.FollowUserAsync(UserId);
        if (!ok) return;
        IsFollowing = !IsFollowing;
        FollowersCount += IsFollowing ? 1 : -1;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}
