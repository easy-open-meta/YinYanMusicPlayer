using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 账号资料编辑页（V2.5）。后端 <c>PUT /api/auth/me</c> 早已存在，
/// 但客户端一直没有任何入口 —— 用户注册后就改不了昵称/性别/简介/头像。
/// 这里补上编辑表单；头像自 V2.9.3 起只能从**内置头像**里挑（见 <see cref="BuiltInAvatars"/>），
/// 不再支持自选相册照片。
/// </summary>
public partial class AccountEditViewModel(IMusicApi api) : ObservableObject
{
    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string displayName = string.Empty;
    [ObservableProperty] private string? bio;
    [ObservableProperty] private string gender = "保密";
    [ObservableProperty] private string? avatarUrl;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isError;

    /// <summary>头像显示地址。data URI（base64 存库）原样返回，相对路径转绝对地址。</summary>
    public string AvatarDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(AvatarUrl)) return string.Empty;
            // 头像现在以 data:image/...;base64,xxx 形式存库（V2.5 调整），
            // 不要再当路径去拼 BaseUrl，否则会得到一个无效 URL、图显示不出来。
            return AvatarUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? AvatarUrl
                : Services.ApiConfig.Absolute(AvatarUrl);
        }
    }

    public bool HasAvatar => !string.IsNullOrWhiteSpace(AvatarUrl);

    /// <summary>性别选项（与注册页保持一致的三档）。</summary>
    public IReadOnlyList<string> GenderOptions { get; } = ["保密", "男", "女"];

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var me = await api.MeAsync();
            if (me is null) return;
            UserName = me.UserName;
            DisplayName = me.DisplayName;
            Bio = me.Bio;
            Gender = string.IsNullOrWhiteSpace(me.Gender) ? "保密" : me.Gender;
            AvatarUrl = me.AvatarUrl;
            NotifyAvatar();
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PickAvatarAsync()
    {
        // V2.9.3：不再支持自选相册照片，只能从 App 内置头像里挑
        // （图片资源由外部产出，文件名与出图规格见 BuiltInAvatars 的类型注释）
        var picked = await SongMenuHelper.ShowAvatarPickerAsync(BuiltInAvatars.All);
        if (picked is null) return;

        IsBusy = true;
        StatusMessage = "处理中…";
        IsError = false;
        try
        {
            var bytes = await BuiltInAvatars.ReadAsync(picked);
            if (bytes is null || bytes.Length == 0)
            {
                // 资源还没放进 App 时会走到这里（见 BuiltInAvatars 的类型注释）
                SetStatus("这张内置头像暂不可用，请换一张。", isError: true);
                return;
            }

            // 仍走原来的 data URI 通道提交：服务端存进 Users.AvatarUrl，
            // 评论面板 / 关注列表 / 用户页都是拿这个字符串渲染的，不必为内置头像单开一套存储。
            var updated = await api.UpdateAvatarAsync(Convert.ToBase64String(bytes), "image/png");
            if (updated is null)
            {
                SetStatus("头像更新失败，请检查网络后重试。", isError: true);
                return;
            }

            AvatarUrl = updated.AvatarUrl;
            NotifyAvatar();
            SetStatus("头像已更新。", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"头像更新失败：{ex.Message}", isError: true);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var name = DisplayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("昵称不能为空。", isError: true);
            return;
        }
        if (name.Length > 32)
        {
            SetStatus("昵称最多 32 个字符。", isError: true);
            return;
        }
        if (Bio is { Length: > 200 })
        {
            SetStatus("简介最多 200 个字符。", isError: true);
            return;
        }

        IsBusy = true;
        try
        {
            var updated = await api.UpdateProfileAsync(
                new UpdateProfileRequest(name, Bio ?? string.Empty, Gender, AvatarUrl));
            if (updated is null)
            {
                SetStatus("保存失败，请检查网络后重试。", isError: true);
                return;
            }
            SetStatus("已保存。", isError: false);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    private void NotifyAvatar()
    {
        OnPropertyChanged(nameof(AvatarDisplay));
        OnPropertyChanged(nameof(HasAvatar));
    }

    private void SetStatus(string msg, bool isError)
    {
        StatusMessage = msg;
        IsError = isError;
    }
}
