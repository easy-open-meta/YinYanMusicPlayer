namespace YinYanMusic.Core.Entities;

/// <summary>
/// 数据字典类型（V2.16）。如 <c>artist_region</c>（歌手地区）/ <c>artist_kind</c>（歌手类型）。
/// <para>
/// 为什么引入：歌手地区/类型以前是前端写死的预设（REGION_PRESETS / KIND_PRESETS），
/// 加选项要改代码。现在选项存数据库，后台「数据字典」页可直接维护，歌手编辑框实时生效。
/// 歌手表 <see cref="Artist.Region"/> / <see cref="Artist.Kind"/> 仍存选项文本本身
/// （逗号分隔），历史数据不受字典项删除影响。
/// </para>
/// </summary>
public class DictType
{
    public int Id { get; set; }

    /// <summary>类型编码（唯一），如 <c>artist_region</c>。客户端按它取选项。</summary>
    public string Type { get; set; } = default!;

    /// <summary>显示名，如「歌手地区」。</summary>
    public string Name { get; set; } = default!;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>数据字典项：一个可选项。歌手表存的是 <see cref="Label"/> 本身。</summary>
public class DictItem
{
    public int Id { get; set; }

    /// <summary>所属字典类型编码（冗余存储，避免每次 join；与 <see cref="DictType.Type"/> 对应）。</summary>
    public string DictType { get; set; } = default!;

    /// <summary>选项文本，如「华语」「乐队」。存进歌手表的就是它。</summary>
    public string Label { get; set; } = default!;

    /// <summary>排序值，越小越靠前。</summary>
    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}