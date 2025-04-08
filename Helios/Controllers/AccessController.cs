using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Utilities.Errors.HeliosErrors;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/fortnite/api/game/v2/")]
public class AccessController : ControllerBase
{
    [HttpPost("tryPlayOnPlatform/account/{accountId}")]
    public async Task<IActionResult> TryPlayOnPlatform([FromRoute] string accountId)
    {
        var userRepository = Constants.repositoryPool.GetRepository<User>();
        User user = await userRepository.FindAsync(new User
        {
            AccountId = accountId
        });
        
        if (user == null)
        {
            return AccountErrors.AccountNotFound(accountId)
                .WithMessage($"User with id {accountId} not found")
                .Apply(HttpContext);
        }

        if (user.Banned)
        {
            return AccountErrors.DisabledAccount.Apply(HttpContext);
        }

        Response.ContentType = "text/plain";
        return Ok("true");
    }

    [HttpGet("enabled_features")]
    public IActionResult EnabledFeatures()
    {
        return Ok(new List<object>());
    }

    [HttpPost("grant_access/{accountId}")]
    public async Task<IActionResult> GrantAccess([FromRoute] string accountId)
    {
        var userRepository = Constants.repositoryPool.GetRepository<User>();
        User user = await userRepository.FindAsync(new User
        {
            AccountId = accountId
        });
        
        if (user == null)
        {
            return AccountErrors.AccountNotFound(accountId)
                .WithMessage($"User with id {accountId} not found")
                .Apply(HttpContext);
        }

        if (user.Banned)
        {
            return AccountErrors.DisabledAccount.Apply(HttpContext);
        }

        return Ok();
    }
}