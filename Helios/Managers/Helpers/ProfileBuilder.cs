using System.Collections.Concurrent;
using System.Text.Json;
using Helios.Classes.MCP;
using Helios.Classes.Caching;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Profiles;
using Helios.Utilities;
using Helios.Utilities.Caching;
using Helios.Utilities.Extensions;
using Newtonsoft.Json;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Helios.Managers.Helpers;

public class ProfileBuilder : MCPProfile
{
    public ProfileBuilder(string accountId, Profiles profile, User user, List<Items> profileItems)
    {
        var timestamp = DateTime.UtcNow.ToIsoUtcString();
        Created = Updated = timestamp;

        SetupCoreProfile(accountId, profile, profileItems.Count);
        GenerateItems(profileItems);
    }

    private void SetupCoreProfile(string accountId, Profiles profile, int initialItemCapacity)
    {
        Rvn = profile.Revision;
        WipeNumber = 1;
        AccountId = accountId;
        ProfileId = profile.ProfileId;
        Version = "no_version";
        CommandRevision = profile.Revision;
        Items = new Dictionary<string, dynamic>(initialItemCapacity);
        Stats = new { attributes = new Dictionary<string, JsonElement>() };
    }

    private void GenerateItems(List<Items> profileItems)
    {
        for (int i = 0; i < profileItems.Count; i++)
        {
            var item = profileItems[i];
            if (string.IsNullOrEmpty(item.TemplateId)) continue;

            if (item.IsAttribute)
            {
                HandleAttribute(item);
            }
            else
            {
                HandleItem(item);
            }
        }
    }

    private void HandleItem(Items item)
    {
        if (string.IsNullOrEmpty(item.Value)) return;

        var parsed = ParseJson(item.Value);
        if (parsed is not JsonElement itemAttributes) return;

        string randomId = Guid.NewGuid().ToString();
        string templateId = item.TemplateId.Contains("loadout", StringComparison.Ordinal)
            ? "CosmeticLocker:cosmeticlocker_athena"
            : item.TemplateId;

        Items[randomId] = new
        {
            attributes = itemAttributes,
            templateId,
            quantity = item.Quantity
        };
    }

    private void HandleAttribute(Items item)
    {
        if (string.IsNullOrEmpty(item.Value)) return;

        var parsed = ParseJson(item.Value);
        if (parsed is not JsonElement parsedAttribute) return;

        Stats.attributes[item.TemplateId] = parsedAttribute;
    }

    private static JsonElement? ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            Logger.Error("JSON input is empty or null");
            return null;
        }

        return HeliosFastCache.GetOrAdd(json, () =>
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(json);
            }
            catch (JsonException ex)
            {
                Logger.Error($"Error deserializing JSON: {ex.Message} for input: {json}");
                return new JsonElement();
            }
        }, TimeSpan.FromMinutes(30));
    }
}