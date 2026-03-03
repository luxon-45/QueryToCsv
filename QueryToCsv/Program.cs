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

var (runArgs, parseExitCode) = ParseRunArgs(args);
if (parseExitCode is not null)
    return parseExitCode.Value;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var logger = ConfigureNLog(30);
var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
logger.Info($"Application started (v{version})");

try
{
    if (runArgs is null)
    {
        Console.WriteLine("=== QueryToCsv ===");
        Console.WriteLine();
    }

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

    if (runArgs is not null)
    {
        var result = RunOneLiner(settings, runArgs, logger);
        if (result == 0)
            logger.Info("Application finished (exit code: 0)");
        else
            logger.Error($"Application finished (exit code: {result})");
        return result;
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

static (CliRunArgs? RunArgs, int? ExitCode) ParseRunArgs(string[] args)
{
    if (args.Length == 0)
        return (null, null);

    string? connectionName = null;
    string? inlineQuery = null;
    string? sqlFile = null;
    string encodingName = "utf-8";
    var includeHeader = true;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-c" or "--connection":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"Error: {args[i]} requires a value.");
                    return (null, 1);
                }
                connectionName = args[++i];
                break;
            case "-q" or "--query":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"Error: {args[i]} requires a value.");
                    return (null, 1);
                }
                inlineQuery = args[++i];
                break;
            case "-f" or "--file":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"Error: {args[i]} requires a value.");
                    return (null, 1);
                }
                sqlFile = args[++i];
                break;
            case "-e" or "--encoding":
                if (i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"Error: {args[i]} requires a value.");
                    return (null, 1);
                }
                encodingName = args[++i];
                break;
            case "--header":
                includeHeader = true;
                break;
            case "--no-header":
                includeHeader = false;
                break;
            default:
                Console.Error.WriteLine($"Error: Unknown option: {args[i]}");
                return (null, 1);
        }
    }

    if (inlineQuery is not null && sqlFile is not null)
    {
        Console.Error.WriteLine("Error: -q and -f cannot be used together.");
        return (null, 1);
    }

    if (inlineQuery is null && sqlFile is null)
    {
        Console.Error.WriteLine("Error: -q or -f is required when using CLI options.");
        return (null, 1);
    }

    return (new CliRunArgs(connectionName, inlineQuery, sqlFile, encodingName, includeHeader), null);
}

static int RunOneLiner(AppSettings settings, CliRunArgs runArgs, Logger logger)
{
    // Resolve connection
    string connectionString;
    string connectionName;

    if (runArgs.ConnectionName is not null)
    {
        var entry = settings.Connections.FirstOrDefault(c =>
            c.Name.Equals(runArgs.ConnectionName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            Console.Error.WriteLine($"Error: Connection \"{runArgs.ConnectionName}\" not found.");
            return 1;
        }
        connectionString = entry.ConnectionString;
        connectionName = entry.Name;
    }
    else if (settings.Connections.Count == 1)
    {
        connectionString = settings.Connections[0].ConnectionString;
        connectionName = settings.Connections[0].Name;
    }
    else
    {
        Console.Error.WriteLine("Error: -c is required when multiple connections are configured.");
        return 1;
    }

    logger.Info($"Connection selected: {connectionName}");

    // Resolve query
    string sql;
    string? baseName;

    if (runArgs.InlineQuery is not null)
    {
        sql = runArgs.InlineQuery;
        baseName = null;
        logger.Info("Query selected: [Inline]");
    }
    else
    {
        var filePath = runArgs.SqlFile!;

        if (!Path.IsPathRooted(filePath))
            filePath = Path.Combine(settings.QueryFolder, filePath);

        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: SQL file not found: {filePath}");
            return 1;
        }

        var sqlEncoding = Encoding.GetEncoding(settings.SqlFileEncoding);
        sql = File.ReadAllText(filePath, sqlEncoding);
        baseName = Path.GetFileNameWithoutExtension(filePath);
        logger.Info($"Query selected: {Path.GetFileName(filePath)}");
    }

    // Resolve encoding
    var csvEncoding = ConsoleUi.ResolveEncoding(runArgs.EncodingName);
    if (csvEncoding is null)
    {
        Console.Error.WriteLine($"Error: Unknown encoding \"{runArgs.EncodingName}\". Use: utf-8, utf-8-bom, utf-16, shift-jis");
        return 1;
    }

    logger.Info($"Header: {(runArgs.IncludeHeader ? "yes" : "no")}, Encoding: {csvEncoding.EncodingName}");

    return QueryExecutor.Execute(settings, connectionString, sql, baseName, runArgs.IncludeHeader, csvEncoding);
}

static int PrintHelp()
{
    var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    Console.WriteLine($"QueryToCsv v{version}");
    Console.WriteLine();
    Console.WriteLine("USAGE");
    Console.WriteLine("  QueryToCsv                      Run interactively");
    Console.WriteLine("  QueryToCsv -q <sql> [options]   Run a query and exit");
    Console.WriteLine("  QueryToCsv -f <file> [options]  Run a SQL file and exit");
    Console.WriteLine("  QueryToCsv --open <target>      Open a folder or file and exit");
    Console.WriteLine("  QueryToCsv -h | --help          Show this help");
    Console.WriteLine();
    Console.WriteLine("OPTIONS");
    Console.WriteLine("  -c, --connection <name>   Connection name from appsettings.json");
    Console.WriteLine("                            (required if multiple connections exist)");
    Console.WriteLine("  -q, --query <sql>         Inline SQL query string");
    Console.WriteLine("  -f, --file <name|path>    SQL file in QueryFolder, or absolute path");
    Console.WriteLine("  -e, --encoding <name>     CSV encoding: utf-8 (default), utf-8-bom,");
    Console.WriteLine("                            utf-16, shift-jis");
    Console.WriteLine("      --header              Include header row (default)");
    Console.WriteLine("      --no-header           Exclude header row");
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

record CliRunArgs(string? ConnectionName, string? InlineQuery, string? SqlFile, string EncodingName, bool IncludeHeader);
