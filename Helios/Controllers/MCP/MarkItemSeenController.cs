using System.Buffers;
using System.Text.Json;
using System.Threading.Tasks;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Profiles;
using Helios.Managers;
using Helios.Utilities.Errors.HeliosErrors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Helios.Controllers.MCP;

[ApiController]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/MarkItemSeen")]
public class MarkItemSeenController : ControllerBase
{
    private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true };

    [HttpPost]
    public async Task<IActionResult> MarkItemSeen(
        [FromRoute] string accountId,
        [FromQuery] string profileId)
    {
        if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(accountId))
            return MCPErrors.InvalidPayload.Apply(HttpContext);

        var userRepository = Constants.repositoryPool.GetRepository<User>();
        var profilesRepository = Constants.repositoryPool.GetRepository<Profiles>();
        var userTask = userRepository.FindAsync(new User { AccountId = accountId });
        var profileTask = profilesRepository.FindAsync(new Profiles { ProfileId = profileId, AccountId = accountId });

        JsonDocument json;
        try
        {
            if (Request.ContentLength.HasValue && Request.ContentLength.Value < 4096)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent((int)Request.ContentLength.Value);
                try
                {
                    int bytesRead = await Request.Body.ReadAsync(buffer, 0, (int)Request.ContentLength.Value);
                    json = JsonDocument.Parse(buffer.AsMemory(0, bytesRead), JsonOptions);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            else
            {
                using var memoryStream = new MemoryStream();
                await Request.Body.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                json = JsonDocument.Parse(memoryStream, JsonOptions);
            }
        }
        catch (JsonException)
        {
            return MCPErrors.InvalidPayload.Apply(HttpContext);
        }

        if (!json.RootElement.TryGetProperty("itemIds", out var itemIdsProperty))
            return MCPErrors.InvalidPayload.Apply(HttpContext);

        var itemIds = new HashSet<string>(itemIdsProperty.GetArrayLength());
        foreach (var itemId in itemIdsProperty.EnumerateArray())
        {
            string id = itemId.GetString();
            if (!string.IsNullOrEmpty(id))
                itemIds.Add(id);
        }
        
        User user = null;
        Profiles profile = null;

        if (itemIds.Count == 0)
        {
            user = await userTask;
            profile = await profileTask;
            
            if (user is null || profile is null)
                return AccountErrors.AccountNotFound(accountId).Apply(HttpContext);
                
            return Ok(ProfileResponseManager.Generate(profile, Array.Empty<object>(), profileId));
        }

        await Task.WhenAll(userTask, profileTask);
        user = await userTask;
        profile = await profileTask;

        if (user is null || profile is null)
            return AccountErrors.AccountNotFound(accountId).Apply(HttpContext);

        var profileItemsRepository = Constants.repositoryPool.GetRepository<Items>();

        var itemsTasks = new List<Task<Items>>(itemIds.Count);
        foreach (var itemId in itemIds)
        {
            itemsTasks.Add(profileItemsRepository.FindAsync(new Items
            {
                ProfileId = profileId,
                AccountId = accountId,
                TemplateId = itemId
            }));
        }

        var items = await Task.WhenAll(itemsTasks);
        
        var validItems = items.Where(i => i != null).ToList();
        if (validItems.Count == 0)
            return Ok(ProfileResponseManager.Generate(profile, Array.Empty<object>(), profileId));

        var updateTasks = new List<Task>(validItems.Count);
        var profileChanges = new List<object>(validItems.Count);
        bool hasChanges = false;

        foreach (var item in validItems)
        {
            var itemValueObj = JObject.Parse(item.Value);
            if ((bool?)itemValueObj["item_seen"] == true)
                continue;
                
            itemValueObj["item_seen"] = true;
            item.Value = itemValueObj.ToString();
            
            updateTasks.Add(profileItemsRepository.UpdateAsync(item));
            
            profileChanges.Add(new
            {
                changeType = "itemAttrChanged",
                itemId = item.TemplateId,
                attributeName = "item_seen",
                attributeValue = true
            });
            
            hasChanges = true;
        }
        
        if (hasChanges)
        {
            profile.Revision++;
            updateTasks.Add(ProfileManager.UpdateProfileAsync(profile));
            await Task.WhenAll(updateTasks);
        }
        
        return Ok(ProfileResponseManager.Generate(profile, profileChanges, profileId));
    }
}