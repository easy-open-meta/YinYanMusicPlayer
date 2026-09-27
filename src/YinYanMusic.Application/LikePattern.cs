namespace YinYanMusic.Application;

/// <summary>
/// 拼 PostgreSQL 的 LIKE / ILIKE 匹配串。
/// 三个要点：
/// 1) 大小写不敏感 —— PostgreSQL 的 <c>LIKE</c> 区分大小写，用 <c>ILIKE</c> 才不区分
///    （EF 里对应 <see cref="Microsoft.EntityFrameworkCore.EF"/>.Functions.ILike）。
///    搜 "ado" 要能搜到 "Ado"。
/// 2) 转义通配符 —— <c>%</c> 和 <c>_</c> 在 LIKE 里是通配符，用户键入的这些字符必须按字面量匹配，
///    否则搜 "100%" 会命中几乎全部记录。
/// 3) **转义符必须显式传给 ILike** —— 见 <see cref="EscapeChar"/>，漏传等于没转义。
/// </summary>
public static class LikePattern
{
    /// <summary>
    /// 必须**显式**传给 <c>EF.Functions.ILike(col, pattern, escapeCharacter)</c> 的转义符。
    /// <para>
    /// ⚠️ 漏传就等着踩坑：Npgsql 生成的 SQL 是 <c>ILIKE @p ESCAPE ''</c> —— 空转义符意味着
    /// **转义被禁用**，于是 <see cref="Contains"/> 加进去的反斜杠退化成字面反斜杠，
    /// 关键词里只要出现 <c>_</c> / <c>%</c> / <c>\</c> 就永远匹配不到任何东西。
    /// </para>
    /// <para>
    /// 2026-09-23 实测（后台按创建者搜歌单）：<c>yeun</c> 命中 2 条，而完整用户名
    /// <c>ck_yeun9</c> 命中 0 条；单独搜 <c>_</c> 也是 0 条 —— 库里明明有下划线。
    /// 这是 <see cref="Contains"/> 自建立起就静默失效的缺陷（影响全项目所有关键词搜索），
    /// 所以凡是 <c>ILike</c> 都必须带上这个转义符。
    /// </para>
    /// </summary>
    public const string EscapeChar = "\\";

    /// <summary>把用户输入变成「包含」匹配串：<c>%escaped%</c>（须配合 <see cref="EscapeChar"/> 使用）。</summary>
    public static string Contains(string keyword)
    {
        var escaped = keyword
            .Replace("\\", "\\\\")   // 先转义转义符本身，顺序不能反
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        return $"%{escaped}%";
    }
}
