using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace YinYanMusic.App.Services;

/// <summary>
/// 全局离线红点（V2.12）：设备离线时在**当前页面**右上角最边缘挂一颗 3px 红点，
/// 点击展开「当前设备处于离线模式。」提示；联网后红点自动消失。
///
/// <para><b>怎么做到"所有页面都有"</b>：不逐页改 XAML（十几个页面会漏会漂），
/// 而是 Shell 级监听 <see cref="Connectivity.ConnectivityChanged"/> + 页面切换事件，
/// 每次都把红点挂到 Shell 当前页的根 Grid 上（与 SongMenuHelper.AttachOverlay 同一套
/// "不动 page.Content" 的手法，见它的 Windows COMException 注释）。</para>
///
/// <para><b>点击行为</b>：红点本身可点（命中区放大到 22×22，3px 的点手指点不中），
/// 点开一个居中小卡片说明离线状态，说明卡期间再点任意处关闭。</para>
/// </summary>
public sealed class OfflineIndicatorService
{
    /// <summary>红点尺寸（用户指定的 3px）。</summary>
    public const double DotSize = 3;

    /// <summary>红点的可点击命中区边长：3px 的点无法用手指命中，热区放大到 22。</summary>
    public const double HitAreaSize = 22;

    private readonly IServiceProvider _services;
    private ContentPage? _currentPage;
    private Grid? _dotHost;
    private bool _offline;
    private bool _hooked;

    public OfflineIndicatorService(IServiceProvider services) => _services = services;

    /// <summary>App 启动时调用一次：挂网络监听 + 页面切换跟随。</summary>
    public void Start()
    {
        if (_hooked) return;
        _hooked = true;

        Connectivity.ConnectivityChanged += OnConnectivityChanged;
        _offline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;

        // Shell 页面切换（含 Tab 内部导航与路由跳转）都从这里经过
        if (Shell.Current is not null)
        {
            Shell.Current.Navigated += OnShellNavigated;
            AttachIfOffline(Shell.Current.CurrentPage as ContentPage);
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var nowOffline = e.NetworkAccess != NetworkAccess.Internet;
        if (nowOffline == _offline) return;
        _offline = nowOffline;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_offline) AttachIfOffline(_currentPage);
            else Detach();
        });
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var page = Shell.Current?.CurrentPage as ContentPage;
            if (ReferenceEquals(page, _currentPage)) return;

            // 离开旧页：红点随页面销毁一起没了，清掉引用即可
            _dotHost = null;
            _currentPage = page;
            AttachIfOffline(page);
        });
    }

    private void AttachIfOffline(ContentPage? page)
    {
        if (!_offline || page is null) return;

        // 页面还在导航动画里时 Content 可能未就绪，推迟到 Loaded
        if (page.Content is null)
        {
            page.Loaded += OnPageLoaded;
            void OnPageLoaded(object? s, EventArgs e)
            {
                page.Loaded -= OnPageLoaded;
                if (_offline && ReferenceEquals(Shell.Current?.CurrentPage, page))
                    AttachIfOffline(page);
            }
            return;
        }

        if (page.Content is not Grid rootGrid)
        {
            AppLog.Warn($"[离线红点] 页面 {page.GetType().Name} 根不是 Grid，跳过");
            return;
        }

        var host = BuildDotHost(page);
        Grid.SetRow(host, 0);
        Grid.SetColumn(host, 0);
        Grid.SetRowSpan(host, Math.Max(1, rootGrid.RowDefinitions.Count));
        Grid.SetColumnSpan(host, Math.Max(1, rootGrid.ColumnDefinitions.Count));
        host.ZIndex = 900;          // 高于汉堡按钮（50），低于弹层（1000）
        host.InputTransparent = false;
        rootGrid.Add(host);

        _dotHost = host;
        _currentPage = page;
    }

    /// <summary>右上角红点：一个透明热区包着 3px 红点，点击展开离线说明。</summary>
    private Grid BuildDotHost(ContentPage page)
    {
        var dot = new Ellipse
        {
            WidthRequest = DotSize,
            HeightRequest = DotSize,
            Fill = Color.FromArgb("#E5484D"),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
        };

        var hit = new Grid
        {
            WidthRequest = HitAreaSize,
            HeightRequest = HitAreaSize,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            InputTransparent = false,
            Children = { dot },
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            // 点击展开说明：复用 SongMenuHelper 的消息卡（居中圆角，点击任意处关闭）
            await SongMenuHelper.ShowMessageDialogAsync("离线模式", "当前设备处于离线模式。", "知道了");
        };
        hit.GestureRecognizers.Add(tap);

        // 外层铺满整页但只在右上角露出热区，其余区域全部穿透
        var host = new Grid { InputTransparent = true, CascadeInputTransparent = false };
        host.Add(hit);
        return host;
    }

    private void Detach()
    {
        if (_dotHost?.Parent is Grid root)
        {
            root.Remove(_dotHost);
        }
        _dotHost = null;
    }
}
