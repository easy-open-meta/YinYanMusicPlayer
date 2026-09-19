using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using YinYanMusic.Data;
using YinYanMusic.Scanner;

// 用法见 --help。默认读取 appsettings.json，可用 appsettings.{环境}.json 与环境变量覆盖。
var cli = CliOptions.Parse(args);
if (cli.ShowHelp || cli.HasError)
{
    CliOptions.PrintHelp();
    return cli.HasError ? 1 : 0;
}

var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .Build();

var musicDirectory = cli.MusicDirectory ?? config["Media:MusicDirectory"];
if (string.IsNullOrWhiteSpace(musicDirectory))
{
    Console.Error.WriteLine("未配置音乐目录：请设置 appsettings.json 的 Media:MusicDirectory，或用 --path 指定。");
    return 1;
}
if (!Directory.Exists(musicDirectory))
{
    Console.Error.WriteLine($"音乐目录不存在：{musicDirectory}");
    return 1;
}

var imageDirectory = cli.NoCover ? null : cli.ImageDirectory ?? config["Media:ImageDirectory"];
if (string.IsNullOrWhiteSpace(imageDirectory)) imageDirectory = null;

var connectionString = config.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("未配置 ConnectionStrings:Default（可写在 appsettings.json / appsettings.Development.json，或用环境变量 ConnectionStrings__Default）。");
    return 1;
}

var batchSize = int.TryParse(config["Scanner:BatchSize"], out var parsedBatch) && parsedBatch > 0 ? parsedBatch : 50;

Console.WriteLine("音言音乐 · 本地音乐导入");
Console.WriteLine($"  音乐目录 : {musicDirectory}");
Console.WriteLine($"  封面目录 : {imageDirectory ?? "(未配置，跳过封面抽取)"}");
Console.WriteLine($"  数据库   : {DescribeConnection(connectionString)}");
Console.WriteLine($"  模式     : {(cli.DryRun ? "预演（不写数据库、不写文件）" : "正式导入")}{(cli.Force ? " + 更新已存在" : string.Empty)}");
Console.WriteLine();

var dbOptions = new DbContextOptionsBuilder<MusicDbContext>().UseNpgsql(connectionString).Options;
await using var db = new MusicDbContext(dbOptions);

if (!cli.DryRun)
{
    var checkResult = await CheckDatabaseAsync(db);
    if (checkResult != 0) return checkResult;
}

var importer = new MusicImporter(
    db,
    new ImportSettings(imageDirectory, config["Media:BaseUrl"], cli.Force, cli.DryRun, batchSize));

ImportSummary summary;
try
{
    summary = await importer.RunAsync(musicDirectory);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"导入中断：{ex.Message}");
    return 1;
}

summary.Print();
if (cli.DryRun) Console.WriteLine("  （预演模式：未写入任何数据、未生成封面文件）");

return summary.Failed > 0 ? 1 : 0;

// 入库前先确认目标库里确实有当前模型的表，避免像手工 ALTER 那样在启动阶段直接抛异常。
static async Task<int> CheckDatabaseAsync(MusicDbContext db)
{
    try
    {
        if (!await db.Database.CanConnectAsync())
        {
            Console.Error.WriteLine("无法连接数据库，请检查 ConnectionStrings:Default。");
            return 1;
        }

        _ = await db.Songs.AnyAsync();
        return 0;
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
    {
        Console.Error.WriteLine("数据库里没有 Songs 表（42P01），已终止导入。");
        Console.Error.WriteLine("  1) 先用 Api 启动一次，让 EnsureCreated 按当前模型建表；");
        Console.Error.WriteLine("  2) 或把 ConnectionStrings:Default 指向一个全新的数据库名；");
        Console.Error.WriteLine("  3) 如果该库属于别的后端（表名是 snake_case 的 songs 等），不要直接复用。");
        return 2;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"数据库检查失败：{ex.Message}");
        return 1;
    }
}

// 只打印主机/库名/用户名，不回显密码。
static string DescribeConnection(string connectionString)
{
    try
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return $"{builder.Host}:{builder.Port}/{builder.Database} (user={builder.Username})";
    }
    catch
    {
        return "(连接串无法解析)";
    }
}
