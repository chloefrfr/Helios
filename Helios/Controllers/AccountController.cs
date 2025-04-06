using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/account/api/public/")]
public class AccountController : ControllerBase
{
    [HttpGet("account/{accountId}")]
    public async Task<IActionResult> FindAccountById([FromRoute] string accountId)
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
        
        return Ok(new
        {
            id = user.AccountId,
            displayName = user.Username,
            name = user.Username,
            failedLoginAttempts = 0,
            lastLogin = DateTime.Now.ToIsoUtcString(),
            numberOfDisplayNameChanges = 0,
            ageGroup = "UNKNOWN",
            headless = false,
            country = "US",
            lastName = "",
            links = new object { },
            preferredLanguage = "en",
            canUpdateDisplayName = false,
            tfaEnabled = true,
            emailVerified = true,
            minorVerified = true,
            minorExpected = true,
            minorStatus = "UNKNOWN"
        });
    }

    [HttpGet("account")]
    public async Task<IActionResult> Account([FromQuery] string accountId)
    {
        if (string.IsNullOrEmpty(accountId))
            return BasicErrors.BadRequest.WithMessage("Invalid account id").Apply(HttpContext);
        
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

        return Ok(new
        {
            id = user.AccountId,
            displayName = user.Username,
            cabinedMode = false,
            externalAuth = new object { }
        });
    }

    [HttpGet("displayName/{username}")]
    public async Task<IActionResult> FindByDisplayName([FromRoute] string username)
    {
        var userRepository = Constants.repositoryPool.GetRepository<User>();
        User user = await userRepository.FindAsync(new User
        {
            Username = username
        });
        
        if (user == null)
        {
            return AccountErrors.AccountNotFound(username)
                .WithMessage($"User with username {username} not found")
                .Apply(HttpContext);
        }

        if (user.Banned)
        {
            return AccountErrors.DisabledAccount.Apply(HttpContext);
        }
   
        return Ok(new
        {
            id = user.AccountId,
            displayName = user.Username,
            externalAuths = new object { }
        });
    }
}