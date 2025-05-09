using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text.Json;
using System.Threading.Tasks;
using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Profiles;
using Helios.Managers;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using JsonException = System.Text.Json.JsonException;

namespace Helios.Controllers.MCP;

[ApiController]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/EquipBattleRoyaleCustomization")]
public class EquipBattleRoyaleCustomization : ControllerBase
{
    private static readonly HashSet<string> ValidSlotNames = new(StringComparer.Ordinal)
    {
        "Character", "Backpack", "Pickaxe", "Glider", "SkyDiveContrail", "MusicPack", "LoadingScreen", "Dance", "ItemWrap"
    };

    [HttpPost]
    public async Task<IActionResult> Init(
        [FromRoute] string accountId,
        [FromQuery] string profileId)
    {
        if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(accountId))
            return MCPErrors.InvalidPayload.Apply(HttpContext);

        var reader = Request.BodyReader;
        var result = await reader.ReadAsync();
        var buffer = result.Buffer;

        if (buffer.IsEmpty)
            return MCPErrors.InvalidPayload.Apply(HttpContext);

        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(buffer);
            reader.AdvanceTo(buffer.End);
        }
        catch (JsonException)
        {
            reader.AdvanceTo(buffer.Start, buffer.End);
            return MCPErrors.InvalidPayload.Apply(HttpContext);
        }

        var root = json.RootElement;

        if (!root.TryGetProperty("itemToSlot", out var itemToSlotProperty)
            || !root.TryGetProperty("slotName", out var slotNameProperty)
            || !root.TryGetProperty("indexWithinSlot", out var indexWithinSlotProperty))
            return MCPErrors.InvalidPayload.Apply(HttpContext);

        string slotName = slotNameProperty.GetString();
        
        if (!ValidSlotNames.Contains(slotName))
            return MCPErrors.InvalidPayload.Apply(HttpContext);

        string itemToSlot = itemToSlotProperty.GetString();
        if (itemToSlot?.StartsWith("item:") == true)
            itemToSlot = itemToSlot[5..]; 
        
        int indexWithinSlot = indexWithinSlotProperty.GetInt32();

        var userRepository = Constants.repositoryPool.For<User>();
        var profilesRepository = Constants.repositoryPool.For<Profiles>();
        var profileItemsRepository = Constants.repositoryPool.For<Items>();

        var userTask = userRepository.FindAsync(new User { AccountId = accountId });
        var profileTask = profilesRepository.FindAsync(new Profiles { ProfileId = profileId, AccountId = accountId });

        string statKey = GetStatKey(slotName, indexWithinSlot);
        
        Task<Items> itemTask = null;
        if (itemToSlot != null && !itemToSlot.Contains("_random") && itemToSlot != "")
        {
            itemTask = profileItemsRepository.FindAsync(new Items
            {
                ProfileId = profileId,
                AccountId = accountId,
                TemplateId = itemToSlot
            });
        }

        Task<Items> statItemTask = null;
        if (statKey != null)
        {
            statItemTask = profileItemsRepository.FindAsync(new Items
            {
                ProfileId = profileId,
                AccountId = accountId,
                TemplateId = statKey
            });
        }

        var initialTasks = new List<Task> { userTask, profileTask };
        if (itemTask != null) initialTasks.Add(itemTask);
        if (statItemTask != null) initialTasks.Add(statItemTask);
        
        await Task.WhenAll(initialTasks);

        var user = await userTask;
        var profile = await profileTask;

        if (user is null || profile is null)
            return AccountErrors.AccountNotFound(accountId).Apply(HttpContext);

        var profileChanges = new List<object>(2);
        
        var item = itemTask != null ? await itemTask : null;
        var statItem = statItemTask != null ? await statItemTask : null;

        if (statItem?.IsAttribute != true)
            return Ok(ProfileResponseManager.Generate(profile, profileChanges, profileId));

        if (item != null)
        {
            UpdateStatItem(indexWithinSlot, slotName, item, statItem, profileChanges);
        }
        else if (itemToSlot != null && (itemToSlot.Contains("_random") || itemToSlot == ""))
        {
            if (slotName == "Dance" || slotName == "ItemWrap")
            {
                UpdateArraySlot(indexWithinSlot, slotName, itemToSlot, statItem, profileChanges);
            }
            else
            {
                statItem.Value = itemToSlot;
                profileChanges.Add(new
                {
                    changeType = "statModified",
                    name = $"favorite_{slotName.ToLower()}",
                    value = itemToSlot
                });
            }
        }

        if (profileChanges.Count > 0)
        {
            await profileItemsRepository.UpdateAsync(statItem);
            
            profile.Revision++;
            await ProfileManager.UpdateProfileAsync(profile);
        }

        return Ok(ProfileResponseManager.Generate(profile, profileChanges, profileId));
    }

    private static string GetStatKey(string slotName, int? indexWithinSlot) => slotName switch
    {
        "Dance" when indexWithinSlot is >= 0 and <= 6 => "favorite_dance",
        "ItemWrap" => "favorite_itemwraps",
        "Character" or "Backpack" or "Pickaxe" or "Glider" or "SkyDiveContrail" or "MusicPack" or "LoadingScreen" 
            => $"favorite_{slotName.ToLower()}",
        _ => null
    };

    private static void UpdateArraySlot(int indexWithinSlot, string slotName, string itemToSlot, Items statItem, List<object> profileChanges)
    {
        string statName = slotName == "Dance" ? "favorite_dance" : "favorite_itemwraps";
        string[] valueArray = SplitOrCreateArray(statItem.Value as string, 7);

        if (indexWithinSlot >= 0 && indexWithinSlot < valueArray.Length)
        {
            valueArray[indexWithinSlot] = itemToSlot;
        }

        statItem.Value = string.Join(',', valueArray);
        profileChanges.Add(new
        {
            changeType = "statModified",
            name = statName,
            value = valueArray
        });
    }

    private static void UpdateStatItem(int indexWithinSlot, string slotName, Items item, Items statItem, List<object> profileChanges)
    {
        switch (slotName)
        {
            case "Dance":
                UpdateDanceSlot(indexWithinSlot, item, statItem, profileChanges);
                break;

            case "ItemWrap":
                UpdateItemWrapSlot(indexWithinSlot, item, statItem, profileChanges);
                break;

            default:
                statItem.Value = item.TemplateId;
                profileChanges.Add(new
                {
                    changeType = "statModified",
                    name = $"favorite_{slotName.ToLower()}",
                    value = item.TemplateId
                });
                break;
        }
    }

    private static void UpdateDanceSlot(int indexWithinSlot, Items item, Items statItem, List<object> profileChanges)
    {
        if (indexWithinSlot < 0 || indexWithinSlot > 6 || string.IsNullOrEmpty(item.TemplateId))
        {
            string[] valueArray = SplitOrCreateArray(statItem.Value as string, 7);
            
            if (indexWithinSlot >= 0 && indexWithinSlot < valueArray.Length)
            {
                valueArray[indexWithinSlot] = string.Empty;
            }

            statItem.Value = string.Join(',', valueArray);
            profileChanges.Add(new
            {
                changeType = "statModified",
                name = "favorite_dance",
                value = valueArray
            });
        }
        else
        {
            string[] valueArray = SplitOrCreateArray(statItem.Value as string, 7);
            
            if (indexWithinSlot >= 0 && indexWithinSlot < valueArray.Length)
            {
                valueArray[indexWithinSlot] = item.TemplateId ?? "";
            }
            else
            {
                Array.Resize(ref valueArray, indexWithinSlot + 1);
                valueArray[indexWithinSlot] = item.TemplateId ?? "";
            }

            statItem.Value = string.Join(',', valueArray);
            profileChanges.Add(new
            {
                changeType = "statModified",
                name = "favorite_dance",
                value = valueArray
            });
        }
    }

    private static void UpdateItemWrapSlot(int indexWithinSlot, Items item, Items statItem, List<object> profileChanges)
    {
        string[] valueArray = SplitOrCreateArray(statItem.Value as string, 7);

        if (indexWithinSlot == -1)
        {
            // Fill all slots with the same item
            string itemId = item.TemplateId ?? "";
            for (int i = 0; i < 7; i++)
            {
                valueArray[i] = itemId;
            }
        }
        else if (indexWithinSlot < 0 || indexWithinSlot > 6 || string.IsNullOrEmpty(item.TemplateId))
        {
            if (indexWithinSlot >= 0 && indexWithinSlot < valueArray.Length)
            {
                valueArray[indexWithinSlot] = "";
            }
        }
        else
        {
            if (indexWithinSlot >= 0 && indexWithinSlot < valueArray.Length)
            {
                valueArray[indexWithinSlot] = item.TemplateId ?? "";
            }
            else
            {
                Array.Resize(ref valueArray, indexWithinSlot + 1);
                valueArray[indexWithinSlot] = item.TemplateId ?? "";
            }
        }

        statItem.Value = string.Join(',', valueArray);
        profileChanges.Add(new
        {
            changeType = "statModified",
            name = "favorite_itemwraps",
            value = valueArray
        });
    }

    private static string[] SplitOrCreateArray(string value, int defaultSize)
    {
        if (string.IsNullOrEmpty(value))
            return new string[defaultSize];
        
        return value.Split(',');
    }
}