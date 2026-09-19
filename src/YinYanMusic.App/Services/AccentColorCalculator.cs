namespace YinYanMusic.App.Services;

/// <summary>
/// 从像素里挑一个「主色」——跨平台共用，平台代码只负责把图解码成 RGBA 像素。
///
/// 思路：按色相分 12 个桶（每 30 度一桶）做直方图，权重里给饱和度和中等亮度加权，
/// 这样既不会把大片黑底/白底当成主色，也不会被少量高饱和噪点带跑。
/// 最后把结果统一拉到「够鲜艳但不刺眼」的区间，适合直接当背景色用。
/// </summary>
public static class AccentColorCalculator
{
    private const int HueBuckets = 12;

    /// <param name="rgba">按行排列的 RGBA 像素，长度 = width * height * 4。</param>
    public static Color? FromPixels(byte[] rgba, int width, int height)
    {
        if (rgba.Length < width * height * 4 || width <= 0 || height <= 0) return null;

        var weight = new double[HueBuckets];
        var sumR = new double[HueBuckets];
        var sumG = new double[HueBuckets];
        var sumB = new double[HueBuckets];
        var count = new int[HueBuckets];
        var sampled = 0;

        for (var i = 0; i < rgba.Length; i += 4)
        {
            var a = rgba[i + 3];
            if (a < 125) continue;                       // 半透明/全透明像素不参与

            var r = rgba[i] / 255.0;
            var g = rgba[i + 1] / 255.0;
            var b = rgba[i + 2] / 255.0;

            RgbToHsl(r, g, b, out var h, out var s, out var l);

            // 太黑（阴影）和太白（高光/白底）都不携带“色彩”信息，直接跳过
            if (l < 0.12 || l > 0.92) continue;
            if (s < 0.08) continue;                      // 灰阶像素没有色相可言

            var bucket = (int)(h * HueBuckets) % HueBuckets;
            // 饱和度越高越像“主题色”；亮度越接近中间越可能是主体而非阴影
            var w = (0.35 + s * 1.65) * (1.0 - Math.Abs(l - 0.5) * 1.2);
            if (w <= 0) continue;

            weight[bucket] += w;
            sumR[bucket] += r * w;
            sumG[bucket] += g * w;
            sumB[bucket] += b * w;
            count[bucket]++;
            sampled++;
        }

        if (sampled == 0) return null;

        var best = 0;
        for (var i = 1; i < HueBuckets; i++)
            if (weight[i] > weight[best]) best = i;
        if (count[best] == 0 || weight[best] <= 0) return null;

        var ar = sumR[best] / weight[best];
        var ag = sumG[best] / weight[best];
        var ab = sumB[best] / weight[best];

        RgbToHsl(ar, ag, ab, out var bh, out var bs, out var bl);
        // 统一观感：饱和度拉到够看得出来，亮度压在背景常用的中间段
        bs = Math.Clamp(bs * 1.25, 0.35, 0.85);
        bl = Math.Clamp(bl, 0.30, 0.62);
        HslToRgb(bh, bs, bl, out var fr, out var fg, out var fb);

        return Color.FromRgb(
            (byte)Math.Clamp(fr * 255, 0, 255),
            (byte)Math.Clamp(fg * 255, 0, 255),
            (byte)Math.Clamp(fb * 255, 0, 255));
    }

    private static void RgbToHsl(double r, double g, double b, out double h, out double s, out double l)
    {
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        l = (max + min) / 2.0;

        if (Math.Abs(max - min) < 1e-6) { h = 0; s = 0; return; }

        var d = max - min;
        s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

        if (Math.Abs(max - r) < 1e-6) h = (g - b) / d + (g < b ? 6 : 0);
        else if (Math.Abs(max - g) < 1e-6) h = (b - r) / d + 2;
        else h = (r - g) / d + 4;

        h /= 6.0;
    }

    private static void HslToRgb(double h, double s, double l, out double r, out double g, out double b)
    {
        if (s <= 1e-6) { r = g = b = l; return; }

        double Hue2Rgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
            return p;
        }

        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        r = Hue2Rgb(p, q, h + 1.0 / 3.0);
        g = Hue2Rgb(p, q, h);
        b = Hue2Rgb(p, q, h - 1.0 / 3.0);
    }
}
