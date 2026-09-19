namespace YinYanMusic.App.Services;

/// <summary>
/// 亚克力底图的模糊算法：分离式箱式模糊（先横后竖，两趟）。
/// 输入是 RGBA 顺序、4 字节/像素的数组，原地处理。
///
/// 为什么自己糊像素、而不是给 Image 挂平台 RenderEffect：
/// RenderEffect 模糊的是「视图已经绘制出来的内容」——图片还没解码完、或视图处在
/// IsVisible=False 的容器里时，它会把视图渲染成**纯黑**（真机实测：浮窗底图区域
/// 亮度只有 20/255，整个浮窗其实是一块靠遮罩撑起来的纯色）。
/// 改成「把封面缩到 40×40 再糊几趟」，得到的是没有细节的柔和色块，
/// 放大铺满后正是磨砂玻璃的观感，且完全不依赖视图生命周期，也不挑系统版本。
/// </summary>
internal static class AcrylicBlur
{
    public static void Apply(byte[] rgba, int width, int height, int radius)
    {
        if (radius <= 0 || width <= 0 || height <= 0 || rgba.Length < width * height * 4) return;

        var src = (byte[])rgba.Clone();
        var dst = new byte[rgba.Length];

        for (var pass = 0; pass < 2; pass++)
        {
            var vertical = pass == 1;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0, n = 0;
                    for (var k = -radius; k <= radius; k++)
                    {
                        // 边缘用钳制取样，避免放大后四周出现变暗的硬边
                        var sx = vertical ? x : Math.Clamp(x + k, 0, width - 1);
                        var sy = vertical ? Math.Clamp(y + k, 0, height - 1) : y;
                        var i = (sy * width + sx) * 4;
                        r += src[i];
                        g += src[i + 1];
                        b += src[i + 2];
                        a += src[i + 3];
                        n++;
                    }
                    var o = (y * width + x) * 4;
                    dst[o] = (byte)(r / n);
                    dst[o + 1] = (byte)(g / n);
                    dst[o + 2] = (byte)(b / n);
                    dst[o + 3] = (byte)(a / n);
                }
            }
            (src, dst) = (dst, src);
        }

        Array.Copy(src, rgba, rgba.Length);
    }
}
