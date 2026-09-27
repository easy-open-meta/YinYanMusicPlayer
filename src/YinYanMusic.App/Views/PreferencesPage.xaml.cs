using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class PreferencesPage : ContentPage
{
    public PreferencesPage(PreferencesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
