using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage(AboutViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}