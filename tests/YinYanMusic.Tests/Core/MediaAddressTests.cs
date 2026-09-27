using YinYanMusic.Core;

namespace YinYanMusic.Tests.Core;

/// <summary>
/// 媒体地址形态判定（V2.13 技术债清理的核心逻辑）。
///
/// <para>这块逻辑被抽到 <see cref="YinYanMusic.Core.MediaAddress"/> 就是为了能这样测 ——
/// 它先前散在 <c>ApiConfig</c> 与 <c>ImageSourceFactory</c> 两处、还各自漏了一种形态，
/// 而两处都带 MAUI 依赖、单测碰不到，只能靠真机回归，于是同类缺陷反复出现。
/// 现在"什么是本地文件"只有一份实现，且没有平台依赖。</para>
///
/// <para>覆盖到 TC-2.13-03（同名假路径不误判）、TC-2.13-04（本地文件不存在）、
/// TC-2.13-05（Windows 盘符路径，含真实 <c>Uri</c> 往返）。
/// 剩下的 TC-2.13-01/02/06/07 涉及真实 UI 与设备，仍按设计文档走真机回归。</para>
/// </summary>
public class MediaAddressTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _tempFile;

    public MediaAddressTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "yinyan-media-address-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _tempFile = Path.Combine(_tempDir, "cover.jpg");
        File.WriteAllBytes(_tempFile, [0xFF, 0xD8, 0xFF, 0xE0]);
    }

    // ── TC-2.13-03：后端相对路径永远不能被判成本地文件 ───────────────────────

    /// <summary>
    /// 这是 V2.13 最要紧的一条：后端的封面/音频都是 <c>/media/...</c> 这种"以斜杠开头"的相对路径。
    /// 只要它们被判成本地文件，App 就会去 <c>File.Exists("/media/image/x.jpg")</c>，
    /// 所有在线封面会一起挂掉 —— 所以这里是**前缀短路**，不依赖磁盘上有没有同名文件。
    /// </summary>
    [Theory]
    [InlineData("/media/image/song-8.jpg")]
    [InlineData("/media/audio/Ado - 向日葵.MP3")]
    [InlineData("/media/lyric/x.lrc")]
    [InlineData("/api/media/image/x.jpg")]
    public void ServerRelativeMediaPath_IsNeverLocal(string raw)
    {
        Assert.False(MediaAddress.IsLocalFile(raw));
    }

    [Theory]
    [InlineData("http://localhost:5116/media/image/x.jpg")]
    [InlineData("https://yinyan.oscode.top/api/media/image/x.jpg")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("api/songs?page=1&pageSize=1")]
    [InlineData("api/media/audio/x.mp3")]
    public void RemoteOrInlineOrApiPath_IsNotLocal(string raw)
    {
        Assert.False(MediaAddress.IsLocalFile(raw));
    }

    // ── TC-2.13-05：Windows 盘符路径 ────────────────────────────────────────

    [Theory]
    [InlineData(@"C:\Music\cover.jpg")]
    [InlineData("C:/Music/cover.jpg")]
    [InlineData(@"E:\CloudMusic\Batch4\Ado - 向日葵.MP3")]
    [InlineData("C:")]                       // 盘符相对路径，沿用旧实现的判定，保持行为不变
    public void WindowsDrivePath_IsLocal(string raw)
    {
        Assert.True(MediaAddress.IsLocalFile(raw));
    }

    /// <summary>
    /// 盘符判定**不看文件是否存在**（存在性只有裸 Unix 路径那条分支才需要），
    /// 所以指向已删文件的本地记录仍会被正确判成本地、不会去拼 BaseUrl（TC-2.13-04 的前半段）。
    /// 后半段"读取失败走失效提示、不崩"由 <c>ImageSourceFactory.ReadBytesAsync</c> 的 try/catch 兜底，
    /// 属 MAUI 侧，靠真机回归。
    /// </summary>
    [Fact]
    public void WindowsDrivePath_ToMissingFile_IsStillLocal()
    {
        Assert.True(MediaAddress.IsLocalFile(@"C:\definitely\missing\cover.jpg"));
    }

    // ── 本地协议与真实文件 ──────────────────────────────────────────────────

    [Theory]
    [InlineData("file:///data/user/0/com.yinyan.music/files/local-covers/x.jpg")]
    [InlineData("file://data/user/0/com.yinyan.music/files/local-covers/x.jpg")]
    // 单斜杠的 file URI 同样合法（RFC 8089 的空 authority 写法）。
    // 判定范围必须与 V2.13 之前 ApiConfig 白名单的 "file:" 前缀一致，否则会退回"被拼 BaseUrl"。
    [InlineData("file:/C:/Music/cover.jpg")]
    [InlineData("content://media/external/audio/media/123")]
    [InlineData("content:media/external/audio/media/123")]
    public void LocalProtocols_AreLocal(string raw)
    {
        Assert.True(MediaAddress.IsLocalFile(raw));
    }

    /// <summary>
    /// 真实存在的裸 Unix 绝对路径要判成本地 —— Android 本地曲库的封面就是这种形态
    /// （<c>FileSystem.AppDataDirectory</c> 返回裸绝对路径），也正是 V2.6 那次真机缺陷的触发条件。
    /// </summary>
    [Fact]
    public void ExistingBareAbsolutePath_IsLocal()
    {
        // 关键前提：探针文件必须与进程当前目录**同盘**。
        // Windows 把 "/xxx" 解析成"当前盘根 + xxx"，跨盘就找不到文件，
        // 那时测的是 Windows 的路径语义，而不是被测的那一行代码。
        var root = Path.GetPathRoot(Directory.GetCurrentDirectory())!;
        var probe = Path.Combine(Directory.GetCurrentDirectory(), "media-address-probe.jpg");
        File.WriteAllBytes(probe, [0xFF, 0xD8]);
        try
        {
            var bareUnix = "/" + Path.GetRelativePath(root, probe).Replace('\\', '/');

            Assert.True(File.Exists(bareUnix), $"前置条件不成立：{bareUnix} 应当存在");
            Assert.True(MediaAddress.IsLocalFile(bareUnix));
        }
        finally
        {
            try { File.Delete(probe); } catch { /* 清理失败不影响用例结论 */ }
        }
    }

    /// <summary>不存在的裸 Unix 绝对路径**不能**判成本地 —— 否则会把后端相对路径一并误判（TC-2.13-04）。</summary>
    [Fact]
    public void MissingBareAbsolutePath_IsNotLocal()
    {
        Assert.False(MediaAddress.IsLocalFile("/data/user/0/com.yinyan.music/files/local-covers/missing.jpg"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_IsNotLocal(string? raw)
    {
        Assert.False(MediaAddress.IsLocalFile(raw));
    }

    // ── LocalPathOf：Windows 与 Unix 的差别全在那一个斜杠上 ──────────────────

    [Theory]
    // Windows：三斜杠 + 盘符（new Uri(path).AbsoluteUri 的产物）—— 开头多出的斜杠必须去掉。
    // 旧实现只做"以 / 开头就保留"，于是这里会返回 "/C:/..."，交给 File.ReadAllBytes 必然取不到文件。
    [InlineData("file:///C:/Music/cover.jpg", "C:/Music/cover.jpg")]
    // Windows：两斜杠 + 盘符
    [InlineData("file://C:/Music/cover.jpg", "C:/Music/cover.jpg")]
    // Windows：单斜杠（空 authority 的合法写法）—— 按固定 7 位切字符串会在这里吃掉盘符冒号
    [InlineData("file:/C:/Music/cover.jpg", "C:/Music/cover.jpg")]
    // 非标准的 file:C:/x.jpg：同样容错
    [InlineData("file:C:/Music/cover.jpg", "C:/Music/cover.jpg")]
    // Unix：开头斜杠就是根目录，必须留
    [InlineData("file:///data/user/0/com.yinyan.music/files/x.jpg", "/data/user/0/com.yinyan.music/files/x.jpg")]
    [InlineData("file:/data/user/0/x.jpg", "/data/user/0/x.jpg")]
    // 畸形写法（两斜杠 + 无盘符 / 无斜杠）：沿用旧实现的"补回根目录"
    [InlineData("file://data/user/0/x.jpg", "/data/user/0/x.jpg")]
    [InlineData("file:data/user/0/x.jpg", "/data/user/0/x.jpg")]
    // 非 file: 一律原样返回
    [InlineData("content://media/external/audio/media/123", "content://media/external/audio/media/123")]
    [InlineData(@"C:\Music\cover.jpg", @"C:\Music\cover.jpg")]
    public void LocalPathOf_StripsOnlyFileScheme(string raw, string expected)
    {
        Assert.Equal(expected, MediaAddress.LocalPathOf(raw));
    }

    /// <summary>
    /// 端到端往返：真实文件 → <c>Uri</c>（App 里 <c>LocalFileAccess.ToPlayableUri</c> 就是这么做的）
    /// → <see cref="MediaAddress.LocalPathOf"/> → 必须仍能 <c>File.Exists</c> 到。
    /// 这条是 TC-2.13-05 在 Windows 上能真正跑到的部分，也是"旧实现的斜杠缺陷"的证据。
    /// </summary>
    [Fact]
    public void FileUriRoundTrip_StaysReadableOnDisk()
    {
        var uri = new Uri(_tempFile);
        Assert.StartsWith("file:///", uri.AbsoluteUri);          // 三斜杠形态，正是出问题的那种

        var back = MediaAddress.LocalPathOf(uri.AbsoluteUri);

        Assert.False(back.StartsWith('/'), $"不该带前导斜杠：{back}");
        Assert.True(File.Exists(back), $"{back} 应当能在磁盘上找到");
        Assert.Equal(File.ReadAllBytes(_tempFile), File.ReadAllBytes(back));
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* 清理失败不影响用例结论 */ }
    }
}
