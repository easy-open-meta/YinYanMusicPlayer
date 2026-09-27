using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Api.Controllers;
using YinYanMusic.Application;
using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Tests.Api;

/// <summary>
/// 评论控制器的状态码映射（V2.9）。服务的数据库逻辑由接口回归覆盖，这里只盯
/// 「ServiceResult 的文案 → HTTP 状态码」这一层：映射漏一条，用户看到的就是 400 而该是 404/403。
/// </summary>
public class CommentsControllerTests
{
    private sealed class FakeCommentService : ICommentService
    {
        public ServiceResult<PagedResult<CommentDto>> ListResult { get; set; } =
            ServiceResult<PagedResult<CommentDto>>.Ok(new PagedResult<CommentDto>([], 0, 1, 20));

        public ServiceResult<CommentDto> CreateResult { get; set; } =
            ServiceResult<CommentDto>.Ok(SampleComment());

        public ServiceResult DeleteResult { get; set; } = ServiceResult.Ok();
        public ServiceResult<CommentLikeState> LikeResult { get; set; } =
            ServiceResult<CommentLikeState>.Ok(new CommentLikeState(1, true));
        public ServiceResult<CommentLikeState> UnlikeResult { get; set; } =
            ServiceResult<CommentLikeState>.Ok(new CommentLikeState(0, false));

        /// <summary>记录控制器传下来的"当前用户"，用来验证未登录时透传的就是 null（隐藏评论靠它过滤）。</summary>
        public long? LastListUserId { get; private set; }
        public bool ListCalled { get; private set; }

        public Task<ServiceResult<PagedResult<CommentDto>>> ListAsync(
            string targetType, long targetId, long? currentUserId, int page, int pageSize)
        {
            ListCalled = true;
            LastListUserId = currentUserId;
            return Task.FromResult(ListResult);
        }

        public Task<ServiceResult<CommentDto>> CreateAsync(long userId, CreateCommentRequest req)
            => Task.FromResult(CreateResult);

        public Task<ServiceResult> DeleteAsync(long userId, long commentId) => Task.FromResult(DeleteResult);

        public Task<ServiceResult<CommentLikeState>> LikeAsync(long userId, long commentId) => Task.FromResult(LikeResult);

        public Task<ServiceResult<CommentLikeState>> UnlikeAsync(long userId, long commentId) => Task.FromResult(UnlikeResult);

        public Task<ServiceResult<PagedResult<AdminCommentDto>>> AdminListAsync(
            string? keyword, string? targetType, bool? isHidden, int page, int pageSize)
            => Task.FromResult(ServiceResult<PagedResult<AdminCommentDto>>.Ok(new PagedResult<AdminCommentDto>([], 0, 1, 20)));

        public Task<ServiceResult> AdminSetHiddenAsync(long commentId, bool hidden) => Task.FromResult(ServiceResult.Ok());

        public Task<ServiceResult> AdminDeleteAsync(long commentId) => Task.FromResult(ServiceResult.Ok());

        public ServiceResult<PagedResult<CommentDto>> ThreadRepliesResult { get; set; } =
            ServiceResult<PagedResult<CommentDto>>.Ok(new PagedResult<CommentDto>([], 0, 1, 200));

        public Task<ServiceResult<PagedResult<CommentDto>>> ListThreadRepliesAsync(
            long commentId, long? currentUserId, int page, int pageSize)
            => Task.FromResult(ThreadRepliesResult);

        public ServiceResult<int> CountResult { get; set; } = ServiceResult<int>.Ok(3);

        /// <summary>记录控制器传下来的当前用户：未登录时必须是 null（隐藏评论的可见性靠它过滤）。</summary>
        public long? LastCountUserId { get; private set; }

        public Task<ServiceResult<int>> CountAsync(string targetType, long targetId, long? currentUserId)
        {
            LastCountUserId = currentUserId;
            return Task.FromResult(CountResult);
        }
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public long? UserId { get; set; } = 42;

        public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException("未登录或令牌无效。");
    }

    private static CommentDto SampleComment() =>
        new(1, 42, "我", null, null, "一条评论", 0, false, false, true, DateTime.UtcNow, []);

    private static (CommentsController Controller, FakeCommentService Service, FakeCurrentUser User) Create()
    {
        var service = new FakeCommentService();
        var user = new FakeCurrentUser();
        return (new CommentsController(service, user), service, user);
    }

    [Fact]
    public async Task List_Anonymous_PassesNullUserIdAndReturnsOk()
    {
        var (controller, service, user) = Create();
        user.UserId = null;

        var result = await controller.List("song", 7);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(service.ListCalled);
        Assert.Null(service.LastListUserId);
    }

    [Fact]
    public async Task List_InvalidTargetType_ReturnsBadRequest()
    {
        var (controller, service, _) = Create();
        service.ListResult = ServiceResult<PagedResult<CommentDto>>.Fail("评论对象类型不正确。");

        var result = await controller.List("album", 7);

        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("评论对象类型不正确。", bad.Value?.ToString());
    }

    [Fact]
    public async Task Create_Success_ReturnsOkWithComment()
    {
        var (controller, _, _) = Create();

        var result = await controller.Create(new CreateCommentRequest(CommentTargets.Song, 7, null, "你好"));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<CommentDto>(ok.Value);
    }

    [Theory]
    [InlineData(CommentService.TargetNotFound)]
    [InlineData(CommentService.CommentNotFound)]
    public async Task Create_TargetOrParentMissing_ReturnsNotFound(string error)
    {
        var (controller, service, _) = Create();
        service.CreateResult = ServiceResult<CommentDto>.Fail(error);

        var result = await controller.Create(new CreateCommentRequest(CommentTargets.Song, 7, null, "你好"));

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData("评论内容不能为空。")]
    [InlineData("评论内容不能超过 500 字。")]
    [InlineData("评论发送过于频繁，请稍后再试。")]
    [InlineData("只能回复主评论。")]
    public async Task Create_ValidationOrThrottle_ReturnsBadRequest(string error)
    {
        var (controller, service, _) = Create();
        service.CreateResult = ServiceResult<CommentDto>.Fail(error);

        var result = await controller.Create(new CreateCommentRequest(CommentTargets.Song, 7, 1, "你好"));

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains(error, bad.Value?.ToString());
    }

    [Fact]
    public async Task Delete_OwnComment_ReturnsNoContent()
    {
        var (controller, _, _) = Create();

        Assert.IsType<NoContentResult>(await controller.Delete(3));
    }

    [Fact]
    public async Task Delete_OtherUsersComment_ReturnsForbid()
    {
        var (controller, service, _) = Create();
        service.DeleteResult = ServiceResult.Fail(CommentService.NoPermission);

        Assert.IsType<ForbidResult>(await controller.Delete(3));
    }

    [Fact]
    public async Task Delete_MissingComment_ReturnsNotFound()
    {
        var (controller, service, _) = Create();
        service.DeleteResult = ServiceResult.Fail(CommentService.CommentNotFound);

        Assert.IsType<NotFoundObjectResult>(await controller.Delete(3));
    }

    [Fact]
    public async Task Like_ReturnsCountAndLikedFlag()
    {
        var (controller, _, _) = Create();

        var result = await controller.Like(3);

        var ok = Assert.IsType<OkObjectResult>(result);
        var state = Assert.IsType<CommentLikeState>(ok.Value);
        Assert.Equal(1, state.LikeCount);
        Assert.True(state.IsLiked);
    }

    [Fact]
    public async Task Like_MissingComment_ReturnsNotFound()
    {
        var (controller, service, _) = Create();
        service.LikeResult = ServiceResult<CommentLikeState>.Fail(CommentService.CommentNotFound);

        Assert.IsType<NotFoundObjectResult>(await controller.Like(3));
    }

    [Fact]
    public async Task Unlike_ReturnsZeroedState()
    {
        var (controller, _, _) = Create();

        var result = await controller.Unlike(3);

        var ok = Assert.IsType<OkObjectResult>(result);
        var state = Assert.IsType<CommentLikeState>(ok.Value);
        Assert.Equal(0, state.LikeCount);
        Assert.False(state.IsLiked);
    }

    [Fact]
    public async Task ListReplies_ReturnsPagedFlatNodes()
    {
        var (controller, _, _) = Create();

        var result = await controller.ListReplies(9);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<PagedResult<CommentDto>>(ok.Value);
    }

    [Fact]
    public async Task ListReplies_MissingComment_ReturnsNotFound()
    {
        var (controller, service, _) = Create();
        service.ThreadRepliesResult = ServiceResult<PagedResult<CommentDto>>.Fail(CommentService.CommentNotFound);

        var result = await controller.ListReplies(9);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task Count_Anonymous_PassesNullUserIdAndReturnsCount()
    {
        var (controller, service, user) = Create();
        user.UserId = null;

        var result = await controller.Count("song", 7);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Null(service.LastCountUserId);   // 未登录必须透传 null：隐藏评论的可见性靠它过滤
        Assert.Contains("3", ok.Value?.ToString());
    }

    [Fact]
    public async Task Count_InvalidTargetType_ReturnsBadRequest()
    {
        var (controller, service, _) = Create();
        service.CountResult = ServiceResult<int>.Fail("评论对象类型不正确。");

        Assert.IsType<BadRequestObjectResult>(await controller.Count("album", 7));
    }
}
