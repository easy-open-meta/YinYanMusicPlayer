using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface ICommentService
{
    /// <summary>
    /// 评论列表：**主评论**分页，每条把自己的整棵回复子树嵌套装好（每条主评论最多 200 条子孙）。
    /// <paramref name="currentUserId"/> 为 null 表示未登录 —— 此时隐藏评论一律不出现，点赞态全 false。
    /// </summary>
    Task<ServiceResult<PagedResult<CommentDto>>> ListAsync(string targetType, long targetId, long? currentUserId, int page, int pageSize);

    /// <summary>
    /// 某条主评论（传回复 Id 会归到它所属的主评论）下**全部回复**的扁平分页，
    /// 供客户端"展开全部"用。按 Id 升序，父节点一定出现在子节点之前。
    /// </summary>
    Task<ServiceResult<PagedResult<CommentDto>>> ListThreadRepliesAsync(long commentId, long? currentUserId, int page, int pageSize);

    /// <summary>
    /// 可见评论**总数（主评论 + 全部回复）**，供「更多」菜单里那条「查看评论：(N)」与面板标题用。
    /// <para>
    /// 刻意不复用列表接口的 <c>Total</c>：那个只数主评论（回复跟着主评论一起翻页，所以分页进不了它），
    /// 拿它当"评论数"会和用户直觉对不上 —— 只写了一条主评论 + 三条回复，用户认为那就是 4 条评论。
    /// 可见性规则与列表一致：后台隐藏的不算，但作者自己看得到自己的。
    /// </para>
    /// </summary>
    Task<ServiceResult<int>> CountAsync(string targetType, long targetId, long? currentUserId);

    Task<ServiceResult<CommentDto>> CreateAsync(long userId, CreateCommentRequest req);

    /// <summary>作者本人删除（管理员走 <see cref="AdminDeleteAsync"/>）。这层楼下还有回复时保留占位。</summary>
    Task<ServiceResult> DeleteAsync(long userId, long commentId);

    Task<ServiceResult<CommentLikeState>> LikeAsync(long userId, long commentId);

    Task<ServiceResult<CommentLikeState>> UnlikeAsync(long userId, long commentId);

    Task<ServiceResult<PagedResult<AdminCommentDto>>> AdminListAsync(string? keyword, string? targetType, bool? isHidden, int page, int pageSize);

    /// <summary>后台隐藏 / 恢复。</summary>
    Task<ServiceResult> AdminSetHiddenAsync(long commentId, bool hidden);

    Task<ServiceResult> AdminDeleteAsync(long commentId);
}

/// <summary>
/// 评论服务（V2.9）。列表/发表/删除/点赞 + 后台审核，另被 SongService / PlaylistService
/// 借道做"删歌曲、删歌单时清评论"。
/// </summary>
public class CommentService(MusicDbContext db) : ICommentService
{
    /// <summary>正文长度上限（与 <see cref="Comment.Content"/> 的列长一致）。</summary>
    private const int MaxContentLength = 500;

    /// <summary>同一用户两条评论之间的最短间隔（TC-2.9-03）。</summary>
    private static readonly TimeSpan PostInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 回复层级上限：主评论算第 1 层（<c>Depth = 0</c>），最深到第 5 层（<c>Depth = 4</c>）。
    /// 不限深度的话，一条评论能被无限怼下去 —— 界面缩进会顶到屏幕外，每层还要多一次装配开销。
    /// </summary>
    public const int MaxDepth = 4;

    /// <summary>
    /// 列表接口里**每条主评论**最多带回的子孙条数。超出的部分不丢，客户端用"展开全部"
    /// 调 <see cref="ListThreadRepliesAsync"/> 分页拉 —— 热门评论下动辄上千条回复，
    /// 一次全带回来会把整个列表响应撑爆。
    /// </summary>
    private const int MaxRepliesPerRoot = 200;

    /// <summary>
    /// 列表每页上限。评论是用户产生的内容，量级不可控 ——
    /// 没有上限时一个 <c>?pageSize=100000</c> 就能把整条评论流一次拉走。
    /// </summary>
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    /// <summary>
    /// 子树分页的每页上限。刻意取得与 <see cref="MaxRepliesPerRoot"/> 一样大：
    /// 客户端第一次"展开全部"取到的正是列表里已经给过的那 200 条（同一套可见性规则、同一 Id 升序），
    /// 于是展开不会出现"越展开越少"的观感，之后才一页页往下追加。
    /// </summary>
    private const int MaxSubtreePageSize = 200;

    /// <summary>
    /// 错误文案常量：控制器按文案映射状态码（404 / 403），集中一处避免
    /// "服务层改一个字，控制器就静默退化成 400"。
    /// </summary>
    public const string CommentNotFound = "评论不存在。";
    public const string TargetNotFound = "评论对象不存在。";
    public const string NoPermission = "无权操作。";

    /// <summary>作者已删除但下面还有回复的那层楼，对外统一显示这句。</summary>
    public const string DeletedContent = "该评论已删除";

    public async Task<ServiceResult<PagedResult<CommentDto>>> ListAsync(
        string targetType, long targetId, long? currentUserId, int page, int pageSize)
    {
        if (!CommentTargets.IsValid(targetType))
            return ServiceResult<PagedResult<CommentDto>>.Fail("评论对象类型不正确。");

        // 未登录时用 0 顶替当前用户 Id：库里 Id 都是正数，0 天然不匹配任何人，
        // 于是"隐藏评论只对作者可见"和"点赞态"两条规则用同一个表达式就够了。
        var me = currentUserId ?? 0;

        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // 只有主评论参与分页：回复跟着自己那条主评论一起翻页，
        // 否则翻到第 2 页会看到"半棵树"（父评论在第 1 页、回复在第 2 页）。
        var rootsQuery = db.Comments.Where(c => c.TargetType == targetType && c.TargetId == targetId && c.ParentId == null
            && (!c.IsHidden || c.UserId == me));

        var total = await rootsQuery.CountAsync();

        // 主评论按时间倒序（新的在上）；再按 Id 兜底，保证同一秒内的多条在翻页时顺序稳定，
        // 否则 Skip/Take 会重复或漏条（TC-2.9-11）。
        var rootRows = await rootsQuery.Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var rootIds = rootRows.Select(c => c.Id).ToList();
        var threads = await LoadThreadsAsync(rootIds, me);
        var replyCounts = await LoadReplyCountsAsync(rootIds, me);
        var liked = await LoadLikedAsync(me, rootIds.Concat(threads.Values.SelectMany(v => v.Select(c => c.Id))));

        var items = new List<CommentDto>(rootRows.Count);
        foreach (var root in rootRows)
        {
            var flat = threads.TryGetValue(root.Id, out var nodes) ? nodes : [];
            var shown = flat.Count > MaxRepliesPerRoot ? flat.Take(MaxRepliesPerRoot).ToList() : flat;
            var childrenOf = BuildChildrenMap(shown);
            var replies = childrenOf.TryGetValue(root.Id, out var direct)
                ? direct.Select(c => BuildNode(c, childrenOf, me, liked)).ToList()
                : [];
            var replyCount = replyCounts.TryGetValue(root.Id, out var count) ? count : 0;

            items.Add(Map(root, me, liked, replies, replyCount, replyCount > shown.Count));
        }

        return ServiceResult<PagedResult<CommentDto>>.Ok(new PagedResult<CommentDto>(items, total, page, pageSize));
    }

    public async Task<ServiceResult<PagedResult<CommentDto>>> ListThreadRepliesAsync(
        long commentId, long? currentUserId, int page, int pageSize)
    {
        var anchor = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (anchor is null) return ServiceResult<PagedResult<CommentDto>>.Fail(CommentNotFound);

        // 传回复的 Id 也接受：归到它所属的那条主评论（客户端只会传主评论，这里只是别让调用方踩空）
        var rootId = anchor.RootId ?? anchor.Id;
        var me = currentUserId ?? 0;
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, MaxSubtreePageSize);

        var query = db.Comments.Where(c => c.RootId == rootId && (!c.IsHidden || c.UserId == me));
        var total = await query.CountAsync();

        // 扁平返回，按 Id 升序：父节点一定先于子节点出现，调用方扫一遍就能自建层级
        var rows = await query.Include(c => c.User)
            .OrderBy(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        var liked = await LoadLikedAsync(me, rows.Select(c => c.Id));
        var items = rows.Select(c => Map(c, me, liked, [])).ToList();

        return ServiceResult<PagedResult<CommentDto>>.Ok(new PagedResult<CommentDto>(items, total, page, pageSize));
    }

    public async Task<ServiceResult<int>> CountAsync(string targetType, long targetId, long? currentUserId)
    {
        if (!CommentTargets.IsValid(targetType))
            return ServiceResult<int>.Fail("评论对象类型不正确。");

        // 与列表同一套可见性：未登录时 me = 0，被隐藏的一律不计
        var me = currentUserId ?? 0;
        var count = await db.Comments.CountAsync(c => c.TargetType == targetType && c.TargetId == targetId
            && (!c.IsHidden || c.UserId == me));

        return ServiceResult<int>.Ok(count);
    }

    public async Task<ServiceResult<CommentDto>> CreateAsync(long userId, CreateCommentRequest req)
    {
        if (!CommentTargets.IsValid(req.TargetType))
            return ServiceResult<CommentDto>.Fail("评论对象类型不正确。");

        var content = req.Content?.Trim() ?? string.Empty;
        if (content.Length == 0) return ServiceResult<CommentDto>.Fail("评论内容不能为空。");
        if (content.Length > MaxContentLength)
            return ServiceResult<CommentDto>.Fail($"评论内容不能超过 {MaxContentLength} 字。");

        if (!await TargetExistsAsync(req.TargetType, req.TargetId))
            return ServiceResult<CommentDto>.Fail(TargetNotFound);

        // 限流：统计"最近 10 秒内这个人发过几条"，与 AuthService 的发码限流同一套做法
        // （都在服务层查库判断，项目里没有引入 ASP.NET 的 RateLimiter）。
        var since = DateTime.UtcNow - PostInterval;
        if (await db.Comments.AnyAsync(c => c.UserId == userId && c.CreatedAt >= since))
            return ServiceResult<CommentDto>.Fail("评论发送过于频繁，请稍后再试。");

        long? parentId = null;
        long? rootId = null;
        var depth = 0;
        if (req.ParentId.HasValue)
        {
            var parent = await db.Comments.FirstOrDefaultAsync(c => c.Id == req.ParentId.Value);
            if (parent is null) return ServiceResult<CommentDto>.Fail(CommentNotFound);
            if (parent.TargetType != req.TargetType || parent.TargetId != req.TargetId)
                return ServiceResult<CommentDto>.Fail("回复的评论不属于同一对象。");
            // 占位楼（作者已删）下面不再接受新回复：回过去也没人看得到作者是谁
            if (parent.IsDeleted) return ServiceResult<CommentDto>.Fail("该评论已删除，无法回复。");
            if (parent.Depth >= MaxDepth) return ServiceResult<CommentDto>.Fail($"回复层级最多 {MaxDepth + 1} 层。");

            parentId = parent.Id;
            depth = parent.Depth + 1;
            // 主评论的 RootId 是 null（"自己就是那层楼"），用 ?? 把它补成父评论的 Id
            rootId = parent.RootId ?? parent.Id;
        }

        var comment = new Comment
        {
            TargetType = req.TargetType,
            TargetId = req.TargetId,
            UserId = userId,
            ParentId = parentId,
            RootId = rootId,
            Depth = depth,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        var author = await db.Users.Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName, u.AvatarUrl }).FirstOrDefaultAsync();

        var dto = new CommentDto(comment.Id, userId, author?.DisplayName ?? string.Empty, author?.AvatarUrl,
            comment.ParentId, comment.Content, comment.LikeCount, false, false, true, comment.CreatedAt, [], depth);
        return ServiceResult<CommentDto>.Ok(dto);
    }

    public async Task<ServiceResult> DeleteAsync(long userId, long commentId)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null) return ServiceResult.Fail(CommentNotFound);
        if (comment.UserId != userId) return ServiceResult.Fail(NoPermission);

        // 已经删过了：重复调用当成功（幂等），否则用户连点两次会看到"评论不存在"
        if (comment.IsDeleted) return ServiceResult.Ok();

        // 下面还有回复 → 只能保留占位：连子回复一起物理删掉，等于替别人删了帖子
        if (await db.Comments.AnyAsync(c => c.ParentId == comment.Id))
        {
            comment.IsDeleted = true;
            comment.Content = string.Empty;
            comment.LikeCount = 0;
            await db.CommentLikes.Where(l => l.CommentId == comment.Id).ExecuteDeleteAsync();
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // 没有回复（或者下面只剩占位楼）→ 直接物理删除，不留空壳；
        // 残余的占位楼由外键级联一并带走（它们已经没有内容，不存在数据损失）
        db.Comments.Remove(comment);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<CommentLikeState>> LikeAsync(long userId, long commentId)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null) return ServiceResult<CommentLikeState>.Fail(CommentNotFound);

        var exists = await db.CommentLikes.AnyAsync(l => l.CommentId == commentId && l.UserId == userId);
        // 已赞过就直接返回当前状态：连点两次不能把 LikeCount 变 2（TC-2.9-07）
        if (!exists)
        {
            db.CommentLikes.Add(new CommentLike { CommentId = commentId, UserId = userId });
            comment.LikeCount++;
            await db.SaveChangesAsync();
        }
        return ServiceResult<CommentLikeState>.Ok(new CommentLikeState(comment.LikeCount, true));
    }

    public async Task<ServiceResult<CommentLikeState>> UnlikeAsync(long userId, long commentId)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null) return ServiceResult<CommentLikeState>.Fail(CommentNotFound);

        var like = await db.CommentLikes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId);
        if (like is not null)
        {
            db.CommentLikes.Remove(like);
            // 历史数据若出现 LikeCount 与点赞行数不一致，也不要让它掉到负数
            comment.LikeCount = Math.Max(0, comment.LikeCount - 1);
            await db.SaveChangesAsync();
        }
        return ServiceResult<CommentLikeState>.Ok(new CommentLikeState(comment.LikeCount, false));
    }

    public async Task<ServiceResult<PagedResult<AdminCommentDto>>> AdminListAsync(
        string? keyword, string? targetType, bool? isHidden, int page, int pageSize)
    {
        var query = db.Comments.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = LikePattern.Contains(keyword.Trim());
            query = query.Where(c => EF.Functions.ILike(c.Content, pattern, LikePattern.EscapeChar));
        }
        if (CommentTargets.IsValid(targetType)) query = query.Where(c => c.TargetType == targetType);
        if (isHidden.HasValue) query = query.Where(c => c.IsHidden == isHidden.Value);

        var total = await query.CountAsync();
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var rows = await query.Include(c => c.User)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        // 标题要现查：TargetId 是多态的，建不了外键也就 join 不到，只能按类型分别查一遍
        var titles = await ResolveTitlesAsync(rows);

        var items = rows.Select(c => new AdminCommentDto(
            c.Id, c.TargetType, c.TargetId,
            titles.TryGetValue((c.TargetType, c.TargetId), out var title) ? title : "（已被删除）",
            c.UserId, c.User.DisplayName, c.ParentId,
            // 占位楼的正文在库里已经清空，后台统一显示这句，否则审核页上是个空白格子
            c.IsDeleted ? DeletedContent : c.Content,
            c.LikeCount, c.IsHidden, c.CreatedAt, c.Depth, c.IsDeleted))
            .ToList();

        return ServiceResult<PagedResult<AdminCommentDto>>.Ok(new PagedResult<AdminCommentDto>(items, total, page, pageSize));
    }

    public async Task<ServiceResult> AdminSetHiddenAsync(long commentId, bool hidden)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null) return ServiceResult.Fail(CommentNotFound);

        comment.IsHidden = hidden;
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> AdminDeleteAsync(long commentId)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment is null) return ServiceResult.Fail(CommentNotFound);

        // 后台删除是**物理删除**（把整棵子树一起带走）：违规内容不该在库里留存。
        // 只想让内容不可见、还想留着恢复的，用隐藏而不是删除。
        db.Comments.Remove(comment);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private async Task<bool> TargetExistsAsync(string targetType, long targetId) => targetType switch
    {
        CommentTargets.Song => await db.Songs.AnyAsync(s => s.Id == targetId),
        CommentTargets.Playlist => await db.Playlists.AnyAsync(p => p.Id == targetId),
        _ => false
    };

    /// <summary>后台列表要显示"评论挂在哪首歌/哪个歌单上"，标题按类型分批查（多态 TargetId，join 不到）。</summary>
    private async Task<Dictionary<(string, long), string>> ResolveTitlesAsync(IReadOnlyList<Comment> rows)
    {
        var result = new Dictionary<(string, long), string>();

        var songIds = rows.Where(c => c.TargetType == CommentTargets.Song).Select(c => c.TargetId).Distinct().ToList();
        if (songIds.Count > 0)
        {
            var songs = await db.Songs.Where(s => songIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Title }).ToListAsync();
            foreach (var s in songs) result[(CommentTargets.Song, s.Id)] = s.Title;
        }

        var playlistIds = rows.Where(c => c.TargetType == CommentTargets.Playlist).Select(c => c.TargetId).Distinct().ToList();
        if (playlistIds.Count > 0)
        {
            var playlists = await db.Playlists.Where(p => playlistIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name }).ToListAsync();
            foreach (var p in playlists) result[(CommentTargets.Playlist, p.Id)] = p.Name;
        }

        return result;
    }

    /// <summary>
    /// 取这几条主评论各自的整棵子树（不含主评论自己）。一条 <c>WHERE RootId IN (…)</c> 解决，
    /// 不写递归查询。
    /// <para>
    /// 总量按 <c>主评论数 × 201</c> 截断：多取的那一条只用于判断"是否被截断"。
    /// ⚠️ 这个预算是**共享**的：万一某条主评论一家占了全部预算，其他主评论会少拿甚至拿不到回复，
    /// 但它们各自的 <c>ReplyCount</c> 由另一条聚合查询给出，客户端照样能看到"共 N 条"并展开全部 ——
    /// 降级是可见的、可恢复的，不会丢数据。
    /// </para>
    /// </summary>
    private async Task<Dictionary<long, List<Comment>>> LoadThreadsAsync(List<long> rootIds, long me)
    {
        var result = new Dictionary<long, List<Comment>>();
        if (rootIds.Count == 0) return result;

        var rows = await db.Comments
            .Where(c => c.RootId != null && rootIds.Contains(c.RootId.Value) && (!c.IsHidden || c.UserId == me))
            .Include(c => c.User)
            .OrderBy(c => c.Id)
            .Take(rootIds.Count * (MaxRepliesPerRoot + 1))
            .ToListAsync();

        foreach (var group in rows.GroupBy(c => c.RootId!.Value))
            result[group.Key] = [.. group.OrderBy(c => c.Id)];

        return result;
    }

    /// <summary>每条主评论下可见的回复总数（与列表同一套可见性规则），用于"共 N 条"。</summary>
    private async Task<Dictionary<long, int>> LoadReplyCountsAsync(List<long> rootIds, long me)
    {
        if (rootIds.Count == 0) return [];

        var rows = await db.Comments
            .Where(c => c.RootId != null && rootIds.Contains(c.RootId.Value) && (!c.IsHidden || c.UserId == me))
            .GroupBy(c => c.RootId!.Value)
            .Select(g => new { RootId = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows.ToDictionary(r => r.RootId, r => r.Count);
    }

    private async Task<HashSet<long>> LoadLikedAsync(long me, IEnumerable<long> commentIds)
    {
        if (me == 0) return [];

        var ids = commentIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var rows = await db.CommentLikes.Where(l => l.UserId == me && ids.Contains(l.CommentId))
            .Select(l => l.CommentId).ToListAsync();
        return rows.ToHashSet();
    }

    private static Dictionary<long, List<Comment>> BuildChildrenMap(IReadOnlyList<Comment> flat)
    {
        var map = new Dictionary<long, List<Comment>>();
        foreach (var node in flat)
        {
            if (node.ParentId is not long parentId) continue;
            if (!map.TryGetValue(parentId, out var list)) map[parentId] = list = [];
            list.Add(node);
        }
        return map;
    }

    /// <summary>
    /// 从某个节点往下递归装配。父节点不在集合里（被后台隐藏，或落在 200 条上限之外）的节点
    /// 自然不会被走到 —— 也就不会以"没有父级的孤儿"出现在列表里。
    /// </summary>
    private static CommentDto BuildNode(Comment node, Dictionary<long, List<Comment>> childrenOf, long me, HashSet<long> liked)
    {
        var replies = childrenOf.TryGetValue(node.Id, out var kids)
            ? kids.Select(c => BuildNode(c, childrenOf, me, liked)).ToList()
            : [];
        return Map(node, me, liked, replies);
    }

    private static CommentDto Map(Comment c, long me, HashSet<long> liked, IReadOnlyList<CommentDto> replies,
                                  int replyCount = 0, bool hasMoreReplies = false)
    {
        // 占位楼：作者与正文都不再对外返回（删除时已清空正文、清掉点赞行）
        if (c.IsDeleted)
            return new CommentDto(c.Id, 0, string.Empty, null, c.ParentId, c.Content.Length > 0 ? c.Content : DeletedContent,
                0, false, false, false, c.CreatedAt, replies, c.Depth, true, replyCount, hasMoreReplies);

        return new CommentDto(c.Id, c.UserId, c.User.DisplayName, c.User.AvatarUrl, c.ParentId, c.Content, c.LikeCount,
            liked.Contains(c.Id), c.IsHidden, c.UserId == me, c.CreatedAt, replies, c.Depth, false, replyCount, hasMoreReplies);
    }
}
