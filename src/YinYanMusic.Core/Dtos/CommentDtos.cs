namespace YinYanMusic.Core.Dtos;

/// <summary>
/// 一条评论。回复是**真树**：<paramref name="Replies"/> 可以一层层往下套（服务端限制最深 5 层）。
/// <para>
/// 两种返回形态：
/// <list type="bullet">
///   <item><b>评论列表</b>（<c>GET api/comments</c>）：主评论分页，每条把自己的整棵子树按
///         <paramref name="Replies"/> 嵌套装好（每条主评论最多 200 条子孙，超出时
///         <paramref name="HasMoreReplies"/> 为 true）。</item>
///   <item><b>子树分页</b>（<c>GET api/comments/{id}/replies</c>）：返回**扁平**列表，
///         <paramref name="Replies"/> 恒为空，调用方按 <paramref name="ParentId"/> 自建层级
///         （按 Id 升序，父节点一定先出现）、按 <paramref name="Depth"/> 决定缩进。</item>
/// </list>
/// </para>
/// </summary>
public record CommentDto(
    long Id,
    long UserId,
    string UserName,
    string? UserAvatarUrl,
    long? ParentId,
    string Content,
    int LikeCount,
    bool IsLiked,
    bool IsHidden,
    bool IsMine,
    DateTime CreatedAt,
    IReadOnlyList<CommentDto> Replies,
    /// <summary>层级：主评论 0，逐层 +1。缩进与"回复 @某人"都按它算。</summary>
    int Depth = 0,
    /// <summary>
    /// 作者已删除的**占位**（这层楼下还有回复所以没被物理删掉）。
    /// 此时 <paramref name="Content"/> 是"该评论已删除"，作者、点赞数、<paramref name="IsMine"/> 都为空值，
    /// 客户端不要给它任何操作按钮。
    /// </summary>
    bool IsDeleted = false,
    /// <summary>仅主评论有意义：整棵子树里可见的回复总数（**不受**每页 200 条上限影响，用于显示"共 N 条"）。</summary>
    int ReplyCount = 0,
    /// <summary>仅主评论有意义：本页带回的子孙被 200 条上限截断了，客户端应给"展开全部"入口。</summary>
    bool HasMoreReplies = false);

/// <summary>
/// 发表评论。<paramref name="TargetType"/> 取值见 <see cref="CommentTargets"/>；
/// <paramref name="ParentId"/> 非空表示回复（可以回复任意层级，只要不超过 5 层）。
/// </summary>
public record CreateCommentRequest(string TargetType, long TargetId, long? ParentId, string Content);

/// <summary>点赞 / 取消后的新状态：客户端就地刷新按钮，不必重新拉整页列表。</summary>
public record CommentLikeState(int LikeCount, bool IsLiked);

/// <summary>
/// 后台评论列表项：比 <see cref="CommentDto"/> 多出 <paramref name="TargetType"/> /
/// <paramref name="TargetId"/> / <paramref name="TargetTitle"/>（"这条评论挂在哪首歌/哪个歌单上"，
/// 审核时必须看得到），少掉点赞态和回复子列表（后台是逐条审核，不关心楼层形状）。
/// </summary>
public record AdminCommentDto(
    long Id,
    string TargetType,
    long TargetId,
    string TargetTitle,
    long UserId,
    string UserName,
    long? ParentId,
    string Content,
    int LikeCount,
    bool IsHidden,
    DateTime CreatedAt,
    /// <summary>层级：主评论 0，逐层 +1。</summary>
    int Depth = 0,
    /// <summary>作者已删除的占位（正文在库里已清空，这里统一显示"该评论已删除"）。</summary>
    bool IsDeleted = false);
