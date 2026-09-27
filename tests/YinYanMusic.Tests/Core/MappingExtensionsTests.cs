using YinYanMusic.Core;
using YinYanMusic.Core.Dtos;
using YinYanMusic.Core.Entities;

namespace YinYanMusic.Tests.Core;

public class MappingExtensionsTests
{
    [Fact]
    public void Category_ToDto_MapsAllFields()
    {
        var category = new Category
        {
            Id = 7,
            Name = "华语专区",
            Slogan = "经典华语",
            ColorHex = "#FF0000",
            IconGlyph = "\uE50A",
            ContentMode = CategoryContentModes.Songs
        };

        var dto = category.ToDto();

        Assert.Equal(7, dto.Id);
        Assert.Equal("华语专区", dto.Name);
        Assert.Equal("经典华语", dto.Slogan);
        Assert.Equal("#FF0000", dto.ColorHex);
        Assert.Equal("\uE50A", dto.IconGlyph);
        Assert.Equal(CategoryContentModes.Songs, dto.ContentMode);
    }

    [Fact]
    public void Artist_ToDto_IncludesStats()
    {
        var artist = new Artist
        {
            Id = 3,
            Name = "周杰伦",
            Region = "CN",
            Kind = "男歌手",
            AvatarUrl = "/img/a.jpg",
            Bio = "简介"
        };

        var dto = artist.ToDto(followerCount: 10, songCount: 20, albumCount: 5);

        Assert.Equal(3, dto.Id);
        Assert.Equal("周杰伦", dto.Name);
        Assert.Equal("CN", dto.Region);
        Assert.Equal("男歌手", dto.Kind);
        Assert.Equal("/img/a.jpg", dto.AvatarUrl);
        Assert.Equal("简介", dto.Bio);
        Assert.Equal(10, dto.FollowerCount);
        Assert.Equal(20, dto.SongCount);
        Assert.Equal(5, dto.AlbumCount);
    }

    [Fact]
    public void Album_ToDto_IncludesTrackCountAndNestedArtist()
    {
        var artist = new Artist { Id = 3, Name = "周杰伦" };
        var album = new Album
        {
            Id = 9,
            Name = "叶惠美",
            CoverUrl = "/img/album.jpg",
            ReleaseDate = new DateOnly(2003, 7, 31),
            Description = "专辑简介",
            Artist = artist
        };

        var dto = album.ToDto(trackCount: 10);

        Assert.Equal(9, dto.Id);
        Assert.Equal("叶惠美", dto.Name);
        Assert.Equal("/img/album.jpg", dto.CoverUrl);
        Assert.Equal(new DateOnly(2003, 7, 31), dto.ReleaseDate);
        Assert.Equal("专辑简介", dto.Description);
        Assert.Equal(10, dto.TrackCount);
        Assert.NotNull(dto.Artist);
        Assert.Equal(3, dto.Artist.Id);
        Assert.Equal("周杰伦", dto.Artist.Name);
    }

    [Fact]
    public void Song_ToDto_MapsBasicFields()
    {
        var artist = new Artist { Id = 3, Name = "周杰伦" };
        var song = new Song
        {
            Id = 1,
            Title = "晴天",
            ArtistId = 3,
            Artist = artist,
            AlbumId = 9,
            Album = new Album { Id = 9, Name = "叶惠美", CoverUrl = "/img/album.jpg" },
            CategoryId = 7,
            Category = new Category { Id = 7, Name = "华语专区" },
            CoverUrl = "/img/song.jpg",
            AudioUrl = "/media/audio/qingtian.mp3",
            LyricUrl = "/lyric/qingtian.lrc",
            DurationSeconds = 269,
            PlayCount = 100
        };

        var dto = song.ToDto();

        Assert.Equal(1, dto.Id);
        Assert.Equal("晴天", dto.Title);
        Assert.Equal(3, dto.ArtistId);
        Assert.Equal("周杰伦", dto.ArtistName);
        Assert.Equal(9, dto.AlbumId);
        Assert.Equal("叶惠美", dto.AlbumName);
        Assert.Equal(7, dto.CategoryId);
        Assert.Equal("华语专区", dto.CategoryName);
        Assert.Equal("/img/song.jpg", dto.CoverUrl);
        Assert.Equal("/media/audio/qingtian.mp3", dto.AudioUrl);
        Assert.Equal("/lyric/qingtian.lrc", dto.LyricUrl);
        Assert.Equal(269, dto.DurationSeconds);
        Assert.Equal(100, dto.PlayCount);
    }

    [Fact]
    public void Song_ToDto_CollaborationArtistsOrderedByPosition()
    {
        var primary = new Artist { Id = 1, Name = "Aimer" };
        var second = new Artist { Id = 2, Name = "EGOIST" };
        var song = new Song
        {
            Id = 5,
            Title = "ninelie",
            ArtistId = 1,
            Artist = primary,
            SongArtists =
            [
                new SongArtist { SongId = 5, ArtistId = 1, Artist = primary, Position = 0 },
                new SongArtist { SongId = 5, ArtistId = 2, Artist = second, Position = 1 }
            ]
        };

        var dto = song.ToDto();

        Assert.NotNull(dto.Artists);
        Assert.Equal(2, dto.Artists.Count);
        Assert.Equal(1, dto.Artists[0].Id);
        Assert.Equal("Aimer", dto.Artists[0].Name);
        Assert.Equal(2, dto.Artists[1].Id);
        Assert.Equal("EGOIST", dto.Artists[1].Name);
        Assert.Equal("Aimer / EGOIST", dto.ArtistsDisplay);
    }

    [Fact]
    public void Song_ToDto_FallsBackToPrimaryArtist_WhenNoSongArtists()
    {
        var song = new Song
        {
            Id = 2,
            Title = "七里香",
            ArtistId = 3,
            Artist = new Artist { Id = 3, Name = "周杰伦" }
        };

        var dto = song.ToDto();

        Assert.NotNull(dto.Artists);
        Assert.Single(dto.Artists);
        Assert.Equal(3, dto.Artists[0].Id);
        Assert.Equal("周杰伦", dto.Artists[0].Name);
        Assert.Equal("周杰伦", dto.ArtistsDisplay);
    }

    [Fact]
    public void Song_ToDto_CoverFallsBackToAlbumCover()
    {
        var song = new Song
        {
            Id = 3,
            Title = "以父之名",
            ArtistId = 3,
            Artist = new Artist { Id = 3, Name = "周杰伦" },
            CoverUrl = null,
            Album = new Album { Id = 9, CoverUrl = "/img/album-cover.jpg" }
        };

        var dto = song.ToDto();

        Assert.Equal("/img/album-cover.jpg", dto.CoverUrl);
    }
}