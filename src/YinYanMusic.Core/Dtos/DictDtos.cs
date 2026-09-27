namespace YinYanMusic.Core.Dtos;

/// <summary>数据字典项（读接口/管理接口通用）。</summary>
public record DictItemDto(int Id, string DictType, string Label, int SortOrder, bool IsEnabled);

/// <summary>
/// 字典类型 + 其下全部项（管理页用）。<paramref name="Items"/> 含禁用项，方便后台勾选启用状态。
/// </summary>
public record DictTypeDto(int Id, string Type, string Name, bool IsEnabled, IReadOnlyList<DictItemDto> Items);

/// <summary>新建字典类型。<paramref name="Type"/> 是编码（如 <c>artist_region</c>），须唯一。</summary>
public record CreateDictTypeRequest(string Type, string Name, bool IsEnabled = true);

/// <summary>编辑字典类型（局部更新：null 表示不改）。</summary>
public record UpdateDictTypeRequest(string? Name, bool? IsEnabled);

/// <summary>新建字典项。<paramref name="Label"/> 是选项文本，同一类型下须唯一。</summary>
public record CreateDictItemRequest(string Label, int SortOrder = 0, bool IsEnabled = true);

/// <summary>编辑字典项（局部更新：null 表示不改）。</summary>
public record UpdateDictItemRequest(string? Label, int? SortOrder, bool? IsEnabled);