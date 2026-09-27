using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using YinYanMusic.Application;
using YinYanMusic.Data;

namespace YinYanMusic.Api.Auth;

/// <summary>
/// JWT 令牌版本校验（V2.5）。
///
/// <para>
/// 无状态 JWT 本身无法"主动作废"：用户改了密码，旧 token 在过期前（App 端 7 天）依然可用。
/// 这里在鉴权管道里补一道比对：签发时把用户当时的 <c>TokenVersion</c> 写进 <c>tv</c> 声明，
/// 每次请求拿它和数据库当前值比，不一致就判令牌失效。改密时版本 +1 → 旧 token 全部立刻失效。
/// 代价是每请求多一次轻量查询（只取一列），相比维护黑名单/引入 Redis 要简单得多。
/// </para>
///
/// <para>
/// ⚠️ 兼容存量 token：本次改动之前签发的 token 没有 <c>tv</c> 声明。
/// 缺失时按 <c>0</c> 处理（所有存量用户的 TokenVersion 默认就是 0），
/// 这样升级不会把所有人踢下线；用户一旦改密，版本变 1，旧 token 才失效。
/// </para>
/// </summary>
public static class TokenVersionValidator
{
    public static void Attach(JwtBearerOptions o)
    {
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var principal = ctx.Principal;
                if (principal is null) return;

                // 取用户 id：TokenService 里同时写了 sub 与 NameIdentifier，任取其一
                var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? principal.FindFirstValue("sub");
                if (!long.TryParse(idClaim, out var userId)) return;

                // 令牌里带的版本；缺失（存量 token）按 0 处理
                var raw = principal.FindFirstValue(ITokenService.TokenVersionClaim);
                var tokenVersion = int.TryParse(raw, out var v) ? v : 0;

                var db = ctx.HttpContext.RequestServices.GetRequiredService<MusicDbContext>();
                // 只查需要的一列，且用 AsNoTracking —— 这是每请求都要跑的路径，尽量轻
                var current = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.TokenVersion, u.IsDisabled })
                    .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);

                // 账号已被删除 / 停用：直接判失效（停用后旧 token 也不该继续用）
                if (current is null || current.IsDisabled)
                {
                    ctx.Fail("账号不可用，请重新登录。");
                    return;
                }

                if (current.TokenVersion != tokenVersion)
                    ctx.Fail("密码已变更，请重新登录。");
            }
        };
    }
}
