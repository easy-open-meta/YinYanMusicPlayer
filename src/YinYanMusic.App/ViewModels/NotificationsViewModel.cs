using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 「消息通知」列表页（V2.15）：拉取自己的通知、单条/全部已读、关联对象跳转。
/// 角标状态由 <see cref="NotificationRealtimeService"/> 推到 <see cref="UnreadCount"/>。
/// </summary>
public partial class NotificationsViewModel(
    IMusicApi api,
    PlayerService player,
    NotificationRealtimeService realtime) : ObservableObject
{
    public ObservableCollection<NotificationDto> Items { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private int unreadCount;

    [ObservableProperty]
    private bool hasMore;

    private int _page = 1;
    private const int PageSize = 20;

    public bool HasUnread => UnreadCount > 0;

    /// <summary>
    /// 页面标题：有未读时在「消息通知」后带上数量（如「消息通知 (3)」）。
    /// 与「我的」页的「本地音乐 (3)」同一套写法（半角括号 + 一个空格）。
    /// 未读归零后括号整块消失，不留「(0)」。
    /// </summary>
    public string TitleText => UnreadCount > 0 ? $"消息通知 ({UnreadCount})" : "消息通知";

    /// <summary>
    /// 列表为空（且本轮加载已结束）—— 控制「暂无通知」空态的显隐。
    /// 列表用 BindableLayout 而不是 CollectionView（原因见页面 XAML 注释：
    /// CollectionView 不算条目根元素的 Margin，卡片会贴在一起），
    /// 代价就是没有 EmptyView 可用，空态得自己判。
    /// </summary>
    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    public NotificationsViewModel()
        : this(
            ServiceHelper.GetRequiredService<IMusicApi>(),
            ServiceHelper.GetRequiredService<PlayerService>(),
            ServiceHelper.GetRequiredService<NotificationRealtimeService>())
    {
    }

    partial void OnUnreadCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasUnread));
        OnPropertyChanged(nameof(TitleText));
    }

    /// <summary>加载结束（IsBusy 落回 false）时空态才该出现，所以每次都得重算。</summary>
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    private bool _realtimeHooked;

    /// <summary>
    /// 订阅实时推送，把新通知直接插到列表顶部。没有这一步的话，页面开着时只有角标会变、
    /// 列表要等下次进页面才刷新（列表本来就是"进页面拉一次"的模型）。
    /// 事件从 SignalR 后台线程抛出，改集合必须在主线程。
    /// </summary>
    private void HookRealtimeOnce()
    {
        if (_realtimeHooked) return;
        _realtimeHooked = true;

        realtime.NotificationReceived += (notification, unread) =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UnreadCount = unread;
                // 去重：重连后服务端可能补推同一条
                if (Items.All(x => x.Id != notification.Id)) Items.Insert(0, notification);
                OnPropertyChanged(nameof(IsEmpty));
            });
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            HookRealtimeOnce();
            _page = 1;
            var page = await api.GetNotificationsAsync(_page, PageSize);
            Items.Clear();
            foreach (var n in page.Items) Items.Add(n);
            HasMore = Items.Count < page.Total;

            var unread = await api.GetNotificationUnreadCountAsync();
            if (unread >= 0) UnreadCount = unread;

            await realtime.EnsureConnectedAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || !HasMore) return;
        IsBusy = true;
        try
        {
            _page++;
            var page = await api.GetNotificationsAsync(_page, PageSize);
            foreach (var n in page.Items)
            {
                // 防重（实时推送可能已插入同一条）
                if (Items.All(x => x.Id != n.Id)) Items.Add(n);
            }
            HasMore = Items.Count < page.Total;
        }
        catch
        {
            _page = Math.Max(1, _page - 1);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task MarkReadAsync(NotificationDto? notification)
    {
        if (notification is null || notification.IsRead) return;
        var (ok, _) = await api.MarkNotificationReadAsync(notification.Id);
        if (!ok) return;

        var idx = Items.IndexOf(notification);
        if (idx >= 0)
        {
            Items[idx] = notification with { IsRead = true, ReadAtUtc = DateTime.UtcNow };
        }
        var unread = await api.GetNotificationUnreadCountAsync();
        if (unread >= 0) UnreadCount = unread;
    }

    [RelayCommand]
    private async Task MarkAllReadAsync()
    {
        var (ok, _) = await api.MarkAllNotificationsReadAsync();
        if (!ok) return;

        for (var i = 0; i < Items.Count; i++)
        {
            if (!Items[i].IsRead)
                Items[i] = Items[i] with { IsRead = true, ReadAtUtc = DateTime.UtcNow };
        }
        UnreadCount = 0;
    }

    /// <summary>
    /// 点击通知：先标已读，再按关联对象跳转。
    /// 判定顺序 <b>歌单 → 专辑 → 歌曲</b>：后台弹窗里三项是三选一，正常只会有一个有值；
    /// 这里定序只是兜底（历史数据 / 直接调接口写入都可能有多个），
    /// 后台列表的「关联」列用同一个顺序，保证列表显示的就是实际会跳到的目标。
    /// </summary>
    [RelayCommand]
    private async Task OpenAsync(NotificationDto? notification)
    {
        if (notification is null) return;

        await MarkReadAsync(notification);

        if (notification.RelatedPlaylistId is long playlistId && playlistId > 0)
        {
            await Shell.Current.GoToAsync($"playlist?id={playlistId}");
            return;
        }

        if (notification.RelatedAlbumId is long albumId && albumId > 0)
        {
            // 专辑在 App 里没有详情页，既有语义就是「点专辑 = 播放整张」（歌手页、搜索页都这么做），
            // 这里保持一致，而不是另开一个页面。
            try
            {
                var songs = await api.SearchSongsAsync(albumId: albumId, pageSize: 100);
                if (songs.Items.Count == 0)
                {
                    await SongMenuHelper.ShowMessageDialogAsync("提示", "关联专辑没有可播放的歌曲。");
                    return;
                }
                // 专辑名直接取歌曲自带的 AlbumName，省一次「按 Id 查专辑」的接口调用
                var albumName = songs.Items
                    .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.AlbumName))?.AlbumName;
                player.PlayQueue(songs.Items, 0, $"专辑：{albumName ?? notification.Title}");
                await Shell.Current.GoToAsync("nowplaying");
            }
            catch (Exception ex)
            {
                AppLog.Warn($"[通知] 播放专辑失败: {ex.Message}");
                await SongMenuHelper.ShowMessageDialogAsync("提示", "专辑播放失败，请稍后再试。");
            }
            return;
        }

        if (notification.RelatedSongId is long songId && songId > 0)
        {
            var song = await api.GetSongAsync(songId);
            if (song is null)
            {
                await SongMenuHelper.ShowMessageDialogAsync("提示", "关联歌曲不存在或已下架。");
                return;
            }
            player.PlayQueue([song], 0, notification.Title);
            await Shell.Current.GoToAsync("nowplaying");
        }
    }

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");
}
