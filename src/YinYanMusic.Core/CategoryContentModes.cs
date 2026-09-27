namespace YinYanMusic.Core;

/// <summary>
/// 分区（音乐专区）的内容类型：决定 App 专区详情页展示哪几段内容。
/// <para>
/// 存**文本**而不是枚举序号 —— 与库里 <c>Role</c> / <c>Kind</c> / <c>Gender</c> 的既有风格一致，
/// App（C#）与后台前端（TS）都不必维护一份枚举映射。
/// </para>
/// <para>
/// ⚠️ 这是**展示**开关，不在服务端强制：标成 <see cref="Songs"/> 的分区里若仍有歌单挂着，
/// 那些歌单只是不显示，数据依然在 —— <c>categoryId</c> 还被"给歌单打标签""后台按分区筛歌"等
/// 场景用着，在查询层过滤会让那些调用莫名返回空。
/// </para>
/// </summary>
public static class CategoryContentModes
{
    /// <summary>歌曲 + 歌单（默认，等于加这个字段之前的历史行为）。</summary>
    public const string Both = "both";

    /// <summary>仅歌曲：专区里不出现歌单段。</summary>
    public const string Songs = "songs";

    /// <summary>仅歌单：专区里不出现歌曲段。</summary>
    public const string Playlists = "playlists";

    public static readonly string[] All = [Both, Songs, Playlists];

    public static bool IsValid(string? mode) =>
        mode is not null && All.Contains(mode, StringComparer.Ordinal);

    /// <summary>
    /// 空值 / 未知值一律归一成 <see cref="Both"/>。读路径用它兜底：
    /// 宁可多显示一段，也不要让专区因为一个脏值变成空白页。
    /// </summary>
    public static string Normalize(string? mode) => IsValid(mode) ? mode! : Both;
}
