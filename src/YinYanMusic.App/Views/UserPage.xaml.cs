using YinYanMusic.App.Services;
using YinYanMusic.App.ViewModels;

namespace YinYanMusic.App.Views;

/// <summary>
/// 用户详情页（搜索用户结果点入）。userId 走导航查询串，到齐后加载资料/统计/公开歌单。
/// </summary>
[QueryProperty(nameof(UserId), "userId")]
public partial class UserPage : ContentPage
{
	private readonly UserDetailViewModel _vm;

	public UserPage() : this(ServiceHelper.GetRequiredService<UserDetailViewModel>())
	{
	}

	public UserPage(UserDetailViewModel vm)
	{
		InitializeComponent();
		_vm = vm;
		BindingContext = vm;
	}

	public string UserId { set => _vm.Apply("userId", value); }
}
