using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.App.Services.LocalLibrary;

namespace YinYanMusic.App.ViewModels;

/// <summary>缓存管理页的三个标签页。</summary>
public enum CacheTab
{
    /// <summary>正在下载（含排队中）。</summary>
    Active,
    /// <summary>已缓存到本机、可直接离线播放。</summary>
    Completed,
    /// <summary>发生过的缓存事件：完成 / 移除 / 清空 / 失败 / 超上限跳过。</summary>
    History,
}

/// <summary>
/// 缓存管理页（V2.7）：三个标签页回答三个不同的问题——
/// 「缓存中」正在下什么、「缓存完成」本机有哪些、「缓存历史」以前发生过什么
/// （缓存过又没了、一直下不下来、因为超上限被跳过）。
///
/// <para>达到上限后**不会自动删旧缓存**（已定案），那"该删哪首"必须由用户决定，
/// 所以「缓存完成」要有逐条列表与可见占用；「缓存中」给实时进度；
/// 「缓存历史」给解释性（"我明明缓存过""为什么这首一直没缓存上"）。</para>
/// </summary>
public partial class CacheManageViewModel : ObservableObject
{
    private readonly CacheStore _store;
    private readonly CacheDownloadService _downloader;
    private readonly PendingPlayReportStore _pendingReports;
    private readonly CacheHistoryStore _historyStore;
    private readonly PlayerService _player;

    /// <summary>「缓存中」：直接绑 <see cref="CacheDownloadService.ActiveDownloads"/> 的快照。</summary>
    public ObservableCollection<ActiveCacheDownload> ActiveItems { get; } = [];

    /// <summary>「缓存完成」。</summary>
    public ObservableCollection<CachedSong> Items { get; } = [];

    /// <summary>「缓存历史」。</summary>
    public ObservableCollection<CacheHistoryEntry> HistoryItems { get; } = [];

    [ObservableProperty]
    private CacheTab _selectedTab = CacheTab.Active;

    [ObservableProperty]
    private bool isBusy;

    /// <summary>概览行：已缓存几首、占用多少、上限多少。</summary>
    [ObservableProperty]
    private string statusText = string.Empty;

    /// <summary>下载动态（"正在缓存 2 首…"）。没有下载时隐藏。</summary>
    [ObservableProperty]
    private string downloadStatus = string.Empty;

    /// <summary>离线补报队列条数提示（V2.11 消费，本版只是让"留痕"看得见，TC-2.7-12）。</summary>
    [ObservableProperty]
    private string pendingReportText = string.Empty;

    /// <summary>
    /// 「播完自动缓存」的状态提示：多少首因为超过上限没自动缓存。
    /// <para>存在的意义：自动缓存撞上限时是**静默跳过**的（不弹窗打断听歌），
    /// 但"静默"不等于"用户不该知道"——这里给他一个能看到的地方，并顺手指向解决动作。</para>
    /// </summary>
    [ObservableProperty]
    private string autoCacheText = string.Empty;

    public CacheManageViewModel(
        CacheStore store,
        CacheDownloadService downloader,
        PendingPlayReportStore pendingReports,
        CacheHistoryStore historyStore,
        PlayerService player)
    {
        _store = store;
        _downloader = downloader;
        _pendingReports = pendingReports;
        _historyStore = historyStore;
        _player = player;
    }

    // ── 标签页 ──────────────────────────────────────────────────────────────

    public bool IsActiveTab => SelectedTab == CacheTab.Active;
    public bool IsCompletedTab => SelectedTab == CacheTab.Completed;
    public bool IsHistoryTab => SelectedTab == CacheTab.History;

    /// <summary>标签上的计数（一眼知道哪个页签有内容）。</summary>
    public string ActiveTabText => ActiveItems.Count > 0 ? $"缓存中 {ActiveItems.Count}" : "缓存中";
    public string CompletedTabText => Items.Count > 0 ? $"缓存完成 {Items.Count}" : "缓存完成";
    public string HistoryTabText => HistoryItems.Count > 0 ? $"缓存历史 {HistoryItems.Count}" : "缓存历史";

    public bool HasActiveItems => ActiveItems.Count > 0;
    public bool HasCompletedItems => Items.Count > 0;
    public bool HasHistoryItems => HistoryItems.Count > 0;

    partial void OnSelectedTabChanged(CacheTab value)
    {
        OnPropertyChanged(nameof(IsActiveTab));
        OnPropertyChanged(nameof(IsCompletedTab));
        OnPropertyChanged(nameof(IsHistoryTab));
    }

    [RelayCommand]
    private void SelectTab(string tab) =>
        SelectedTab = tab switch
        {
            "completed" => CacheTab.Completed,
            "history" => CacheTab.History,
            _ => CacheTab.Active,
        };

    // ── 加载 ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            // 进页先对账：用户在文件管理器里删过缓存文件的话，这里把索引纠正回来（TC-2.7-10）
            await _store.ReconcileAsync();
            await ReloadAsync();

            _store.CacheChanged -= OnCacheChanged;
            _store.CacheChanged += OnCacheChanged;
            _downloader.ProgressChanged -= OnDownloadProgress;
            _downloader.ProgressChanged += OnDownloadProgress;
            _downloader.ActiveDownloadsChanged -= OnActiveDownloadsChanged;
            _downloader.ActiveDownloadsChanged += OnActiveDownloadsChanged;
            _downloader.Completed -= OnDownloadCompleted;
            _downloader.Completed += OnDownloadCompleted;
            _historyStore.HistoryChanged -= OnHistoryChanged;
            _historyStore.HistoryChanged += OnHistoryChanged;

            RefreshActiveItems();
            UpdateDownloadStatus();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>离开页面退订：单例服务不该长期持有已销毁页面的 VM。</summary>
    public void Detach()
    {
        _store.CacheChanged -= OnCacheChanged;
        _downloader.ProgressChanged -= OnDownloadProgress;
        _downloader.ActiveDownloadsChanged -= OnActiveDownloadsChanged;
        _downloader.Completed -= OnDownloadCompleted;
        _historyStore.HistoryChanged -= OnHistoryChanged;
    }

    private void OnCacheChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await ReloadAsync(); }
            catch (Exception ex) { AppLog.Warn($"[CachePage] 刷新失败: {ex.Message}"); }
        });

    private void OnHistoryChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try { await ReloadHistoryAsync(); }
            catch (Exception ex) { AppLog.Warn($"[CachePage] 历史刷新失败: {ex.Message}"); }
        });

    private void OnDownloadProgress(object? sender, CacheDownloadProgress e) =>
        MainThread.BeginInvokeOnMainThread(UpdateDownloadStatus);

    private void OnActiveDownloadsChanged(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RefreshActiveItems();
            UpdateDownloadStatus();
        });

    private void OnDownloadCompleted(object? sender, CacheDownloadCompleted e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            UpdateDownloadStatus();
            await SongMenuHelper.ShowToastAsync(e.Message);
        });
    }

    /// <summary>把「缓存中」快照同步进集合（顺序即入队顺序）。</summary>
    private void RefreshActiveItems()
    {
        ActiveItems.Clear();
        foreach (var item in _downloader.ActiveDownloads) ActiveItems.Add(item);

        OnPropertyChanged(nameof(HasActiveItems));
        OnPropertyChanged(nameof(ActiveTabText));
    }

    private async Task ReloadAsync()
    {
        var all = await _store.GetAllAsync();
        Items.Clear();
        foreach (var item in all) Items.Add(item);

        OnPropertyChanged(nameof(HasCompletedItems));
        OnPropertyChanged(nameof(CompletedTabText));

        var used = await _store.TotalBytesAsync();
        var missing = all.Count(c => c.IsMissing);
        StatusText = all.Count == 0
            ? $"还没有缓存任何歌曲 · 上限 {_store.LimitGb:0.#} GB"
            : $"已缓存 {all.Count} 首 · 占用 {CachedSong.FormatSize(used)} / 上限 {_store.LimitGb:0.#} GB"
              + (missing > 0 ? $" · {missing} 首文件已丢失" : string.Empty);

        var pending = await _pendingReports.CountAsync();
        PendingReportText = pending > 0
            ? $"离线播放待补报 {pending} 条（联网后自动上报，V2.11 生效）"
            : string.Empty;

        // 播完自动缓存被上限拦下的次数（静默跳过，但要让用户在这儿看得见）
        var skipped = _store.AutoCacheSkippedCount;
        AutoCacheText = !_store.AutoCacheEnabled
            ? "已关闭「播完自动缓存」（可在缓存设置里重新开启）"
            : skipped > 0
                ? $"有 {skipped} 首因超过上限没有自动缓存 · 清理缓存或调高上限后继续"
                : string.Empty;

        await ReloadHistoryAsync();
    }

    private async Task ReloadHistoryAsync()
    {
        var history = await _historyStore.GetRecentAsync();
        HistoryItems.Clear();
        foreach (var entry in history) HistoryItems.Add(entry);

        OnPropertyChanged(nameof(HasHistoryItems));
        OnPropertyChanged(nameof(HistoryTabText));
    }

    private void UpdateDownloadStatus()
    {
        var active = _downloader.ActiveCount;
        DownloadStatus = active > 0 ? $"正在缓存 {active} 首…" : string.Empty;
    }

    // ── 「缓存完成」页的操作 ────────────────────────────────────────────────

    /// <summary>
    /// 播放某一首：整张缓存列表入队（与本地音乐页同一套规则，避免队列长度为 1 时退化成单曲循环）。
    /// 有网时 PlayerService 会自动优先走远程源（TC-2.7-03），无网才用缓存文件。
    /// </summary>
    [RelayCommand]
    private async Task PlayAsync(CachedSong item)
    {
        if (item is null) return;
        var index = Items.IndexOf(item);
        if (index < 0) index = 0;

        _player.PlayQueue([.. Items.Select(c => c.ToDto())], index, "缓存音乐");
        await Shell.Current.GoToAsync("nowplaying");
    }

    /// <summary>播放全部缓存（整张列表入队）。</summary>
    [RelayCommand]
    private async Task PlayAllAsync()
    {
        if (Items.Count == 0) return;
        _player.PlayQueue([.. Items.Select(c => c.ToDto())], 0, "缓存音乐");
        await Shell.Current.GoToAsync("nowplaying");
    }

    /// <summary>逐条菜单：播放 / 查看文件位置 / 移除缓存。</summary>
    [RelayCommand]
    private async Task ShowMenuAsync(CachedSong item)
    {
        if (item is null) return;

        var options = new List<string> { "播放", "查看文件位置", "移除缓存" };
        var choice = await SongMenuHelper.ShowBottomSheetAsync(item.Title, options);

        if (choice == "播放")
        {
            await PlayAsync(item);
        }
        else if (choice == "查看文件位置")
        {
            await SongMenuHelper.ShowMessageDialogAsync("缓存文件位置", item.FilePath, "确定");
        }
        else if (choice == "移除缓存")
        {
            var confirm = await SongMenuHelper.ShowRoundedConfirmAsync(
                "移除缓存",
                $"确定删除「{item.Title}」的本地缓存吗？\n\n删除后离线将无法播放这首歌，联网时可重新缓存。",
                "移除", "取消", destructive: true);
            if (!confirm) return;

            await _store.RemoveAsync(item.SongId);
            await SongMenuHelper.ShowToastAsync("已移除缓存");
        }
    }

    /// <summary>清空全部缓存（文件与索引一起删，TC-2.7-09）。</summary>
    [RelayCommand]
    private async Task ClearAllAsync()
    {
        if (Items.Count == 0)
        {
            await SongMenuHelper.ShowToastAsync("当前没有缓存");
            return;
        }

        var used = await _store.TotalBytesAsync();
        var confirm = await SongMenuHelper.ShowRoundedConfirmAsync(
            "清空缓存",
            $"将删除全部 {Items.Count} 首缓存，释放约 {CachedSong.FormatSize(used)} 空间。\n\n" +
            "已经缓存到本机的音频文件会被删除，联网时可重新缓存。",
            "清空", "取消", destructive: true);
        if (!confirm) return;

        await _store.ClearAsync();
        await SongMenuHelper.ShowToastAsync("已清空缓存");
    }

    // ── 「缓存历史」页的操作 ────────────────────────────────────────────────

    /// <summary>清空历史（只删日志，不动已缓存的歌）。</summary>
    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        if (HistoryItems.Count == 0)
        {
            await SongMenuHelper.ShowToastAsync("没有缓存历史");
            return;
        }

        var confirm = await SongMenuHelper.ShowRoundedConfirmAsync(
            "清空缓存历史",
            $"将删除 {HistoryItems.Count} 条历史记录。\n\n已缓存的歌曲不会被删除，只是清掉这份日志。",
            "清空", "取消", destructive: true);
        if (!confirm) return;

        await _historyStore.ClearAsync();
        await SongMenuHelper.ShowToastAsync("已清空缓存历史");
    }

    // ── 缓存设置（V2.9 从「服务器设置」页迁来） ─────────────────────────────

    /// <summary>打开缓存设置弹窗：容量上限 + 「播完自动缓存」。</summary>
    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var result = await SongMenuHelper.ShowCacheSettingsAsync(
            FormatLimit(_store.LimitGb), _store.AutoCacheEnabled);
        if (result is not { Save: true }) return;

        var text = result.LimitText;
        if (!double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var gb)
            && !double.TryParse(text, out gb))
        {
            // 弹窗里已就地拦过非法文本，这里只是双保险（TC-2.7-06）
            await SongMenuHelper.ShowToastAsync($"上限无效，范围 {CacheStore.MinLimitGb:0.#} ~ {CacheStore.MaxLimitGb:0.#} GB");
            return;
        }

        gb = Math.Round(gb, 1);
        if (!_store.TrySetLimitGb(gb))
        {
            await SongMenuHelper.ShowToastAsync($"上限必须在 {CacheStore.MinLimitGb:0.#} ~ {CacheStore.MaxLimitGb:0.#} GB 之间");
            return;
        }

        if (_store.AutoCacheEnabled != result.AutoCacheEnabled)
            _store.AutoCacheEnabled = result.AutoCacheEnabled;

        await SongMenuHelper.ShowToastAsync($"已保存：上限 {FormatLimit(gb)} GB");
        await ReloadAsync();
    }

    /// <summary>上限展示统一到「0.#」一位小数（5 → "5"，5.5 → "5.5"）。</summary>
    private static string FormatLimit(double gb) => gb.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    [RelayCommand]
    private static Task BackAsync() => Shell.Current.GoToAsync("..");
}
