using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Helios.Utilities;

public class Logger
{
    static Logger()
    {
        var heliosSerilogTheme = new AnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
        {
            [ConsoleThemeStyle.Text] = "\x1b[38;5;253m",
            [ConsoleThemeStyle.SecondaryText] = "\x1b[38;5;246m",
            [ConsoleThemeStyle.TertiaryText] = "\x1b[38;5;242m",
            [ConsoleThemeStyle.Invalid] = "\x1b[38;5;196m",
            [ConsoleThemeStyle.Null] = "\x1b[38;5;42m",
            [ConsoleThemeStyle.Name] = "\x1b[38;5;46m",
            [ConsoleThemeStyle.String] = "\x1b[38;5;227m",
            [ConsoleThemeStyle.Number] = "\x1b[38;5;203m",
            [ConsoleThemeStyle.Boolean] = "\x1b[38;5;51m",
            [ConsoleThemeStyle.LevelVerbose] = "\x1b[38;5;253m",
            [ConsoleThemeStyle.LevelDebug] = "\x1b[38;5;87m",
            [ConsoleThemeStyle.LevelInformation] = "\x1b[38;5;46m",
            [ConsoleThemeStyle.LevelWarning] = "\x1b[38;5;220m",
            [ConsoleThemeStyle.LevelError] = "\x1b[38;5;196m",
            [ConsoleThemeStyle.LevelFatal] = "\x1b[38;5;199m",
        });


        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                theme: heliosSerilogTheme
            )
            .CreateLogger();

    }

    public static void Info(string message, string method = null)
    {
        if (!string.IsNullOrEmpty(method))
        {
            var methodColor = GetMethodColor(method);
            Log.Information("{MethodColor} ({Method}) {Message}", methodColor, method, message);
        }
        else
        {
            Log.Information(message);
        }
    }

    public static void Warn(string message)
    {
        Log.Warning(message);
    }

    public static void Error(string message, Exception ex = null)
    {
        Log.Error(ex, message);
    }

    public static void Debug(string message)
    {
        Log.Debug(message);
    }

    public static void Fatal(string message)
    {
        Log.Fatal(message);
    }

    private static string GetMethodColor(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => "\x1b[38;5;46m",
            "POST" => "\x1b[38;5;51m",
            "PUT" => "\x1b[38;5;227m",
            "DELETE" => "\x1b[38;5;196m",
            _ => "\x1b[38;5;253m"
        };
    }

    public static void Close()
    {
        Log.CloseAndFlush();
    }
}