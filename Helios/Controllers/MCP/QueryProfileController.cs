using Helios.Classes.MCP;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Profiles;
using Helios.Managers;
using Helios.Managers.Helpers;
using Helios.Utilities.Errors.HeliosErrors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Helios.Controllers.MCP;

[ApiController]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/QueryProfile")]
public class QueryProfileController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> QueryProfile([FromRoute] string accountId, [FromQuery] string profileId)
    {
        var userRepository = Constants.repositoryPool.GetRepository<User>();
        var profilesRepository = Constants.repositoryPool.GetRepository<Profiles>();
        
        var userTask = userRepository.FindAsync(new User { AccountId = accountId });
        var profileTask = profilesRepository.FindAsync(new Profiles { ProfileId = profileId, AccountId = accountId });
        
        await Task.WhenAll(userTask, profileTask);
        
        var user = await userTask;
        var profile = await profileTask;

        if (user == null)
        {
            return AccountErrors.AccountNotFound(accountId)
                .WithMessage($"User with id {accountId} not found")
                .Apply(HttpContext);
        }
        
        if (DateTime.TryParse(user.LastLogin, out DateTime lastLogin))
        {
            if (lastLogin.Date != DateTime.Now.Date)
            {
                user.LastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                await userRepository.UpdateAsync(user);
            }
        }
        else
        {
            user.LastLogin = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            await userRepository.UpdateAsync(user);
        }

        if (profile == null && profileId == "common_public")
        {
            var defaultResponse = ProfileResponseManager.Generate(new Profiles
            {
                AccountId = accountId,
                ProfileId = "common_public",
                Revision = 0
            }, new List<object> { new DefaultProfileResponse(profileId, accountId) }, "common_public");
            
            return Ok(defaultResponse);
        }

        var profileItemsRepository = Constants.repositoryPool.GetRepository<Items>();
        var profileItems = await profileItemsRepository.FindManyAsync(new Items
        {
            AccountId = accountId,
            ProfileId = profileId,
        });

        var finalProfile = new ProfileBuilder(accountId, profile, user, profileItems);
        
        var profileChanges = new List<object>(1)
        {
            new 
            {
                changeType = "fullProfileUpdate",
                profile = finalProfile
            }
        };

        return Ok(profileChanges);
    }
}