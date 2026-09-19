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
    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var s = settings.Value;
        var expires = DateTime.UtcNow.AddMinutes(s.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            // ⚠️ 必须有角色声明，否则 [Authorize(Roles = "admin")] 会把所有人都拒掉（403）
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(user.Role) ? "user" : user.Role),
            new("display_name", user.DisplayName)
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
