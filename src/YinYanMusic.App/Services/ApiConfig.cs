using YinYanMusic.Core;

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
    ///
    /// <para><b>V2.13 起，本地文件的判定接在这里</b>：白名单前缀（<c>http</c> / <c>https</c> / <c>data:</c>）
    /// <b>或</b> 是设备上的文件（<c>file:</c> / <c>content:</c> / Windows 盘符 / 裸 Unix 绝对路径）
    /// 都原样返回，不再拼 BaseUrl。判据统一走 <see cref="MediaAddress.IsLocalFile"/>，
    /// 与 <c>ImageSourceFactory</c>、<c>LocalFileAccess</c> 共用同一份实现。</para>
    ///
    /// <para><b>为什么要把判据放在这一层（而不是继续在各调用点打补丁）</b>：V2.6 之后同类缺陷出现了五次，
    /// 每次都是"某处忘了处理本地路径"，修一处漏一处；只要 <see cref="Absolute(string?)"/> 本身是干净的，
    /// 任何新增的"取地址 → 拼 BaseUrl"调用都自动安全，不必靠人记得逐处处理。</para>
    ///
    /// <para><b>性能</b>：判据里 <c>/media/...</c> 走前缀短路，**不会**为封面/音频这类绝大多数地址多做
    /// <c>File.Exists</c>；只有不以 <c>/media/</c> 开头的、并以 <c>/</c> 开头的字符串（即 Android 本地曲库的
    /// 裸路径）才会真的去 stat 一次。进度条刷新、切歌等高频路径都不经过这里（它们用的是秒数，不是地址）。</para>
    ///
    /// <para>
    /// <b>Nginx 反代部署兼容（V2.15）</b>：生产环境 Nginx 通常只反代 <c>/api</c>，而音频/封面/歌词
    /// 在库里存的是 <c>/media/audio/...</c>、<c>/media/image/...</c> 相对路径 —— 若让它们原样请求，
    /// Nginx 找不到 <c>/media</c> 静态资源就会 404（本地开发有 Vite 的 <c>/media</c> 代理所以正常）。
    /// 这里把 <c>/media/...</c> 统一重写为 <c>/api/media/...</c>，走 Nginx 的 <c>/api</c> 反代；
    /// 服务端新增的 <c>api/media</c> 端点从音乐/图片目录返回文件，行为与静态托管一致（含 Range）。
    /// </para>
    /// </summary>
    public static string Absolute(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        // ① 已带协议、不需要拼 BaseUrl 的形态。
        //    file:/content: 不在这里 —— 它们交给 ② 与其它本地形态一起判定，
        //    避免"哪些前缀算本地"在两处各维护一份（历史上就是那么分叉的）。
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return url;

        // ② V2.13：设备上的文件原样返回。拼了 BaseUrl 只会得到 https://域名/data/user/0/... 这种
        //    必然 404 的假地址（真机表现：本地歌封面/底图全部加载失败）。
        if (MediaAddress.IsLocalFile(url)) return url;

        if (url.StartsWith("/media/audio/", StringComparison.Ordinal))
            url = "/api/media/audio/" + url["/media/audio/".Length..];
        else if (url.StartsWith("/media/image/", StringComparison.Ordinal))
            url = "/api/media/image/" + url["/media/image/".Length..];

        return $"{BaseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
    }
}