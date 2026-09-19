using Microsoft.Maui.Controls.Shapes;   // RoundRectangle（圆角卡片 / chip）
using Microsoft.Maui.Layouts;           // FlexWrap / FlexDirection（标签 chip 自动换行）
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Services;

public static class SongMenuHelper
{
    /// <summary>
    /// 歌曲「更多」菜单。<paramref name="context"/> 是这首歌**所在的那张列表**，
    /// <paramref name="contextName"/> 是它的显示名（"发现" / 歌单名 / 歌手名 …）。
    ///
    /// ⚠️ 这两个参数不是可选的装饰：不传的话菜单只能把**这一首**塞进播放队列，
    /// 队列长度为 1 时「列表循环」的下一首等于它自己 —— 表现就是播完一遍又重播同一首
    /// （用户实测反馈过）。凡是调用方能拿到所在列表，就必须传进来。
    /// </summary>
    public static async Task ShowMenuAsync(SongDto song, IMusicApi api, PlayerService player,
                                           IReadOnlyList<SongDto>? context = null, string? contextName = null)
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
                PlayWithinList(player, song, context, contextName);
                await Shell.Current.GoToAsync("nowplaying");
            }
            else if (choice == "下一首播放")
            {
                // 队列为空时不能只把这一首塞进去：那样同样退化成「单曲循环」。
                // 有列表上下文就以列表入队（从这首歌开始），没有才退回单曲。
                if (player.Queue.Count == 0 && context is { Count: > 0 })
                    PlayWithinList(player, song, context, contextName);
                else
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

    // ===== 以「列表上下文」入队 ===============================================
    // 菜单里绝不能只把被点的那一首塞进队列：队列长度为 1 时，
    // 列表循环（`next = (index + 1) % count`）算出来还是它自己，
    // 用户看到的就是「列表循环模式下播完一遍又重播同一首」。
    // 因此只要拿得到所在列表，就整张列表入队，并把起点定在这首歌上。

    /// <summary>把 <paramref name="context"/> 整张列表入队并从 <paramref name="song"/> 处开始播放；
    /// 没有列表上下文时退回「单曲播放」。</summary>
    private static void PlayWithinList(PlayerService player, SongDto song,
                                       IReadOnlyList<SongDto>? context, string? contextName)
    {
        if (context is { Count: > 0 })
        {
            var index = IndexOfSong(context, song);
            player.PlayQueue(context, index >= 0 ? index : 0, contextName ?? "单曲播放");
            return;
        }
        player.PlayQueue([song], 0, contextName ?? "单曲播放");
    }

    /// <summary>在列表里找这首歌。列表项与被点项通常就是同一个实例，但用 Id 兜底更保险。</summary>
    private static int IndexOfSong(IReadOnlyList<SongDto> list, SongDto song)
    {
        for (var i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], song) || list[i].Id == song.Id) return i;
        return -1;
    }

    // ===== 弹层点击动效 ======================================================
    // 组成：① 进场（底部抽屉从屏下升上来 / 居中卡片放大淡入）
    //       ② 行点击反馈（底色闪一下主色）
    //       ③ 出场（抽屉滑回屏下 / 卡片缩回淡出）
    //
    // 三个硬约束，写错就会「看着没动效」：
    //   * **遮罩必须是面板的兄弟节点，不能给外层 Grid 设背景色再改它的 Opacity。**
    //     父容器的 Opacity 会把子元素一起淡掉 —— 遮罩淡出比面板滑出快时，
    //     面板还没滑走就已经透明了，整块看起来只是「淡出」而不是「滑走」（实测踩过）。
    //   * 进场**必须 await 完**再 await tcs —— 否则用户刚展开就点，
    //     出场动画和进场动画会同时改同一个 TranslationY，互相打架。
    //   * 出场**必须跑在拆掉面板之前** —— 提前 `detach()` 等于把面板瞬间抽走，动效根本来不及播。

    private const uint SheetInMs = 280;     // 抽屉升入
    private const uint SheetOutMs = 220;    // 抽屉降出
    private const uint SheetFadeInMs = 140; // 抽屉自身淡入（只为盖住挂树第一帧）
    private const uint ScrimInMs = 180;     // 遮罩淡入
    private const uint ScrimOutMs = 160;    // 遮罩淡出
    private const uint DialogInMs = 200;    // 居中卡片放大
    private const uint DialogOutMs = 150;   // 居中卡片缩回
    private const uint ToastInMs = 150;     // 轻提示淡入
    private const uint ToastHoldMs = 1500;  // 轻提示停留
    private const uint ToastOutMs = 200;    // 轻提示淡出

    /// <summary>
    /// 遮罩层：#80000000 的独立容器，透明度由自己控制。
    /// 放在 overlay 里当**面板的兄弟**（先 Add 遮罩、再 Add 面板），
    /// 这样「遮罩淡出」和「面板滑走」互不牵连。
    ///
    /// ⚠️ 用「带 BackgroundColor 的 Grid」而不是 `BoxView`：真机上 `BoxView.Color`
    /// 接 `#80000000` 会渲染成近乎不透明的黑（实测页面底色 246 → 10，
    /// 而正确值应是 123），整页被糊死。Grid 的 BackgroundColor 半透明正常。
    /// </summary>
    private static Grid MakeScrim() => new() { BackgroundColor = Color.FromArgb("#80000000"), Opacity = 0 };

    /// <summary>行点击反馈：底色闪一下（主色 10%）+ 轻微缩小，给点击一个即时回应。</summary>
    private static void FlashRow(VisualElement row)
    {
        row.BackgroundColor = Res("Primary", "#512BD4").WithAlpha(0.10f);
        _ = row.ScaleTo(0.98, 70, Easing.CubicOut);
    }

    /// <summary>
    /// 把弹层挂到当前页面上，返回「撤销挂载」的委托。
    ///
    /// ⚠️ **不要再用「把 page.Content 包一层新 Grid、结束时再还原」的写法。**
    /// 本文件原来就是那么写的，在 Windows 上会直接崩：
    ///
    ///     System.Runtime.InteropServices.COMException (0x800F1000)
    ///     没有检测到已安装的组件。   Source=WinRT.Runtime
    ///       ABI.System.Collections.Generic.IListMethods`2.AppendDynamic(...)
    ///       Microsoft.Maui.Handlers.LayoutHandler.SetVirtualView(...)
    ///       Microsoft.Maui.Handlers.ContentViewHandler.UpdateContent(...)
    ///       Microsoft.Maui.Controls.ContentPage.set_Content(...)
    ///
    /// 根因：MAUI 10.0.60 起，Windows 版 `ContentViewHandler.UpdateContent`
    /// 会先 `view.Handler?.DisconnectHandler()`（dotnet/maui#30047，为修 #29930），
    /// 于是替换 `ContentPage.Content` 时旧内容的 WinRT 引用被作废，
    /// 重新挂到新父级的那一刻就抛 COMException。Android 不受影响
    /// （原生 View 允许换父级，会自动从旧父级摘掉），所以只有 Windows 崩。
    ///
    /// 改法：**完全不动 page.Content**，把 overlay 直接加进页面根布局并铺满它。
    /// 本 App 所有 ContentPage 的根都是 `Grid`（PlaylistDetailPage 是
    /// `Auto,Auto,Auto,*`），所以跨满所有行列，弹层就不会像以前那样落进某个 Auto 行里。
    /// </summary>
    private static Action AttachOverlay(ContentPage page, View overlay)
    {
        if (page.Content is Grid rootGrid)
        {
            Grid.SetRow(overlay, 0);
            Grid.SetColumn(overlay, 0);
            Grid.SetRowSpan(overlay, Math.Max(1, rootGrid.RowDefinitions.Count));
            Grid.SetColumnSpan(overlay, Math.Max(1, rootGrid.ColumnDefinitions.Count));
            // 根 Grid 普遍带 Padding（PlaylistDetailPage 是 16,12），负 Margin 抵消掉，
            // 让遮罩铺满整屏、底部抽屉仍然贴屏底。padding 区在 Grid 自己的边界内，
            // 负 Margin 不会超出父级边界，所以不会被裁。
            var p = rootGrid.Padding;
            overlay.Margin = new Thickness(-p.Left, -p.Top, -p.Right, -p.Bottom);
            // MainPage 的汉堡按钮是 ZIndex=50，遮罩要盖在它上面（弹层期间不该点到它）
            overlay.ZIndex = 1000;
            rootGrid.Add(overlay);          // 加在最后 → Z 序在最上层
            return () => rootGrid.Remove(overlay);
        }

        // 兜底：根不是 Grid 的页面（目前没有）仍然只能包一层。
        // 但必须**先把旧内容从页面上摘下来**再装进包装层，否则它的平台视图还挂在页面上，
        // 重新 Add 会撞同一个 COMException。
        var oldContent = page.Content as View;
        page.Content = null;
        var wrapper = new Grid();
        if (oldContent is not null) wrapper.Add(oldContent);
        wrapper.Add(overlay);
        page.Content = wrapper;
        return () =>
        {
            wrapper.Remove(overlay);
            page.Content = null;
            if (oldContent is not null) page.Content = oldContent;
        };
    }

    /// <summary>
    /// 把抽屉挂到页面上并播「从屏下升入」动效，返回「撤销挂载」的委托。
    /// 抽屉是 VerticalOptions=End 锚在底部的，所以位移 = 自身高度时整块正好在可视区之外；
    /// 父 Grid 默认 clipChildren，滑出边界就被裁掉，不需要动 IsVisible。
    /// </summary>
    private static async Task<Action> SheetInAsync(ContentPage page, Grid overlay, VisualElement scrim, View sheet)
    {
        sheet.Opacity = 0;   // 挂树第一帧可能落在终点位置，先透明挡一下（由下面的 FadeTo 补回）

        var detach = AttachOverlay(page, overlay);

        // 等一次布局量出真实高度 —— 升入距离 = 抽屉自身高度，菜单行数多时不会「滑得飞快」。
        // 兜底 90ms：万一 SizeChanged 在订阅之前就发过了，也不至于卡住。
        var offset = 320d;
        var measured = new TaskCompletionSource();
        void OnSize(object? sender, EventArgs e) => measured.TrySetResult();
        sheet.SizeChanged += OnSize;
        await Task.WhenAny(measured.Task, Task.Delay(90));
        sheet.SizeChanged -= OnSize;
        if (sheet.Height > 0) offset = sheet.Height;

        sheet.TranslationY = offset;
        await Task.WhenAll(
            sheet.TranslateTo(0, 0, SheetInMs, Easing.CubicOut),
            sheet.FadeTo(1, SheetFadeInMs),
            scrim.FadeTo(1, ScrimInMs));

        return detach;
    }

    /// <summary>底部抽屉降出：整块滑回屏下（遮罩只淡出自己，不会把面板一起淡掉）。</summary>
    private static Task SheetOutAsync(VisualElement scrim, View sheet)
    {
        var offset = sheet.Height > 0 ? sheet.Height : 320d;
        return Task.WhenAll(
            sheet.TranslateTo(0, offset, SheetOutMs, Easing.CubicIn),
            scrim.FadeTo(0, ScrimOutMs));
    }

    /// <summary>居中对话框入场：遮罩淡入 + 卡片从 0.92 放大回 1，返回「撤销挂载」的委托。</summary>
    private static async Task<Action> DialogInAsync(ContentPage page, Grid overlay, VisualElement scrim, Border card)
    {
        card.Opacity = 0;
        card.Scale = 0.92;

        var detach = AttachOverlay(page, overlay);

        await Task.WhenAll(
            scrim.FadeTo(1, ScrimInMs),
            card.FadeTo(1, DialogInMs),
            card.ScaleTo(1, DialogInMs + 40, Easing.CubicOut));

        return detach;
    }

    /// <summary>居中对话框出场：卡片缩回 0.94 + 淡出，遮罩同时淡出。</summary>
    private static Task DialogOutAsync(VisualElement scrim, Border card) => Task.WhenAll(
        card.ScaleTo(0.94, DialogOutMs, Easing.CubicIn),
        card.FadeTo(0, DialogOutMs),
        scrim.FadeTo(0, DialogOutMs));

    internal static async Task<string?> ShowBottomSheetAsync(string title, List<string> options, HashSet<string>? disabledOptions = null)
    {
        var page = Shell.Current?.CurrentPage as ContentPage;
        if (page is null) return null;

        var tcs = new TaskCompletionSource<string?>();

        // 遮罩是独立一层（面板的兄弟），透明度各管各的，详见上方「弹层点击动效」的说明
        var overlay = new Grid();
        var scrim = MakeScrim();
        overlay.Add(scrim);

        var cancelTap = new TapGestureRecognizer();
        cancelTap.Tapped += (_, _) => tcs.TrySetResult(null);
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
                // 点击动效：先点亮这一行，再交出结果 —— 出场动画由 ShowBottomSheetAsync 统一播
                tap.Tapped += (_, _) => { FlashRow(row); tcs.TrySetResult(optText); };
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
        cancelTap2.Tapped += (_, _) => { FlashRow(cancelRow); tcs.TrySetResult(null); };
        cancelRow.GestureRecognizers.Add(cancelTap2);
        layout.Add(cancelRow);

        sheet.Content = layout;
        overlay.Add(sheet);

        // 覆盖层由 SheetInAsync 挂到页面根 Grid 上（跨满所有行列，不会落进某个 Auto 行），
        // 期间**不碰 page.Content** —— 原因见 AttachOverlay 的注释（Windows 换 Content 会崩）。
        var pageGone = false;
        EventHandler disappearingHandler = (_, _) => { pageGone = true; tcs.TrySetResult(null); };
        page.Disappearing += disappearingHandler;

        // 入场动效（抽屉从屏下升上来）；必须等它播完再 await tcs，
        // 否则用户一进来就点、会和入场动画抢 TranslationY。
        var detach = await SheetInAsync(page, overlay, scrim, sheet);

        var result = await tcs.Task;

        page.Disappearing -= disappearingHandler;

        // 出场动效：必须跑在拆掉面板之前，否则面板会被瞬间抽走、看不到动效。
        // 页面已经切走时跳过（动画播在没人看的界面上没意义）。
        if (!pageGone) await SheetOutAsync(scrim, sheet);

        detach();

        return result;
    }

    // ===== 居中圆角「对话框」 ================================================
    // 两类弹层分工（**只有这两种，别再引入第三种**）：
    //   ShowBottomSheetAsync —— 从底部升起的**选择列表**（歌单选择、歌曲菜单、音质选择）
    //   PresentDialogAsync   —— 屏幕居中的**对话框**（提示、二次确认、编辑表单、主题选择）
    //
    // 例外：ShowToastAsync 的轻提示（复制成功之类）不算弹层 —— 它**不吃触摸**、自动消失、
    // 不需要用户回应，所以既不替换 page.Content 也不参与上面的取舍。
    //
    // 对话框一律走 DialogBody/DialogTitle/DialogMessage/DialogButtons 这几个积木，
    // 保证「新建歌单」那套视觉（Border 圆角 20 + 投影 3,6 r18 + 取消/确定双按钮）是全站唯一标准。
    // 历史坑：早先 ShowMessageDialogAsync 系走 PresentSheetAsync 做成了「底部抽屉 + 左对齐灰标题
    // + 分隔线 + 整行按钮」，与居中卡片是两套完全不同的语言，同一个 App 里点两次弹窗长得不一样。

    /// <summary>提示对话框（单一「知道了」）。与「新建歌单」同款居中卡片，只有一颗主色按钮。</summary>
    public static async Task ShowMessageDialogAsync(string title, string message, string okText = "确定")
    {
        var tcs = new TaskCompletionSource<bool>();
        var body = DialogBody();

        body.Add(DialogTitle(title));
        body.Add(DialogMessage(message));
        body.Add(DialogSingleButton(okText, () => tcs.TrySetResult(true), Res("Primary", "#512BD4")));

        await PresentDialogAsync(body, tcs);
    }

    /// <summary>从应用资源（含合并字典）取颜色，取不到用兜底色。
    /// 强调色会随「主题颜色」切换，所以对话框不能把颜色写死。</summary>
    private static Color Res(string key, string fallback)
        => FindResource(Application.Current?.Resources, key) as Color ?? Color.FromArgb(fallback);

    private static object? FindResource(ResourceDictionary? dict, string key)
    {
        if (dict is null) return null;
        if (dict.TryGetValue(key, out var value) && value is not null) return value;
        foreach (var merged in dict.MergedDictionaries)
        {
            var hit = FindResource(merged, key);
            if (hit is not null) return hit;
        }
        return null;
    }

    private static VerticalStackLayout DialogBody()
        => new() { Spacing = 0, Padding = new Thickness(20, 18, 20, 16) };

    private static Label DialogTitle(string text) => new()
    {
        Text = text,
        FontSize = 17,
        FontAttributes = FontAttributes.Bold,
        TextColor = Res("Gray900", "#212121"),
        LineBreakMode = LineBreakMode.TailTruncation
    };

    /// <summary>对话框正文：14pt 灰字，与标题之间留 10dp。</summary>
    private static Label DialogMessage(string text) => new()
    {
        Text = text,
        FontSize = 14,
        TextColor = Res("Gray500", "#6E6E6E"),
        Margin = new Thickness(0, 10, 0, 0),
        LineBreakMode = LineBreakMode.WordWrap
    };

    /// <summary>对话框按钮的统一外形（取消/确定/单独一颗共用），只有底色和字色不同。</summary>
    private static Button DialogButton(string text, Color background, Color textColor) => new()
    {
        Text = text,
        FontSize = 15,
        CornerRadius = 12,
        HeightRequest = 44,
        Padding = new Thickness(0),
        BackgroundColor = background,
        TextColor = textColor
    };

    /// <summary>对话框里的一行按钮：左取消（浅灰）右确认（主色/危险色）。</summary>
    private static Grid DialogButtons(string cancelText, Action onCancel,
                                      string acceptText, Action onAccept,
                                      Color acceptBackground)
    {
        var row = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 18, 0, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        var cancel = DialogButton(cancelText, Res("SurfaceLight", "#F1F1F1"), Res("Gray600", "#404040"));
        cancel.Clicked += (_, _) => onCancel();

        var accept = DialogButton(acceptText, acceptBackground, Res("OnPrimary", "#FFFFFF"));
        accept.Clicked += (_, _) => onAccept();

        row.Add(cancel);
        row.Add(accept);
        Grid.SetColumn(accept, 1);
        return row;
    }

    /// <summary>只有一颗按钮的对话框（提示类）：按钮铺满整行 —— 两颗按钮里空着半行会更难看。</summary>
    private static Button DialogSingleButton(string text, Action onTap, Color background)
    {
        var button = DialogButton(text, background, Res("OnPrimary", "#FFFFFF"));
        button.Margin = new Thickness(0, 18, 0, 0);
        button.Clicked += (_, _) => onTap();
        return button;
    }

    /// <summary>把已搭好的内容以「屏幕居中的圆角卡片 + 遮罩」呈现，返回用户的选择。</summary>
    private static async Task<T?> PresentDialogAsync<T>(VerticalStackLayout body, TaskCompletionSource<T?> tcs)
    {
        var page = Shell.Current?.CurrentPage as ContentPage;
        if (page is null) return default;

        var overlay = new Grid();
        var scrim = MakeScrim();
        overlay.Add(scrim);
        var dismiss = new TapGestureRecognizer();
        dismiss.Tapped += (_, _) => tcs.TrySetResult(default);
        overlay.GestureRecognizers.Add(dismiss);

        var card = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) },
            BackgroundColor = Res("CardBackground", "#FFFFFF"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            MinimumWidthRequest = 300,
            MaximumWidthRequest = 460,
            Margin = new Thickness(24),
            Content = body,
            // 居中卡片四周都是留白，投影不会被父容器裁掉
            //（对比 Styles.xaml 里「投影落点必须留在元素自己的 Margin」那个坑）
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Colors.Black),
                Offset = new Point(3, 6),
                Radius = 18,
                Opacity = 0.3f
            }
        };

        overlay.Add(card);

        // 与 ShowBottomSheetAsync 同理：由 DialogInAsync 挂到页面根 Grid（跨满所有行列，
        // 居中卡片不会落进页面 Grid 的某一行里），期间不换 page.Content
        var pageGone = false;
        EventHandler disappearingHandler = (_, _) => { pageGone = true; tcs.TrySetResult(default); };
        page.Disappearing += disappearingHandler;

        var detach = await DialogInAsync(page, overlay, scrim, card);

        var result = await tcs.Task;

        page.Disappearing -= disappearingHandler;

        if (!pageGone) await DialogOutAsync(scrim, card);

        detach();

        return result;
    }

    /// <summary>居中的圆角二次确认框。<paramref name="destructive"/>=true 时确认键用危险色（删除等不可逆操作）。</summary>
    public static async Task<bool> ShowRoundedConfirmAsync(string title, string message,
                                                          string acceptText, string cancelText = "取消",
                                                          bool destructive = false)
    {
        var tcs = new TaskCompletionSource<bool>();
        var body = DialogBody();

        body.Add(DialogTitle(title));
        body.Add(DialogMessage(message));
        body.Add(DialogButtons(
            cancelText, () => tcs.TrySetResult(false),
            acceptText, () => tcs.TrySetResult(true),
            destructive ? Res("Danger", "#E5484D") : Res("Primary", "#512BD4")));

        return await PresentDialogAsync(body, tcs);
    }

    /// <summary>编辑歌单对话框的返回值。<see cref="CategoryId"/> 为 null 表示「清空标签」。</summary>
    public sealed record PlaylistEditResult(string Name, int? CategoryId);

    /// <summary>编辑歌单：改名 + 选标签（分区）。返回 null 表示用户取消。</summary>
    public static async Task<PlaylistEditResult?> ShowEditPlaylistAsync(
        string title, string initialName, IReadOnlyList<CategoryDto> categories, int? initialCategoryId,
        string acceptText = "保存")
    {
        var tcs = new TaskCompletionSource<PlaylistEditResult?>();
        var body = DialogBody();

        body.Add(DialogTitle(title));

        var nameEntry = new Entry
        {
            Text = initialName,
            FontSize = 16,
            HeightRequest = 46,
            Margin = new Thickness(0, 14, 0, 0),
            BackgroundColor = Res("SurfaceLight", "#F1F1F1"),
            TextColor = Res("Gray900", "#212121")
        };
        body.Add(nameEntry);

        var errorLabel = new Label
        {
            Text = "歌单名不能为空",
            FontSize = 12,
            TextColor = Res("Danger", "#E5484D"),
            IsVisible = false,
            Margin = new Thickness(0, 6, 0, 0)
        };
        body.Add(errorLabel);

        body.Add(new Label
        {
            Text = "标签",
            FontSize = 13,
            TextColor = Res("Gray500", "#6E6E6E"),
            Margin = new Thickness(0, 16, 0, 8)
        });

        // 标签单选：第一枚是「无标签」(Id=null)，其余来自 Categories 表
        int? selected = initialCategoryId;
        var chips = new List<(Border Chip, Label Text, int? Id)>();
        var wrap = new FlexLayout { Wrap = FlexWrap.Wrap, Direction = FlexDirection.Row };

        void Paint()
        {
            foreach (var (chip, text, id) in chips)
            {
                var isOn = Nullable.Equals(id, selected);
                chip.BackgroundColor = isOn ? Res("Primary", "#512BD4") : Res("SurfaceLight", "#F1F1F1");
                text.TextColor = isOn ? Res("OnPrimary", "#FFFFFF") : Res("Gray600", "#404040");
            }
        }

        void AddChip(int? id, string text)
        {
            var label = new Label
            {
                Text = text,
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };
            var chip = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(15) },
                Padding = new Thickness(14, 7),
                Margin = new Thickness(0, 0, 8, 8),
                Content = label
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => { selected = id; Paint(); };
            chip.GestureRecognizers.Add(tap);

            chips.Add((chip, label, id));
            wrap.Add(chip);
        }

        AddChip(null, "无标签");
        foreach (var category in categories) AddChip(category.Id, category.Name);
        Paint();
        body.Add(wrap);

        body.Add(DialogButtons(
            "取消", () => tcs.TrySetResult(null),
            acceptText, () =>
            {
                var name = nameEntry.Text?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    errorLabel.IsVisible = true;
                    return;   // 不关闭对话框，让用户就地改
                }
                tcs.TrySetResult(new PlaylistEditResult(name, selected));
            },
            Res("Primary", "#512BD4")));

        return await PresentDialogAsync(body, tcs);
    }

    /// <summary>
    /// 主题颜色选择。和「新建歌单」同一套居中卡片：标题 + 一排圆形色块 + 取消/确定。
    ///
    /// 选中态**只改本地变量**，点「确定」才返回 key —— 与新建歌单「选好标签再创建」一致；
    /// 取消返回 null，主题不会被改，调用方不需要任何回滚（旧实现是「点一下就立即生效」，
    /// 那个「取消」按钮其实是摆设）。
    ///
    /// 色块尺寸 48dp、间距 8dp 是有意的常量：卡片宽度由内容撑开（300~460dp），
    /// 一排 5 个刚好把内容区占满，不要改成「填满等分格」——等分格里再写死宽度就会
    /// 溢出被裁（见 MEMORY「固定尺寸子元素放进 * 等分 Grid」那条）。
    /// </summary>
    public static async Task<string?> ShowThemePickerAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        var body = DialogBody();

        body.Add(DialogTitle("选择主题颜色"));

        var themes = ThemeService.Instance.Themes;
        var selected = ThemeService.Instance.CurrentKey;

        var row = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var marks = new List<(string Key, Label Mark)>();
        void Paint()
        {
            foreach (var (key, mark) in marks) mark.IsVisible = key == selected;
        }

        foreach (var theme in themes)
        {
            // 勾选标记用 OnPrimary 色：紫/红/橙/蓝上是白勾，纯白主题上是深色勾，都可读
            var mark = new Label
            {
                FontFamily = "MaterialIcons",
                Text = "\uE5CA",
                FontSize = 24,
                TextColor = Color.FromArgb(theme.OnPrimary),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                IsVisible = false
            };
            // 用 Border 而不是已过时的 Frame（CS0618）。描边是为了让「纯白」主题的色块
            // 在白卡片上仍有轮廓。
            var swatch = new Border
            {
                WidthRequest = 48,
                HeightRequest = 48,
                StrokeThickness = 1,
                Stroke = Color.FromArgb("#C4C4CC"),
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(24) },
                BackgroundColor = Color.FromArgb(theme.Primary),
                Content = mark
            };
            var tap = new TapGestureRecognizer();
            var key = theme.Key;
            tap.Tapped += (_, _) => { selected = key; Paint(); };
            swatch.GestureRecognizers.Add(tap);

            marks.Add((key, mark));
            row.Add(swatch);
        }
        Paint();
        body.Add(row);

        body.Add(DialogButtons(
            "取消", () => tcs.TrySetResult(null),
            "确定", () => tcs.TrySetResult(selected),
            Res("Primary", "#512BD4")));

        return await PresentDialogAsync(body, tcs);
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

    // ===== 轻提示（toast） ===================================================
    // Android 与 Windows 共用同一套「页内浮层」实现，而不是各平台原生 Toast：
    //   · Windows 桌面端没有与 Android Toast 对等的原生控件，自绘才能保证两端观感一致；
    //   · 本 App 所有浮层都是代码拼的深色圆角面板，自绘天然沿用同一套视觉语言。
    // 行为约定：**不吃触摸**（提示期间照样能点播放/切歌）、约 1.5s 后自动淡出、同一时刻只留一条。

    private static View? _activeToast;
    private static Action? _activeToastDetach;

    /// <summary>
    /// 弹一条自动消失的轻提示（"复制「xxx」成功" 之类）。
    /// <paramref name="page"/> 省略时取 Shell 当前页；页面取不到就静默跳过
    /// —— 提示只是反馈，不该因为它失败把主流程（复制本身）带崩。
    /// </summary>
    public static async Task ShowToastAsync(string message, ContentPage? page = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        page ??= Shell.Current?.CurrentPage as ContentPage;
        if (page is null) return;

        // 上一条还没退场就先摘掉，避免两条提示叠在一起
        DismissActiveToast();

        var label = new Label
        {
            Text = message,
            FontSize = 14.5,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2,          // 长歌名撑两行封顶，不会把提示条拉成一堵墙
        };

        var card = new Border
        {
            // 深色半透明：播放页本身就是深色亚克力，浅色页面上对比也够
            BackgroundColor = Color.FromArgb("#E6202030"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Padding = new Thickness(18, 10),
            Margin = new Thickness(40, 0, 40, 96),   // 底边留出播放控制条的高度
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            MaximumWidthRequest = 320,
            Content = label,
        };

        // 整层输入穿透（含子元素），否则提示停留的 1.5s 会变成一片点不动的死区
        var overlay = new Grid { InputTransparent = true, CascadeInputTransparent = true, Opacity = 0 };
        overlay.Add(card);

        var detach = AttachOverlay(page, overlay);
        _activeToast = overlay;
        _activeToastDetach = detach;

        try
        {
            await overlay.FadeTo(1, ToastInMs, Easing.CubicOut);
            await Task.Delay(TimeSpan.FromMilliseconds(ToastHoldMs));
            if (!ReferenceEquals(_activeToast, overlay)) return;   // 已被新的一条顶掉
            await overlay.FadeTo(0, ToastOutMs, Easing.CubicIn);
        }
        catch (Exception ex)
        {
            // 页面在提示期间被销毁/替换时动画可能失败：提示丢了没关系，绝不能往上抛
            System.Diagnostics.Debug.WriteLine($"[Toast] {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_activeToast, overlay))
            {
                _activeToast = null;
                _activeToastDetach = null;
                detach();
            }
        }
    }

    /// <summary>摘掉当前提示（同一条只摘一次：detach 的字段先清空再执行）。</summary>
    private static void DismissActiveToast()
    {
        var detach = _activeToastDetach;
        _activeToastDetach = null;
        _activeToast = null;
        detach?.Invoke();
    }
}
