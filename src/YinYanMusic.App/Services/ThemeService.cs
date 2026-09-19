using Microsoft.Maui.Controls;

namespace YinYanMusic.App.Services;

/// <summary>
/// 全局主题色服务。所有页面通过 DynamicResource 引用 Primary / OnPrimary / Accent / PrimarySoft，
/// 切换主题时只需更新 Application.Current.Resources 中对应键值，UI 即实时刷新。
/// 播放页（NowPlayingPage）整体的深色亚克力背景不随主题变化，但其交互强调元素
/// （进度条、主播放按钮、音质 badge、保存封面等）已改用 Primary/OnPrimary，随全局主题统一变化。
/// </summary>
public sealed class ThemeService
{
    private const string PrefKey = "theme_key";

    public static readonly ThemeService Instance = new();

    /// <summary>主题切换后触发（用于刷新代码里手动设置的颜色，如底部选中 Tab）。</summary>
    public event EventHandler? ThemeChanged;

    public const string DefaultThemeKey = "purple";

    /// <summary>内置主题调色板。</summary>
    public IReadOnlyList<ThemeOption> Themes { get; } = new List<ThemeOption>
    {
        new("purple", "紫色", "#512BD4", "#FFFFFF", "#512BD4", "#EDE9FB", "#3A1E9E"),
        new("red",    "红色", "#E53935", "#FFFFFF", "#E53935", "#FDECEA", "#AB2622"),
        new("orange", "橙色", "#FB8C00", "#FFFFFF", "#FB8C00", "#FFF1DD", "#C25E00"),
        new("blue",   "蓝色", "#1E88E5", "#FFFFFF", "#1E88E5", "#E3F2FD", "#1160A8"),
        // 纯白主题：主色为白，OnPrimary 用深色文字保证可读性；Accent 用中灰，
        // 使浅色背景上的选中/链接/标记仍清晰可见，整体呈现干净纯白风。
        new("white",  "纯白", "#FFFFFF", "#1F1F2E", "#6E6E8A", "#EFEFF4", "#C8C8D4"),
    };

    public string CurrentKey { get; private set; } = DefaultThemeKey;

    private ThemeService() { }

    /// <summary>应用启动时调用：读取已保存的主题并应用（不重复持久化）。</summary>
    public void Initialize()
    {
        var saved = Preferences.Default.Get(PrefKey, DefaultThemeKey);
        ApplyTheme(saved, persist: false);
    }

    /// <summary>切换主题并应用到全局资源。</summary>
    public void ApplyTheme(string key, bool persist = true)
    {
        var theme = Find(key) ?? Find(DefaultThemeKey)!;
        CurrentKey = theme.Key;

        var app = Application.Current;
        if (app is not null)
        {
            var r = app.Resources;
            r["Primary"] = Color.FromArgb(theme.Primary);
            r["OnPrimary"] = Color.FromArgb(theme.OnPrimary);
            r["Accent"] = Color.FromArgb(theme.Accent);
            r["PrimarySoft"] = Color.FromArgb(theme.PrimarySoft);
            // 派生色：供深色模式/禁用态/开关轨道等使用，保证整体协调。
            r["PrimaryDark"] = Color.FromArgb(theme.PrimaryDark);
            r["Secondary"] = Color.FromArgb(theme.PrimarySoft);
            r["Tertiary"] = Color.FromArgb(theme.PrimaryDark);
        }

        if (persist)
            Preferences.Default.Set(PrefKey, theme.Key);

        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public string CurrentDisplayName => (Find(CurrentKey) ?? Find(DefaultThemeKey)!).DisplayName;

    private ThemeOption? Find(string key) =>
        System.Linq.Enumerable.FirstOrDefault(Themes, t => t.Key == key);
}

public sealed record ThemeOption(
    string Key,
    string DisplayName,
    string Primary,
    string OnPrimary,
    string Accent,
    string PrimarySoft,
    string PrimaryDark);
