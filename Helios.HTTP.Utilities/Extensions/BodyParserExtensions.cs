using Helios.HTTP.Utilities.Classes;
using Helios.HTTP.Utilities.Interfaces;
using Helios.HTTP.Utilities.Parsers;
using Microsoft.Extensions.DependencyInjection;

namespace Helios.HTTP.Utilities.Extensions;

public static class BodyParserExtensions
{
    public static IServiceCollection AddBodyParser(this IServiceCollection services, Action<ParserOptions> configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
            
        services.AddSingleton<IBodyParser, BodyParser>();
        return services;
    }
}