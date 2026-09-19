using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(IMeService me, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<object>> Overview()
        => Ok(await me.GetOverviewAsync(currentUser.RequireUserId()));

    [HttpGet("playlists")]
    public async Task<ActionResult<IReadOnlyList<PlaylistDto>>> MyPlaylists()
        => Ok(await me.GetMyPlaylistsAsync(currentUser.RequireUserId()));

    /// <summary>当前用户关注的歌手完整信息（“我的”页关注列表）。</summary>
    [HttpGet("following-artists/details")]
    public async Task<ActionResult<IReadOnlyList<ArtistDto>>> FollowedArtists()
        => Ok(await me.GetFollowedArtistsAsync(currentUser.RequireUserId()));

    /// <summary>关注当前用户的粉丝列表。</summary>
    [HttpGet("followers")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> Followers()
        => Ok(await me.GetFollowersAsync(currentUser.RequireUserId()));

    [HttpPut("following/{userId:long}")]
    public async Task<IActionResult> FollowUser(long userId)
    {
        var result = await me.FollowAsync(currentUser.RequireUserId(), userId);
        return result.Success ? NoContent() : BadRequest(new { message = result.Error });
    }

    [HttpDelete("following/{userId:long}")]
    public async Task<IActionResult> UnfollowUser(long userId)
    {
        var result = await me.UnfollowAsync(currentUser.RequireUserId(), userId);
        return result.Success ? NoContent() : NotFound();
    }

    /// <summary>当前用户关注的歌手 Id 列表（客户端据此决定菜单显示"关注歌手"还是"取消关注歌手"）。</summary>
    [HttpGet("following-artists")]
    public async Task<ActionResult<IReadOnlyList<long>>> FollowedArtistIds()
        => Ok(await me.GetFollowedArtistIdsAsync(currentUser.RequireUserId()));

    /// <summary>当前用户关注的用户完整列表（"我的"页关注列表的用户区）。</summary>
    [HttpGet("following")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> FollowedUsers()
        => Ok(await me.GetFollowedUsersAsync(currentUser.RequireUserId()));

    [HttpPut("following-artists/{artistId:long}")]
    public async Task<IActionResult> FollowArtist(long artistId)
    {
        var result = await me.FollowArtistAsync(currentUser.RequireUserId(), artistId);
        return result.Success ? NoContent() : BadRequest(new { message = result.Error });
    }

    [HttpDelete("following-artists/{artistId:long}")]
    public async Task<IActionResult> UnfollowArtist(long artistId)
    {
        var result = await me.UnfollowArtistAsync(currentUser.RequireUserId(), artistId);
        return result.Success ? NoContent() : NotFound();
    }
}
