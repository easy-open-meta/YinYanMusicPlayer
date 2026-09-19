using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

public partial class LibraryView : ContentView
{
	public LibraryView() : this(ServiceHelper.GetRequiredService<LibraryViewModel>())
	{
	}

	public LibraryView(LibraryViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
	}
}