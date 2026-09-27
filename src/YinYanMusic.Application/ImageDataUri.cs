namespace YinYanMusic.Application;

/// <summary>
/// 图片字段里允许出现的 <c>data:</c> URI（base64 内联图）的校验规则。
/// <para>
/// 背景：用户头像（V2.5）已经以 <c>data:image/...;base64,…</c> 形式存库，客户端
/// <c>ImageSourceFactory</c> / 后台 <c>mediaUrl()</c> 都能直接渲染。专辑封面与歌手头像
/// 沿用同一套（列本来就是 <c>text</c>，无需迁移），好处是**不必往服务器部署图片文件**。
/// </para>
/// <para>
/// ⚠️ 体积上限存在的理由：这些字段会进入**每一行列表响应**（歌曲列表的封面还会用专辑封面兜底），
/// 一张几 MB 的原图 base64 能把一页列表撑到几十 MB。所以后台侧先缩到 512px 再转 base64
/// （约 30~60KB），这里的上限是最后一道闸，拦的是绕过客户端直接调接口的情况。
/// </para>
/// </summary>
public static class ImageDataUri
{
    /// <summary>解码后的字节上限。与用户头像（<c>AuthService</c>）保持一致。</summary>
    public const int MaxDecodedBytes = 2 * 1024 * 1024;

    public static readonly string[] AllowedMimeTypes =
        ["image/png", "image/jpeg", "image/jpg", "image/webp", "image/gif"];

    public static bool IsDataUri(string? value) =>
        value is not null && value.StartsWith("data:", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 校验一个"可空图片字段"。非 data URI（普通 URL / 相对路径 / 空值）一律放行 ——
    /// 兼容既有数据，也允许继续填外链。返回错误信息，<c>null</c> 表示通过。
    /// </summary>
    public static string? Validate(string? value)
    {
        if (!IsDataUri(value)) return null;

        var comma = value!.IndexOf(',');
        if (comma <= 5) return "图片数据格式不正确。";

        // 头部形如 image/png;base64
        var header = value[5..comma];
        var semi = header.IndexOf(';');
        var mime = (semi > 0 ? header[..semi] : header).Trim().ToLowerInvariant();
        if (!AllowedMimeTypes.Contains(mime))
            return $"不支持的图片格式（{mime}），请用 PNG / JPG / WebP / GIF。";

        byte[] bytes;
        try { bytes = Convert.FromBase64String(value[(comma + 1)..]); }
        catch (FormatException) { return "图片数据格式不正确。"; }

        // 按解码后的真实字节数判，而不是 base64 字符串长度（后者会膨胀约 1/3）
        if (bytes.Length > MaxDecodedBytes)
            return $"图片过大（{bytes.Length / 1024.0 / 1024.0:F1}MB），请压缩到 {MaxDecodedBytes / 1024 / 1024}MB 以内。";

        return null;
    }
}
