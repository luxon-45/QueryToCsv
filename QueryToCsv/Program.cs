using System.Reflection;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;
using QueryToCsv;

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

    if (sqlFiles.Length == 0)
    {
        Console.Error.WriteLine("No query files found.");
        logger.Error("Application finished (exit code: 1)");
        return 1;
    }

    var fileNames = sqlFiles.Select(Path.GetFileName).ToArray()!;
    var selectedIndex = ConsoleUi.SelectQuery(fileNames!);
    logger.Info($"Query selected: {fileNames[selectedIndex]}");
    Console.WriteLine();

    var includeHeader = ConsoleUi.AskIncludeHeader();
    Console.WriteLine();

    var csvEncoding = ConsoleUi.SelectEncoding();
    logger.Info($"Header: {(includeHeader ? "yes" : "no")}, Encoding: {csvEncoding.EncodingName}");
    Console.WriteLine();

    var exitCode = QueryExecutor.Execute(settings, sqlFiles[selectedIndex], includeHeader, csvEncoding);

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
