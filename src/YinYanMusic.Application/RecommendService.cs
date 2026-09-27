using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface IRecommendService
{
    /// <summary>
    /// 推荐歌单（V2.12）。<paramref name="scene"/> 取 <see cref="RecommendScenes"/> 之一，
    /// <paramref name="userId"/> 为 null（未登录）时 for-you 退化为热播。
    /// </summary>
    Task<PagedResult<PlaylistDto>> GetPlaylistsAsync(string? scene, int? categoryId, long? userId, int page, int pageSize);

    /// <summary>后台推荐位管理列表：全部歌单 + 各自的人工干预状态。</summary>
    Task<PagedResult<AdminRecommendedDto>> AdminListAsync(string? keyword, int page, int pageSize);

    /// <summary>后台设置某歌单的推荐位（整行覆盖；没有记录就新建）。</summary>
    Task<ServiceResult> AdminSetAsync(long playlistId, UpdateRecommendedRequest req);

    /// <summary>后台移除某歌单的人工干预，回到纯规则排序。</summary>
    Task<ServiceResult> AdminRemoveAsync(long playlistId);
}

/// <summary>
/// 推荐歌单专区（V2.12）。
///
/// <para>**v1 走规则不走模型**：数据量不足以支撑协同过滤，而且规则可解释、可运营干预 ——
/// 榜单排错了，后台改一行 SortOrder 就能修，模型排错了只能等下次训练。</para>
///
/// <para>排序总公式（五个场景共用，只有 <c>Score</c> 的来源不同）：
/// <c>人工置顶序号 → 规则分 + 人工加权分 → 创建时间 → Id</c>。
/// 「人工置顶」之所以能压过「规则分」，是因为它是独立的第一个排序键 ——
/// 运营置顶就是"不管它播放量多少，先放第一个"，靠加分是做不到"不管多少"的。</para>
/// </summary>
public class RecommendService(MusicDbContext db, AudioMetadataService metadata) : IRecommendService
{
    public const string PlaylistNotFound = "歌单不存在。";
    public const string NoOverride = "该歌单没有人工干预记录。";

    /// <summary>猜你喜欢：歌单里命中"用户感兴趣的分类"的歌曲，每首给 3 分。</summary>
    private const int CategoryMatchScore = 3;

    /// <summary>猜你喜欢：命中"用户感兴趣的歌手"的歌曲，每首给 2 分（分类比歌手更能代表口味广度）。</summary>
    private const int ArtistMatchScore = 2;

    public async Task<PagedResult<PlaylistDto>> GetPlaylistsAsync(string? scene, int? categoryId, long? userId, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        var requested = Normalize(scene);

        // 分区推荐缺 categoryId：返回**空列表**，不静默换场景。
        // 空态用户会去查参数，错的数据用户不会查 —— 悄悄给一份热播榜单是最难排查的一类 bug。
        if (requested == RecommendScenes.Zone && categoryId is null)
            return new PagedResult<PlaylistDto>([], 0, page, pageSize);

        var effective = requested;
        List<int> likedCategories = [];
        List<long> likedArtists = [];
        if (requested == RecommendScenes.ForYou)
        {
            if (userId is not long uid)
            {
                // 未登录退化（TC-2.12-05）：接口匿名可访问，登录态由 token 决定，没有 token 就没有口味可算
                effective = RecommendScenes.Hot;
            }
            else
            {
                (likedCategories, likedArtists) = await LoadTasteAsync(uid);
                // 冷启动退化：登录了但一首歌没喜欢、一个歌单没收藏 —— 个性化无从谈起，
                // 给通用热播榜比给一份全 0 分（= 按创建时间排）的"个性化"榜单更合理
                if (likedCategories.Count == 0 && likedArtists.Count == 0) effective = RecommendScenes.Hot;
            }
        }

        // 候选集 + 人工干预左连接。左连接而不是内连接：绝大多数歌单没有干预行，
        // 内连接会把它们全部滤掉。
        // IsHidden 的过滤放在这里（而不是排序里）：下线是"不进推荐候选集"，不是"排最后一名"。
        var candidates =
            from p in db.Playlists.AsNoTracking()
            join r in db.AdminRecommended.AsNoTracking() on p.Id equals r.PlaylistId into rs
            from r in rs.DefaultIfEmpty()
            where !p.IsSystem && (r == null || !r.IsHidden)
            select new { Playlist = p, Pin = r };

        if (requested == RecommendScenes.Zone)
            candidates = candidates.Where(x => x.Playlist.CategoryId == categoryId);

        // 场景分：只算当前场景需要的那一个。
        // 不做成"四个分值列一起投影"，是因为那会给每个列表白跑三条相关子查询 ——
        // 歌单内的歌曲聚合是逐行子查询，代价不低。
        // 四个分支的匿名类型属性名/类型/顺序一致，编译期是同一个类型，所以 switch 能统一。
        var scored = effective switch
        {
            RecommendScenes.Collected => candidates.Select(x => new
            {
                x.Playlist,
                x.Pin,
                Score = (long)x.Playlist.CollectedBy.Count,
            }),

            // 最新上架没有"分数"可言，名次完全由后面的 CreatedAt 兜底键决定
            RecommendScenes.New => candidates.Select(x => new
            {
                x.Playlist,
                x.Pin,
                Score = 0L,
            }),

            RecommendScenes.ForYou => candidates.Select(x => new
            {
                x.Playlist,
                x.Pin,
                Score = (long)(
                    // 歌单里有多少首命中"用户感兴趣的分类"
                    x.Playlist.Songs.Count(ps => likedCategories.Contains(ps.Song.CategoryId ?? 0)) * CategoryMatchScore
                    // ……有多少首命中"用户感兴趣的歌手"
                    + x.Playlist.Songs.Count(ps => likedArtists.Contains(ps.Song.ArtistId)) * ArtistMatchScore
                    // 歌单自己的分区也命中：整张歌单都在用户的口味区里，额外算一次
                    + (x.Playlist.CategoryId != null && likedCategories.Contains(x.Playlist.CategoryId ?? 0) ? CategoryMatchScore : 0)),
            }),

            // hot 与 zone：歌单播放次数 = 歌单内全部歌曲 PlayCount 之和。
            // 与 PlaylistService.SearchAsync 同一口径（子查询实时算，不做冗余列），
            // 保证"推荐榜里的播放量"和"歌单列表里的播放量"永远是同一个数。
            _ => candidates.Select(x => new
            {
                x.Playlist,
                x.Pin,
                Score = x.Playlist.Songs.Sum(ps => (long?)ps.Song.PlayCount) ?? 0,
            }),
        };

        var ordered = scored
            // ① 人工置顶：SortOrder > 0 的排在所有未置顶之前，序号小的更前（TC-2.12-07）
            .OrderBy(x => x.Pin != null && x.Pin.SortOrder > 0 ? 0 : 1)
            // 这里的判空/判 0 必须与 ① 完全同义：只看 Pin != null 是不够的 ——
            // "有干预行但 SortOrder = 0"（比如只设了加权或只设了下线）会拿到排序键 0，
            // 比未置顶歌单的 int.MaxValue 还小，于是被**误当成置顶**排到第一名。
            .ThenBy(x => x.Pin == null || x.Pin.SortOrder <= 0 ? int.MaxValue : x.Pin.SortOrder)
            // ② 规则分 + 人工加权分（可为负，用于压低某个高播放量但内容不好的歌单）
            .ThenByDescending(x => x.Score + (x.Pin == null ? 0 : x.Pin.Weight))
            // ③ 同分兜底：新歌单靠前；再用 Id 兜底保证**分页稳定** ——
            //    没有最后一层时 PostgreSQL 对同分行的顺序不作保证，翻页会出现
            //    "某个歌单在第 1 页和第 2 页各出现一次"或干脆漏掉。
            .ThenByDescending(x => x.Playlist.CreatedAt)
            .ThenByDescending(x => x.Playlist.Id);

        var total = await ordered.CountAsync();
        var ids = await ordered
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => x.Playlist.Id)
            .ToListAsync();

        if (ids.Count == 0) return new PagedResult<PlaylistDto>([], total, page, pageSize);

        // 二段查：排名已经在上面的 id 序里定下来了，这里只负责把实体取回来。
        // 这么绕一下是因为"排名键"和"卡片要的字段"分属两条查询计划 ——
        // 排名只要 id + 聚合分（要跑子查询），卡片要 Owner/Category/统计（要连表）。
        // 投影与歌单列表接口逐字段一致，客户端推荐卡直接复用 PlaylistDto 模板。
        var fetched = await db.Playlists.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new PlaylistDto(
                p.Id, p.Name, p.Description, p.CoverUrl, p.CategoryId, p.Category != null ? p.Category.Name : null,
                p.OwnerId, p.Owner.DisplayName, p.Songs.Count, p.CollectedBy.Count, p.CreatedAt, p.IsSystem,
                p.Songs.Sum(ps => (long?)ps.Song.PlayCount) ?? 0))
            .ToListAsync();

        // 还原 id 序：上面那条 IN 查询的返回顺序由数据库决定，跟排名无关
        var byId = fetched.ToDictionary(x => x.Id);
        var items = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();

        // 无封面歌单回退取第一首歌封面 —— 与其它所有歌单列表接口同一套兜底
        await metadata.FillPlaylistCoversAsync(items);

        return new PagedResult<PlaylistDto>(items, total, page, pageSize);
    }

    public async Task<PagedResult<AdminRecommendedDto>> AdminListAsync(string? keyword, int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        // 后台**不过滤系统歌单**：运营需要看到"我喜欢的音乐"确实没被推荐（它由 !IsSystem 规则排除），
        // 列表里看不见反而会让人以为漏了数据
        var q = db.Playlists.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 与歌单列表页同一套语义：歌单名 / 创建者名 任一命中
            var pat = LikePattern.Contains(keyword);
            q = q.Where(p => EF.Functions.ILike(p.Name, pat, LikePattern.EscapeChar)
                          || EF.Functions.ILike(p.Owner.DisplayName, pat, LikePattern.EscapeChar));
        }

        var total = await q.CountAsync();

        var joined =
            from p in q
            join r in db.AdminRecommended.AsNoTracking() on p.Id equals r.PlaylistId into rs
            from r in rs.DefaultIfEmpty()
            select new { P = p, R = r };

        var items = await joined
            // 已干预的排前面、其中置顶序小的更前：运营打开这页多半是来检查/微调现有配置，
            // 一屏就能看到"我设过什么"，而不是在几百个歌单里翻。
            // 判 0 的逻辑与推荐排序里的置顶判定保持同义，避免"后台看它是第一个，推荐里却不是"。
            .OrderBy(x => x.R == null ? 1 : 0)
            .ThenBy(x => x.R == null || x.R.SortOrder <= 0 ? int.MaxValue : x.R.SortOrder)
            .ThenByDescending(x => x.P.CreatedAt)
            .ThenByDescending(x => x.P.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdminRecommendedDto(
                x.P.Id, x.P.Name, x.P.CoverUrl, x.P.Category != null ? x.P.Category.Name : null,
                x.P.Owner.DisplayName, x.P.Songs.Count, x.P.CollectedBy.Count,
                x.P.Songs.Sum(ps => (long?)ps.Song.PlayCount) ?? 0, x.P.CreatedAt, x.P.IsSystem,
                x.R == null ? 0 : x.R.SortOrder, x.R == null ? 0 : x.R.Weight,
                x.R != null && x.R.IsHidden, x.R != null))
            .ToListAsync();

        return new PagedResult<AdminRecommendedDto>(items, total, page, pageSize);
    }

    public async Task<ServiceResult> AdminSetAsync(long playlistId, UpdateRecommendedRequest req)
    {
        if (!await db.Playlists.AnyAsync(p => p.Id == playlistId)) return ServiceResult.Fail(PlaylistNotFound);

        var row = await db.AdminRecommended.FindAsync(playlistId);
        if (row is null)
        {
            row = new AdminRecommended { PlaylistId = playlistId };
            db.AdminRecommended.Add(row);
        }

        // 负的置顶序号没有语义（置顶只有"不置顶 = 0"和"第 N 位 > 0"两种），
        // 直接夹到 0 而不是报错：后台表单里输错一个负号就整单失败，体验比夹一下差。
        // Weight 不夹 —— 负权重是"压低"，是正经用法。
        row.SortOrder = Math.Max(0, req.SortOrder);
        row.Weight = req.Weight;
        row.IsHidden = req.IsHidden;

        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AdminRemoveAsync(long playlistId)
    {
        // ExecuteDelete 而不是"查出来再删"：少一次往返，也不需要把实体拉进跟踪器。
        // 影响行数 = 0 说明本来就没有干预记录，如实告诉后台，别假装成功
        // （后台"移除干预"按钮在无记录的行上本不该出现，真出现就是状态不同步）。
        var affected = await db.AdminRecommended.Where(r => r.PlaylistId == playlistId).ExecuteDeleteAsync();
        return affected > 0 ? ServiceResult.Ok() : ServiceResult.Fail(NoOverride);
    }

    /// <summary>
    /// 把查询串里的场景名归一到已知场景；未知 / 空值落到热播。
    /// 大小写与首尾空格都容忍（<c>?scene=HOT</c> 也能用）—— 接口是公开入口，不值得为这点差异返回 400。
    /// </summary>
    private static string Normalize(string? scene) => scene?.Trim().ToLowerInvariant() switch
    {
        RecommendScenes.Collected => RecommendScenes.Collected,
        RecommendScenes.New => RecommendScenes.New,
        RecommendScenes.ForYou => RecommendScenes.ForYou,
        RecommendScenes.Zone => RecommendScenes.Zone,
        _ => RecommendScenes.Default,
    };

    /// <summary>
    /// 「猜你喜欢」的口味信号（V2.12）。返回 (感兴趣的分类 Id, 感兴趣的歌手 Id)。
    /// </summary>
    private async Task<(List<int> Categories, List<long> Artists)> LoadTasteAsync(long userId)
    {
        // 分类信号两路合一：喜欢的歌曲所属分类 + 收藏过的歌单所属分区。
        // 两路都算，是因为"只收藏歌单、没点过喜欢"的用户并不少 —— 只认一条会给这类人退化成热播。
        var likedSongCategories = db.LikedSongs.AsNoTracking()
            .Where(l => l.UserId == userId && l.Song.CategoryId != null)
            .Select(l => l.Song.CategoryId ?? 0);
        var collectedPlaylistCategories = db.PlaylistCollections.AsNoTracking()
            .Where(c => c.UserId == userId && c.Playlist.CategoryId != null)
            .Select(c => c.Playlist.CategoryId ?? 0);
        var categories = await likedSongCategories.Union(collectedPlaylistCategories).ToListAsync();

        // 歌手信号：喜欢歌曲的主歌手 + 联合创作者（SongArtists）。两路 UNION 去重。
        // 联合创作必须算进来：用户喜欢一首合唱曲时，合作歌手名下的歌单拿不到任何加分就说不通了。
        var mainArtists = db.LikedSongs.AsNoTracking()
            .Where(l => l.UserId == userId).Select(l => l.Song.ArtistId);
        var jointArtists = db.LikedSongs.AsNoTracking()
            .Where(l => l.UserId == userId)
            .SelectMany(l => l.Song.SongArtists.Select(sa => sa.ArtistId));
        var artists = await mainArtists.Union(jointArtists).ToListAsync();

        return (categories, artists);
    }
}
