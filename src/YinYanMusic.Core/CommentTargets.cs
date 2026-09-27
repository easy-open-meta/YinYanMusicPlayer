namespace YinYanMusic.Core;

/// <summary>
/// 评论挂载的对象类型：歌曲 / 歌单（V2.9）。
/// <para>
/// 存文本而不是枚举序号，与库里 <c>Role</c> / <c>Kind</c> / <c>CategoryContentModes</c> 的既有风格一致，
/// App（C#）与后台前端（TS）都不必维护一份枚举映射。
/// </para>
/// <para>
/// 两种对象共用一张 <c>Comments</c> 表而不是拆成 <c>SongComments</c> / <c>PlaylistComments</c>：
/// 字段、审核动作、点赞语义完全一致，分表只会让列表查询、后台分页、级联清理各写两遍。
/// 代价是 <c>TargetId</c> 是多态的、**建不了外键**，所以删歌曲/删歌单时必须由服务层
/// 显式清理评论 —— 见 <c>SongService.DeleteAsync</c> / <c>PlaylistService.DeleteAsync</c>。
/// </para>
/// </summary>
public static class CommentTargets
{
    /// <summary>歌曲评论。</summary>
    public const string Song = "song";

    /// <summary>歌单评论。</summary>
    public const string Playlist = "playlist";

    public static readonly string[] All = [Song, Playlist];

    public static bool IsValid(string? targetType) =>
        targetType is not null && All.Contains(targetType, StringComparer.Ordinal);
}
