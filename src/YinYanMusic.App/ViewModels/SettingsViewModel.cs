using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YinYanMusic.App.Services;

namespace YinYanMusic.App.ViewModels;

/// <summary>服务器设置页（M0.5）：编辑/保存 API 地址 + 测试连接 + 恢复默认。</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ApiConfigStore _store;
    private readonly HttpClient _http;

    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _sourceLabel = string.Empty;
    [ObservableProperty] private string _testResult = string.Empty;
    [ObservableProperty] private Color _testColor = Colors.Gray;
    [ObservableProperty] private bool _busy;

    public SettingsViewModel(ApiConfigStore store)
    {
        _store = store;
        // 复用 DI 里的同一个 HttpClient 实例（持有 AuthTokenHandler）；直接拿 singleton。
        _http = IPlatformApplication.Current?.Services.GetRequiredService<HttpClient>()
               ?? throw new InvalidOperationException("HttpClient not registered");
        _url = _store.Current;
        _sourceLabel = $"当前来源：{_store.CurrentSource}";
    }

    partial void OnUrlChanged(string value)
    {
        // 清掉旧测试结果，让用户重新测试
        if (!string.IsNullOrEmpty(TestResult)) { TestResult = string.Empty; TestColor = Colors.Gray; }
    }

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
            var temp = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var resp = await temp.GetAsync(MakeAbsolute(Url));
            if (resp.IsSuccessStatusCode)
            {
                TestResult = "连接成功 ✓";
                TestColor = Color.FromArgb("#16A34A");
            }
            else
            {
                TestResult = $"服务器返回 HTTP {(int)resp.StatusCode}";
                TestColor = Color.FromArgb("#E5484D");
            }
        }
        catch (Exception ex)
        {
            TestResult = $"连不上：{ex.Message}";
            TestColor = Color.FromArgb("#E5484D");
        }
        finally { Busy = false; }
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