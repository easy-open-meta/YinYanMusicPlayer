using System.Reflection;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 「关于」页（汉堡菜单 → 关于）：应用名称、版本号、构建信息、平台、简介与版权信息。
/// </summary>
public partial class AboutViewModel : ObservableObject
{
    public string AppName => "音言音乐";

    /// <summary>应用版本号（来自 csproj 的 ApplicationDisplayVersion），后附当前平台与 CPU 架构。</summary>
    public string Version => $"v{AppInfo.Current.VersionString} ({GetPlatformName()} {RuntimeInformation.ProcessArchitecture})";

    /// <summary>构建号（来自 csproj 的 ApplicationVersion）。</summary>
    public string Build => $"Build {AppInfo.Current.BuildString}";

    /// <summary>程序集构建时间（由 csproj 注入的 BuildDate 元数据提供，UTC）。</summary>
    public string BuildDate
    {
        get
        {
            var attr = typeof(AboutViewModel).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "BuildDate");
            return attr?.Value is { Length: > 0 } value ? $"构建于 {value} (UTC)" : string.Empty;
        }
    }

    /// <summary>当前运行平台与系统版本。</summary>
    public string Platform => $"{GetPlatformName()} {DeviceInfo.Current.VersionString}";

    private static string GetPlatformName()
    {
        var current = DeviceInfo.Current.Platform;
        return current == DevicePlatform.Android ? "Android"
            : current == DevicePlatform.WinUI ? "Windows"
            : current.ToString();
    }

    public string Description =>
        "一款现代化跨平台音乐播放器，支持 Windows 与 Android 双端。\n" +
        "服务端基于 ASP.NET Core + PostgreSQL，支持在线曲库、定时扫描本地音乐、" +
        "歌词、歌单、收藏、关注等完整音乐社区能力。";

    public string Copyright => $"© {DateTime.Now.Year} 易开元(EOM) 保留所有权利";

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");
}