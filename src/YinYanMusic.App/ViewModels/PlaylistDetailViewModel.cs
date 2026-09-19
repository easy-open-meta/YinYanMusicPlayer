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
    private int? _categoryId;

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

    /// <summary>系统歌单（"我喜欢的音乐"）：由注册流程生成、与 LikedSongs 绑定，
    /// 不提供改名 / 换标签 / 删除。</summary>
    [ObservableProperty]
    private bool isSystem;

    /// <summary>是否显示"编辑"入口（自己的、且不是系统歌单）。</summary>
    [ObservableProperty]
    private bool canEdit;

    /// <summary>是否显示"删除"入口。</summary>
    [ObservableProperty]
    private bool canDelete;

    /// <summary>当前标签（分区）名；为空时界面上不显示标签 chip。</summary>
    [ObservableProperty]
    private string? categoryName;

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
            // "我喜欢的音乐"不是 Playlists 表里的行（由 LikedSongs 虚拟而成），
            // 没有名字/标签可改，也不允许删除 —— 三个入口全关掉。
            IsSystem = true;
            CanEdit = false;
            CanDelete = false;
            CategoryName = null;
            _categoryId = null;
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
        IsSystem = detail.IsSystem;
        // 系统歌单不给改名/换标签/删除，前端先隐藏入口；服务端另有一道同样的校验兜底
        CanEdit = detail.IsOwner && !detail.IsSystem;
        CanDelete = CanEdit;
        CategoryName = detail.CategoryName;
        _categoryId = detail.CategoryId;
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

    /// <summary>编辑歌单：改名 + 选标签（分区）。仅自己的非系统歌单可用。</summary>
    [RelayCommand]
    private async Task EditPlaylistAsync()
    {
        if (!CanEdit) return;

        var categories = await api.GetCategoriesAsync();
        var edited = await SongMenuHelper.ShowEditPlaylistAsync("编辑歌单", Name, categories, _categoryId);
        if (edited is null) return;   // 用户取消

        // 与"新建歌单"保持同一套规则：不允许重名（排除自己）
        var mine = await api.GetMyPlaylistsAsync();
        if (mine.Any(p => p.Id != _playlistId
                          && string.Equals(p.Name, edited.Name, StringComparison.OrdinalIgnoreCase)))
        {
            await SongMenuHelper.ShowMessageDialogAsync("提示", $"已存在名为「{edited.Name}」的歌单，请换个名字", "确定");
            return;
        }

        // CategoryId 为 null 表示用户在弹框里选了"无标签"，此时要显式清空
        var ok = await api.UpdatePlaylistAsync(_playlistId, edited.Name, edited.CategoryId,
                                               clearCategory: edited.CategoryId is null);
        if (!ok)
        {
            await SongMenuHelper.ShowMessageDialogAsync("提示", "保存失败，请稍后重试", "确定");
            return;
        }

        Name = edited.Name;
        _categoryId = edited.CategoryId;
        CategoryName = categories.FirstOrDefault(c => c.Id == edited.CategoryId)?.Name;
    }

    [RelayCommand]
    private async Task DeletePlaylistAsync()
    {
        // 系统歌单（"我喜欢的音乐"）不提供删除；服务端也会拒绝，这里只是不弹框
        if (!CanDelete) return;

        var confirmed = await SongMenuHelper.ShowRoundedConfirmAsync(
            "删除歌单",
            $"确定要删除「{Name}」吗？歌单里的歌曲不会被删除。",
            "删除", "取消", destructive: true);
        if (!confirmed) return;

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

    /// <summary>
    /// 「下一首播放」。队列为空（还没开始播）时不能只把这一首塞进去 ——
    /// 队列长度为 1 时列表循环的下一首就是它自己，会一直重播同一首。
    /// 这种情况改成把整个歌单入队、从这首歌开始播，之后才能自然往下走。
    /// </summary>
    public void InsertNextSong(SongDto song)
    {
        if (player.Queue.Count == 0 && Songs.Count > 0)
        {
            var index = Songs.IndexOf(song);
            player.PlayQueue(Songs, index >= 0 ? index : 0, Name);
            return;
        }
        player.InsertNext(song);
    }

    public Task GoToArtistAsync(long artistId) => Shell.Current.GoToAsync($"artist?artistId={artistId}");
}