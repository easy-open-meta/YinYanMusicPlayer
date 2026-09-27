using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Api.Controllers;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Tests.Api;

/// <summary>
/// 推荐接口（V2.12）的对外契约：匿名可访问（TC-2.12-06），且未登录时把 userId 透传成 null
/// 让服务层去降级 —— 控制器**不能**在这里自作主张地 401，否则游客打开发现页会看到一片空白。
/// </summary>
public class RecommendControllerTests
{
    private sealed class FakeRecommendService : IRecommendService
    {
        public string? LastScene { get; private set; }
        public int? LastCategoryId { get; private set; }
        public long? LastUserId { get; private set; }
        public bool LastUserIdWasPassed { get; private set; }

        public Task<PagedResult<PlaylistDto>> GetPlaylistsAsync(string? scene, int? categoryId, long? userId, int page, int pageSize)
        {
            LastScene = scene;
            LastCategoryId = categoryId;
            LastUserId = userId;
            LastUserIdWasPassed = true;
            return Task.FromResult(new PagedResult<PlaylistDto>([], 0, page, pageSize));
        }

        public Task<PagedResult<AdminRecommendedDto>> AdminListAsync(string? keyword, int page, int pageSize)
            => Task.FromResult(new PagedResult<AdminRecommendedDto>([], 0, page, pageSize));

        public Task<ServiceResult> AdminSetAsync(long playlistId, UpdateRecommendedRequest req)
            => Task.FromResult(ServiceResult.Ok());

        public Task<ServiceResult> AdminRemoveAsync(long playlistId) => Task.FromResult(ServiceResult.Ok());
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public long? UserId { get; set; }
        public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException("未登录或令牌无效。");
    }

    // ── TC-2.12-06 匿名可访问 ─────────────────────────────────────────────
    [Fact]
    public async Task Playlists_Anonymous_Succeeds()
    {
        var service = new FakeRecommendService();
        var controller = new RecommendController(service, new FakeCurrentUser { UserId = null });

        var result = await controller.Playlists(RecommendScenes.Hot, null, 1, 20);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(service.LastUserIdWasPassed);
        Assert.Null(service.LastUserId);
    }

    /// <summary>登录用户的 Id 必须原样透传，否则 for-you 永远只能是热播。</summary>
    [Fact]
    public async Task Playlists_LoggedIn_PassesUserIdThrough()
    {
        var service = new FakeRecommendService();
        var controller = new RecommendController(service, new FakeCurrentUser { UserId = 42 });

        await controller.Playlists(RecommendScenes.ForYou, null, 1, 20);

        Assert.Equal(42, service.LastUserId);
    }

    [Fact]
    public async Task Playlists_PassesSceneAndCategoryIdThrough()
    {
        var service = new FakeRecommendService();
        var controller = new RecommendController(service, new FakeCurrentUser());

        await controller.Playlists(RecommendScenes.Zone, 7, 2, 50);

        Assert.Equal(RecommendScenes.Zone, service.LastScene);
        Assert.Equal(7, service.LastCategoryId);
    }

    /// <summary>
    /// 场景容错是**服务层**的职责，控制器只负责透传 —— 未知名原样送下去，由服务层落到热播。
    /// 这条用例锁住"控制器不要自己做场景白名单"，否则以后加场景要改两处。
    /// </summary>
    [Fact]
    public async Task Playlists_UnknownScene_IsPassedThroughUntouched()
    {
        var service = new FakeRecommendService();
        var controller = new RecommendController(service, new FakeCurrentUser());

        await controller.Playlists("something-new", null, 1, 20);

        Assert.Equal("something-new", service.LastScene);
    }
}
