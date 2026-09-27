using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

/// <summary>
/// 缓存管理页（V2.7）。进入时对账 + 接管播放器；离开时退订事件。
/// </summary>
public partial class CacheManagePage : ContentPage
{
    private readonly CacheManageViewModel _vm;
    private readonly PlayerService _player;
    private bool _playerAttached;

    public CacheManagePage() : this(
        ServiceHelper.GetRequiredService<CacheManageViewModel>(),
        ServiceHelper.GetRequiredService<PlayerService>())
    {
    }

    public CacheManagePage(CacheManageViewModel vm, PlayerService player)
    {
        InitializeComponent();
        _vm = vm;
        _player = player;
        BindingContext = vm;
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        // 与本地音乐页同理：本页可能在没有 MainPage 的情况下抵达（未登录 → 设置 → 缓存管理），
        // 此时必须由本页的 MediaElement 接管播放内核，否则点歌不出声。
        // ⚠️ 已登录时 MainPage 的长期内核一直在，AttachPlayer 会**主动不接管** ——
        // 否则本页退出后元素被销毁、ExoPlayer 被 Release，其后所有播放都会"有进度没声音"。
        if (!_playerAttached)
        {
            _playerAttached = true;
            _player.AttachPlayer(Player);
        }

        _ = _vm.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        // 离开时只退订事件（单例服务不该长期持有已销毁的 VM），并且**归还播放器**：
        // 若本页元素正在被使用（未登录直达场景），把播放迁回 MainPage 的内核并续播。
        // 绝不调 DetachPlayer —— 那会把正在播的歌一起停掉。
        _vm.Detach();
        _player.ReleasePlayer(Player);
        base.OnNavigatedFrom(args);
    }
}
