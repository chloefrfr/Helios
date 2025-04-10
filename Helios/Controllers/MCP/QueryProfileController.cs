using System.Text.Json;
using Helios.Classes.MCP;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Profiles;
using Helios.Managers;
using Helios.Managers.Helpers;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Helios.Controllers.MCP;

[ApiController]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/QueryProfile")]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/ClaimMfaEnabled")]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/ClientQuestLogin")]
public class QueryProfileController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> QueryProfile(
        [FromRoute] string accountId, 
        [FromQuery] string profileId, 
        [FromHeader(Name = "User-Agent")] string userAgent)
    {
        Logger.Debug(userAgent);
        if (userAgent is null)
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);
        
        if (profileId is null || accountId is null)
            return MCPErrors.InvalidPayload.Apply(HttpContext);
        
        var parsedUserAgent = UserAgentParser.Parse(userAgent);
        
        var userRepository = Constants.repositoryPool.GetRepository<User>();
        var profilesRepository = Constants.repositoryPool.GetRepository<Profiles>();
        
        var userTask = userRepository.FindAsync(new User { AccountId = accountId });
        var profileTask = profilesRepository.FindAsync(new Profiles { ProfileId = profileId, AccountId = accountId });
        
        await Task.WhenAll(userTask, profileTask);
        
        var user = await userTask;
        var profile = await profileTask;
        
        if (user is null)
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

        if (profile is null && profileId == "common_public")
        {
            var defaultResponse = ProfileResponseManager.Generate(new Profiles
            {
                AccountId = accountId,
                ProfileId = "common_public",
                Revision = 0
            }, new List<object>
            {
                new {
                    changeType = "fullProfileUpdate",
                    profile = new DefaultProfileResponse("common_public", accountId)
                }
            }, "common_public");
            
            return Ok(defaultResponse);
        }
        
        if (profile is null)
            return MCPErrors.TemplateNotFound.Apply(HttpContext);
        
        var profileItemsRepository = Constants.repositoryPool.GetRepository<Items>();
        var profileItems = await profileItemsRepository.FindManyAsync(new Items
        {
            AccountId = accountId,
            ProfileId = profileId,
        });

        if (profile.ProfileId == "athena")
        {
            var itemProcessingTasks = profileItems.Where(item => item.IsAttribute && item.TemplateId == "season_num")
                .Select(async item =>
                {
                    try
                    {
                        var deserializedValue = JsonSerializer.Deserialize<dynamic>(item.Value) ?? new JObject();
                        deserializedValue = parsedUserAgent.Season.ToString();
                        item.Value = JsonSerializer.Serialize(deserializedValue);

                        await profileItemsRepository.UpdateAsync(item);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error updating item (TemplateId: {item.TemplateId}): {ex.Message}");
                    }
                }).ToArray();

            await Task.WhenAll(itemProcessingTasks);
        }
        
        var finalProfile = new ProfileBuilder(accountId, profile, user, profileItems);
        
        var profileChanges = new List<object>
        {
            new 
            {
                changeType = "fullProfileUpdate",
                profile = finalProfile
            }
        };

        return Ok(ProfileResponseManager.Generate(profile, profileChanges, profileId));
    }
}