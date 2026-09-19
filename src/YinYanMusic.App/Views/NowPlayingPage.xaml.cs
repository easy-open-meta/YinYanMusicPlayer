using System.ComponentModel;
using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Views;

public partial class NowPlayingPage : ContentPage
{
	private readonly NowPlayingViewModel _vm;
	private bool _isRotating;
	private bool _longPressActive;
	private IDispatcherTimer? _rotateTimer;
	private bool _coverLongPressSet;
	private bool _titleLongPressSet;

	public NowPlayingPage() : this(ServiceHelper.GetRequiredService<NowPlayingViewModel>())
	{
	}

	public NowPlayingPage(NowPlayingViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;

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
	}

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
			var title = _vm.Player.CurrentTitle;
			var artist = _vm.Player.CurrentArtist;
			if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(artist)) return;

			var options = new List<string>();
			if (!string.IsNullOrEmpty(title)) options.Add("复制歌曲名");
			if (!string.IsNullOrEmpty(artist)) options.Add("复制歌手名");
			if (options.Count == 0) return;

			var picked = await SongMenuHelper.ShowBottomSheetAsync("选择复制", options);
			var text = picked switch
			{
				"复制歌曲名" => title,
				"复制歌手名" => artist,
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
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[复制异常] {ex.Message}");
		}
	}

	private void OnPlayerPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(PlayerService.PositionSeconds))
			MainThread.BeginInvokeOnMainThread(() => ProgressSlider.Value = _vm.Player.PositionSeconds);
		else if (e.PropertyName == nameof(PlayerService.DurationSeconds))
			MainThread.BeginInvokeOnMainThread(() => ProgressSlider.Maximum = _vm.Player.DurationSeconds);
		else if (e.PropertyName == nameof(PlayerService.Current))
		{
			_ = _vm.RefreshIsLikedAsync();
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
#if WINDOWS
		if (VolumePopup.IsVisible) ApplyAcrylicBackground();
#endif
	}

#if WINDOWS
	private void ApplyAcrylicBackground()
	{
		var acrylic = new Microsoft.UI.Xaml.Media.AcrylicBrush
		{
			TintColor = new Windows.UI.Color { A = 255, R = 26, G = 26, B = 46 },
			TintOpacity = 0.55,
			TintLuminosityOpacity = 0.8,
			FallbackColor = new Windows.UI.Color { A = 230, R = 26, G = 26, B = 46 }
		};

		switch (VolumePopup.Handler?.PlatformView)
		{
			case Microsoft.UI.Xaml.Controls.Border border:
				border.Background = acrylic;
				break;
			case Microsoft.UI.Xaml.Controls.Panel panel:
				panel.Background = acrylic;
				break;
		}
	}
#endif


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

	private void SetupLongPress()
	{

#if WINDOWS
		if (VinylDisc.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement platformElement)
		{
			_longPressTimer = Dispatcher.CreateTimer();
			_longPressTimer.Interval = TimeSpan.FromMilliseconds(300);
			_longPressTimer.Tick += (_, _) =>
			{
				_longPressTimer.Stop();
				_longPressActive = false;
				if (_isPointerPressed)
					MainThread.BeginInvokeOnMainThread(ShowCoverPreview);
			};

			platformElement.PointerPressed += (_, _) =>
			{
				_isPointerPressed = true;
				_longPressActive = true;
				_isRotating = false;
				_rotateTimer?.Stop();
				_longPressTimer.Start();
			};
			platformElement.PointerReleased += (_, _) =>
			{
				_isPointerPressed = false;
				_longPressTimer.Stop();
				_longPressActive = false;
				UpdateRotation();
			};
		}
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

	private void OnQueueClicked(object? sender, EventArgs e)
	{
		var queue = _vm.Player.Queue;
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
	}

	private void OnCloseQueueClicked(object? sender, EventArgs e) => QueueOverlay.IsVisible = false;

	private void OnQueueOverlayBackgroundTapped(object? sender, TappedEventArgs e) => QueueOverlay.IsVisible = false;

	private void OnQueueSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is QueueItem item)
		{
			_vm.Player.PlayAt(item.Index);
			QueueOverlay.IsVisible = false;
			QueueList.SelectedItem = null;
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
		Loaded -= OnPageLoaded;

		base.OnNavigatedFrom(args);
	}
}
