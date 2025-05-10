using Helios.Configuration;
using Helios.Database.Tables.Account;

namespace Helios.Utilities.Tokens;

public class VerifyToken
{
    public static async Task<bool> Verify(HttpContext context)
    {

        string authorization = context.Request.Headers["Authorization"].ToString();

        if (string.IsNullOrWhiteSpace(authorization))
            return false;

        if (authorization.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase))
        {
            authorization = authorization.Substring(7).Trim();
        }
        else if (authorization.StartsWith("eg1~", StringComparison.OrdinalIgnoreCase))
        {
            authorization = authorization.Substring(4).Trim();
        }

        authorization = authorization.Replace("eg1~", "");

        if (string.IsNullOrWhiteSpace(authorization))
            return false;

        string accountId = null;

        try
        {
            var decodedToken = TokenGenerator.DecodeToken(authorization);

            if (decodedToken == null || !decodedToken.ContainsKey("sub"))
            {
                Logger.Error($"Invalid access token: {authorization}");
                return false;
            }

            accountId = decodedToken["sub"] as string;

            if (string.IsNullOrEmpty(accountId))
            {
                Logger.Error($"Token 'sub' value is missing.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Token decoding error: {ex.Message}");
            return false;
        }

        try
        {
            var uRepo = Constants.repositoryPool.Repo<User>();
            var user = await uRepo().FindAsync(new User { AccountId = accountId });
            if (user is null)
            {
                return false;
            }

            if (user.Banned)
            {
                return false;
            }

            string userAgent = context.Request.Headers["User-Agent"].ToString();
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return false;
            }

            var userAgentInfo = UserAgentParser.Parse(userAgent);
            if (userAgentInfo is null)
            {
                return false;
            }

            string userJson = System.Text.Json.JsonSerializer.Serialize(user);
            context.Response.Cookies.Append("User", userJson, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7)
            });

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error verifying token: {ex.Message}", ex);
            return false;
        }
    }
}