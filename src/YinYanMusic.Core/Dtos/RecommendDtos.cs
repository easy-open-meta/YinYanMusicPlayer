namespace YinYanMusic.Core.Dtos;

/// <summary>
/// 推荐场景编码（V2.12）。用字符串常量而不是 enum：接口是 <c>?scene=hot</c> 这种查询串，
/// 枚举要额外配 <c>JsonStringEnumConverter</c> 才能收发中文场景名，而这里只需要几个字面量。
/// </summary>
public static class RecommendScenes
{
    /// <summary>热播：歌单内歌曲播放次数之和降序。</summary>
    public const string Hot = "hot";

    /// <summary>高分收藏：收藏人数降序。</summary>
    public const string Collected = "collected";

    /// <summary>最新上架：创建时间降序。</summary>
    public const string New = "new";

    /// <summary>猜你喜欢：按用户喜欢的歌曲所属分类与歌手加权；未登录或没有任何兴趣信号时退化为热播。</summary>
    public const string ForYou = "for-you";

    /// <summary>分区推荐：指定 <c>categoryId</c> 的分区内按播放次数降序。缺 categoryId 时返回空列表（不静默换场景）。</summary>
    public const string Zone = "zone";

    /// <summary>未知 / 缺省场景一律落到热播 —— 推荐接口是公开入口，宁可给一份通用榜单也不要 400。</summary>
    public const string Default = Hot;
}

/// <summary>
/// 后台「推荐位管理」列表里的一行：歌单本体 + 它的推荐指标 + 人工干预状态（V2.12）。
/// <para>不做成 <see cref="PlaylistDto"/> 的子集或复用：那个 DTO 是**面向 C 端卡片**的，
/// 这里有 <see cref="SortOrder"/>/<see cref="Weight"/>/<see cref="IsHidden"/>/<see cref="HasOverride"/>
/// 四个只有后台才关心的字段，混在一起会让客户端模型背上运营概念。</para>
/// </summary>
public record AdminRecommendedDto(
    long PlaylistId,
    string Name,
    string? CoverUrl,
    string? CategoryName,
    string OwnerName,
    int TrackCount,
    int CollectorCount,
    /// <summary>歌单播放次数 = 歌单内全部歌曲 PlayCount 之和，与 C 端列表同一口径。</summary>
    long PlayCount,
    DateTime CreatedAt,
    /// <summary>系统歌单（如"我喜欢的音乐"）。后台列表照常展示，但规则排序永远不会推荐它。</summary>
    bool IsSystem,
    /// <summary>人工置顶序号，0 = 未置顶。</summary>
    int SortOrder,
    /// <summary>人工加权分。</summary>
    int Weight,
    /// <summary>是否已下线（从全部推荐场景里摘掉）。</summary>
    bool IsHidden,
    /// <summary>是否**存在**人工干预记录。false 表示这一行的三个干预字段都是默认值，不是运营设的 0。</summary>
    bool HasOverride);

/// <summary>
/// 后台设置推荐位（V2.12）。整行覆盖语义 —— 三个字段一次性提交，不做"只改其中一个"的局部更新：
/// 后台表单本来就是这三项一起编辑的，局部更新反而会出现"置顶成功但下线状态被重置"这类意外。
/// </summary>
public record UpdateRecommendedRequest(int SortOrder = 0, int Weight = 0, bool IsHidden = false);
