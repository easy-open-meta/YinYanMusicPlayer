using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YinYanMusic.App.Services;
using YinYanMusic.App.Services.LocalLibrary;

namespace YinYanMusic.App.ViewModels;

/// <summary>服务器设置页（M0.5）：编辑/保存 API 地址 + 测试连接 + 恢复默认。
/// <para>V2.9 起缓存设置（上限 / 播完自动缓存 / 清理入口）已整体迁到「缓存管理」页
/// —— 它们是"本机存多少缓存"的事，与"连哪台服务器"无关。</para>
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ApiConfigStore _store;
    private readonly HttpClient _http;

    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _sourceLabel = string.Empty;
    [ObservableProperty] private string _testResult = string.Empty;
    [ObservableProperty] private Color _testColor = Colors.Gray;
    [ObservableProperty] private bool _busy;

    public SettingsViewModel(ApiConfigStore store, CacheStore cache)
    {
        _store = store;
        // 复用 DI 里的同一个 HttpClient 实例（持有 AuthTokenHandler）；直接拿 singleton。
        _http = IPlatformApplication.Current?.Services.GetRequiredService<HttpClient>()
               ?? throw new InvalidOperationException("HttpClient not registered");
        _url = _store.Current;
        _sourceLabel = $"当前来源：{_store.CurrentSource}";
        // cache 参数保留：缓存管理页入口（抽屉）未登录也能进，但该页仍在 VM 层声明依赖以保持 DI 形状稳定。
        _ = cache;
    }

    partial void OnUrlChanged(string value)
    {
        // 清掉旧测试结果，让用户重新测试
        if (!string.IsNullOrEmpty(TestResult)) { TestResult = string.Empty; TestColor = Colors.Gray; }
    }

    /// <summary>
    /// 「测试连接」：**同时用两套网络栈各试一次**，并把完整异常链写进日志。
    ///
    /// <para>为什么要两套栈：Android 上 .NET 默认走 <c>HttpClientHandler</c> → 落到 Java 的
    /// <c>HttpURLConnection</c>，它受系统「明文 HTTP 策略 / 局域网访问策略」管辖，
    /// 失败时只会抛出一个笼统的 "Connection failure"，看不出根因；
    /// 而 <c>SocketsHttpHandler</c> 是托管实现，不走 Java 策略栈。
    /// 两者一起报结果，就能一眼区分「被系统策略拦」和「网络真的不通」——
    /// 这两种情况在真机联调里长得一模一样，靠猜会浪费很多时间。</para>
    /// </summary>
    [RelayCommand]
    private async Task TestAsync()
    {
        if (Busy) return;
        Busy = true;
        TestResult = "测试中…";
        TestColor = Colors.Gray;
        try
        {
            // 临时测这个 URL（不写入），避免改了又回不去
            var target = MakeAbsolute(Url);
            var viaDefault = await ProbeAsync(target, new HttpClientHandler());
            var viaManaged = await ProbeAsync(target, new SocketsHttpHandler());

            var ok = viaDefault.Ok || viaManaged.Ok;
            TestColor = ok ? Color.FromArgb("#16A34A") : Color.FromArgb("#E5484D");

            if (viaDefault.Ok)
                TestResult = $"连接成功 ✓（HTTP {viaDefault.Detail}）";
            else if (viaManaged.Ok)
                TestResult = $"默认栈被拦，托管栈可用 ✓（HTTP {viaManaged.Detail}）\n默认栈错误：{viaDefault.Detail}";
            else
                TestResult = $"连不上。\n默认栈：{viaDefault.Detail}\n托管栈：{viaManaged.Detail}";
        }
        catch (Exception ex)
        {
            TestResult = $"连不上：{Describe(ex)}";
            TestColor = Color.FromArgb("#E5484D");
        }
        finally { Busy = false; }
    }

    /// <summary>用指定的网络栈探一次；返回值只保留"给人看的最短原因"。</summary>
    private static async Task<(bool Ok, string Detail)> ProbeAsync(Uri url, HttpMessageHandler handler)
    {
        try
        {
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(6) };
            using var resp = await client.GetAsync(url);
            var code = (int)resp.StatusCode;
            return resp.IsSuccessStatusCode
                ? (true, code.ToString())
                : (false, $"HTTP {code}");
        }
        catch (Exception ex)
        {
            var text = Describe(ex);
            AppLog.Warn($"[设置] 测试连接失败（{handler.GetType().Name}）：{text}");
            return (false, text);
        }
    }

    /// <summary>把异常链压成一行：<c>最外层 ← … ← 最内层</c>（截断到 140 字符，避免撑爆设置页布局）。</summary>
    private static string Describe(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
            parts.Add($"{e.GetType().Name}: {e.Message}");

        var text = string.Join(" ← ", parts);
        return text.Length <= 140 ? text : text[..140] + "…";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Busy) return;
        Busy = true;
        try
        {
            await _store.SaveAsync(Url.Trim());
            SourceLabel = $"当前来源：{_store.CurrentSource}";
            TestResult = "已保存 ✓ 立即生效";
            TestColor = Color.FromArgb("#16A34A");
        }
        catch (Exception ex)
        {
            TestResult = "保存失败：" + ex.Message;
            TestColor = Color.FromArgb("#E5484D");
        }
        finally { Busy = false; }
    }

    [RelayCommand]
    private void Reset()
    {
        _store.ResetToDefault();
        Url = _store.Current;
        SourceLabel = $"当前来源：{_store.CurrentSource}";
        TestResult = "已恢复默认值";
        TestColor = Color.FromArgb("#8A8AA3");
    }

    private static Uri MakeAbsolute(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("地址不能为空");
        var cleaned = url.Trim().TrimEnd('/');
        return new Uri(cleaned + "/api/songs?page=1&pageSize=1", UriKind.Absolute);
    }
}
