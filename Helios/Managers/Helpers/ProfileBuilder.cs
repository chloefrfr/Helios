using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Helios.Classes.MCP;
using Helios.Classes.Caching;
using Helios.Database.Tables.Account;
using Helios.Database.Tables.Profiles;
using Helios.Utilities;
using Helios.Utilities.Caching;
using Helios.Utilities.Extensions;

namespace Helios.Managers.Helpers;

public class ProfileBuilder : MCPProfile
{
    // Pre-initialize static resources
    private static readonly JsonDocumentOptions JsonOptions = new() { AllowTrailingCommas = true };
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    
    // Cache common strings to reduce allocations
    private const string LoadoutKeyword = "loadout";
    private const string CosmeticLockerType = "CosmeticLocker:cosmeticlocker_athena";
    private const string EmptyJson = "{}";
    
    public ProfileBuilder(string accountId, Profiles profile, User user, List<Items> profileItems)
    {
        // Get timestamp once
        var timestamp = DateTime.UtcNow.ToIsoUtcString();
        Created = Updated = timestamp;

        // Setup initial capacity based on items count
        SetupCoreProfile(accountId, profile, profileItems.Count);
        
        // Process all items at once
        GenerateItems(profileItems);
    }

    private void SetupCoreProfile(string accountId, Profiles profile, int initialItemCapacity)
    {
        // Set all properties directly
        Rvn = profile.Revision;
        WipeNumber = 1;
        AccountId = accountId;
        ProfileId = profile.ProfileId;
        Version = "no_version";
        CommandRevision = profile.Revision;
        
        // Initialize collections with appropriate capacity
        Items = new Dictionary<string, dynamic>(initialItemCapacity);
        Stats = new { attributes = new Dictionary<string, JsonElement>(initialItemCapacity / 3) }; // Estimate attribute count
    }

    private void GenerateItems(List<Items> profileItems)
    {
        // Process items in a single pass
        int count = profileItems.Count;
        for (int i = 0; i < count; i++)
        {
            var item = profileItems[i];
            
            // Skip invalid items early
            if (string.IsNullOrEmpty(item.TemplateId)) continue;
            if (string.IsNullOrEmpty(item.Value)) continue;

            // Process based on type
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
        var parsed = ParseJson(item.Value);
        
        bool isLoadout = item.TemplateId.Contains(LoadoutKeyword, StringComparison.Ordinal);
        string templateId = isLoadout ? CosmeticLockerType : item.TemplateId;
        
        if (!parsed.HasValue)
        {
            using var doc = JsonDocument.Parse(EmptyJson, JsonOptions);
            var attributes = doc.RootElement.Clone();
            
            Items[item.TemplateId] = new
            {
                attributes,
                templateId,
                quantity = item.Quantity
            };
        }
        else
        {
            Items[item.TemplateId] = new
            {
                attributes = parsed.Value,
                templateId,
                quantity = item.Quantity
            };
        }
    }

    private void HandleAttribute(Items item)
    {
        var parsed = ParseJson(item.Value);
        if (!parsed.HasValue) return;
        
        var fixedAttribute = FixNumericStrings(parsed.Value);
        Stats.attributes[item.TemplateId] = fixedAttribute;
    }
    
    private JsonElement FixNumericStrings(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object && element.ValueKind != JsonValueKind.Array)
        {
            return element.Clone();
        }
        
        var fixed_value = FixNumbers(element);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(fixed_value), JsonOptions);
        return doc.RootElement.Clone();
    }
    
    private object FixNumbers(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    prop => prop.Name,
                    prop => FixNumbers(prop.Value)
                ),
                
            JsonValueKind.Array => element.EnumerateArray()
                .Select(FixNumbers)
                .ToList(),
                
            JsonValueKind.String => 
                TryParseNumeric(element.GetString(), out var num) ? num : element.GetString(),
                
            _ => element.Clone()
        };
    }

    private bool TryParseNumeric(string? str, out object number)
    {
        if (string.IsNullOrEmpty(str))
        {
            number = str!;
            return false;
        }
        
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
            return null;
        }
        
        return HeliosFastCache.GetOrAdd(json, () =>
        {
            try
            {
                using var doc = JsonDocument.Parse(json, JsonOptions);
                return doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                return (JsonElement?)null;
            }
        }, CacheDuration);
    }
}