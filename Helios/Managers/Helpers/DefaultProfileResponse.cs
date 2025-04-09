using System.Collections.Concurrent;
using Helios.Classes.MCP;
using Helios.Utilities.Extensions;

namespace Helios.Managers.Helpers;

public class DefaultProfileResponse : MCPProfile
{
    public DefaultProfileResponse(string profileId, string accountId)
    {
        var timestamp = DateTime.UtcNow.ToIsoUtcString();
        Created = Updated = timestamp;

        SetupDefaultProfile(profileId, accountId);
    }

    public void SetupDefaultProfile(string profileId, string accountId)
    {
        Rvn = 0;
        WipeNumber = 1;
        AccountId = accountId;
        ProfileId = profileId;
        Version = "no_version";
        CommandRevision = 0;
        Items = new Dictionary<string, dynamic>();
        Stats = new { attributes = new ConcurrentDictionary<string, dynamic>() };
    }
}