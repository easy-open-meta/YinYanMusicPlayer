using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Java.Nio;

namespace YinYanMusic.App.Services;

/// <summary>
/// Android 图片内存缓存：基于官方 <c>Android.Util.LruCache</c>，容量按应用可用内存
/// （<c>ActivityManager.MemoryClass</c>）的 1/8 分配，并在 <c>onTrimMemory</c> 时按系统
/// 内存压力分级清理。参考 Android 官方文档「Caching Bitmaps」的推荐做法。
/// 支持缓存两种条目：解码后的 <see cref="Bitmap"/>（亚克力底图等）与图片原始字节
/// （全局封面/头像加载，避免重复下载）。
/// </summary>
public sealed class AndroidBitmapCache
{
    private readonly BitmapLruCache _cache;

    public AndroidBitmapCache()
    {
        var am = (ActivityManager?)Android.App.Application.Context.GetSystemService(Context.ActivityService);
        var memoryClass = am?.MemoryClass ?? 32;   // 单位 MB；拿不到时按 32MB 兜底
        // 低内存设备（go 版/入门机）用更小的缓存比例，避免缓存抢占本就不宽裕的堆内存
        var divisor = am?.IsLowRamDevice == true ? 16 : 8;
        _cache = new BitmapLruCache(memoryClass * 1024 * 1024 / divisor);
    }

    public Bitmap? GetBitmap(string key) => _cache.Get(new Java.Lang.String(key)) as Bitmap;

    public void PutBitmap(string key, Bitmap bitmap) => _cache.Put(new Java.Lang.String(key), bitmap);

    public byte[]? GetBytes(string key)
    {
        var value = _cache.Get(new Java.Lang.String(key));
        return value is ByteBuffer buf ? ReadBuffer(buf) : null;
    }

    public void PutBytes(string key, byte[] bytes) => _cache.Put(new Java.Lang.String(key), ByteBuffer.Wrap(bytes));

    /// <summary>
    /// 系统内存压力回调（MainApplication.OnTrimMemory 转发）：
    /// 临界级别清空全部缓存，较低压力时先裁剪到一半。
    /// </summary>
    public void OnTrimMemory(TrimMemory level)
    {
        if (level >= TrimMemory.RunningCritical)
        {
            _cache.EvictAll();
        }
        else if (level >= TrimMemory.RunningLow || level >= TrimMemory.UiHidden)
        {
            _cache.TrimToSize(_cache.MaxSize() / 2);
        }
    }

    private static byte[] ReadBuffer(ByteBuffer buffer)
    {
        buffer.Clear();
        var result = new byte[buffer.Remaining()];
        buffer.Get(result);
        return result;
    }

    /// <summary>按字节计量的 LruCache：位图占多少内存就算多少，避免只按条目数限制导致 OOM。</summary>
    private sealed class BitmapLruCache : LruCache
    {
        public BitmapLruCache(int maxSize) : base(maxSize) { }

        protected override int SizeOf(Java.Lang.Object key, Java.Lang.Object value)
        {
            if (value is Bitmap bmp) return bmp.ByteCount;
            if (value is ByteBuffer buf) return buf.Capacity();
            return 1;
        }
    }
}