using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class ThirdPartyLicensesPage : ContentPage
{
    public ThirdPartyLicensesPage(ThirdPartyLicensesViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}