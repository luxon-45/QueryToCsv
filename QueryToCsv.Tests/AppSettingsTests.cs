using Microsoft.Extensions.Configuration;
using Xunit;

namespace QueryToCsv.Tests;

public class AppSettingsTests
{
    // The executable's directory stands in for a real installation; {base} in an
    // expected message is replaced with it.
    private const string BaseDirectoryPlaceholder = "{base}";

    // key, configured value -> the single notice reported
    public static TheoryData<string, string, string> RejectedValues() => new()
    {
        { "QueryFolder", "", "QueryFolder is blank; using \"{base}\\queries\"." },
        { "QueryFolder", "   ", "QueryFolder is blank; using \"{base}\\queries\"." },
        {
            "QueryFolder", "not-there",
            "QueryFolder \"{base}\\not-there\" does not name an existing folder; using \"{base}\\queries\"."
        },
        { "OutputFolder", "", "OutputFolder is blank; using \"{base}\\output\"." },
        { "QueryTimeout", "0", "QueryTimeout \"0\" is not a whole number greater than 0; using 30." },
        { "QueryTimeout", "-1", "QueryTimeout \"-1\" is not a whole number greater than 0; using 30." },
        { "QueryTimeout", "abc", "QueryTimeout \"abc\" is not a whole number greater than 0; using 30." },
        { "LogRetentionDays", "0", "LogRetentionDays \"0\" is not a whole number greater than 0; using 30." },
        { "LogRetentionDays", "x", "LogRetentionDays \"x\" is not a whole number greater than 0; using 30." },
        {
            "SqlFileEncoding", "not-an-encoding",
            "SqlFileEncoding \"not-an-encoding\" is not an encoding the runtime recognizes; using \"UTF-8\"."
        },
        { "CsvSettings:Delimiter", "", "CsvSettings.Delimiter \"\" is not exactly one character; using \",\"." },
        { "CsvSettings:Delimiter", ",,", "CsvSettings.Delimiter \",,\" is not exactly one character; using \",\"." },
        { "CsvSettings:Delimiter", "\\t", "CsvSettings.Delimiter \"\\t\" is not exactly one character; using \",\"." },
        { "CsvSettings:NewLine", "", "CsvSettings.NewLine \"\" is not \"CRLF\" or \"LF\"; using \"CRLF\"." },
        { "CsvSettings:NewLine", "crlf", "CsvSettings.NewLine \"crlf\" is not \"CRLF\" or \"LF\"; using \"CRLF\"." },
        { "CsvSettings:NewLine", "CR", "CsvSettings.NewLine \"CR\" is not \"CRLF\" or \"LF\"; using \"CRLF\"." },
        {
            "CsvSettings:DateFormat", "!",
            "CsvSettings.DateFormat \"!\" is not a usable date format; dates use the invariant-culture default."
        },
    };

    [Theory]
    [MemberData(nameof(RejectedValues))]
    public void FromConfiguration_RejectedValue_ReportsItAndUsesTheDefault(
        string key,
        string value,
        string expectedNotice)
    {
        using var dir = new TempDirectory();

        var (settings, notices) = AppSettings.FromConfiguration(Configuration((key, value)), dir.Path);

        Assert.Equal([expectedNotice.Replace(BaseDirectoryPlaceholder, dir.Path)], notices);
        AssertBuiltInDefaults(settings, dir.Path);
    }

    [Fact]
    public void FromConfiguration_EmptyConfiguration_UsesTheDefaultsAndReportsNothing()
    {
        using var dir = new TempDirectory();

        var (settings, notices) = AppSettings.FromConfiguration(Configuration(), dir.Path);

        Assert.Empty(notices);
        Assert.Empty(settings.Connections);
        AssertBuiltInDefaults(settings, dir.Path);
    }

    [Fact]
    public void FromConfiguration_UsableValues_AreKeptAndReportNothing()
    {
        using var dir = new TempDirectory();
        var queryFolder = Path.Combine(dir.Path, "sql");
        Directory.CreateDirectory(queryFolder);

        var (settings, notices) = AppSettings.FromConfiguration(
            Configuration(
                ("QueryFolder", "sql"),
                ("OutputFolder", "csv"),
                ("QueryTimeout", "120"),
                ("LogRetentionDays", "7"),
                ("SqlFileEncoding", "Shift-JIS"),
                ("CsvSettings:Delimiter", "\t"),
                ("CsvSettings:NullValue", "NULL"),
                ("CsvSettings:NewLine", "LF"),
                ("CsvSettings:DateFormat", "yyyy-MM-dd HH:mm:ss")),
            dir.Path);

        Assert.Empty(notices);
        Assert.Equal(queryFolder, settings.QueryFolder);
        Assert.Equal(Path.Combine(dir.Path, "csv"), settings.OutputFolder);
        Assert.Equal(120, settings.QueryTimeout);
        Assert.Equal(7, settings.LogRetentionDays);
        Assert.Equal("Shift-JIS", settings.SqlFileEncoding);
        Assert.Equal("\t", settings.CsvSettings.Delimiter);
        Assert.Equal("NULL", settings.CsvSettings.NullValue);
        Assert.Equal("LF", settings.CsvSettings.NewLine);
        Assert.Equal("yyyy-MM-dd HH:mm:ss", settings.CsvSettings.DateFormat);
    }

    [Fact]
    public void FromConfiguration_AbsoluteFolders_AreKeptAsWritten()
    {
        using var dir = new TempDirectory();
        var elsewhere = Path.Combine(dir.Path, "elsewhere");
        Directory.CreateDirectory(elsewhere);

        var (settings, notices) = AppSettings.FromConfiguration(
            Configuration(("QueryFolder", elsewhere), ("OutputFolder", elsewhere)),
            dir.Path);

        Assert.Empty(notices);
        Assert.Equal(elsewhere, settings.QueryFolder);
        Assert.Equal(elsewhere, settings.OutputFolder);
    }

    [Fact]
    public void FromConfiguration_SeveralRejectedValues_ReportsEachOnce()
    {
        using var dir = new TempDirectory();

        var (_, notices) = AppSettings.FromConfiguration(
            Configuration(("QueryTimeout", "0"), ("CsvSettings:NewLine", "CR")),
            dir.Path);

        Assert.Equal(
            [
                "QueryTimeout \"0\" is not a whole number greater than 0; using 30.",
                "CsvSettings.NewLine \"CR\" is not \"CRLF\" or \"LF\"; using \"CRLF\".",
            ],
            notices);
    }

    [Fact]
    public void FromConfiguration_Connections_AreReadInConfigurationOrder()
    {
        using var dir = new TempDirectory();

        var (settings, _) = AppSettings.FromConfiguration(
            Configuration(
                ("Connections:0:Name", "Dev"),
                ("Connections:0:ConnectionString", "Server=dev;Database=a;"),
                ("Connections:1:Name", "Prod"),
                ("Connections:1:ConnectionString", "Server=prod;Database=b;")),
            dir.Path);

        Assert.Equal(["Dev", "Prod"], settings.Connections.Select(c => c.Name));
        Assert.Equal("Server=prod;Database=b;", settings.Connections[1].ConnectionString);
    }

    [Theory]
    [InlineData("", "Server=x;Database=y;", "Connections[0].Name is required.")]
    [InlineData("   ", "Server=x;Database=y;", "Connections[0].Name is required.")]
    [InlineData("Dev Server", "", "Connections[0].ConnectionString is required.")]
    [InlineData("Dev Server", "   ", "Connections[0].ConnectionString is required.")]
    [InlineData("Dev Server", "Server=x;Database=y;", null)]
    public void ValidationError_ConnectionEntry_RequiresNameAndConnectionString(
        string name,
        string connectionString,
        string? expected)
    {
        var settings = new AppSettings
        {
            Connections = [new ConnectionEntry { Name = name, ConnectionString = connectionString }],
        };

        Assert.Equal(expected, settings.ValidationError());
    }

    [Fact]
    public void ValidationError_NoConnections_NamesTheMissingSetting()
    {
        var settings = new AppSettings();

        Assert.Equal("Connections must contain at least one entry.", settings.ValidationError());
    }

    [Fact]
    public void ValidationError_SecondConnectionIncomplete_NamesThatEntry()
    {
        var settings = new AppSettings
        {
            Connections =
            [
                new ConnectionEntry { Name = "Dev", ConnectionString = "Server=x;Database=y;" },
                new ConnectionEntry { Name = "Prod", ConnectionString = "" },
            ],
        };

        Assert.Equal("Connections[1].ConnectionString is required.", settings.ValidationError());
    }

    [Fact]
    public void Constructor_NoConfiguration_AppliesDocumentedDefaults()
    {
        var settings = new AppSettings();

        Assert.Empty(settings.Connections);
        Assert.Equal(30, settings.QueryTimeout);
        Assert.Equal("UTF-8", settings.SqlFileEncoding);
        Assert.Equal(30, settings.LogRetentionDays);
        Assert.Equal(",", settings.CsvSettings.Delimiter);
        Assert.Equal("", settings.CsvSettings.NullValue);
        Assert.Equal("CRLF", settings.CsvSettings.NewLine);
        Assert.Null(settings.CsvSettings.DateFormat);
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] entries)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
            .Build();

    private static void AssertBuiltInDefaults(AppSettings settings, string baseDirectory)
    {
        Assert.Equal(Path.Combine(baseDirectory, "queries"), settings.QueryFolder);
        Assert.Equal(Path.Combine(baseDirectory, "output"), settings.OutputFolder);
        Assert.Equal(30, settings.QueryTimeout);
        Assert.Equal(30, settings.LogRetentionDays);
        Assert.Equal("UTF-8", settings.SqlFileEncoding);
        Assert.Equal(",", settings.CsvSettings.Delimiter);
        Assert.Equal("", settings.CsvSettings.NullValue);
        Assert.Equal("CRLF", settings.CsvSettings.NewLine);
        Assert.Null(settings.CsvSettings.DateFormat);
    }
}
