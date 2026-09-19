#if ANDROID
using AndroidX.Media3.Common;

namespace YinYanMusic.App;

/// <summary>
/// 包一层 ExoPlayer，把“下一曲/上一曲”重定向到 PlayerService 的队列逻辑，
/// 让 CommunityToolkit.MediaElement 内建的 Media3 会话（Android 13+ 系统媒体控件实际绑定的会话）
/// 也能驱动应用自己的播放队列。
/// 注意：Media3 会话会按 AvailableCommands / IsCommandAvailable 决定系统面板上按钮是否可用，
/// 单曲队列时 ExoPlayer 不上报切歌命令，必须在这里补报。
/// </summary>
public class CustomForwardingPlayer : ForwardingPlayer
{
    private readonly Action? _onNext;
    private readonly Action? _onPrevious;

    public CustomForwardingPlayer(IPlayer player, Action? onNext, Action? onPrevious) : base(player)
    {
        _onNext = onNext;
        _onPrevious = onPrevious;
    }

    // media3 1.8 的命令常量（ForwardingPlayer.InterfaceConsts）：
    // CommandSeekToPreviousMediaItem = 6, CommandSeekToPrevious = 7,
    // CommandSeekToNextMediaItem = 8, CommandSeekToNext = 9。
    // 曾被误写成 15/16/17/18（实际是设置重复模式/查询类命令），导致系统面板按钮不可用。
    private const int CommandSeekToPreviousMediaItem = ForwardingPlayer.InterfaceConsts.CommandSeekToPreviousMediaItem;
    private const int CommandSeekToPrevious = ForwardingPlayer.InterfaceConsts.CommandSeekToPrevious;
    private const int CommandSeekToNextMediaItem = ForwardingPlayer.InterfaceConsts.CommandSeekToNextMediaItem;
    private const int CommandSeekToNext = ForwardingPlayer.InterfaceConsts.CommandSeekToNext;

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

    public override void SeekToNextMediaItem()
    {
        Android.Util.Log.Info("YinYan", "ForwardingPlayer.SeekToNextMediaItem intercepted");
        _onNext?.Invoke();
    }

    public override void SeekToPreviousMediaItem()
    {
        Android.Util.Log.Info("YinYan", "ForwardingPlayer.SeekToPreviousMediaItem intercepted");
        _onPrevious?.Invoke();
    }

    public override bool IsCommandAvailable(int command)
    {
        var overrideCmds = command == CommandSeekToNext || command == CommandSeekToPrevious
            || command == CommandSeekToNextMediaItem || command == CommandSeekToPreviousMediaItem;
        return overrideCmds || base.IsCommandAvailable(command);
    }

    public override PlayerCommands AvailableCommands
    {
        get
        {
            var builder = base.AvailableCommands.BuildUpon()
                .Add(CommandSeekToNext)
                .Add(CommandSeekToPrevious)
                .Add(CommandSeekToNextMediaItem)
                .Add(CommandSeekToPreviousMediaItem);
            return builder.Build();
        }
    }
}
#endif
