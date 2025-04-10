using System.Text.Json;
using Helios.Classes.UserAgent;
using Helios.Configuration;
using Helios.Database.Repository;
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
        if (userAgent is null)
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);

        if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(accountId))
            return MCPErrors.InvalidPayload.Apply(HttpContext);

        var parsedUserAgent = UserAgentParser.Parse(userAgent);
        var userRepository = Constants.repositoryPool.GetRepository<User>();
        var now = DateTime.Now;
        
        var profilesRepository = Constants.repositoryPool.GetRepository<Profiles>();
        var userTask = userRepository.FindAsync(new User { AccountId = accountId });
        var profileTask = profilesRepository.FindAsync(new Profiles { ProfileId = profileId, AccountId = accountId });

        await Task.WhenAll(userTask, profileTask);
        var user = await userTask;
        var profile = await profileTask;

        if (user is null) return AccountErrors.AccountNotFound(accountId).Apply(HttpContext);
        if (profileId == "common_public")
        {
            UpdateLastLoginIfNeeded(user, now);
            await userRepository.UpdateAsync(user);

            return Ok(GenerateDefaultPublicProfile(accountId));
        }
        
        UpdateLastLoginIfNeeded(user, now);
        await userRepository.UpdateAsync(user);

        if (profile is null) return MCPErrors.TemplateNotFound.Apply(HttpContext);

        var profileItemsRepository = Constants.repositoryPool.GetRepository<Items>();
        var profileItems = await profileItemsRepository.FindManyAsync(new Items
        {
            AccountId = accountId,
            ProfileId = profileId,
        });

        if (profile.ProfileId == "athena")
        {
            await ProcessSeasonItems(profileItemsRepository, profileItems, parsedUserAgent);
        }

        var finalProfile = new ProfileBuilder(accountId, profile, user, profileItems);
        var profileChanges = new[] { new { changeType = "fullProfileUpdate", profile = finalProfile } };

        return Ok(ProfileResponseManager.Generate(profile, profileChanges, profileId));
    }

    private void UpdateLastLoginIfNeeded(User user, DateTime now)
    {
        var todayString = now.ToString("yyyy-MM-dd");
        if (user.LastLogin?.StartsWith(todayString) != true)
        {
            user.LastLogin = now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    private static object GenerateDefaultPublicProfile(string accountId) => new
    {
        profileRevision = 0,
        profileId = "common_public",
        profileChanges = new[]
        {
            new
            {
                changeType = "fullProfileUpdate",
                profile = new
                {
                    _id = $"common_public-{accountId}",
                    created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    updated = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    rvn = 0,
                    profileId = "common_public",
                    accountId
                }
            }
        },
        profileCommandRevision = 0,
        serverTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
    };

    private static async Task ProcessSeasonItems(
        Repository<Items> repository,
        IEnumerable<Items> items,
        SeasonInfo userAgent)
    {
        var season = userAgent.Season.ToString();
        var updateTasks = items
            .Where(item => item.IsAttribute && item.TemplateId == "season_num")
            .Select(item =>
            {
                try
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(item.Value);
                    var currentValue = jsonElement.GetString();

                    if (currentValue == season)
                        return Task.CompletedTask;

                    item.Value = JsonSerializer.Serialize(season);
                    return repository.UpdateAsync(item);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error updating season item: {ex.Message}");
                    return Task.CompletedTask;
                }
            });

        await Task.WhenAll(updateTasks);
    }
}