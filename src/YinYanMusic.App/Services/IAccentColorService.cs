namespace YinYanMusic.App.Services;

/// <summary>
/// 从封面图提取「主色」，用于播放页等场景的动态配色。
/// 取不到图（或图里没有可用色彩）时返回 null，调用方自行降级到默认色。
/// </summary>
public interface IAccentColorService
{
    Task<Color?> ExtractAsync(string? imageUrl, CancellationToken ct = default);
}

/// <summary>
/// 给视图应用平台原生的背景模糊（Android 12+ 的 RenderEffect / Windows 的合成层）。
/// 平台不支持时返回 false，调用方应保留未模糊的兜底外观。
/// </summary>
public interface IBlurService
{
    bool Apply(VisualElement view, float radius);

    void Clear(VisualElement view);
}

/// <summary>
/// 生成「亚克力底图」：把封面缩到 <see cref="AcrylicSize"/> 见方、再糊几趟，
/// 得到一张没有细节的柔和色块图，交给浮窗当铺底。
/// 因为是在像素层做出的磨砂效果，不依赖视图生命周期、也不挑系统版本
/// （老的 RenderEffect 方案在「视图不可见 / 图片没加载完」时会把底图渲染成纯黑）。
/// 生成过程带内存缓存：同一张封面重复打开浮窗时直接命中，不再下载与重算。
/// 取不到图时返回 null，调用方保留半透明遮罩即可。
/// </summary>
public interface IAcrylicImageService
{
    Task<ImageSource?> CreateAsync(string? coverUrl, CancellationToken ct = default);
}

public static class AcrylicDefaults
{
    /// <summary>底图边长（像素）。放大会被双线性插值抹平，越小越柔、也越省事。</summary>
    public const int Size = 40;

    /// <summary>箱式模糊半径（单位：底图像素），配合 Size 决定磨砂的粗细。</summary>
    public const int BlurRadius = 4;
}
