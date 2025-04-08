using System.Text;
using Helios.Configuration;

namespace Helios.Classes.Tokens;

public class TokenSettings
{
    public const string AppName = "fortnite";
    public const string ServiceName = "fortnite";
    public const int AccessTokenExpiry = 4 * 3600; 
    public const int RefreshTokenExpiry = 14 * 24 * 3600; 
    public static byte[] Secret => Encoding.UTF8.GetBytes(Constants.config.JWTClientSecret);
}