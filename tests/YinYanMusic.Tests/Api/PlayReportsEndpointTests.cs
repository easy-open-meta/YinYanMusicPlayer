using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Api.Controllers;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Tests.Api;

/// <summary>
/// 离线播放补报接口（V2.11）的状态码映射与参数校验：
/// 空批次 / 超大批次要被拒在门外，UserId 必须透传（补报串账号是最不能接受的故障）。
/// </summary>
public class PlayReportsEndpointTests
{
    private sealed class FakePlaybackService : IPlaybackService
    {
        public ServiceResult SaveResult { get; set; } = ServiceResult.Ok();
        public PlaybackProgressDto? LastResult { get; set; }
        public PlayReportsAck AckResult { get; set; } = new(0, 0, []);

        public long? LastAcceptUserId { get; private set; }
        public PlayReportsBatchRequest? LastBatch { get; private set; }

        public Task<ServiceResult> SaveAsync(long userId, PlaybackProgressRequest req) => Task.FromResult(SaveResult);
        public Task<PlaybackProgressDto?> GetLastAsync(long userId) => Task.FromResult(LastResult);

        public Task<PlayReportsAck> AcceptReportsAsync(long userId, PlayReportsBatchRequest req)
        {
            LastAcceptUserId = userId;
            LastBatch = req;
            return Task.FromResult(AckResult);
        }
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public long? UserId { get; set; } = 42;
        public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException("未登录或令牌无效。");
    }

    [Fact]
    public async Task Reports_EmptyItems_ReturnsBadRequest()
    {
        var service = new FakePlaybackService();
        var controller = new PlaybackController(service, new FakeCurrentUser());

        var result = await controller.Reports(new PlayReportsBatchRequest([]));

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Reports_OversizedBatch_ReturnsBadRequestWithoutCallingService()
    {
        var service = new FakePlaybackService();
        var controller = new PlaybackController(service, new FakeCurrentUser());
        var items = Enumerable.Range(0, IPlaybackService.MaxBatchSize + 1)
            .Select(i => new PlayReportRequest(i + 1, $"k{i}", DateTime.UtcNow, 0))
            .ToList();

        var result = await controller.Reports(new PlayReportsBatchRequest(items));

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Null(service.LastBatch);   // 超限批次不进服务层
    }

    [Fact]
    public async Task Reports_ValidBatch_PassesCurrentUserAndReturnsAck()
    {
        var service = new FakePlaybackService
        {
            AckResult = new PlayReportsAck(2, 1, [99])
        };
        var controller = new PlaybackController(service, new FakeCurrentUser());
        var items = new List<PlayReportRequest>
        {
            new(7, "k1", DateTime.UtcNow, 10),
            new(8, "k2", DateTime.UtcNow, 20),
        };

        var result = await controller.Reports(new PlayReportsBatchRequest(items));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var ack = Assert.IsType<PlayReportsAck>(ok.Value);
        Assert.Equal(2, ack.Accepted);
        Assert.Equal(1, ack.Duplicated);
        Assert.Equal([99], ack.UnknownSongs);
        Assert.Equal(42, service.LastAcceptUserId);
    }
}
