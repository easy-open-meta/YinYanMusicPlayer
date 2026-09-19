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

    [HttpGet("artists")]
    public async Task<ActionResult<PagedResult<ArtistDto>>> GetArtists([FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await catalog.GetArtistsAsync(keyword, page, pageSize));

    [HttpGet("artists/{id:long}")]
    public async Task<ActionResult<ArtistDto>> GetArtist(long id)
    {
        var dto = await catalog.GetArtistAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("artists")]
    public async Task<ActionResult<ArtistDto>> CreateArtist(CreateArtistRequest req)
        => Ok(await catalog.CreateArtistAsync(req));

    [HttpGet("albums")]
    public async Task<ActionResult<PagedResult<AlbumDto>>> GetAlbums([FromQuery] long? artistId, [FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await catalog.GetAlbumsAsync(artistId, keyword, page, pageSize));

    [HttpGet("albums/{id:long}")]
    public async Task<ActionResult<AlbumDto>> GetAlbum(long id)
    {
        var dto = await catalog.GetAlbumAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("albums")]
    public async Task<ActionResult<AlbumDto>> CreateAlbum(CreateAlbumRequest req)
    {
        var result = await catalog.CreateAlbumAsync(req);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }
}
