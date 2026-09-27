using YinYanMusic.Core.Dtos;

namespace YinYanMusic.App.Services;

/// <summary>
/// 歌曲的两位"按人"动作：**点歌手名去谁的主页**、**关注哪几位**。
/// <para>
/// 播放页（歌手行 / 关注按钮）与歌曲「更多」菜单共用这一处实现 ——
/// 同一件事在两处长得不一样时，用户会以为功能坏了；之前菜单里那两项就还是只认主歌手。
/// </para>
/// <para>
/// 底层弹层是 <see cref="SongMenuHelper.ShowBottomSheetAsync"/>（App 里统一的底部选择列表）。
/// </para>
/// </summary>
internal static class SongArtistSheets
{
    /// <summary>不同动作失败时的统一文案（与菜单里原有的口径一致）。</summary>
    private const string FailMessage = "操作失败，请稍后重试。";

    private const string PartialFailMessage = "部分歌手操作失败，请稍后重试。";

    /// <summary>
    /// 这首歌里**能操作**的歌手（有 ID 的那几位）。
    /// <para>
    /// 本地歌与离线缓存的索引里没有歌手 ID → 返回空：跳主页、关注都要 ID，拿 0 去请求只会 404，
    /// 不如干脆不给这个动作（调用方据此隐藏菜单项 / 关注按钮）。
    /// </para>
    /// </summary>
    public static IReadOnlyList<SongArtistRef> Followable(SongDto? song) => song switch
    {
        null => [],
        { Artists: { Count: > 0 } list } => [.. list.Where(a => a.Id > 0)],
        // 列表接口没带歌手数组时的兜底：至少还有主歌手
        { ArtistId: > 0 } => [new SongArtistRef(song.ArtistId, song.ArtistName)],
        _ => []
    };

    /// <summary>
    /// 点歌手名要进谁的主页：单歌手直接返回他的 ID（调用方直接跳，不多一步），
    /// 联合创作弹「选择歌手」让用户挑 —— 固定跳主歌手等于把其他合作者藏起来。
    /// </summary>
    /// <returns>选中的歌手 ID；用户关掉弹层时 null。</returns>
    public static async Task<long?> PickArtistAsync(IReadOnlyList<SongArtistRef> credits)
    {
        if (credits.Count == 0) return null;
        if (credits.Count == 1) return credits[0].Id;

        var (labels, byLabel) = LabelRows(credits, _ => true);
        var picked = await SongMenuHelper.ShowBottomSheetAsync("选择歌手", labels);
        return picked is not null && byLabel.TryGetValue(picked, out var id) ? id : null;
    }

    /// <summary>
    /// 关注 / 取消关注：单歌手直接切换（不弹层，菜单与播放页都是这个手感），
    /// 联合创作弹「关注歌手」列表 —— 首行全部关注 / 全部取消，其余每位一行并带当前状态。
    /// </summary>
    /// <returns>
    /// null = 用户直接关掉了弹层（什么都没发生）；否则给出反馈文案与"这几位里谁已关注"的最终状态
    /// （调用方用它更新按钮 / 菜单状态，不必再查一次接口）。
    /// </returns>
    public static async Task<ArtistFollowOutcome?> FollowAsync(IMusicApi api, IReadOnlyList<SongArtistRef> credits)
    {
        if (credits.Count == 0) return null;

        // 先问一次关注列表：既决定文案（关注 / 取消关注），也决定每位那行的 ✓ / +
        var followedIds = await ReadFollowedIdsAsync(api);

        return credits.Count == 1
            ? await ToggleSingleAsync(api, credits[0], followedIds)
            : await ShowFollowSheetAsync(api, credits, followedIds);
    }

    private static async Task<ArtistFollowOutcome> ToggleSingleAsync(IMusicApi api, SongArtistRef artist, HashSet<long> followedIds)
    {
        var follow = !followedIds.Contains(artist.Id);
        var ok = await TrySetFollowAsync(api, artist.Id, follow);
        if (ok)
        {
            if (follow) followedIds.Add(artist.Id);
            else followedIds.Remove(artist.Id);
        }

        var message = ok
            ? (follow ? $"已关注「{artist.Name}」" : $"已取消关注「{artist.Name}」")
            : FailMessage;
        return new ArtistFollowOutcome(message, new HashSet<long>(followedIds.Where(id => id == artist.Id)));
    }

    private static async Task<ArtistFollowOutcome?> ShowFollowSheetAsync(
        IMusicApi api, IReadOnlyList<SongArtistRef> credits, HashSet<long> followedIds)
    {
        var allFollowed = credits.All(a => followedIds.Contains(a.Id));

        var (options, byLabel) = LabelRows(
            credits,
            a => followedIds.Contains(a.Id),
            header: allFollowed ? $"全部取消关注（{credits.Count} 位）" : $"全部关注（{credits.Count} 位）");

        var choice = await SongMenuHelper.ShowBottomSheetAsync("关注歌手", options);
        if (choice is null) return null;

        // 要执行的动作：整批（全部关注 / 全部取消）或某一位
        var actions = new List<(SongArtistRef Artist, bool Follow)>();
        if (choice == options[0])
        {
            // 只补还没关注的几位：已关注的不再发一次白跑的请求。
            // 必须先 ToList 落地 —— Where 是延迟求值，而循环里在改 followedIds，一边筛一边改会漏元素。
            actions.AddRange(allFollowed
                ? credits.Select(a => (a, false))
                : credits.Where(a => !followedIds.Contains(a.Id)).ToList().Select(a => (a, true)));
        }
        else if (byLabel.TryGetValue(choice, out var artistId))
        {
            var artist = credits.First(a => a.Id == artistId);
            actions.Add((artist, !followedIds.Contains(artist.Id)));
        }

        if (actions.Count == 0) return null;

        var failed = false;
        foreach (var (artist, follow) in actions)
        {
            if (await TrySetFollowAsync(api, artist.Id, follow))
            {
                if (follow) followedIds.Add(artist.Id);
                else followedIds.Remove(artist.Id);
            }
            else
            {
                failed = true;
            }
        }

        return new ArtistFollowOutcome(
            BuildMessage(actions, failed),
            new HashSet<long>(followedIds.Where(id => credits.Any(c => c.Id == id))));
    }

    private static string BuildMessage(List<(SongArtistRef Artist, bool Follow)> actions, bool failed)
    {
        if (failed) return PartialFailMessage;

        var followed = actions.Where(x => x.Follow).Select(x => x.Artist.Name).ToList();
        var unfollowed = actions.Where(x => !x.Follow).Select(x => x.Artist.Name).ToList();
        return (followed.Count, unfollowed.Count) switch
        {
            ( > 0, 0) => $"已关注{Quote(followed)}",
            (0, > 0) => $"已取消关注{Quote(unfollowed)}",
            _ => $"已更新关注：{Quote([.. actions.Select(x => x.Artist.Name)])}"
        };
    }

    private static string Quote(IEnumerable<string> names) => string.Concat(names.Select(n => $"「{n}」"));

    /// <summary>
    /// 生成弹层的行文案，并给出"行文案 → 歌手 ID"的映射。
    /// <para>
    /// 必须用映射回查 ID，不能反过来解析名字：歌手名里什么字符都可能有，
    /// 而且「✓ 名字（点击取消关注）」这种行文案本身就不是名字。同名歌手给后一行加个空格避免撞行（极罕见）。
    /// </para>
    /// </summary>
    private static (List<string> Options, Dictionary<string, long> ByLabel) LabelRows(
        IReadOnlyList<SongArtistRef> credits, Func<SongArtistRef, bool> isFollowed, string? header = null)
    {
        var options = new List<string>();
        if (header is not null) options.Add(header);

        var byLabel = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var artist in credits)
        {
            var label = header is null
                ? artist.Name                                                   // 「选择歌手」：只有名字
                : isFollowed(artist) ? $"✓ {artist.Name}（点击取消关注）" : $"+ {artist.Name}";
            while (!byLabel.TryAdd(label, artist.Id)) label += " ";
            options.Add(label);
        }

        return (options, byLabel);
    }

    private static async Task<HashSet<long>> ReadFollowedIdsAsync(IMusicApi api)
    {
        try
        {
            return [.. await api.GetFollowedArtistIdsAsync()];
        }
        catch
        {
            // 拿不到关注列表：按"都没关注"处理。用户点下去仍可能成功（只是文案可能说反），
            // 比直接报错让功能不可用要好。
            return [];
        }
    }

    private static async Task<bool> TrySetFollowAsync(IMusicApi api, long artistId, bool follow)
    {
        try
        {
            return follow ? await api.FollowArtistAsync(artistId) : await api.UnfollowArtistAsync(artistId);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// 一次关注动作的结果。<paramref name="Message"/> 给"用提示反馈"的入口（歌曲菜单）读，
/// 播放页不看它 —— 那里图标（✓ / +）本身就是反馈。
/// </summary>
internal sealed record ArtistFollowOutcome(string Message, IReadOnlySet<long> FollowedIds);
