using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 数据字典（V2.16）：字典类型 + 字典项。
/// 读接口公开（歌手下拉框、App 等按类型取启用项）；写接口仅超管（后台「数据字典」页）。
/// </summary>
[ApiController]
[Route("api/dicts")]
public class DictsController(IDictService dicts) : ControllerBase
{
    /// <summary>公开：返回某类型下启用的字典项（如歌手地区 / 歌手类型的下拉选项）。</summary>
    [HttpGet("{type}")]
    public async Task<ActionResult<IReadOnlyList<DictItemDto>>> GetEnabledItems(string type)
        => Ok(await dicts.GetEnabledItemsAsync(type));

    /// <summary>后台：全部类型（含禁用）及每类型的全部项。</summary>
    [Authorize(Roles = "admin")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DictTypeDto>>> GetAll()
        => Ok(await dicts.GetAllAsync());

    [Authorize(Roles = "admin")]
    [HttpPost("types")]
    public async Task<ActionResult<DictTypeDto>> CreateType(CreateDictTypeRequest req)
    {
        var result = await dicts.CreateTypeAsync(req);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [Authorize(Roles = "admin")]
    [HttpPut("types/{id:int}")]
    public async Task<ActionResult<DictTypeDto>> UpdateType(int id, UpdateDictTypeRequest req)
    {
        var result = await dicts.UpdateTypeAsync(id, req);
        if (!result.Success)
        {
            return result.Error == "字典类型不存在。"
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }
        return Ok(result.Data);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("types/{id:int}")]
    public async Task<IActionResult> DeleteType(int id)
    {
        var result = await dicts.DeleteTypeAsync(id);
        return result.Success ? NoContent() : NotFound(new { message = result.Error });
    }

    [Authorize(Roles = "admin")]
    [HttpPost("{type}/items")]
    public async Task<ActionResult<DictItemDto>> CreateItem(string type, CreateDictItemRequest req)
    {
        var result = await dicts.CreateItemAsync(type, req);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [Authorize(Roles = "admin")]
    [HttpPut("items/{id:int}")]
    public async Task<ActionResult<DictItemDto>> UpdateItem(int id, UpdateDictItemRequest req)
    {
        var result = await dicts.UpdateItemAsync(id, req);
        if (!result.Success)
        {
            return result.Error == "字典项不存在。"
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }
        return Ok(result.Data);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> DeleteItem(int id)
    {
        var result = await dicts.DeleteItemAsync(id);
        return result.Success ? NoContent() : NotFound(new { message = result.Error });
    }
}