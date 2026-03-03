using System.Diagnostics;
using System.Reflection;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;
using QueryToCsv;

if (args.Length >= 1 && (args[0] == "-h" || args[0] == "--help"))
    return PrintHelp();

// --open <target>: open a folder or file and exit
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

    var connectionIndex = ConsoleUi.SelectConnection(settings.Connections);
    var connectionString = settings.Connections[connectionIndex].ConnectionString;
    logger.Info($"Connection selected: {settings.Connections[connectionIndex].Name}");
    Console.WriteLine();

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

    var exitCode = QueryExecutor.Execute(settings, connectionString, sql, baseName, includeHeader, csvEncoding);

    if (exitCode == 0)
        logger.Info("Application finished (exit code: 0)");
    else
        logger.Error($"Application finished (exit code: {exitCode})");

    return exitCode;
}
catch (Exception ex)
{
    logger.Error(ex, "Unhandled exception");
    logger.Error("Application finished (exit code: 1)");
    return 1;
}
finally
{
    LogManager.Shutdown();
}

static int PrintHelp()
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    Console.WriteLine($"QueryToCsv v{version}");
    Console.WriteLine();
    Console.WriteLine("USAGE");
    Console.WriteLine("  QueryToCsv                      Run interactively");
    Console.WriteLine("  QueryToCsv --open <target>      Open a folder or file and exit");
    Console.WriteLine("  QueryToCsv -h | --help          Show this help");
    Console.WriteLine();
    Console.WriteLine("--open TARGETS");
    Console.WriteLine("  queries       Open the queries folder in Explorer");
    Console.WriteLine("  output        Open the output folder in Explorer");
    Console.WriteLine("  config        Open appsettings.json in default editor");
    Console.WriteLine("  log           Open the logs folder in Explorer");
    Console.WriteLine("  <file path>   Open a specific file with its default app");
    Console.WriteLine();
    Console.WriteLine("CANCELLING");
    Console.WriteLine("  Ctrl+C        Exit at any time");
    Console.WriteLine("  Ctrl+Z+Enter  Exit at any input prompt");
    return 0;
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

            if (string.IsNullOrWhiteSpace(path))
            {
                var key = target.ToLowerInvariant() == "queries" ? "QueryFolder" : "OutputFolder";
                Console.Error.WriteLine($"Error: {key} is not configured in appsettings.json.");
                return 1;
            }

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
            path = target;
            isFile = true;
            break;
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
            var msg = target.ToLowerInvariant() == "output"
                ? "Error: Output folder does not exist yet. Run a query first to create it."
                : $"Error: Folder not found: {path}";
            Console.Error.WriteLine(msg);
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
