using CommunityToolkit.Mvvm.ComponentModel;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 首页「推荐歌单」专区的场景标签（V2.12）。
///
/// <para>做成小类而不是只放字符串：标签要显示中文名（"高分收藏"）、请求要用英文场景码（"collected"），
/// 两者必须成对出现才不会错位 —— 只传字符串给模板的话，很容易出现"点了'最新上架'，请求却发的是 hot"。</para>
///
/// <para><see cref="IsSelected"/> 写在标签自己身上，而不是让 XAML 去跟 HomeViewModel 的
/// SelectedRecommendTab 做比较：MAUI 的 <c>DataTrigger.Value</c> 不支持绑定到另一个属性
/// （只能写字面量），绕开它最省事，也让"选中态该给谁画"这件事有唯一出处。</para>
/// </summary>
public partial class RecommendSceneTab(string scene, string name) : ObservableObject
{
    /// <summary>请求参数用的场景码，取值见 <c>YinYanMusic.Core.Dtos.RecommendScenes</c>。</summary>
    public string Scene { get; } = scene;

    /// <summary>界面上显示的中文名。</summary>
    public string Name { get; } = name;

    [ObservableProperty]
    private bool isSelected;
}
