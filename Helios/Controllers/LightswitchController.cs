using Helios.Classes.Endpoints.Lightswitch;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Tokens;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/lightswitch/api/service/")]
public class LightswitchController : ControllerBase
{
    private static readonly List<string> DefaultCatalogIds = new() { "a7f138b2e51945ffbfdacc1af0541053" };
    private static readonly List<string> DefaultAllowedActions = new() { "Play", "Download" };
    private static readonly LauncherInfoDTO DefaultLauncherInfo = new()
    {
        AppName = "Fortnite",
        CatalogItemId = "4fe75bbc5a674f4f9b356b5c90567da5",
        Namespace = "fn"
    };
    
    [Route("bulk/status")]
    public async Task<IActionResult> GetAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader) || string.IsNullOrEmpty(authHeader))
        {
            string route = Constants.ExtractSanitiztedRoute(Request.Path);
            return AuthenticationErrors.AuthenticationFailed(route).AddVariables([authHeader]).Apply(HttpContext);
        }
        
        string token = authHeader.ToString().Replace("bearer eg1~", "");
        
        string accountId;
        try
        {
            var decodedToken = TokenGenerator.DecodeToken(token);
            if (decodedToken == null)
            {
                return AuthenticationErrors.InvalidToken("Failed to decode token").Apply(HttpContext);
            }
        
            accountId = decodedToken["sub"] as string;
            if (string.IsNullOrEmpty(accountId))
            {
                return AuthenticationErrors.InvalidToken("Missing subject in token").Apply(HttpContext);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Token decoding error", ex);
            return AuthenticationErrors.InvalidToken("Failed to decode token").Apply(HttpContext);
        }

        var userRepository = Constants.repositoryPool.GetRepository<User>();
        User user = await userRepository.FindAsync(new User
        {
            AccountId = accountId
        });

        if (user == null)
        {
            return AccountErrors.AccountNotFound(accountId).WithMessage($"User with id {accountId} not found").Apply(HttpContext);
        }

        if (user.Banned)
        {
            return AccountErrors.DisabledAccount.Apply(HttpContext);
        }
        
        return Ok(new List<object>
        {
            new {
                serviceInstanceId = "fortnite",
                status = "Up",
                message = "Servers are UP!",
                overrideCatalogIds = DefaultCatalogIds,
                allowedActions = DefaultAllowedActions,
                banned = false,
                launcherInfoDTO = DefaultLauncherInfo
            }
        });
    }
}