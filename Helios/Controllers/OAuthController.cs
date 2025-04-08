using System.Security.Cryptography;
using System.Text;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.HTTP.Utilities.Interfaces;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Extensions;
using Helios.Utilities.Tokens;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Helios.Controllers;

[ApiController]
[Route("/account/api/oauth/")]
public class OAuthController : ControllerBase
{
    private readonly IBodyParser _bodyParser;
    private const int AccessTokenLifetimeMinutes = 15;
    private const int RefreshTokenLifetimeDays = 7;
    
    private static readonly Dictionary<string, (string Token, DateTime Expiry)> _clientCredentialsCache = 
        new Dictionary<string, (string, DateTime)>();
    private static readonly object _cacheLock = new object();

    public OAuthController(IBodyParser bodyParser)
    {
        _bodyParser = bodyParser;
    }

    [HttpPost("token")]
    public async Task<IActionResult> AuthToken()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeaders) || string.IsNullOrEmpty(authHeaders))
            return AuthenticationErrors.InvalidHeader.Apply(HttpContext);

        var tokenParts = authHeaders.ToString().Split(' ', 2);
        if (tokenParts.Length != 2 || !tokenParts[0].Equals("Basic", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticationErrors.OAuth.InvalidClient
                .WithMessage("Invalid authorization header format. Expected 'Basic <token>'.")
                .Apply(HttpContext);
        }

        string decodedClientAuth;
        try
        {
            decodedClientAuth = Encoding.UTF8.GetString(Convert.FromBase64String(tokenParts[1]));
        }
        catch
        {
            return AuthenticationErrors.OAuth.InvalidClient.Apply(HttpContext);
        }

        int colonIndex = decodedClientAuth.IndexOf(':');
        if (colonIndex == -1)
            return AuthenticationErrors.OAuth.InvalidClient.Apply(HttpContext);

        string clientId = decodedClientAuth.Substring(0, colonIndex);

        Dictionary<string, string> body;
        try
        {
            var parsedBody = await _bodyParser.ParseAsync(Request);
            body = new Dictionary<string, string>(parsedBody);
        }
        catch
        {
            return AuthenticationErrors.InvalidRequest.Apply(HttpContext);
        }

        if (!body.TryGetValue("grant_type", out var grantType))
            return AuthenticationErrors.OAuth.InvalidClient.Apply(HttpContext);

        switch (grantType)
        {
            case "client_credentials":
                return await HandleClientCredentialsGrantAsync(clientId);

            case "password":
                return await HandlePasswordGrantAsync(clientId, body);
                
            default:
                return AuthenticationErrors.OAuth.UnsupportedGrant(grantType).Apply(HttpContext);
        }
    }

    private async Task<IActionResult> HandleClientCredentialsGrantAsync(string clientId)
    {
        lock (_cacheLock)
        {
            if (_clientCredentialsCache.TryGetValue(clientId, out var cachedValue) && 
                cachedValue.Expiry > DateTime.UtcNow.AddMinutes(5)) 
            {
                var timeRemaining = (int)(cachedValue.Expiry - DateTime.UtcNow).TotalSeconds;
                
                return Ok(new
                {
                    access_token = $"eg1~{cachedValue.Token}",
                    expires_in = timeRemaining,
                    expires_at = cachedValue.Expiry.ToIsoUtcString(),
                    token_type = "bearer",
                    client_id = clientId,
                    internal_client = true,
                    client_service = "fortnite"
                });
            }
        }
        
        var accessToken = TokenGenerator.GenerateToken(Constants.config.JWTClientSecret,
            Constants.config.JWTClientSecret);
        var expiresAt = DateTime.UtcNow.AddMinutes(60);
        
        lock (_cacheLock)
        {
            _clientCredentialsCache[clientId] = (accessToken, expiresAt);
        }

        return Ok(new
        {
            access_token = $"eg1~{accessToken}",
            expires_in = 3600,
            expires_at = expiresAt.ToIsoUtcString(),
            token_type = "bearer",
            client_id = clientId,
            internal_client = true,
            client_service = "fortnite"
        });
    }

    private async Task<IActionResult> HandlePasswordGrantAsync(string clientId, Dictionary<string, string> body)
    {
        if (!body.TryGetValue("username", out var username) || !body.TryGetValue("password", out var password))
        {
            return BasicErrors.BadRequest
                .WithMessage("Invalid username or password.")
                .Apply(HttpContext);
        }

        var userRepository = Constants.repositoryPool.GetRepository<User>();
        
        var user = await userRepository.FindAsync(new User { Email = username });
        
        if (user == null || user.Banned || !PasswordHasher.VerifyPassword(password, user.Password))
            return AuthenticationErrors.OAuth.InvalidAccountCredentials.Apply(HttpContext);

        var accountId = user.AccountId;
        var now = DateTime.UtcNow;
        
        var accessTokenTask = TokenUtilities.CreateAccessTokenAsync(clientId, "password", user);
        var refreshTokenTask = TokenUtilities.CreateRefreshTokenAsync(clientId, user);

        _ = Task.Run(async () =>
        {
            var tokensRepository = Constants.repositoryPool.GetRepository<Tokens>();
            await tokensRepository.DeleteAsync(new Tokens { AccountId = accountId, Type = "accesstoken" });
            await tokensRepository.DeleteAsync(new Tokens { AccountId = accountId, Type = "refreshtoken" });
        });

        await Task.WhenAll(accessTokenTask, refreshTokenTask);
        var accessToken = await accessTokenTask;
        var refreshToken = await refreshTokenTask;

        var deviceId = Request.Headers.TryGetValue("X-Epic-Device-Id", out var rawDeviceId)
            ? rawDeviceId.ToString()
            : GenerateDeviceId();

        return Ok(new
        {
            access_token = $"eg1~{accessToken}",
            expires_in = AccessTokenLifetimeMinutes * 60,
            expires_at = now.AddMinutes(AccessTokenLifetimeMinutes).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            token_type = "bearer",
            account_id = accountId,
            client_id = clientId,
            internal_client = true,
            client_service = "fortnite",
            refresh_token = $"eg1~{refreshToken}",
            refresh_expires = RefreshTokenLifetimeDays * 86400,
            refresh_expires_at = now.AddDays(RefreshTokenLifetimeDays).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            displayName = user.Username,
            app = "fortnite",
            in_app_id = accountId,
            device_id = deviceId,
        });
    }

    private string GenerateDeviceId()
    {
        return Guid.NewGuid().ToString("N");
    }
}