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

    /// <summary>
    /// 当前歌单 Id。0 = 虚拟歌单「我喜欢的音乐」（本地过滤出来的集合，没有服务端歌单行），
    /// 评论入口据此判断能不能开（V2.9）。
    /// </summary>
    public long PlaylistId => _playlistId;

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

    /// <summary>歌单播放次数 = 歌单内全部歌曲 PlayCount 之和（V2.4）。</summary>
    [ObservableProperty]
    private long playCount;

    /// <summary>歌单内歌曲搜索关键词（V2.4）。输入即搜。</summary>
    [ObservableProperty]
    private string? searchKeyword;

    /// <summary>
    /// 搜索框是否展开（默认收起）。
    /// 收起时只显示右侧放大镜按钮，点它才展开 —— 歌单头部本来就紧凑，
    /// 常驻一条搜索框会占掉一行高度，视觉上也压过「播放全部」。
    /// </summary>
    [ObservableProperty]
    private bool isSearchVisible;

    /// <summary>搜索中（避免连打时并发请求）。</summary>
    [ObservableProperty]
    private bool isSearching;

    /// <summary>是否处于搜索态（有关键词）。空态提示据此区分"歌单为空"与"没搜到"。</summary>
    public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchKeyword);

    /// <summary>搜索无结果（用于显示空态提示）。</summary>
    public bool IsSearchEmpty => IsSearchActive && Songs.Count == 0;

    /// <summary>列表为空但没在搜索 —— 歌单本身没有歌。</summary>
    public bool IsPlaylistEmpty => !IsSearchActive && Songs.Count == 0;

    public string CoverUrl { get; private set; } = string.Empty;

    public ObservableCollection<SongDto> Songs { get; } = [];

    partial void OnSearchKeywordChanged(string? value)
    {
        OnPropertyChanged(nameof(IsSearchActive));
        if (_suppressSearchReload) return;
        _ = ApplySearchAsync();
    }

    /// <summary>
    /// 执行歌单内搜索（V2.4）。空关键词回到完整列表。
    /// 走服务端接口（而非本地过滤），保证大歌单下也只取一页数据。
    /// </summary>
    private async Task ApplySearchAsync()
    {
        // 虚拟歌单（id=0，"我喜欢的音乐"）没有服务端歌单行，只能本地过滤
        if (_playlistId == 0)
        {
            ApplyLocalFilter();
            return;
        }

        var kw = SearchKeyword;
        IsSearching = true;
        try
        {
            var result = await api.SearchPlaylistSongsAsync(_playlistId, kw, 1, SearchPageSize);
            Songs.Clear();
            foreach (var s in result.Items) Songs.Add(s);
            await SongMenuHelper.MarkLikedAsync(api, Songs);
            RefreshEmptyStates();
        }
        catch
        {
            // 搜索失败不清空列表，避免用户看到"歌曲突然全没了"
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>本地过滤（仅用于 id=0 的虚拟歌单）。</summary>
    private void ApplyLocalFilter()
    {
        var kw = SearchKeyword?.Trim();
        Songs.Clear();
        if (string.IsNullOrWhiteSpace(kw))
        {
            foreach (var s in _allSongs) Songs.Add(s);
        }
        else
        {
            foreach (var s in _allSongs.Where(s =>
                s.Title.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                (s.ArtistName?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.AlbumName?.Contains(kw, StringComparison.OrdinalIgnoreCase) ?? false)))
                Songs.Add(s);
        }
        RefreshEmptyStates();
    }

    /// <summary>虚拟歌单的完整列表缓存（本地搜索用）。</summary>
    private readonly List<SongDto> _allSongs = [];

    /// <summary>搜索每页条数：歌单内搜索一次多取一些，减少翻页。</summary>
    private const int SearchPageSize = 50;

    private void RefreshEmptyStates()
    {
        OnPropertyChanged(nameof(IsSearchEmpty));
        OnPropertyChanged(nameof(IsPlaylistEmpty));
    }

    /// <summary>清空搜索（搜索框的清除按钮）。</summary>
    [RelayCommand]
    private void ClearSearch() => SearchKeyword = null;

    /// <summary>切换搜索框展开/收起（右上角放大镜按钮）。</summary>
    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        // 收起时顺带清空关键词并恢复完整列表（否则会出现
        // "搜索框看不见了，但列表还被关键词过滤着"的困惑状态）
        if (!IsSearchVisible) SearchKeyword = null;
    }

    /// <summary>收起搜索框（页面返回/离开时调用，避免下次进来还展开着）。</summary>
    public void CollapseSearch()
    {
        // 离开页面时只是把 UI 状态复位，不必再发一次请求把完整列表拉回来
        // （页面马上要被销毁，这次请求纯属浪费，还可能与导航竞争）
        _suppressSearchReload = true;
        try
        {
            IsSearchVisible = false;
            SearchKeyword = null;
        }
        finally
        {
            _suppressSearchReload = false;
        }
    }

    /// <summary>抑制一次搜索重载（仅用于页面离开时的状态复位）。</summary>
    private bool _suppressSearchReload;

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
            // 虚拟歌单：缓存完整列表供本地搜索；播放次数按同样口径累加
            _allSongs.Clear();
            _allSongs.AddRange(liked);
            PlayCount = liked.Sum(s => (long)s.PlayCount);
            RefreshEmptyStates();
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
        // V2.4：歌单播放次数（服务端按歌单内歌曲 PlayCount 求和算出）
        PlayCount = detail.PlayCount;
        // 搜索框每次进入页面重置为收起状态，避免带着上次的关键词/展开态
        IsSearchVisible = false;
        SearchKeyword = null;
        Songs.Clear();
        foreach (var song in detail.Songs) Songs.Add(song);
        _allSongs.Clear();
        _allSongs.AddRange(detail.Songs);
        await SongMenuHelper.MarkLikedAsync(api, Songs);
        RefreshEmptyStates();

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
        // 同步虚拟歌单的本地缓存，否则搜索时已移除的歌会"复活"
        if (removed) _allSongs.RemoveAll(s => s.Id == song.Id);
        RefreshEmptyStates();
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