using Helios.Database;
using Helios.Database.Repository;
using Helios.Socket.Typings;

namespace Helios.Socket;

public class WebSocketConfiguration
{
    internal static ConfigTypes config { get; } = Configuration.GetConfig();
    internal const string xmlString = "http://fortmp.dev/config";
    
    public string ServerUrl { get; set; } = "ws://0.0.0.0:8181";
    public bool RestartAfterListenError { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
    public static DatabaseCtx dbContext { get; } = new DatabaseCtx(config.DatabaseConnectionUrl);
    public static RepositoryPool repositoryPool { get; } = new RepositoryPool(config.DatabaseConnectionUrl);
    
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "admin123";
    public string AdminPasswordHash { get; set; } = ""; // dont touch
    public string AdminSecretKey { get; set; } = Guid.NewGuid().ToString(); 
    public int AdminTokenExpirationMinutes { get; set; } = 60;
}