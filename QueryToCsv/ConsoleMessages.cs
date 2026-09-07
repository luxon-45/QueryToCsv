namespace QueryToCsv;

internal static class ConsoleMessages
{
    internal static void WriteWarning(string message)
    {
        WriteTagged(message);
    }

    internal static void WriteError(string message)
    {
        WriteTagged(message);
    }

    internal static void WriteUsageError(string message)
    {
        WriteTagged(message);
        Console.Error.WriteLine(
            $"Try '{ApplicationVersion.ApplicationName} --help' for more information.");
    }

    private static void WriteTagged(string message)
    {
        Console.Error.WriteLine($"{ApplicationVersion.ApplicationName}: {message}");
    }
}
