using System.Globalization;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace QueryToCsv;

internal static class LogSetup
{
    internal const string FolderName = "logs";

    private const string DateStampFormat = "yyyyMMdd";

    // NLog resolves ${date} for every entry, so a new file begins each day.
    private const string FileNamePattern =
        ApplicationVersion.ApplicationName + "_${date:format=" + DateStampFormat + "}.log";

    private const string EntryLayout =
        "${longdate} [${level:uppercase=true:padding=-5}] ${message}${onexception:inner= ${exception:format=tostring}}";

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    internal static string PreferredDirectory => Path.Combine(AppContext.BaseDirectory, FolderName);

    internal static string FallbackDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        ApplicationVersion.ApplicationName,
        FolderName);

    private static string WritableDirectory => ResolveDirectory(PreferredDirectory, FallbackDirectory);

    internal static Logger Configure()
    {
        var fileTarget = new FileTarget("file")
        {
            FileName = Path.Combine(WritableDirectory, FileNamePattern),
            Layout = EntryLayout,
        };

        var config = new LoggingConfiguration();
        config.AddTarget(fileTarget);
        config.AddRule(LogLevel.Info, LogLevel.Fatal, fileTarget);

        LogManager.Configuration = config;
        return LogManager.GetCurrentClassLogger();
    }

    internal static void DeleteExpiredLogs(int retentionDays)
        => DeleteExpiredLogs(WritableDirectory, retentionDays, DateTime.Now);

    // NLog's own MaxArchiveDays deletes nothing when the date lives in FileName instead
    // of an archive pattern (measured: QueryToCsv_20250101.log survived a run with
    // MaxArchiveDays=30), so the retention limit is applied here
    // (docs/rules/dotnet.md, LOGGING).
    internal static void DeleteExpiredLogs(string directory, int retentionDays, DateTime now)
    {
        if (!Directory.Exists(directory))
            return;

        var oldestKept = now.Date.AddDays(1 - retentionDays);

        // The listing is materialized before the first delete
        // (docs/rules/standard.md, Snapshot Before Mutate).
        var expired = Directory.GetFiles(directory, $"{ApplicationVersion.ApplicationName}_*.log")
            .Where(path => IsExpired(path, oldestKept))
            .ToList();

        foreach (var path in expired)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn(ex, $"Failed to delete an expired log file: {path}");
            }
        }
    }

    // The directory beside the executable is not writable in every installation, and a
    // run that cannot log still has to run (docs/rules/dotnet.md, LOGGING).
    internal static string ResolveDirectory(string preferred, string fallback)
        => IsWritable(preferred) ? preferred : fallback;

    private static bool IsExpired(string path, DateTime oldestKept)
    {
        var stamp = Path.GetFileNameWithoutExtension(path)[(ApplicationVersion.ApplicationName.Length + 1)..];

        return DateTime.TryParseExact(
                   stamp, DateStampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
               && date < oldestKept;
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);

            var probe = Path.Combine(directory, $"{Guid.NewGuid():N}.probe");
            using (File.Create(probe))
            {
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return false;
        }
    }
}
