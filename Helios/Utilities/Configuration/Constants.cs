using System.Text.RegularExpressions;
using Helios.Classes.Typings;
using Helios.Database;
using Helios.Database.Repository;
using Helios.Managers.Unreal;

namespace Helios.Configuration;

public static class Constants
{
    public static ConfigTypes config { get; } = Configuration.GetConfig();
    public static DatabaseCtx dbContext { get; } = new DatabaseCtx(config.DatabaseConnectionUrl);
    public static RepositoryPool repositoryPool { get; } = new RepositoryPool(config.DatabaseConnectionUrl);
    public static UnrealAssetProvider FileProvider { get; set; } = null;
    
    public static DateTime ActiveUntil => new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    public static string ExtractSanitiztedRoute(PathString route)
    {
        string sanitizedRoute = null;
        sanitizedRoute = Regex.Replace(route, @"^(https?:\/\/)?([a-zA-Z0-9.-]+)(:\d+)?", "");
        sanitizedRoute = Regex.Replace(sanitizedRoute, @".*\/fortnite", "");
        
        return sanitizedRoute;
    }

    public const string xmlString = "http://fortmp.dev/config";
}