namespace YinYanMusic.Scanner;

/// <summary>
/// 命令行参数。用法：
///   YinYanMusic.Scanner [--path &lt;目录&gt;] [--dry-run] [--force] [--image-dir &lt;目录&gt;] [--no-cover]
/// 不传参数时使用 appsettings.json 里的 Media:MusicDirectory 与 Media:ImageDirectory。
/// </summary>
public sealed class CliOptions
{
    public string? MusicDirectory { get; private set; }
    public string? ImageDirectory { get; private set; }
    public bool DryRun { get; private set; }
    public bool Force { get; private set; }
    public bool NoCover { get; private set; }
    public bool ShowHelp { get; private set; }

    /// <summary>参数无法识别或缺值时置位，用于返回非零退出码。</summary>
    public bool HasError { get; private set; }

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--path" or "-p":
                    options.MusicDirectory = NextValue(args, ref i, arg);
                    if (options.MusicDirectory is null) options.HasError = true;
                    break;
                case "--image-dir":
                    options.ImageDirectory = NextValue(args, ref i, arg);
                    if (options.ImageDirectory is null) options.HasError = true;
                    break;
                case "--dry-run" or "-n":
                    options.DryRun = true;
                    break;
                case "--force" or "-f":
                    options.Force = true;
                    break;
                case "--no-cover":
                    options.NoCover = true;
                    break;
                case "--help" or "-h" or "/?":
                    options.ShowHelp = true;
                    break;
                default:
                    Console.Error.WriteLine($"无法识别的参数：{arg}（用 --help 查看用法）");
                    options.ShowHelp = true;
                    options.HasError = true;
                    break;
            }
        }
        return options;
    }

    private static string? NextValue(string[] args, ref int index, string name)
    {
        if (index + 1 >= args.Length)
        {
            Console.Error.WriteLine($"参数 {name} 缺少值");
            return null;
        }
        return args[++index];
    }

    public static void PrintHelp()
    {
        Console.WriteLine("""
            音言音乐 · 本地音乐导入工具

            用法：
              YinYanMusic.Scanner [选项]

            选项：
              -p, --path <目录>       要扫描的音乐目录（默认取 appsettings.json 的 Media:MusicDirectory）
                  --image-dir <目录>  封面输出目录（默认取 Media:ImageDirectory；留空则不抽封面）
                  --no-cover          跳过封面抽取
              -n, --dry-run           预演：只解析并打印，不写数据库、不写封面文件
              -f, --force             已存在的歌曲（按 AudioUrl 判断）重新解析并更新元数据
              -h, --help              显示帮助

            说明：
              · 只索引文件，不移动/复制音频；音频通过 Api 的 /media/audio 路由对外提供
                （Api 已把 Media:MusicDirectory 挂到该路由，Program.cs 中配置）。
              · 音频 URL 写为 /media/audio/<相对路径>，App 端 ApiConfig.Absolute() 会自动补前缀。
              · 连接串取 appsettings.json / appsettings.Development.json 的 ConnectionStrings:Default，
                也可用环境变量 ConnectionStrings__Default 覆盖。
            """);
    }
}
