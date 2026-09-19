using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

public partial class SearchViewModel(IMusicApi api, PlayerService player) : ObservableObject
{
    public ObservableCollection<SongDto> HotSongs { get; } = [];
    public ObservableCollection<SongDto> SearchResults { get; } = [];
    public ObservableCollection<AlbumDto> AlbumResults { get; } = [];
    public ObservableCollection<UserDto> UserResults { get; } = [];
    public ObservableCollection<string> SearchHistory { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanClearSearch))]
    [NotifyPropertyChangedFor(nameof(HasAlbumResults))]
    [NotifyPropertyChangedFor(nameof(HasUserResults))]
    private string searchText = string.Empty;

    public bool CanClearSearch => !string.IsNullOrWhiteSpace(SearchText);
    public bool HasAlbumResults => IsSearching && AlbumResults.Count > 0;
    public bool HasUserResults => IsSearching && UserResults.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAlbumResults))]
    [NotifyPropertyChangedFor(nameof(HasUserResults))]
    private bool isSearching;

    [ObservableProperty]
    private bool isBusy;

    private bool _loaded;
    private const int MaxHistory = 10;
    private const string HistoryKey = "search_history";

    /// <summary>
    /// 搜索序号：连续触发搜索（或清空）时，只有最新一次的结果会被采用，
    /// 过期在途请求的结果直接丢弃——修复"快速连搜时结果被静默吞掉/被旧结果覆盖"的问题。
    /// </summary>
    private int _searchSeq;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_loaded) return;
        IsBusy = true;
        try
        {
            var songs = await api.SearchSongsAsync(pageSize: 4);
        Replace(HotSongs, songs.Items);
        await SongMenuHelper.MarkLikedAsync(api, HotSongs);
        LoadHistory();
            _loaded = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        var keyword = SearchText.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            IsSearching = false;
            return;
        }
        var seq = ++_searchSeq;
        IsBusy = true;
        try
        {
            // 三路并行：歌曲（匹配歌名/歌手/专辑名，专辑名由服务端歌曲搜索覆盖）+ 专辑 + 用户
            var songTask = api.SearchSongsAsync(keyword: keyword, pageSize: 50);
            var albumTask = api.SearchAlbumsAsync(keyword, limit: 10);
            var userTask = api.SearchUsersAsync(keyword, limit: 10);
            await Task.WhenAll(songTask, albumTask, userTask);

            // 期间又触发了新的搜索/清空 → 本次结果过期，丢弃（不更新 UI、不写历史）
            if (seq != _searchSeq) return;

            Replace(SearchResults, songTask.Result.Items);
            await SongMenuHelper.MarkLikedAsync(api, SearchResults);
            Replace(AlbumResults, albumTask.Result);
            Replace(UserResults, userTask.Result);
            // 先填数据、后置 IsSearching：触发 HasAlbumResults/HasUserResults 重新求值，
            // 保证"专辑/用户"两个小节当次搜索立即显示（此前通知顺序反了，显隐会滞后一次搜索）
            IsSearching = true;
            AddToHistory(keyword);
        }
        finally
        {
            if (seq == _searchSeq) IsBusy = false;
        }
    }

    /// <summary>点击用户结果：进入用户详情页（资料/关注/公开歌单）。</summary>
    [RelayCommand]
    private Task OpenUserAsync(UserDto user) => Shell.Current.GoToAsync($"userDetail?userId={user.Id}");

    /// <summary>点击专辑结果：加载该专辑下所有歌曲并入队播放。</summary>
    [RelayCommand]
    private async Task PlayAlbumAsync(AlbumDto album)
    {
        var songs = await api.SearchSongsAsync(albumId: album.Id, pageSize: 50);
        if (songs.Items.Count == 0) return;
        player.PlayQueue(songs.Items, 0, $"专辑：{album.Name}");
        _ = GoNowPlayingAsync();
    }

    [RelayCommand]
    private void SearchFromHistory(string keyword)
    {
        SearchText = keyword;
        _ = SearchCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ClearSearch()
    {
        _searchSeq++;   // 使在途搜索结果作废，避免清空后被旧结果重新填上
        SearchText = string.Empty;
        IsSearching = false;
        SearchResults.Clear();
        AlbumResults.Clear();
        UserResults.Clear();
        OnPropertyChanged(nameof(HasAlbumResults));
        OnPropertyChanged(nameof(HasUserResults));
    }

    [RelayCommand]
    private void ClearHistory()
    {
        SearchHistory.Clear();
        Preferences.Set(HistoryKey, string.Empty);
    }

    [RelayCommand]
    private void RemoveHistoryItem(string keyword)
    {
        SearchHistory.Remove(keyword);
        SaveHistory();
    }

    [RelayCommand]
    private void PlaySearchResult(SongDto song)
    {
        if (player.Current?.Id == song.Id) { _ = GoNowPlayingAsync(); return; }
        var index = SearchResults.IndexOf(song);
        player.PlayQueue(SearchResults, index >= 0 ? index : 0, "搜索");
        _ = GoNowPlayingAsync();
    }

    [RelayCommand]
    private void PlayHot(SongDto song)
    {
        if (player.Current?.Id == song.Id) { _ = GoNowPlayingAsync(); return; }
        var index = HotSongs.IndexOf(song);
        player.PlayQueue(HotSongs, index >= 0 ? index : 0, "搜索");
        _ = GoNowPlayingAsync();
    }

    [RelayCommand]
    private Task GoNowPlayingAsync() => Shell.Current.GoToAsync("nowplaying");

    private void AddToHistory(string keyword)
    {
        SearchHistory.Remove(keyword);
        SearchHistory.Insert(0, keyword);
        while (SearchHistory.Count > MaxHistory)
            SearchHistory.RemoveAt(SearchHistory.Count - 1);
        SaveHistory();
    }

    private void LoadHistory()
    {
        var saved = Preferences.Get(HistoryKey, string.Empty);
        SearchHistory.Clear();
        if (string.IsNullOrWhiteSpace(saved)) return;
        foreach (var item in saved.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            SearchHistory.Add(item);
    }

    private void SaveHistory()
    {
        Preferences.Set(HistoryKey, string.Join('\n', SearchHistory));
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}