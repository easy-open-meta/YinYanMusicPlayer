using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YinYanMusic.App.Services;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 「偏好设置」页（主界面抽屉 → 偏好设置）：本机行为开关。
/// 与「服务器设置」页刻意分开 —— 那页是"这台设备怎么连服务器"，这页是"这台设备的窗口怎么表现"。
///
/// <para>目前只有一项：关闭窗口时隐藏到托盘、音乐继续播。**仅 Windows**：
/// Android 没有"关闭窗口"这个动作，入口与卡片都不显示（见 <see cref="IsHideToTrayOnWindowCloseSupported"/>）。</para>
/// </summary>
public partial class PreferencesViewModel : ObservableObject
{
    /// <summary>关闭窗口时隐藏到托盘（写 Preferences，拨一下就生效，不用点保存）。</summary>
    [ObservableProperty] private bool _hideToTrayOnWindowClose;

    /// <summary>开关的操作反馈，说明当前状态下点 ✕ 会发生什么。</summary>
    [ObservableProperty] private string _hideToTrayHint = string.Empty;

    /// <summary>当前平台是否支持该偏好。入口与卡片都看它，Android 上整块不显示。</summary>
    public bool IsHideToTrayOnWindowCloseSupported { get; } = AppPreferences.IsHideToTrayOnWindowCloseSupported;

    public PreferencesViewModel()
    {
        // 直接写字段：构造时不要把读出来的值又回写一遍 Preferences
        _hideToTrayOnWindowClose = AppPreferences.HideToTrayOnWindowClose;
        _hideToTrayHint = Describe(_hideToTrayOnWindowClose);
    }

    partial void OnHideToTrayOnWindowCloseChanged(bool value)
    {
        AppPreferences.HideToTrayOnWindowClose = value;
        HideToTrayHint = Describe(value);
    }

    private static string Describe(bool enabled) => enabled
        ? "已开启：点 ✕ 后窗口收进右下角托盘，音乐继续播；左键单击托盘图标还原窗口，右键可退出程序。"
        : "已关闭：点 ✕ 会退出程序，音乐也随之停止。";

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    /// <summary>
    /// 退出程序（Windows 专属）：开启"关窗口隐藏到托盘"之后，✕ 与任务栏的「关闭窗口」都不会再退出，
    /// 这里是不用去关开关、也不用任务管理器的出口之一（另一个是托盘图标的右键菜单）。
    /// </summary>
    [RelayCommand]
    private static async Task ExitAppAsync()
    {
        var confirm = await SongMenuHelper.ShowRoundedConfirmAsync(
            "退出程序", "将停止播放并关闭音言音乐。", "退出", "取消", destructive: true);
        if (!confirm) return;
#if WINDOWS
        WindowsWindowCloseHook.Exit();
#endif
    }
}
