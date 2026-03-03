using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.SqlClient;
using NLog;

namespace QueryToCsv;

public static partial class QueryExecutor
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|MERGE|GRANT|REVOKE|DENY|BULK|INTO|OPENROWSET|OPENDATASOURCE|OPENQUERY)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProhibitedKeywordsRegex();

    [GeneratedRegex(@"--[^\r\n]*|/\*[\s\S]*?\*/|'(?:[^']|'')*'", RegexOptions.None)]
    private static partial Regex CommentsAndStringsRegex();

    public static int Execute(AppSettings settings, string connectionString, string sql, string? baseName, bool includeHeader, Encoding csvEncoding)
    {
        var label = baseName ?? "Direct Input";

        if (!IsSelectOnly(sql))
        {
            Logger.Error($"Rejected: non-SELECT statement in {label}");
            Console.Error.WriteLine("Error: Only SELECT statements are allowed.");
            return 1;
        }

        var outputPath = BuildOutputPath(settings, baseName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var tempPath = outputPath + ".tmp";
        try
        {
            using var connection = new SqlConnection(connectionString);
            Logger.Info("Connecting to SQL Server...");
            Console.WriteLine("Connecting...");
            connection.Open();

            using var command = new SqlCommand(sql, connection);
            command.CommandTimeout = settings.QueryTimeout;

            Logger.Info($"Executing: {label}");
            Console.WriteLine("Executing query...");
            using var reader = command.ExecuteReader();

            Console.WriteLine("Writing CSV...");
            var rowCount = WriteCsv(reader, tempPath, csvEncoding, includeHeader, settings.CsvSettings);

            File.Move(tempPath, outputPath);

            Logger.Info($"CSV written: {outputPath} ({rowCount} rows)");
            Console.WriteLine();
            Console.WriteLine($"Done: {outputPath}");
            Console.WriteLine($"Rows: {rowCount.ToString("N0", CultureInfo.InvariantCulture)}");
            Console.WriteLine();
            Console.WriteLine($"QueryToCsv --open output");
            Console.WriteLine($"QueryToCsv --open \"{outputPath}\"");
            return 0;
        }
        catch (SqlException ex) when (ex.Number == -2)
        {
            Logger.Error("Query timed out");
            Console.Error.WriteLine("Error: Query timed out.");
            return 1;
        }
        catch (SqlException ex)
        {
            Logger.Error(ex, "SQL execution failed");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* best effort cleanup */ }
        }
    }

    private static bool IsSelectOnly(string sql)
    {
        var stripped = CommentsAndStringsRegex().Replace(sql, " ");
        return !ProhibitedKeywordsRegex().IsMatch(stripped);
    }

    private static string BuildOutputPath(AppSettings settings, string? baseName)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputDir = settings.OutputFolder;

        var prefix = string.IsNullOrEmpty(baseName) ? timestamp : $"{baseName}_{timestamp}";

        var candidate = Path.Combine(outputDir, $"{prefix}.csv");
        if (!File.Exists(candidate))
            return candidate;

        for (var i = 2; ; i++)
        {
            candidate = Path.Combine(outputDir, $"{prefix}_{i}.csv");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static int WriteCsv(
        SqlDataReader reader,
        string outputPath,
        Encoding encoding,
        bool includeHeader,
        CsvSettings csvSettings)
    {
        var newLine = csvSettings.NewLine == "LF" ? "\n" : "\r\n";
        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = csvSettings.Delimiter,
            NewLine = newLine,
        };

        using var writer = new StreamWriter(outputPath, false, encoding);
        using var csv = new CsvWriter(writer, csvConfig);

        var fieldCount = reader.FieldCount;

        if (includeHeader)
        {
            for (var i = 0; i < fieldCount; i++)
                csv.WriteField(reader.GetName(i));
            csv.NextRecord();
        }

        var rowCount = 0;
        while (reader.Read())
        {
            for (var i = 0; i < fieldCount; i++)
            {
                if (reader.IsDBNull(i))
                {
                    csv.WriteField(csvSettings.NullValue);
                }
                else
                {
                    csv.WriteField(FormatValue(reader, i, csvSettings.DateFormat));
                }
            }
            csv.NextRecord();
            rowCount++;
        }

        return rowCount;
    }

    private static string FormatValue(SqlDataReader reader, int ordinal, string? dateFormat)
    {
        var type = reader.GetFieldType(ordinal);

        if (type == typeof(DateTime))
        {
            var value = reader.GetDateTime(ordinal);
            return dateFormat is not null
                ? value.ToString(dateFormat, CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
        }

        if (type == typeof(DateTimeOffset))
        {
            var value = reader.GetFieldValue<DateTimeOffset>(ordinal);
            return dateFormat is not null
                ? value.ToString(dateFormat, CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
        }

        var rawValue = reader.GetValue(ordinal);

        if (rawValue is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return rawValue.ToString() ?? "";
    }
}
