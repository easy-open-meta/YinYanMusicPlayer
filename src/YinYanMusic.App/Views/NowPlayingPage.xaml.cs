using System.ComponentModel;
using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Views;

public partial class NowPlayingPage : ContentPage
{
	private readonly NowPlayingViewModel _vm;
	private readonly IBlurService _blur = ServiceHelper.GetRequiredService<IBlurService>();
	private readonly IAcrylicImageService _acrylic = ServiceHelper.GetRequiredService<IAcrylicImageService>();
	private bool _isRotating;
	private bool _longPressActive;
	private IDispatcherTimer? _rotateTimer;
	private bool _coverLongPressSet;
	private bool _titleLongPressSet;

	// 亚克力：整页背景的封面模糊半径（dp，会按屏幕密度换算成像素）与主色叠加的不透明度
	private const float BackdropBlurRadius = 24f;
	private const double AccentLayerOpacity = 0.42;

	public NowPlayingPage() : this(ServiceHelper.GetRequiredService<NowPlayingViewModel>())
	{
	}

	public NowPlayingPage(NowPlayingViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;

		// 原生模糊只能作用在已创建的原生视图上，Handler 就绪后再挂；换 Handler 会再次触发
		BackdropCover.HandlerChanged += (_, _) => ApplyBackdropBlur();
		ApplyBackdropBlur();
		_vm.PropertyChanged += OnViewModelPropertyChanged;

		ProgressSlider.Maximum = _vm.Player.DurationSeconds > 0 ? _vm.Player.DurationSeconds : 1;
		ProgressSlider.Value = _vm.Player.PositionSeconds;
		VinylDisc.Rotation = _vm.Player.VinylRotation;

		VinylDisc.HandlerChanged += (_, _) => SetupLongPress();
		CoverImage.HandlerChanged += (_, _) => SetupLongPress();
		TitleLabel.HandlerChanged += (_, _) => SetupTitleLongPress();
		Loaded += OnPageLoaded;
	}

	private double _marqueeMaxWidth;        // 跑马灯可用最大宽度（页宽 - 预留音质标签空间）

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		// 歌名与音质标签是并排的：记录跑马灯可用最大宽度，
		// 否则长歌名会把音质标签挤出屏幕（StackLayout 不会自动压缩子元素）。
		if (width > 0)
		{
			var maxW = Math.Max(80, width - 170);
			if (Math.Abs(_marqueeMaxWidth - maxW) > 0.5)
			{
				_marqueeMaxWidth = maxW;
				RestartMarquee();
			}
		}
	}

	protected override void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		VinylDisc.Rotation = _vm.Player.VinylRotation;
		ProgressSlider.Maximum = _vm.Player.DurationSeconds > 0 ? _vm.Player.DurationSeconds : 1;
		ProgressSlider.Value = _vm.Player.PositionSeconds;
		_vm.Player.PropertyChanged += OnPlayerPropertyChanged;
		_ = _vm.RefreshIsLikedAsync();
		_ = _vm.RefreshArtistFollowedAsync();
		_ = _vm.RefreshAccentAsync();
#if WINDOWS
		// Windows：本页的原生长按订阅在离开后依然有效，而计时器已被 OnNavigatedFrom 置空，
		// 回到本页必须重新装回，否则长按封面会抛 NullReferenceException。
		// 同时清掉可能残留的按下态，避免唱片旋转被 _longPressActive 永久卡住。
		_isPointerPressed = false;
		_longPressActive = false;
		SetupLongPress();
#endif
		_isRotating = false;
		UpdateRotation();
		RestartMarquee();

		// Windows：窗口关闭不触发页面导航，需在窗口销毁时主动停掉页面定时器，
		// 避免 tick 触摸已销毁的原生元素抛 COMException
		if (Window is not null)
			Window.Destroying += OnWindowDestroying;
	}

	private void OnWindowDestroying(object? sender, EventArgs e)
	{
		StopAllPageTimers();
		if (Window is not null)
			Window.Destroying -= OnWindowDestroying;
	}

	private void StopAllPageTimers()
	{
		_isRotating = false;
		_rotateTimer?.Stop();
		_rotateTimer = null;
		StopMarquee();
	}

	private void OnPageLoaded(object? sender, EventArgs e)
	{

		SetupLongPress();
		SetupTitleLongPress();
		ApplyBackdropBlur();
	}

	// ===== 动态亚克力背景 ==================================================

	/// <summary>
	/// 给整页铺底封面加平台原生模糊。
	/// 平台不支持时什么都不做，退化成未模糊的封面底图（仍有主色与深色渐变保证可读）。
	/// 播放列表浮窗的底图不走这里——它由 <see cref="IAcrylicImageService"/> 在像素层做好。
	/// </summary>
	private void ApplyBackdropBlur()
	{
		if (BackdropCover.Handler is not null)
			_blur.Apply(BackdropCover, BackdropBlurRadius);
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(NowPlayingViewModel.AccentColor)) return;
		MainThread.BeginInvokeOnMainThread(() => AnimateAccent(_vm.AccentColor));
	}

	/// <summary>主色切换走 320ms 过渡，避免切歌时背景颜色硬跳。</summary>
	private void AnimateAccent(Color? target)
	{
		var from = BackdropAccent.Color ?? Colors.Transparent;
		var to = target ?? Colors.Transparent;
		var fromOpacity = BackdropAccent.Opacity;
		// 取不到主色时整层淡出，只留原来的深色渐变，观感和改造前一致
		var toOpacity = target is null ? 0.0 : AccentLayerOpacity;

		var anim = new Animation(t =>
		{
			BackdropAccent.Color = Blend(from, to, (float)t);
			BackdropAccent.Opacity = fromOpacity + (toOpacity - fromOpacity) * (float)t;
		}, 0, 1, Easing.CubicInOut);
		anim.Commit(this, nameof(AnimateAccent), length: 320);
	}

	private static Color Blend(Color a, Color b, float t) => Color.FromRgba(
		a.Red + (b.Red - a.Red) * t,
		a.Green + (b.Green - a.Green) * t,
		a.Blue + (b.Blue - a.Blue) * t,
		a.Alpha + (b.Alpha - a.Alpha) * t);

	private IDispatcherTimer? _marqueeTimer;
	private double _marqueeUnit;           // 一个滚动周期宽度 = 歌名宽 + 间隔

	/// <summary>重启歌名跑马灯：进入页面 / 切歌 / 页面尺寸变化时调用。文本未溢出容器时保持静止。</summary>
	private void RestartMarquee()
	{
		StopMarquee();
		MarqueeTrack.TranslationX = 0;
		_ = RestartMarqueeAfterLayoutAsync();
	}

	private async Task RestartMarqueeAfterLayoutAsync()
	{
		try
		{
			await Task.Delay(60);   // 等布局完成后再测量
			if (_marqueeMaxWidth <= 0) return;

			// 无约束测量文本自然宽度（容器已裁剪，Label 实际渲染宽度会被压缩，所以要独立测量）
			var textWidth = TitleLabel.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
			_marqueeUnit = textWidth + MarqueeTrack.Spacing;

			// 容器宽度自适应：不溢出时紧贴文本宽度（音质 badge 始终紧跟歌名），溢出时才占满可用宽度滚动
			var scrolling = textWidth + 4 > _marqueeMaxWidth;
			TitleMarquee.WidthRequest = scrolling ? _marqueeMaxWidth : Math.Max(80, textWidth + 4);

			MarqueeCopy.IsVisible = scrolling;   // 不滚动时隐藏副本，避免短歌名重复显示两遍
			if (!scrolling)
			{
				MarqueeTrack.TranslationX = 0;
				return;
			}

			// 匀速滚动一个周期（歌名+间隔）后瞬移复位——内容周期化，复位点视觉无缝
			var totalMs = Math.Clamp(_marqueeUnit * 20, 2000, 12000);
			var step = _marqueeUnit / (totalMs / 16.0);
			_marqueeTimer = Dispatcher.CreateTimer();
			_marqueeTimer.Interval = TimeSpan.FromMilliseconds(16);
			_marqueeTimer.Tick += (_, _) =>
			{
				// Windows：直接关闭窗口时原生元素可能已销毁，触摸会抛 COMException
				if (MarqueeTrack.Handler is null)
				{
					StopMarquee();
					return;
				}
				try
				{
					var x = MarqueeTrack.TranslationX - step;
					if (x <= -_marqueeUnit) x += _marqueeUnit;
					MarqueeTrack.TranslationX = x;
				}
				catch (System.Runtime.InteropServices.COMException)
				{
					StopMarquee();
				}
			};
			_marqueeTimer.Start();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[跑马灯] {ex.Message}");
		}
	}

	private bool token_source_cancelled() => _marqueeTimer is not null && false;

	private void StopMarquee()
	{
		_marqueeTimer?.Stop();
		_marqueeTimer = null;
	}

	private async void OnArtistTapped(object? sender, TappedEventArgs e)
	{
		var song = _vm.Player.Current;
		if (song is null || song.ArtistId == 0) return;
		await Shell.Current.GoToAsync($"artist?artistId={song.ArtistId}");
	}


	private bool _titlePointerPressed;
	private CancellationTokenSource? _titleLongPressCts;

	private void SetupTitleLongPress()
	{
#if WINDOWS
		if (TitleLabel.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement platformElement)
		{
			platformElement.PointerPressed += (_, _) =>
			{
				_titlePointerPressed = true;
				// 取消上一次尚未完成的延迟，避免快速连点时堆叠多个任务
				_titleLongPressCts?.Cancel();
				_titleLongPressCts?.Dispose();
				var cts = new CancellationTokenSource();
				_titleLongPressCts = cts;
				_ = Task.Run(async () =>
				{
					try
					{
						await Task.Delay(500, cts.Token);
					}
					catch (OperationCanceledException)
					{
						return;
					}
					if (_titlePointerPressed)
						MainThread.BeginInvokeOnMainThread(ShowCopyMenu);
				});
			};
			platformElement.PointerReleased += (_, _) => _titlePointerPressed = false;
		}
#elif ANDROID
		if (_titleLongPressSet) return;
		if (TitleLabel.Handler?.PlatformView is Android.Views.View platformView)
		{
			_titleLongPressSet = true;
			platformView.LongClickable = true;
			platformView.LongClick += (_, e) =>
			{
				e.Handled = true;
				MainThread.BeginInvokeOnMainThread(ShowCopyMenu);
			};
		}
#endif
	}

	private async void ShowCopyMenu()
	{
		try
		{
			// 取 Current?.Title 而不是 CurrentTitle：后者在没歌时是"未在播放"的占位文案，
			// 不该被复制出去（也顺带让"没歌时不显示复制选项"这条判断真的生效）。
			var title = _vm.Player.Current?.Title ?? string.Empty;
			var artist = _vm.Player.CurrentArtist;
			var album = _vm.Player.CurrentAlbum;
			if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(artist) && string.IsNullOrEmpty(album)) return;

			var options = new List<string>();
			if (!string.IsNullOrEmpty(title)) options.Add("复制歌曲名");
			if (!string.IsNullOrEmpty(artist)) options.Add("复制歌手名");
			if (!string.IsNullOrEmpty(album)) options.Add("复制专辑名");
			if (options.Count == 0) return;

			var picked = await SongMenuHelper.ShowBottomSheetAsync("选择复制", options);
			var text = picked switch
			{
				"复制歌曲名" => title,
				"复制歌手名" => artist,
				"复制专辑名" => album,
				_ => null
			};
			if (string.IsNullOrEmpty(text)) return;

#if WINDOWS
			var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
			package.SetText(text);
			Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
#else
			await Clipboard.Default.SetTextAsync(text);
#endif

			// 复制成功的反馈：两端都用同一套页内轻提示（Windows 没有原生 Toast 对等物）
			await SongMenuHelper.ShowToastAsync($"复制「{text}」成功", this);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[复制异常] {ex.Message}");
		}
	}

	private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(PlayerService.PositionSeconds))
			MainThread.BeginInvokeOnMainThread(() => ProgressSlider.Value = Math.Min(_vm.Player.PositionSeconds, _vm.Player.DurationSeconds));
		else if (e.PropertyName == nameof(PlayerService.DurationSeconds))
			MainThread.BeginInvokeOnMainThread(() => ProgressSlider.Maximum = _vm.Player.DurationSeconds);
		else if (e.PropertyName == nameof(PlayerService.Current))
		{
			_ = _vm.RefreshIsLikedAsync();
			_ = _vm.RefreshAccentAsync();                         // 切歌后按新封面重算背景主色
			MainThread.BeginInvokeOnMainThread(RestartMarquee);   // 切歌后按新歌名重新评估跑马灯
		}
		else if (e.PropertyName == nameof(PlayerService.IsPlaying))
			MainThread.BeginInvokeOnMainThread(UpdateRotation);
	}

	private void UpdateRotation()
	{
		if (_longPressActive) return;
		if (_vm.Player.IsPlaying && !_isRotating)
		{
			_isRotating = true;
			_rotateTimer = Dispatcher.CreateTimer();
			_rotateTimer.Interval = TimeSpan.FromMilliseconds(16);
			_rotateTimer.Tick += (_, _) =>
			{
				// Windows：直接关闭窗口时不走页面导航，原生元素可能已销毁——触摸会抛 COMException
				if (VinylDisc.Handler is null)
				{
					_isRotating = false;
					_rotateTimer?.Stop();
					return;
				}
				try
				{
					VinylDisc.Rotation += 0.72;
					_vm.Player.VinylRotation = VinylDisc.Rotation % 360;
				}
				catch (System.Runtime.InteropServices.COMException)
				{
					// 元素已随窗口销毁：停表，进程随后自然退出
					_isRotating = false;
					_rotateTimer?.Stop();
					_rotateTimer = null;
				}
			};
			_rotateTimer.Start();
		}
		else if (!_vm.Player.IsPlaying && _isRotating)
		{
			_isRotating = false;
			_rotateTimer?.Stop();
			_rotateTimer = null;
		}
	}

	private void OnProgressDragCompleted(object? sender, EventArgs e)
	{
		if (sender is Slider slider)
			_vm.SliderChangedCommand.Execute(slider.Value);
	}

	private void OnVolumeButtonClicked(object? sender, EventArgs e)
	{
		VolumePopup.IsVisible = !VolumePopup.IsVisible;
		VolumePopupOverlay.IsVisible = VolumePopup.IsVisible;
	}

	private void OnVolumePopupOverlayTapped(object? sender, TappedEventArgs e)
	{
		VolumePopup.IsVisible = false;
		VolumePopupOverlay.IsVisible = false;
	}

	private void ShowCoverPreview()
	{
		var coverUrl = _vm.Player.CoverUrl;
		if (string.IsNullOrEmpty(coverUrl)) return;
		PreviewImage.Source = ImageSource.FromUri(new Uri(coverUrl));
		CoverPreviewOverlay.IsVisible = true;
	}

	private IDispatcherTimer? _longPressTimer;
	private bool _isPointerPressed;
#if WINDOWS
	// 已挂上原生 PointerPressed/PointerReleased 的那个平台元素（同一个只挂一次，元素重建后重新挂）
	private Microsoft.UI.Xaml.UIElement? _coverLongPressElement;
#endif

#if WINDOWS
	/// <summary>
	/// 保证长按计时器存在（Windows）。
	/// <para>
	/// <c>OnNavigatedFrom</c> 会把 <c>_longPressTimer</c> 置空来停用长按，但挂在原生元素上的
	/// PointerPressed 订阅并不会随之解除，而 <c>OnNavigatedTo</c> 也不会重建计时器。
	/// 于是「离开播放页 → 返回 → 长按封面」时，原生事件回调里的 <c>_longPressTimer</c> 仍是 null，
	/// <c>Start()</c> 直接抛 NullReferenceException。因此回到页面时必须重新装回计时器。
	/// </para>
	/// </summary>
	private void EnsureLongPressTimer()
	{
		if (_longPressTimer is not null) return;

		var timer = Dispatcher.CreateTimer();
		timer.Interval = TimeSpan.FromMilliseconds(300);
		// 捕获 timer 本身而不是每次都读字段：字段随时可能被 OnNavigatedFrom 置空
		timer.Tick += (_, _) =>
		{
			timer.Stop();
			_longPressActive = false;
			// 页面已离开就不要再弹封面预览
			if (_isPointerPressed && _longPressTimer is not null)
				MainThread.BeginInvokeOnMainThread(ShowCoverPreview);
		};
		_longPressTimer = timer;
	}
#endif

	private void SetupLongPress()
	{

#if WINDOWS
		// 计时器可能已被 OnNavigatedFrom 置空，先补上再挂/复用订阅
		EnsureLongPressTimer();

		if (VinylDisc.Handler?.PlatformView is not Microsoft.UI.Xaml.UIElement platformElement)
			return;

		// 同一元素重复调用时不要重复订阅；换了平台元素（Handler 重建）则重新订阅
		if (ReferenceEquals(_coverLongPressElement, platformElement)) return;
		_coverLongPressElement = platformElement;

		platformElement.PointerPressed += (_, _) =>
		{
			// 先取局部变量再判空，绝不直接碰字段：
			// 原生事件订阅在页面离开后依然有效，计时器为 null 即代表本次按下应整体作废。
			var timer = _longPressTimer;
			if (timer is null) return;

			_isPointerPressed = true;
			_longPressActive = true;
			_isRotating = false;
			_rotateTimer?.Stop();
			timer.Start();
		};
		platformElement.PointerReleased += (_, _) =>
		{
			_isPointerPressed = false;
			var timer = _longPressTimer;
			timer?.Stop();
			_longPressActive = false;
			// 页面已离开：不要再恢复唱片旋转
			if (timer is null) return;
			UpdateRotation();
		};
#elif ANDROID
		if (_coverLongPressSet) return;
		if (CoverImage.Handler?.PlatformView is Android.Views.View coverView)
		{
			_coverLongPressSet = true;
			coverView.LongClickable = true;
			coverView.LongClick += (_, e) =>
			{
				e.Handled = true;
				MainThread.BeginInvokeOnMainThread(ShowCoverPreview);
			};
		}
		if (VinylDisc.Handler?.PlatformView is Android.Views.View discView)
		{
			discView.LongClickable = true;
			discView.LongClick += (_, e) =>
			{
				e.Handled = true;
				MainThread.BeginInvokeOnMainThread(ShowCoverPreview);
			};
		}
#endif
	}

	private void OnCoverPreviewBackgroundTapped(object? sender, TappedEventArgs e)
	{
		CoverPreviewOverlay.IsVisible = false;
	}

	private void OnCoverPreviewCloseClicked(object? sender, EventArgs e)
	{
		CoverPreviewOverlay.IsVisible = false;
	}

	private async void OnSaveCoverClicked(object? sender, EventArgs e)
	{
		var coverUrl = _vm.Player.CoverUrl;
		if (string.IsNullOrEmpty(coverUrl))
		{
			await SongMenuHelper.ShowMessageDialogAsync("提示", "1可保存的封面", "确定");
			return;
		}

		try
		{
			using var http = new HttpClient();
			var bytes = await http.GetByteArrayAsync(coverUrl);

			var rawName = $"{_vm.Player.CurrentTitle}-{_vm.Player.CurrentArtist}.jpg";
			var fileName = string.Join("_", rawName.Split(Path.GetInvalidFileNameChars()));

			var picturesDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
			var saveDir = Path.Combine(picturesDir, "YinYanMusic");
			Directory.CreateDirectory(saveDir);
			var path = Path.Combine(saveDir, fileName);
			await File.WriteAllBytesAsync(path, bytes);

			await SongMenuHelper.ShowMessageDialogAsync("保存成功", $"封面已保存到：\n{path}", "确定");
		}
		catch (Exception ex)
		{
			await SongMenuHelper.ShowMessageDialogAsync("保存失败", ex.Message, "确定");
		}
	}

	private async void OnQueueClicked(object? sender, EventArgs e)
	{
		var queue = _vm.Player.Queue;
		System.Diagnostics.Debug.WriteLine($"[Queue] OnQueueClicked: queueCount={queue.Count}, currentIndex={_vm.Player.CurrentIndex}");
		var items = new List<QueueItem>();
		for (var i = 0; i < queue.Count; i++)
		{
			items.Add(new QueueItem
			{
				Index = i,
				Title = queue[i].Title,
				Artist = queue[i].ArtistName,
				DurationSeconds = queue[i].DurationSeconds,
				IsCurrent = i == _vm.Player.CurrentIndex
			});
		}
		QueueList.ItemsSource = items;
		QueueCountLabel.Text = $"共 {items.Count} 首";
		QueueOverlay.IsVisible = true;
		// 底图随后补上：先让浮窗立刻出来（色调层本身已保证可读），
		// 磨砂底图生成好再换上，避免首次下载封面时点击要干等。
		await RefreshQueueAcrylicAsync();
	}

	/// <summary>给播放列表浮窗的亚克力底层换上当前封面的磨砂底图。</summary>
	private async Task RefreshQueueAcrylicAsync()
	{
		var src = await _acrylic.CreateAsync(_vm.Player.CoverUrl);
		if (src is not null) QueueAcrylicCover.Source = src;
	}

	private void OnCloseQueueClicked(object? sender, EventArgs e) => QueueOverlay.IsVisible = false;

	private void OnQueueOverlayBackgroundTapped(object? sender, TappedEventArgs e) => QueueOverlay.IsVisible = false;

	// 显式 Tap 事件（TappedEventArgs 无绑定参数，从 sender 拿 BindingContext）：
	// SelectionChanged 在 Android 上被行内 PointerGestureRecognizer 吞掉，收不到。
	private void OnQueueItemTapped(object? sender, TappedEventArgs e)
	{
		if ((sender as BindableObject)?.BindingContext is QueueItem item)
		{
			System.Diagnostics.Debug.WriteLine($"[Queue] tap item index={item.Index}, title={item.Title}");
			_vm.Player.PlayAt(item.Index);
			QueueOverlay.IsVisible = false;
		}
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		_isRotating = false;
		_rotateTimer?.Stop();
		_rotateTimer = null;
		_longPressTimer?.Stop();
		_longPressTimer = null;

		// 取消可能仍在等待的长按延迟任务，避免在已离开（可能已销毁）的页面上弹出菜单
		_titlePointerPressed = false;
		_titleLongPressCts?.Cancel();
		_titleLongPressCts?.Dispose();
		_titleLongPressCts = null;

		// 停止歌名跑马灯
		StopMarquee();

		if (Window is not null)
			Window.Destroying -= OnWindowDestroying;

		_vm.Player.VinylRotation = VinylDisc.Rotation % 360;
		_vm.Player.PropertyChanged -= OnPlayerPropertyChanged;
		_vm.PropertyChanged -= OnViewModelPropertyChanged;
		Loaded -= OnPageLoaded;

		base.OnNavigatedFrom(args);
	}
}
