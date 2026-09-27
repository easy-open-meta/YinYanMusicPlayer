using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using YinYanMusic.Application;

namespace YinYanMusic.Api.Auth;

/// <summary>
/// 超级管理员「一次性引导码」：码只存在于<b>进程内存</b>，启动时打印到控制台日志。
/// 不写文件、不进数据库、不下发到前端 —— 管理端即便短暂暴露，拿不到码也建不了超管。
///
/// 生命周期：启动时「无超管」→ 生成并打印（docker logs / journalctl 可查）；
/// 超管建成 → Consume() 销毁。容器/服务以只读或受限账号运行时照样工作，
/// 不再依赖 /var/lib 之类的可写目录（文件方案曾因权限被拒导致注册卡死）。
///
/// 取舍：重启后码会重新生成 —— 引导码本来就是"首次部署创建超管"用的一次性秘密，
/// 重启换码无安全损失；`docker logs` 里出现过的旧码随日志留存，但新码才能注册。
/// </summary>
public sealed class BootstrapCodeService(AuthOptions options, ILogger<BootstrapCodeService> logger)
    : IBootstrapCodeService
{
    private readonly object _sync = new();
    private string? _code;

    public bool HasCode => _code is not null;

    public void Ensure(bool hasAdmin)
    {
        lock (_sync)
        {
            if (hasAdmin) { _code = null; return; }          // 已有超管：确保码不存在
            if (!options.RequireBootstrapCode) { _code = null; return; }
            if (_code is null) Create();
        }
    }

    public bool Verify(string? code)
    {
        if (!options.RequireBootstrapCode) return true;
        lock (_sync)
        {
            // 码只在启动时生成（Ensure）；运行期为 null 说明配置关了校验或已销毁，直接拒绝
            if (_code is null || string.IsNullOrWhiteSpace(code)) return false;
            return FixedTimeEquals(_code, code.Trim());
        }
    }

    public void Consume()
    {
        lock (_sync)
        {
            _code = null;
        }
    }

    private void Create()
    {
        _code = Generate();
        logger.LogWarning(
            "[引导码] 系统还没有超级管理员。创建超管需要这个一次性引导码：{Code}（仅本次运行有效，超管创建成功后即销毁；重启会重新生成）",
            _code);
    }

    private static string Generate()
    {
        var hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToUpperInvariant();
        return string.Join('-', Enumerable.Range(0, 5).Select(i => hex.Substring(i * 4, 4)));
    }

    /// <summary>先哈希再比较：无论输入多长都比 32 字节，避免长度泄漏与长度分支的时序差。</summary>
    private static bool FixedTimeEquals(string expected, string actual)
    {
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(actual));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
