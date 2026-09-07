using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace QueryToCsv;

public class ConnectionEntry
{
    public string Name { get; set; } = "";
    public string ConnectionString { get; set; } = "";
}

public class CsvSettings
{
    public const string DefaultDelimiter = ",";
    public const string DefaultNullValue = "";
    public const string DefaultNewLine = "CRLF";

    public string Delimiter { get; set; } = DefaultDelimiter;
    public string NullValue { get; set; } = DefaultNullValue;
    public string NewLine { get; set; } = DefaultNewLine;
    public string? DateFormat { get; set; }
}

public class AppSettings
{
    public const string FileName = "appsettings.json";
    public const string DefaultQueryFolderName = "queries";
    public const string DefaultOutputFolderName = "output";
    public const string DefaultSqlFileEncoding = "UTF-8";
    public const int DefaultQueryTimeout = 30;
    public const int DefaultLogRetentionDays = 30;

    private const string QueryFolderKey = "QueryFolder";
    private const string OutputFolderKey = "OutputFolder";
    private const string QueryTimeoutKey = "QueryTimeout";
    private const string LogRetentionDaysKey = "LogRetentionDays";
    private const string SqlFileEncodingKey = "SqlFileEncoding";
    private const string DelimiterKey = "CsvSettings:Delimiter";
    private const string NullValueKey = "CsvSettings:NullValue";
    private const string NewLineKey = "CsvSettings:NewLine";
    private const string DateFormatKey = "CsvSettings:DateFormat";

    public List<ConnectionEntry> Connections { get; set; } = [];
    public string QueryFolder { get; set; } = "";
    public string OutputFolder { get; set; } = "";
    public int QueryTimeout { get; set; } = DefaultQueryTimeout;
    public string SqlFileEncoding { get; set; } = DefaultSqlFileEncoding;
    public int LogRetentionDays { get; set; } = DefaultLogRetentionDays;
    public CsvSettings CsvSettings { get; set; } = new();

    // Returns the settings to run on, plus one notice per configured value that was
    // rejected. A missing or unreadable file is itself a notice, never a failure
    // (docs/rules/standard.md, Configuration Values).
    public static (AppSettings Settings, IReadOnlyList<string> Notices) Load()
    {
        var baseDirectory = AppContext.BaseDirectory;

        if (!File.Exists(Path.Combine(baseDirectory, FileName)))
        {
            return (Defaults(baseDirectory),
                [$"{FileName} not found; continuing with built-in defaults."]);
        }

        IConfiguration config;
        try
        {
            config = new ConfigurationBuilder()
                .SetBasePath(baseDirectory)
                .AddJsonFile(FileName)
                .Build();
        }
        catch (Exception)
        {
            return (Defaults(baseDirectory),
                [$"failed to load {FileName}; continuing with built-in defaults."]);
        }

        return FromConfiguration(config, baseDirectory);
    }

    internal static (AppSettings Settings, IReadOnlyList<string> Notices) FromConfiguration(
        IConfiguration config,
        string baseDirectory)
    {
        var notices = new List<string>();
        var settings = Defaults(baseDirectory);

        T Accept<T>((T Value, string? Notice) resolved)
        {
            if (resolved.Notice is not null)
                notices.Add(resolved.Notice);

            return resolved.Value;
        }

        settings.Connections = config.GetSection("Connections").GetChildren()
            .Select(entry => new ConnectionEntry
            {
                Name = entry["Name"] ?? "",
                ConnectionString = entry["ConnectionString"] ?? "",
            })
            .ToList();

        settings.QueryFolder = Accept(ResolveFolder(
            config, QueryFolderKey, Path.Combine(baseDirectory, DefaultQueryFolderName), baseDirectory, mustExist: true));
        settings.OutputFolder = Accept(ResolveFolder(
            config, OutputFolderKey, Path.Combine(baseDirectory, DefaultOutputFolderName), baseDirectory, mustExist: false));
        settings.QueryTimeout = Accept(ResolvePositiveNumber(config, QueryTimeoutKey, DefaultQueryTimeout));
        settings.LogRetentionDays = Accept(ResolvePositiveNumber(config, LogRetentionDaysKey, DefaultLogRetentionDays));
        settings.SqlFileEncoding = Accept(ResolveEncodingName(config, SqlFileEncodingKey));
        settings.CsvSettings.Delimiter = Accept(ResolveDelimiter(config, DelimiterKey));
        settings.CsvSettings.NullValue = config[NullValueKey] ?? CsvSettings.DefaultNullValue;
        settings.CsvSettings.NewLine = Accept(ResolveNewLine(config, NewLineKey));
        settings.CsvSettings.DateFormat = Accept(ResolveDateFormat(config, DateFormatKey));

        return (settings, notices);
    }

    // The reason the configuration cannot be used, or null when it can. Connections is
    // required input rather than a defaulted value: no built-in value can name the
    // server an operator means to query (docs/rules/QueryToCsv.md, Required Input).
    public string? ValidationError()
    {
        if (Connections.Count == 0)
            return "Connections must contain at least one entry.";

        for (var i = 0; i < Connections.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(Connections[i].Name))
                return $"Connections[{i}].Name is required.";

            if (string.IsNullOrWhiteSpace(Connections[i].ConnectionString))
                return $"Connections[{i}].ConnectionString is required.";
        }

        return null;
    }

    private static AppSettings Defaults(string baseDirectory) => new()
    {
        QueryFolder = Path.Combine(baseDirectory, DefaultQueryFolderName),
        OutputFolder = Path.Combine(baseDirectory, DefaultOutputFolderName),
    };

    // A relative folder resolves against the executable's directory, never against the
    // working directory (docs/rules/dotnet.md, FILEPATH). QueryFolder has to exist to be
    // usable; OutputFolder is created when the CSV is written.
    private static (string Value, string? Notice) ResolveFolder(
        IConfiguration config,
        string key,
        string fallback,
        string baseDirectory,
        bool mustExist)
    {
        var raw = config[key];
        if (raw is null)
            return (fallback, null);

        if (string.IsNullOrWhiteSpace(raw))
            return (fallback, $"{DisplayKey(key)} is blank; using \"{fallback}\".");

        string resolved;
        try
        {
            resolved = Path.GetFullPath(raw, baseDirectory);
        }
        catch (ArgumentException)
        {
            return (fallback, $"{DisplayKey(key)} \"{raw}\" is not a usable folder path; using \"{fallback}\".");
        }

        if (mustExist && !Directory.Exists(resolved))
            return (fallback, $"{DisplayKey(key)} \"{resolved}\" does not name an existing folder; using \"{fallback}\".");

        return (resolved, null);
    }

    private static (int Value, string? Notice) ResolvePositiveNumber(IConfiguration config, string key, int fallback)
    {
        var raw = config[key];
        if (raw is null)
            return (fallback, null);

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0)
            return (value, null);

        return (fallback, $"{DisplayKey(key)} \"{raw}\" is not a whole number greater than 0; using {fallback}.");
    }

    private static (string Value, string? Notice) ResolveEncodingName(IConfiguration config, string key)
    {
        var raw = config[key];
        if (raw is null)
            return (DefaultSqlFileEncoding, null);

        try
        {
            Encoding.GetEncoding(raw);
            return (raw, null);
        }
        catch (ArgumentException)
        {
            return (DefaultSqlFileEncoding,
                $"{DisplayKey(key)} \"{raw}\" is not an encoding the runtime recognizes; using \"{DefaultSqlFileEncoding}\".");
        }
    }

    private static (string Value, string? Notice) ResolveDelimiter(IConfiguration config, string key)
    {
        var raw = config[key];
        if (raw is null)
            return (CsvSettings.DefaultDelimiter, null);

        if (raw.Length == 1)
            return (raw, null);

        return (CsvSettings.DefaultDelimiter,
            $"{DisplayKey(key)} \"{raw}\" is not exactly one character; using \"{CsvSettings.DefaultDelimiter}\".");
    }

    private static (string Value, string? Notice) ResolveNewLine(IConfiguration config, string key)
    {
        var raw = config[key];
        if (raw is null)
            return (CsvSettings.DefaultNewLine, null);

        if (raw is "CRLF" or "LF")
            return (raw, null);

        return (CsvSettings.DefaultNewLine,
            $"{DisplayKey(key)} \"{raw}\" is not \"CRLF\" or \"LF\"; using \"{CsvSettings.DefaultNewLine}\".");
    }

    private static (string? Value, string? Notice) ResolveDateFormat(IConfiguration config, string key)
    {
        var raw = config[key];
        if (raw is null)
            return (null, null);

        try
        {
            DateTime.UnixEpoch.ToString(raw, CultureInfo.InvariantCulture);
            return (raw, null);
        }
        catch (FormatException)
        {
            return (null,
                $"{DisplayKey(key)} \"{raw}\" is not a usable date format; dates use the invariant-culture default.");
        }
    }

    // Configuration paths are colon-separated; a message names the setting the way
    // appsettings.json spells it.
    private static string DisplayKey(string key) => key.Replace(':', '.');
}
