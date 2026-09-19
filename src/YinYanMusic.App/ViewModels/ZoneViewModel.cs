using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>
/// 分区详情页：头部展示分区信息（导航查询串带入，不二次请求），
/// 下方列出该分区下的公开歌单（api/playlists?categoryId=x，服务端已排除系统歌单）。
/// </summary>
public partial class ZoneViewModel(IMusicApi api) : ObservableObject
{
    public long CategoryId { get; private set; }

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string slogan = string.Empty;

    /// <summary>分区卡片底色（#RRGGBB）。无数据时回退主题紫，保证头部可见。</summary>
    [ObservableProperty]
    private string colorHex = "#512BD4";

    [ObservableProperty]
    private string iconGlyph = string.Empty;

    public ObservableCollection<PlaylistDto> Playlists { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private Task OpenPlaylistAsync(PlaylistDto playlist) => Shell.Current.GoToAsync($"playlist?id={playlist.Id}");

    /// <summary>由 ZonePage 的 QueryProperty 回调逐个喂参数；categoryId 到齐后触发加载歌单。</summary>
    public void Apply(string key, string value)
    {
        switch (key)
        {
            case "categoryId": CategoryId = long.TryParse(value, out var id) ? id : 0; break;
            case "name": Name = value; break;
            case "slogan": Slogan = value; break;
            case "colorHex": if (!string.IsNullOrWhiteSpace(value)) ColorHex = value; break;
            case "icon": IconGlyph = value; break;
        }
        if (key == "categoryId") _ = LoadPlaylistsAsync();
    }

    private async Task LoadPlaylistsAsync()
    {
        if (CategoryId <= 0) return;
        IsBusy = true;
        try
        {
            var result = await api.SearchPlaylistsAsync(categoryId: (int)CategoryId, pageSize: 50);
            Playlists.Clear();
            foreach (var p in result.Items.Where(p => !p.IsSystem)) Playlists.Add(p);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
