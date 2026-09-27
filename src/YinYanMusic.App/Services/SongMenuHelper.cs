using Microsoft.Maui.Controls.Shapes;   // RoundRectangle（圆角卡片 / chip）
using Microsoft.Maui.Layouts;           // FlexWrap / FlexDirection（标签 chip 自动换行）
using YinYanMusic.App.Services.LocalLibrary;
using YinYanMusic.App.ViewModels;
using YinYanMusic.Core;
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

            // 歌手信息跳转 / 关注歌手：只认**有 ID 的**歌手（本地歌与离线缓存没有 → 两项都不显示）。
            // 联合创作时有几位就列几位，别只认主歌手 —— 菜单项文案带上人数，点开是选择/关注列表。
            var credits = SongArtistSheets.Followable(song);
            var artistLabel = credits.Count > 0
                ? $"歌手：{credits[0].Name}" + (credits.Count > 1 ? $" 等 {credits.Count} 人" : string.Empty)
                : null;

            // 多歌手时是"打开一层列表"，所以用省略号（与「添加到歌单...」同一约定）；
            // 单歌手才是直接的关注 / 取消关注开关。
            string? followLabel = null;
            if (credits.Count == 1)
            {
                var followed = await IsArtistFollowedAsync(api, credits[0].Id);
                followLabel = followed ? "取消关注歌手" : "关注歌手";
            }
            else if (credits.Count > 1)
            {
                followLabel = "关注歌手...";
            }

            var options = new List<string> { "播放", "下一首播放" };
            if (artistLabel is not null) options.Add(artistLabel);
            options.Add(likeLabel);
            if (followLabel is not null) options.Add(followLabel);
            options.Add("添加到歌单...");

            // V2.9 评论：入口文案带上评论数。本地歌走不到这里（那条分支是 ShowLocalMenuAsync）
            options.Add(await GetCommentMenuLabelAsync(api, CommentTargets.Song, song.Id));

            // V2.7 缓存：在线歌才有这一项（本地歌在 ShowLocalMenuAsync 那条分支里）
            var cacheLabel = await GetCacheMenuLabelAsync(song);
            if (cacheLabel is not null) options.Add(cacheLabel);

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
                var artistId = await SongArtistSheets.PickArtistAsync(credits);
                if (artistId is long id) await Shell.Current.GoToAsync($"artist?artistId={id}");
            }
            else if (choice == likeLabel)
            {
                if (isLiked) { await api.UnlikeAsync(song.Id); song.IsLiked = false; }
                else { await api.LikeAsync(song.Id); song.IsLiked = true; }
            }
            else if (followLabel is not null && choice == followLabel)
            {
                var outcome = await SongArtistSheets.FollowAsync(api, credits);
                if (outcome is not null) await ShowMessageDialogAsync("提示", outcome.Message, "确定");
            }
            else if (choice == "添加到歌单...")
            {
                await ShowAddToPlaylistAsync(song, api);
            }
            else if (IsCommentOption(choice))
            {
                await ShowCommentsAsync(song.Title, CommentTargets.Song, song.Id);
            }
            else if (cacheLabel is not null && choice == cacheLabel)
            {
                await RunCacheMenuActionAsync(cacheLabel, song);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[菜单异常] {ex.Message}");
        }
    }

    /// <summary>
    /// 「更多」菜单里那条「查看评论：(N)」的文案前缀。它带数字，认不出等值比较，
    /// 所以两处菜单（在线歌曲菜单、歌单详情页里手写的歌曲菜单）统一用 <see cref="IsCommentOption"/> 判断。
    /// </summary>
    public const string CommentOptionPrefix = "查看评论";

    /// <summary>用户选的是不是「查看评论：(N)」那一项。</summary>
    public static bool IsCommentOption(string? choice) =>
        choice is not null && choice.StartsWith(CommentOptionPrefix, StringComparison.Ordinal);

    /// <summary>
    /// 组出「查看评论：(N)」。取不到数（离线、接口失败）就退化成不带数字的「查看评论」——
    /// 让整条入口消失，比少一个数字糟糕得多。
    /// </summary>
    public static async Task<string> GetCommentMenuLabelAsync(IMusicApi api, string targetType, long targetId)
    {
        var count = await api.GetCommentCountAsync(targetType, targetId);
        return count is int n ? $"{CommentOptionPrefix}：({n})" : CommentOptionPrefix;
    }

    // ===== V2.6 本地歌菜单 =====================================================
    // 本地歌没有 songId，也没有在线歌手，所以在线那套选项（喜欢 / 添加到歌单 /
    // 歌手跳转 / 关注歌手）**一个都不能出现**：
    //   - "添加到歌单"会往在线歌单里塞一个服务端不认识的负数 Id，换设备必然变成空歌（TC-2.6-10）；
    //   - "喜欢"要 PUT api/songs/{id}/like，负数 Id 必然 404；
    //   - "歌手：xx"跳转需要 ArtistId，本地歌是 0。
    // 这里单独走一个精简菜单，而不是给 ShowMenuAsync 加一堆 if ——
    // 本地/在线的语义差异太大，混在一起容易改坏在线那条路径。

    /// <summary>
    /// 本地歌的「更多」菜单（V2.6）。只有本地语义的选项：
    /// 播放 / 下一首播放 / 查看文件位置 / 从曲库移除。
    /// </summary>
    public static async Task ShowLocalMenuAsync(SongDto song, PlayerService player,
                                                IReadOnlyList<SongDto>? context = null)
    {
        try
        {
            var options = new List<string> { "播放", "下一首播放", "查看文件位置", "从本地曲库移除" };

            var choice = await ShowBottomSheetAsync(song.Title, options);
            if (choice is null) return;

            if (choice == "播放")
            {
                if (player.Current?.Id == song.Id) { await Shell.Current.GoToAsync("nowplaying"); return; }
                PlayWithinList(player, song, context, "本地音乐");
                await Shell.Current.GoToAsync("nowplaying");
            }
            else if (choice == "下一首播放")
            {
                if (player.Queue.Count == 0 && context is { Count: > 0 })
                    PlayWithinList(player, song, context, "本地音乐");
                else
                    player.InsertNext(song);
            }
            else if (choice == "查看文件位置")
            {
                await ShowMessageDialogAsync("文件位置", song.AudioUrl, "确定");
            }
            else if (choice == "从本地曲库移除")
            {
                var confirm = await ShowRoundedConfirmAsync(
                    "移除记录",
                    $"确定从本地曲库中移除「{song.Title}」吗？\n\n设备上的音乐文件不会被删除。",
                    "移除", "取消", destructive: true);
                if (!confirm) return;

                var store = ServiceHelper.GetService<LocalLibraryStore>();
                if (store is null) return;
                await store.RemoveAsync(LocalLibraryStore.ToLocalId(song.Id));
                await ShowToastAsync("已从本地曲库移除");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[本地菜单异常] {ex.Message}");
        }
    }

    // ===== V2.7 歌曲缓存菜单项 ================================================
    // 缓存是"在线歌"才有的概念（本地曲库的歌本来就在本机、没有 songId），
    // 所以菜单项的文案与动作都收口在这里：菜单里出现哪个词，就由这里决定它干什么。
    // PlaylistDetailPage 有自己的菜单实现（不走 ShowMenuAsync），靠这两个方法复用同一套语义，
    // 避免"歌曲菜单里叫缓存到本地、歌单里叫下载"这种同一功能两种叫法。

    /// <summary>未缓存时的菜单文案。</summary>
    public const string CacheAction = "缓存到本地";

    /// <summary>已缓存时的菜单文案。</summary>
    public const string UncacheAction = "移除本地缓存";

    /// <summary>
    /// 取这首歌在菜单里应显示的缓存项文案（已缓存 → "移除本地缓存"）。
    /// 会先确保缓存索引加载完成，否则刚启动时会把已缓存的歌误判成未缓存。
    /// 缓存服务缺失（理论上不会）时返回 null，调用方据此不显示该菜单项。
    /// </summary>
    public static async Task<string?> GetCacheMenuLabelAsync(SongDto song)
    {
        if (song.IsLocal || song.Id <= 0) return null;   // 本地歌没有缓存概念（TC-2.6-10）

        var cache = ServiceHelper.GetService<CacheStore>();
        if (cache is null) return null;

        await cache.EnsureIndexAsync();
        return cache.IsCached(song.Id) ? UncacheAction : CacheAction;
    }

    /// <summary>
    /// 执行缓存的菜单动作。<paramref name="label"/> 是 <see cref="GetCacheMenuLabelAsync"/>
    /// 返回的那个文案（菜单统一用"文案即动作"的模式，见 ShowMenuAsync）。
    /// </summary>
    public static async Task RunCacheMenuActionAsync(string label, SongDto song)
    {
        var cache = ServiceHelper.GetService<CacheStore>();
        if (cache is null) return;

        if (label == UncacheAction)
        {
            var confirm = await ShowRoundedConfirmAsync(
                "移除本地缓存",
                $"确定删除「{song.Title}」的本地缓存吗？\n\n删除后离线将无法播放这首歌，联网时可重新缓存。",
                "移除", "取消", destructive: true);
            if (!confirm) return;

            await cache.RemoveAsync(song.Id);
            await ShowToastAsync("已移除本地缓存");
            return;
        }

        // 缓存到本地：容量预检/并发/断点续传/重试都在下载服务里，这里只负责交互。
        // 交互用**进度浮层**（TC-2.7-01 要求"进度可见"）：下载期间一直显示歌名 + 百分比 + 进度条，
        // 完成/失败后短暂停留再淡出；成功不再额外弹 Toast（浮层已经说了"已缓存 ✓"），
        // 失败则交给下面的对话框给出完整原因与下一步动作（TC-2.7-07）。
        var downloader = ServiceHelper.GetService<CacheDownloadService>();
        if (downloader is null) return;

        var result = await RunCacheWithProgressAsync(song, downloader);
        if (result.Status is not (CacheEnqueueStatus.Queued or CacheEnqueueStatus.AlreadyCached))
            await ShowMessageDialogAsync("缓存未完成", result.Message, "知道了");
    }

    // ===== 缓存下载进度浮层（V2.7） ==========================================
    // 为什么要有它：点「缓存到本地」后，一首 40MB 的 FLAC 可能要下十几秒甚至更久，
    // 期间如果界面毫无反馈，用户只会觉得"点了没反应"（这正是验收用例 TC-2.7-01 的诉求）。
    // 形态与 Toast 一致（页内浮层、不吃触摸、可继续操作），只是**会停留到下载结束**。

    private static View? _activeCacheOverlay;
    private static Action? _activeCacheDetach;

    /// <summary>
    /// 带进度浮层地执行一次手动缓存：立刻显示浮层 → 订阅下载进度 → 完成后把浮层改成
    /// "已缓存 ✓"（失败则改成原因）→ 淡出。拿不到当前页面时退化为无浮层的等待。
    /// </summary>
    private static async Task<CacheEnqueueResult> RunCacheWithProgressAsync(SongDto song, CacheDownloadService downloader)
    {
        var page = Shell.Current?.CurrentPage as ContentPage;
        if (page is null) return await downloader.EnqueueAndWaitAsync(song);

        // 同一时刻只留一个缓存浮层（用户连点两首时，前一个直接让位）
        DismissActiveCacheOverlay();

        var titleLabel = new Label
        {
            Text = "正在缓存",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
        };
        var nameLabel = new Label
        {
            Text = song.Title,
            FontSize = 12,
            TextColor = Color.FromArgb("#D8D8E0"),
            LineBreakMode = LineBreakMode.TailTruncation,
            Margin = new Thickness(0, 2, 0, 8),
        };
        var bar = new ProgressBar
        {
            Progress = 0,
            ProgressColor = Res("Primary", "#512BD4"),
            BackgroundColor = Color.FromArgb("#3A3A4A"),
            HeightRequest = 4,
            VerticalOptions = LayoutOptions.Center,
        };
        var percentLabel = new Label
        {
            Text = "0%",
            FontSize = 11,
            TextColor = Color.FromArgb("#D8D8E0"),
            WidthRequest = 46,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalOptions = LayoutOptions.Center,
        };

        var barRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8,
        };
        barRow.Add(bar);
        Grid.SetColumn(percentLabel, 1);
        barRow.Add(percentLabel);

        var body = new VerticalStackLayout { Spacing = 0 };
        body.Add(titleLabel);
        body.Add(nameLabel);
        body.Add(barRow);

        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#E6202030"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(16) },
            Padding = new Thickness(16, 12),
            // 底边留出播放控制条的高度，与 Toast 同一套定位
            Margin = new Thickness(40, 0, 40, 96),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            MinimumWidthRequest = 280,
            MaximumWidthRequest = 380,
            Content = body,
        };

        // 整层输入穿透：下载期间用户照样能切歌/翻列表，不该被一个进度条挡住
        var overlay = new Grid { InputTransparent = true, CascadeInputTransparent = true, Opacity = 0 };
        overlay.Add(card);

        var detach = AttachOverlay(page, overlay);
        _activeCacheOverlay = overlay;
        _activeCacheDetach = detach;

        void OnProgress(object? sender, CacheDownloadProgress progress)
        {
            if (progress.SongId != song.Id) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (progress.TotalBytes > 0)
                {
                    bar.Progress = Math.Clamp(progress.Percent / 100d, 0, 1);
                    percentLabel.Text = $"{progress.Percent:F0}%";
                }
                else
                {
                    // 服务器没给 Content-Length：给不出百分比就报已下载的体积
                    percentLabel.Text = CachedSong.FormatSize(progress.BytesReceived);
                }
            });
        }

        downloader.ProgressChanged += OnProgress;

        try
        {
            await overlay.FadeTo(1, 160, Easing.CubicOut);

            var result = await downloader.EnqueueAndWaitAsync(song);

            var succeeded = result.Status is CacheEnqueueStatus.Queued or CacheEnqueueStatus.AlreadyCached;
            if (succeeded)
            {
                titleLabel.Text = result.Status == CacheEnqueueStatus.Queued ? "已缓存 ✓" : "已在本地缓存 ✓";
                bar.Progress = 1;
                percentLabel.Text = "100%";
                await Task.Delay(1000);
            }
            else
            {
                // 失败/被拒：浮层只做短暂提示，完整原因由调用方的对话框给出
                titleLabel.Text = "缓存未完成";
                titleLabel.TextColor = Color.FromArgb("#FF8A8A");
                nameLabel.Text = result.Message;
                await Task.Delay(1600);
            }

            await overlay.FadeTo(0, 200, Easing.CubicIn);
            return result;
        }
        catch (Exception ex)
        {
            // 不在这里重试下载（那会变成"失败一次下两次"）：如实报错，交给调用方提示
            System.Diagnostics.Debug.WriteLine($"[缓存进度浮层] {ex.Message}");
            return new CacheEnqueueResult(CacheEnqueueStatus.Failed, $"缓存失败：{ex.Message}");
        }
        finally
        {
            downloader.ProgressChanged -= OnProgress;
            if (ReferenceEquals(_activeCacheOverlay, overlay))
            {
                _activeCacheOverlay = null;
                _activeCacheDetach = null;
                detach();
            }
        }
    }

    /// <summary>摘掉当前的缓存进度浮层（同一条只摘一次：字段先清空再执行）。</summary>
    private static void DismissActiveCacheOverlay()
    {
        var detach = _activeCacheDetach;
        _activeCacheDetach = null;
        _activeCacheOverlay = null;
        detach?.Invoke();
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

        // 内容超过屏高 70% 时在面板内部滚动：列表本来没有高度上限，
        // 行数一多（联合创作可以有 10 位歌手，「关注歌手」列表就是 10 行 + 表头 + 取消）
        // 整个面板会顶出屏幕上方，排在最前的那几行就永远点不到了。
        //
        // ⚠️ 判据只能用"行数 × 行高"这种**跟布局无关**的估算，不能让 ScrollView 自己去量：
        // Android 上 ScrollView 会撑满给它的可用高度（内容的 VerticalOptions 默认 Fill 也一样被拉伸），
        // 所以「MaximumHeightRequest = 屏高70%」或「按 layout.Height 收口」都会得到同一个结果 ——
        // 三行的「选择歌手」也被撑到屏高 70%，菜单下面留一大片空白（2026-09-23 真机截图）。
        // 行高是上面写死的 48（dp），估算差几个像素只影响"要不要滚动"，不影响观感。
        const double rowHeight = 48;
        var estimatedContentHeight = rowHeight * (options.Count + 2) + 60;   // + 表头/取消/分隔条/灰条
        // 上限取屏高 80%：导入时最多 10 位歌手，「关注歌手」最多 12 行（≈630dp），
        // 800dp 高的手机/平板正好放得下 —— 那样连"取消"都看得到，不用滚。再多才滚。
        var maxSheetHeight = page.Height > 0 ? page.Height * 0.8 : 600;
        sheet.Content = estimatedContentHeight > maxSheetHeight
            ? new ScrollView { Content = layout, HeightRequest = maxSheetHeight }
            : layout;
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

    // ===== 评论面板（V2.9）===================================================
    // 歌曲与歌单共用一个面板，对象由 targetType + targetId 决定（取值见 Core 的 CommentTargets）。
    //
    // 骨架仍是「底部抽屉 + 独立遮罩 + AttachOverlay」，但**不复用 ShowBottomSheetAsync**：
    // 那个是「选项列表 → 点一行就返回并关闭」的一次性菜单，而评论面板要能停留、滚动、
    // 输入与二次交互，属于另一类内容。
    //
    // 列表用 VerticalStackLayout 手工堆，不用 CollectionView：抽屉高度是固定的，
    // CollectionView 在 Android 上遇到空列表量不出高度（ZonePage 就是因此改成
    // ScrollView + BindableLayout 的），而一页只有 20 条，手工堆更省心。

    private const int CommentPageSize = 20;

    /// <summary>
    /// 评论面板。<paramref name="title"/> 是展示用的对象名（歌名 / 歌单名），
    /// <paramref name="targetType"/> 取 <see cref="CommentTargets.Song"/> 或 <see cref="CommentTargets.Playlist"/>。
    /// <para>
    /// 未登录也能查看（列表接口公开可读）；发表 / 回复 / 点赞 / 删除会先要求登录 ——
    /// 与「喜欢、收藏」那种直接请求、失败静默的旧做法不同，评论是**用户自己的发声**，
    /// 静默失败会让人以为发出去了。
    /// </para>
    /// </summary>
    public static async Task ShowCommentsAsync(string title, string targetType, long targetId)
    {
        var page = Shell.Current?.CurrentPage as ContentPage;
        if (page is null) return;

        var api = ServiceHelper.GetRequiredService<IMusicApi>();
        var auth = ServiceHelper.GetRequiredService<IAuthService>();

        var tcs = new TaskCompletionSource<bool>();
        // 面板还开着时页面被切走（返回手势等）→ 收起并结束等待；ReloadAsync 也靠它避免回填已拆掉的视图
        var pageGone = false;

        var overlay = new Grid();
        var scrim = MakeScrim();
        overlay.Add(scrim);

        var dismiss = new TapGestureRecognizer();
        dismiss.Tapped += (_, _) => tcs.TrySetResult(true);
        overlay.GestureRecognizers.Add(dismiss);

        // —— 顶栏：对象名 + 条数 + 关闭 ——
        var header = new Grid { ColumnSpacing = 8, Padding = new Thickness(16, 14, 12, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var countLabel = new Label { Text = "评论", FontSize = 12, TextColor = Res("Gray400", "#9E9E9E") };
        var headerStack = new VerticalStackLayout { Spacing = 2 };
        headerStack.Add(new Label
        {
            Text = title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Res("Gray900", "#212121"),
            LineBreakMode = LineBreakMode.TailTruncation
        });
        headerStack.Add(countLabel);
        header.Add(headerStack);

        var closeLabel = new Label
        {
            Text = "✕",
            FontSize = 17,
            TextColor = Res("Gray500", "#6E6E6E"),
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(10, 0)
        };
        var closeTap = new TapGestureRecognizer();
        closeTap.Tapped += (_, _) => tcs.TrySetResult(true);
        closeLabel.GestureRecognizers.Add(closeTap);
        header.Add(closeLabel);
        Grid.SetColumn(closeLabel, 1);

        // —— 列表区 ——
        var list = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(16, 0, 16, 4) };
        var emptyLabel = new Label
        {
            Text = "还没有评论，来说两句。",
            FontSize = 13,
            TextColor = Res("Gray400", "#9E9E9E"),
            Margin = new Thickness(0, 28),
            HorizontalOptions = LayoutOptions.Center
        };

        var moreButton = new Button
        {
            Text = "加载更多",
            FontSize = 13,
            HeightRequest = 40,
            CornerRadius = 12,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 10, 0, 0),
            BackgroundColor = Res("SurfaceLight", "#F1F1F1"),
            TextColor = Res("Gray600", "#404040")
        };

        var footerHint = new Label
        {
            FontSize = 12,
            TextColor = Res("Gray400", "#9E9E9E"),
            Margin = new Thickness(0, 8, 0, 8),
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };

        var footer = new VerticalStackLayout { Spacing = 0 };
        footer.Add(moreButton);
        footer.Add(footerHint);

        var scroll = new ScrollView { Content = list };

        // —— 输入区 ——
        var replyLabel = new Label
        {
            FontSize = 12,
            TextColor = Res("Primary", "#512BD4"),
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalOptions = LayoutOptions.Center
        };
        var replyCancel = new Label
        {
            Text = "✕",
            FontSize = 13,
            TextColor = Res("Gray500", "#6E6E6E"),
            Padding = new Thickness(10, 0),
            VerticalOptions = LayoutOptions.Center
        };
        var replyBar = new Grid { IsVisible = false, ColumnSpacing = 4, Margin = new Thickness(0, 0, 0, 6) };
        replyBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        replyBar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        replyBar.Add(replyLabel);
        replyBar.Add(replyCancel);
        Grid.SetColumn(replyCancel, 1);

        var entry = new Entry
        {
            Placeholder = "说点什么…",
            FontSize = 15,
            HeightRequest = 44,
            // 与服务端 500 字上限一致：前端先挡住，用户不必发出去才知道超长
            MaxLength = 500,
            BackgroundColor = Res("SurfaceLight", "#F1F1F1"),
            TextColor = Res("Gray900", "#212121"),
            PlaceholderColor = Res("Gray400", "#9E9E9E")
        };

        var sendButton = new Button
        {
            Text = "发送",
            FontSize = 14,
            HeightRequest = 42,
            WidthRequest = 68,
            CornerRadius = 12,
            Padding = new Thickness(0),
            BackgroundColor = Res("Primary", "#512BD4"),
            TextColor = Res("OnPrimary", "#FFFFFF")
        };

        var inputRow = new Grid { ColumnSpacing = 8 };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        inputRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        inputRow.Add(entry);
        inputRow.Add(sendButton);
        Grid.SetColumn(sendButton, 1);

        var inputArea = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(16, 8, 16, 12) };
        inputArea.Add(new BoxView { HeightRequest = 0.5, Color = Res("CardBorder", "#E4E4EC"), Margin = new Thickness(-16, 0, -16, 8) });
        inputArea.Add(replyBar);
        inputArea.Add(inputRow);

        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        body.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        body.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        body.Add(header);
        body.Add(scroll);
        Grid.SetRow(scroll, 1);
        body.Add(inputArea);
        Grid.SetRow(inputArea, 2);

        var sheet = new Frame
        {
            CornerRadius = 16,
            BackgroundColor = Res("CardBackground", "#FFFFFF"),
            VerticalOptions = LayoutOptions.End,
            Padding = new Thickness(0),
            HasShadow = true,
            Content = body
        };
        // 固定占屏高 72%：评论列表必须能滚（按内容自适应会让长列表把面板顶出屏幕上方）。
        // 页面还没量出高度时给个兜底值，入场动画只用它算"从屏下升入"的距离。
        sheet.HeightRequest = page.Height > 0 ? page.Height * 0.72 : 520;
        overlay.Add(sheet);

        // —— 状态与行为 ——
        var loadedPages = 1;
        var total = 0;
        var loading = false;
        CommentDto? replyTo = null;
        // 服务端每条主评论最多带回 200 条子孙，超出的由"展开全部"分页追加：
        // rootId → 已取回的扁子孙（Id 升序，父节点必先于子节点出现），以及已取到第几页
        var expandedRoots = new Dictionary<long, List<CommentDto>>();
        var expandedPages = new Dictionary<long, int>();
        var expandedChildMap = new Dictionary<long, List<CommentDto>>();
        var currentRoots = new List<CommentDto>();
        var rootHasMore = false;

        void SetReplyTarget(CommentDto? target)
        {
            replyTo = target;
            replyBar.IsVisible = target is not null;
            if (target is not null)
            {
                replyLabel.Text = $"回复 @{target.UserName}";
                entry.Focus();
            }
        }

        async Task<bool> EnsureLoggedInAsync()
        {
            if (auth.IsLoggedIn) return true;
            await ShowMessageDialogAsync("需要登录", "登录后才能发表评论、回复或点赞。");
            return false;
        }

        async Task ReloadAsync()
        {
            if (loading || pageGone) return;
            loading = true;
            footerHint.Text = "正在加载…";
            footerHint.IsVisible = true;

            var items = new List<CommentDto>();
            var failed = false;
            for (var p = 1; p <= loadedPages; p++)
            {
                var result = await api.GetCommentsAsync(targetType, targetId, p, CommentPageSize);
                if (result is null) { failed = true; break; }
                total = result.Total;
                items.AddRange(result.Items);
                if (result.Items.Count < CommentPageSize) break;
            }

            loading = false;
            if (pageGone) return;

            // 重新拉过就丢掉旧的"展开全部"结果：里面可能还含着刚被删掉的那几条
            expandedRoots.Clear();
            expandedPages.Clear();
            currentRoots = items;
            rootHasMore = items.Count < total;

            countLabel.Text = $"共 {total} 条评论";
            // 标题用"全部评论数"（含回复），与「更多」菜单里那条「查看评论：(N)」同一个口径 ——
            // 列表的 total 只数主评论，拿它当标题会和菜单上的数字对不上。
            if (await api.GetCommentCountAsync(targetType, targetId) is int visibleCount)
                countLabel.Text = $"共 {visibleCount} 条评论";
            footerHint.Text = "评论加载失败，请检查网络后重试。";
            footerHint.IsVisible = failed;
            Render();
        }

        /// <summary>
        /// 把"展开全部"取回来的扁子孙按父节点分组。没展开过的主评论不受影响 ——
        /// 它们的层级是服务端在列表里就装配好的（<see cref="CommentDto.Replies"/>）。
        /// </summary>
        void RebuildChildMap()
        {
            expandedChildMap.Clear();
            foreach (var flat in expandedRoots.Values)
                foreach (var node in flat)
                {
                    if (node.ParentId is not long parentId) continue;
                    if (!expandedChildMap.TryGetValue(parentId, out var kids)) expandedChildMap[parentId] = kids = [];
                    kids.Add(node);
                }
        }

        IReadOnlyList<CommentDto> ChildrenOf(CommentDto node) =>
            expandedChildMap.TryGetValue(node.Id, out var kids) ? kids : node.Replies;

        void Render()
        {
            RebuildChildMap();
            list.Clear();
            if (currentRoots.Count == 0) list.Add(emptyLabel);
            foreach (var root in currentRoots) list.Add(BuildCommentRow(root, null));
            moreButton.IsVisible = rootHasMore;
            list.Add(footer);
        }

        /// <summary>
        /// "展开全部"：第一次取的粒度与服务端在列表里给的那一批**完全一致**（同一可见性规则、
        /// 同一 Id 升序、同样是 200 条），所以展开不会出现"越展开越少"；之后每点一次追加下一页。
        /// </summary>
        async Task ExpandAsync(CommentDto root)
        {
            const int pageSize = 200;

            var nextPage = (expandedPages.TryGetValue(root.Id, out var done) ? done : 0) + 1;
            try
            {
                var page = await api.GetCommentRepliesAsync(root.Id, nextPage, pageSize);
                if (page is null) { await ShowToastAsync("展开失败，请稍后再试"); return; }

                expandedPages[root.Id] = nextPage;
                if (!expandedRoots.TryGetValue(root.Id, out var flat)) expandedRoots[root.Id] = flat = [];
                flat.AddRange(page.Items);
                Render();
            }
            catch { await ShowToastAsync("网络异常，请稍后再试"); }
        }

        Label ActionLabel(string text, Color color, Func<Task> onTap)
        {
            var label = new Label { Text = text, FontSize = 12, TextColor = color, Padding = new Thickness(0, 4) };
            var tap = new TapGestureRecognizer();
            // 每个动作自己处理异常并给提示，所以这里不 await 结果也不会漏错误
            tap.Tapped += (_, _) => _ = onTap();
            label.GestureRecognizers.Add(tap);
            return label;
        }

        // 评论头像：同一张图在面板里会出现多次（同一作者的多条评论），而 V2.5 的头像是 data URI
        // （库里实测是几十 KB 的 base64）—— 每行都重新解一次纯属浪费。
        // 缓存放**面板作用域**里：面板一关就释放，不会像静态缓存那样随会话无限长大。
        var avatarSources = new Dictionary<string, ImageSource>();

        ImageSource? AvatarSource(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (avatarSources.TryGetValue(url, out var cached)) return cached;

            // 解析统一交给 ImageSourceFactory：data URI / 本地文件 / content:// / 后端相对路径只有那一份判断
            var source = ImageSourceFactory.From(url);
            if (source is not null) avatarSources[url] = source;
            return source;
        }

        /// <summary>没有头像时用昵称首字兜底；取法与 XAML 的 Initial 转换器一致（StringInfo 保证 emoji 昵称不被切半）。</summary>
        static string InitialOf(string name) =>
            string.IsNullOrWhiteSpace(name)
                ? "?"
                : System.Globalization.StringInfo.GetNextTextElement(name.Trim()).ToUpperInvariant();

        /// <summary>圆形头像：有图显示图，没图显示首字 —— 与关注列表 / 粉丝页同一套观感。</summary>
        View BuildAvatar(CommentDto comment, double size)
        {
            var box = new Grid
            {
                HeightRequest = size,
                WidthRequest = size,
                // 顶对齐：昵称底下还有正文与操作行，头像要贴着自己那条评论的昵称
                VerticalOptions = LayoutOptions.Start
            };

            // 先铺首字兜底：图没加载出来（或压根没设头像）时就是这个圆
            box.Add(AvatarCircle(size, Res("PrimarySoft", "#EDE9FE"), new Label
            {
                Text = InitialOf(comment.UserName),
                FontSize = size * 0.42,
                FontAttributes = FontAttributes.Bold,
                TextColor = Res("Accent", "#512BD4"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }));

            var source = AvatarSource(comment.UserAvatarUrl);
            if (source is not null)
                box.Add(AvatarCircle(size, Colors.Transparent, new Image { Source = source, Aspect = Aspect.AspectFill }));

            return box;
        }

        // 用 Frame 而不是 Border：全站头像（关注列表 / 粉丝页 / 用户页 / 关于页）都是
        // Frame + IsClippedToBounds 这套圆角裁切，各端已经跑熟；换 Border 得重新验一遍圆形裁切，没必要。
        static Frame AvatarCircle(double size, Color background, View content) => new()
        {
            CornerRadius = (float)(size / 2),
            Padding = new Thickness(0),
            IsClippedToBounds = true,
            HeightRequest = size,
            WidthRequest = size,
            BackgroundColor = background,
            Content = content
        };

        /// <summary>
        /// 一行评论 + 它下面的整棵子树（递归）。
        /// <paramref name="parentName"/> 是父评论作者：缩进到顶格之后要靠"回复 @某人"点明从属关系，
        /// 否则第 5 层看起来和第 4 层是并列的。
        /// </summary>
        View BuildCommentRow(CommentDto comment, string? parentName)
        {
            // 缩进最多到第 4 层（再深就往同一格上叠）；
            // 4 = 服务端 CommentService.MaxDepth（主评论算第 1 层，最深第 5 层）
            const int maxIndentDepth = 3;
            const int maxReplyDepth = 4;
            const double indentStep = 22;

            var stack = new VerticalStackLayout
            {
                Padding = new Thickness(Math.Min(comment.Depth, maxIndentDepth) * indentStep, 10, 0, 0),
                Spacing = 4
            };

            // 这一行的右侧内容（昵称/时间、正文、操作）都先攒在 body 里，头像占左列，最后组装
            var body = new VerticalStackLayout { Spacing = 4 };

            // 占位楼（作者已删）没有作者可显示，只剩一句"该评论已删除"
            if (!comment.IsDeleted)
            {
                var head = new HorizontalStackLayout { Spacing = 8 };
                head.Add(new Label
                {
                    Text = comment.UserName,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Res("Gray800", "#333333")
                });
                if (parentName is not null && comment.Depth > maxIndentDepth)
                    head.Add(new Label
                    {
                        Text = $"回复 @{parentName}",
                        FontSize = 11,
                        TextColor = Res("Gray500", "#6E6E6E"),
                        VerticalOptions = LayoutOptions.Center
                    });
                head.Add(new Label
                {
                    // 时间格式跟本地曲库/缓存历史保持一致（MM-dd HH:mm，服务端存 UTC，这里转本地）
                    Text = comment.CreatedAt.ToLocalTime().ToString("MM-dd HH:mm"),
                    FontSize = 11,
                    TextColor = Res("Gray400", "#9E9E9E"),
                    VerticalOptions = LayoutOptions.Center
                });
                if (comment.IsHidden)
                    head.Add(new Label
                    {
                        Text = "已隐藏",
                        FontSize = 11,
                        TextColor = Res("Danger", "#E5484D"),
                        VerticalOptions = LayoutOptions.Center
                    });
                body.Add(head);
            }

            // 正文纯文本直出：服务端只存文本、不渲染 HTML，客户端也不做二次解析
            body.Add(new Label
            {
                Text = comment.Content,
                FontSize = 14,
                TextColor = comment.IsDeleted ? Res("Gray400", "#9E9E9E") : Res("Gray900", "#212121"),
                LineBreakMode = LineBreakMode.WordWrap
            });

            // 占位楼不给任何操作：作者没了、正文也清空了，点赞 / 回复 / 删除都没有对象
            if (!comment.IsDeleted)
            {
                var actions = new HorizontalStackLayout { Spacing = 18, Margin = new Thickness(0, 2, 0, 0) };
                actions.Add(ActionLabel(
                    comment.LikeCount > 0 ? $"♥ {comment.LikeCount}" : "♥",
                    comment.IsLiked ? Res("Danger", "#E5484D") : Res("Gray500", "#6E6E6E"),
                    () => ToggleLikeAsync(comment)));
                // 到顶了就不再给"回复"：点下去也只能被服务端拒掉
                if (comment.Depth < maxReplyDepth)
                    actions.Add(ActionLabel("回复", Res("Gray500", "#6E6E6E"), () =>
                    {
                        SetReplyTarget(comment);
                        return Task.CompletedTask;
                    }));
                if (comment.IsMine) actions.Add(ActionLabel("删除", Res("Gray500", "#6E6E6E"), () => DeleteAsync(comment)));
                body.Add(actions);
            }

            if (comment.IsDeleted)
            {
                // 作者都藏起来了，就别再留出头像那一列
                stack.Add(body);
            }
            else
            {
                // 头像在左、昵称与正文在右；回复用小一号头像，给逐层缩进让出宽度
                var row = new Grid { ColumnSpacing = 10 };
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                row.Add(BuildAvatar(comment, comment.Depth == 0 ? 34 : 26));
                row.Add(body);
                Grid.SetColumn(body, 1);
                stack.Add(row);
            }

            // 子回复：整棵子树挂在自己这一行下面
            foreach (var child in ChildrenOf(comment))
                stack.Add(BuildCommentRow(child, comment.IsDeleted ? null : comment.UserName));

            // 子孙没取全（列表里每条主评论上限 200 条）→ 给"展开全部"，点一次追加一页
            var loaded = expandedRoots.TryGetValue(comment.Id, out var flat) ? flat.Count : 0;
            if (comment.HasMoreReplies && loaded < comment.ReplyCount)
                stack.Add(ActionLabel(
                    loaded == 0
                        ? $"展开全部回复（共 {comment.ReplyCount} 条）"
                        : $"继续展开（还剩 {comment.ReplyCount - loaded} 条）",
                    Res("Primary", "#512BD4"),
                    () => ExpandAsync(comment)));

            return stack;
        }

        async Task ToggleLikeAsync(CommentDto comment)
        {
            try
            {
                if (!await EnsureLoggedInAsync()) return;
                var state = await api.SetCommentLikeAsync(comment.Id, !comment.IsLiked);
                if (state is null) { await ShowToastAsync("操作失败，请稍后再试"); return; }
                await ReloadAsync();
            }
            catch { await ShowToastAsync("网络异常，请稍后再试"); }
        }

        async Task DeleteAsync(CommentDto comment)
        {
            try
            {
                if (!await ShowRoundedConfirmAsync("删除评论", "删除后无法恢复，确定删除吗？", "删除", destructive: true)) return;
                var (ok, error) = await api.DeleteCommentAsync(comment.Id);
                if (!ok) { await ShowToastAsync(error ?? "删除失败"); return; }
                await ReloadAsync();
            }
            catch { await ShowToastAsync("网络异常，请稍后再试"); }
        }

        async Task SendAsync()
        {
            var content = entry.Text?.Trim() ?? string.Empty;
            if (content.Length == 0) { await ShowToastAsync("评论内容不能为空"); return; }
            try
            {
                if (!await EnsureLoggedInAsync()) return;

                sendButton.IsEnabled = false;
                var parentId = replyTo?.Id;
                var (created, error) = await api.CreateCommentAsync(targetType, targetId, parentId, content);
                if (created is null)
                {
                    // 空内容 / 超长 / 10 秒限流都由服务端判定，原样透出它的文案（比客户端自己编更准）
                    await ShowMessageDialogAsync("发表失败", error ?? "请稍后再试");
                    return;
                }

                entry.Text = string.Empty;
                SetReplyTarget(null);
                await ReloadAsync();
                if (created.ParentId is null)
                {
                    // 主评论按时间倒序排在第一页：滚到顶部，用户立刻看到自己刚发的那条
                    await scroll.ScrollToAsync(0, 0, true);
                }
                else
                {
                    await ShowToastAsync("回复已发表");
                }
            }
            catch { await ShowToastAsync("网络异常，请稍后再试"); }
            finally { sendButton.IsEnabled = true; }
        }

        replyCancel.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => SetReplyTarget(null))
        });
        moreButton.Clicked += (_, _) => { loadedPages++; _ = ReloadAsync(); };
        sendButton.Clicked += (_, _) => _ = SendAsync();
        entry.Completed += (_, _) => _ = SendAsync();

        EventHandler disappearingHandler = (_, _) => { pageGone = true; tcs.TrySetResult(true); };
        page.Disappearing += disappearingHandler;

        // 由 SheetInAsync 挂到页面根 Grid（不换 page.Content，原因见 AttachOverlay 注释）
        var detach = await SheetInAsync(page, overlay, scrim, sheet);

        // 入场动画播完再拉数据：用户先看到面板，再看到"正在加载…"，不会对着空白等
        await ReloadAsync();

        await tcs.Task;

        page.Disappearing -= disappearingHandler;

        if (!pageGone) await SheetOutAsync(scrim, sheet);

        detach();
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

    /// <summary>
    /// 长文本阅读弹窗（开源协议全文等）：与提示弹窗同一套居中卡片，但正文可滚动。
    /// 必要性：Apache-2.0 全文十几 KB，像 <see cref="ShowMessageDialogAsync"/> 那样
    /// 直接塞一个 Label 会把卡片顶出屏幕，且后面内容没法看。
    /// </summary>
    public static async Task ShowLongTextDialogAsync(string title, string text, string note = "", string okText = "知道了")
    {
        var tcs = new TaskCompletionSource<bool>();
        var body = DialogBody();

        body.Add(DialogTitle(title));

        if (!string.IsNullOrWhiteSpace(note))
        {
            body.Add(new Label
            {
                Text = note,
                FontSize = 12,
                TextColor = Res("Gray400", "#919191"),
                Margin = new Thickness(0, 6, 0, 0),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        var content = DialogMessage(text);
        content.Margin = new Thickness(0, 10, 0, 0);

        // ⚠️ 只在内容确实超出时套 ScrollView，且高度必须用 HeightRequest **显式**给：
        // Android 上 ScrollView 会撑满给它的可用高度（同 ShowBottomSheetAsync 的注释），
        // 用 MaximumHeightRequest 的话短文本（MIT 只有十几行）也会被拉成满屏、下面留一片空白。
        // 判据用「字符数 ÷ 每行字符数 × 行高」这种与布局无关的估算；估偏小是安全的
        //（顶多多出一条滚动条），估偏大才会留白。
        const double approxCharsPerLine = 52;
        const double approxLineHeight = 19;
        var estimatedTextHeight = Math.Ceiling(text.Length / approxCharsPerLine) * approxLineHeight;
        var page = Shell.Current?.CurrentPage as ContentPage;
        var maxTextHeight = page is { Height: > 0 } ? page.Height * 0.6 : 420;

        body.Add(estimatedTextHeight > maxTextHeight
            ? new ScrollView { Content = content, HeightRequest = maxTextHeight }
            : (View)content);

        body.Add(DialogSingleButton(okText, () => tcs.TrySetResult(true), Res("Primary", "#512BD4")));

        await PresentDialogAsync(body, tcs);
    }

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

    /// <summary>缓存设置对话框的返回值（原样带回表单；校验在调用方做）。<see cref="Save"/>=false 表示用户取消。</summary>
    public sealed record CacheSettingsResult(bool Save, string LimitText, bool AutoCacheEnabled);

    /// <summary>
    /// 缓存设置（V2.9 从「服务器设置」页迁来）：上限（GB）+ 「播完自动缓存」开关。
    /// 与「新建歌单」同一套居中卡片；开关拨动只改本地变量，点「保存」才返回，取消则什么都不改。
    /// </summary>
    public static async Task<CacheSettingsResult?> ShowCacheSettingsAsync(
        string initialLimitText, bool initialAutoCacheEnabled)
    {
        var tcs = new TaskCompletionSource<CacheSettingsResult?>();
        var body = DialogBody();

        body.Add(DialogTitle("缓存设置"));
        body.Add(DialogMessage("缓存上限（GB）"));

        var entry = new Entry
        {
            Text = initialLimitText,
            Placeholder = "例如 5",
            Keyboard = Keyboard.Numeric,
            IsSpellCheckEnabled = false,
            FontSize = 16,
            HeightRequest = 46,
            Margin = new Thickness(0, 6, 0, 0),
            BackgroundColor = Res("SurfaceLight", "#F1F1F1"),
            TextColor = Res("Gray900", "#212121")
        };
        body.Add(entry);

        var errorLabel = new Label
        {
            Text = $"请输入数字，范围 {CacheStore.MinLimitGb:0.#} ~ {CacheStore.MaxLimitGb:0.#} GB。",
            FontSize = 12,
            TextColor = Res("Danger", "#E5484D"),
            IsVisible = false,
            Margin = new Thickness(0, 6, 0, 0)
        };
        body.Add(errorLabel);

        var autoCache = initialAutoCacheEnabled;
        var switchRow = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            Margin = new Thickness(0, 16, 0, 0)
        };
        switchRow.Add(new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "播完自动缓存", FontSize = 15, TextColor = Res("Gray900", "#212121") },
                new Label
                {
                    Text = "完整听完的在线音乐会自动缓存到本机（仅 WiFi；达上限时跳过）",
                    FontSize = 12,
                    TextColor = Res("Gray500", "#6E6E6E")
                },
            }
        });
        var toggle = new Switch { VerticalOptions = LayoutOptions.Center, IsToggled = initialAutoCacheEnabled };
        toggle.Toggled += (_, _) => autoCache = toggle.IsToggled;
        Grid.SetColumn(toggle, 1);
        switchRow.Add(toggle);
        body.Add(switchRow);

        body.Add(DialogButtons(
            "取消", () => tcs.TrySetResult(null),
            "保存", () =>
            {
                var text = (entry.Text ?? string.Empty).Trim();
                // 就地校验：非法输入不让弹窗消失，用户改完再保存
                if (!double.TryParse(text, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var gb)
                    && !double.TryParse(text, out gb))
                {
                    errorLabel.IsVisible = true;
                    return;
                }
                tcs.TrySetResult(new CacheSettingsResult(true, text, autoCache));
            },
            Res("Primary", "#512BD4")));

        return await PresentDialogAsync(body, tcs);
    }

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
    /// 内置头像选择器（V2.9.3）：铺开一排圆头像，点一个就返回它的资源名；取消 / 点遮罩返回 null。
    /// <para>
    /// 用居中卡片而不是底部抽屉：这是"从一堆小图里挑一张"，铺开的方格比竖排列表好扫；
    /// 沿用「新建歌单」那套 DialogBody / DialogButton 积木，与全站弹层同一视觉。
    /// </para>
    /// <para>
    /// 资源没到位时（<see cref="BuiltInAvatars.All"/> 里登记了但文件不在），对应格子就是底色圆圈 ——
    /// 出图规格见 <see cref="BuiltInAvatars"/> 的类型注释。
    /// </para>
    /// </summary>
    public static async Task<string?> ShowAvatarPickerAsync(IReadOnlyList<string> presets)
    {
        // 每行 4 个、每个 64dp：当前 8 张正好两行；用 FlexLayout 自动换行，
        // 以后加减内置头像不用改布局
        const double avatarSize = 64;

        var tcs = new TaskCompletionSource<string?>();
        var body = DialogBody();

        body.Add(DialogTitle("选择头像"));

        var grid = new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            Direction = FlexDirection.Row,
            JustifyContent = FlexJustify.Center,
            Margin = new Thickness(0, 14, 0, 0)
        };

        foreach (var preset in presets)
        {
            var circle = new Frame
            {
                CornerRadius = (float)(avatarSize / 2),
                Padding = new Thickness(0),
                IsClippedToBounds = true,
                HeightRequest = avatarSize,
                WidthRequest = avatarSize,
                Margin = new Thickness(5),
                BackgroundColor = Res("PrimarySoft", "#EDE9FE"),
                Content = new Image { Source = BuiltInAvatars.SourceOf(preset), Aspect = Aspect.AspectFill }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => tcs.TrySetResult(preset);
            circle.GestureRecognizers.Add(tap);

            grid.Add(circle);
        }

        body.Add(grid);

        var cancel = DialogButton("取消", Res("SurfaceLight", "#F1F1F1"), Res("Gray600", "#404040"));
        cancel.Margin = new Thickness(0, 18, 0, 0);
        cancel.Clicked += (_, _) => tcs.TrySetResult(null);
        body.Add(cancel);

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
