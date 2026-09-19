namespace YinYanMusic.App.Services;

/// <summary>
/// API 地址解析 + 拼接。优先级（高→低，详见设计文档 v4.x §3）：
///   ① 应用内设置（设置页写入 Preferences / Settings，运行时改；最高）
///   ② 环境变量 YINYAN_API_BASEURL（Windows/服务器场景；启动时读）
///   ③ 配置文件 api.json（设备私有目录或 %LOCALAPPDATA%）
///   ④ 平台默认值（编译期常量，仅兜底）
///
/// ⚠️ <b>不能直接改 BaseUrl</b>：HttpClient 在 DI 注册时把 BaseAddress 定死了，
/// 改完也不生效。正确的做法是改完地址后所有请求<b>不走</b> HttpClient.BaseAddress，
/// 而由本类的 <see cref="Absolute(string?)"/> 在每次调用时拼绝对地址 —— 见 <see cref="MusicApiService"/>。
/// <see cref="SetBaseUrl(string?)"/> 只更新内存中的覆盖值；持久化由 <see cref="ApiConfigStore"/> 完成。
/// </summary>
public static class ApiConfig
{
    /// <summary>平台默认值 —— 真正的最终兜底，应只在文件/环境变量/设置都拿不到时生效。</summary>
    /// <remarks>
    /// **发布版（Release）：全平台统一走外网域名 + HTTPS** —— TLS 由 Nginx 终结，API 进程内不自建 HTTPS。
    /// **调试版（Debug）：Windows 连本机 `http://localhost:5116`，Android 仍走外网域名。**
    ///
    /// 调试期为什么要分叉：本机 `dotnet run` 起的是 localhost，Windows 上直接连即可；
    /// 而 Android 的 `localhost` 是**设备自己**，写 localhost 必然连不上，所以调试也走域名。
    /// Android 要连局域网明文后端：在设置页手填（Debug 包的 `network_security_config.xml`
    /// 有 `debug-overrides`，http 全开）。
    ///
    /// ⚠️ 该域名必须配**公网可信证书**：Android 与 Windows 都不信任自签名证书，会直接连不上
    /// （表现为所有请求失败，且没有明显报错）。
    ///
    /// Windows 安装包形态下，安装向导填的地址会写成机器级环境变量 `YINYAN_API_BASEURL`，
    /// 优先级②会盖掉这里，所以装在没有本地 API 的机器上也能正常工作。
    /// </remarks>
    public static string DefaultBaseUrl { get; } =
#if DEBUG
        // 调试：Windows 连本机 API；Android 的 localhost 是设备自己，只能继续走域名。
        DeviceInfo.Platform == DevicePlatform.Android
            ? "https://yinyan.oscode.top"
            : "http://localhost:5116";
#else
        // 发布：全平台统一外网域名。
        "https://yinyan.oscode.top";
#endif

    /// <summary>
    /// 当前生效的 BaseUrl。来源可能是 ①设置 ②环境变量 ③配置文件 ④默认值，由 <see cref="ApiConfigStore"/> 加载。
    /// 只读 —— 调用 <see cref="SetBaseUrl(string?)"/> 修改。
    /// </summary>
    public static string BaseUrl { get; private set; } = DefaultBaseUrl;

    /// <summary>更新当前 BaseUrl（不持久化）。设置页保存后会调用一次。</summary>
    public static void SetBaseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            BaseUrl = DefaultBaseUrl;
            return;
        }
        BaseUrl = url.TrimEnd('/');
    }

    /// <summary>
    /// 把后端返回的相对路径（<c>"/media/audio/x.flac"</c> / <c>"api/songs/123"</c> 等）拼成绝对 URL。
    /// 已是 <c>http(s)://</c> 开头的直接返回。
    /// </summary>
    public static string Absolute(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;
        return $"{BaseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
    }
}