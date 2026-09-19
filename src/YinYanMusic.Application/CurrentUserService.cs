using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace YinYanMusic.Application;

public interface ICurrentUserService
{
    long? UserId { get; }
    long RequireUserId();
}

public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private long? _userId;

    public long? UserId
    {
        get
        {
            if (_userId.HasValue) return _userId;
            var claim = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            _userId = long.TryParse(claim, out var id) ? id : null;
            return _userId;
        }
    }

    public long RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException("未登录或令牌无效。");
}