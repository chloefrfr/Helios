using System.Security.Cryptography;
using System.Text;
using Helios.Configuration;
using Jose;

namespace Helios.Utilities.Tokens;

public class TokenGenerator
{
    private const int TokenLifetimeMinutes = 240;
    private const int RandomBytesForP = 128;
    private const int RandomBytesForJti = 32;

    /// <summary>
    /// Generates a JWT using the provided client ID and client secret.
    /// The token is signed using HMAC SHA-256.
    /// </summary>
    /// <param name="clientId">The client ID used for identification.</param>
    /// <param name="clientSecret">The client secret used for signing the token.</param>
    /// <returns>A string representing the generated JWT.</returns>
    /// <exception cref="ArgumentException">Thrown when clientId or clientSecret is null or empty.</exception>
    public static string GenerateToken(string clientId, string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("Client secret cannot be null or empty.", nameof(clientSecret));

        try
        {
            string p = Convert.ToBase64String(GenerateRandomBytes(RandomBytesForP));
            string jti = BitConverter.ToString(GenerateRandomBytes(RandomBytesForJti)).Replace("-", "")
                .ToLowerInvariant();

            DateTime utcNow = DateTime.UtcNow;
            DateTime expirationTime = utcNow.AddMinutes(TokenLifetimeMinutes);

            var payload = new Dictionary<string, object>
            {
                { "p", p },
                { "clsvc", "fortnite" },
                { "t", "s" },
                { "mver", false },
                { "clid", clientId },
                { "ic", true },
                { "exp", new DateTimeOffset(expirationTime).ToUnixTimeSeconds() },
                { "am", "client_credentials" },
                { "iat", new DateTimeOffset(utcNow).ToUnixTimeSeconds() },
                { "jti", jti },
                { "creation_date", utcNow.ToString("yyyy-MM-dd HH:mm:ss.fffZ") },
            };

            var token = JWT.Encode(payload, Encoding.UTF8.GetBytes(clientSecret), JwsAlgorithm.HS256);
            return token;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error generating token: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Decodes a JWT token and returns the claims as a dictionary.
    /// </summary>
    /// <param name="token">The JWT token to decode.</param>
    /// <returns>A dictionary containing the claims of the decoded token.</returns>
    /// <exception cref="ArgumentException">Thrown when token or clientSecret is null or empty.</exception>
    public static IDictionary<string, object> DecodeToken(string token)
    {
        string clientSecret = Constants.config.JWTClientSecret;
        
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token cannot be null or empty.", nameof(token));
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("Client secret cannot be null or empty.", nameof(clientSecret));

        try
        {
            var payload = JWT.Decode<IDictionary<string, object>>(token, Encoding.UTF8.GetBytes(clientSecret), JwsAlgorithm.HS256);
            return payload;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error decoding token: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Generates a cryptographically secure random byte array.
    /// </summary>
    /// <param name="size">The number of random bytes to generate.</param>
    /// <returns>A byte array containing random data.</returns>
    private static byte[] GenerateRandomBytes(int size)
    {
        byte[] randomBytes = new byte[size];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return randomBytes;
    }
}