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

            if (int.TryParse(input, out var num))
            {
                switch (num)
                {
                    case 1: return new UTF8Encoding(false);
                    case 2: return new UTF8Encoding(true);
                    case 3: return Encoding.Unicode;
                    case 4: return Encoding.GetEncoding("Shift-JIS");
                }
            }

            Console.Error.WriteLine("Please enter a number between 1 and 4.");
        }
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
