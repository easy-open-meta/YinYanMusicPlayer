using YinYanMusic.App.ViewModels;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Services;

public static class SongMenuHelper
{
    public static async Task ShowMenuAsync(SongDto song, IMusicApi api, PlayerService player)
    {
        try
        {
            var isLiked = await IsSongLikedAsync(api, song.Id);
            var likeLabel = isLiked ? "取消喜欢" : "喜欢";

            // 歌手信息跳转：没有歌手信息（ArtistId=0）时不显示该选项
            var artistLabel = song.ArtistId > 0 ? $"歌手：{song.ArtistName}" : null;

            // 关注歌手：没有歌手信息（ArtistId=0）时不显示这一项
            var followed = song.ArtistId > 0 && await IsArtistFollowedAsync(api, song.ArtistId);
            var followLabel = followed ? "取消关注歌手" : "关注歌手";

            var options = new List<string> { "播放", "下一首播放" };
            if (artistLabel is not null) options.Add(artistLabel);
            options.Add(likeLabel);
            if (song.ArtistId > 0) options.Add(followLabel);
            options.Add("添加到歌单...");

            var choice = await ShowBottomSheetAsync(song.Title, options);
            if (choice is null) return;

            if (choice == "播放")
            {
                if (player.Current?.Id == song.Id) { await Shell.Current.GoToAsync("nowplaying"); return; }
                player.PlayQueue([song], 0, "单曲播放");
                await Shell.Current.GoToAsync("nowplaying");
            }
            else if (choice == "下一首播放")
            {
                player.InsertNext(song);
            }
            else if (artistLabel is not null && choice == artistLabel)
            {
                await Shell.Current.GoToAsync($"artist?artistId={song.ArtistId}");
            }
            else if (choice == likeLabel)
            {
                if (isLiked) { await api.UnlikeAsync(song.Id); song.IsLiked = false; }
                else { await api.LikeAsync(song.Id); song.IsLiked = true; }
            }
            else if (choice == followLabel)
            {
                var ok = followed
                    ? await api.UnfollowArtistAsync(song.ArtistId)
                    : await api.FollowArtistAsync(song.ArtistId);
                if (ok)
                    await ShowMessageDialogAsync("提示", followed ? $"已取消关注「{song.ArtistName}」" : $"已关注「{song.ArtistName}」", "确定");
                else
                    await ShowMessageDialogAsync("提示", "操作失败，请稍后重试。", "确定");
            }
            else if (choice == "添加到歌单...")
            {
                await ShowAddToPlaylistAsync(song, api);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[菜单异常] {ex.Message}");
        }
    }

    internal static async Task<string?> ShowBottomSheetAsync(string title, List<string> options, HashSet<string>? disabledOptions = null)
    {
        var page = Shell.Current?.CurrentPage as ContentPage;
        if (page is null) return null;

        var tcs = new TaskCompletionSource<string?>();

        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };
        var cancelTap = new TapGestureRecognizer();
        cancelTap.Tapped += (_, _) => { overlay.IsVisible = false; tcs.TrySetResult(null); };
        overlay.GestureRecognizers.Add(cancelTap);

        var sheet = new Frame
        {
            CornerRadius = 16,
            BackgroundColor = Colors.White,
            VerticalOptions = LayoutOptions.End,
            Padding = new Thickness(0),
            HasShadow = true
        };

        var layout = new VerticalStackLayout { Spacing = 0 };

        var headerLabel = new Label
        {
            Text = title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            Padding = new Thickness(16, 16, 16, 12),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        layout.Add(headerLabel);
        layout.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#E4E4EC") });

        foreach (var option in options)
        {
            var optText = option;
            var isDisabled = disabledOptions is not null && disabledOptions.Contains(optText);
            var row = new Grid { Padding = new Thickness(16, 14), HeightRequest = 48 };
            var label = new Label
            {
                Text = isDisabled ? $"{optText}（已添加）" : optText,
                FontSize = 16,
                TextColor = isDisabled ? Color.FromArgb("#B0B0B0") : Colors.Black,
                VerticalOptions = LayoutOptions.Center
            };
            row.Add(label);

            if (!isDisabled)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => { overlay.IsVisible = false; tcs.TrySetResult(optText); };
                row.GestureRecognizers.Add(tap);
            }

            layout.Add(row);
            layout.Add(new BoxView { HeightRequest = 0.5, Color = Color.FromArgb("#F0F0F0"), Margin = new Thickness(16, 0) });
        }

        layout.Add(new BoxView { HeightRequest = 8, Color = Color.FromArgb("#F6F6F9") });
        var cancelRow = new Grid { Padding = new Thickness(16, 14), HeightRequest = 48 };
        var cancelLabel = new Label { Text = "取消", FontSize = 16, TextColor = Color.FromArgb("#7C7C8A"), VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center };
        cancelRow.Add(cancelLabel);
        var cancelTap2 = new TapGestureRecognizer();
        cancelTap2.Tapped += (_, _) => { overlay.IsVisible = false; tcs.TrySetResult(null); };
        cancelRow.GestureRecognizers.Add(cancelTap2);
        layout.Add(cancelRow);

        sheet.Content = layout;
        overlay.Add(sheet);

        // 统一把页面内容包进一个铺满整页的 Grid 再叠加遮罩。
        // 若直接把 overlay 加到页面原有的 Grid，它会落在第 0 行；当该行是 Auto 时
        // （例如 PlaylistDetailPage 的 Auto,Auto,Auto,*），VerticalOptions=End 只会在那一行
        // 的高度内对齐，Windows 上就表现为弹层"跑到顶部"。Android 恰好能撑满，所以之前没暴露。
        var oldContent = page.Content as View;
        var rootGrid = new Grid();
        if (oldContent is not null) rootGrid.Add(oldContent);
        rootGrid.Add(overlay);
        page.Content = rootGrid;

        EventHandler disappearingHandler = (_, _) => tcs.TrySetResult(null);
        page.Disappearing += disappearingHandler;

        var result = await tcs.Task;

        page.Disappearing -= disappearingHandler;

        if (oldContent is not null) page.Content = oldContent;

        return result;
    }

    private static async Task<T?> PresentSheetAsync<T>(Action<TaskCompletionSource<T?>, VerticalStackLayout> buildLayout)
    {
        var page = Shell.Current?.CurrentPage as ContentPage;
        if (page is null) return default;

        var tcs = new TaskCompletionSource<T?>();
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };
        var sheet = new Frame
        {
            CornerRadius = 16,
            BackgroundColor = Colors.White,
            VerticalOptions = LayoutOptions.End,
            Padding = new Thickness(0),
            HasShadow = true
        };

        var layout = new VerticalStackLayout { Spacing = 0 };
        buildLayout(tcs, layout);

        var cancelTap = new TapGestureRecognizer();
        cancelTap.Tapped += (_, _) => tcs.TrySetResult(default);
        overlay.GestureRecognizers.Add(cancelTap);

        sheet.Content = layout;
        overlay.Add(sheet);

        // 同 ShowBottomSheetAsync：必须包一层铺满整页的容器，否则弹层会落在原 Grid 的 Auto 行里
        var oldContent = page.Content as View;
        var rootGrid = new Grid();
        if (oldContent is not null) rootGrid.Add(oldContent);
        rootGrid.Add(overlay);
        page.Content = rootGrid;

        EventHandler disappearingHandler = (_, _) => tcs.TrySetResult(default);
        page.Disappearing += disappearingHandler;

        var result = await tcs.Task;

        page.Disappearing -= disappearingHandler;

        if (oldContent is not null) page.Content = oldContent;

        return result;
    }

    private static void AddHeader(VerticalStackLayout layout, string title)
    {
        layout.Add(new Label
        {
            Text = title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            Padding = new Thickness(16, 16, 16, 12),
            LineBreakMode = LineBreakMode.TailTruncation
        });
        layout.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#E4E4EC") });
    }

    private static Grid MakeActionRow(string text, Color textColor, Action onTap)
    {
        var row = new Grid { Padding = new Thickness(16, 14), HeightRequest = 48 };
        row.Add(new Label { Text = text, FontSize = 16, TextColor = textColor, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center });
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => onTap();
        row.GestureRecognizers.Add(tap);
        return row;
    }

    public static async Task ShowMessageDialogAsync(string title, string message, string okText = "确定")
    {
        await PresentSheetAsync<bool>((tcs, layout) =>
        {
            AddHeader(layout, title);
            layout.Add(new Label
            {
                Text = message,
                FontSize = 15,
                TextColor = Color.FromArgb("#333333"),
                Padding = new Thickness(16, 16),
                LineBreakMode = LineBreakMode.WordWrap,
                HorizontalTextAlignment = TextAlignment.Start
            });
            layout.Add(new BoxView { HeightRequest = 8, Color = Color.FromArgb("#F6F6F9") });
            layout.Add(MakeActionRow(okText, Color.FromArgb("#512BD4"), () => tcs.TrySetResult(true)));
        });
    }

    public static async Task<bool> ShowConfirmDialogAsync(string title, string message, string acceptText, string cancelText)
    {
        return await PresentSheetAsync<bool>((tcs, layout) =>
        {
            AddHeader(layout, title);
            layout.Add(new Label
            {
                Text = message,
                FontSize = 15,
                TextColor = Color.FromArgb("#333333"),
                Padding = new Thickness(16, 16),
                LineBreakMode = LineBreakMode.WordWrap,
                HorizontalTextAlignment = TextAlignment.Start
            });
            layout.Add(new BoxView { HeightRequest = 0.5, Color = Color.FromArgb("#F0F0F0"), Margin = new Thickness(16, 0) });
            layout.Add(MakeActionRow(acceptText, Color.FromArgb("#512BD4"), () => tcs.TrySetResult(true)));
            layout.Add(new BoxView { HeightRequest = 0.5, Color = Color.FromArgb("#F0F0F0"), Margin = new Thickness(16, 0) });
            layout.Add(MakeActionRow(cancelText, Color.FromArgb("#7C7C8A"), () => tcs.TrySetResult(false)));
        });
    }

    public static async Task<string?> ShowInputDialogAsync(string title, string? message, string? initialValue = null, string okText = "确定")
    {
        Entry? entry = null;
        return await PresentSheetAsync<string?>((tcs, layout) =>
        {
            AddHeader(layout, title);
            if (!string.IsNullOrEmpty(message))
                layout.Add(new Label { Text = message, FontSize = 14, TextColor = Color.FromArgb("#666666"), Padding = new Thickness(16, 12, 16, 4) });
            entry = new Entry
            {
                Text = initialValue ?? string.Empty,
                FontSize = 16,
                BackgroundColor = Color.FromArgb("#F6F6F9"),
                Margin = new Thickness(16, 4, 16, 12),
                HeightRequest = 44
            };
            layout.Add(entry);
            layout.Add(new BoxView { HeightRequest = 0.5, Color = Color.FromArgb("#F0F0F0"), Margin = new Thickness(16, 0) });
            layout.Add(MakeActionRow(okText, Color.FromArgb("#512BD4"), () => tcs.TrySetResult(entry!.Text)));
            layout.Add(new BoxView { HeightRequest = 0.5, Color = Color.FromArgb("#F0F0F0"), Margin = new Thickness(16, 0) });
            layout.Add(MakeActionRow("取消", Color.FromArgb("#7C7C8A"), () => tcs.TrySetResult(null)));
        });
    }

    private static async Task<bool> IsSongLikedAsync(IMusicApi api, long songId)
    {
        try
        {
            var liked = await api.GetLikedSongsAsync();
            return liked.Any(s => s.Id == songId);
        }
        catch { return false; }
    }

    private static async Task<bool> IsArtistFollowedAsync(IMusicApi api, long artistId)
    {
        try
        {
            var ids = await api.GetFollowedArtistIdsAsync();
            return ids.Contains(artistId);
        }
        catch { return false; }
    }

    public static async Task MarkLikedAsync(IMusicApi api, IEnumerable<SongDto> songs)
    {
        try
        {
            var likedIds = (await api.GetLikedSongsAsync()).Select(s => s.Id).ToHashSet();
            foreach (var s in songs) s.IsLiked = likedIds.Contains(s.Id);
        }
        catch { }
    }

    /// <summary>"添加到歌单..."：选目标歌单并添加（已含该歌的歌单置灰禁选）。供各歌曲菜单复用。</summary>
    public static async Task ShowAddToPlaylistAsync(SongDto song, IMusicApi api)
    {
        var playlists = await api.GetMyPlaylistsAsync();
        var myPlaylists = playlists.Where(p => !p.IsSystem).ToList();
        if (myPlaylists.Count == 0)
        {
            await ShowMessageDialogAsync("提示", "还没有自己的歌单，先创建一个吧", "确定");
            return;
        }

        var containedIds = await api.GetPlaylistsContainingSongAsync(song.Id);
        var disabledNames = myPlaylists
            .Where(p => containedIds.Contains(p.Id))
            .Select(p => p.Name)
            .ToHashSet();

        var names = myPlaylists.Select(p => p.Name).ToList();
        var picked = await ShowBottomSheetAsync("添加到歌单", names, disabledNames);
        var target = myPlaylists.FirstOrDefault(p => p.Name == picked);
        if (target is null) return;
        await api.AddSongsToPlaylistAsync(target.Id, [song.Id]);
        await ShowMessageDialogAsync("提示", $"已添加到歌单「{target.Name}」", "确定");

        try
        {
            var libVm = ServiceHelper.GetRequiredService<LibraryViewModel>();
            await libVm.LoadCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[刷新歌单列表失败] {ex.Message}");
        }
    }
}
