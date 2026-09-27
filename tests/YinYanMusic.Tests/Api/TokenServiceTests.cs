using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using YinYanMusic.Api.Auth;
using YinYanMusic.Application;
using YinYanMusic.Core.Entities;

namespace YinYanMusic.Tests.Api;

public class TokenServiceTests
{
    private static TokenService CreateService(string secretKey, int expiryMinutes = 60)
    {
        var settings = Options.Create(new JwtSettings
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SecretKey = secretKey,
            ExpiryMinutes = expiryMinutes
        });
        return new TokenService(settings);
    }

    private static User CreateUser() => new()
    {
        Id = 42,
        UserName = "alice",
        DisplayName = "Alice",
        Role = "admin",
        TokenVersion = 3
    };

    [Fact]
    public void CreateToken_ReturnsNonEmptyToken()
    {
        var service = CreateService(new string('k', 64));
        var (token, _) = service.CreateToken(CreateUser());

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void CreateToken_ContainsExpectedClaims()
    {
        var service = CreateService(new string('k', 64));
        var (token, _) = service.CreateToken(CreateUser());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("42", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("alice", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value);
        Assert.Equal("admin", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("3", jwt.Claims.First(c => c.Type == ITokenService.TokenVersionClaim).Value);
        Assert.Equal("Alice", jwt.Claims.First(c => c.Type == "display_name").Value);
    }

    [Fact]
    public void CreateToken_DefaultLifetime_UsesConfiguredExpiry()
    {
        var service = CreateService(new string('k', 64), expiryMinutes: 120);
        var (_, expires) = service.CreateToken(CreateUser());

        Assert.True(expires > DateTime.UtcNow.AddMinutes(119));
        Assert.True(expires <= DateTime.UtcNow.AddMinutes(120).AddSeconds(5));
    }

    [Fact]
    public void CreateToken_CustomLifetime_OverridesDefault()
    {
        var service = CreateService(new string('k', 64), expiryMinutes: 120);
        var (_, expires) = service.CreateToken(CreateUser(), lifetime: TimeSpan.FromHours(8));

        Assert.True(expires > DateTime.UtcNow.AddHours(7.9));
        Assert.True(expires <= DateTime.UtcNow.AddHours(8).AddSeconds(5));
    }

    [Fact]
    public void CreateToken_DefaultRole_IsUser()
    {
        var service = CreateService(new string('k', 64));
        var user = CreateUser();
        user.Role = "";

        var (token, _) = service.CreateToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("user", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }
}