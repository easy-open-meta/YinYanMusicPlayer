using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using YinYanMusic.App.Services;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.ViewModels;

/// <summary>「全部专区」网格页：分区列表完全来自 Categories 表（数据驱动）。</summary>
public partial class ZonesViewModel(IMusicApi api) : ObservableObject
{
    public ObservableCollection<CategoryDto> Zones { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var cats = await api.GetCategoriesAsync();
            Replace(Zones, cats);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task BackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private Task OpenZoneAsync(CategoryDto zone) => Shell.Current.GoToAsync($"zone?{ZoneQuery.Build(zone)}");

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}
