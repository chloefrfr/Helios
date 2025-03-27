namespace Helios.Configuration.Services;

public static class WebhostConfiguration
{
    public static void ConfigureWebhosts(ConfigureWebHostBuilder webhosts)
    {
        webhosts.ConfigureKestrel(options =>
        {
            options.Limits.MaxConcurrentConnections = 100;
            options.Limits.MaxConcurrentUpgradedConnections = 100;
            options.Limits.MaxRequestBodySize = 10 * 1024;
        });

    }
}