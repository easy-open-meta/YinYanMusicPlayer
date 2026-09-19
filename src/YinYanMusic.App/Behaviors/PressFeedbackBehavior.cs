using Microsoft.Maui.Controls;

namespace YinYanMusic.App.Behaviors;

/// <summary>
/// 点击动效：按下时缩小 + 变淡，松开后回弹。
/// 挂到「眼睛看到的那层表面」（带 BackgroundColor / StrokeShape / Shadow 的那个元素）上；
/// 挂在内层内容布局上时，只有内层文字在缩、表面纹丝不动，看起来就是「没有动效」。
///
/// 走指针事件（PointerGestureRecognizer），Windows 鼠标/触摸与 Android 触摸都生效。
/// ⚠️ 指针事件在 Android 上会**拦截触摸**，所以 TapGestureRecognizer 必须和它挂在同一个元素上。
///
/// 三处刻意的取舍，都是为了让反馈「不会偶尔看不见」：
/// 1) 按下态**立即生效**、不做渐入。原来是 ScaleTo(…, 60)：快速轻点可能一帧都没渲染出来就抬手了。
///    回弹仍然是动画，观感不受影响。
/// 2) 按下态有**最短可见时长**（<see cref="MinVisibleMs"/>）：抬手早于它时先补足再回弹，
///    于是「轻点闪一下」不会退化成「什么都没发生」。
/// 3) 回弹用 CubicOut 而不是 SpringOut：SpringOut 会过冲抖动，150ms 只有约 9 帧，
///    采样不足时看着像卡顿；CubicOut 单调收敛，同样好看且更省。
/// </summary>
public class PressFeedbackBehavior : Behavior<View>
{
    private View? _attached;
    private bool _pressed;
    private long _pressedAtTicks;
    private int _generation;

    /// <summary>按下时的缩放比例。</summary>
    public double PressedScale { get; set; } = 0.96;

    /// <summary>按下时的不透明度。</summary>
    public double PressedOpacity { get; set; } = 0.55;

    /// <summary>按下态至少可见的毫秒数；抬手更早也要补足这么多。</summary>
    public int MinVisibleMs { get; set; } = 90;

    /// <summary>回弹毫秒数。</summary>
    public int ReleaseMs { get; set; } = 150;

    protected override void OnAttachedTo(View bindable)
    {
        _attached = bindable;

        var pressed = new PointerGestureRecognizer();
        pressed.PointerPressed += OnPointerPressed;
        pressed.PointerReleased += OnPointerReleased;
        pressed.PointerExited += OnPointerExited;
        bindable.GestureRecognizers.Add(pressed);

        base.OnAttachedTo(bindable);
    }

    protected override void OnDetachingFrom(View bindable)
    {
        _generation++;      // 作废挂起的回弹，别打到已被回收 / 已换父级的视图上
        _pressed = false;
        _attached = null;
        base.OnDetachingFrom(bindable);
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (_attached is null) return;

        // 注意：这里刻意不因「上一次的 _pressed 还是 true」而直接 return ——
        // 万一某次抬手事件丢了，旧写法的守卫会让这个元素**从此再也不响应**。
        // 现在按「每次按下都是新的一次」处理，状态自愈。
        _generation++;
        _pressed = true;
        _pressedAtTicks = Environment.TickCount64;

        // 立刻进入按下态（不渐入）：保证极短的点击也有一帧可见反馈
        _attached.Scale = PressedScale;
        _attached.Opacity = PressedOpacity;
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e) => Release();

    // 按住后移出区域：取消按压态，且不触发点击（由 TapGestureRecognizer 自行判断）
    private void OnPointerExited(object? sender, PointerEventArgs e) => Release();

    private async void Release()
    {
        if (_attached is null || !_pressed) return;
        _pressed = false;

        var generation = ++_generation;
        var visible = (int)(Environment.TickCount64 - _pressedAtTicks);
        var remain = MinVisibleMs - visible;
        if (remain > 0)
        {
            await Task.Delay(remain);
            // 等待期间又按下了 / 视图被摘除 → 交给新的那一次处理
            if (generation != _generation || _attached is null || _pressed) return;
        }

        var target = _attached;
        if (target is null) return;
        // 注意：ScaleTo / FadeTo 的时长参数是 uint，传 int 变量编译不过（字面量 150 可以隐式转换）
        _ = target.ScaleTo(1, (uint)ReleaseMs, Easing.CubicOut);
        _ = target.FadeTo(1, (uint)ReleaseMs);
    }
}
