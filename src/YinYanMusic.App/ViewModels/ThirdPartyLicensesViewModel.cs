using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YinYanMusic.App.Services;

namespace YinYanMusic.App.ViewModels;

/// <summary>「第三方开源组件引用」页（汉堡菜单 → 第三方开源组件引用）：列出项目依赖的主要开源组件及其许可证。</summary>
public partial class ThirdPartyLicensesViewModel : ObservableObject
{
    public IReadOnlyList<OpenSourceComponent> Components { get; } =
    [
        new(".NET MAUI", "MIT", "跨平台 UI 框架，用于构建 Android / Windows 客户端"),
        new("ASP.NET Core", "MIT", "服务端 Web API 框架"),
        new("SignalR (Microsoft.AspNetCore.SignalR)", "MIT", "站内通知的实时推送通道（服务端 Hub + 客户端 SignalR.Client）"),
        new("Entity Framework Core", "MIT", "ORM 数据访问层"),
        new("Npgsql", "MIT", "PostgreSQL 数据库驱动"),
        new("BCrypt.Net-Next", "MIT", "用户密码哈希存储"),
        new("MailKit", "MIT", "发送邮箱验证码（账号安全中心的邮箱绑定）"),
        new("Scalar.AspNetCore", "MIT", "服务端接口文档页面，部署后可直接查看接口"),
        new("CommunityToolkit.Maui", "MIT", "MAUI 工具包（媒体元素、行为等）"),
        new("CommunityToolkit.Maui.MediaElement", "MIT", "跨平台媒体播放组件（Android 端接入 ExoPlayer）"),
        new("CommunityToolkit.Mvvm", "MIT", "MVVM 工具包（ObservableObject / RelayCommand）"),
        new("ATL (z440.atl.core)", "MIT", "音频标签与元数据解析（标题/歌手/专辑/内嵌封面/时长）"),
        new("sqlite-net-pcl", "MIT", "本地 SQLite 数据库（本地曲库与缓存索引）"),
        new("ExoPlayer (Media3)", "Apache-2.0", "Android 媒体播放内核"),
        new("Xamarin.AndroidX.Media", "MIT AND Apache-2.0", "Android 播放通知的媒体样式（androidx.media 的 MediaStyle）"),
    ];

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    /// <summary>
    /// 点卡片看协议全文：按该组件的许可证表达式取包内原文。
    /// 单一许可证取一份；表达式是 <c>MIT AND Apache-2.0</c> 的要取两份（如 Xamarin.AndroidX.Media）。
    /// </summary>
    [RelayCommand]
    private async Task ShowLicenseAsync(OpenSourceComponent component)
    {
        var candidates = new List<string>();
        if (component.License.Contains("MIT", StringComparison.Ordinal)) candidates.Add("MIT.txt");
        if (component.License.Contains("Apache-2.0", StringComparison.Ordinal)) candidates.Add("Apache-2.0.txt");

        var parts = new List<string>();
        foreach (var file in candidates)
        {
            var text = await ReadLicenseTextAsync(file);
            if (!string.IsNullOrWhiteSpace(text)) parts.Add(text.Trim());
        }

        if (parts.Count == 0)
        {
            // 资源没打进包（构建/打包问题）时给一句人话，而不是抛异常崩掉
            await SongMenuHelper.ShowMessageDialogAsync("未找到协议原文", $"包内没有该组件的许可证原文（{component.License}）。");
            return;
        }

        await SongMenuHelper.ShowLongTextDialogAsync(
            $"{component.Name} · {component.License}",
            string.Join($"{Environment.NewLine}{Environment.NewLine}", parts),
            // MIT 原文里第一段是 <year> <copyright holders> 占位：逐包的版权声明随各自发布包提供，
            // 这里不编造具体的版权主体。
            note: "版权声明随各自发布包提供，此处为许可证原文。");
    }

    /// <summary>
    /// 读包内协议原文。文件放在 <c>Resources/Raw/Licenses/</c>，csproj 的 <c>MauiAsset</c> 保留子目录，
    /// 所以资源名正好是 <c>Licenses/xxx.txt</c>（与 <see cref="Services.BuiltInAvatars"/> 同一约定）。
    /// ⚠️ Windows 是**非打包**形态（<c>WindowsPackageType=None</c>），
    /// <c>OpenAppPackageFileAsync</c> 可能取不到，要退回应用目录下的同名相对路径 —— 同
    /// <c>BuiltInAvatars.OpenAsync</c> 的处理。读不到返回 null 由调用方提示，不抛异常。
    /// </summary>
    private static async Task<string?> ReadLicenseTextAsync(string fileName)
    {
        var path = $"Licenses/{fileName}";
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(path);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, "Licenses", fileName);
            return File.Exists(fallback) ? await File.ReadAllTextAsync(fallback) : null;
        }
    }
}

/// <summary>开源组件条目：名称、许可证、用途说明。</summary>
public sealed record OpenSourceComponent(string Name, string License, string Description);