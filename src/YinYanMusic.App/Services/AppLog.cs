namespace YinYanMusic.App.Services;

/// <summary>
/// 统一的轻量日志出口（V2.7 起新增的服务用它）。
///
/// <para>历史上各文件的日志各写一套（<c>PlayerService</c> 里是内联的
/// <c>#if ANDROID Android.Util.Log.Info("YinYan", …)</c>），新代码再照抄就越来越散。
/// 这里收口一个最小实现：Android 走 logcat（真机排查唯一可用的通道），
/// 其余平台走 <see cref="System.Diagnostics.Debug"/>。</para>
///
/// <para>刻意不引日志框架：本项目日志只服务于现场排查，输出到 logcat 已经够用。</para>
/// </summary>
public static class AppLog
{
    private const string Tag = "YinYan";

    /// <summary>
    /// 可选的**文件日志**（Windows / iOS 排障用）。
    ///
    /// <para>非 Android 平台原本只有 <see cref="System.Diagnostics.Debug"/> 输出，脱离调试器就什么都看不到
    /// —— Windows 端反馈的问题（如"本地音乐点了不播"）完全无从下手。</para>
    ///
    /// <para>所以加了这条旁路：环境变量 <c>YINYAN_LOG_FILE</c> 指向一个路径时，日志**同时**追加写进该文件。
    /// 不设变量时与原来完全一致（零开销、零副作用），出问题只要带上变量启动一次就能拿到现场。</para>
    /// </summary>
    private static readonly string? LogFilePath = ResolveLogFilePath();

    private static string? ResolveLogFilePath()
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("YINYAN_LOG_FILE");
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
        catch
        {
            return null;
        }
    }

    private static void Emit(string level, string message)
    {
#if ANDROID
        var text = message;
#else
        var text = $"[{level}] {message}";
#endif

        var file = LogFilePath;
        if (file is not null)
        {
            try
            {
                File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}\n");
            }
            catch
            {
                // 写日志失败绝不能影响业务
            }
        }

#if ANDROID
        switch (level)
        {
            case "W": Android.Util.Log.Warn(Tag, text); break;
            case "E": Android.Util.Log.Error(Tag, text); break;
            default: Android.Util.Log.Info(Tag, text); break;
        }
#else
        System.Diagnostics.Debug.WriteLine(text);
#endif
    }

    public static void Info(string message) => Emit("I", message);

    public static void Warn(string message) => Emit("W", message);

    public static void Error(string message, Exception? ex = null) =>
        Emit("E", ex is null ? message : $"{message}: {ex.Message}");
}
