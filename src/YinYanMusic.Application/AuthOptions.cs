namespace YinYanMusic.Application;

/// <summary>
/// 账号相关开关（配置节 <c>Auth</c>）。
/// 由 Program.cs 从配置绑定后以单例注入 —— 不引 <c>IOptions</c>，
/// 避免为两个开关给 Application 层加依赖。
/// </summary>
public sealed class AuthOptions
{
    /// <summary>是否允许 App（用户端）自助注册。默认 true —— App 是用户端，注册必须开放。</summary>
    public bool AllowAppRegistration { get; set; } = true;

    /// <summary>
    /// 后台注册页创建超管时是否校验一次性引导码。默认 true。
    /// 关掉后回到"谁都能打开 /register 抢建超管"的状态，仅本机开发时可关。
    /// </summary>
    public bool RequireBootstrapCode { get; set; } = true;
}
