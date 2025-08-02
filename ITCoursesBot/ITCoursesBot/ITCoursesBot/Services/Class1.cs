using Serilog;
using System.Runtime.CompilerServices;

public static class LoggerService
{
    static LoggerService()
    {
        // Инициализация Serilog при первом обращении к классу
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            // Пишем в файлы вида "Logs/log-2025-08-02.txt"
            .WriteTo.File(
                path: Path.Combine("Logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}"
            )
            .CreateLogger();
    }

    public static void LogMessage(
        string message,
        string logLevel = "Information",
        [CallerMemberName] string caller = null,
        [CallerFilePath] string filePath = null,
        [CallerLineNumber] int lineNumber = 0)
    {
        // Формируем контекст
        var ts = DateTimeOffset.Now;
        var className = ExtractClassName(filePath);
        var output = $"{message} (at {className}.{caller}:{lineNumber})";

        // Выбираем уровень и логируем через Serilog
        switch (logLevel.ToUpperInvariant())
        {
            case "DEBUG":
                Log.Debug(output);
                break;
            case "WARNING":
                Log.Warning(output);
                break;
            case "ERROR":
                Log.Error(output);
                break;
            case "CRITICAL":
                Log.Fatal(output);
                break;
            case "INFORMATION":
            default:
                Log.Information(output);
                break;
        }
    }

    private static string ExtractClassName(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return "UnknownClass";

        try
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            return fileName ?? "UnknownClass";
        }
        catch
        {
            return "UnknownClass";
        }
    }

    // Удобные методы-обёртки
    public static void LogDebug(string message, [CallerMemberName] string c = null, [CallerFilePath] string f = null, [CallerLineNumber] int l = 0)
        => LogMessage(message, "DEBUG", c, f, l);

    public static void LogInfo(string message, [CallerMemberName] string c = null, [CallerFilePath] string f = null, [CallerLineNumber] int l = 0)
        => LogMessage(message, "INFORMATION", c, f, l);

    public static void LogWarning(string message, [CallerMemberName] string c = null, [CallerFilePath] string f = null, [CallerLineNumber] int l = 0)
        => LogMessage(message, "WARNING", c, f, l);

    public static void LogError(string message, [CallerMemberName] string c = null, [CallerFilePath] string f = null, [CallerLineNumber] int l = 0)
        => LogMessage(message, "ERROR", c, f, l);

    public static void LogCritical(string message, [CallerMemberName] string c = null, [CallerFilePath] string f = null, [CallerLineNumber] int l = 0)
        => LogMessage(message, "CRITICAL", c, f, l);
}
