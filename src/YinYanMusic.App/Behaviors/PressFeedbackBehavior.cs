using Microsoft.Maui.Controls;

namespace YinYanMusic.App.Behaviors;

/// <summary>
/// 点击动效：按下时缩小 + 变淡，松开后回弹（弹性缓动）。
/// 挂到行容器（Grid / Frame 等 VisualElement）上即可，与 TapGestureRecognizer 共存；
/// 走指针事件（PointerGestureRecognizer），Windows 鼠标/触摸与 Android 触摸都生效。
/// </summary>
public class PressFeedbackBehavior : Behavior<View>
{
    private View? _attached;
    private bool _pressed;

    /// <summary>按下时的缩放比例。</summary>
    public double PressedScale { get; set; } = 0.96;

    /// <summary>按下时的不透明度。</summary>
    public double PressedOpacity { get; set; } = 0.55;

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
        _attached = null;
        base.OnDetachingFrom(bindable);
    }

    private void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (_attached is null || _pressed) return;
        _pressed = true;
        _ = _attached.ScaleTo(PressedScale, 60, Easing.CubicOut);
        _ = _attached.FadeTo(PressedOpacity, 60, Easing.CubicOut);
    }

    private void OnPointerReleased(object? sender, PointerEventArgs e) => Release();

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        // 按住后移出区域：取消按压态，且不触发点击（由 TapGestureRecognizer 自行判断）
        Release();
    }

    private void Release()
    {
        if (_attached is null || !_pressed) return;
        _pressed = false;
        _ = _attached.ScaleTo(1, 150, Easing.SpringOut);
        _ = _attached.FadeTo(1, 150);
    }
}
