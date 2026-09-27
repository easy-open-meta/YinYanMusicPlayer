using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Api.Controllers;

/// <summary>
/// 播放进度（V2.10 断点续播）。两个接口都要求登录 —— 进度是账号维度的数据。
/// </summary>
[ApiController]
[Route("api/me/playback")]
[Authorize]
public class PlaybackController(IPlaybackService playback, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>
    /// 上报播放进度。客户端在暂停、切歌、退出 App 以及播放中每 30 秒各报一次；
    /// 失败由客户端静默忽略（进度丢一条不影响播放本身）。
    /// </summary>
    [HttpPut("progress")]
    public async Task<IActionResult> SaveProgress(PlaybackProgressRequest req)
    {
        var result = await playback.SaveAsync(currentUser.RequireUserId(), req);
        return result.Success ? NoContent() : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// 最近一次播放，供"继续播放"卡片用。没有任何记录（新用户 / 刚清过）返回 204，
    /// 客户端据此不显示这张卡片。
    /// </summary>
    [HttpGet("last")]
    public async Task<ActionResult<PlaybackProgressDto>> Last()
    {
        var dto = await playback.GetLastAsync(currentUser.RequireUserId());
        return dto is null ? NoContent() : Ok(dto);
    }

    /// <summary>
    /// 离线播放补报（V2.11）：客户端把本地队列里"离线听过的缓存歌"批量补交上来。
    /// 按客户端幂等键去重 —— 重复提交不重复计数（TC-2.11-02）；曲库里已删的歌在
    /// unknownSongs 里返回（TC-2.11-03）。三类结果客户端统一从队列删除、不重试。
    /// </summary>
    [HttpPost("reports")]
    public async Task<ActionResult<PlayReportsAck>> Reports(PlayReportsBatchRequest req)
    {
        if ((req.Items?.Count ?? 0) == 0) return BadRequest(new { message = "items 不能为空。" });
        if (req.Items.Count > IPlaybackService.MaxBatchSize)
            return BadRequest(new { message = $"单次最多 {IPlaybackService.MaxBatchSize} 条，请分批提交。" });

        var ack = await playback.AcceptReportsAsync(currentUser.RequireUserId(), req);
        return Ok(ack);
    }
}
