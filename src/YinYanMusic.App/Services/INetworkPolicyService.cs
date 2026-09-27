namespace YinYanMusic.App.Services;

/// <summary>
/// 网络策略检测。Android 用官方 <c>ConnectivityManager.RestrictBackgroundStatus</c>
/// 检测省流量模式（Data Saver）；其他平台用 <see cref="NoopNetworkPolicyService"/> 不限制。
/// </summary>
public interface INetworkPolicyService
{
    /// <summary>系统是否开启了省流量模式（Data Saver）。</summary>
    bool IsDataSaverEnabled { get; }
}