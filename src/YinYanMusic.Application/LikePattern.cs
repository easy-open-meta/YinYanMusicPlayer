namespace YinYanMusic.Application;

/// <summary>
/// 拼 PostgreSQL 的 LIKE / ILIKE 匹配串。
/// 两个要点：
/// 1) 大小写不敏感 —— PostgreSQL 的 <c>LIKE</c> 区分大小写，用 <c>ILIKE</c> 才不区分
///    （EF 里对应 <see cref="Microsoft.EntityFrameworkCore.EF"/>.Functions.ILike）。
///    搜 "ado" 要能搜到 "Ado"。
/// 2) 转义通配符 —— <c>%</c> 和 <c>_</c> 在 LIKE 里是通配符，<c>\</c> 是默认转义符。
///    用户键入的这些字符必须按字面量匹配，否则搜 "100%" 会命中几乎全部记录。
/// </summary>
public static class LikePattern
{
    /// <summary>把用户输入变成「包含」匹配串：<c>%escaped%</c>。</summary>
    public static string Contains(string keyword)
    {
        var escaped = keyword
            .Replace("\\", "\\\\")   // 先转义转义符本身，顺序不能反
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        return $"%{escaped}%";
    }
}
