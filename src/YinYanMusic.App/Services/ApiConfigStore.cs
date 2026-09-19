using System.Diagnostics;
using System.Text.Json;

namespace YinYanMusic.App.Services;

/// <summary>
/// API 地址的**持久化层**：加载/保存到 Preferences 与磁盘文件 <c>api.json</c>。
/// 启动时按优先级解析并应用到 <see cref="ApiConfig"/>：
///
///   ① Preferences（设备上的"应用内设置"，设置页保存的最高优先级）
///   ② 环境变量 <c>YINYAN_API_BASEURL</c>
///   ③ 部署期配置文件 <c>{AppDataDirectory}/api.json</c>（运维 ship 前置入）
///   ④ <see cref="ApiConfig.DefaultBaseUrl"/>
///
/// ⚠️ <b>调用方约定</b>：<see cref="ApiConfigStore"/> 自身**没有**单例外壳 —— 直接 <c>new</c> 即可。
/// 不过 <see cref="LoadAsync"/> 在 app 启动早期（MauiProgram）跑一次就够了。
/// </summary>
public sealed class ApiConfigStore
{
    private const string PrefKey = "yinyan.api.baseUrl";
    private const string EnvKey = "YINYAN_API_BASEURL";

    private readonly string _filePath;

    public ApiConfigStore()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "api.json");
    }

    /// <summary>api.json 的实际位置（用于诊断/运维）。</summary>
    public string FilePath => _filePath;

    /// <summary>当前生效的 URL（不一定持久化，取决于来源）。</summary>
    public string Current => ApiConfig.BaseUrl;

    /// <summary>当前来源标签，方便设置页展示「这个值是哪儿来的」。</summary>
    public string CurrentSource { get; private set; } = "default";

    /// <summary>应用启动时调用一次：按优先级解析并把值塞给 <see cref="ApiConfig"/>。</summary>
    public void Load()
    {
        var prefs = ReadPref();
        var env = Environment.GetEnvironmentVariable(EnvKey);
        var file = ReadFile();

        string? chosen;
        string source;
        if (prefs is { Length: > 0 })      { chosen = prefs; source = "设置"; }
        else if (env is { Length: > 0 })    { chosen = env;   source = "环境变量"; }
        else if (file is { Length: > 0 })   { chosen = file;  source = "配置文件"; }
        else                                 { chosen = null;   source = "默认值"; }

        var final = chosen ?? ApiConfig.DefaultBaseUrl;
        ApiConfig.SetBaseUrl(final);
        CurrentSource = source;
        Debug.WriteLine($"[ApiConfigStore] baseUrl = {final} ( {source} )");
    }

    /// <summary>设置页保存时调用：写 Preferences + 写文件 + 立即生效（更新内存中的 BaseUrl）。</summary>
    public async Task SaveAsync(string url)
    {
        var cleaned = (url ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(cleaned))
            throw new ArgumentException("地址不能为空", nameof(url));

        Preferences.Default.Set(PrefKey, cleaned);
        try { await WriteFileAsync(cleaned); } catch { /* 写文件失败不影响 Preferences；用调试输出 */ }
        ApiConfig.SetBaseUrl(cleaned);
        CurrentSource = "设置";
        Debug.WriteLine($"[ApiConfigStore] saved baseUrl = {cleaned}");
    }

    /// <summary>恢复默认：清掉 Preferences 与文件，URL 回到默认值。</summary>
    public void ResetToDefault()
    {
        Preferences.Default.Remove(PrefKey);
        try { if (File.Exists(_filePath)) File.Delete(_filePath); } catch { }
        ApiConfig.SetBaseUrl(ApiConfig.DefaultBaseUrl);
        CurrentSource = "默认值";
    }

    /// <summary>"测试连接"：用当前 URL 调一次轻量接口，返回 (是否 2xx, 错误消息)。</summary>
    public async Task<(bool ok, string? error)> TestConnectionAsync(HttpClient http)
    {
        try
        {
            // 走一个不需鉴权、轻量、稳定的端点（歌曲列表首页）
            using var resp = await http.GetAsync(ApiConfig.Absolute("api/songs?page=1&pageSize=1"));
            if (resp.IsSuccessStatusCode) return (true, null);
            return (false, $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    // --- 持久化读写 ---

    private static string? ReadPref() => Preferences.Default.Get<string?>(PrefKey, null);

    private string? ReadFile()
    {
        try
        {
            if (!File.Exists(_filePath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(_filePath));
            if (doc.RootElement.TryGetProperty("baseUrl", out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        }
        catch (Exception ex) { Debug.WriteLine($"[ApiConfigStore] read api.json failed: {ex.Message}"); }
        return null;
    }

    private async Task WriteFileAsync(string url)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(new { baseUrl = url }));
    }
}