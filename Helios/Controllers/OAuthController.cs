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

    public OAuthController(IBodyParser bodyParser)
    {
        _bodyParser = bodyParser;
    }
    
    [HttpPost("token")]
    public async Task<IActionResult> AuthToken()
    {
        string route = Constants.ExtractSanitiztedRoute(Request.Path);

        if (!Request.Headers.TryGetValue("Authorization", out var authHeaders) || string.IsNullOrEmpty(authHeaders))
        {
            return AuthenticationErrors.InvalidHeader.Apply(HttpContext);
        }

        
        string[] token = authHeaders.ToString().Split(' ', 2);
        if (token.Length != 2 || !token[0].Equals("Basic", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticationErrors.OAuth.InvalidClient
                .WithMessage("Invalid authorization header format. Expected 'Basic <token>'.")
                .Apply(HttpContext);
        }
        
        Dictionary<string, string> body;
        try
        {
            var parsedBody = await _bodyParser.ParseAsync(Request);
            body = new Dictionary<string, string>(parsedBody);
        }
        catch (Exception e)
        {
            Logger.Error($"Failed to parse body {e.Message}");
            return AuthenticationErrors.InvalidRequest.Apply(HttpContext);
        }
        
        var userRepository = Constants.repositoryPool.GetRepository<User>();
        
        string? grantType = body.TryGetValue("grant_type", out var value) ? value : null;
        string clientId = Encoding.UTF8.GetString(Convert.FromBase64String(token[1])).Split(':')[0];

        if (grantType is null)
        {
            return AuthenticationErrors.OAuth.InvalidClient.Apply(HttpContext);
        }

        if (clientId is null)
        {
            return AuthenticationErrors.OAuth.InvalidClient.Apply(HttpContext);
        }

        User foundUser = null;

        Logger.Debug($"Requested grant_type: {grantType}");
        
        switch (grantType)
        {
            case "client_credentials":
                string access_token = TokenGenerator.GenerateToken("test", "test");
                Logger.Debug($"Generated access_token: {access_token}");
                
                return Ok(new
                {
                    access_token = $"eg1~{access_token}",
                    expires_in = 3600,
                    expires_at =  DateTime.UtcNow.AddHours(1).ToIsoUtcString(),
                    token_type = "bearer",
                    client_id = clientId,
                    internal_client = true,
                    client_service = "fortnite"
                });
            case "password":
            {
                if (!body.TryGetValue("username", out string username) ||
                    !body.TryGetValue("password", out string password))
                {
                    return BasicErrors.BadRequest
                        .WithMessage("Invalid username or password.")
                        .Apply(HttpContext);
                }

                var user = await userRepository.FindAsync(new User { Email = username });
                if (user is null)
                    return AuthenticationErrors.OAuth.InvalidAccountCredentials.Apply(HttpContext);

                if (user.Banned)
                    return AccountErrors.DisabledAccount.Apply(HttpContext);

                var deviceId = Request.Headers.TryGetValue("X-Epic-Device-Id", out var rawDeviceId)
                    ? rawDeviceId.ToString()
                    : GenerateDeviceId();
                
                Logger.Debug($"DeviceId: {deviceId}");

                var response = new
                {
                    
                };

                return Ok(response);
            }
            
            default:
                return AuthenticationErrors.OAuth.UnsupportedGrant(grantType).Apply(HttpContext);
        }
    }
    
    private string GenerateDeviceId()
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] bytes = new byte[16]; 
            rng.GetBytes(bytes);
            return Guid.NewGuid().ToString("N"); 
        }
    }
}