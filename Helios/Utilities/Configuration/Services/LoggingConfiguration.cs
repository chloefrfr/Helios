using Serilog;

namespace Helios.Configuration.Services;

public static class LoggingConfiguration
{
    public static void ConfigureLogging(ILoggingBuilder logging, IConfiguration configuration)
    {
        logging.ClearProviders();
        logging.AddSerilog();

        logging.AddConfiguration(configuration.GetSection("Logging"));
        logging.AddConsole().SetMinimumLevel(LogLevel.Debug);
    }
}