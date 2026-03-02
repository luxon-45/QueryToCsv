using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace QueryToCsv;

public class CsvSettings
{
    public string Delimiter { get; set; } = ",";
    public string NullValue { get; set; } = "";
    public string NewLine { get; set; } = "CRLF";
    public string? DateFormat { get; set; }
}

public class AppSettings
{
    public string ConnectionString { get; set; } = "";
    public string QueryFolder { get; set; } = "";
    public string OutputFolder { get; set; } = "";
    public int QueryTimeout { get; set; } = 30;
    public string SqlFileEncoding { get; set; } = "UTF-8";
    public int LogRetentionDays { get; set; } = 30;
    public CsvSettings CsvSettings { get; set; } = new();

    public static AppSettings? Load()
    {
        var baseDir = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "appsettings.json");

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine("Error: appsettings.json not found.");
            return null;
        }

        IConfiguration config;
        try
        {
            config = new ConfigurationBuilder()
                .SetBasePath(baseDir)
                .AddJsonFile("appsettings.json")
                .Build();
        }
        catch
        {
            Console.Error.WriteLine("Error: Failed to load appsettings.json.");
            return null;
        }

        var settings = new AppSettings
        {
            ConnectionString = config["ConnectionString"] ?? "",
            QueryFolder = config["QueryFolder"] ?? "",
            OutputFolder = config["OutputFolder"] ?? "",
            SqlFileEncoding = config["SqlFileEncoding"] ?? "UTF-8",
        };

        if (int.TryParse(config["QueryTimeout"], out var timeout))
            settings.QueryTimeout = timeout;

        if (int.TryParse(config["LogRetentionDays"], out var retention) && retention > 0)
            settings.LogRetentionDays = retention;

        var csvSection = config.GetSection("CsvSettings");
        if (csvSection.Exists())
        {
            settings.CsvSettings.Delimiter = csvSection["Delimiter"] ?? ",";
            settings.CsvSettings.NullValue = csvSection["NullValue"] ?? "";
            settings.CsvSettings.NewLine = csvSection["NewLine"] ?? "CRLF";
            settings.CsvSettings.DateFormat = csvSection["DateFormat"];
        }

        // パスを exe 基準で解決
        settings.QueryFolder = Path.GetFullPath(settings.QueryFolder, baseDir);
        settings.OutputFolder = Path.GetFullPath(settings.OutputFolder, baseDir);

        return settings;
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Console.Error.WriteLine("Error: ConnectionString is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(QueryFolder))
        {
            Console.Error.WriteLine("Error: QueryFolder is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            Console.Error.WriteLine("Error: OutputFolder is required.");
            return false;
        }

        if (QueryTimeout <= 0)
        {
            Console.Error.WriteLine("Error: QueryTimeout must be greater than 0.");
            return false;
        }

        if (CsvSettings.Delimiter.Length != 1)
        {
            Console.Error.WriteLine("Error: Delimiter must be exactly one character.");
            return false;
        }

        if (CsvSettings.NewLine is not ("CRLF" or "LF"))
        {
            Console.Error.WriteLine("Error: NewLine must be \"CRLF\" or \"LF\".");
            return false;
        }

        try
        {
            Encoding.GetEncoding(SqlFileEncoding);
        }
        catch
        {
            Console.Error.WriteLine($"Error: SqlFileEncoding \"{SqlFileEncoding}\" is not a valid encoding.");
            return false;
        }

        if (CsvSettings.DateFormat is not null)
        {
            try
            {
                DateTime.Now.ToString(CsvSettings.DateFormat, CultureInfo.InvariantCulture);
            }
            catch
            {
                Console.Error.WriteLine($"Error: DateFormat \"{CsvSettings.DateFormat}\" is not valid.");
                return false;
            }
        }

        if (!Directory.Exists(QueryFolder))
        {
            Console.Error.WriteLine($"Error: QueryFolder not found: {QueryFolder}");
            return false;
        }

        return true;
    }
}
