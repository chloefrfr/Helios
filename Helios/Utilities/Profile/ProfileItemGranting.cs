using System.Collections.Concurrent;
using System.Text.Json;
using CUE4Parse.Utils;
using Helios.Classes.MCP;
using Helios.Configuration;
using Helios.Database.Tables.Profiles;

namespace Helios.Utilities.Profile;

public class ProfileItemGranting
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions()
    {
        WriteIndented = true,
    };
    
    
    private static readonly Dictionary<string, string> CosmeticTypeMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["characters"] = "AthenaCharacter",
        ["backpacks"] = "AthenaBackpack",
        ["pickaxes"] = "AthenaPickaxe",
        ["dances"] = "AthenaDance",
        ["musicpacks"] = "AthenaMusicPack",
        ["pets"] = "AthenaBackpack",
        ["sprays"] = "AthenaDance",
        ["toys"] = "AthenaDance",
        ["loadingscreens"] = "AthenaLoadingScreen",
        ["gliders"] = "AthenaGlider",
        ["contrails"] = "AthenaSkyDiveContrail",
        ["petcarriers"] = "AthenaPetCarrier",
        ["battlebuses"] = "AthenaBattleBus",
        ["victoryposes"] = "AthenaVictoryPose",
        ["consumableemotes"] = "AthenaConsumableEmote",
        ["wraps"] = "AthenaItemWrap",
        ["itemwraps"] = "AthenaItemWrap"
    };

    public static async Task GrantAll(string accountId)
    {
        var profileItemsRepository = Constants.repositoryPool.Repo<Items>();

        var cosmeticFiles = await Constants.FileProvider.LoadAllCosmeticsAsync(CancellationToken.None); 
        var itemsToSave = new ConcurrentBag<Items>();
        
        await Parallel.ForEachAsync(cosmeticFiles, async (cosmeticPath, ct) =>
        {
            var cosmeticName = cosmeticPath.SubstringAfterLast("/").SubstringBefore(".");
            var cosmeticTypeKey = GetCosmeticTypeKey(cosmeticPath);

            if (!CosmeticTypeMapping.TryGetValue(cosmeticTypeKey, out var cosmeticType))
            {
                Logger.Error($"Unknown cosmetic type: {cosmeticName}");
                return;
            }

            var templateId = $"{cosmeticType}:{cosmeticName}";
            var existingItem = await profileItemsRepository().FindAsync(new Items
            {
                AccountId = accountId,
                ProfileId = "athena",
                TemplateId = templateId
            });
            if (existingItem != null)
                return;

            var newItem = new Items
            {
                AccountId = accountId,
                ProfileId = "athena",
                TemplateId = templateId,
                Value = JsonSerializer.Serialize(new
                {
                    item_seen = false,
                    variants = new List<Variants>(),
                    xp = 0,
                    favorite = false
                }),
                Quantity = 1,
                IsAttribute = false
            };
            
            itemsToSave.Add(newItem);
        }); 

        if (itemsToSave.Count > 0)
        {
            await profileItemsRepository().BulkInsertAsync(itemsToSave);
        }
    }
    
    private static string GetCosmeticTypeKey(string cosmeticPath)
    {
        var pathSegments = cosmeticPath.ToLower().Split('/');
        return pathSegments.Length > 5 ? pathSegments[5] : string.Empty;
    }
    
    // private static Items CreateItem(
    //     string accountId, 
    //     string templateId, 
    //     Dictionary<string, List<string>>? variants,
    //     string profileId = "athena",
    //     string? defaultValue = null)
    // {
    //     return new Items
    //     {
    //         AccountId = accountId,
    //         ProfileId = profileId,
    //         TemplateId = templateId,
    //         Value = defaultValue ?? JsonSerializer.Serialize(new
    //         {
    //             item_seen = false,
    //             variants = variants ?? new(),
    //             xp = 0,
    //             favorite = false
    //         }, JsonOptions),
    //         Quantity = 1,
    //         IsAttribute = false
    //     };
    // }
}