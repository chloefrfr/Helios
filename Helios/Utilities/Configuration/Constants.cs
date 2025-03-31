using System.Text.RegularExpressions;
using Helios.Classes.Typings;
using Helios.Database;
using Helios.Database.Repository;

namespace Helios.Configuration;

public static class Constants
{
    public static ConfigTypes config { get; } = Configuration.GetConfig();
    public static DatabaseCtx dbContext { get; } = new DatabaseCtx(config.DatabaseConnectionUrl);
    public static RepositoryPool repositoryPool { get; } = new RepositoryPool(config.DatabaseConnectionUrl);

    public static string ExtractSanitiztedRoute(PathString route)
    {
        string sanitizedRoute = null;
        sanitizedRoute = Regex.Replace(route, @"^(https?:\/\/)?([a-zA-Z0-9.-]+)(:\d+)?", "");
        sanitizedRoute = Regex.Replace(sanitizedRoute, @".*\/fortnite", "");
        
        return sanitizedRoute;
    }

    public const string xmlString = "http://fortmp.dev/config";
}