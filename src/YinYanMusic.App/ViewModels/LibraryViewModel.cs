using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

public partial class LibraryViewModel(IMusicApi api, IAuthService auth, PlayerService player) : ObservableObject
{
    public ObservableCollection<PlaylistDto> MyPlaylists { get; } = [];
    public ObservableCollection<PlaylistDto> CollectedPlaylists { get; } = [];
    public ObservableCollection<SongDto> LikedSongs { get; } = [];

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string avatarUrl = string.Empty;

    [ObservableProperty]
    private string gender = string.Empty;

    [ObservableProperty]
    private string stats = string.Empty;

    /// <summary>关注数量（订阅者）。</summary>
    [ObservableProperty]
    private int followingCount;

    /// <summary>粉丝数量（跟踪者）。</summary>
    [ObservableProperty]
    private int followersCount;

    [ObservableProperty]
    private int myPlaylistCount;

    [ObservableProperty]
    private int collectedPlaylistCount;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var me = await api.MeAsync();
            DisplayName = me?.DisplayName ?? auth.CurrentUser?.DisplayName ?? "未登录";
            AvatarUrl = me?.AvatarUrl ?? string.Empty;
            Gender = me?.Gender ?? string.Empty;

            var overview = await api.GetOverviewAsync();
            // “关注”统计 = 关注的用户（Follow 表）+ 关注的歌手（ArtistFollow 表），两者都算关注对象。
            FollowingCount = (int)(overview.ArtistFollowing + overview.Following);
            FollowersCount = (int)overview.Followers;

            var my = await api.GetMyPlaylistsAsync();
            var collected = (await api.GetCollectedPlaylistsAsync()).ToList();
            var liked = await api.GetLikedSongsAsync();
            var myFiltered = my.Where(p => !p.IsSystem).ToList();
            // 歌单封面回退已由服务端统一处理（无封面时取歌单内第一首歌的封面）
            Replace(MyPlaylists, myFiltered);
            Replace(CollectedPlaylists, collected);
            Replace(LikedSongs, liked);
            var firstCover = liked.Count > 0 ? liked[0].CoverUrl : null;
            var likedPlaylist = new PlaylistDto(0, "我喜欢的音乐", null, firstCover, null, null, 0, "我", liked.Count, 0, DateTime.MinValue, IsSystem: true);
            MyPlaylists.Insert(0, likedPlaylist);
            MyPlaylistCount = MyPlaylists.Count;
            CollectedPlaylistCount = collected.Count;
            Stats = $"{liked.Count} 首喜欢的音乐 · {MyPlaylists.Count} 个歌单 · {collected.Count} 个收藏";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>个人卡三个统计位的跳转：关注（歌手）/ 歌单 / 粉丝。</summary>
    [RelayCommand]
    private Task OpenFollowingAsync() => Shell.Current.GoToAsync("followArtists");

    [RelayCommand]
    private Task OpenMyPlaylistsAsync() => Shell.Current.GoToAsync("myPlaylists");

    [RelayCommand]
    private Task OpenFollowersAsync() => Shell.Current.GoToAsync("followers");

    [RelayCommand]
    private async Task PlayLikedAsync()
    {
        if (LikedSongs.Count == 0)
        {
            await SongMenuHelper.ShowMessageDialogAsync("提示", "还没有喜欢的歌曲。", "好的");
            return;
        }
        player.PlayQueue(LikedSongs, 0, "我喜欢的音乐");
        await Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private async Task OpenPlaylistAsync(PlaylistDto playlist)
    {

        await Shell.Current.GoToAsync($"playlist?id={playlist.Id}");
    }

    [RelayCommand]
    private async Task CreatePlaylistAsync()
    {
        var name = await SongMenuHelper.ShowInputDialogAsync("新建歌单", "歌单名称：", "我的歌单");
        if (string.IsNullOrWhiteSpace(name)) return;
        var trimmedName = name.Trim();
        if (trimmedName == "我喜欢的音乐")
        {
            await SongMenuHelper.ShowMessageDialogAsync("提示", "该名称为系统保留，请换个名字", "确定");
            return;
        }
        var existing = await api.GetMyPlaylistsAsync();
        if (existing.Any(p => string.Equals(p.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
        {
            await SongMenuHelper.ShowMessageDialogAsync("提示", $"已存在名为「{trimmedName}」的歌单，请换个名字", "确定");
            return;
        }
        var created = await api.CreatePlaylistAsync(trimmedName, null);
        if (created is not null) await LoadAsync();
    }

    [RelayCommand]
    private async Task AddToPlaylistAsync(SongDto song)
    {
        var playlists = await api.GetMyPlaylistsAsync();
        if (playlists.Count == 0)
        {
            await SongMenuHelper.ShowMessageDialogAsync("提示", "还没有自己的歌单，先创建一个吧。", "好的");
            return;
        }
        var names = playlists.Select(p => p.Name).ToList();
        var picked = await SongMenuHelper.ShowBottomSheetAsync($"收藏「{song.Title}」到歌单", names);
        var target = playlists.FirstOrDefault(p => p.Name == picked);
        if (target is null) return;
        await api.AddSongsToPlaylistAsync(target.Id, [song.Id]);
        await SongMenuHelper.ShowMessageDialogAsync("提示", $"已加入歌单「{target.Name}」。", "好的");
        await LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task ToggleLikeAsync(SongDto song)
    {
        var ok = await api.LikeAsync(song.Id);
        if (ok) await SongMenuHelper.ShowMessageDialogAsync("提示", $"已喜欢「{song.Title}」。", "好的");
    }

    [RelayCommand]
    private void PlaySong(SongDto song)
    {
        if (player.Current?.Id == song.Id) { _ = Shell.Current.GoToAsync("nowplaying"); return; }
        var index = LikedSongs.IndexOf(song);
        player.PlayQueue(LikedSongs, index >= 0 ? index : 0, "我喜欢的音乐");
        _ = Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var confirm = await SongMenuHelper.ShowConfirmDialogAsync("退出登录", "确定要退出当前账号吗？", "退出", "取消");
        if (!confirm) return;
        await auth.LogoutAsync();
        await Shell.Current.GoToAsync("///login");
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}

public partial class NowPlayingViewModel(PlayerService player, IMusicApi api) : ObservableObject
{
    public PlayerService Player { get; } = player;

    [ObservableProperty]
    private bool isLiked;

    /// <summary>当前歌手是否已关注（播放页歌手名旁的 + / ✓ 按钮）。</summary>
    [ObservableProperty]
    private bool isArtistFollowed;

    /// <summary>没有歌手信息（ArtistId=0）时隐藏关注按钮。</summary>
    [ObservableProperty]
    private bool canFollowArtist;

    [RelayCommand]
    private async Task ToggleLikeAsync()
    {
        if (Player.Current is null) return;
        if (IsLiked)
        {
            await api.UnlikeAsync(Player.Current.Id);
            IsLiked = false;
        }
        else
        {
            await api.LikeAsync(Player.Current.Id);
            IsLiked = true;
        }
    }

    public async Task RefreshIsLikedAsync()
    {
        if (Player.Current is null) { IsLiked = false; return; }
        try
        {
            var liked = await api.GetLikedSongsAsync();
            IsLiked = liked.Any(s => s.Id == Player.Current.Id);
        }
        catch { IsLiked = false; }
    }

    /// <summary>播放页的快速关注：+ 关注、✓ 取消关注（图标切换即是反馈，不再弹提示）。</summary>
    [RelayCommand]
    private async Task ToggleFollowArtistAsync()
    {
        var artistId = Player.Current?.ArtistId ?? 0;
        if (artistId <= 0) return;
        if (IsArtistFollowed)
        {
            if (await api.UnfollowArtistAsync(artistId)) IsArtistFollowed = false;
        }
        else
        {
            if (await api.FollowArtistAsync(artistId)) IsArtistFollowed = true;
        }
    }

    public async Task RefreshArtistFollowedAsync()
    {
        var artistId = Player.Current?.ArtistId ?? 0;
        CanFollowArtist = artistId > 0;
        if (artistId <= 0) { IsArtistFollowed = false; return; }
        try
        {
            var ids = await api.GetFollowedArtistIdsAsync();
            IsArtistFollowed = ids.Contains(artistId);
        }
        catch { IsArtistFollowed = false; }
    }

    [RelayCommand]
    private void Toggle() => player.TogglePlayPause();

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private Task NextAsync() => player.NextAsync();

    [RelayCommand]
    private Task PreviousAsync() => player.PreviousAsync();

    [RelayCommand]
    private void SliderChanged(double value) => player.SeekTo(value);

    [RelayCommand]
    private void CyclePlayMode() => player.CyclePlayMode();
}