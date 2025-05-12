using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Utilities.Extensions;
using Newtonsoft.Json;

namespace Helios.Managers;

public class FriendsManager
{
    public static async Task CreateFriendshipAsync(string accountId, string friendId)
    {
        await Constants.repositoryPool.Repo<Friends>()().SaveAsync(new Friends
        {
            AccountId = accountId,
            FriendId = friendId,
            Status = "PENDING",
            Direction = accountId == friendId ? "OUTBOUND" : "INBOUND",
            CreatedAt = DateTime.UtcNow.ToIsoUtcString(),
            Alias = string.Empty
        });
    }
    
    public static object CreateStanzaPayload(string accountId, string status, string direction, string timestamp)
    {
        return new
        {
            payload = new
            {
                accountId,
                status,
                direction,
                created = timestamp,
                favorite = false
            },
            type = "com.epicgames.friends.core.apiobjects.Friend",
            timestamp
        };
    }

    public static async Task SendStanzaAsync(string toAccountId, object stanza)
    {
        string json = JsonConvert.SerializeObject(stanza);
        await Constants.GlobalXmppClientService.ForwardStanzaAsync(toAccountId, json);
    }
}