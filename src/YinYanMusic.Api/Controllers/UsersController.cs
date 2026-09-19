using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(IUserService users, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>按用户名/昵称模糊搜索用户（搜索页"用户"结果区）。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Search([FromQuery] string keyword, [FromQuery] int limit = 10)
        => Ok(await users.SearchAsync(keyword, Math.Clamp(limit, 1, 50)));

    /// <summary>用户详情页资料（含粉丝/关注/歌单统计与观看者的关注状态）。</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<UserProfileDto>> GetProfile(long id)
        => await users.GetProfileAsync(id, currentUser.UserId) is { } profile ? Ok(profile) : NotFound();

    /// <summary>该用户的公开歌单（不含"我喜欢的音乐"等系统歌单）。</summary>
    [HttpGet("{id:long}/playlists")]
    public async Task<ActionResult<IReadOnlyList<PlaylistDto>>> GetPlaylists(long id)
        => Ok(await users.GetPublicPlaylistsAsync(id));
}
