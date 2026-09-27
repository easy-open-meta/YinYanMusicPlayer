namespace YinYanMusic.App.Services;

/// <summary>非 Android 平台空实现：不限制（Windows 不涉及系统省流量模式）。</summary>
public sealed class NoopNetworkPolicyService : INetworkPolicyService
{
    public bool IsDataSaverEnabled => false;
}