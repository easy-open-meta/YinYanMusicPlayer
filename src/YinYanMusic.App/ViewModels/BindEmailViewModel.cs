using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YinYanMusic.App.Services;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 邮箱绑定（V2.5 三级页面）：发送验证码 + 提交绑定。
///
/// <para>
/// 依赖 SMTP；未配置时服务端返回 <c>sent=false</c> + 明确文案，这里原样透出，
/// 并把 <see cref="EmailBindingAvailable"/> 置 false（页面据此提示不可用）。
/// </para>
/// </summary>
public partial class BindEmailViewModel(IMusicApi api) : ObservableObject
{
    [ObservableProperty] private string? currentEmail;
    [ObservableProperty] private string emailInput = string.Empty;
    [ObservableProperty] private string codeInput = string.Empty;

    /// <summary>验证码已发送（决定显示验证码输入框与绑定按钮）。</summary>
    [ObservableProperty] private bool codeSent;

    /// <summary>倒计时秒数（0 表示可再次发送）。</summary>
    [ObservableProperty] private int countdown;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isError;

    private CancellationTokenSource? _countdownCts;

    public bool HasEmail => !string.IsNullOrWhiteSpace(CurrentEmail);

    /// <summary>是否可以点「获取验证码」。</summary>
    public bool CanSendCode => Countdown <= 0 && !IsBusy;

    public string SendCodeText => Countdown > 0 ? $"{Countdown} 秒后可重发" : "获取验证码";

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var me = await api.MeAsync();
            CurrentEmail = me?.Email;
            OnPropertyChanged(nameof(HasEmail));
            // 已绑定过就把邮箱填进去，方便直接换绑
            if (HasEmail) EmailInput = CurrentEmail!;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SendCodeAsync()
    {
        var email = EmailInput?.Trim() ?? string.Empty;
        if (!IsPlausibleEmail(email))
        {
            SetStatus("请输入正确的邮箱地址。", isError: true);
            return;
        }

        IsBusy = true;
        try
        {
            var result = await api.SendEmailCodeAsync(email);
            if (result is null)
            {
                SetStatus("发送失败，请稍后重试。", isError: true);
                return;
            }
            if (!result.Sent)
            {
                // SMTP 未配置或发信失败：服务端给了明确文案，直接透出
                SetStatus(result.Message ?? "验证码发送失败。", isError: true);
                return;
            }

            CodeSent = true;
            SetStatus(result.Message ?? "验证码已发送，请查收邮件。", isError: false);
            StartCountdown(60);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task BindAsync()
    {
        var email = EmailInput?.Trim() ?? string.Empty;
        var code = CodeInput?.Trim() ?? string.Empty;
        if (!IsPlausibleEmail(email)) { SetStatus("请输入正确的邮箱地址。", isError: true); return; }
        if (code.Length == 0) { SetStatus("请输入验证码。", isError: true); return; }

        IsBusy = true;
        try
        {
            var (ok, error) = await api.BindEmailAsync(email, code);
            if (!ok)
            {
                SetStatus(error ?? "绑定失败。", isError: true);
                return;
            }
            CurrentEmail = email;
            OnPropertyChanged(nameof(HasEmail));
            CodeSent = false;
            CodeInput = string.Empty;
            SetStatus("邮箱绑定成功。", isError: false);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    /// <summary>倒计时：每秒减一，归零后可重发。离开页面时取消。</summary>
    private void StartCountdown(int seconds)
    {
        _countdownCts?.Cancel();
        _countdownCts = new CancellationTokenSource();
        var token = _countdownCts.Token;

        Countdown = seconds;
        NotifySendCodeState();

        _ = Task.Run(async () =>
        {
            try
            {
                while (Countdown > 0 && !token.IsCancellationRequested)
                {
                    await Task.Delay(1000, token);
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        Countdown--;
                        NotifySendCodeState();
                    });
                }
            }
            catch (TaskCanceledException) { /* 页面已离开，正常结束 */ }
        }, token);
    }

    /// <summary>离开页面时停掉倒计时，避免后台空转。</summary>
    public void StopCountdown()
    {
        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        _countdownCts = null;
    }

    partial void OnCountdownChanged(int value) => NotifySendCodeState();
    partial void OnIsBusyChanged(bool value) => NotifySendCodeState();

    private void NotifySendCodeState()
    {
        OnPropertyChanged(nameof(CanSendCode));
        OnPropertyChanged(nameof(SendCodeText));
    }

    private void SetStatus(string msg, bool isError)
    {
        StatusMessage = msg;
        IsError = isError;
    }

    /// <summary>
    /// 客户端侧邮箱格式预校验（与服务端同口径，但只是"早点拦"，不是安全边界）。
    /// </summary>
    private static bool IsPlausibleEmail(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length > 256) return false;
        var at = s.IndexOf('@');
        if (at <= 0 || at != s.LastIndexOf('@')) return false;
        var domain = s[(at + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.')
               && !s.Any(char.IsWhiteSpace);
    }
}
