namespace YinYanMusic.App.Services;

/// <summary>
/// 客户端本地偏好（跟着设备走，与 <see cref="ApiConfigStore"/> / CacheStore 一样落在 Preferences 上）。
/// 这里只放**纯本机行为**的开关：与服务器无关，也不需要账号。
/// </summary>
public static class AppPreferences
{
    private const string HideToTrayOnWindowCloseKey = "yinyan.window.hideToTrayOnClose";

    /// <summary>
    /// 关闭窗口时**隐藏到托盘**（任务栏右下角通知区域）、音乐继续播放。
    ///
    /// <para>隐藏后任务栏上不再有窗口，只有托盘图标：左键单击还原窗口，右键菜单可以退出程序。
    /// 默认关闭：点 ✕ 就是退出程序，是用户既有的预期；要"关窗口不打断听歌"由用户自己打开。
    /// 关掉这个开关后，窗口恢复成"✕ = 退出程序"。</para>
    ///
    /// <para>⚠️ 只有 Windows 有「关闭窗口」这个动作，Android 上该偏好恒为 false
    /// （见 <see cref="IsHideToTrayOnWindowCloseSupported"/>）。</para>
    /// </summary>
    public static bool HideToTrayOnWindowClose
    {
        get => Preferences.Default.Get(HideToTrayOnWindowCloseKey, false);
        set => Preferences.Default.Set(HideToTrayOnWindowCloseKey, value);
    }

    /// <summary>
    /// 当前平台是否支持「关闭窗口隐藏到托盘」——**只有 Windows 支持**。
    /// 抽屉入口、偏好设置页的卡片都用它决定显不显示：Android 上这个功能整块不存在。
    /// </summary>
    public static bool IsHideToTrayOnWindowCloseSupported => DeviceInfo.Platform == DevicePlatform.WinUI;
}
