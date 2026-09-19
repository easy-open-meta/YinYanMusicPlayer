using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Maui.Views;
using System.Globalization;
using System.Text.Json;
using YinYanMusic.Core.Dtos;
#if ANDROID
using CommunityToolkit.Maui.Core.Handlers;
using AndroidX.Media3.Common;
#endif

namespace YinYanMusic.App.Services;

public enum PlayMode { Sequential, ListLoop, SingleLoop, Random }

public partial class PlayerService(IMusicApi api) : ObservableObject
{
    private MediaElement? _player;
    private List<SongDto> _queue = [];
    private int _index = -1;
    private readonly Random _rng = new();
    private bool _mediaReady;
    // 播放意图：PlayAt 里的 Play() 在 Windows 上切 Source 时会丢失（新源打开后停在暂停态），
    // 需要在 MediaOpened 后补一次 Play。用户手动暂停/播放失败时清除，避免与用户意图打架。
    private bool _playPending;
    private bool _seeking;
    private DateTime _lastPlayAtTime = DateTime.MinValue;
    private CancellationTokenSource? _timerCts;
#if ANDROID
    private IPlayer? _exoPlayer;
    private CustomForwardingPlayer? _forwardingPlayer;
    private double _lastPosSec;
#endif

    static void LogInfo(string msg)
    {
#if ANDROID
        Android.Util.Log.Info("YinYan", msg);
#else
        System.Diagnostics.Debug.WriteLine(msg);
#endif
    }
    static void LogWarn(string msg)
    {
#if ANDROID
        Android.Util.Log.Warn("YinYan", msg);
#else
        System.Diagnostics.Debug.WriteLine(msg);
#endif
    }

    public double VinylRotation { get; set; }
    private static string VolumeFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YinYanMusic", "volume.txt");
    private static string PlayModeFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YinYanMusic", "playmode.txt");
    private static string StateFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YinYanMusic", "player_state.json");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCurrent))]
    [NotifyPropertyChangedFor(nameof(CurrentTitle))]
    [NotifyPropertyChangedFor(nameof(CurrentArtist))]
    [NotifyPropertyChangedFor(nameof(CoverUrl))]
    [NotifyPropertyChangedFor(nameof(QualityLabel))]
    private SongDto? current;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private double positionSeconds;

    [ObservableProperty]
    private double durationSeconds = 1;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private double volume = LoadSavedVolume();

    private static double LoadSavedVolume()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YinYanMusic", "volume.txt");
            if (File.Exists(path)) return double.Parse(File.ReadAllText(path), CultureInfo.InvariantCulture);
        }
        catch { }
        return 1.0;
    }

    private static PlayMode LoadSavedPlayMode()
    {
        try
        {
            if (File.Exists(PlayModeFilePath) &&
                Enum.TryParse<PlayMode>(File.ReadAllText(PlayModeFilePath), out var saved))
                return saved;
        }
        catch { }
        return PlayMode.ListLoop;
    }

    [ObservableProperty]
    private PlayMode playMode = LoadSavedPlayMode();

    [ObservableProperty]
    private string sourceName = string.Empty;

    public bool HasCurrent => Current is not null;
    public string CurrentTitle => Current?.Title ?? "未在播放";
    public string CurrentArtist => Current?.ArtistName ?? string.Empty;
    public string CoverUrl => Current?.CoverUrl is null ? string.Empty : ApiConfig.Absolute(Current.CoverUrl);

    /// <summary>
    /// 当前歌曲的音质标签：取音频文件扩展名（FLAC / MP3 / M4A …）。
    /// 播放页用来显示小标签；没有音频地址或扩展名异常时返回空字符串，标签会自动隐藏。
    /// </summary>
    public string QualityLabel
    {
        get
        {
            var url = Current?.AudioUrl;
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            // 去掉可能的 query/fragment 再取扩展名（URL 可能是转义过的，不影响扩展名）
            var ext = Path.GetExtension(url.Split('?', '#')[0]).TrimStart('.');
            return ext.Length is > 0 and <= 5 ? ext.ToUpperInvariant() : string.Empty;
        }
    }
    public IReadOnlyList<SongDto> Queue => _queue;
    public int CurrentIndex => _index;
    public string PlayModeIcon => PlayMode switch
    {
        PlayMode.Sequential => "\uE05F",
        PlayMode.ListLoop => "\uE028",
        PlayMode.SingleLoop => "\uE041",
        PlayMode.Random => "\uE043",
        _ => "\uE028"
    };

    partial void OnVolumeChanged(double value)
    {
        if (_player is not null)
            _player.Volume = value;
        try
        {
            var dir = Path.GetDirectoryName(VolumeFilePath);
            if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(VolumeFilePath, value.ToString(CultureInfo.InvariantCulture));
        }
        catch { }
    }

    partial void OnPlayModeChanged(PlayMode value)
    {
        OnPropertyChanged(nameof(PlayModeIcon));
#if ANDROID
        ApplyPlayModeToExoPlayer();
#endif
        try
        {
            var dir = Path.GetDirectoryName(PlayModeFilePath);
            if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(PlayModeFilePath, value.ToString());
        }
        catch { }
    }

#if ANDROID
    private void ApplyPlayModeToExoPlayer()
    {
        if (_exoPlayer is null) return;
        try
        {
            _exoPlayer.RepeatMode = PlayMode == PlayMode.SingleLoop ? 1 : 2;
            _exoPlayer.ShuffleModeEnabled = PlayMode == PlayMode.Random;
            Android.Util.Log.Info("YinYan", $"ApplyPlayModeToExoPlayer: mode={PlayMode}, repeat={_exoPlayer.RepeatMode}, shuffle={_exoPlayer.ShuffleModeEnabled}");
        }
        catch (Exception ex)
        {
            Android.Util.Log.Info("YinYan", $"ApplyPlayModeToExoPlayer failed: {ex.Message}");
        }
    }
#endif

#if ANDROID
    partial void OnIsPlayingChanged(bool value)
    {
        if (Current is null) return;
        MediaNotificationManager.Instance.UpdatePlaybackState(
            value, (long)(PositionSeconds * 1000), (long)(DurationSeconds * 1000));
    }
#endif


    public void AttachPlayer(MediaElement player)
    {
        // 同一个元素重复调用直接返回：否则事件会被重复绑定，MediaEnded 等会被触发多次
        if (ReferenceEquals(_player, player)) return;

        if (_player is not null)
        {
            try { _player.Stop(); } catch { }
            UnbindPlayerEvents(_player);
        }

        _player = player;

#if ANDROID
        // Reflection is required to access MediaElementHandler's internal MediaManager,
        // which holds the ExoPlayer and Media3 MediaSession instances.
        // CommunityToolkit.Maui.MediaElement does not expose these as public API.
        // If a toolkit upgrade breaks this, update the member names below.
        // Failure degrades gracefully: notification next/prev buttons won't show,
        // but core playback via MediaElement continues to work.
        try
        {
            if (player.Handler is MediaElementHandler meh)
            {
                var mmProp = typeof(MediaElementHandler).GetProperty("MediaManager",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (mmProp?.GetValue(meh) is { } mm)
                {
                    var playerProp = mm.GetType().GetProperty("Player",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    _exoPlayer = playerProp?.GetValue(mm) as IPlayer;
                    Android.Util.Log.Info("YinYan", $"AttachPlayer: _exoPlayer = {(_exoPlayer is null ? "null" : "OK")}, type={_exoPlayer?.GetType().FullName}");

                    var sessionField = mm.GetType().GetField("session",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (sessionField?.GetValue(mm) is { } sessionObj && _exoPlayer is not null)
                    {
                        var onNext = new Action(() => MainThread.BeginInvokeOnMainThread(async () => { try { await NextAsync(); } catch { } }));
                        var onPrev = new Action(() => MainThread.BeginInvokeOnMainThread(async () => { try { await PreviousAsync(); } catch { } }));
                        _forwardingPlayer = new CustomForwardingPlayer(_exoPlayer, onNext, onPrev);

                        var setPlayerMethod = sessionObj.GetType().GetMethod("set_Player",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        setPlayerMethod?.Invoke(sessionObj, new object[] { _forwardingPlayer });
                        Android.Util.Log.Info("YinYan", $"AttachPlayer: ForwardingPlayer set on session, setPlayer={setPlayerMethod?.Name}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Android.Util.Log.Error("YinYan", $"AttachPlayer: reflection failed, degrading without ExoPlayer/ForwardingPlayer: {ex.Message}");
        }
#endif

        player.Volume = Volume;
        player.MediaOpened += OnMediaOpened;
        player.StateChanged += OnStateChanged;
        player.MediaFailed += OnMediaFailed;
        player.MediaEnded += OnMediaEnded;

        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = new CancellationTokenSource();
        var cts = _timerCts;
        Device.StartTimer(TimeSpan.FromMilliseconds(500), () =>
        {
            if (cts.IsCancellationRequested) return false;
            if (_player is not null)
            {
                try
                {
                    PositionSeconds = _player.Position.TotalSeconds;
                    var dur = _player.Duration.TotalSeconds;
                    if (dur > 1)
                    {
                        DurationSeconds = dur;
                    }
#if ANDROID
                    if (_exoPlayer is not null && _queue.Count > 1 && IsPlaying && PlayMode != PlayMode.SingleLoop)
                    {
                        var pos = _exoPlayer.CurrentPosition / 1000.0;
                        if (_lastPosSec > 1 && pos < 0.3 && _lastPosSec > DurationSeconds - 1.5)
                        {
                            Android.Util.Log.Info("YinYan", $"Timer: loop detected, pos={pos:F1}, last={_lastPosSec:F1}, dur={DurationSeconds:F1}, advancing");
                            _ = NextAsync();
                        }
                        _lastPosSec = pos;
                    }
#endif
                }
                catch { }
            }
            return !cts.IsCancellationRequested;
        });

#if ANDROID
        MediaNotificationManager.Instance.Init();
        MediaNotificationManager.Instance.PlayAction = () => MainThread.BeginInvokeOnMainThread(TogglePlayPause);
        MediaNotificationManager.Instance.PauseAction = () => MainThread.BeginInvokeOnMainThread(TogglePlayPause);
        MediaNotificationManager.Instance.NextAction = () => MainThread.BeginInvokeOnMainThread(async () => { try { await NextAsync(); } catch { } });
        MediaNotificationManager.Instance.PreviousAction = () => MainThread.BeginInvokeOnMainThread(async () => { try { await PreviousAsync(); } catch { } });
        MediaNotificationManager.Instance.StopAction = () => MainThread.BeginInvokeOnMainThread(() => { try { _player?.Pause(); } catch { } });
        MediaNotificationManager.Instance.SeekAction = (posMs) => MainThread.BeginInvokeOnMainThread(() => SeekTo(posMs / 1000.0));
#endif
    }

    /// <summary>
    /// 解绑 MediaElement 事件、停止进度定时器，并释放对播放器与 ExoPlayer 的引用。
    /// 只在播放器确实不再需要时调用（例如应用退出）；导航离开页面时不能调用，因为播放要继续。
    /// 调用后仍可再次 <see cref="AttachPlayer"/> 重新接管。
    /// </summary>
    public void DetachPlayer()
    {
        _timerCts?.Cancel();
        _timerCts?.Dispose();
        _timerCts = null;

        if (_player is not null)
        {
            UnbindPlayerEvents(_player);
            _player = null;
        }

        _mediaReady = false;
        _playPending = false;
#if ANDROID
        _forwardingPlayer = null;
        _exoPlayer = null;
        _lastPosSec = 0;
#endif
        LogInfo("DetachPlayer: 已解绑事件并停止进度定时器");
    }

    private void UnbindPlayerEvents(MediaElement player)
    {
        player.MediaOpened -= OnMediaOpened;
        player.StateChanged -= OnStateChanged;
        player.MediaFailed -= OnMediaFailed;
        player.MediaEnded -= OnMediaEnded;
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        _mediaReady = true;
        // Windows 平台切歌自动播放的关键：Source 切换时紧跟的 Play() 会丢，这里在新源打开后补放。
        // 若已在播放（其他平台/首次播放），Play() 是幂等的，无副作用。
        if (_playPending)
        {
            _playPending = false;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { _player?.Play(); } catch { }
            });
        }
        try
        {
            var dur = _player?.Duration.TotalSeconds ?? 0;
            if (dur > 0)
            {
                DurationSeconds = dur;
                if (Current is not null)
                    // 与进度条显示同口径：向下取整，避免列表 3:01 / 播放页 3:00 的 1 秒偏差
                    Current.DurationSeconds = (int)dur;
            }
        }
        catch { }
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        IsPlaying = e.NewState == MediaElementState.Playing;
        if (e.NewState == MediaElementState.Playing)
        {
            _mediaReady = true;
            _playPending = false;
            try
            {
                var dur = _player?.Duration.TotalSeconds ?? 0;
                if (dur > 1)
                {
                    DurationSeconds = dur;
                    if (Current is not null)
                        // 与进度条显示同口径：向下取整
                        Current.DurationSeconds = (int)dur;
                }
            }
            catch { }
        }
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        IsPlaying = false;
        _mediaReady = false;
        _playPending = false;
        ErrorMessage = e.ErrorMessage;
        // 之前这里不落日志，导致"点了没反应"完全无从排查（ErrorMessage 也没在 UI 上显示）
        LogWarn($"MediaFailed: {e.ErrorMessage}");
    }

    // async void 事件处理器：异常若逃逸会直接崩溃应用，必须在这里兜住
    private async void OnMediaEnded(object? sender, EventArgs e)
    {
        try
        {
            await HandleMediaEndedAsync();
        }
        catch (Exception ex)
        {
            LogWarn($"OnMediaEnded failed: {ex.Message}");
        }
    }

    private async Task HandleMediaEndedAsync()
    {
        var elapsedSincePlayAt = DateTime.UtcNow - _lastPlayAtTime;
        if (elapsedSincePlayAt < TimeSpan.FromSeconds(1))
        {
            LogWarn($"HandleMediaEndedAsync: spurious MediaEnded {elapsedSincePlayAt.TotalMilliseconds:F0}ms after PlayAt, recovering");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try { _player?.SeekTo(TimeSpan.Zero, CancellationToken.None); _player?.Play(); } catch { }
            });
            return;
        }
        _mediaReady = false;
        LogInfo($"HandleMediaEndedAsync: PlayMode={PlayMode}, _index={_index}, queueCount={_queue.Count}");

        switch (PlayMode)
        {
            case PlayMode.SingleLoop:
                if (Current is not null)
                    await MainThread.InvokeOnMainThreadAsync(() => PlayAt(_index));
                break;
            default:
                await NextAsync();
                break;
        }
    }


    public void PlayQueue(IReadOnlyList<SongDto> songs, int startIndex, string? sourceName = null)
    {
        if (songs.Count == 0) return;
        _queue = [.. songs];
        if (sourceName is not null) SourceName = sourceName;
        PlayAt(startIndex);
    }

    /// <summary>
    /// 播放指定歌曲。ATL 解析失败（时长未知）的歌曲自动跳过：
    /// skipStep=+1 时向后跳（下一首/点选），-1 时向前跳（上一首）；整队都无效则放弃播放。
    /// </summary>
    public void PlayAt(int index, int skipStep = 1)
    {
        if (_queue.Count == 0) return;
        index = ((index % _queue.Count) + _queue.Count) % _queue.Count;

        // 跳过无时长的歌（时长为 0 = 服务端 ATL 解析失败，播放必然无声）
        var visited = 0;
        while (_queue[index].DurationSeconds <= 0 && visited < _queue.Count)
        {
            LogWarn($"PlayAt: 跳过无时长的歌曲 index={index} ({_queue[index].Title})");
            index = ((index + skipStep) % _queue.Count + _queue.Count) % _queue.Count;
            visited++;
        }
        if (visited >= _queue.Count)
        {
            LogWarn($"PlayAt: 队列中所有歌曲均无时长，放弃播放");
            return;
        }

        _lastPlayAtTime = DateTime.UtcNow;
        LogInfo($"PlayAt: called index={index}, queueCount={_queue.Count}");
        _index = index;

        var song = _queue[index];
        Current = song;
        PositionSeconds = 0;
        _mediaReady = false;
        DurationSeconds = song.DurationSeconds > 0 ? song.DurationSeconds : 1;
        ErrorMessage = string.Empty;

        if (_player is null)
        {
            ErrorMessage = "播放器未初始化";
            _playPending = false;
            LogWarn("PlayAt: _player 为空（MediaElement 未绑定），未开始播放");
            return;
        }

        try
        {
            _player.MetadataTitle = song.Title;
            _player.MetadataArtist = song.ArtistName;
            if (!string.IsNullOrEmpty(song.CoverUrl))
                _player.MetadataArtworkUrl = ApiConfig.Absolute(song.CoverUrl);
            var url = ApiConfig.Absolute(song.AudioUrl);
#if ANDROID
            MediaNotificationManager.Instance.UpdateMetadata(
                song.Title, song.ArtistName, (long)(DurationSeconds * 1000),
                string.IsNullOrEmpty(song.CoverUrl) ? null : ApiConfig.Absolute(song.CoverUrl));
            MediaNotificationManager.Instance.UpdatePlaybackState(
                true, 0, (long)(DurationSeconds * 1000));
#endif
            System.Diagnostics.Debug.WriteLine($"[播放] {url}");
            _playPending = true;   // 播放意图：若紧随的 Play() 在切源时被 Windows 丢弃，MediaOpened 后会补放
            _player.Source = new Uri(url);
            _player.Play();
#if ANDROID
            if (_exoPlayer is not null)
            {
                try
                {
                    _exoPlayer.RepeatMode = PlayMode == PlayMode.SingleLoop ? 1 : 2;
                    _exoPlayer.ShuffleModeEnabled = PlayMode == PlayMode.Random;
                    Android.Util.Log.Info("YinYan", $"PlayAt: RepeatMode={_exoPlayer.RepeatMode}, Shuffle={_exoPlayer.ShuffleModeEnabled}");
                }
                catch { }
            }
#endif
            _ = api.RecordPlayAsync(song.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[播放异常] {ex.Message}");
        }
    }

    public async Task NextAsync()
    {
        if (_queue.Count == 0) return;
        LogInfo($"NextAsync: _index={_index}, queueCount={_queue.Count}, PlayMode={PlayMode}");
        int next;
        switch (PlayMode)
        {
            case PlayMode.Random:
                if (_queue.Count == 1) { await ReplayCurrentAsync(); return; }
                do { next = _rng.Next(_queue.Count); } while (next == _index);
                break;
            case PlayMode.Sequential:
                next = _index + 1;
                if (next >= _queue.Count) return;
                break;
            default:
                next = (_index + 1) % _queue.Count;
                break;
        }
        await MainThread.InvokeOnMainThreadAsync(() => PlayAt(next, skipStep: 1));
    }

    public async Task PreviousAsync()
    {
        if (_queue.Count == 0) return;
        int prev;
        switch (PlayMode)
        {
            case PlayMode.Random:
                if (_queue.Count == 1) { await ReplayCurrentAsync(); return; }
                do { prev = _rng.Next(_queue.Count); } while (prev == _index);
                break;
            case PlayMode.Sequential:
                prev = _index - 1;
                if (prev < 0) return;
                break;
            default:
                prev = (_index - 1 + _queue.Count) % _queue.Count;
                break;
        }
        await MainThread.InvokeOnMainThreadAsync(() => PlayAt(prev, skipStep: -1));
    }

    private async Task ReplayCurrentAsync()
    {
        if (Current is null) return;
        await MainThread.InvokeOnMainThreadAsync(() => PlayAt(_index));
    }

    public void TogglePlayPause()
    {
        if (_player is null || Current is null) return;
        try
        {
            if (IsPlaying) { _playPending = false; _player.Pause(); }  // 用户手动暂停，撤销自动补放
            else _player.Play();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Toggle异常] {ex.Message}");
        }
    }

    public void SeekTo(double seconds)
    {
        if (_player is null) return;
        try
        {
            var clamped = Math.Max(0, Math.Min(seconds, DurationSeconds));
            PositionSeconds = clamped;
            _player.SeekTo(TimeSpan.FromSeconds(clamped), CancellationToken.None);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Seek异常] {ex.Message}");
        }
    }


	public void CyclePlayMode()
	{
		PlayMode = PlayMode switch
		{
			PlayMode.Sequential => PlayMode.ListLoop,
			PlayMode.ListLoop => PlayMode.SingleLoop,
			PlayMode.SingleLoop => PlayMode.Random,
			_ => PlayMode.Sequential
		};
	}

	public void InsertNext(SongDto song)
	{
		if (_queue.Count == 0 || _index < 0)
		{
			_queue = [song];
			PlayAt(0);
			return;
		}
		_queue.Insert(_index + 1, song);

	}

	public void SaveState()
	{
		try
		{
			if (_queue.Count == 0 || _index < 0) return;
			var state = new PlayerState
			{
				Queue = _queue,
				Index = _index,
				PositionSeconds = PositionSeconds,
				SourceName = SourceName,
				PlayMode = PlayMode
			};
			var dir = Path.GetDirectoryName(StateFilePath);
			if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
			var json = JsonSerializer.Serialize(state);
			File.WriteAllText(StateFilePath, json);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[保存状态异常] {ex.Message}");
		}
	}

	public async Task RestoreStateAsync()
	{
		try
		{
			if (!File.Exists(StateFilePath)) return;
			var json = File.ReadAllText(StateFilePath);
			var state = JsonSerializer.Deserialize<PlayerState>(json);
			if (state is null || state.Queue.Count == 0) return;

			_queue = [.. state.Queue];
			_index = state.Index;
			Current = _queue[_index];
			PositionSeconds = state.PositionSeconds;
			SourceName = state.SourceName;
			PlayMode = state.PlayMode;
			DurationSeconds = Current.DurationSeconds > 0 ? Current.DurationSeconds : 1;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[恢复状态异常] {ex.Message}");
		}
		await Task.CompletedTask;
	}

	/// <summary>
	/// 每次程序启动（全新进程）时调用：清空上一次会话残留的播放状态，
	/// 不再自动恢复上次歌曲。这样迷你播放条会保持隐藏，由用户自己从任意列表重新点歌。
	/// 同时删除磁盘上的 player_state.json，避免下次误恢复。
	/// </summary>
	public void ResetForNewSession()
	{
		_queue = [];
		_index = -1;
		Current = null;
		IsPlaying = false;
		PositionSeconds = 0;
		DurationSeconds = 1;
		_mediaReady = false;
		_playPending = false;
		try
		{
			if (File.Exists(StateFilePath)) File.Delete(StateFilePath);
		}
		catch { }
	}

	private class PlayerState
	{
		public List<SongDto> Queue { get; set; } = [];
		public int Index { get; set; }
		public double PositionSeconds { get; set; }
		public string SourceName { get; set; } = string.Empty;
		public PlayMode PlayMode { get; set; }
	}
}




