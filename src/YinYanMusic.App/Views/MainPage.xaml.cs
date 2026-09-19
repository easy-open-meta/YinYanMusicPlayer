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
    private bool _playerSubscribed;
    private int _currentTab = 0;
    private const double DrawerWidth = 300;

    private readonly IAuthService _auth = ServiceHelper.GetRequiredService<IAuthService>();

	public MainPage() : this(ServiceHelper.GetRequiredService<HomeView>(), ServiceHelper.GetRequiredService<LibraryView>(), ServiceHelper.GetRequiredService<SearchView>(), ServiceHelper.GetRequiredService<PlayerService>())
	{
	}

	public MainPage(HomeView homeView, LibraryView libraryView, SearchView searchView, PlayerService player)
	{
		InitializeComponent();
#if ANDROID
		MiniProgressContainer.IsVisible = false;
#endif
		_homeView = homeView;
		_libraryView = libraryView;
		_searchView = searchView;
		_player = player;

		MiniPlayer.BindingContext = _player;

        // 抽屉初始隐藏在屏幕左侧外
        DrawerPanel.TranslationX = -DrawerWidth;
        CurrentThemeLabel.Text = ThemeService.Instance.CurrentDisplayName;

        // 主题切换时刷新代码里手动设置的颜色（底部选中 Tab、当前主题名）
        ThemeService.Instance.ThemeChanged += (_, _) =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ApplyTabColors();
                CurrentThemeLabel.Text = ThemeService.Instance.CurrentDisplayName;
            });
        };

        Loaded += async (_, _) =>
		{
			if (!_playerAttached)
			{
				_playerAttached = true;
				_player.AttachPlayer(Player);
			}
			SetupProgressHover();
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
	// 否则离场后的页面实例会被单例的事件一直引用（MainPage 是 Transient），
	// 且旧实例仍会去更新已经销毁的 UI。
    private void SubscribePlayer()
    {
        if (_playerSubscribed) return;
        _playerSubscribed = true;
        _player.PropertyChanged += OnPlayerPropertyChanged;
        MiniPlayer.IsVisible = _player.HasCurrent;
        UpdateMiniProgress();
    }

	private void UnsubscribePlayer()
	{
		if (!_playerSubscribed) return;
		_playerSubscribed = false;
		_player.PropertyChanged -= OnPlayerPropertyChanged;
	}


    private double _progressContainerWidth;

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerService.HasCurrent))
            MiniPlayer.IsVisible = _player.HasCurrent;
        else if (e.PropertyName == nameof(PlayerService.PositionSeconds) || e.PropertyName == nameof(PlayerService.DurationSeconds))
            MainThread.BeginInvokeOnMainThread(UpdateMiniProgress);
    }

    private void UpdateMiniProgress()
    {
        var dur = _player.DurationSeconds;
        var pos = _player.PositionSeconds;
        var ratio = dur > 0 ? Math.Min(pos / dur, 1) : 0;
        MiniProgressFill.WidthRequest = _progressContainerWidth * ratio;
        MiniProgressSlider.Maximum = dur > 0 ? dur : 1;
        MiniProgressSlider.Value = pos;
    }

    private void OnMiniProgressDragCompleted(object? sender, EventArgs e)
    {
        if (sender is Slider slider)
            _player.SeekTo(slider.Value);
    }

    private void SetupProgressHover()
    {
        MiniProgressContainer.SizeChanged += (_, _) =>
        {
            _progressContainerWidth = MiniProgressContainer.Width;
            UpdateMiniProgress();
        };
#if WINDOWS
        if (MiniProgressContainer.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement platformElement)
        {
            platformElement.PointerEntered += (_, _) =>
            {
                MiniProgressContainer.HeightRequest = 8;
                MiniProgressTrack.HeightRequest = 6;
                MiniProgressFill.HeightRequest = 6;
            };
            platformElement.PointerExited += (_, _) =>
            {
                MiniProgressContainer.HeightRequest = 3;
                MiniProgressTrack.HeightRequest = 3;
                MiniProgressFill.HeightRequest = 3;
            };
        }
#endif
    }

    private void ShowTab(int tab)
    {
        _currentTab = tab;
        ContentHost.Children.Clear();
        ContentHost.Children.Add(tab switch { 1 => _searchView, 2 => _libraryView, _ => _homeView });
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

    private async void OnMiniPlayerTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("nowplaying");

    private void OnToggleClicked(object? sender, EventArgs e) => _player.TogglePlayPause();

    private void OnPreviousClicked(object? sender, EventArgs e) => _ = _player.PreviousAsync();

    private void OnNextClicked(object? sender, EventArgs e) => _ = _player.NextAsync();

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
                Artist = queue[i].ArtistName,
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

    // 显式 Tap 事件：SelectionChanged 在 Android 上被行内 PointerGestureRecognizer 吞掉，收不到。
    private void OnQueueItemTapped(object? sender, TappedEventArgs e)
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

    private async void OnDrawerLogoutClicked(object? sender, EventArgs e)
    {
        // 与删除歌单同一套居中圆角确认框（不用原生 DisplayAlert，风格与全站统一）
        var confirm = await SongMenuHelper.ShowRoundedConfirmAsync(
            "退出登录", "确定要退出当前账号吗？", "退出", "取消", destructive: true);
        if (!confirm) return;
        await _auth.LogoutAsync();
        await CloseDrawerAsync();
        await Shell.Current.GoToAsync("///login");
    }

    #endregion
}
