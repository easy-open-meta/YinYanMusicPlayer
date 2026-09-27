using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 分区详情页：头部展示分区信息（导航查询串带入，不二次请求），
/// 下方按分区的**内容类型**（<c>CategoryContentModes</c>）分两段展示：
/// <list type="bullet">
/// <item>歌曲 —— <c>api/songs?categoryId=x</c>（服务端按播放量倒序，取最热门一批）；</item>
/// <item>歌单 —— <c>api/playlists?categoryId=x</c>（服务端已排除系统歌单）。</item>
/// </list>
/// 「仅歌曲」的专区不出现歌单段，且**根本不会去请求歌单**。
/// <para>
/// ⚠️ 两段各自加载、各自吞异常：一段失败不能让另一段也变空
/// （同歌曲页的教训：附属数据抛错把主内容一起带走，表现为"整页空白且没有提示"）。
/// </para>
/// </summary>
public partial class ZoneViewModel(IMusicApi api, PlayerService player) : ObservableObject
{
    /// <summary>分区歌曲一次取多少：专区是入口页，不做无限翻页，给最热门的一批即可。</summary>
    private const int SongPageSize = 30;

    private const int PlaylistPageSize = 50;

    /// <summary>本分区的内容类型，取值见 <see cref="CategoryContentModes"/>。</summary>
    private string _contentMode = CategoryContentModes.Both;

    public long CategoryId { get; private set; }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string slogan = string.Empty;

    /// <summary>分区卡片底色（#RRGGBB）。无数据时回退主题紫，保证头部可见。</summary>
    [ObservableProperty]
    private string colorHex = "#512BD4";

    [ObservableProperty]
    private string iconGlyph = string.Empty;

    /// <summary>该分区的歌曲（服务端按播放量倒序）。同时是播放入队用的整张列表。</summary>
    public ObservableCollection<SongDto> Songs { get; } = [];

    /// <summary>该分区下的公开歌单。</summary>
    public ObservableCollection<PlaylistDto> Playlists { get; } = [];

    /// <summary>是否展示歌曲段（内容类型为 both / songs）。</summary>
    [ObservableProperty]
    private bool showSongs = true;

    /// <summary>是否展示歌单段（内容类型为 both / playlists）。</summary>
    [ObservableProperty]
    private bool showPlaylists = true;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private Task OpenPlaylistAsync(PlaylistDto playlist) => Shell.Current.GoToAsync($"playlist?id={playlist.Id}");

    /// <summary>
    /// ⚠️ 播放入口必须带**整张列表**（PlayerService 在队列长度为 1 时"下一首"等于自己），
    /// 所以这里传分区歌曲全集，而不是单首。
    /// </summary>
    [RelayCommand]
    private void PlaySong(SongDto song)
    {
        if (player.Current?.Id == song.Id)
        {
            _ = Shell.Current.GoToAsync("nowplaying");
            return;
        }
        var index = Songs.IndexOf(song);
        player.PlayQueue(Songs, index >= 0 ? index : 0, Name);
        _ = Shell.Current.GoToAsync("nowplaying");
    }

    /// <summary>由 ZonePage 的 QueryProperty 回调逐个喂参数；categoryId 到齐后触发加载。</summary>
    public void Apply(string key, string value)
    {
        AppLog.Info($"[Zone] Apply: key={key}, value={value}");
        switch (key)
        {
            case "categoryId": CategoryId = long.TryParse(value, out var id) ? id : 0; break;
            case "name": Name = value; break;
            case "slogan": Slogan = value; break;
            case "colorHex": if (!string.IsNullOrWhiteSpace(value)) ColorHex = value; break;
            case "icon": IconGlyph = value; break;
            case "mode":
                _contentMode = CategoryContentModes.Normalize(value);
                SyncVisibility();
                break;
        }
        if (key == "categoryId") _ = LoadDeferredAsync();
    }

    /// <summary>按内容类型决定展示哪几段。</summary>
    private void SyncVisibility()
    {
        ShowSongs = _contentMode != CategoryContentModes.Playlists;
        ShowPlaylists = _contentMode != CategoryContentModes.Songs;
    }

    /// <summary>
    /// 等这一轮 QueryProperty 全部喂完再发请求。
    /// Shell 会为每个参数同步调一次 <see cref="Apply"/>，而 <c>mode</c>（导航串的最后一个参数）
    /// 总比 <c>categoryId</c> 晚到 —— 若在 categoryId 那一拍就按默认 both 去拉，
    /// "仅歌曲"的专区会白拉一遍用不上的歌单。让出一次调用栈即可避开。
    /// </summary>
    private async Task LoadDeferredAsync()
    {
        await Task.Yield();
        await LoadAllAsync();
    }

    /// <summary>只拉需要的那几段（各自内部处理失败），这里只负责转菊花的开与关。</summary>
    private async Task LoadAllAsync()
    {
        if (CategoryId <= 0) return;
        SyncVisibility();

        IsBusy = true;
        try
        {
            var tasks = new List<Task>(2);
            if (ShowSongs) tasks.Add(LoadSongsAsync());
            if (ShowPlaylists) tasks.Add(LoadPlaylistsAsync());
            if (tasks.Count > 0) await Task.WhenAll(tasks);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadSongsAsync()
    {
        try
        {
            var result = await api.SearchSongsAsync(categoryId: (int)CategoryId, pageSize: SongPageSize);
            Songs.Clear();
            foreach (var song in result.Items) Songs.Add(song);
            AppLog.Info($"[Zone] 分区歌曲 {Songs.Count} 首（categoryId={CategoryId}）");
        }
        catch (Exception ex)
        {
            AppLog.Error($"[Zone] 分区歌曲加载失败：{ex.Message}", ex);
        }
    }

    private async Task LoadPlaylistsAsync()
    {
        try
        {
            var result = await api.SearchPlaylistsAsync(categoryId: (int)CategoryId, pageSize: PlaylistPageSize);
            Playlists.Clear();
            // 系统歌单（我喜欢的音乐等）不是"公开歌单"，不进专区
            foreach (var p in result.Items.Where(p => !p.IsSystem)) Playlists.Add(p);
            AppLog.Info($"[Zone] 分区歌单 {Playlists.Count} 个（categoryId={CategoryId}）");
        }
        catch (Exception ex)
        {
            AppLog.Error($"[Zone] 分区歌单加载失败：{ex.Message}", ex);
        }
    }
}
