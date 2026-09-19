using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

[ApiController]
[Route("api/songs")]
public class SongsController(ISongService songs, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SongDto>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] long? artistId,
        [FromQuery] long? albumId,
        [FromQuery] int? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await songs.SearchAsync(keyword, artistId, albumId, categoryId, page, pageSize));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SongDto>> Get(long id)
    {
        var dto = await songs.GetAsync(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<SongDto>> Create(CreateSongRequest req)
    {
        var result = await songs.CreateAsync(req);
        return result.Success ? Ok(result.Data) : BadRequest(new { message = result.Error });
    }

    [Authorize]
    [HttpPost("{id:long}/play")]
    public async Task<IActionResult> RecordPlay(long id)
    {
        await songs.RecordPlayAsync(id);
        return NoContent();
    }

    [Authorize]
    [HttpGet("liked")]
    public async Task<ActionResult<IReadOnlyList<SongDto>>> GetLiked()
        => Ok(await songs.GetLikedAsync(currentUser.RequireUserId()));

    [Authorize]
    [HttpPut("{id:long}/like")]
    public async Task<IActionResult> Like(long id)
    {
        var ok = await songs.LikeAsync(currentUser.RequireUserId(), id);
        return ok ? NoContent() : NotFound(new { message = "歌曲不存在。" });
    }

    [Authorize]
    [HttpDelete("{id:long}/like")]
    public async Task<IActionResult> Unlike(long id)
    {
        var ok = await songs.UnlikeAsync(currentUser.RequireUserId(), id);
        return ok ? NoContent() : NotFound();
    }
}
