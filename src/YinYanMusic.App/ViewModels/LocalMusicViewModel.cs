using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.App.Services.LocalLibrary;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 本地音乐页（V2.6）：扫描设备音频、浏览/搜索/播放本地曲库。
///
/// 这一页的定位是**纯本地能力**：除了登录态本身，它不依赖任何服务端接口。
/// 所以即使离线（TC-2.6-05）或后端挂了，扫描、列表、播放都照常工作。
/// </summary>
public partial class LocalMusicViewModel(
    LocalLibraryStore store,
    ILocalMediaScanner scanner,
    PlayerService player) : ObservableObject
{
    /// <summary>完整曲库（内存态）。搜索/排序都在这份数据上做，不再回查 SQLite。</summary>
    private readonly List<LocalSong> _all = [];

    public ObservableCollection<LocalSong> Songs { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isScanning;

    /// <summary>扫描进度文案（"正在解析 42/318"）。</summary>
    [ObservableProperty]
    private string scanProgress = string.Empty;

    /// <summary>顶部状态行：曲库大小 / 最近一次扫描结果。</summary>
    [ObservableProperty]
    private string statusText = string.Empty;

    /// <summary>搜索关键词。变化即过滤（本地数据量小，直接内存过滤，不用防抖）。</summary>
    [ObservableProperty]
    private string searchKeyword = string.Empty;

    [ObservableProperty]
    private bool isSearchVisible;

    /// <summary>是否有曲目（控制"空态引导"与"扫描按钮"的措辞）。</summary>
    public bool HasSongs => _all.Count > 0;

    /// <summary>当前过滤结果为空但曲库非空（搜索没命中）。</summary>
    public bool IsSearchEmpty => _all.Count > 0 && Songs.Count == 0;

    /// <summary>曲库本身为空（还没扫过，或设备里确实没有音频）。</summary>
    public bool IsLibraryEmpty => _all.Count == 0;

    public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchKeyword);

    /// <summary>扫描按钮下方的说明文案（两端措辞不同：Android 全盘、Windows 选文件夹）。</summary>
    public string ScanDescription => scanner.ScanActionDescription;

    partial void OnSearchKeywordChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearchActive));
        ApplyFilter();
    }

    partial void OnIsSearchVisibleChanged(bool value)
    {
        // 收起搜索框时清空关键词：否则列表停在过滤态，用户会以为歌丢了
        if (!value && IsSearchActive) SearchKeyword = string.Empty;
    }

    // ── 加载 ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var songs = await store.GetAllAsync();
            _all.Clear();
            _all.AddRange(songs);
            ApplyFilter();
            UpdateStatus();

            // 文件失效事件：播放器发现本地文件没了会抛出来，这里提示用户移除记录。
            // 用 -= 再 += 保证重复 Load 不会重复订阅（单例 PlayerService + 本 VM 是 Transient）。
            player.LocalFileMissing -= OnLocalFileMissing;
            player.LocalFileMissing += OnLocalFileMissing;

            // 曲库变更事件：SongMenuHelper 的「从本地曲库移除」直接操作 Store、拿不到本 VM，
            // 靠这个事件把列表刷新回来（否则移除后界面不变，用户以为没删掉）。
            store.LibraryChanged -= OnLibraryChanged;
            store.LibraryChanged += OnLibraryChanged;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 曲库在别处被改动（菜单里的「移除」）→ 重载列表。
    /// 必须切回主线程：事件可能从后台线程（扫描/DB 回调）抛出，直接改 ObservableCollection 会崩。
    /// </summary>
    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await ReloadFromStoreAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[本地曲库] 刷新失败: {ex.Message}"); }
        });
    }

    /// <summary>离开页面时退订，避免单例播放器/Store 一直持有已销毁页面的 VM。</summary>
    public void Detach()
    {
        player.LocalFileMissing -= OnLocalFileMissing;
        store.LibraryChanged -= OnLibraryChanged;
    }

    private void OnLocalFileMissing(object? sender, SongDto song)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            var confirm = await SongMenuHelper.ShowRoundedConfirmAsync(
                "文件不存在",
                $"「{song.Title}」对应的文件已被移动或删除，是否从本地曲库中移除这条记录？",
                "移除", "保留");
            if (!confirm) return;

            await store.RemoveAsync(LocalLibraryStore.ToLocalId(song.Id));
            await LoadAsync();
        });
    }

    private void UpdateStatus()
    {
        if (_all.Count == 0)
        {
            StatusText = "本地曲库还是空的，点下方按钮扫描设备里的音乐";
            return;
        }

        var totalSeconds = _all.Sum(s => (long)s.DurationSeconds);
        var totalPlays = _all.Sum(s => (long)s.LocalPlayCount);
        var span = TimeSpan.FromSeconds(totalSeconds);
        var durationText = span.TotalHours >= 1
            ? $"{(int)span.TotalHours} 小时 {span.Minutes} 分"
            : $"{span.Minutes} 分 {span.Seconds} 秒";

        StatusText = $"{_all.Count} 首 · 共 {durationText} · 本地播放 {totalPlays} 次";
    }

    // ── 搜索 ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleSearch() => IsSearchVisible = !IsSearchVisible;

    [RelayCommand]
    private void ClearSearch() => SearchKeyword = string.Empty;

    /// <summary>
    /// 内存过滤：命中标题 / 歌手 / 专辑，与在线曲库的搜索口径保持一致（V2.4 定下来的三字段）。
    /// 本地曲库通常只有几百首，直接 LINQ 过滤比回查 SQLite 更快，也不用防抖。
    /// </summary>
    private void ApplyFilter()
    {
        var keyword = SearchKeyword?.Trim();

        IEnumerable<LocalSong> filtered = _all;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = _all.Where(s =>
                Contains(s.Title, keyword) ||
                Contains(s.ArtistName, keyword) ||
                Contains(s.AlbumName, keyword));
        }

        Songs.Clear();
        foreach (var song in filtered) Songs.Add(song);

        OnPropertyChanged(nameof(HasSongs));
        OnPropertyChanged(nameof(IsSearchEmpty));
        OnPropertyChanged(nameof(IsLibraryEmpty));
    }

    private static bool Contains(string? source, string keyword) =>
        !string.IsNullOrEmpty(source) &&
        source.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    // ── 扫描 ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning) return;

        // Android 支持两种扫描方式，让用户选；Windows 只有"选文件夹"一种，直接进。
        var folderOnly = false;
        if (scanner.SupportsScanModeChoice)
        {
            var choice = await SongMenuHelper.ShowBottomSheetAsync("扫描本地音乐",
                ["扫描设备全部音乐", "选择文件夹"]);
            if (choice is null) return;                       // 用户取消
            folderOnly = choice == "选择文件夹";
        }

        IsScanning = true;
        ScanProgress = "正在准备…";
        try
        {
            var progress = new Progress<int>(done =>
                ScanProgress = $"正在解析 {done} 个文件…");

            var result = await scanner.ScanAsync(progress, default, folderOnly);

            // 权限被拒/取消/失败：把原因原样透出（TC-2.6-03 要求"明确引导，不崩溃"）
            if (result.Status is not LocalScanStatus.Success)
            {
                ScanProgress = string.Empty;
                StatusText = result.Message ?? "扫描未完成";
                if (result.Status is LocalScanStatus.PermissionDenied or LocalScanStatus.Failed)
                    await SongMenuHelper.ShowMessageDialogAsync("扫描未完成", result.Message ?? "请稍后重试。");
                return;
            }

            await ReloadFromStoreAsync();
            ScanProgress = string.Empty;
            StatusText = result.Message ?? $"扫描完成，共 {_all.Count} 首";
        }
        catch (Exception ex)
        {
            ScanProgress = string.Empty;
            StatusText = $"扫描失败：{ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private async Task ReloadFromStoreAsync()
    {
        var songs = await store.GetAllAsync();
        _all.Clear();
        _all.AddRange(songs);
        ApplyFilter();
        UpdateStatus();
    }

    // ── 播放 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 点列表项播放：整张列表入队（而不是只塞这一首），
    /// 否则列表循环模式下播完一遍又会重播同一首。
    /// </summary>
    [RelayCommand]
    private async Task PlaySongAsync(LocalSong song)
    {
        var index = Songs.IndexOf(song);
        if (index < 0) index = 0;

        var queue = Songs.Select(LocalLibraryStore.ToDto).ToList();
        player.PlayQueue(queue, index, "本地音乐");
        await Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private async Task PlayAllAsync()
    {
        if (Songs.Count == 0) return;
        player.PlayQueue(Songs.Select(LocalLibraryStore.ToDto).ToList(), 0, "本地音乐");
        await Shell.Current.GoToAsync("nowplaying");
    }

    /// <summary>长按/更多菜单。本地歌只有本地语义，菜单刻意精简（见 SongMenuHelper 本地分支）。</summary>
    [RelayCommand]
    private async Task ShowMenuAsync(LocalSong song)
    {
        var dto = LocalLibraryStore.ToDto(song);
        await SongMenuHelper.ShowLocalMenuAsync(dto, player, Songs.Select(LocalLibraryStore.ToDto).ToList());
    }

    /// <summary>随机播放：打乱后入队（不动列表顺序，只影响队列）。</summary>
    [RelayCommand]
    private async Task ShuffleAsync()
    {
        if (Songs.Count == 0) return;
        var queue = Songs.Select(LocalLibraryStore.ToDto).ToList();
        for (var i = queue.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (queue[i], queue[j]) = (queue[j], queue[i]);
        }
        player.PlayQueue(queue, 0, "本地音乐 · 随机");
        await Shell.Current.GoToAsync("nowplaying");
    }

    [RelayCommand]
    private static Task BackAsync() => Shell.Current.GoToAsync("..");
}
