using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using WinUIApp = Microsoft.UI.Xaml.Application;
using WinUIWindow = Microsoft.UI.Xaml.Window;

namespace YinYanMusic.App.Services;

/// <summary>
/// Windows 专属：把主窗口的「关闭」换行为「隐藏到托盘」，音乐继续播（「偏好设置」里可开关）。
/// </summary>
/// <remarks>
/// <para>为什么不用 MAUI 的 <c>Window.Destroying</c>：那个事件只是"知道窗口要没了"，改不了结果。
/// 要拦住关闭只能落到平台层 —— WinUI 3 的 <see cref="AppWindow.Closing"/> 是唯一能取消的钩子。</para>
///
/// <para>拦下来之后窗口是**藏起来**（<c>SW_HIDE</c>）而不是最小化：隐藏后任务栏上不再占位，
/// 只留右下角托盘图标（<see cref="WindowsTrayIcon"/>），这才是"守在托盘里"该有的样子。
/// 它同时覆盖 ✕ / Alt+F4 / 任务栏「关闭窗口」，所以真正的退出只能由托盘菜单或
/// 「偏好设置」里的「退出程序」按钮放行一次（<see cref="Exit"/>）。</para>
/// </remarks>
internal static class WindowsWindowCloseHook
{
    private static WinUIWindow? _nativeWindow;
    private static nint _hwnd;
    private static bool _exitRequested;

    /// <summary>窗口创建时挂上关闭拦截（由 MauiProgram 的 Windows 生命周期事件调用，只挂一次）。</summary>
    public static void Attach(WinUIWindow nativeWindow)
    {
        if (_nativeWindow is not null) return;
        _nativeWindow = nativeWindow;
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        nativeWindow.AppWindow.Closing += OnClosing;
        SetWindowIcon(nativeWindow.AppWindow);
        AppLog.Info($"[托盘] main hwnd=0x{_hwnd.ToInt64():X}");
    }

    /// <summary>
    /// 标题栏 / 任务栏图标（V2.12）：MAUI 对未打包应用（WindowsPackageType=None）不会自动设置
    /// 窗口图标，标题栏左侧一直是默认的空白图标。AppWindow.SetIcon 接受磁盘上的 .ico 路径 ——
    /// MAUI 的 Resizetizer 已经把 MauiIcon 编译成了 exe 旁边的 icon.ico，直接指过去。
    /// 找不到文件时静默跳过（SetIcon 抛异常会崩启动，不能让它带上主线）。
    /// </summary>
    private static void SetWindowIcon(AppWindow appWindow)
    {
        try
        {
            var exeDir = AppContext.BaseDirectory;
            var icoPath = Path.Combine(exeDir, "icon.ico");
            if (File.Exists(icoPath))
                appWindow.SetIcon(icoPath);
            else
                AppLog.Warn("[标题栏] 未找到 icon.ico，窗口图标保持默认");
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[标题栏] 设置窗口图标失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 「退出程序」：先摘掉托盘图标，再放行一次真正的关闭。
    /// 走 <see cref="WinUIWindow.Close"/> 而不是直接杀进程 —— MAUI 的 OnSleep（保存播放状态、上报进度）挂在正常关闭路径上。
    /// </summary>
    public static void Exit()
    {
        _exitRequested = true;
        WindowsTrayIcon.Remove();
        if (_nativeWindow is null)
        {
            WinUIApp.Current?.Exit();   // 兜底：窗口还没建好（入口在界面里，正常到不了这里）
            return;
        }
        _nativeWindow.Close();
    }

    private static void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_exitRequested) return;                              // 自己请求的退出，放行
        if (!AppPreferences.HideToTrayOnWindowClose) return;     // 偏好关闭：照旧「✕ = 退出程序」

        args.Cancel = true;
        HideToTray();
    }

    /// <summary>隐藏窗口 + 保证托盘图标在（图标只挂一次，之后一直留到退出）。</summary>
    private static void HideToTray()
    {
        if (_hwnd == nint.Zero) return;

        WindowsTrayIcon.EnsureCreated(_hwnd, RestoreFromTray, Exit);
        ShowWindow(_hwnd, SwHide);
        WindowsTrayIcon.ShowHideHintOnce();
        AppLog.Info("[托盘] 关窗口 → 已隐藏到托盘，播放继续");
    }

    /// <summary>托盘左键：把窗口显示回来（此前若是最小化状态则还原，否则直接显示）。</summary>
    private static void RestoreFromTray()
    {
        if (_hwnd == nint.Zero) return;

        ShowWindow(_hwnd, IsIconic(_hwnd) ? SwRestore : SwShow);
        SetForegroundWindow(_hwnd);
        AppLog.Info("[托盘] 已还原主窗口");
    }

    private const int SwHide = 0, SwShow = 5, SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);
}
