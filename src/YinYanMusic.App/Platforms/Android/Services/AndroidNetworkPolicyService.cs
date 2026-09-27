using Android.Content;
using Android.Net;

namespace YinYanMusic.App.Services;

/// <summary>
/// Android 省流量模式检测：基于官方 <c>ConnectivityManager.RestrictBackgroundStatus</c>。
/// 「播完自动缓存」等非用户主动发起的大流量下载应尊重该开关，避免偷跑用户流量。
/// </summary>
public sealed class AndroidNetworkPolicyService : INetworkPolicyService
{
    public bool IsDataSaverEnabled
    {
        get
        {
            try
            {
                var cm = (ConnectivityManager?)Android.App.Application.Context.GetSystemService(Context.ConnectivityService);
                return cm is not null && cm.RestrictBackgroundStatus == RestrictBackgroundStatus.Enabled;
            }
            catch
            {
                return false;   // 判断不出来按未开启处理（调用方还有 WiFi 判断兜底）
            }
        }
    }
}