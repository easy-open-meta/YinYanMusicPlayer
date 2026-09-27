using Android.App;
using Android.Content;
using Android.Database;
using Android.Media;
using Android.OS;
using Android.Provider;

namespace YinYanMusic.App.Services;

/// <summary>
/// Android 系统媒体音量桥接：基于官方 <c>AudioManager</c>（STREAM_MUSIC）读取/设置音量，
/// 用 <c>ContentObserver</c> 监听 <c>Settings.System.VOLUME_MUSIC</c> 的变化，
/// 实现系统音量与 App 音量条的双向同步。
/// </summary>
public sealed class AndroidSystemVolumeService : ISystemVolumeService, IDisposable
{
    private readonly AudioManager _audioManager;
    private readonly ContentResolver _contentResolver;
    private readonly VolumeObserver _observer;
    private bool _disposed;

    public AndroidSystemVolumeService()
    {
        var context = Android.App.Application.Context;
        _audioManager = context.GetSystemService(Context.AudioService) as AudioManager
            ?? throw new InvalidOperationException("AudioManager 不可用");
        _contentResolver = context.ContentResolver;
        _observer = new VolumeObserver(new Handler(Looper.MainLooper), this);

        var uri = Settings.System.GetUriFor(Settings.System.VolumeMusic);
        _contentResolver.RegisterContentObserver(uri, true, _observer);
    }

    /// <inheritdoc />
    public double Volume
    {
        get
        {
            var max = _audioManager.GetStreamMaxVolume(Android.Media.Stream.Music);
            return max <= 0 ? 0 : (double)_audioManager.GetStreamVolume(Android.Media.Stream.Music) / max;
        }
    }

    /// <inheritdoc />
    public event EventHandler<double>? VolumeChanged;

    /// <inheritdoc />
    public void SetVolume(double value)
    {
        var max = _audioManager.GetStreamMaxVolume(Android.Media.Stream.Music);
        var index = (int)Math.Round(Math.Clamp(value, 0, 1) * max);
        _audioManager.SetStreamVolume(Android.Media.Stream.Music, index, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _contentResolver.UnregisterContentObserver(_observer); } catch { }
    }

    private void OnSystemVolumeChanged() => VolumeChanged?.Invoke(this, Volume);

    private sealed class VolumeObserver(Handler handler, AndroidSystemVolumeService owner) : ContentObserver(handler)
    {
        public override void OnChange(bool selfChange) => owner.OnSystemVolumeChanged();
    }
}