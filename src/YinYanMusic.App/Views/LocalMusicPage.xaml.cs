using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

/// <summary>
/// 本地音乐页（V2.6）。进入时重载曲库、接管播放器；离开时退订，避免单例持有已销毁的视图。
/// </summary>
public partial class LocalMusicPage : ContentPage
{
    private readonly LocalMusicViewModel _vm;
    private readonly PlayerService _player;
    private bool _playerAttached;

    public LocalMusicPage() : this(
        ServiceHelper.GetRequiredService<LocalMusicViewModel>(),
        ServiceHelper.GetRequiredService<PlayerService>())
    {
    }

    public LocalMusicPage(LocalMusicViewModel vm, PlayerService player)
    {
        InitializeComponent();
        _vm = vm;
        _player = player;
        BindingContext = vm;

        // 迷你播放条的「播放列表」按钮 → 本页的队列浮窗
        MiniPlayer.QueueRequested += (_, _) => ShowQueue();
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // V2.6：本页可能由登录页**未登录**直达，此时 MainPage 尚未创建、
        // 它里面的 MediaElement 也就不存在，PlayerService 没有可用的播放器内核。
        // 所以这里用本页自己的 MediaElement 接管（幂等，重复进入不会重复绑定）。
        //
        // ⚠️ V2.7 起 AttachPlayer 变成"**只有在还没有长期内核时才接管**"：
        // 已登录时 MainPage 的内核一直在，本页就不抢 —— 否则本页退出时元素被销毁、
        // 底层 ExoPlayer 被 Release，而 PlayerService 还攥着它，
        // 之后播放就会变成"进度条在动但没声音"。
        if (!_playerAttached)
        {
            _playerAttached = true;
            _player.AttachPlayer(Player);
        }

        // 迷你播放条跟随同一个单例播放器；Attach 内部会先退订旧的，重复进入安全
        MiniPlayer.Attach(_player);

        _ = _vm.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        // PlayerService 是单例、生命周期长于本页：不退订会让它一直持有已销毁的 VM / 视图。
        // 注意**不能** DetachPlayer —— 那会停掉播放，而用户可能只是去看播放页。
        _vm.Detach();
        MiniPlayer.Detach();
        // 归还播放器：万一本页元素正在被使用（未登录直达场景），把播放迁回 MainPage 的内核；
        // 已登录时本页根本没接管，这行是空操作。
        _player.ReleasePlayer(Player);
        base.OnNavigatedFrom(args);
    }

    // ── 播放队列浮窗（与 MainPage 同一套交互） ──────────────────────────────

    private void ShowQueue()
    {
        var queue = _player.Queue;
        var items = new List<QueueItem>();
        for (var i = 0; i < queue.Count; i++)
        {
            items.Add(new QueueItem
            {
                Index = i,
                Title = queue[i].Title,
                Artist = queue[i].ArtistsDisplay,   // 本地歌没有歌手列表时它自会退回 ArtistName
                DurationSeconds = queue[i].DurationSeconds,
                IsCurrent = i == _player.CurrentIndex,
            });
        }
        QueueList.ItemsSource = items;
        QueueCountLabel.Text = $"共 {items.Count} 首";
        QueueOverlay.IsVisible = true;
    }

    private void OnCloseQueueClicked(object? sender, EventArgs e) => QueueOverlay.IsVisible = false;

    private void OnOverlayBackgroundTapped(object? sender, EventArgs e) => QueueOverlay.IsVisible = false;

    // 显式 Tap 事件：SelectionChanged 在 Android 上被行内 PointerGestureRecognizer 吞掉，收不到。
    private void OnQueueItemTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is QueueItem item)
        {
            _player.PlayAt(item.Index);
            QueueOverlay.IsVisible = false;
        }
    }
}
