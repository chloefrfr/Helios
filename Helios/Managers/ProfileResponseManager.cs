using Helios.Classes.MCP.Response;
using Helios.Database.Tables.Profiles;
using Helios.Utilities.Caching;
using Helios.Utilities.Extensions;

namespace Helios.Managers;

public class ProfileResponseManager
{
    private static string GetCacheKey(string accountId, string profileId, int revision) => $"profile_response_{accountId}_{profileId}_{revision}";
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    
    public static BaseMCPResponse Generate(Profiles profile, IEnumerable<object> changes, string profileId)
    {
        string cacheKey = GetCacheKey(profile.AccountId, profileId, profile.Revision);
        
        return HeliosFastCache.GetOrAdd(cacheKey, 
            () => GenerateBaseResponse(profile, changes.ToList(), profileId),
            _cacheExpiration);
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