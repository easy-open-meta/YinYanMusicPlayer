using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

[ApiController]
[Route("api/catalog")]
public class CatalogController(ICatalogService catalog) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories()
        => Ok(await catalog.GetCategoriesAsync());

    [Authorize(Roles = "admin")]
    [HttpPost("categories")]
    public async Task<ActionResult<CategoryDto>> CreateCategory(CategoryDto req)
    {
        var result = await catalog.CreateCategoryAsync(req);
        return result.Success ? Ok(result.Data) : Conflict(new { message = result.Error });
    }

    [Authorize(Roles = "admin")]
    [HttpPut("categories/{id:int}")]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(int id, CategoryDto req)
    {
        var result = await catalog.UpdateCategoryAsync(id, req);
        if (!result.Success)
        {
            return result.Error == "分类不存在。"
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }
        return Ok(result.Data);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var result = await catalog.DeleteCategoryAsync(id);
        if (!result.Success)
        {
            return result.Error == "分类不存在。"
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }
        return NoContent();
    }

    [HttpGet("artists")]
    public async Task<ActionResult<PagedResult<ArtistDto>>> GetArtists([FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await catalog.GetArtistsAsync(keyword, page, pageSize));

    [HttpGet("artists/{id:long}")]
    public async Task<ActionResult<ArtistDto>> GetArtist(long id)
    {
        var dto = await catalog.GetArtistAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    // 建歌手/建专辑同样是后台行为（App 端没有这两个调用），以前裸奔，现在收归超管
    [Authorize(Roles = "admin")]
    [HttpPost("artists")]
    public async Task<ActionResult<ArtistDto>> CreateArtist(CreateArtistRequest req)
    {
        var result = await catalog.CreateArtistAsync(req);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    [Authorize(Roles = "admin")]
    [HttpPut("artists/{id:long}")]
    public async Task<ActionResult<ArtistDto>> UpdateArtist(long id, UpdateArtistRequest req)
    {
        var result = await catalog.UpdateArtistAsync(id, req);
        if (!result.Success)
        {
            return result.Error == "歌手不存在。"
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }
        return Ok(result.Data);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("artists/{id:long}")]
    public async Task<IActionResult> DeleteArtist(long id)
    {
        var result = await catalog.DeleteArtistAsync(id);
        if (!result.Success)
        {
            return result.Error == "歌手不存在。"
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }
        return NoContent();
    }

    [HttpGet("albums")]
    public async Task<ActionResult<PagedResult<AlbumDto>>> GetAlbums([FromQuery] long? artistId, [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await catalog.GetAlbumsAsync(artistId, keyword, page, pageSize));

    [HttpGet("albums/{id:long}")]
    public async Task<ActionResult<AlbumDto>> GetAlbum(long id)
    {
        var dto = await catalog.GetAlbumAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [Authorize(Roles = "admin")]
    [HttpPost("albums")]
    public async Task<ActionResult<AlbumDto>> CreateAlbum(CreateAlbumRequest req)
    {
        var result = await catalog.CreateAlbumAsync(req);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    [Authorize(Roles = "admin")]
    [HttpPut("albums/{id:long}")]
    public async Task<ActionResult<AlbumDto>> UpdateAlbum(long id, UpdateAlbumRequest req)
    {
        var result = await catalog.UpdateAlbumAsync(id, req);
        if (!result.Success)
        {
            return result.Error == "专辑不存在。"
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }
        return Ok(result.Data);
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("albums/{id:long}")]
    public async Task<IActionResult> DeleteAlbum(long id)
    {
        var result = await catalog.DeleteAlbumAsync(id);
        if (!result.Success)
        {
            return result.Error == "专辑不存在。"
                ? NotFound(new { message = result.Error })
                : Conflict(new { message = result.Error });
        }
        return NoContent();
    }
}
