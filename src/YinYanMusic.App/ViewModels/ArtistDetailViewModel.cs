using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>歌手详情页的两个标签页。</summary>
public enum ArtistTab
{
    Songs,
    Albums,
}

/// <summary>专辑列表的排序方式。</summary>
public enum AlbumSortMode
{
    /// <summary>发行时间倒序（最新在前，无发行日期的排最后）—— 服务端默认就是这个顺序。</summary>
    ReleaseDate,

    /// <summary>按名称排（忽略大小写）。</summary>
    Name,
}

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

    /// <summary>该歌手的专辑（专辑标签页）。</summary>
    public ObservableCollection<AlbumDto> Albums { get; } = [];

    /// <summary>
    /// 歌曲一页取几首。首屏只取这一页 —— 歌手页的瓶颈是"页面什么时候有内容"，
    /// 不是"能不能一次全拿到"：一次 36 首（服务端还要逐首查/探元数据）会让首屏白等，
    /// 剩下的滚到列表底部时再取（见 <see cref="LoadMoreSongsAsync"/>）。
    /// </summary>
    private const int SongPageSize = 20;

    private int _songPage;              // 已经取到第几页
    private bool _allSongsLoaded;       // 歌曲是否已全部取回（避免没完没了地翻页）

    /// <summary>正在取下一页（列表底部据此显示转圈）。</summary>
    [ObservableProperty]
    private bool isLoadingMore;

    /// <summary>还有没取回来的歌。</summary>
    public bool HasMoreSongs => Songs.Count < SongCount;

    // ── 标签页：歌曲 / 专辑 ───────────────────────────────────────────────────
    // 与「缓存管理」页同一套写法：私有枚举字段 + IsXxxTab 计算属性 + SelectTab 命令，
    // 切换时手动通知两个 IsXxxTab（XAML 用 DataTrigger 高亮当前页）。
    private ArtistTab _tab = ArtistTab.Songs;

    public bool IsSongsTab => _tab == ArtistTab.Songs;
    public bool IsAlbumsTab => _tab == ArtistTab.Albums;

    [RelayCommand]
    private void SelectTab(string tab)
    {
        _tab = tab == "albums" ? ArtistTab.Albums : ArtistTab.Songs;
        OnPropertyChanged(nameof(IsSongsTab));
        OnPropertyChanged(nameof(IsAlbumsTab));
    }

    // ── 专辑排序：发行时间（默认）/ 名称 ──────────────────────────────────────
    // 专辑一次全取回来（pageSize 100），排序直接在内存里做，不再回查服务端。
    [ObservableProperty]
    private AlbumSortMode albumSort = AlbumSortMode.ReleaseDate;

    /// <summary>排序按钮上的文字：显示的是**当前**排序方式。</summary>
    public string AlbumSortText => AlbumSort == AlbumSortMode.ReleaseDate ? "发行时间" : "名称";

    [RelayCommand]
    private void ToggleAlbumSort()
    {
        AlbumSort = AlbumSort == AlbumSortMode.ReleaseDate ? AlbumSortMode.Name : AlbumSortMode.ReleaseDate;
        OnPropertyChanged(nameof(AlbumSortText));
        ApplyAlbumSort();
    }

    private void ApplyAlbumSort()
    {
        var sorted = AlbumSort == AlbumSortMode.ReleaseDate
            // 先按"有没有发行日期"排，把没日期的压到最后（PostgreSQL 的 DESC 默认把 NULL 排最前，
            // 服务端也是这么处理的，两边口径保持一致）
            ? Albums.OrderBy(a => a.ReleaseDate == null)
                    .ThenByDescending(a => a.ReleaseDate)
                    .ThenByDescending(a => a.Id)
            : Albums.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(a => a.Id);

        var list = sorted.ToList();
        Albums.Clear();
        foreach (var album in list) Albums.Add(album);
    }

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private async Task LoadAsync(string? idParam)
    {
        if (!long.TryParse(idParam, out var id) || id == 0) return;
        _artistId = id;
        _songPage = 0;
        _allSongsLoaded = false;
        Songs.Clear();
        OnPropertyChanged(nameof(HasMoreSongs));

        // 详情 / 歌曲 / 专辑**并行**发出：原来是顺序 await（详情 → 歌曲 → 专辑），
        // 三次往返 + 各自的服务端耗时串成一条线，页面要等最后一件事回来才有内容。
        // 现在谁先回来谁先上屏 —— 列表（用户真正要看的）几乎总是第一个。
        // 三个请求各自吞异常并记日志：一个失败不该让整页空掉。
        var detailTask = SafeAsync(() => api.GetArtistAsync(id), "歌手资料");
        var songsTask = SafeAsync(() => api.SearchSongsAsync(artistId: id, page: 1, pageSize: SongPageSize), "歌曲列表");
        var albumsTask = SafeAsync(() => api.GetAlbumsAsync(artistId: id, pageSize: 100), "专辑列表");

        var first = await songsTask;
        if (first is not null)
        {
            _songPage = 1;
            foreach (var song in first.Items) Songs.Add(song);
            SongCount = (int)first.Total;
            OnPropertyChanged(nameof(HasMoreSongs));
        }

        // 头像兜底：先取任意一首歌的封面，没有歌就取第一张有封面的专辑 ——
        // 与「关注列表」的歌手画像同一口径（那边是服务端兜底），别让这类页面的头像空着。
        AvatarUrl = Songs.Count > 0
            ? Songs[0].CoverUrl ?? string.Empty
            : string.Empty;

        var detail = await detailTask;
        if (detail is not null)
        {
            ArtistName = detail.Name;
            FollowerCount = (int)detail.FollowerCount;
            AlbumCount = (int)detail.AlbumCount;
        }
        if (string.IsNullOrEmpty(ArtistName) && Songs.Count > 0)
            ArtistName = Songs[0].ArtistName;

        var albums = await albumsTask;
        if (albums is not null)
        {
            Albums.Clear();
            foreach (var album in albums.Items) Albums.Add(album);
        }

        if (string.IsNullOrEmpty(AvatarUrl) && Albums.Count > 0)
            AvatarUrl = Albums.FirstOrDefault(a => !string.IsNullOrEmpty(a.CoverUrl))?.CoverUrl ?? string.Empty;
    }

    /// <summary>列表滚到接近底部时自动取下一页（`RemainingItemsThreshold` 触发，可能连续触发多次）。</summary>
    [RelayCommand]
    private async Task LoadMoreSongsAsync()
    {
        // ⚠️ 空列表直接返回：CollectionView 的阈值事件在空列表/满屏不满一屏时也会触发，
        // 不挡住就会对着服务端空转（而且没有任何可滚动的迹象让用户知道发生了什么）。
        if (IsLoadingMore || _allSongsLoaded || Songs.Count == 0) return;
        if (!HasMoreSongs) { _allSongsLoaded = true; return; }

        IsLoadingMore = true;
        try
        {
            var next = await api.SearchSongsAsync(artistId: _artistId, page: _songPage + 1, pageSize: SongPageSize);
            _songPage++;
            // 服务端按播放量倒序分页，理论上不会重复；但翻页期间有人播放/后台改数据都可能让顺序变，
            // 这里按 Id 去重，宁可少一首也不要列表里冒出两条一样的。
            var known = Songs.Select(s => s.Id).ToHashSet();
            foreach (var song in next.Items)
                if (known.Add(song.Id)) Songs.Add(song);

            SongCount = (int)next.Total;
            if (Songs.Count >= SongCount || next.Items.Count == 0) _allSongsLoaded = true;
            OnPropertyChanged(nameof(HasMoreSongs));
        }
        catch (Exception ex)
        {
            // 失败不置 _allSongsLoaded：用户再滚一下就会自然重试
            AppLog.Error($"[歌手页] 加载更多失败：{ex.Message}", ex);
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    /// <summary>并行发出的请求各自兜异常 + 记日志，返回 null 表示这一路失败。</summary>
    private static async Task<T?> SafeAsync<T>(Func<Task<T?>> call, string what) where T : class
    {
        try
        {
            return await call();
        }
        catch (Exception ex)
        {
            AppLog.Error($"[歌手页] {what}加载失败：{ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// 后台把剩余页取回来补进队列。
    /// <para>
    /// 点歌路径**绝不能**等这个请求 —— 用户的感受是"点了另一首，怎么还在放上一首"：
    /// 之前这里是在点击路径上 await 补齐列表，网络那几百毫秒里旧歌一直在响（2026-09-23 真机反馈）。
    /// 现在先出声，剩下的后台补；队列若已被换成别的来源，<see cref="PlayerService.AppendToQueue"/> 会丢弃这次结果。
    /// </para>
    /// </summary>
    private async Task CompleteQueueAsync()
    {
        if (_allSongsLoaded || Songs.Count == 0 || !HasMoreSongs) return;

        try
        {
            var sourceName = ArtistName;
            var all = await api.SearchSongsAsync(artistId: _artistId, page: 1, pageSize: Math.Max(SongCount, SongPageSize));
            // await 后回到 UI 线程（命令在 UI 线程上起）：AppendToQueue 会直接改队列浮窗读的那个 List
            player.AppendToQueue(all.Items, sourceName);

            // 顺手把列表也补全：用户看到的是"歌都在"，队列与界面口径一致
            var known = Songs.Select(s => s.Id).ToHashSet();
            foreach (var song in all.Items)
                if (known.Add(song.Id)) Songs.Add(song);
            _songPage = 1;
            _allSongsLoaded = true;
            SongCount = (int)all.Total;
            OnPropertyChanged(nameof(HasMoreSongs));
        }
        catch (Exception ex)
        {
            // 补不齐就用已加载的那些：能播一部分总比点了没反应强
            AppLog.Error($"[歌手页] 后台补齐队列失败：{ex.Message}", ex);
        }
    }

    [RelayCommand]
    private void PlayAll()
    {
        if (Songs.Count == 0) return;
        // ⚠️ 先出声再补队列：不在这条路径上等任何网络请求
        int startIndex = player.PlayMode == PlayMode.Random
            ? Random.Shared.Next(Songs.Count)
            : 0;
        player.PlayQueue(Songs, startIndex, ArtistName);
        _ = Shell.Current.GoToAsync("nowplaying");
        // 补齐**不能 await 在命令里**：AsyncRelayCommand 默认不允许并发执行，
        // await 就会让这个命令在这几百毫秒里处于"执行中"，用户连点第二首会被直接忽略。
        _ = CompleteQueueAsync();
    }

    [RelayCommand]
    private void PlaySong(SongDto song)
    {
        if (player.Current?.Id == song.Id) { _ = Shell.Current.GoToAsync("nowplaying"); return; }
        var index = Songs.IndexOf(song);
        // 同上：先切音源（同步、瞬时），跳转与补齐都不挡在它们前面
        player.PlayQueue(Songs, index >= 0 ? index : 0, ArtistName);
        _ = Shell.Current.GoToAsync("nowplaying");
        _ = CompleteQueueAsync();
    }

    /// <summary>
    /// 播放整张专辑：先把该专辑的歌取回来再整张入队。
    /// ⚠️ 必须带整张列表 —— 队列长度为 1 时"下一首"等于自己（本项目的老坑）。
    /// </summary>
    [RelayCommand]
    private async Task PlayAlbumAsync(AlbumDto album)
    {
        try
        {
            var songs = await api.SearchSongsAsync(albumId: album.Id, pageSize: 100);
            if (songs.Items.Count == 0) return;
            player.PlayQueue(songs.Items, 0, $"专辑：{album.Name}");
            _ = Shell.Current.GoToAsync("nowplaying");
        }
        catch (Exception ex)
        {
            AppLog.Error($"[歌手页] 播放专辑失败：{ex.Message}", ex);
        }
    }
}
