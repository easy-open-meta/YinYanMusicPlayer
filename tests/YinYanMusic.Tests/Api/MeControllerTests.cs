using Microsoft.AspNetCore.Mvc;
using YinYanMusic.Api.Controllers;
using YinYanMusic.Application;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.Tests.Api;

public class MeControllerTests
{
    private sealed class FakeMeService : IMeService
    {
        public object Overview { get; set; } = new { liked = 1 };
        public List<PlaylistDto> Playlists { get; set; } = [];
        public ServiceResult FollowResult { get; set; } = ServiceResult.Ok();
        public ServiceResult UnfollowResult { get; set; } = ServiceResult.Ok();
        public ServiceResult FollowArtistResult { get; set; } = ServiceResult.Ok();
        public ServiceResult UnfollowArtistResult { get; set; } = ServiceResult.Ok();

        public Task<object> GetOverviewAsync(long userId) => Task.FromResult(Overview);
        public Task<IReadOnlyList<PlaylistDto>> GetMyPlaylistsAsync(long userId) =>
            Task.FromResult<IReadOnlyList<PlaylistDto>>(Playlists);
        public Task<IReadOnlyList<ArtistDto>> GetFollowedArtistsAsync(long userId) =>
            Task.FromResult<IReadOnlyList<ArtistDto>>(new List<ArtistDto>());
        public Task<IReadOnlyList<UserDto>> GetFollowedUsersAsync(long userId) =>
            Task.FromResult<IReadOnlyList<UserDto>>(new List<UserDto>());
        public Task<IReadOnlyList<UserDto>> GetFollowersAsync(long userId) =>
            Task.FromResult<IReadOnlyList<UserDto>>(new List<UserDto>());
        public Task<ServiceResult> FollowAsync(long userId, long targetUserId) => Task.FromResult(FollowResult);
        public Task<ServiceResult> UnfollowAsync(long userId, long targetUserId) => Task.FromResult(UnfollowResult);
        public Task<IReadOnlyList<long>> GetFollowedArtistIdsAsync(long userId) =>
            Task.FromResult<IReadOnlyList<long>>(new List<long>());
        public Task<ServiceResult> FollowArtistAsync(long userId, long artistId) => Task.FromResult(FollowArtistResult);
        public Task<ServiceResult> UnfollowArtistAsync(long userId, long artistId) => Task.FromResult(UnfollowArtistResult);
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public long? UserId { get; set; } = 42;

        public long RequireUserId() => UserId ?? throw new UnauthorizedAccessException("未登录");
    }

    private static (MeController Controller, FakeMeService Service) CreateController()
    {
        var service = new FakeMeService();
        var controller = new MeController(service, new FakeCurrentUser());
        return (controller, service);
    }

    [Fact]
    public async Task Overview_ReturnsOkWithData()
    {
        var (controller, _) = CreateController();

        var result = await controller.Overview();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(ok.Value);
    }

    [Fact]
    public async Task MyPlaylists_ReturnsOkWithList()
    {
        var (controller, service) = CreateController();
        service.Playlists =
        [
            new PlaylistDto(1, "我的歌单", null, null, null, null, 42, "Alice", 3, 5, DateTime.UtcNow, false)
        ];

        var result = await controller.MyPlaylists();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<PlaylistDto>>(ok.Value);
        Assert.Single(list);
        Assert.Equal("我的歌单", list[0].Name);
    }

    [Fact]
    public async Task FollowUser_Success_ReturnsNoContent()
    {
        var (controller, service) = CreateController();
        service.FollowResult = ServiceResult.Ok();

        var result = await controller.FollowUser(7);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task FollowUser_Failure_ReturnsBadRequest()
    {
        var (controller, service) = CreateController();
        service.FollowResult = ServiceResult.Fail("不能关注自己");

        var result = await controller.FollowUser(42);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(bad.Value);
    }

    [Fact]
    public async Task UnfollowUser_Failure_ReturnsNotFound()
    {
        var (controller, service) = CreateController();
        service.UnfollowResult = ServiceResult.Fail("未关注该用户");

        var result = await controller.UnfollowUser(7);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task FollowArtist_Success_ReturnsNoContent()
    {
        var (controller, _) = CreateController();

        var result = await controller.FollowArtist(9);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task UnfollowArtist_Failure_ReturnsNotFound()
    {
        var (controller, service) = CreateController();
        service.UnfollowArtistResult = ServiceResult.Fail("未关注该歌手");

        var result = await controller.UnfollowArtist(9);

        Assert.IsType<NotFoundResult>(result);
    }
}