using Helios.Http.Configuration.Routing.Classes;
using Microsoft.Extensions.DependencyInjection;

namespace Helios.Http.Configuration.Routing;

public static class RouteExtensions
{
    /// <summary>
    /// Configures Helios routing services with optional custom configuration.
    /// Simplifies dependency injection and routing setup.
    /// </summary>
    /// <param name="services">Dependency injection container</param>
    /// <param name="configure">Optional configuration callback</param>
    /// <returns>Configured service collection</returns>
    public static IServiceCollection AddHeliosRouting(
        this IServiceCollection services,
        Action<HeliosRoutingOptions>? configure = null)
    {
        var options = new HeliosRoutingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddControllers();

        return services;
    }
}