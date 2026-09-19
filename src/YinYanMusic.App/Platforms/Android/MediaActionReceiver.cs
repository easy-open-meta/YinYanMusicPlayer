#if ANDROID
using Android.App;
using Android.Content;

namespace YinYanMusic.App;

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter(new[] { "com.yinyan.music.MEDIA_ACTION" })]
public class MediaActionReceiver : BroadcastReceiver
{
    public const string ActionExtra = "ACTION_TYPE";
    public const string ActionPrev = "PREV";
    public const string ActionPlay = "PLAY";
    public const string ActionPause = "PAUSE";
    public const string ActionNext = "NEXT";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != "com.yinyan.music.MEDIA_ACTION") return;
        var action = intent.GetStringExtra(ActionExtra);
        var mgr = MediaNotificationManager.Instance;
        switch (action)
        {
            case ActionPrev: mgr.PreviousAction?.Invoke(); break;
            case ActionPlay: mgr.PlayAction?.Invoke(); break;
            case ActionPause: mgr.PauseAction?.Invoke(); break;
            case ActionNext: mgr.NextAction?.Invoke(); break;
        }
    }
}
#endif