namespace YinYanMusic.App.Services;

/// <summary>
/// 系统媒体音量桥接。值域 0~1（占最大音量的比例）。
/// Android 用官方 <c>AudioManager</c>（STREAM_MUSIC）实现；其他平台用
/// <see cref="NoopSystemVolumeService"/> 保持 App 内软件音量。
/// </summary>
public interface ISystemVolumeService
{
    /// <summary>当前系统媒体音量比例（0~1）。</summary>
    double Volume { get; }

    /// <summary>系统媒体音量变化事件（参数为新的 0~1 比例）。</summary>
    event EventHandler<double>? VolumeChanged;

    /// <summary>设置系统媒体音量（0~1）。</summary>
    void SetVolume(double value);
}