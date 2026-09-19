using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YinYanMusic.App.Services;

namespace YinYanMusic.App.ViewModels;

public partial class LoginViewModel(IAuthService auth) : ObservableObject
{
    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入用户名和密码。";
            return;
        }
        IsBusy = true;
        try
        {
            await auth.LoginAsync(UserName.Trim(), Password);
            await Shell.Current.GoToAsync("///main");
        }
        catch (System.Net.Http.HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // 「用户名或密码错误」≠ 「连不上服务器」—— 这两种之前被吞成同一条文案，是早期
            // 「真机登录失败」排查时误导我们排查客户端的根本原因（M0 文档已记一笔）。
            ErrorMessage = "用户名或密码错误。";
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            // ex.StatusCode == null 表示连接级失败（DNS / 拒连 / 超时 / TLS）；有码表示协议级失败。
            // 都归到「连不上/服务异常」一类，让用户去检查地址/网络（去服务器设置页）。
            ErrorMessage = $"连不上服务器：{ex.Message}{(ex.StatusCode is null ? "" : $"（HTTP {(int)ex.StatusCode}）")}";
        }
        catch (Exception ex)
        {
            ErrorMessage = "登录失败：" + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoRegisterAsync() =>
        await Shell.Current.GoToAsync("///register");
}

public partial class RegisterViewModel(IAuthService auth) : ObservableObject
{
    [ObservableProperty]
    private string userName = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string selectedGender = "保密";

    public List<string> Genders { get; } = ["保密", "男", "女"];

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy) return;
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(UserName) || UserName.Trim().Length < 3)
        {
            ErrorMessage = "用户名至少 3 个字符。";
            return;
        }
        if (Password.Length < 6)
        {
            ErrorMessage = "密码长度至少 6 位。";
            return;
        }
        if (Password != ConfirmPassword)
        {
            ErrorMessage = "两次输入的密码不一致。";
            return;
        }
        IsBusy = true;
        try
        {
            await auth.RegisterAsync(UserName.Trim(), Password, string.IsNullOrWhiteSpace(DisplayName) ? UserName.Trim() : DisplayName.Trim(), SelectedGender, null);
            await Shell.Current.GoToAsync("///main");
        }
        catch (Exception)
        {
            ErrorMessage = "注册失败，用户名可能已被占用。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackAsync() =>
        await Shell.Current.GoToAsync("///login");
}