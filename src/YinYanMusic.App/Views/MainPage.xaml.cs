using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Views;

public class QueueItem
{
    public int Index { get; set; }
    public string IndexText => (Index + 1).ToString();
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public bool IsCurrent { get; set; }
}

public partial class MainPage : ContentPage
{
    private readonly PlayerService _player;
    private readonly IAcrylicImageService _acrylic = ServiceHelper.GetRequiredService<IAcrylicImageService>();
    private readonly HomeView _homeView;
    private readonly LibraryView _libraryView;
    private readonly SearchView _searchView;
    private bool _mainLoaded;
    private bool _playerAttached;
    private int _currentTab = 0;
    private const double DrawerWidth = 300;

    private readonly IAuthService _auth = ServiceHelper.GetRequiredService<IAuthService>();

	public MainPage() : this(ServiceHelper.GetRequiredService<HomeView>(), ServiceHelper.GetRequiredService<LibraryView>(), ServiceHelper.GetRequiredService<SearchView>(), ServiceHelper.GetRequiredService<PlayerService>())
	{
	}

	public MainPage(HomeView homeView, LibraryView libraryView, SearchView searchView, PlayerService player)
	{
		InitializeComponent();
		_homeView = homeView;
		_libraryView = libraryView;
		_searchView = searchView;
		_player = player;

		// 迷你播放条：把「播放列表」按钮接到本页的队列浮窗上
		MiniPlayer.QueueRequested += (_, _) => OnQueueClicked(this, EventArgs.Empty);

        // 抽屉初始隐藏在屏幕左侧外
        DrawerPanel.TranslationX = -DrawerWidth;
        CurrentThemeLabel.Text = ThemeService.Instance.CurrentDisplayName;

        // 「偏好设置」入口只对 Windows 显示：Android 没有"关闭窗口"这个动作，整块功能不存在。
        // 它下面的分隔线也一起跟着显隐 —— 否则 Android 上两条线贴成一条粗线
        PreferencesDivider.IsVisible = PreferencesRow.IsVisible = AppPreferences.IsHideToTrayOnWindowCloseSupported;

        // 主题切换时刷新代码里手动设置的颜色（底部选中 Tab、当前主题名）
        ThemeService.Instance.ThemeChanged += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ApplyTabColors();
                CurrentThemeLabel.Text = ThemeService.Instance.CurrentDisplayName;
            });
        };

		// ⚠️ 兜底：Activity 销毁重建后，MainPage 的 MediaElement 会重新挂上 Handler，
		// 但它的 Loaded **不一定再触发**，于是没人去做"重新登记主内核"——
		// 表现就是点任何歌都播不了（2026-09-22 回归：日志只剩 `PlayAt: _player 为空`）。
		// 这里在 Handler 就绪时补一次登记：`AttachPrimaryPlayer` 内部幂等（已经生效就直接返回），
		// 只有在 `_player` 为 null 时才会真正接管，不会重复迁移播放状态。
		Player.HandlerChanged += (_, _) =>
		{
			if (Player.Handler is not null) _player.AttachPrimaryPlayer(Player);
		};

		Loaded += async (_, _) =>
		{
			if (!_playerAttached)
			{
				_playerAttached = true;
				// ⚠️ 这里必须用 AttachPrimaryPlayer（而不是 AttachPlayer）：本页的 1x1 MediaElement
				// 是**长期存活的播放内核**。二级页面（本地音乐 / 缓存管理）自带的 MediaElement
				// 会随页面退出而销毁，若让它抢走播放器，返回后每次播放都会落到已释放的实例上
				// —— 通知栏进度条照走、完全没有声音（2026-09-22 真机踩到）。
				_player.AttachPrimaryPlayer(Player);
			}
			if (!_mainLoaded)
			{
				ShowTab(0);
				_mainLoaded = true;
			}
		};
	}

	protected override void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		SubscribePlayer();
		if (!_mainLoaded) return;
		if (_currentTab == 0) _ = ServiceHelper.GetRequiredService<HomeViewModel>().LoadCommand.ExecuteAsync(null);
		else if (_currentTab == 1) _ = ServiceHelper.GetRequiredService<SearchViewModel>().LoadCommand.ExecuteAsync(null);
		else _ = ServiceHelper.GetRequiredService<LibraryViewModel>().LoadCommand.ExecuteAsync(null);
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		UnsubscribePlayer();
		base.OnNavigatedFrom(args);
	}

	// PlayerService 是单例，生命周期长于页面：订阅与取消订阅必须成对，
	// 否则离场后的页面实例会被单例的事件一直引用（MainPage 是 Transient）。
	// V2.6：迷你播放条的绑定与进度刷新已移入 MiniPlayerView.Attach/Detach，
	// 这里不再直接订阅 PlayerService —— 组件自己管，避免两处重复刷新。
	private void SubscribePlayer() => MiniPlayer.Attach(_player);

	private void UnsubscribePlayer() => MiniPlayer.Detach();

    private void ShowTab(int tab)
    {
        _currentTab = tab;
        ContentHost.Children.Clear();
        View view = tab switch { 1 => _searchView, 2 => _libraryView, _ => _homeView };
        // 基本动画：切换 Tab 时新内容淡入，避免生硬跳变
        view.Opacity = 0;
        ContentHost.Children.Add(view);
        ApplyTabColors();
        _ = view.FadeToAsync(1, 200, Easing.CubicOut);
        ApplyTabColors();
        if (tab == 0) _ = ServiceHelper.GetRequiredService<HomeViewModel>().LoadCommand.ExecuteAsync(null);
        else if (tab == 1) _ = ServiceHelper.GetRequiredService<SearchViewModel>().LoadCommand.ExecuteAsync(null);
        else _ = ServiceHelper.GetRequiredService<LibraryViewModel>().LoadCommand.ExecuteAsync(null);
    }

    /// <summary>根据当前主题色刷新底部 Tab 的选中/未选中颜色（运行时主题切换需手动刷新）。</summary>
    private void ApplyTabColors()
    {
        if (Application.Current is null) return;
        var accent = (Color)Application.Current.Resources["Accent"];
        var gray = (Color)Application.Current.Resources["Gray500"];
        TabHome.TextColor = _currentTab == 0 ? accent : gray;
        TabSearch.TextColor = _currentTab == 1 ? accent : gray;
        TabLibrary.TextColor = _currentTab == 2 ? accent : gray;
    }

    private void OnTabHomeClicked(object? sender, EventArgs e) => ShowTab(0);

    private void OnTabSearchClicked(object? sender, EventArgs e) => ShowTab(1);

    private void OnTabLibraryClicked(object? sender, EventArgs e) => ShowTab(2);

    // 注：迷你播放条上的「点进播放页 / 上一首 / 播放暂停 / 下一首」已随组件迁入
    // MiniPlayerView.xaml.cs。MainPage 只保留「播放列表」浮窗（它要盖住整页，
    // 而组件只占页脚那一行，所以浮窗由宿主页面提供）。

    private void OnQueueClicked(object? sender, EventArgs e)
    {
        var queue = _player.Queue;
        var items = new List<QueueItem>();
        for (var i = 0; i < queue.Count; i++)
        {
            items.Add(new QueueItem
            {
                Index = i,
                Title = queue[i].Title,
                Artist = queue[i].ArtistsDisplay,   // 全部歌手（联合创作 → "Aimer / EGOIST"）
                DurationSeconds = queue[i].DurationSeconds,
                IsCurrent = i == _player.CurrentIndex
            });
        }
        QueueList.ItemsSource = items;
        QueueCountLabel.Text = $"共 {items.Count} 首";
        QueueOverlay.IsVisible = true;
        // 底图随后补上：先让浮窗立刻出来（色调层本身已保证可读），
        // 磨砂底图生成好再换上，避免首次下载封面时点击要干等。
        _ = RefreshQueueAcrylicAsync();
    }

    /// <summary>给播放列表浮窗的亚克力底层换上当前封面的磨砂底图。</summary>
    private async Task RefreshQueueAcrylicAsync()
    {
        var src = await _acrylic.CreateAsync(_player.CoverUrl);
        if (src is not null) QueueAcrylicCover.Source = src;
    }

    private void OnCloseQueueClicked(object? sender, EventArgs e) => QueueOverlay.IsVisible = false;

    private void OnOverlayBackgroundTapped(object? sender, EventArgs e) => QueueOverlay.IsVisible = false;

    // 行点击走 PressFeedbackBehavior.Tapped（同一路指针事件判定"按下未移动即点击"）：
    // 比独立 TapGestureRecognizer 可靠——后者在 CollectionView 里会把几像素漂移当滚动、吞掉点击。
    private void OnQueueItemTapped(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is QueueItem item)
        {
            _player.PlayAt(item.Index);
            QueueOverlay.IsVisible = false;
        }
    }

    #region 设置抽屉 / 主题颜色

    private async void OnMenuClicked(object? sender, EventArgs e) => await OpenDrawerAsync();

    private async Task OpenDrawerAsync()
    {
        DrawerOverlay.IsVisible = true;
        DrawerBackdrop.Opacity = 0;
        await Task.WhenAll(
            DrawerPanel.TranslateTo(0, 0, 250, Easing.CubicOut),
            DrawerBackdrop.FadeTo(1, 250, Easing.CubicOut));
    }

    private async Task CloseDrawerAsync()
    {
        await Task.WhenAll(
            DrawerPanel.TranslateTo(-DrawerWidth, 0, 220, Easing.CubicIn),
            DrawerBackdrop.FadeTo(0, 220, Easing.CubicIn));
        DrawerOverlay.IsVisible = false;
    }

    private async void OnDrawerCloseClicked(object? sender, EventArgs e) => await CloseDrawerAsync();

    private async void OnDrawerBackdropTapped(object? sender, EventArgs e) => await CloseDrawerAsync();

    /// <summary>
    /// 主题颜色：交给全局对话框（与「新建歌单」同一套居中卡片）。
    /// 弹窗只返回用户选中的 key，**应用主题是这里的职责**；ThemeChanged 事件会顺手
    /// 刷新抽屉里的主题名与底部 Tab 配色。
    /// </summary>
    private async void OnThemeColorClicked(object? sender, EventArgs e)
    {
        var key = await SongMenuHelper.ShowThemePickerAsync();
        if (key is null) return;                       // 取消 = 什么都不做
        ThemeService.Instance.ApplyTheme(key);
        CurrentThemeLabel.Text = ThemeService.Instance.CurrentDisplayName;
    }

    /// <summary>
    /// 缓存管理（V2.7）：在线歌缓存下来的音频、占用空间与清理入口。
    /// 与其他抽屉入口一致，先收起抽屉再导航，否则返回时抽屉还开着、遮罩仍在最上层。
    /// </summary>
    private async void OnDrawerCacheClicked(object? sender, EventArgs e)
    {
        await CloseDrawerAsync();
        await Shell.Current.GoToAsync("cacheManage");
    }

    /// <summary>
    /// 安全中心（V2.5 调整）：入口从账号页移到抽屉里。
    /// 导航前先收起抽屉，否则返回时抽屉还开着、遮罩仍在最上层。
    /// </summary>
    private async void OnDrawerSecurityClicked(object? sender, EventArgs e)
    {
        await CloseDrawerAsync();
        await Shell.Current.GoToAsync("security");
    }

    /// <summary>关于（V2.15）：应用信息。导航前先收起抽屉，否则返回时抽屉还开着、遮罩仍在最上层。</summary>
    private async void OnDrawerAboutClicked(object? sender, EventArgs e)
    {
        await CloseDrawerAsync();
        await Shell.Current.GoToAsync("about");
    }

    /// <summary>第三方开源组件引用（V2.15）：开源许可证列表。导航前先收起抽屉。</summary>
    private async void OnDrawerThirdPartyClicked(object? sender, EventArgs e)
    {
        await CloseDrawerAsync();
        await Shell.Current.GoToAsync("thirdParty");
    }

    /// <summary>
    /// 偏好设置：本机行为开关（目前是「关闭窗口时最小化」，Windows 专属）。
    /// 与「服务器设置」分开 —— 那页是"这台设备怎么连服务器"，这页是"这台设备的窗口怎么表现"。
    /// 与其他抽屉入口一致，导航前先收起抽屉，否则返回时抽屉还开着、遮罩仍在最上层。
    /// </summary>
    private async void OnDrawerPreferencesClicked(object? sender, EventArgs e)
    {
        await CloseDrawerAsync();
        await Shell.Current.GoToAsync("preferences");
    }

    private async void OnDrawerLogoutClicked(object? sender, EventArgs e)
    {        // 与删除歌单同一套居中圆角确认框（不用原生 DisplayAlert，风格与全站统一）
        var confirm = await SongMenuHelper.ShowRoundedConfirmAsync(
            "退出登录", "确定要退出当前账号吗？", "退出", "取消", destructive: true);
        if (!confirm) return;
        await _auth.LogoutAsync();
        await CloseDrawerAsync();
        await Shell.Current.GoToAsync("///login");
    }

    #endregion
}
