using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface IDictService
{
    /// <summary>读接口：返回某类型下**启用**的字典项（按排序、ID 升序）。</summary>
    Task<IReadOnlyList<DictItemDto>> GetEnabledItemsAsync(string type);

    /// <summary>管理接口：返回全部类型（含禁用）及每类型的全部项。</summary>
    Task<IReadOnlyList<DictTypeDto>> GetAllAsync();

    Task<ServiceResult<DictTypeDto>> CreateTypeAsync(CreateDictTypeRequest req);
    Task<ServiceResult<DictTypeDto>> UpdateTypeAsync(int id, UpdateDictTypeRequest req);
    Task<ServiceResult<bool>> DeleteTypeAsync(int id);
    Task<ServiceResult<DictItemDto>> CreateItemAsync(string type, CreateDictItemRequest req);
    Task<ServiceResult<DictItemDto>> UpdateItemAsync(int id, UpdateDictItemRequest req);
    Task<ServiceResult<bool>> DeleteItemAsync(int id);
}

public class DictService(MusicDbContext db) : IDictService
{
    public async Task<IReadOnlyList<DictItemDto>> GetEnabledItemsAsync(string type) =>
        await db.DictItems.AsNoTracking()
            .Where(i => i.DictType == type && i.IsEnabled)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new DictItemDto(i.Id, i.DictType, i.Label, i.SortOrder, i.IsEnabled))
            .ToListAsync();

    public async Task<IReadOnlyList<DictTypeDto>> GetAllAsync()
    {
        var types = await db.DictTypes.AsNoTracking().OrderBy(t => t.Id).ToListAsync();
        var items = await db.DictItems.AsNoTracking()
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .ToListAsync();

        return types
            .Select(t => new DictTypeDto(
                t.Id, t.Type, t.Name, t.IsEnabled,
                items.Where(i => i.DictType == t.Type)
                    .Select(i => new DictItemDto(i.Id, i.DictType, i.Label, i.SortOrder, i.IsEnabled))
                    .ToList()))
            .ToList();
    }

    public async Task<ServiceResult<DictTypeDto>> CreateTypeAsync(CreateDictTypeRequest req)
    {
        var type = req.Type.Trim();
        var name = req.Name.Trim();
        if (string.IsNullOrEmpty(type))
            return ServiceResult<DictTypeDto>.Fail("字典类型编码不能为空。");
        if (string.IsNullOrEmpty(name))
            return ServiceResult<DictTypeDto>.Fail("字典类型名称不能为空。");
        if (await db.DictTypes.AnyAsync(t => t.Type == type))
            return ServiceResult<DictTypeDto>.Fail($"字典类型「{type}」已存在。");

        var entity = new DictType { Type = type, Name = name, IsEnabled = req.IsEnabled };
        db.DictTypes.Add(entity);
        await db.SaveChangesAsync();
        return ServiceResult<DictTypeDto>.Ok(entity.ToDto());
    }

    public async Task<ServiceResult<DictTypeDto>> UpdateTypeAsync(int id, UpdateDictTypeRequest req)
    {
        var type = await db.DictTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type is null) return ServiceResult<DictTypeDto>.Fail("字典类型不存在。");

        if (req.Name is not null)
        {
            var name = req.Name.Trim();
            if (string.IsNullOrEmpty(name))
                return ServiceResult<DictTypeDto>.Fail("字典类型名称不能为空。");
            type.Name = name;
        }
        if (req.IsEnabled.HasValue) type.IsEnabled = req.IsEnabled.Value;

        await db.SaveChangesAsync();
        return ServiceResult<DictTypeDto>.Ok(type.ToDto());
    }

    public async Task<ServiceResult<bool>> DeleteTypeAsync(int id)
    {
        var type = await db.DictTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type is null) return ServiceResult<bool>.Fail("字典类型不存在。");

        // 冗余存储没有外键，删类型前先清掉它名下的项。
        db.DictItems.RemoveRange(db.DictItems.Where(i => i.DictType == type.Type));
        db.DictTypes.Remove(type);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<DictItemDto>> CreateItemAsync(string type, CreateDictItemRequest req)
    {
        if (!await db.DictTypes.AnyAsync(t => t.Type == type))
            return ServiceResult<DictItemDto>.Fail($"字典类型「{type}」不存在。");

        var label = req.Label.Trim();
        if (string.IsNullOrEmpty(label))
            return ServiceResult<DictItemDto>.Fail("字典项名称不能为空。");
        if (await db.DictItems.AnyAsync(i => i.DictType == type && i.Label == label))
            return ServiceResult<DictItemDto>.Fail($"字典项「{label}」已存在。");

        var entity = new DictItem
        {
            DictType = type,
            Label = label,
            SortOrder = req.SortOrder,
            IsEnabled = req.IsEnabled
        };
        db.DictItems.Add(entity);
        await db.SaveChangesAsync();
        return ServiceResult<DictItemDto>.Ok(entity.ToDto());
    }

    public async Task<ServiceResult<DictItemDto>> UpdateItemAsync(int id, UpdateDictItemRequest req)
    {
        var item = await db.DictItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return ServiceResult<DictItemDto>.Fail("字典项不存在。");

        if (req.Label is not null)
        {
            var label = req.Label.Trim();
            if (string.IsNullOrEmpty(label))
                return ServiceResult<DictItemDto>.Fail("字典项名称不能为空。");
            if (label != item.Label
                && await db.DictItems.AnyAsync(i => i.DictType == item.DictType && i.Label == label))
                return ServiceResult<DictItemDto>.Fail($"字典项「{label}」已存在。");
            item.Label = label;
        }
        if (req.SortOrder.HasValue) item.SortOrder = req.SortOrder.Value;
        if (req.IsEnabled.HasValue) item.IsEnabled = req.IsEnabled.Value;

        await db.SaveChangesAsync();
        return ServiceResult<DictItemDto>.Ok(item.ToDto());
    }

    public async Task<ServiceResult<bool>> DeleteItemAsync(int id)
    {
        var item = await db.DictItems.FirstOrDefaultAsync(i => i.Id == id);
        if (item is null) return ServiceResult<bool>.Fail("字典项不存在。");

        db.DictItems.Remove(item);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }
}

internal static class DictMapping
{
    // 管理页读取走 GetAllAsync（那里手动按 DictType 编码分组），增改类型响应里不带项即可。
    public static DictTypeDto ToDto(this DictType t)
        => new(t.Id, t.Type, t.Name, t.IsEnabled, []);

    public static DictItemDto ToDto(this DictItem i)
        => new(i.Id, i.DictType, i.Label, i.SortOrder, i.IsEnabled);
}