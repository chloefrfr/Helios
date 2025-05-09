using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.HTTP.Utilities.Interfaces;
using Helios.Utilities;
using Helios.Utilities.Caching;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Extensions;
using Helios.Utilities.Tokens;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/account/api/oauth/")]
public class OAuthController : ControllerBase
{
    private readonly IBodyParser _bodyParser;
    private const int AccessTokenLifetimeHours = 8;
    private const int RefreshTokenLifetimeDays = 7;

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
                return HandleClientCredentialsGrant(clientId);

            case "password":
                return await HandlePasswordGrantAsync(clientId, body);
                
            default:
                return AuthenticationErrors.OAuth.UnsupportedGrant(grantType).Apply(HttpContext);
        }
    }

    private IActionResult HandleClientCredentialsGrant(string clientId)
    {
        string cacheKey = $"client_token:{clientId}";

        if (HeliosFastCache.TryGet<(string Token, DateTime Expiry)>(cacheKey, out var tokenInfo) && 
            tokenInfo.Expiry > DateTime.UtcNow.AddMinutes(5))
        {
            var timeRemaining = (int)(tokenInfo.Expiry - DateTime.UtcNow).TotalSeconds;
            
            return Ok(new
            {
                access_token = $"eg1~{tokenInfo.Token}",
                expires_in = timeRemaining,
                expires_at = tokenInfo.Expiry.ToIsoUtcString(),
                token_type = "bearer",
                client_id = clientId,
                internal_client = true,
                client_service = "fortnite"
            });
        }
        
        var accessToken = TokenGenerator.GenerateToken(Constants.config.JWTClientSecret,
            Constants.config.JWTClientSecret);
        var expiresAt = DateTime.UtcNow.AddMinutes(60);
        
        HeliosFastCache.Set(cacheKey, (accessToken, expiresAt), TimeSpan.FromMinutes(60));

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

        var userRepository = Constants.repositoryPool.For<User>();
        
        var user = await userRepository.FindAsync(new User { Email = username });
        
        if (user == null || user.Banned || !PasswordHasher.VerifyPassword(password, user.Password))
            return AuthenticationErrors.OAuth.InvalidAccountCredentials.Apply(HttpContext);

        var accountId = user.AccountId;
        var now = DateTime.UtcNow;
        
        var requestKey = $"user_token_req:{accountId}";

        var (accessToken, refreshToken) = await Task.Run(() => HeliosFastCache.GetOrAdd<(string, string)>(
            requestKey,
            () => {
                var accessTokenTask = TokenUtilities.CreateAccessTokenAsync(clientId, "password", user);
                var refreshTokenTask = TokenUtilities.CreateRefreshTokenAsync(clientId, user);
                
                Task.WhenAll(accessTokenTask, refreshTokenTask).GetAwaiter().GetResult();
                
                CleanupOldTokens(accountId);
                
                return (accessTokenTask.Result, refreshTokenTask.Result);
            },
            TimeSpan.FromSeconds(5)
        ));

        var deviceId = Request.Headers.TryGetValue("X-Epic-Device-Id", out var rawDeviceId)
            ? rawDeviceId.ToString()
            : Guid.NewGuid().ToString("N");

        return Ok(new
        {
            access_token = $"eg1~{accessToken}",
            expires_in = AccessTokenLifetimeHours * 60 * 60,
            expires_at = now.AddHours(AccessTokenLifetimeHours).ToIsoUtcString(),
            token_type = "bearer",
            account_id = accountId,
            client_id = clientId,
            internal_client = true,
            client_service = "fortnite",
            refresh_token = $"eg1~{refreshToken}",
            refresh_expires = RefreshTokenLifetimeDays * 86400,
            refresh_expires_at = now.AddDays(RefreshTokenLifetimeDays).ToIsoUtcString(),
            displayName = user.Username,
            app = "fortnite",
            in_app_id = accountId,
            device_id = deviceId,
        });
    }
    
    private void CleanupOldTokens(string accountId)
    {
        Task.Run(async () =>
        {
            try 
            {
                var tokensRepository = Constants.repositoryPool.For<Tokens>();
                await tokensRepository.DeleteAsync(new Tokens { AccountId = accountId, Type = "accesstoken" });
                await tokensRepository.DeleteAsync(new Tokens { AccountId = accountId, Type = "refreshtoken" });
            }
            catch (Exception ex)
            {
                Logger.Error($"Error cleaing up old tokens: {ex.Message}");
            }
        });
    }
}