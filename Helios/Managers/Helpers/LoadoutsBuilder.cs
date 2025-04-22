using Helios.Classes.MCP;
using Helios.Database.Tables.Profiles;
using Helios.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Helios.Managers.Helpers;

public class LoadoutsBuilder
{
    private static readonly Dictionary<string, (string PropertyName, int MaxItems)> _slotDefinitions = new()
    {
        { "Character", ("CharacterId", 1) },
        { "Backpack", ("BackpackId", 1) },
        { "Pickaxe", ("PickaxeId", 1) },
        { "Glider", ("GliderId", 1) },
        { "SkyDiveContrail", ("ContrailId", 1) },
        { "MusicPack", ("MusicPackId", 1) },
        { "LoadingScreen", ("LoadingScreenId", 1) },
        { "Dance", ("DanceId", 6) },
        { "ItemWrap", ("ItemWrapId", 7) }
    };

    public static string GetDatabaseId(string slotName)
    {
        return _slotDefinitions.TryGetValue(slotName, out var definition) 
            ? definition.PropertyName 
            : null;
    }

    private static List<string> GetItemsForSlot(string dbId, List<Loadouts> loadouts, int maxItems)
    {
        var items = new List<string>();
        var property = typeof(Loadouts).GetProperty(dbId);
        
        if (property == null)
        {
            Logger.Error($"Property not found: {dbId}");
            return Enumerable.Repeat<string>(null, maxItems).ToList();
        }

        foreach (var loadout in loadouts)
        {
            var value = property.GetValue(loadout);
            if (value == null) continue;

            if (value is IEnumerable<object> collection)
            {
                foreach (var item in collection.Where(i => i != null))
                {
                    items.Add(item.ToString());
                    if (items.Count >= maxItems) break;
                }
            }
            else
            {
                items.Add(value.ToString());
            }

            if (items.Count >= maxItems) break;
        }

        return items.Take(maxItems)
                   .Concat(Enumerable.Repeat<string>(null, maxItems - items.Count))
                   .Take(maxItems)
                   .ToList();
    }

    public static Dictionary<string, dynamic> Build(List<Loadouts> loadouts)
    {
        if (loadouts == null)
            throw new ArgumentNullException(nameof(loadouts));

        var lockerLoadout = new Dictionary<string, dynamic>();

        try
        {
            foreach (var loadout in loadouts.Where(l => l != null && !string.IsNullOrEmpty(l.TemplateId)))
            {
                var templateId = loadout.TemplateId;
                var slots = _slotDefinitions.Keys.ToArray();
                var slotsResult = BuildSlots(slots, loadouts);

                lockerLoadout[templateId] = new
                {
                    templateId = templateId,
                    attributes = new
                    {
                        locker_slots_data = new
                        {
                            slots = slotsResult
                        },
                        use_count = 0,
                        banner_color_template = loadout.BannerColorId ?? "DefaultColor",
                        banner_icon_template = loadout.BannerId ?? "StandardBanner",
                        locker_name = loadout.LockerName ?? "My Loadout",
                        item_seen = false,
                        favorite = false,
                        last_updated = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    },
                    quantity = 1
                };
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error building loadouts: {ex.Message}");
            throw;
        }

        return lockerLoadout;
    }

    private static Dictionary<string, dynamic> BuildSlots(string[] slots, List<Loadouts> loadouts)
    {
        var result = new Dictionary<string, dynamic>();

        foreach (var slotName in slots)
        {
            try
            {
                var dbId = GetDatabaseId(slotName);
                if (string.IsNullOrEmpty(dbId))
                {
                    Logger.Warn($"No database ID found for slot: {slotName}");
                    continue;
                }

                var maxItems = _slotDefinitions[slotName].MaxItems;
                var items = GetItemsForSlot(dbId, loadouts, maxItems);

                result[slotName] = new
                {
                    items = items,
                    activeVariants = new List<Variants>()
                };
            }
            catch (Exception ex)
            {
                Logger.Warn($"Error building slot {slotName}: {ex.Message}");
            }
        }

        return result;
    }
}