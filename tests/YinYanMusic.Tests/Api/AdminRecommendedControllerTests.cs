using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Api.Controllers;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Tests.Api;

/// <summary>
/// 后台推荐位管理接口（V2.12）的状态码映射：歌单不存在 → 404、
/// 没有干预记录还来"移除干预" → 404（如实报错，不假装成功）。
/// </summary>
public class AdminRecommendedControllerTests
{
    private sealed class FakeRecommendService : IRecommendService
    {
        public ServiceResult SetResult { get; set; } = ServiceResult.Ok();
        public ServiceResult RemoveResult { get; set; } = ServiceResult.Ok();

        public Task<PagedResult<PlaylistDto>> GetPlaylistsAsync(string? scene, int? categoryId, long? userId, int page, int pageSize)
            => Task.FromResult(new PagedResult<PlaylistDto>([], 0, page, pageSize));

        public Task<PagedResult<AdminRecommendedDto>> AdminListAsync(string? keyword, int page, int pageSize)
            => Task.FromResult(new PagedResult<AdminRecommendedDto>([], 0, page, pageSize));

        public Task<ServiceResult> AdminSetAsync(long playlistId, UpdateRecommendedRequest req) => Task.FromResult(SetResult);

        public Task<ServiceResult> AdminRemoveAsync(long playlistId) => Task.FromResult(RemoveResult);
    }

    [Fact]
    public async Task Set_Ok_ReturnsNoContent()
    {
        var controller = new AdminRecommendedController(new FakeRecommendService());

        var result = await controller.Set(1, new UpdateRecommendedRequest(SortOrder: 1));

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Set_UnknownPlaylist_ReturnsNotFound()
    {
        var service = new FakeRecommendService { SetResult = ServiceResult.Fail(RecommendService.PlaylistNotFound) };
        var controller = new AdminRecommendedController(service);

        Assert.IsType<NotFoundObjectResult>(await controller.Set(99999, new UpdateRecommendedRequest()));
    }

    [Fact]
    public async Task Remove_WithoutOverride_ReturnsNotFound()
    {
        var service = new FakeRecommendService { RemoveResult = ServiceResult.Fail(RecommendService.NoOverride) };
        var controller = new AdminRecommendedController(service);

        Assert.IsType<NotFoundObjectResult>(await controller.Remove(1));
    }

    [Fact]
    public async Task List_ReturnsOk()
    {
        var controller = new AdminRecommendedController(new FakeRecommendService());

        var result = await controller.List(null, 1, 20);

        Assert.IsType<OkObjectResult>(result.Result);
    }
}
