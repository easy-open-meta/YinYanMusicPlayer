using Microsoft.EntityFrameworkCore;
using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;
using YinYanMusic.Data;

namespace YinYanMusic.Application;

public interface ICatalogService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync();
    Task<ServiceResult<CategoryDto>> CreateCategoryAsync(CategoryDto req);
    Task<ServiceResult<CategoryDto>> UpdateCategoryAsync(int id, CategoryDto req);
    Task<ServiceResult<bool>> DeleteCategoryAsync(int id);
    Task<PagedResult<ArtistDto>> GetArtistsAsync(string? keyword, int page, int pageSize);
    Task<ArtistDto?> GetArtistAsync(long id);
    Task<ServiceResult<ArtistDto>> CreateArtistAsync(CreateArtistRequest req);
    Task<ServiceResult<ArtistDto>> UpdateArtistAsync(long id, UpdateArtistRequest req);
    Task<ServiceResult<bool>> DeleteArtistAsync(long id);
    Task<PagedResult<AlbumDto>> GetAlbumsAsync(long? artistId, string? keyword, int page, int pageSize);
    Task<AlbumDto?> GetAlbumAsync(long id);
    Task<ServiceResult<AlbumDto>> CreateAlbumAsync(CreateAlbumRequest req);
    Task<ServiceResult<AlbumDto>> UpdateAlbumAsync(long id, UpdateAlbumRequest req);
    Task<ServiceResult<bool>> DeleteAlbumAsync(long id);
}

public class CatalogService(MusicDbContext db, AudioMetadataService metadata) : ICatalogService
{
    private const string ContentModeError =
        $"内容类型只支持 {CategoryContentModes.Both} / {CategoryContentModes.Songs} / {CategoryContentModes.Playlists}。";

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync() =>
        await db.Categories.OrderBy(c => c.Id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slogan, c.ColorHex, c.IconGlyph, c.ContentMode))
            .ToListAsync();

    public async Task<ServiceResult<CategoryDto>> CreateCategoryAsync(CategoryDto req)
    {
        var name = req.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name))
            return ServiceResult<CategoryDto>.Fail("分类已存在。");
        // 不传 = 默认 both；传了非法值直接拒绝，别让脏值进库（App 侧另有归一兜底，但那是读路径的保险）
        if (req.ContentMode is not null && !CategoryContentModes.IsValid(req.ContentMode))
            return ServiceResult<CategoryDto>.Fail(ContentModeError);

        var cat = new Category
        {
            Name = name,
            Slogan = req.Slogan,
            ColorHex = req.ColorHex,
            IconGlyph = req.IconGlyph,
            ContentMode = CategoryContentModes.Normalize(req.ContentMode)
        };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return ServiceResult<CategoryDto>.Ok(cat.ToDto());
    }

    public async Task<ServiceResult<CategoryDto>> UpdateCategoryAsync(int id, CategoryDto req)
    {
        var cat = await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (cat is null) return ServiceResult<CategoryDto>.Fail("分类不存在。");

        var name = req.Name.Trim();
        if (!string.IsNullOrEmpty(name) && name != cat.Name
            && await db.Categories.AnyAsync(c => c.Name == name))
            return ServiceResult<CategoryDto>.Fail("分类名已存在。");
        if (req.ContentMode is not null && !CategoryContentModes.IsValid(req.ContentMode))
            return ServiceResult<CategoryDto>.Fail(ContentModeError);

        if (!string.IsNullOrEmpty(name)) cat.Name = name;
        if (req.Slogan is not null) cat.Slogan = req.Slogan;
        if (req.ColorHex is not null) cat.ColorHex = req.ColorHex;
        if (req.IconGlyph is not null) cat.IconGlyph = req.IconGlyph;
        // null = 这次不改（局部更新约定）；传了就用传的
        if (req.ContentMode is not null) cat.ContentMode = req.ContentMode;

        await db.SaveChangesAsync();
        return ServiceResult<CategoryDto>.Ok(cat.ToDto());
    }

    public async Task<ServiceResult<bool>> DeleteCategoryAsync(int id)
    {
        var cat = await db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        if (cat is null) return ServiceResult<bool>.Fail("分类不存在。");

        // 引用检查：还有歌曲挂在这上面不让删。
        var songCount = await db.Songs.CountAsync(s => s.CategoryId == id);
        if (songCount > 0)
            return ServiceResult<bool>.Fail($"该分类下还有 {songCount} 首歌曲，无法删除。请先将歌曲改到其它分类或清空分类。");

        db.Categories.Remove(cat);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<PagedResult<ArtistDto>> GetArtistsAsync(string? keyword, int page, int pageSize)
    {
        var q = db.Artists.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 与歌曲页同一套语义：一个关键词同时匹配多个字段。
            // 歌手这边是 名字 / 地区 / 类型 —— 搜「华语」能列出所有华语歌手，
            // 搜「乐队」能列出所有乐队（后台列表里这三列本来就在展示）。
            var pat = LikePattern.Contains(keyword);
            q = q.Where(a => EF.Functions.ILike(a.Name, pat, LikePattern.EscapeChar)
                || (a.Region != null && EF.Functions.ILike(a.Region, pat, LikePattern.EscapeChar))
                || (a.Kind != null && EF.Functions.ILike(a.Kind, pat, LikePattern.EscapeChar)));
        }
        var total = await q.CountAsync();
        // 统计字段用子查询在库端算（一次往返），否则列表里 songCount/albumCount/followerCount
        // 会全部是 0 —— 前端「歌手」页那三列就一直是 0，看起来像"没加载数据"。
        var items = await q.OrderBy(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new ArtistDto(
                a.Id, a.Name, a.Region, a.Kind, a.AvatarUrl, a.Bio,
                db.ArtistFollows.Count(f => f.ArtistId == a.Id),
                // 歌数走关联表：联合创作的歌在每位歌手名下都算一首（与歌手页的列表口径一致）
                db.SongArtists.Count(sa => sa.ArtistId == a.Id),
                db.Albums.Count(al => al.ArtistId == a.Id)))
            .ToListAsync();
        return new PagedResult<ArtistDto>(items, total, page, pageSize);
    }

    public async Task<ArtistDto?> GetArtistAsync(long id)
    {
        var artist = await db.Artists.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (artist is null) return null;
        var songCount = await db.SongArtists.CountAsync(sa => sa.ArtistId == id);
        var albumCount = await db.Albums.CountAsync(a => a.ArtistId == id);
        var followerCount = await db.ArtistFollows.CountAsync(f => f.ArtistId == id);
        return artist.ToDto(followerCount, songCount, albumCount);
    }

    public async Task<ServiceResult<ArtistDto>> CreateArtistAsync(CreateArtistRequest req)
    {
        // 头像允许存 base64（data URI），但要走格式与体积校验 —— 见 ImageDataUri
        if (ImageDataUri.Validate(req.AvatarUrl) is string avatarError)
            return ServiceResult<ArtistDto>.Fail(avatarError);

        var artist = new Artist
        {
            Name = req.Name.Trim(),
            Region = req.Region,
            Kind = req.Kind,
            AvatarUrl = req.AvatarUrl,
            Bio = req.Bio
        };
        db.Artists.Add(artist);
        await db.SaveChangesAsync();
        return ServiceResult<ArtistDto>.Ok(artist.ToDto());
    }

    public async Task<ServiceResult<ArtistDto>> UpdateArtistAsync(long id, UpdateArtistRequest req)
    {
        var artist = await db.Artists.FirstOrDefaultAsync(a => a.Id == id);
        if (artist is null) return ServiceResult<ArtistDto>.Fail("歌手不存在。");

        if (req.AvatarUrl is not null && ImageDataUri.Validate(req.AvatarUrl) is string avatarError)
            return ServiceResult<ArtistDto>.Fail(avatarError);

        if (req.Name is not null) artist.Name = req.Name.Trim();
        if (req.Region is not null) artist.Region = req.Region;
        if (req.Kind is not null) artist.Kind = req.Kind;
        if (req.ClearAvatar) artist.AvatarUrl = null;
        else if (req.AvatarUrl is not null) artist.AvatarUrl = req.AvatarUrl;
        if (req.Bio is not null) artist.Bio = req.Bio;

        await db.SaveChangesAsync();
        var songCount = await db.SongArtists.CountAsync(sa => sa.ArtistId == id);
        var albumCount = await db.Albums.CountAsync(a => a.ArtistId == id);
        var followerCount = await db.ArtistFollows.CountAsync(f => f.ArtistId == id);
        return ServiceResult<ArtistDto>.Ok(artist.ToDto(followerCount, songCount, albumCount));
    }

    public async Task<ServiceResult<bool>> DeleteArtistAsync(long id)
    {
        var artist = await db.Artists.FirstOrDefaultAsync(a => a.Id == id);
        if (artist is null) return ServiceResult<bool>.Fail("歌手不存在。");

        var songCount = await db.SongArtists.CountAsync(sa => sa.ArtistId == id);
        if (songCount > 0)
            return ServiceResult<bool>.Fail($"该歌手下还有 {songCount} 首歌曲，无法删除。请先删除或迁移歌曲后再试。");

        var albumCount = await db.Albums.CountAsync(a => a.ArtistId == id);
        if (albumCount > 0)
            return ServiceResult<bool>.Fail($"该歌手下还有 {albumCount} 张专辑，无法删除。请先删除或迁移专辑后再试。");

        db.ArtistFollows.RemoveRange(db.ArtistFollows.Where(f => f.ArtistId == id));
        db.Artists.Remove(artist);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<PagedResult<AlbumDto>> GetAlbumsAsync(long? artistId, string? keyword, int page, int pageSize)
    {
        var q = db.Albums.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 与歌曲页同一套语义：专辑名 / 歌手名 任一命中即可 ——
            // 搜歌手名能直接列出他名下的专辑（这也是歌曲页搜歌手名能出结果的同一条路径）。
            var pat = LikePattern.Contains(keyword);
            q = q.Where(a => EF.Functions.ILike(a.Name, pat, LikePattern.EscapeChar) || EF.Functions.ILike(a.Artist.Name, pat, LikePattern.EscapeChar));
        }
        if (artistId.HasValue)
        {
            // 联合创作专辑（V2.12）：主歌手（单值列）或任一关联歌手命中的都算"他/她的专辑"
            q = q.Where(a => a.ArtistId == artistId || a.AlbumArtists.Any(aa => aa.ArtistId == artistId));
        }
        var total = await q.CountAsync();
        var items = await q
            // 按发行时间倒序（最新的在前）。⚠️ PostgreSQL 的 DESC 默认把 NULL 排最前，
            // 所以先按"有没有发行日期"排一次，把没日期的专辑压到最后，再按日期倒序。
            .OrderBy(a => a.ReleaseDate == null)
            .ThenByDescending(a => a.ReleaseDate)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AlbumDto(a.Id, a.Name, a.CoverUrl, a.ReleaseDate, a.Description,
                new ArtistDto(a.Artist.Id, a.Artist.Name, a.Artist.Region, a.Artist.Kind, a.Artist.AvatarUrl, a.Artist.Bio, 0, 0, 0),
                db.Songs.Count(s => s.AlbumId == a.Id)))
            .ToListAsync();
        // 联合创作列表（V2.12）：一页专辑数有限（≤50），逐张取歌手列表，无 N+1 灾难
        var withArtists = new List<AlbumDto>(items.Count);
        foreach (var album in items)
            withArtists.Add(album with { Artists = await LoadAlbumArtistsAsync(album.Id) });
        items = withArtists;
        // 发行时间以**文件标签**为准（库里那份是导入那一刻的快照）—— 见 AudioMetadataService
        await metadata.FillAlbumReleaseDatesAsync(items);
        return new PagedResult<AlbumDto>(items, total, page, pageSize);
    }

    public async Task<AlbumDto?> GetAlbumAsync(long id)
    {
        var album = await db.Albums.AsNoTracking().Include(a => a.Artist).FirstOrDefaultAsync(a => a.Id == id);
        if (album is null) return null;
        var dto = album.ToDto(await db.Songs.CountAsync(s => s.AlbumId == id));
        dto = dto with { Artists = await LoadAlbumArtistsAsync(id) };
        await metadata.FillAlbumReleaseDatesAsync([dto]);
        return dto;
    }

    public async Task<ServiceResult<AlbumDto>> CreateAlbumAsync(CreateAlbumRequest req)
    {
        var artistIds = await ResolveAlbumArtistIdsAsync(req.ArtistIds, req.ArtistId);
        if (artistIds is null) return ServiceResult<AlbumDto>.Fail("歌手不存在。");
        // 封面允许存 base64（data URI），同样走格式与体积校验
        if (ImageDataUri.Validate(req.CoverUrl) is string coverError)
            return ServiceResult<AlbumDto>.Fail(coverError);

        var album = new Album
        {
            ArtistId = artistIds[0],   // 单值列 = 主歌手（与关联表 Position=0 同步）
            Name = req.Name.Trim(),
            ReleaseDate = req.ReleaseDate,
            Description = req.Description,
            CoverUrl = req.CoverUrl
        };
        for (var i = 0; i < artistIds.Count; i++)
            album.AlbumArtists.Add(new AlbumArtist { ArtistId = artistIds[i], Position = i });

        db.Albums.Add(album);
        await db.SaveChangesAsync();
        await db.Entry(album).Reference(a => a.Artist).LoadAsync();
        var dto = album.ToDto();
        dto = dto with { Artists = await LoadAlbumArtistsAsync(album.Id) };
        return ServiceResult<AlbumDto>.Ok(dto);
    }

    public async Task<ServiceResult<AlbumDto>> UpdateAlbumAsync(long id, UpdateAlbumRequest req)
    {
        var album = await db.Albums.Include(a => a.Artist).FirstOrDefaultAsync(a => a.Id == id);
        if (album is null) return ServiceResult<AlbumDto>.Fail("专辑不存在。");

        if (req.Name is not null) album.Name = req.Name.Trim();
        if (req.ClearReleaseDate) album.ReleaseDate = null;
        else if (req.ReleaseDate.HasValue) album.ReleaseDate = req.ReleaseDate;
        if (req.Description is not null) album.Description = req.Description;
        if (req.ClearCover) album.CoverUrl = null;
        else if (req.CoverUrl is not null)
        {
            if (ImageDataUri.Validate(req.CoverUrl) is string coverError)
                return ServiceResult<AlbumDto>.Fail(coverError);
            album.CoverUrl = req.CoverUrl;
        }

        // 完整歌手列表（V2.12）：传了（非空）就整体重建关联并同步单值列
        if (req.ArtistIds is { Count: > 0 })
        {
            var artistIds = await ResolveAlbumArtistIdsAsync(req.ArtistIds, 0);
            if (artistIds is null) return ServiceResult<AlbumDto>.Fail("歌手不存在。");

            var old = await db.AlbumArtists.Where(aa => aa.AlbumId == id).ToListAsync();
            db.AlbumArtists.RemoveRange(old);
            for (var i = 0; i < artistIds.Count; i++)
                db.AlbumArtists.Add(new AlbumArtist { AlbumId = id, ArtistId = artistIds[i], Position = i });
            album.ArtistId = artistIds[0];
        }

        await db.SaveChangesAsync();
        var result = album.ToDto();
        result = result with { Artists = await LoadAlbumArtistsAsync(album.Id) };
        return ServiceResult<AlbumDto>.Ok(result);
    }

    /// <summary>
    /// 归一专辑的歌手列表：传了 ArtistIds 以它为准（去重），否则退回单歌手 [artistId]。
    /// 返回 null 表示至少有一个歌手 Id 在曲库里不存在。
    /// </summary>
    private async Task<List<long>?> ResolveAlbumArtistIdsAsync(IReadOnlyList<long>? artistIds, long singleArtistId)
    {
        var ids = artistIds is { Count: > 0 }
            ? artistIds.Distinct().ToList()
            : [singleArtistId];

        var existing = await db.Artists.Where(a => ids.Contains(a.Id)).Select(a => a.Id).ToListAsync();
        if (existing.Count != ids.Count) return null;
        return ids;
    }

    /// <summary>读一张专辑的完整歌手列表（按 Position 排）。专辑不存在返回空列表。</summary>
    private async Task<IReadOnlyList<SongArtistRef>> LoadAlbumArtistsAsync(long albumId) =>
        await db.AlbumArtists.AsNoTracking()
            .Where(aa => aa.AlbumId == albumId)
            .OrderBy(aa => aa.Position)
            .Select(aa => new SongArtistRef(aa.ArtistId, aa.Artist.Name))
            .ToListAsync();

    public async Task<ServiceResult<bool>> DeleteAlbumAsync(long id)
    {
        var album = await db.Albums.FirstOrDefaultAsync(a => a.Id == id);
        if (album is null) return ServiceResult<bool>.Fail("专辑不存在。");

        var songCount = await db.Songs.CountAsync(s => s.AlbumId == id);
        if (songCount > 0)
            return ServiceResult<bool>.Fail($"该专辑下还有 {songCount} 首歌曲，无法删除。请先删除或迁移歌曲后再试。");

        db.Albums.Remove(album);
        await db.SaveChangesAsync();
        return ServiceResult<bool>.Ok(true);
    }
}