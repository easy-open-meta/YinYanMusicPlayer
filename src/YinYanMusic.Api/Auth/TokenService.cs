using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using YinYanMusic.Application;
using YinYanMusic.Core.Entities;

namespace YinYanMusic.Api.Auth;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "YinYanMusic.Api";
    public string Audience { get; set; } = "YinYanMusic.App";
    public string SecretKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60 * 24 * 7;
}

public class TokenService(IOptions<JwtSettings> settings) : ITokenService
{
    public (string Token, DateTime ExpiresAt) CreateToken(User user, TimeSpan? lifetime = null)
    {
        var s = settings.Value;
        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(s.ExpiryMinutes));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("display_name", user.DisplayName),
            // 角色：后台 [Authorize(Roles="admin")] 全靠它。以前没写，
            // 所以 api/catalog/categories 的 admin 校验对所有人都 403。
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(user.Role) ? "user" : user.Role),
            // 令牌版本（V2.5）：鉴权时与库中 Users.TokenVersion 比对，
            // 改密会让版本自增 → 旧 token 立即失效（见 AuthService.ChangePasswordAsync）。
            new(ITokenService.TokenVersionClaim, user.TokenVersion.ToString())
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(s.SecretKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: s.Issuer,
            audience: s.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
