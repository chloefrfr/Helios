using Helios.Classes.Typings;
using Helios.Database;

namespace Helios.Configuration;

public static class Constants
{
    public static ConfigTypes config { get; } = Configuration.GetConfig();
    public static DatabaseCtx dbContext { get; } = new DatabaseCtx(config.DatabaseConnectionUrl);

    public const string xmlString = "http://fortmp.dev/config";
}