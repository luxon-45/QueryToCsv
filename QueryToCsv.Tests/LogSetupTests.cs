using System.Globalization;
using Xunit;

namespace QueryToCsv.Tests;

public class LogSetupTests
{
    private static readonly DateTime Now = new(2026, 3, 2, 15, 30, 45, DateTimeKind.Unspecified);

    private const int RetentionDays = 30;

    [Fact]
    public void ResolveDirectory_PreferredDirectoryWritable_KeepsItAndLeavesNoProbeBehind()
    {
        using var dir = new TempDirectory();
        var preferred = Path.Combine(dir.Path, "logs");
        var fallback = Path.Combine(dir.Path, "appdata-logs");

        var resolved = LogSetup.ResolveDirectory(preferred, fallback);

        Assert.Equal(preferred, resolved);
        Assert.Empty(Directory.GetFileSystemEntries(preferred));
    }

    [Fact]
    public void ResolveDirectory_PreferredDirectoryUnusable_UsesTheFallback()
    {
        using var dir = new TempDirectory();

        // A file where the directory would go: creating the directory fails, which is
        // what an application folder the installation cannot write to looks like here.
        var blocker = Path.Combine(dir.Path, "blocked");
        File.WriteAllText(blocker, "");

        var preferred = Path.Combine(blocker, "logs");
        var fallback = Path.Combine(dir.Path, "appdata-logs");

        Assert.Equal(fallback, LogSetup.ResolveDirectory(preferred, fallback));
    }

    [Theory]
    // Days between the file's own date and today -> the file survives the sweep
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(29, true)]
    [InlineData(30, false)]
    [InlineData(365, false)]
    public void DeleteExpiredLogs_DailyFile_KeepsTheMostRecentRetentionDays(int daysOld, bool kept)
    {
        using var dir = new TempDirectory();
        // The stamp the application writes is invariant-culture, whatever the machine runs
        var stamp = Now.Date.AddDays(-daysOld).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var name = $"QueryToCsv_{stamp}.log";
        File.WriteAllText(Path.Combine(dir.Path, name), "");

        LogSetup.DeleteExpiredLogs(dir.Path, RetentionDays, Now);

        Assert.Equal(kept, File.Exists(Path.Combine(dir.Path, name)));
    }

    [Theory]
    // Only this application's own dated log files are the sweep's to remove
    [InlineData("OtherApp_20250101.log")]
    [InlineData("QueryToCsv_20250101.txt")]
    [InlineData("QueryToCsv_notadate.log")]
    [InlineData("QueryToCsv.log")]
    [InlineData("notes.txt")]
    public void DeleteExpiredLogs_FileOutsideTheNamePattern_IsLeftAlone(string name)
    {
        using var dir = new TempDirectory();
        File.WriteAllText(Path.Combine(dir.Path, name), "");

        LogSetup.DeleteExpiredLogs(dir.Path, RetentionDays, Now);

        Assert.True(File.Exists(Path.Combine(dir.Path, name)));
    }

    [Fact]
    public void DeleteExpiredLogs_MissingDirectory_DoesNothing()
    {
        using var dir = new TempDirectory();

        LogSetup.DeleteExpiredLogs(Path.Combine(dir.Path, "not-created-yet"), RetentionDays, Now);
    }
}
