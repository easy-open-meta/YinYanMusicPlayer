namespace YinYanMusic.App.Services;

/// <summary>
/// 非 Android 平台的空实现：不桥接系统音量，App 音量仍由 <see cref="PlayerService"/> 自己管理。
/// </summary>
public sealed class NoopSystemVolumeService : ISystemVolumeService
{
    public static readonly NoopSystemVolumeService Shared = new();

    public double Volume => 1.0;

    public event EventHandler<double>? VolumeChanged { add { } remove { } }

    public void SetVolume(double value) { }
}