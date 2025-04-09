using System.Text.Json;
using Helios.Classes.MCP;
using Helios.Database.Tables.Profiles;

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
        return CreateItemBase(profileId, accountId, templateId);
    }

    public static Items CreateStatItem(string profileId, string accountId, string templateId, dynamic value)
    {
        return CreateItemBase(profileId, accountId, templateId, value, isAttribute: true);
    }
    
    public static Items CreateCCItem(string profileId, string accountId, string templateId)
    {
        var itemValue = new { platform = "EpicPC", level = 1 };

        int quantity = templateId == "Currency:MtxPurchased" ? 0 : 1;

        return CreateItemBase(profileId, accountId, templateId, itemValue, quantity);
    }
}