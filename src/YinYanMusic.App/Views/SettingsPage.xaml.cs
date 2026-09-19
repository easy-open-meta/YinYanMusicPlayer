namespace YinYanMusic.App.Views;

using YinYanMusic.App.ViewModels;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}