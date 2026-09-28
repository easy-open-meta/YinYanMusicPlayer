using YinYanMusic.Core.Entities;

namespace YinYanMusic.Application;

public interface ITokenService
{
    /// <summary>签发令牌。<paramref name="lifetime"/> 为空时用配置里的默认有效期（App 与后台一致：7 天）；
    /// 后台登录 / 后台注册显式传 <see cref="AuthService"/> 里的 <c>AdminTokenLifetime</c>，便于单独调整。</summary>
    (string Token, DateTime ExpiresAt) CreateToken(User user, TimeSpan? lifetime = null);

    /// <summary>
    /// 令牌版本声明名（V2.5）。签发时写入用户当前的 <c>TokenVersion</c>，
    /// 鉴权时与库中值比对：不一致即判失效（改密后旧 token 立即失效）。
    /// </summary>
    const string TokenVersionClaim = "tv";
}