using System.Text.Json;
using Helios.Classes.MCP;
using Helios.Database.Tables.Profiles;
using Helios.Utilities.Caching;

namespace Helios.Utilities.Profile;

public static class ProfileItemCreator
{
    private static Items CreateItemBase(string profileId, string accountId, string templateId, object value = null, int quantity = 1, bool isAttribute = false)
    {
        var itemValue = value ?? new { xp = 0, level = 1, variants = new List<Variants>(), item_seen = false };

        return new Items
        {
            AccountId = accountId,
            ProfileId = profileId,
            TemplateId = templateId,
            Value = JsonSerializer.Serialize(itemValue),
            Quantity = quantity,
            IsAttribute = isAttribute
        };
    }
    
    public static Items CreateItem(string profileId, string accountId, string templateId)
    {
        string cacheKey = $"item:{profileId}:{accountId}:{templateId}";
        
        return HeliosFastCache.GetOrAdd(cacheKey, () => CreateItemBase(profileId, accountId, templateId));
    }

    public static Items CreateStatItem(string profileId, string accountId, string templateId, dynamic value)
    {
        // For stat items with dynamic values, caching may not be appropriate
        // since value could change frequently, but we'll implement a cache key that includes value hash
        string valueHash = value?.GetHashCode().ToString() ?? "null";
        string cacheKey = $"statitem:{profileId}:{accountId}:{templateId}:{valueHash}";
        
        return HeliosFastCache.GetOrAdd(cacheKey, () => CreateItemBase(profileId, accountId, templateId, value, isAttribute: true));
    }
    
    public static Items CreateCCItem(string profileId, string accountId, string templateId)
    {
        string cacheKey = $"ccitem:{profileId}:{accountId}:{templateId}";
        
        return HeliosFastCache.GetOrAdd(cacheKey, () => {
            var itemValue = new { platform = "EpicPC", level = 1 };
            int quantity = templateId == "Currency:MtxPurchased" ? 0 : 1;
            return CreateItemBase(profileId, accountId, templateId, itemValue, quantity);
        });
    }

    public static Items CreateLoadoutItem(string profileId, string accountId, string templateId)
    {
        string cacheKey = $"loadoutitem:{profileId}:{accountId}:{templateId}";
        
        return HeliosFastCache.GetOrAdd(cacheKey, () => {
            return CreateItemBase(profileId, accountId, templateId, new
            {
                favorite = false,
                item_seen = false,
                use_count = 0,
                locker_name = "Default Loadout",
                locker_slots_data = new
                {
                    slots = new
                    {
                        Dance = new
                        {
                            items = new[] { "AthenaDance:EID_DanceMoves", "", "", "", "", "" }
                        },
                        Glider = new
                        {
                            items = new[] { "AthenaGlider:DefaultGlider" }
                        },
                        Pickaxe = new
                        {
                            items = new[] { "AthenaPickaxe:DefaultPickaxe" },
                            activeVariants = new object[] { }
                        },
                        Backpack = new
                        {
                            items = new[] { "" },
                            activeVariants = new[]
                            {
                                new
                                {
                                    variants = new object[] { }
                                }
                            }
                        },
                        ItemWrap = new
                        {
                            items = new[] { "", "", "", "", "", "", "" },
                            activeVariants = new object[] { null, null, null, null, null, null, null }
                        },
                        Character = new
                        {
                            items = new[] { "AthenaCharacter:CID_001_Athena_Commando_F_Default" },
                            activeVariants = new[]
                            {
                                new
                                {
                                    variants = new object[] { }
                                }
                            }
                        },
                        MusicPack = new
                        {
                            items = new[] { "" },
                            activeVariants = new object[] { null }
                        },
                        LoadingScreen = new
                        {
                            items = new[] { "" },
                            activeVariants = new object[] { null }
                        },
                        SkyDiveContrail = new
                        {
                            items = new[] { "" },
                            activeVariants = new object[] { null }
                        }
                    }
                },
                banner_icon_template = "",
                banner_color_template = ""
            }, 1);
        });
    }
}