﻿using System.Collections.Concurrent;
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
    
        if (!parsed.HasValue) 
        {
            string templateId = item.TemplateId.Contains("loadout", StringComparison.Ordinal)
                ? "CosmeticLocker:cosmeticlocker_athena"
                : item.TemplateId;

            using var doc = JsonDocument.Parse("{}");
            var attributes = doc.RootElement.Clone();
        
            Items[templateId] = new
            {
                attributes,
                templateId,
                quantity = item.Quantity
            };
            return;
        }

        string itemTemplateId = item.TemplateId.Contains("loadout", StringComparison.Ordinal)
            ? "CosmeticLocker:cosmeticlocker_athena"
            : item.TemplateId;

        Items[itemTemplateId] = new
        {
            attributes = parsed.Value,
            templateId = itemTemplateId,
            quantity = item.Quantity
        };
    }

    private void HandleAttribute(Items item)
    {
        if (string.IsNullOrEmpty(item.Value)) return;

        var parsed = ParseJson(item.Value);
        if (!parsed.HasValue) return; 
        var fixedAttribute = FixNumericStrings(parsed.Value); // fixes season_num being a string
        Stats.attributes[item.TemplateId] = fixedAttribute;
    }
    
    private JsonElement FixNumericStrings(JsonElement element)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(FixNumbers(element)));
        return doc.RootElement.Clone();
    }
    
    private object FixNumbers(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                prop => prop.Name,
                prop => FixNumbers(prop.Value)
            ),
            JsonValueKind.Array => element.EnumerateArray().Select(FixNumbers).ToList(),
            JsonValueKind.String => TryParseNumeric(element.GetString(), out var num) ? num : element.GetString(),
            _ => element.Clone()
        };
    }

    private bool TryParseNumeric(string? str, out object number)
    {
        if (int.TryParse(str, out var intVal))
        {
            number = intVal;
            return true;
        }
        if (double.TryParse(str, out var doubleVal))
        {
            number = doubleVal;
            return true;
        }

        number = str!;
        return false;
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
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                return (JsonElement?)null;
            }
        }, TimeSpan.FromMinutes(30));
    }
}