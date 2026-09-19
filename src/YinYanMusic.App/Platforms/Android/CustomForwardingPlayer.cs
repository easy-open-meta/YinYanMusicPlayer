#if ANDROID
using AndroidX.Media3.Common;

namespace YinYanMusic.App;

public class CustomForwardingPlayer : ForwardingPlayer
{
    private readonly Action? _onNext;
    private readonly Action? _onPrevious;

    public CustomForwardingPlayer(IPlayer player, Action? onNext, Action? onPrevious) : base(player)
    {
        _onNext = onNext;
        _onPrevious = onPrevious;
    }

    public override void SeekToNext()
    {
        Android.Util.Log.Info("YinYan", "ForwardingPlayer.SeekToNext intercepted");
        _onNext?.Invoke();
    }

    public override void SeekToPrevious()
    {
        Android.Util.Log.Info("YinYan", "ForwardingPlayer.SeekToPrevious intercepted");
        _onPrevious?.Invoke();
    }
}
#endif