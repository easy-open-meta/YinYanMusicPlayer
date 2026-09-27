using System.Runtime.InteropServices;
using YinYanMusic.App.Services;

namespace YinYanMusic.App.Services;

/// <summary>
/// Windows 专属：右下角**通知区域（托盘）图标**。
///
/// <para>「隐藏到托盘」必须自己挂 <c>Shell_NotifyIcon</c> —— MAUI / WinUI 3 都没有托盘支持，
/// 所以这里是一小段 Win32：托盘图标 → 消息 →（左键还原 / 右键菜单 → 退出）。
/// 刻意不引第三方托盘库：整套逻辑只有"一个图标 + 一个菜单"，不值得为它多一条依赖。</para>
///
/// <para>⚠️ 回调走**我们自己创建的隐藏窗口**，而不是主窗口：WinUI 3 的窗口过程由
/// WindowImpl 自己管着（实测外部 <c>SetWindowLongPtr</c> 换掉的过程会被它重新装回去，
/// 消息就漏了），所以另起一个只用来收托盘消息的窗口最稳 —— 顺带也不用碰 WinUI 的消息处理。</para>
/// </summary>
internal static class WindowsTrayIcon
{
    /// <summary>托盘回调消息：WM_APP + 1（lParam 低字是鼠标/通知事件）。</summary>
    private const uint WmTrayIcon = 0x8000 + 1;

    private const string TrayWindowClass = "YinYanMusic.TrayWindow";
    private const string TrayWindowTitle = "YinYanMusic.TrayWindow";
    private const uint TrayId = 1;
    private const uint CmdRestore = 1;
    private const uint CmdExit = 2;

    private static nint _hwnd;          // 只用来收托盘消息的隐藏窗口
    private static nint _ownerHwnd;     // 主窗口，仅用于菜单归属
    private static nint _icon;
    private static bool _ownsIcon;
    private static bool _added;
    private static bool _hideHintShown;
    private static uint _taskbarCreatedMessage;
    private static Action? _onRestore;
    private static Action? _onExit;

    // 必须留强引用：委托被 GC 之后，窗口消息还会跳进那块已释放的内存
    private static WndProcDelegate? _wndProc;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    /// <summary>
    /// 确保托盘图标存在（首次隐藏时调用，之后保持到退出——用户随时可以再右键退出）。
    /// <paramref name="onRestore"/> 与 <paramref name="onExit"/> 都跑在**界面线程**上
    /// （托盘消息发到我们自己的窗口，而窗口是在界面线程上建的），所以可以直接操作主窗口。
    /// </summary>
    /// <param name="ownerHwnd">主窗口句柄，仅用于菜单归属（弹菜单时它得能当前台窗口）。</param>
    public static void EnsureCreated(nint ownerHwnd, Action onRestore, Action onExit)
    {
        _onRestore = onRestore;
        _onExit = onExit;

        if (_added) return;

        if (_hwnd == nint.Zero) _hwnd = CreateTrayWindow();
        if (_hwnd == nint.Zero)
        {
            AppLog.Warn("[托盘] 消息窗口创建失败，托盘图标不可用");
            return;
        }

        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
        _icon = LoadAppIcon();
        _ownerHwnd = ownerHwnd;
        if (AddIcon()) _added = true;
    }

    /// <summary>移除托盘图标并销毁消息窗口（退出前一定要调，否则托盘里会留下"幽灵图标"）。</summary>
    public static void Remove()
    {
        if (_added)
        {
            var data = NewData(NifMessage | NifIcon | NifTip);
            Shell_NotifyIconW(NimDelete, ref data);
            _added = false;
        }

        if (_hwnd != nint.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = nint.Zero;
        }

        if (_ownsIcon && _icon != nint.Zero) DestroyIcon(_icon);
        _icon = nint.Zero;
        _ownsIcon = false;
        _wndProc = null;
    }

    /// <summary>
    /// 第一次隐藏时提示一句 —— 窗口"消失"之后用户得知道去哪儿找它，
    /// 之后不再打扰（同一次运行里只说一次）。
    /// </summary>
    public static void ShowHideHintOnce()
    {
        if (_hideHintShown || !_added) return;
        _hideHintShown = true;

        var data = NewData(NifInfo | NifIcon);
        data.szInfoTitle = "音言音乐已隐藏到托盘";
        data.szInfo = "音乐继续播放。左键单击托盘图标可以找回窗口，右键可以退出程序。";
        data.dwInfoFlags = NiifInfo;
        if (!Shell_NotifyIconW(NimModify, ref data))
            AppLog.Warn("[托盘] 气泡提示发送失败");
    }

    private static void OnRestoreRequested()
    {
        AppLog.Info("[托盘] 单击图标 → 还原窗口");
        _onRestore?.Invoke();
    }

    private static void OnExitRequested()
    {
        AppLog.Info("[托盘] 菜单选择退出程序");
        _onExit?.Invoke();
    }

    private static nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        // 资源管理器重启会把托盘清空，收到它就得重新挂一次
        if (_taskbarCreatedMessage != 0 && msg == _taskbarCreatedMessage)
        {
            AppLog.Info("[托盘] 资源管理器重启，重新挂上图标");
            AddIcon();
            return nint.Zero;
        }

        // 我们自己销毁窗口（退出时）不要走 DefWindowProc：它是顶层窗口，DefWindowProc 会顺手
        // PostQuitMessage，把 WM_QUIT 塞进共享的消息队列
        if (msg == WmDestroy) return nint.Zero;

        if (msg == WmTrayIcon)
        {
            var evt = (uint)(lParam.ToInt64() & 0xFFFF);
            switch (evt)
            {
                // 旧协议（未调 NIM_SETVERSION）直接发鼠标消息；
                // 新协议（NOTIFYICON_VERSION_4）发 NIN_SELECT / NIN_KEYSELECT / WM_CONTEXTMENU。
                // 两种都认，免得不同 Windows 版本上"点了没反应"。
                case WmLeftButtonUp:
                case WmLeftButtonDoubleClick:
                case NinSelect:
                case NinKeySelect:
                    OnRestoreRequested();
                    return nint.Zero;
                case WmRightButtonUp:
                case WmContextMenu:
                    ShowMenu();
                    return nint.Zero;
            }

            // 鼠标移动之类的高频事件直接丢；其余（气泡通知等）记一条，托盘"没反应"时靠它判断
            // 消息到底有没有送到我们窗口
            if (evt >= NinBalloonShow)
                AppLog.Info($"[托盘] 托盘事件 0x{evt:X}");
            return nint.Zero;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    /// <summary>右键菜单：还原窗口 / 退出程序。</summary>
    private static void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == nint.Zero) return;

        try
        {
            AppendMenuW(menu, MfString | MfDefault, CmdRestore, "显示主窗口");
            AppendMenuW(menu, MfSeparator, 0, null);
            AppendMenuW(menu, MfString, CmdExit, "退出程序");

            if (!GetCursorPos(out var pt)) return;

            // 弹出菜单的经典要求：先把自己设成前台窗口，否则点别处菜单不消失。
            // 拿主窗口当归属（它虽然是隐藏的，但仍是这个应用唯一的"正经"窗口）。
            var owner = _ownerHwnd != nint.Zero ? _ownerHwnd : _hwnd;
            SetForegroundWindow(owner);

            var cmd = TrackPopupMenu(menu, TpmReturnCmd | TpmRightButton | TpmNoNotify,
                                     pt.X, pt.Y, 0, owner, nint.Zero);
            PostMessageW(owner, WmNull, nint.Zero, nint.Zero);

            if (cmd == CmdRestore) OnRestoreRequested();
            else if (cmd == CmdExit) OnExitRequested();
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    /// <summary>建一个永不显示的顶层窗口，只用来收托盘回调（它属于界面线程，消息由主消息循环派发）。</summary>
    private static nint CreateTrayWindow()
    {
        var hInstance = GetModuleHandleW(null);
        _wndProc = WndProc;

        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = TrayWindowClass,
        };

        if (RegisterClassExW(ref wc) == 0)
            AppLog.Warn($"[托盘] 窗口类注册失败，错误={Marshal.GetLastWin32Error()}");

        var hwnd = CreateWindowExW(0, TrayWindowClass, TrayWindowTitle, WsPopup,
                                   0, 0, 0, 0, nint.Zero, nint.Zero, hInstance, nint.Zero);
        AppLog.Info($"[托盘] tray hwnd=0x{hwnd.ToInt64():X} (0 = 创建失败)");
        return hwnd;
    }

    private static bool AddIcon()
    {
        var data = NewData(NifMessage | NifIcon | NifTip);
        data.uCallbackMessage = WmTrayIcon;
        data.szTip = "音言音乐";

        var ok = Shell_NotifyIconW(NimAdd, ref data);
        AppLog.Info(ok ? "[托盘] 图标已挂上" : "[托盘] 图标挂载失败（Shell_NotifyIcon 返回 false）");
        return ok;
    }

    private static NOTIFYICONDATA NewData(uint flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = TrayId,
        uFlags = flags,
        hIcon = _icon,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    /// <summary>用 exe 自带的图标（MauiIcon 编译进可执行文件的那个）；取不到就退回系统默认图标。</summary>
    private static nint LoadAppIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe)
                && ExtractIconExW(exe, 0, out var large, out var small, 1) > 0)
            {
                if (large != nint.Zero) DestroyIcon(large);
                if (small != nint.Zero) { _ownsIcon = true; return small; }
            }
            AppLog.Warn("[托盘] 取不到 exe 图标，改用系统默认图标");
        }
        catch (Exception ex)
        {
            AppLog.Warn($"[托盘] 提取图标异常：{ex.Message}");
        }

        return LoadIconW(nint.Zero, IdiApplication);
    }

    // ── Win32 ────────────────────────────────────────────────────────────────

    private const uint NimAdd = 0, NimModify = 1, NimDelete = 2;
    private const uint NifMessage = 0x1, NifIcon = 0x2, NifTip = 0x4, NifInfo = 0x10;
    private const uint NiifInfo = 0x1;
    private const uint WmLeftButtonUp = 0x0202, WmLeftButtonDoubleClick = 0x0203, WmRightButtonUp = 0x0205;
    private const uint WmContextMenu = 0x007B;
    private const uint NinSelect = 0x0400, NinKeySelect = 0x0401, NinBalloonShow = 0x0402;
    private const uint WmNull = 0;
    private const uint WmDestroy = 0x0002;
    private const uint MfString = 0x0, MfSeparator = 0x800, MfDefault = 0x1000;
    private const uint TpmReturnCmd = 0x100, TpmRightButton = 0x2, TpmNoNotify = 0x80;
    private const uint WsPopup = 0x80000000;
    private const int IdiApplication = 32512;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconExW(string lpszFile, int nIconIndex,
                                              out nint phiconLarge, out nint phiconSmall, uint nIcons);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName,
                                               uint dwStyle, int x, int y, int nWidth, int nHeight,
                                               nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint LoadIconW(nint hInstance, nint lpIconName);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool PostMessageW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(nint hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(nint hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(nint hMenu, uint uFlags, int x, int y, int nReserved,
                                             nint hWnd, nint prcRect);
}
