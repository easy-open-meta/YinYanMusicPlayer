using System.ComponentModel;
using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core;
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

		// 两行跑马灯（歌名 / 歌手）：最小宽度不同 —— 歌名短时也要留出放徽标的位置，
		// 歌手那行可以更紧。可用最大宽度由 OnSizeAllocated 按页面宽度算好填进来。
		_titleMarquee = new MarqueeRow(TitleMarquee, MarqueeTrack, TitleLabel, MarqueeCopy, minWidth: 80);
		_artistMarquee = new MarqueeRow(ArtistMarquee, ArtistTrack, ArtistLabel, ArtistCopy, minWidth: 40);

		Loaded += OnPageLoaded;
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		// 两行各自算可用宽度：歌名那行右边跟着音质/已缓存徽标，歌手那行右边跟着关注按钮。
		// 不记录的话长文本会把右边的徽标/按钮挤出屏幕（StackLayout 不会自动压缩子元素）。
		//
		// 歌手那行的 122 是这么来的（少算一项，长歌手名就会顶到右侧按钮上）：
		//   页面左右内边距 24×2 = 48 + 收藏按钮那一列 40 + 关注按钮 28 + 行内间距 6 = 122。
		// 之前按「页宽 - 70」预留（只算了关注按钮），联合创作的长歌手名正好压到收藏/关注按钮上。
		if (width <= 0) return;

		var titleMax = Math.Max(80, width - 170);
		var artistMax = Math.Max(60, width - 122);
		if (Math.Abs(_titleMarquee.MaxWidth - titleMax) <= 0.5 && Math.Abs(_artistMarquee.MaxWidth - artistMax) <= 0.5) return;

		_titleMarquee.MaxWidth = titleMax;
		_artistMarquee.MaxWidth = artistMax;
		RestartMarquee();
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
		// 基本动画：页面内容整体淡入
		_ = RootGrid.FadeToAsync(1, 300, Easing.CubicOut);

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

	/// <summary>
	/// 一行跑马灯（歌名一行、歌手一行）。容器裁剪 + 平移；文本不溢出时保持静止。
	/// 抽成类是因为页面上有两行要用同一套逻辑 —— 复制一遍必然分叉（原来的实现只服务歌名一行）。
	/// </summary>
	private sealed class MarqueeRow(Grid viewport, HorizontalStackLayout track, Label label, Label copy, double minWidth)
	{
		public double MaxWidth { get; set; }          // 可用最大宽度（页面尺寸变化时更新）
		private double Unit { get; set; }            // 一个滚动周期宽度 = 文本宽 + 间隔
		private double Step { get; set; }

		public void Reset() => track.TranslationX = 0;

		/// <summary>测量文本并按需决定是否滚动；返回 true 表示这一行需要滚动。</summary>
		public bool MeasureAndApply()
		{
			if (MaxWidth <= 0) return false;

			// 无约束测量文本自然宽度（容器已裁剪，Label 实际渲染宽度会被压缩，所以要独立测量）
			var textWidth = label.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
			Unit = textWidth + track.Spacing;

			// 容器宽度自适应：不溢出时紧贴文本宽度（后面的徽标/按钮始终紧跟文本），溢出时才占满可用宽度滚动
			var scrolling = textWidth + 4 > MaxWidth;
			viewport.WidthRequest = scrolling ? MaxWidth : Math.Max(minWidth, textWidth + 4);
			copy.IsVisible = scrolling;   // 不滚动时隐藏副本，避免短文本重复显示两遍

			if (!scrolling)
			{
				track.TranslationX = 0;
				return false;
			}

			// 匀速滚动一个周期（文本+间隔）后瞬移复位 —— 内容周期化，复位点视觉无缝
			var totalMs = Math.Clamp(Unit * 20, 2000, 12000);
			Step = Unit / (totalMs / 16.0);
			return true;
		}

		/// <summary>推进一帧。原生元素已销毁时返回 false（调用方据此停掉计时器）。</summary>
		public bool Advance()
		{
			// Windows：直接关闭窗口时原生元素可能已销毁，触摸会抛 COMException
			if (track.Handler is null) return false;
			try
			{
				var x = track.TranslationX - Step;
				if (x <= -Unit) x += Unit;
				track.TranslationX = x;
				return true;
			}
			catch (System.Runtime.InteropServices.COMException)
			{
				return false;
			}
		}
	}

	private MarqueeRow _titleMarquee = null!;
	private MarqueeRow _artistMarquee = null!;

	/// <summary>重启两行跑马灯：进入页面 / 切歌 / 页面尺寸变化时调用。</summary>
	private void RestartMarquee()
	{
		StopMarquee();
		_titleMarquee.Reset();
		_artistMarquee.Reset();
		_ = RestartMarqueeAfterLayoutAsync();
	}

	private async Task RestartMarqueeAfterLayoutAsync()
	{
		try
		{
			await Task.Delay(60);   // 等布局完成后再测量

			// ⚠️ 两行都要测，不能用 || 短路（短路会跳过第二行，歌手就永远不滚）
			var titleScrolling = _titleMarquee.MeasureAndApply();
			var artistScrolling = _artistMarquee.MeasureAndApply();
			if (!titleScrolling && !artistScrolling) return;

			_marqueeTimer = Dispatcher.CreateTimer();
			_marqueeTimer.Interval = TimeSpan.FromMilliseconds(16);
			_marqueeTimer.Tick += (_, _) =>
			{
				// 任一行对应的原生元素已销毁就整体停掉（两行生命周期一致）
				if (!_titleMarquee.Advance() || !_artistMarquee.Advance()) StopMarquee();
			};
			_marqueeTimer.Start();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[跑马灯] {ex.Message}");
		}
	}

	private void StopMarquee()
	{
		_marqueeTimer?.Stop();
		_marqueeTimer = null;
	}

	/// <summary>
	/// 点歌手那一行：单歌手直接进他的详情页；**联合创作先让用户挑一位** ——
	/// 一首歌挂着好几个人，固定跳主歌手等于把其他合作者藏起来（用户想看的可能正是那一位）。
	/// 挑人与跳转的实现放在 SongArtistSheets 里，歌曲「更多」菜单的同一项用的是它。
	/// </summary>
	private async void OnArtistTapped(object? sender, TappedEventArgs e)
	{
		// 只认有 ID 的歌手：本地歌与离线缓存的索引里没有歌手 ID，跳过去也只会是空页面
		var credits = SongArtistSheets.Followable(_vm.Player.Current);
		if (credits.Count == 0) return;

		var artistId = await SongArtistSheets.PickArtistAsync(credits);
		if (artistId is long id) await Shell.Current.GoToAsync($"artist?artistId={id}");
	}


	private bool _titlePointerPressed;
	private CancellationTokenSource? _titleLongPressCts;

	/// <summary>逐个歌手那几条复制项的前缀（多歌手时才出现）。</summary>
	private const string ArtistOptionPrefix = "复制歌手：";

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
			var artists = _vm.Player.CurrentArtists;
			var artist = _vm.Player.CurrentArtist;
			var album = _vm.Player.CurrentAlbum;
			if (string.IsNullOrEmpty(title) && artists.Count == 0 && string.IsNullOrEmpty(album)) return;

			var options = new List<string>();
			if (!string.IsNullOrEmpty(title)) options.Add("复制歌曲名");
			// 歌手：单歌手时照旧一条；**多歌手（联合创作）时展开** —— 除"全部"外每位歌手各一条。
			// 复制整串（如"陈小春、陈国坤、…"）拿去搜索是搜不到的，用户要的通常是其中某一位。
			if (artists.Count > 1)
			{
				options.Add("复制全部歌手");
				foreach (var name in artists) options.Add(ArtistOptionPrefix + name);
			}
			else if (!string.IsNullOrEmpty(artist))
			{
				options.Add("复制歌手名");
			}
			if (!string.IsNullOrEmpty(album)) options.Add("复制专辑名");
			if (options.Count == 0) return;

			var picked = await SongMenuHelper.ShowBottomSheetAsync("选择复制", options);
			string? text = picked switch
			{
				"复制歌曲名" => title,
				"复制歌手名" => artist,                                        // 单歌手（多歌手时不会出现这一项）
				"复制全部歌手" => artists.Count > 0 ? string.Join(" / ", artists) : artist,
				"复制专辑名" => album,
				_ => null
			};
			// 逐个歌手那几条是动态文案（"复制歌手：Aimer"），没法写进 switch 的常量模式
			if (text is null && picked is not null && picked.StartsWith(ArtistOptionPrefix, StringComparison.Ordinal))
				text = picked[ArtistOptionPrefix.Length..];
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
			// 切歌必须重算关注状态：VM 里缓存着"这首歌有哪几位歌手"，
			// 不刷新的话联合创作那首的弹层会列上一首的歌手（关注按钮的状态同样会串台）。
			_ = _vm.RefreshArtistFollowedAsync();
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

	/// <summary>
	/// 打开评论面板（V2.9）。本地歌只在设备上、没有服务端记录（Id 是负数），
	/// 评论要挂的 songId 根本不存在 —— 入口按钮已隐藏，这里再挡一道（命令式路径仍可能触发）。
	/// </summary>
	private async void OnCommentsClicked(object? sender, EventArgs e)
	{
		var song = _vm.Player.Current;
		if (song is null) return;
		if (song.IsLocal)
		{
			await SongMenuHelper.ShowToastAsync("本地音乐暂不支持评论", this);
			return;
		}
		await SongMenuHelper.ShowCommentsAsync(song.Title, CommentTargets.Song, song.Id);
	}

	private void ShowCoverPreview()
	{
		var coverUrl = _vm.Player.CoverUrl;
		if (string.IsNullOrEmpty(coverUrl)) return;

		// ⚠️ 不能用 ImageSource.FromUri(new Uri(coverUrl))：本地歌的封面是磁盘路径
		// （/data/user/0/<pkg>/files/local-covers/x.jpg）或 content:// URI，
		// FromUri 解析不了 → 预览一片空白（真机踩到）。
		// 统一走 ImageSourceFactory，与列表封面同一份判断逻辑。
		var src = ImageSourceFactory.From(coverUrl);
		if (src is null) return;

		PreviewImage.Source = src;
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
			await SongMenuHelper.ShowMessageDialogAsync("提示", "当前歌曲没有可保存的封面", "确定");
			return;
		}

		try
		{
			// 本地歌的封面在磁盘上（或 content://），HttpClient 取不到 —— 统一走工厂读字节。
			var bytes = await ImageSourceFactory.ReadBytesAsync(coverUrl);
			if (bytes is null || bytes.Length == 0)
			{
				await SongMenuHelper.ShowMessageDialogAsync("保存失败", "封面内容读取失败。", "确定");
				return;
			}

			// 扩展名按实际字节判断，不再硬编码 .jpg（本地抽出的封面可能是 png/webp）
			var ext = GuessImageExtension(bytes);
			// 歌名-全部歌手（联合创作时把合作者都写进去）。
			// 分隔符刻意用 "_" 而不是界面上的 " / "："/" 是路径分隔符，进文件名只会被下面清洗成占位下划线。
			var artistText = _vm.Player.CurrentArtists.Count > 0
				? string.Join("_", _vm.Player.CurrentArtists)
				: _vm.Player.CurrentArtist;
			var rawName = $"{_vm.Player.CurrentTitle}-{artistText}{ext}";
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

	/// <summary>按文件头判断图片类型（本地抽出的封面可能是 png/webp，不该一律存成 .jpg）。</summary>
	private static string GuessImageExtension(byte[] bytes)
	{
		if (bytes.Length >= 8 &&
			bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
			return ".png";
		if (bytes.Length >= 12 &&
			bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
			bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
			return ".webp";
		if (bytes.Length >= 3 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
			return ".gif";
		return ".jpg";
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
				Artist = queue[i].ArtistsDisplay,   // 全部歌手（联合创作 → "Aimer / EGOIST"）
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

    // 行点击走 PressFeedbackBehavior.Tapped（同一路指针事件判定"按下未移动即点击"）：
    // 比独立 TapGestureRecognizer 可靠——后者在 CollectionView 里会把几像素漂移当滚动、吞掉点击。
    private void OnQueueItemTapped(object? sender, EventArgs e)
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
