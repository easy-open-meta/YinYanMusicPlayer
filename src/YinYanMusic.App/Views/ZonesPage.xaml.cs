using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class ZonesPage : ContentPage
{
	private readonly ZonesViewModel _vm;

	public ZonesPage() : this(ServiceHelper.GetRequiredService<ZonesViewModel>())
	{
	}

	public ZonesPage(ZonesViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
	}

	protected override void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		_ = _vm.LoadCommand.ExecuteAsync(null);
	}
}
