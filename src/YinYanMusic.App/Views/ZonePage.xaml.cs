using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

/// <summary>
/// 分区详情页。分区展示信息通过导航查询串带入（categoryId/name/slogan/colorHex/icon），
/// QueryProperty 收到后转喂 ZoneViewModel.Apply；categoryId 到齐即触发该分区歌单加载。
/// </summary>
[QueryProperty(nameof(CategoryId), "categoryId")]
[QueryProperty(nameof(ZoneName), "name")]
[QueryProperty(nameof(Slogan), "slogan")]
[QueryProperty(nameof(ColorHex), "colorHex")]
[QueryProperty(nameof(Icon), "icon")]
public partial class ZonePage : ContentPage
{
	private readonly ZoneViewModel _vm;

	public ZonePage() : this(ServiceHelper.GetRequiredService<ZoneViewModel>())
	{
	}

	public ZonePage(ZoneViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
	}

	// 注意：属性名 Name 与 ContentPage.Name 冲突，故用 ZoneName 承接查询参数 "name"。
	public string CategoryId { set => _vm.Apply("categoryId", value); }
	public string ZoneName { set => _vm.Apply("name", value); }
	public string Slogan { set => _vm.Apply("slogan", value); }
	public string ColorHex { set => _vm.Apply("colorHex", value); }
	public string Icon { set => _vm.Apply("icon", value); }
}
