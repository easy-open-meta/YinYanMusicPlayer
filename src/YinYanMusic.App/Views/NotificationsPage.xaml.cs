using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class NotificationsPage : ContentPage
{
    private readonly NotificationsViewModel _vm;

    public NotificationsPage() : this(ServiceHelper.GetRequiredService<NotificationsViewModel>())
    {
    }

    public NotificationsPage(NotificationsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // 每次进入都拉一遍：离线期间漏掉的通知靠 REST 补齐（TC-2.15-08）
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
