using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;

namespace Helios.Database;

internal class Logger
{
    private class LogLevelSymbolEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var symbol = logEvent.Level switch
            {
                LogEventLevel.Verbose => "VER",
                LogEventLevel.Debug => "DBG",
                LogEventLevel.Information => "INF",
                LogEventLevel.Warning => "WRN",
                LogEventLevel.Error => "ERR",
                LogEventLevel.Fatal => "FTL",
                _ => "UNK"
            };
        
            var color = logEvent.Level switch
            {
                LogEventLevel.Verbose => "38;5;244m",
                LogEventLevel.Debug => "1;38;5;33m",
                LogEventLevel.Information => "1;38;5;48m",
                LogEventLevel.Warning => "1;38;5;220m",
                LogEventLevel.Error => "1;38;5;196m",
                LogEventLevel.Fatal => "1;38;5;129m",
                _ => "38;5;253m"
            };

            var coloredSymbol = $"\x1b[{color}{symbol}\x1b[0m";
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("LevelSymbol", coloredSymbol));
        }
    }

    static Logger()
    {
        var heliosSerilogTheme = new AnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
        {
            [ConsoleThemeStyle.Text] = "\x1b[38;5;250m",
            [ConsoleThemeStyle.SecondaryText] = "\x1b[38;5;240m",
            [ConsoleThemeStyle.TertiaryText] = "\x1b[38;5;238m",
            [ConsoleThemeStyle.Invalid] = "\x1b[38;5;196m\x1b[4m",
            [ConsoleThemeStyle.Null] = "\x1b[38;5;42m",
            [ConsoleThemeStyle.Name] = "\x1b[38;5;81m",
            [ConsoleThemeStyle.String] = "\x1b[38;5;228m",
            [ConsoleThemeStyle.Number] = "\x1b[38;5;209m",
            [ConsoleThemeStyle.Boolean] = "\x1b[38;5;45m",
            [ConsoleThemeStyle.LevelVerbose] = "\x1b[38;5;244m",
            [ConsoleThemeStyle.LevelDebug] = "\x1b[1;38;5;33m",
            [ConsoleThemeStyle.LevelInformation] = "\x1b[1;38;5;48m",
            [ConsoleThemeStyle.LevelWarning] = "\x1b[1;38;5;220m",
            [ConsoleThemeStyle.LevelError] = "\x1b[1;38;5;196m",
            [ConsoleThemeStyle.LevelFatal] = "\x1b[1;38;5;129m",
        });
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.With<LogLevelSymbolEnricher>()
            .WriteTo.Console(
                outputTemplate: "[\x1b[38;5;242m{Timestamp:HH:mm:ss.fff}\x1b[0m] {LevelSymbol} \x1b[38;5;242m|\x1b[0m {Message:lj}{NewLine}{Exception}",
                theme: heliosSerilogTheme
            )
            .CreateLogger();
    }

    internal static void Info(string message, string method = null)
    {
        if (!string.IsNullOrEmpty(method))
        {
            var methodColor = GetMethodColor(method);
            Log.Information("{MethodColor}⟪{Method}⟫\x1b[0m {Message}", methodColor, method, message);
        }
        else
        {
            Log.Information("\x1b[38;5;250m{Message}\x1b[0m", message);
        }
    }

    internal static void Warn(string message)
    {
        Log.Warning("\x1b[38;5;220m{Message}\x1b[0m", message);
    }

    internal static void Error(string message, Exception ex = null)
    {
        Log.Error(ex, "\x1b[38;5;196m{Message}\x1b[0m", message);
    }

    internal static void Debug(string message)
    {
        Log.Debug("\x1b[38;5;33m{Message}\x1b[0m", message);
    }

    internal static void Fatal(string message)
    {
        Log.Fatal("\x1b[38;5;129m{Message}\x1b[0m", message);
    }

    private static string GetMethodColor(string method)
    {
        return method.ToUpperInvariant() switch
        {
            "GET" => "\x1b[1;38;5;48m",     
            "POST" => "\x1b[1;38;5;51m",      
            "PUT" => "\x1b[1;38;5;227m",       
            "DELETE" => "\x1b[1;38;5;196m",   
            _ => "\x1b[1;38;5;255m"           
        };
    }

    internal static void Close()
    {
        Log.CloseAndFlush();
    }
}