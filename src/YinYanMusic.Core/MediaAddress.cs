namespace YinYanMusic.Core;

/// <summary>
/// 媒体地址形态判定（V2.13 技术债清理的产物）。
///
/// <para><b>为什么存在</b>：V2.6 引入本地音乐库后，「这个地址是后端资源还是设备上的文件」
/// 这个问题在五处独立实现里各自出错 —— 后端相对路径被拼成假 URL 或反过来本地路径被当 URL 加载，
/// 症状都是"封面/音频加载失败"，但位置不同，每次都要单独排查。根治办法只有一条：
/// **这个判断只留一份实现**，客户端所有取址路径都复用它。</para>
///
/// <para><b>为什么放在 Core</b>：这里没有任何 MAUI / 平台依赖（只用到 <see cref="System.IO"/>），
/// 因此可以被单元测试直接覆盖。客户端侧的两个使用者 —— <c>ApiConfig.Absolute</c>（负责拼 BaseUrl）
/// 与 <c>ImageSourceFactory</c>（负责构造 ImageSource）—— 都复用它，
/// 而 MAUI 那侧要靠真机回归验证（Core 不反向引用 App，所以这里不写 cref）。</para>
/// </summary>
public static class MediaAddress
{
    /// <summary>
    /// 后端媒体资源在库里的约定前缀。由 Scanner 写入，形如 <c>/media/audio/相对路径</c>、
    /// <c>/media/image/xxx.jpg</c>。见 <c>YinYanMusic.Scanner.CliOptions</c> 的说明。
    /// </summary>
    public const string MediaPrefix = "/media/";

    /// <summary>
    /// 是不是"设备上的文件"而不是后端 URL。覆盖四种形态：<c>file:</c> / <c>content:</c> 协议、
    /// Windows 盘符路径（<c>C:\...</c>）、Android 的裸 Unix 绝对路径（<c>/data/user/0/&lt;pkg&gt;/files/...</c>）。
    /// 另有一条例外：<c>/media/...</c> 一定是后端资源，直接短路判否（见方法内注释）。
    /// </summary>
    public static bool IsLocalFile(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;

        // ① 显式本地协议：带了前缀就不必再猜。
        //    这里用 "file:" / "content:" 前缀（而不是 "file://"），与 V2.13 之前 ApiConfig 白名单的
        //    判定范围**完全一致** —— file:/C:/x.jpg 这种单斜杠写法也是合法的 file URI，
        //    收窄成双斜杠会把以前能放行的形态重新变成"拼 BaseUrl"。
        if (raw.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("content:", StringComparison.OrdinalIgnoreCase))
            return true;

        // ② 后端资源前缀短路：/media/... 一定是服务端资源，永远不可能是设备上的文件。
        //    这一条是"方案 A 不会引入 stat 抖动"的关键 —— 封面 / 音频 / 歌词绝大多数都是 /media/...，
        //    有这条短路就**不会**为它们多做一次 File.Exists（设计文档 §3.13.3 要求先确认的正是这一点）。
        //    同时堵住一个真实风险：万一磁盘上恰好存在 /media/image/x.jpg 这种同名路径，
        //    没有这条短路就会被误判成本地文件，导致所有在线封面一起挂掉（TC-2.13-03）。
        if (raw.StartsWith(MediaPrefix, StringComparison.Ordinal)) return false;

        // ③ Windows 盘符路径（C:\ 或 C:/）。
        //    不能用 Path.IsPathRooted：后端返回的 "/media/audio/x.flac" 在 Windows 上也算 rooted，
        //    会被误判成本地文件。
        if (raw.Length >= 2
            && char.IsLetter(raw[0])
            && raw[1] == ':'
            && (raw.Length == 2 || raw[2] is '\\' or '/'))
            return true;

        // ④ 裸 Unix 绝对路径（Android 本地曲库的封面落在 FileSystem.AppDataDirectory）。
        //    ⚠️ 不能只凭"以 / 开头"判定 —— 后端的相对路径同样以 / 开头，那样会把在线封面
        //    全当成本地文件、FromFile 全部失败。所以这里用"磁盘上确实有这个文件"作判据：
        //    语义精确，且两端一致。走到这里说明它不是 /media/...（已在 ② 短路），
        //    因此这次 File.Exists 只可能落在真正的本地路径上。
        if (raw.StartsWith('/') && File.Exists(raw)) return true;

        return false;
    }

    /// <summary>
    /// 剥掉 <c>file:</c> 协议前缀，得到 <c>File.ReadAllBytes</c> / <c>ImageSource.FromFile</c> 能用的路径。
    /// 非 <c>file:</c> 开头的原样返回。
    ///
    /// <para>要认的写法（Windows 与 Unix 的差别全在那一两个斜杠上）：</para>
    /// <list type="table">
    /// <item><c>file:///C:/x.jpg</c>（三斜杠 + 盘符，Windows 的 <c>new Uri(path).AbsoluteUri</c> 产物）→ <c>C:/x.jpg</c></item>
    /// <item><c>file://C:/x.jpg</c>、<c>file:/C:/x.jpg</c> → <c>C:/x.jpg</c></item>
    /// <item><c>file:///data/user/0/x.jpg</c>（三斜杠 + Unix 根）→ <c>/data/user/0/x.jpg</c>，开头的斜杠是根目录，必须留</item>
    /// <item><c>file:/data/x.jpg</c>、<c>file:data/x.jpg</c>（畸形写法）→ 补回根目录</item>
    /// </list>
    ///
    /// <para>⚠️ 两处都别按固定位数切字符串：<c>"file://"</c> 是 7 个字符、<c>"file:/"</c> 是 6 个，
    /// 按 7 切会把单斜杠写法的盘符冒号一起吃掉（<c>file:/C:/x.jpg</c> → <c>:Music/...</c>）。
    /// 所以先统一剥 <c>"file:"</c>，再判断剩下的是不是 <c>//</c> 开头。</para>
    ///
    /// <para>⚠️ 第三个分支（<c>/C:/x.jpg</c> 去掉前导斜杠）是 V2.13 修掉的旧缺陷：
    /// 旧实现只做"以 / 开头就保留"，于是 Windows 的 <c>file:///C:/x.jpg</c> 会留下 <c>/C:/x.jpg</c>，
    /// 交给 <c>File.ReadAllBytes</c> / <c>File.Exists</c> 是一个带盘符冒号的畸形路径，必然取不到文件
    /// （本地歌会被误判成"文件已失效"）。Unix 侧行为与旧实现完全一致。</para>
    /// </summary>
    public static string LocalPathOf(string raw)
    {
        if (!raw.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) return raw;

        var rest = raw[5..];

        // "//" 是协议与路径之间的 authority 分隔（file://host/path）。本项目不产生带主机名的写法，
        // 去掉它就够了；剩下的是纯路径部分。
        if (rest.StartsWith("//", StringComparison.Ordinal)) rest = rest[2..];

        // ① 已经是盘符路径：file://C:/x.jpg
        if (rest.Length >= 2 && char.IsLetter(rest[0]) && rest[1] == ':')
            return rest;

        // ② 盘符路径但多一个前导斜杠：file:///C:/x.jpg → C:/x.jpg
        if (rest.Length >= 3 && rest[0] == '/' && char.IsLetter(rest[1]) && rest[2] == ':')
            return rest[1..];

        // ③ Unix 绝对路径：开头斜杠就是根目录，原样保留
        if (rest.StartsWith('/')) return rest;

        // ④ 相对形态（file://data/x.jpg）：补回根目录，与旧实现一致
        return "/" + rest;
    }
}
