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
    Task<PagedResult<ArtistDto>> GetArtistsAsync(string? keyword, int page, int pageSize);
    Task<ArtistDto?> GetArtistAsync(long id);
    Task<ArtistDto> CreateArtistAsync(CreateArtistRequest req);
    Task<PagedResult<AlbumDto>> GetAlbumsAsync(long? artistId, string? keyword, int page, int pageSize);
    Task<AlbumDto?> GetAlbumAsync(long id);
    Task<ServiceResult<AlbumDto>> CreateAlbumAsync(CreateAlbumRequest req);
}

public class CatalogService(MusicDbContext db) : ICatalogService
{
    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync() =>
        await db.Categories.OrderBy(c => c.Id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slogan, c.ColorHex, c.IconGlyph))
            .ToListAsync();

    public async Task<ServiceResult<CategoryDto>> CreateCategoryAsync(CategoryDto req)
    {
        var name = req.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == name))
            return ServiceResult<CategoryDto>.Fail("分类已存在。");
        var cat = new Category { Name = name, Slogan = req.Slogan, ColorHex = req.ColorHex, IconGlyph = req.IconGlyph };
        db.Categories.Add(cat);
        await db.SaveChangesAsync();
        return ServiceResult<CategoryDto>.Ok(cat.ToDto());
    }

    public async Task<PagedResult<ArtistDto>> GetArtistsAsync(string? keyword, int page, int pageSize)
    {
        var q = db.Artists.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(a => a.Name.Contains(keyword));
        var total = await q.CountAsync();
        var items = await q.OrderBy(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => a.ToDto())
            .ToListAsync();
        return new PagedResult<ArtistDto>(items, total, page, pageSize);
    }

    public async Task<ArtistDto?> GetArtistAsync(long id)
    {
        var artist = await db.Artists.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (artist is null) return null;
        var songCount = await db.Songs.CountAsync(s => s.ArtistId == id);
        var albumCount = await db.Albums.CountAsync(a => a.ArtistId == id);
        var followerCount = await db.ArtistFollows.CountAsync(f => f.ArtistId == id);
        return artist.ToDto(followerCount, songCount, albumCount);
    }

    public async Task<ArtistDto> CreateArtistAsync(CreateArtistRequest req)
    {
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
        return artist.ToDto();
    }

    public async Task<PagedResult<AlbumDto>> GetAlbumsAsync(long? artistId, string? keyword, int page, int pageSize)
    {
        var q = db.Albums.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(a => a.Name.Contains(keyword));
        if (artistId.HasValue) q = q.Where(a => a.ArtistId == artistId);
        var total = await q.CountAsync();
        var items = await q.OrderBy(a => a.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AlbumDto(a.Id, a.Name, a.CoverUrl, a.ReleaseDate, a.Description,
                new ArtistDto(a.Artist.Id, a.Artist.Name, a.Artist.Region, a.Artist.Kind, a.Artist.AvatarUrl, a.Artist.Bio, 0, 0, 0)))
            .ToListAsync();
        return new PagedResult<AlbumDto>(items, total, page, pageSize);
    }

    public async Task<AlbumDto?> GetAlbumAsync(long id)
    {
        var album = await db.Albums.AsNoTracking().Include(a => a.Artist).FirstOrDefaultAsync(a => a.Id == id);
        return album?.ToDto();
    }

    public async Task<ServiceResult<AlbumDto>> CreateAlbumAsync(CreateAlbumRequest req)
    {
        if (!await db.Artists.AnyAsync(a => a.Id == req.ArtistId))
            return ServiceResult<AlbumDto>.Fail("歌手不存在。");
        var album = new Album
        {
            ArtistId = req.ArtistId,
            Name = req.Name.Trim(),
            ReleaseDate = req.ReleaseDate,
            Description = req.Description,
            CoverUrl = req.CoverUrl
        };
        db.Albums.Add(album);
        await db.SaveChangesAsync();
        await db.Entry(album).Reference(a => a.Artist).LoadAsync();
        return ServiceResult<AlbumDto>.Ok(album.ToDto());
    }
}