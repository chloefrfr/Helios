using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Helios.Classes.UserAgent;
using Helios.Configuration;
using Helios.Database.Repository;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Profiles;
using Helios.Managers;
using Helios.Managers.Helpers;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers.MCP;

[ApiController]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/QueryProfile")]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/ClaimMfaEnabled")]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/ClientQuestLogin")]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/SetHardcoreModifier")]
public class QueryProfile : ControllerBase
{
    private static readonly HashSet<string> DefaultPublicProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "common_public",
        "collections",
        "creative"
    };
    
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = null };

    [HttpPost]
    public async Task<IActionResult> Init(
        [FromRoute] string accountId,
        [FromQuery] string profileId,
        [FromHeader(Name = "User-Agent")] string userAgent)
    {
        if (userAgent is null)
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);

        if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(accountId))
            return MCPErrors.InvalidPayload.Apply(HttpContext);

        var parsedUserAgent = UserAgentParser.Parse(userAgent);
        
        var userRepository = Constants.repositoryPool.For<User>(false);
        var profilesRepository = Constants.repositoryPool.For<Profiles>(false);

        var now = DateTime.Now;
        var utcNow = DateTime.UtcNow;
        var todayString = now.ToIsoUtcString();
        var utcNowString = utcNow.ToIsoUtcString();

        var userTask = userRepository.FindAsync(new User { AccountId = accountId });
        var profileTask = profilesRepository.FindAsync(new Profiles { ProfileId = profileId, AccountId = accountId });

        await Task.WhenAll(userTask, profileTask);
        
        var user = await userTask;
        if (user is null) 
            return AccountErrors.AccountNotFound(accountId).Apply(HttpContext);
        
        if (DefaultPublicProfiles.Contains(profileId))
        {
            if (!user.LastLogin?.StartsWith(todayString) == true)
            {
                user.LastLogin = now.ToIsoUtcString();
                _ = userRepository.UpdateAsync(user);
            }

            return Ok(GenerateDefaultPublicProfile(accountId, profileId, utcNowString));
        }

        var profile = await profileTask;
        if (profile is null) 
            return MCPErrors.TemplateNotFound.Apply(HttpContext);

        if (!user.LastLogin?.StartsWith(todayString) == true)
        {
            user.LastLogin = now.ToIsoUtcString();
            _ = userRepository.UpdateAsync(user);
        }

        var profileItemsRepository = Constants.repositoryPool.For<Items>();
        var profileItems = await profileItemsRepository.FindAllAsync(new Items
        {
            AccountId = accountId,
            ProfileId = profileId,
        }, useCache: false);

        var itemsList = profileItems.ToList();

        if (profile.ProfileId == "athena")
        {
            await ProcessSeasonItems(profileItemsRepository, itemsList, parsedUserAgent);
        }

        var finalProfile = new ProfileBuilder(accountId, profile, user, itemsList);
        var profileChanges = new object[] 
        { 
            new { changeType = "fullProfileUpdate", profile = finalProfile } 
        };

        return Ok(ProfileResponseManager.Generate(profile, profileChanges, profileId));
    }

    private static object GenerateDefaultPublicProfile(string accountId, string profileId, string utcNowString) => new
    {
        profileRevision = 0,
        profileId,
        profileChanges = new object[]
        {
            new
            {
                changeType = "fullProfileUpdate",
                profile = new
                {
                    _id = $"{profileId}-{accountId}",
                    created = utcNowString,
                    updated = utcNowString,
                    rvn = 0,
                    profileId,
                    accountId
                }
            }
        },
        profileCommandRevision = 0,
        serverTime = utcNowString
    };

    private static async Task ProcessSeasonItems(
        Repository<Items> repository,
        List<Items> items,
        SeasonInfo userAgent)
    {
        var season = userAgent.Season.ToString();
        
        async Task UpdateSeasonItem(Items item)
        {
            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(item.Value);
                
                string currentValue;
                if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    currentValue = jsonElement.GetInt32().ToString();
                }
                else
                {
                    currentValue = jsonElement.GetString();
                }

                if (currentValue == season)
                    return;

                if (int.TryParse(season, out int seasonNum))
                {
                    item.Value = JsonSerializer.Serialize(seasonNum, JsonOptions);
                }
                else
                {
                    item.Value = JsonSerializer.Serialize(season, JsonOptions);
                }
                
                await repository.UpdateAsync(item);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error updating season item: {ex.Message}");
            }
        }

        var seasonItems = items.Where(item => item.IsAttribute && item.TemplateId == "season_num").ToList();
        if (seasonItems.Count == 0)
            return;

        var updateTasks = new Task[seasonItems.Count];
        for (int i = 0; i < seasonItems.Count; i++)
        {
            updateTasks[i] = UpdateSeasonItem(seasonItems[i]);
        }

        await Task.WhenAll(updateTasks);
    }
}