using Helios.Classes.MCP.Response;
using Helios.Database.Tables.Profiles;
using Helios.Utilities.Extensions;

namespace Helios.Managers;

public class ProfileResponseManager
{
    public static BaseMCPResponse Generate(Profiles profile, IEnumerable<object> changes, string profileId)
    {
        return GenerateBaseResponse(profile, changes.ToList(), profileId);
    }
    
    private static BaseMCPResponse GenerateBaseResponse(Profiles profile, List<object> changes, string profileId)
    {
        return new BaseMCPResponse
        {
            ProfileRevision = profile.Revision,
            ProfileId = profileId,
            ProfileChangesBaseRevision = profile.Revision - 1,
            ProfileChanges = changes,
            ProfileCommandRevision = profile.Revision,
            ServerTime = DateTime.UtcNow.ToIsoUtcString(),
            ResponseVersion = 1
        };
    }
}