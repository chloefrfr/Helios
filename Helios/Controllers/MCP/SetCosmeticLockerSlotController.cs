using System.Text.Json;
using System.Buffers;
using Helios.Classes.MCP;
using Helios.Configuration;
using Helios.Database.Repository;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Profiles;
using Helios.Managers;
using Helios.Managers.Helpers;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using JsonException = System.Text.Json.JsonException;

namespace Helios.Controllers.MCP;

[ApiController]
[Route("/fortnite/api/game/v2/profile/{accountId}/client/SetCosmeticLockerSlot")]
public class SetCosmeticLockerSlotController : ControllerBase
{
    private static readonly Dictionary<string, Action<Loadouts, string>> CategoryPropertyMap = new(9)
    {
        { "CharacterId", (loadout, item) => loadout.CharacterId = item },
        { "BackpackId", (loadout, item) => loadout.BackpackId = item },
        { "PickaxeId", (loadout, item) => loadout.PickaxeId = item },
        { "GliderId", (loadout, item) => loadout.GliderId = item },
        { "ContrailId", (loadout, item) => loadout.ContrailId = item },
        { "LoadingScreenId", (loadout, item) => loadout.LoadingScreenId = item },
        { "MusicPackId", (loadout, item) => loadout.MusicPackId = item },
        { "BannerId", (loadout, item) => loadout.BannerId = item },
        { "BannerColorId", (loadout, item) => loadout.BannerColorId = item }
    };

    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNameCaseInsensitive = true 
    };

    [HttpPost]
    public async Task<IActionResult> SetCosmeticLockerSlot(
        [FromRoute] string accountId,
        [FromQuery] string profileId,
        [FromHeader(Name = "User-Agent")] string userAgent)
    {
        if (userAgent is null)
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);

        if (string.IsNullOrEmpty(profileId) || string.IsNullOrEmpty(accountId))
            return MCPErrors.InvalidPayload.Apply(HttpContext);
        
        var bodyResult = await ParseRequestBodyAsync();
        if (!bodyResult.Success)
            return MCPErrors.InvalidPayload.Apply(HttpContext);
            
        var root = bodyResult.JsonElement;

        if (!root.TryGetProperty("itemToSlot", out var itemToSlotProperty) ||
            !root.TryGetProperty("category", out var categoryProperty) ||
            !root.TryGetProperty("slotIndex", out var slotIndexProperty) ||
            !root.TryGetProperty("lockerItem", out var lockerItemProperty))
        {
            return MCPErrors.InvalidPayload.Apply(HttpContext);
        }

        string category = categoryProperty.GetString();
        string lockerItem = lockerItemProperty.GetString();
        string itemToSlot = itemToSlotProperty.GetString();
        
        if (itemToSlot?.StartsWith("item:") == true)
            itemToSlot = itemToSlot.AsSpan(5).ToString();
        
        int slotIndex = slotIndexProperty.GetInt32();

        var userRepository = Constants.repositoryPool.GetRepository<User>();
        var profilesRepository = Constants.repositoryPool.GetRepository<Profiles>();
        var profileItemsRepository = Constants.repositoryPool.GetRepository<Items>();
        var loadoutsRepository = Constants.repositoryPool.GetRepository<Loadouts>();
        
        var userTask = userRepository.FindAsync(new User { AccountId = accountId });
        var profileTask = profilesRepository.FindAsync(new Profiles { ProfileId = profileId, AccountId = accountId });
            
        var loadoutTask = loadoutsRepository.FindAsync(new Loadouts { AccountId = accountId, ProfileId = profileId, LockerName = lockerItem });
        
        Task<Items> itemTask = null;
        if (!string.IsNullOrEmpty(lockerItem))
        {
            itemTask = profileItemsRepository.FindAsync(new Items
            {
                ProfileId = profileId,
                AccountId = accountId,
                TemplateId = lockerItem
            });
        }
        
        var tasks = new List<Task>(4);
        tasks.Add(userTask);
        tasks.Add(profileTask);
        tasks.Add(loadoutTask);
        if (itemTask != null) tasks.Add(itemTask);
        
        await Task.WhenAll(tasks);
        
        var user = await userTask;
        var profile = await profileTask;
        var loadout = await loadoutTask;

        if (user is null || profile is null || loadout is null)
            return AccountErrors.AccountNotFound(accountId).Apply(HttpContext);
        
        var item = itemTask != null ? await itemTask : null;
        
        if (item == null && (string.IsNullOrEmpty(itemToSlot) || itemToSlot.Contains("_random")))
        {
            return await HandleRandomItemAsync(accountId, profileId, category, slotIndex, itemToSlot, profile, profileItemsRepository);
        }
        
        if (item == null)
            return MCPErrors.ItemNotFound.WithMessage("Item not found.").Apply(HttpContext);
            
        var locker = JsonConvert.DeserializeObject<LoadoutDefinition>(item.Value);
        var lockerSlotsData = locker.locker_slots_data;
        
        if (!lockerSlotsData.slots.ContainsKey(category))
            return MCPErrors.ItemNotFound.WithMessage($"Category {category} not found.").Apply(HttpContext);
        
        var categoryInDb = LoadoutsBuilder.GetDatabaseId(category);
        bool updated = false;
        
        if (CategoryPropertyMap.TryGetValue(categoryInDb, out var updateAction))
        {
            lockerSlotsData.slots[category].items = new List<string>(1) { itemToSlot };
            updateAction(loadout, itemToSlot);
            updated = true;
        }
        else if (categoryInDb == "DanceId")
        {
            updated = UpdateArrayBasedCategory(loadout, lockerSlotsData, category, itemToSlot, slotIndex, loadout.DanceId, 6);
        }
        else if (categoryInDb == "ItemWrapId")
        {
            updated = UpdateArrayBasedCategory(loadout, lockerSlotsData, category, itemToSlot, slotIndex, loadout.ItemWrapId, 7);
        }
        
        if (!updated)
            return MCPErrors.ItemNotFound.WithMessage($"Unknown category {category}.").Apply(HttpContext);
        
        var profileChanges = profile.Revision != 0 ? new List<object>(1) : new List<object>(0);
        Task updateLoadoutTask = loadoutsRepository.UpdateAsync(loadout);
        
        if (profile.Revision != 0)
        {
            profileChanges.Add(new
            {
                changeType = "itemAttrChanged",
                itemId = lockerItem,
                attributeName = "locker_slots_data",
                attributeValue = lockerSlotsData
            });
            
            item.Value = JsonConvert.SerializeObject(locker);
            var updateItemTask = profileItemsRepository.UpdateAsync(item);
            
            profile.Revision++;
            var updateProfileTask = ProfileManager.UpdateProfileAsync(profile);
            
            await Task.WhenAll(updateLoadoutTask, updateItemTask, updateProfileTask);
        }
        else
        {
            await updateLoadoutTask;
        }
        
        return Ok(ProfileResponseManager.Generate(profile, profileChanges, profileId));
    }
    
    private async Task<IActionResult> HandleRandomItemAsync(
        string accountId, 
        string profileId, 
        string category, 
        int slotIndex, 
        string itemToSlot,
        Profiles profile,
        Repository<Items> profileItemsRepository)
    {
        var randomLoadoutData = new LoadoutDefinition
        {
            locker_slots_data = new LockerSlotData
            {
                slots = new Dictionary<string, LockerSlot>
                {
                    [category] = new LockerSlot { items = new List<string> { itemToSlot } }
                }
            }
        };

        var item = new Items
        {
            AccountId = accountId,
            ProfileId = "athena",
            TemplateId = itemToSlot,
            Value = JsonConvert.SerializeObject(randomLoadoutData)
        };

        await profileItemsRepository.UpdateAsync(item).ConfigureAwait(false);

        profile.Revision++;
        await ProfileManager.UpdateProfileAsync(profile).ConfigureAwait(false);

        var response = ProfileResponseManager.Generate(profile, new List<object>
        {
            new {
                changeType = "item_updated",
                item = itemToSlot,
                category,
                slotIndex,
                loadout = randomLoadoutData
            }
        }, profileId);

        return Ok(response);
    }
    
    private bool UpdateArrayBasedCategory(Loadouts loadout, LockerSlotData lockerSlotsData, 
        string category, string itemToSlot, int slotIndex, string[] itemArray, int arraySize)
    {
        if (itemArray == null)
            itemArray = new string[arraySize];
            
        if (slotIndex == -1)
        {
            Array.Fill(itemArray, itemToSlot);
        }
        else if (slotIndex >= 0 && slotIndex < arraySize)
        {
            itemArray[slotIndex] = itemToSlot;
        }
        else
        {
            return false;
        }

        lockerSlotsData.slots[category].items = new List<string>(itemArray);
        return true;
    }
    
    private async Task<(bool Success, JsonElement JsonElement)> ParseRequestBodyAsync()
    {
        try
        {
            var reader = Request.BodyReader;
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            if (buffer.IsEmpty)
                return (false, default);

            var json = JsonDocument.Parse(buffer);
            reader.AdvanceTo(buffer.End);
            return (true, json.RootElement);
        }
        catch (JsonException)
        {
            return (false, default);
        }
    }
}