using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YinYanMusic.App.Services;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 修改密码（V2.5 三级页面）。
///
/// <para>
/// ⚠️ 关键点：改密成功后服务端会让 <c>TokenVersion</c> 自增 → 当前 token 立即失效。
/// 所以这里必须主动登出并回登录页，否则用户会停在"每个请求都 401"的状态。
/// </para>
/// </summary>
public partial class ChangePasswordViewModel(IMusicApi api, IAuthService auth) : ObservableObject
{
    [ObservableProperty] private string oldPassword = string.Empty;
    [ObservableProperty] private string newPassword = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isError;

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(OldPassword)) { SetStatus("请输入当前密码。", isError: true); return; }
        if (NewPassword.Length < 6) { SetStatus("新密码长度至少 6 位。", isError: true); return; }
        if (NewPassword != ConfirmPassword) { SetStatus("两次输入的新密码不一致。", isError: true); return; }
        if (NewPassword == OldPassword) { SetStatus("新密码不能与当前密码相同。", isError: true); return; }

        IsBusy = true;
        try
        {
            var (ok, error) = await api.ChangePasswordAsync(OldPassword, NewPassword);
            if (!ok)
            {
                SetStatus(error ?? "修改失败。", isError: true);
                return;
            }

            // 改密后 token 已失效：主动登出回登录页，避免后续请求全部 401
            SetStatus("密码已修改，请用新密码重新登录。", isError: false);
            await Task.Delay(1200);          // 让提示看得见
            await auth.LogoutAsync();
            // 三斜杠 = 绝对路由（与 AuthViewModels 里的写法一致）
            await Shell.Current.GoToAsync("///login");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    private void SetStatus(string msg, bool isError)
    {
        StatusMessage = msg;
        IsError = isError;
    }
}
