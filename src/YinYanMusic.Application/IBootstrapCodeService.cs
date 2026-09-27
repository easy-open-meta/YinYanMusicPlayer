namespace YinYanMusic.Application;

/// <summary>
/// 超级管理员「一次性引导码」：<b>超管是系统里唯一且不可再建的账号</b>，
/// 谁先打开后台注册页谁就拿到它，所以必须用只有服务器知道的秘密再加一道锁。
///
/// 接口定义在 Application 层、实现在 Api 层（与 <see cref="ITokenService"/> 同样的套路），
/// 因为码存在服务器磁盘上、属于宿主环境的事。
/// </summary>
public interface IBootstrapCodeService
{
    /// <summary>启动时调用：<paramref name="hasAdmin"/> 为 false 时确保码存在，为 true 时清掉残留文件。</summary>
    void Ensure(bool hasAdmin);

    /// <summary>校验客户端提交的码。无效/未生成/不需要校验时返回值含义见实现。</summary>
    bool Verify(string? code);

    /// <summary>超管创建成功后调用：立即销毁，避免一次性码长期留在磁盘上。</summary>
    void Consume();

    /// <summary>当前是否存在可用的引导码（供启动日志/安装向导展示）。</summary>
    bool HasCode { get; }
}
