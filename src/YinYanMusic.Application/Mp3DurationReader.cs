using ATL;

namespace YinYanMusic.Application;

/// <summary>
/// 音频时长探测：统一使用 ATL（z440.atl.core）从音频数据逐帧解析，返回 double 秒，失败返回 0。
/// 注意：不要引入第二套时长计算方式（如 TagLib 元数据估算），避免时长口径再次分叉
/// （历史教训：TagLib 估算 + Math.Round 导致列表比播放页多显示 1 秒）。
/// </summary>
public static class Mp3DurationReader
{
    public static double GetDuration(string filePath)
    {
        try
        {
            var track = new Track(filePath);
            return track.DurationMs > 0 ? track.DurationMs / 1000.0 : 0;
        }
        catch
        {
            return 0;
        }
    }
}
