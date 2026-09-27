namespace YinYanMusic.Core.Entities;

/// <summary>
/// 歌曲 / 歌单评论（V2.9）。
/// <para>
/// 回复是**真树**：<see cref="ParentId"/> 自引用，层级不限（深度由服务层把关，最多 5 层）。
/// 树形数据的读法有两种，这里选的是"物化 + 索引"而不是递归查询 —— <see cref="RootId"/> 记下
/// "我属于哪条主评论"，取一整棵子树就是一条 <c>WHERE RootId IN (…)</c>，列表页每次都取
/// "这一页主评论的全部回复"，递归 CTE 在那个位置是实打实的成本。
/// </para>
/// <para>
/// <see cref="TargetType"/> + <see cref="TargetId"/> 是多态挂载（取值见 <see cref="CommentTargets"/>），
/// 所以**没有指向 Song / Playlist 的外键**：删歌曲、删歌单时数据库不会自动清评论，
/// 必须由服务层显式删（<c>SongService.DeleteAsync</c> / <c>PlaylistService.DeleteAsync</c>）。
/// </para>
/// </summary>
public class Comment
{
    public long Id { get; set; }

    /// <summary>挂载对象类型：<c>song</c> / <c>playlist</c>，取值见 <see cref="CommentTargets"/>。</summary>
    public string TargetType { get; set; } = default!;

    /// <summary>挂载对象 Id（无外键，理由见类型注释）。</summary>
    public long TargetId { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = default!;

    /// <summary>
    /// 父评论 Id：null 就是主评论，否则是回复（任意层级，最多 5 层）。
    /// 自引用外键带级联 —— 物理删一条评论时它的整棵子树由数据库一并删掉（作者删除走的是
    /// <see cref="IsDeleted"/> 占位，不触发级联）。
    /// </summary>
    public long? ParentId { get; set; }
    public Comment? Parent { get; set; }

    /// <summary>
    /// 所属**主评论** Id；主评论自己为 null（"我就是那层楼"，不必回指自己）。
    /// 子树查询与"这条主评论下共多少回复"都靠它。
    /// </summary>
    public long? RootId { get; set; }

    /// <summary>
    /// 层级：主评论 0，逐层 +1。服务层限制最深 4（也就是第 5 层）。
    /// 客户端按它决定缩进多少、以及缩进到顶格后改用"回复 @某人"表达从属关系。
    /// </summary>
    public int Depth { get; set; }

    /// <summary>
    /// 正文。纯文本，**不渲染 HTML / Markdown**（避免 XSS，也不引入渲染器依赖）；
    /// 长度 1–500 字，由服务层校验。
    /// </summary>
    public string Content { get; set; } = default!;

    /// <summary>
    /// 点赞数（冗余列）。与 <see cref="Likes"/> 的行数始终保持一致，由服务层维护 ——
    /// 列表要按它显示，若每次现算 count 就退化成逐条子查询。
    /// </summary>
    public int LikeCount { get; set; }

    /// <summary>
    /// 后台软隐藏。隐藏后普通用户的列表里不再出现（它下面的整棵子树也跟着不出现），
    /// 但作者自己仍看得到，后台可随时恢复。保留行而不是物理删除，是为了留下审计痕迹。
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// 作者本人删除，但**这层楼下还有回复** —— 于是保留成占位：正文与作者都不再对外返回，
    /// 回复照常留在它下面（删掉会让子回复跟着消失，等于替别人删了帖子）。
    /// 没有回复的评论删除时是物理删除，不留占位。
    /// </summary>
    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CommentLike> Likes { get; set; } = [];

    /// <summary>本条评论的直接回复。</summary>
    public ICollection<Comment> Replies { get; set; } = [];
}

/// <summary>
/// 评论点赞去重表（V2.9）。复合主键 (CommentId, UserId) 天然保证"同一用户对同一条评论
/// 只算一次赞"，幂等性不依赖应用层先查后插。
/// </summary>
public class CommentLike
{
    public long CommentId { get; set; }
    public Comment Comment { get; set; } = default!;

    public long UserId { get; set; }
    public User User { get; set; } = default!;
}
