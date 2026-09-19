using YinYanMusic.Core.Entities;

namespace YinYanMusic.Application;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}