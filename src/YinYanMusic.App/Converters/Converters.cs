using System.Globalization;
using YinYanMusic.Core.Dtos;
using YinYanMusic.App.Services;

namespace YinYanMusic.App.Converters;

public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrWhiteSpace(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class SecondsToTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double seconds)
        {
            if (value is int i) seconds = i;
            else return "0:00";
        }
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class BoolToPlayPauseConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "\uE034" : "\uE037";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class CollectedTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "已收藏 ✓" : "☆ 收藏";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class AbsoluteUrlConverter : IValueConverter
{
    /// <summary>
    /// 统一委托给 <see cref="ImageSourceFactory"/> —— 判断逻辑只此一份。
    /// 历史上 XAML 转换器与页面代码后置各写了一套，导致"列表封面能显示、
    /// 长按预览却空白"这类不一致（本地曲库上线后真机踩到）。
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ImageSourceFactory.From(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public class NullToFallbackConverter : IValueConverter
{
    public string Fallback { get; set; } = string.Empty;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s)) return s;
        return parameter as string ?? Fallback;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// 专辑副标题：专辑名下方那一行 —— 「2024 年 · 1 首」；**没有发行日期就只显示「1 首」**。
/// <para>
/// ⚠️ 别用 <c>StringFormat='{0:yyyy} 年'</c> 拼这一段：值为 null 时格式串里的字面量照旧输出，
/// 界面上会留一个孤零零的「 年」—— 而库里没有发行日期的专辑并不少。这里两段（日期 / 首数）
/// 任何一段缺失都要能优雅退化，用转换器最省事。
/// </para>
/// </summary>
public class AlbumSubtitleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is AlbumDto album
            ? (album.ReleaseDate is { } d
                ? $"{d.Year} 年 · {album.TrackCount} 首"
                : $"{album.TrackCount} 首")
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// 取名字的**首个字符**，给"没有头像时"的字母头像用（用户取昵称、歌手取艺名）。
/// <para>
/// 用 <see cref="StringInfo.GetNextTextElement(string)"/> 而不是 <c>s[0]</c>：
/// 后者会把代理对（emoji、部分生僻字）劈成半个字符，界面上就是一个乱码方块。
/// 中日文首字原样返回；拉丁字母转大写，视觉上更像头像。
/// </para>
/// </summary>
public class InitialConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return "?";

        var first = StringInfo.GetNextTextElement(s.Trim());
        return first.ToUpper(culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// 音量 0.0–1.0 → 百分比文案（"72%"），给播放页音量浮窗的数字标签用。
/// 底层刻度就是 0–1（MediaElement / AudioManager 归一化），0–100 只是展示层换算，
/// 所以滑条保持绑定 Player.Volume 不动，这里只做 ×100 取整显示。
/// </summary>
public class VolumePercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value is double d ? d : 0;
        return $"{Math.Round(Math.Clamp(v, 0, 1) * 100)}%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
