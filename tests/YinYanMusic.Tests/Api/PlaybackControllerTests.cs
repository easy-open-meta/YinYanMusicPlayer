using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Api.Controllers;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Tests.Api;

/// <summary>
/// 播放进度接口（V2.10）的状态码映射：这条链路里有两种"正常"的空结果
/// （没有任何进度记录 → 204），映射写错会让客户端一直显示一张空的"继续播放"卡片。
/// </summary>
public class PlaybackControllerTests
{
    private sealed class FakePlaybackService : IPlaybackService
    {
        public ServiceResult SaveResult { get; set; } = ServiceResult.Ok();
        public PlaybackProgressDto? LastResult { get; set; }

        /// <summary>记录控制器透传下来的当前用户：两个接口都必须带登录用户，否则进度会串账号。</summary>
        public long? LastSaveUserId { get; private set; }
        public long? LastGetUserId { get; private set; }

        public Task<ServiceResult> SaveAsync(long userId, PlaybackProgressRequest req)
        {
            LastSaveUserId = userId;
            return Task.FromResult(SaveResult);
        }

        public Task<PlaybackProgressDto?> GetLastAsync(long userId)
        {
            LastGetUserId = userId;
            return Task.FromResult(LastResult);
        }

        public Task<PlayReportsAck> AcceptReportsAsync(long userId, PlayReportsBatchRequest req) =>
            Task.FromResult(new PlayReportsAck(0, 0, []));
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public long? UserId { get; set; } = 42;

        public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException("未登录或令牌无效。");
    }

    private static (PlaybackController Controller, FakePlaybackService Service, FakeCurrentUser User) Create()
    {
        var service = new FakePlaybackService();
        var user = new FakeCurrentUser();
        return (new PlaybackController(service, user), service, user);
    }

    private static PlaybackProgressDto Sample() =>
        new(7, 62.5, DateTime.UtcNow, new SongDto { Id = 7, Title = "一首歌", AudioUrl = "media/a.mp3" });

    [Fact]
    public async Task SaveProgress_Ok_ReturnsNoContentWithCurrentUser()
    {
        var (controller, service, _) = Create();

        var result = await controller.SaveProgress(new PlaybackProgressRequest(7, 62.5));

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(42, service.LastSaveUserId);
    }

    [Fact]
    public async Task SaveProgress_UnknownSong_ReturnsBadRequest()
    {
        var (controller, service, _) = Create();
        service.SaveResult = ServiceResult.Fail("歌曲不存在。");

        var result = await controller.SaveProgress(new PlaybackProgressRequest(999, 1));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("歌曲不存在。", bad.Value?.ToString());
    }

    [Fact]
    public async Task Last_WithRecord_ReturnsOkWithSong()
    {
        var (controller, service, _) = Create();
        service.LastResult = Sample();

        var result = await controller.Last();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<PlaybackProgressDto>(ok.Value);
        Assert.Equal(7, dto.SongId);
        Assert.Equal(62.5, dto.PositionSeconds);
        Assert.Equal(42, service.LastGetUserId);
    }

    [Fact]
    public async Task Last_NoRecord_ReturnsNoContent()
    {
        var (controller, service, _) = Create();
        service.LastResult = null;

        Assert.IsType<NoContentResult>((await controller.Last()).Result);
    }
}
