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
        catch (Exception)
        {
            ErrorMessage = "登录失败，请检查用户名和密码。";
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