using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YinYanMusic.App.Services;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 安全中心（V2.5）：<b>入口列表</b>，不再直接放表单。
///
/// <para>
/// 改成三级结构（汉堡菜单 → 安全中心 → 修改密码 / 邮箱绑定）的原因：
/// 原先两个表单堆在一页，进入页面就要加载全部字段，改密与邮箱是两件独立的事，
/// 混在一起既长又容易误操作。拆开后每页只做一件事，也更贴近"设置 → 二级选项"的习惯。
/// </para>
///
/// <para>
/// 这一页只负责：显示当前账号状态（已绑定邮箱 / 密码提示）+ 两个跳转入口。
/// 具体逻辑在 <see cref="ChangePasswordViewModel"/> 与 <see cref="BindEmailViewModel"/>。
/// </para>
/// </summary>
public partial class SecurityCenterViewModel(IMusicApi api) : ObservableObject
{
    /// <summary>已绑定邮箱（未绑定时为空）。</summary>
    [ObservableProperty] private string? currentEmail;

    /// <summary>邮箱绑定是否可用（SMTP 已配置）。不可用时该项置灰并给出说明。</summary>
    [ObservableProperty] private bool emailBindingAvailable;

    [ObservableProperty] private bool isBusy;

    public bool HasEmail => !string.IsNullOrWhiteSpace(CurrentEmail);

    /// <summary>邮箱项的副标题：已绑定显示邮箱，未绑定按可用性给不同提示。</summary>
    public string EmailSummary => HasEmail
        ? CurrentEmail!
        : (EmailBindingAvailable ? "未绑定" : "服务器未配置邮件服务");

    /// <summary>邮箱项是否可点（不可用时置灰，避免点进去才发现用不了）。</summary>
    public bool CanOpenEmail => EmailBindingAvailable;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            EmailBindingAvailable = await api.IsEmailBindingAvailableAsync();
            var me = await api.MeAsync();
            CurrentEmail = me?.Email;
            NotifyEmailState();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task OpenChangePasswordAsync() => Shell.Current.GoToAsync("changePassword");

    [RelayCommand]
    private async Task OpenBindEmailAsync()
    {
        if (!CanOpenEmail) return;   // 不可用时不给跳（按钮已置灰，这里再兜一层）
        await Shell.Current.GoToAsync("bindEmail");
    }

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    partial void OnCurrentEmailChanged(string? value) => NotifyEmailState();
    partial void OnEmailBindingAvailableChanged(bool value) => NotifyEmailState();

    private void NotifyEmailState()
    {
        OnPropertyChanged(nameof(HasEmail));
        OnPropertyChanged(nameof(EmailSummary));
        OnPropertyChanged(nameof(CanOpenEmail));
    }
}
