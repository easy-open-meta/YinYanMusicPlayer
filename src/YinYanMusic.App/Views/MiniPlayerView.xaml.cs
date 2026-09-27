using System.ComponentModel;
using YinYanMusic.App.Services;

namespace YinYanMusic.App.Views;

/// <summary>
/// 迷你播放条（从 MainPage 抽出，V2.6 起供多个页面复用）。
///
/// <para>用法：把 <c>BindingContext</c> 设为 <see cref="PlayerService"/>，然后
/// <c>Attach(player)</c> 订阅属性变化。**不用 PlayerService 的 PropertyChanged 事件**
/// 会让进度条不动（进度靠定时器改 PositionSeconds 驱动）。</para>
///
/// <para>队列浮窗不在这里实现 —— 各宿主页面的布局不同（MainPage 是 3 行 Grid、
/// 本地音乐页是 6 行），浮窗需要盖住整页，所以由宿主订阅
/// <see cref="QueueRequested"/> 自行呈现。这样组件本身只管播放条，不假设页面结构。</para>
/// </summary>
public partial class MiniPlayerView : ContentView
{
    private PlayerService? _player;
    private bool _subscribed;
    private double _containerWidth;

    /// <summary>用户点了「播放列表」按钮。宿主页面据此弹出队列浮窗。</summary>
    public event EventHandler? QueueRequested;

    public MiniPlayerView()
    {
        InitializeComponent();

#if ANDROID
        // Android 上不显示迷你播放条上方的进度条（MainPage 原有行为，抽组件时一并保留）：
        // 屏幕空间紧张，细进度线价值有限，且 Android 的系统媒体通知里已有进度。
        ProgressContainer.IsVisible = false;
#endif

        ProgressContainer.SizeChanged += (_, _) =>
        {
            _containerWidth = ProgressContainer.Width;
            UpdateProgress();
        };

        // Windows：鼠标悬停时把进度条加粗，便于拖动（与 MainPage 原行为一致）
        ProgressContainer.HandlerChanged += (_, _) => SetupHover();
    }

    /// <summary>绑定播放器并开始跟随它的状态。可重复调用（会先退订旧的）。</summary>
    public void Attach(PlayerService player)
    {
        if (ReferenceEquals(_player, player) && _subscribed) return;

        Detach();

        _player = player;
        BindingContext = player;
        player.PropertyChanged += OnPlayerPropertyChanged;
        _subscribed = true;

        SetVisible(player.HasCurrent);
        UpdateProgress();
    }

    /// <summary>退订。宿主页面在 <c>OnNavigatedFrom</c> 调用，避免单例播放器长期持有已销毁的视图。</summary>
    public void Detach()
    {
        if (_player is not null && _subscribed)
            _player.PropertyChanged -= OnPlayerPropertyChanged;

        _subscribed = false;
        _player = null;
    }

    /// <summary>迷你播放条显示/隐藏（首次出现时淡入，避免生硬闪现）。</summary>
    private void SetVisible(bool visible)
    {
        if (visible == IsVisible) return;
        if (visible)
        {
            Opacity = 0;
            IsVisible = true;
            _ = this.FadeToAsync(1, 220, Easing.CubicOut);
        }
        else
        {
            IsVisible = false;
            Opacity = 1;
        }
    }

    private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerService.HasCurrent))
            MainThread.BeginInvokeOnMainThread(() => SetVisible(_player?.HasCurrent ?? false));
        else if (e.PropertyName is nameof(PlayerService.PositionSeconds) or nameof(PlayerService.DurationSeconds))
            MainThread.BeginInvokeOnMainThread(UpdateProgress);
    }

    private void UpdateProgress()
    {
        if (_player is null) return;

        var dur = _player.DurationSeconds;
        var pos = _player.PositionSeconds;
        var ratio = dur > 0 ? Math.Min(pos / dur, 1) : 0;

        ProgressFill.WidthRequest = _containerWidth * ratio;
        ProgressSlider.Maximum = dur > 0 ? dur : 1;
        ProgressSlider.Value = pos;
    }

    private void OnProgressDragCompleted(object? sender, EventArgs e)
    {
        if (sender is Slider slider) _player?.SeekTo(slider.Value);
    }

    private void SetupHover()
    {
#if WINDOWS
        if (ProgressContainer.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement el)
        {
            el.PointerEntered += (_, _) => SetBarThickness(6);
            el.PointerExited += (_, _) => SetBarThickness(3);
        }
#endif
    }

    private void SetBarThickness(double thickness)
    {
        // 容器高度固定，只让进度条本身变粗。
        // ⚠️ 之前 hover 时把容器从 20 改到 8，导致整个 mini player 在鼠标进入时
        // 整体上跳 12px —— 在本地音乐页面（有滚动列表 + mini player）体感非常"抖"，
        // 也让人误以为"和在线音乐页面的 mini player 不一样"（其实在线页同样代码，
        // 只是你可能没把鼠标移到过它上方）。容器 20 + BoxView 顶部对齐（VerticalOptions=Start），
        // hover 时 BoxView 变粗但位置不变，整个 mini player 不抖。
        ProgressTrack.HeightRequest = thickness;
        ProgressFill.HeightRequest = thickness;
    }

    private async void OnBodyTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("nowplaying");

    private void OnToggleClicked(object? sender, EventArgs e) => _player?.TogglePlayPause();

    private void OnPreviousClicked(object? sender, EventArgs e) => _ = _player?.PreviousAsync();

    private void OnNextClicked(object? sender, EventArgs e) => _ = _player?.NextAsync();

    private void OnQueueClicked(object? sender, EventArgs e) => QueueRequested?.Invoke(this, EventArgs.Empty);
}
