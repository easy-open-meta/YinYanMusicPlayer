namespace YinYanMusic.App.Services;

/// <summary>
/// App 内置头像（V2.9.3）。用户不再自选相册照片，只能从这几张里挑。
///
/// <para>
/// <b>出图规格</b>（照这个画，代码与界面都不用改）：
/// <list type="number">
///   <item>8 张，文件名与顺序：<c>boy_1.png</c>…<c>boy_4.png</c>（男）、<c>girl_1.png</c>…<c>girl_4.png</c>（女）；</item>
///   <item>PNG 正方形 320×320，**无任何文字/字母**；</item>
///   <item>主体居中、别贴边 —— 头像框是**圆形裁切**，四角会被切掉；</item>
///   <item>四角允许透明（圆形裁切之外，看不见），但**主体必须铺满内接圆**：
///         这层图是叠在"昵称首字兜底"圆之上的，主体四周留透明会露出底下的首字；</item>
///   <item>8 张风格统一，男女靠发型 / 装扮区分。</item>
/// </list>
/// </para>
/// <para>
/// <b>为什么选中之后还是要"上传"一张图</b>：服务端 <c>Users.AvatarUrl</c> 存的是 data URI，
/// 评论面板、关注列表、用户页都直接拿这个字符串渲染。把选中的内置头像编码成 data URI 提交，
/// 服务端与其它消费方一行都不用改；若改成存"内置头像的键"，那些地方全会显示不出图。
/// </para>
/// </summary>
public static class BuiltInAvatars
{
    /// <summary>
    /// 内置头像的资源名（顺序即选择器里的显示顺序：先男后女）。
    /// 文件放在 <c>Resources/Raw/avatars/</c>，csproj 的 <c>MauiAsset</c> 会保留子目录，
    /// 所以资源名正好等于 <c>avatars/xxx.png</c>。
    /// </summary>
    public static readonly string[] All =
    [
        "avatars/boy_1.png", "avatars/boy_2.png", "avatars/boy_3.png", "avatars/boy_4.png",
        "avatars/girl_1.png", "avatars/girl_2.png", "avatars/girl_3.png", "avatars/girl_4.png",
    ];

    /// <summary>给 <see cref="Image"/> 用的图源；路径为空返回 null。</summary>
    public static ImageSource? SourceOf(string? path) =>
        string.IsNullOrWhiteSpace(path)
            ? null
            : ImageSource.FromStream(async _ => await OpenAsync(path) ?? Stream.Null);

    /// <summary>读出原始字节（选中后转 data URI 提交）。读不到返回 null，调用方给提示。</summary>
    public static async Task<byte[]?> ReadAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        try
        {
            await using var stream = await OpenAsync(path);
            if (stream is null) return null;

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 打开包内资源。先走 MAUI 的 <see cref="FileSystem.OpenAppPackageFileAsync(string)"/> ——
    /// Android 的资源在 APK 里，只有它能读；失败再退回应用目录下的同名相对路径：
    /// 本项目的 Windows 形态是<b>非打包</b>（<c>WindowsPackageType=None</c>），
    /// 资源被原样复制到 <c>AppContext.BaseDirectory/avatars/</c>（已核对构建输出确有这些位置）。
    /// 两边都不是（比如资源没被打包）时返回 null，由调用方提示，而不是抛异常。
    /// </summary>
    private static async Task<Stream?> OpenAsync(string path)
    {
        try
        {
            return await FileSystem.OpenAppPackageFileAsync(path);
        }
        catch
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, path.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fallback) ? File.OpenRead(fallback) : null;
        }
    }
}
