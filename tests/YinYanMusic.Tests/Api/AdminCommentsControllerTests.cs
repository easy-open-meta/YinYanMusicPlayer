using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Api.Controllers;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Tests.Api;

/// <summary>后台评论审核接口的状态码映射（V2.9）。</summary>
public class AdminCommentsControllerTests
{
    private sealed class FakeCommentService : ICommentService
    {
        public ServiceResult<PagedResult<AdminCommentDto>> AdminListResult { get; set; } =
            ServiceResult<PagedResult<AdminCommentDto>>.Ok(new PagedResult<AdminCommentDto>([], 0, 1, 20));

        public ServiceResult SetHiddenResult { get; set; } = ServiceResult.Ok();
        public ServiceResult AdminDeleteResult { get; set; } = ServiceResult.Ok();

        /// <summary>控制器透传下来的筛选条件，用来确认不会把「未筛」误写成筛某个值。</summary>
        public (string? Keyword, string? TargetType, bool? IsHidden)? LastFilter { get; private set; }

        public Task<ServiceResult<PagedResult<AdminCommentDto>>> AdminListAsync(
            string? keyword, string? targetType, bool? isHidden, int page, int pageSize)
        {
            LastFilter = (keyword, targetType, isHidden);
            return Task.FromResult(AdminListResult);
        }

        public Task<ServiceResult> AdminSetHiddenAsync(long commentId, bool hidden) => Task.FromResult(SetHiddenResult);

        public Task<ServiceResult> AdminDeleteAsync(long commentId) => Task.FromResult(AdminDeleteResult);

        public Task<ServiceResult<PagedResult<CommentDto>>> ListThreadRepliesAsync(
            long commentId, long? currentUserId, int page, int pageSize)
            => throw new NotSupportedException("后台控制器不该调用 App 侧的子树分页接口");

        public Task<ServiceResult<int>> CountAsync(string targetType, long targetId, long? currentUserId)
            => throw new NotSupportedException("后台控制器不该调用 App 侧的评论计数接口");

        public Task<ServiceResult<PagedResult<CommentDto>>> ListAsync(
            string targetType, long targetId, long? currentUserId, int page, int pageSize)
            => throw new NotSupportedException("后台控制器不该调用 App 侧的列表接口");

        public Task<ServiceResult<CommentDto>> CreateAsync(long userId, CreateCommentRequest req)
            => throw new NotSupportedException();

        public Task<ServiceResult> DeleteAsync(long userId, long commentId) => throw new NotSupportedException();

        public Task<ServiceResult<CommentLikeState>> LikeAsync(long userId, long commentId) => throw new NotSupportedException();

        public Task<ServiceResult<CommentLikeState>> UnlikeAsync(long userId, long commentId) => throw new NotSupportedException();
    }

    private static (AdminCommentsController Controller, FakeCommentService Service) Create()
    {
        var service = new FakeCommentService();
        return (new AdminCommentsController(service), service);
    }

    [Fact]
    public async Task List_ReturnsPagedResult()
    {
        var (controller, _) = Create();

        var result = await controller.List(null, null, null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<PagedResult<AdminCommentDto>>(ok.Value);
    }

    [Fact]
    public async Task List_PassesFiltersThrough()
    {
        var (controller, service) = Create();

        await controller.List("关键词", "playlist", true, 2, 50);

        Assert.Equal(("关键词", "playlist", (bool?)true), service.LastFilter);
    }

    [Fact]
    public async Task List_ServiceFails_ReturnsBadRequest()
    {
        var (controller, service) = Create();
        service.AdminListResult = ServiceResult<PagedResult<AdminCommentDto>>.Fail("查询失败。");

        var result = await controller.List(null, null, null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SetHidden_ReturnsNoContent()
    {
        var (controller, _) = Create();

        Assert.IsType<NoContentResult>(await controller.SetHidden(5, true));
    }

    [Fact]
    public async Task SetHidden_MissingComment_ReturnsNotFound()
    {
        var (controller, service) = Create();
        service.SetHiddenResult = ServiceResult.Fail(CommentService.CommentNotFound);

        Assert.IsType<NotFoundObjectResult>(await controller.SetHidden(5, false));
    }

    [Fact]
    public async Task Delete_ReturnsNoContent()
    {
        var (controller, _) = Create();

        Assert.IsType<NoContentResult>(await controller.Delete(5));
    }

    [Fact]
    public async Task Delete_MissingComment_ReturnsNotFound()
    {
        var (controller, service) = Create();
        service.AdminDeleteResult = ServiceResult.Fail(CommentService.CommentNotFound);

        Assert.IsType<NotFoundObjectResult>(await controller.Delete(5));
    }
}
