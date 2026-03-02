using System.Diagnostics;
using System.Reflection;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;
using QueryToCsv;

// --open <target> : フォルダ/ファイルを開いて即終了
if (args.Length >= 2 && args[0] == "--open")
    return HandleOpen(args[1]);

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var logger = ConfigureNLog(30);
var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
logger.Info($"Application started (v{version})");

try
{
    Console.WriteLine("=== QueryToCsv ===");
    Console.WriteLine();

    var settings = AppSettings.Load();
    if (settings is null)
    {
        logger.Error("Application finished (exit code: 1)");
        return 1;
    }

    logger = ConfigureNLog(settings.LogRetentionDays);
    logger.Info("Settings loaded");

    if (!settings.Validate())
    {
        logger.Error("Application finished (exit code: 1)");
        return 1;
    }

    var sqlFiles = Directory.GetFiles(settings.QueryFolder, "*.sql");
    Array.Sort(sqlFiles, (a, b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));

    var fileNames = sqlFiles.Select(Path.GetFileName).ToArray()!;
    var selectedIndex = ConsoleUi.SelectQuery(fileNames!);

    string sql;
    string? baseName;

    if (selectedIndex == -1)
    {
        Console.WriteLine();
        sql = ConsoleUi.InputQuery();
        baseName = null;
        logger.Info("Query selected: [Direct Input]");
    }
    else
    {
        var sqlFilePath = sqlFiles[selectedIndex];
        var sqlEncoding = Encoding.GetEncoding(settings.SqlFileEncoding);
        sql = File.ReadAllText(sqlFilePath, sqlEncoding);
        baseName = Path.GetFileNameWithoutExtension(sqlFilePath);
        logger.Info($"Query selected: {fileNames[selectedIndex]}");
    }
    Console.WriteLine();

    var includeHeader = ConsoleUi.AskIncludeHeader();
    Console.WriteLine();

    var csvEncoding = ConsoleUi.SelectEncoding();
    logger.Info($"Header: {(includeHeader ? "yes" : "no")}, Encoding: {csvEncoding.EncodingName}");
    Console.WriteLine();

    var exitCode = QueryExecutor.Execute(settings, sql, baseName, includeHeader, csvEncoding);

    if (exitCode == 0)
        logger.Info("Application finished (exit code: 0)");
    else
        logger.Error($"Application finished (exit code: {exitCode})");

    return exitCode;
}
catch (Exception ex)
{
    logger.Error(ex, ex.Message);
    logger.Error("Application finished (exit code: 1)");
    return 1;
}
finally
{
    LogManager.Shutdown();
}

static int HandleOpen(string target)
{
    var baseDir = AppContext.BaseDirectory;

    string path;
    bool isFile;

    switch (target.ToLowerInvariant())
    {
        case "queries":
        case "output":
        {
            var settings = AppSettings.Load();
            if (settings is null)
                return 1;

            path = target.ToLowerInvariant() == "queries"
                ? settings.QueryFolder
                : settings.OutputFolder;
            isFile = false;
            break;
        }
        case "config":
            path = Path.Combine(baseDir, "appsettings.json");
            isFile = true;
            break;
        case "log":
            path = Path.Combine(baseDir, "logs");
            isFile = false;
            break;
        default:
            Console.Error.WriteLine($"Error: Unknown target \"{target}\". Use: queries, output, config, log");
            return 1;
    }

    if (isFile)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Error: File not found: {path}");
            return 1;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
    else
    {
        if (!Directory.Exists(path))
        {
            Console.Error.WriteLine($"Error: Folder not found: {path}");
            return 1;
        }
        Process.Start("explorer.exe", path);
    }

    return 0;
}

static Logger ConfigureNLog(int maxArchiveDays)
{
    var logDir = Path.Combine(AppContext.BaseDirectory, "logs");

    var config = new LoggingConfiguration();

    var fileTarget = new FileTarget("file")
    {
        FileName = Path.Combine(logDir, "QueryToCsv.log"),
        ArchiveEvery = FileArchivePeriod.Day,
        ArchiveFileName = Path.Combine(logDir, "QueryToCsv.{#}.log"),
        ArchiveNumbering = ArchiveNumberingMode.Date,
        ArchiveDateFormat = "yyyyMMdd",
        MaxArchiveDays = maxArchiveDays,
        Layout = "${longdate} [${level:uppercase=true:padding=-5}] ${message}${onexception:inner= ${exception:format=tostring}}",
    };

    config.AddTarget(fileTarget);
    config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);

    LogManager.Configuration = config;
    return LogManager.GetCurrentClassLogger();
}
