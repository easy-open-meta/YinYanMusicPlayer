using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

[ApiController]
[Route("api/playlists")]
public class PlaylistsController(IPlaylistService playlists, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PlaylistDto>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] int? categoryId,
        [FromQuery] long? ownerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await playlists.SearchAsync(keyword, categoryId, ownerId, page, pageSize));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<PlaylistDetailDto>> Get(long id)
    {
        var dto = await playlists.GetAsync(id, currentUser.UserId);
        return dto is null ? NotFound() : Ok(dto);
    }

    [Authorize]
    [HttpPost]
    public async Task<ActionResult<PlaylistDto>> Create(CreatePlaylistRequest req)
        => Ok(await playlists.CreateAsync(currentUser.RequireUserId(), req));

    [Authorize]
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdatePlaylistRequest req)
        => MapResult(await playlists.UpdateAsync(currentUser.RequireUserId(), id, req));

    [Authorize]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id)
        => MapResult(await playlists.DeleteAsync(currentUser.RequireUserId(), id));

    [Authorize]
    [HttpPost("{id:long}/songs")]
    public async Task<IActionResult> AddSongs(long id, AddSongsToPlaylistRequest req)
        => MapResult(await playlists.AddSongsAsync(currentUser.RequireUserId(), id, req));

    [Authorize]
    [HttpDelete("{id:long}/songs/{songId:long}")]
    public async Task<IActionResult> RemoveSong(long id, long songId)
        => MapResult(await playlists.RemoveSongAsync(currentUser.RequireUserId(), id, songId));

    [Authorize]
    [HttpPut("{id:long}/collect")]
    public async Task<IActionResult> Collect(long id)
        => MapResult(await playlists.CollectAsync(currentUser.RequireUserId(), id));

    [Authorize]
    [HttpDelete("{id:long}/collect")]
    public async Task<IActionResult> Uncollect(long id)
        => MapResult(await playlists.UncollectAsync(currentUser.RequireUserId(), id));

    [Authorize]
    [HttpGet("collected")]
    public async Task<ActionResult<IReadOnlyList<PlaylistDto>>> GetCollected()
        => Ok(await playlists.GetCollectedAsync(currentUser.RequireUserId()));

    [Authorize]
    [HttpGet("contains-song")]
    public async Task<ActionResult<IReadOnlyList<long>>> ContainsSong([FromQuery] long songId)
        => Ok(await playlists.GetPlaylistsContainingSongAsync(currentUser.RequireUserId(), songId));

    private IActionResult MapResult(ServiceResult r) => r.Error switch
    {
        "歌单不存在。" or "未收藏该歌单。" => NotFound(new { message = r.Error }),
        "无权操作。" => Forbid(),
        _ => r.Success ? NoContent() : BadRequest(new { message = r.Error })
    };
}
