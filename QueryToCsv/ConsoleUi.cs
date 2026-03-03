using System.Text;
using Microsoft.Data.SqlClient;

namespace QueryToCsv;

public static class ConsoleUi
{
    public static int SelectQuery(string[] fileNames)
    {
        Console.WriteLine("=== Select a query ===");
        Console.WriteLine("0. Enter query directly");
        for (var i = 0; i < fileNames.Length; i++)
            Console.WriteLine($"{i + 1}. {fileNames[i]}");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Enter number: ");
            var input = Console.ReadLine();
            if (input is null) Environment.Exit(1);

            if (int.TryParse(input, out var num))
            {
                if (num == 0)
                    return -1;

                if (num >= 1 && num <= fileNames.Length)
                    return num - 1;
            }

            Console.Error.WriteLine($"Please enter a number between 0 and {fileNames.Length}.");
        }
    }

    public static string InputQuery()
    {
        Console.WriteLine("Enter SQL query (end with Ctrl+Z):");
        var lines = new List<string>();
        while (true)
        {
            Console.Write("  > ");
            var line = Console.ReadLine();
            if (line is null) break;
            lines.Add(line);
        }
        var sql = string.Join(Environment.NewLine, lines);
        if (string.IsNullOrWhiteSpace(sql))
        {
            Console.Error.WriteLine("Error: No query entered.");
            Environment.Exit(1);
        }
        return sql;
    }

    public static bool AskIncludeHeader()
    {
        while (true)
        {
            Console.Write("Include header row? (y/n): ");
            var input = Console.ReadLine();
            if (input is null) Environment.Exit(1);

            if (input.Equals("y", StringComparison.OrdinalIgnoreCase)) return true;
            if (input.Equals("n", StringComparison.OrdinalIgnoreCase)) return false;

            Console.Error.WriteLine("Please enter y or n.");
        }
    }

    public static Encoding SelectEncoding()
    {
        Console.WriteLine("=== Select encoding ===");
        Console.WriteLine("1. UTF-8");
        Console.WriteLine("2. UTF-8 with BOM");
        Console.WriteLine("3. UTF-16 LE");
        Console.WriteLine("4. Shift-JIS");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Enter number: ");
            var input = Console.ReadLine();
            if (input is null) Environment.Exit(1);

            var encoding = input switch
            {
                "1" => ResolveEncoding("utf-8"),
                "2" => ResolveEncoding("utf-8-bom"),
                "3" => ResolveEncoding("utf-16"),
                "4" => ResolveEncoding("shift-jis"),
                _ => null,
            };

            if (encoding is not null)
                return encoding;

            Console.Error.WriteLine("Please enter a number between 1 and 4.");
        }
    }

    public static Encoding? ResolveEncoding(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "utf-8" or "utf8" => new UTF8Encoding(false),
            "utf-8-bom" or "utf8-bom" => new UTF8Encoding(true),
            "utf-16" or "utf16" => Encoding.Unicode,
            "shift-jis" or "shiftjis" or "shift_jis" => Encoding.GetEncoding("Shift-JIS"),
            _ => null,
        };
    }

    public static int SelectConnection(IReadOnlyList<ConnectionEntry> connections)
    {
        if (connections.Count == 1)
            return 0;

        Console.WriteLine("=== Select connection ===");
        for (var i = 0; i < connections.Count; i++)
        {
            var info = FormatConnectionInfo(connections[i].ConnectionString);
            Console.WriteLine($"{i + 1}. {connections[i].Name} ({info})");
        }
        Console.WriteLine();

        while (true)
        {
            Console.Write("Enter number: ");
            var input = Console.ReadLine();
            if (input is null) Environment.Exit(1);

            if (int.TryParse(input, out var num) && num >= 1 && num <= connections.Count)
                return num - 1;

            Console.Error.WriteLine($"Please enter a number between 1 and {connections.Count}.");
        }

    }

    private static string FormatConnectionInfo(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var server = string.IsNullOrEmpty(builder.DataSource) ? "?" : builder.DataSource;
            var database = string.IsNullOrEmpty(builder.InitialCatalog) ? "?" : builder.InitialCatalog;
            return $"{server} - {database}";
        }
        catch
        {
            return "?";
        }
    }
}
