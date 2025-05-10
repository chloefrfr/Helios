using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Managers;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Extensions;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Helios.Controllers;

[ApiController]
[Route("/friends/api/")]
public class FriendsController : ControllerBase
{
    private const string StatusAccepted = "ACCEPTED";
    private const string StatusPending = "PENDING";
    private const string DirectionOutbound = "OUTBOUND";
    private const string DirectionInbound = "INBOUND";
    
    private static readonly Dictionary<string, HashSet<string>> _recentPlayers = new();
    private static readonly Random _random = new();
    
    [HttpGet("public/list/fortnite/{accountId}/recentPlayers")]
    public async Task<IActionResult> GetRecentPlayers(string accountId)
    {
        var uRepo = Constants.repositoryPool.For<User>();
        var users = await uRepo.FindAllByTableAsync();

        var distinctUsernames = users
            .Where(u => !string.IsNullOrWhiteSpace(u.Username) && u.AccountId != accountId) 
            .Select(u => u.Username)
            .Distinct()
            .ToList();

        if (distinctUsernames.Count == 0)
        {
            return BasicErrors.BadRequest.WithMessage("No valid usernames found.").Apply(HttpContext);
        }

        if (!_recentPlayers.TryGetValue(accountId, out var playerSet))
        {
            playerSet = new HashSet<string>();
            _recentPlayers[accountId] = playerSet;
        }

        var randomIndex = _random.Next(distinctUsernames.Count);
        var randomPlayer = distinctUsernames[randomIndex];

        playerSet.Add(randomPlayer);

        return Ok(playerSet.ToList());
    }
    
    [HttpGet("v1/{accountId}/settings")]
    public IActionResult GetSettings(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return BasicErrors.BadRequest.WithMessage("Invalid account id").Apply(HttpContext);
        
        return Ok(new
        {
            acceptInvites = "public", // public, friends_of_friends, private
            mutualPrivacy = "ALL" // ALL, FRIENDS, NONE
        });
    }
    
    [HttpGet("public/blocklist/{accountId}")]
    public async Task<IActionResult> GetPublicBlocklist(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return BasicErrors.BadRequest.WithMessage("Invalid account id").Apply(HttpContext);

        var friends = await Constants.repositoryPool.For<Friends>()
            .FindAllAsync(new Friends { AccountId = accountId, Status = "BLOCKED" });

        if (friends == null || friends.Count() == 0)
            return FriendsErrors.AccountNotFound
                .WithMessage($"No blocked friends found for accountId: {accountId}")
                .Apply(HttpContext);

        var blockedUsers = friends.Select(f => new
        {
            f.AccountId,
            f.CreatedAt
        });

        return Ok(new { blockedUsers });
    }
    
    [HttpGet("public/friends/{accountId}")]
    public async Task<IActionResult> GetPublicFriends(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return BasicErrors.BadRequest.WithMessage("Invalid account id").Apply(HttpContext);

        try
        {
            var friends = await Constants.repositoryPool.For<Friends>()
                .FindAllAsync(new Friends { AccountId = accountId});

            if (friends == null || friends.Count() == 0)
                return FriendsErrors.AccountNotFound
                    .WithMessage($"No friends found for accountId: {accountId}")
                    .Apply(HttpContext);

            var result = friends.Select(f => new
            {
                accountId = f.FriendId,
                created = f.CreatedAt,
                direction = f.Direction,
                favorite = false,
                status = f.Status
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error while fetching friends for accountId {accountId}: {ex.Message}");
            return InternalErrors.ServerError.Apply(HttpContext);
        }
    }
    
    [HttpGet("v1/{accountId}/recent/{type}")]
    public IActionResult GetRecentByType(string accountId, string type)
    {
        return NoContent();
    }

    [HttpPost("v1/{accountId}/friends/{friendId}")]
    [HttpPost("v1/friends/{accountId}/{friendId}")]
    [HttpPost("public/friends/{accountId}/{friendId}")]
    public async Task<IActionResult> AddFriend(string accountId, string friendId)
    {
        if (string.IsNullOrWhiteSpace(accountId) || string.IsNullOrWhiteSpace(friendId))
            return BasicErrors.BadRequest.WithMessage("Invalid account id or friend id").Apply(HttpContext);

        var userRepo = Constants.repositoryPool.For<User>();
        var friendRepo = Constants.repositoryPool.For<Friends>();

        var user = await userRepo.FindAsync(new User { AccountId = accountId});
        var friend = await userRepo.FindAsync(new User { AccountId = friendId });

        if (user == null || friend == null)
            return AccountErrors.AccountNotFound(accountId)
                .WithMessage("User or friend not found.")
                .Apply(HttpContext);
        
        try
        {
            var existingFriendEntry = await friendRepo.FindAsync(new Friends { AccountId = friendId, FriendId = accountId });
            var reverseFriendEntry = await friendRepo.FindAsync(new Friends { AccountId = accountId, FriendId = friendId });
            var timestamp = DateTime.UtcNow.ToIsoUtcString();

            if (existingFriendEntry != null && reverseFriendEntry != null)
            {
                if (existingFriendEntry.Status == StatusAccepted)
                    return FriendsErrors.RequestAlreadySent
                        .WithMessage($"Friendship between {accountId} and {friendId} already exists.")
                        .Apply(HttpContext);
                
                if (existingFriendEntry.Status == StatusPending)
                {
                    existingFriendEntry.Status = reverseFriendEntry.Status = StatusAccepted;
                    existingFriendEntry.Direction = reverseFriendEntry.Direction = DirectionOutbound;

    
                    await Task.WhenAll(
                        friendRepo.UpdateAsync(existingFriendEntry),
                        friendRepo.UpdateAsync(reverseFriendEntry)
                    );

                    var outboundStanza = FriendsManager.CreateStanzaPayload(friend.AccountId, "ACCEPTED", "OUTBOUND", timestamp);
                    var inboundStanza = FriendsManager.CreateStanzaPayload(user.AccountId, "ACCEPTED", "OUTBOUND", timestamp);

                    await Task.WhenAll(
                        FriendsManager.SendStanzaAsync(user.AccountId, outboundStanza),
                        FriendsManager.SendStanzaAsync(friend.AccountId, inboundStanza),

                        Constants.GlobalXmppClientService.ForwardPresenceStanzaAsync(user.AccountId, friend.AccountId, false),
                        Constants.GlobalXmppClientService.ForwardPresenceStanzaAsync(friend.AccountId, user.AccountId, false)
                    );

                    return NoContent();
                }
            }
            
            await Task.WhenAll(
                FriendsManager.CreateFriendshipAsync(user.AccountId, friend.AccountId),
                FriendsManager.CreateFriendshipAsync(friend.AccountId, user.AccountId)
            );

            var outboundPendingStanza = FriendsManager.CreateStanzaPayload(friend.AccountId, "PENDING", "OUTBOUND", timestamp);
            var inboundPendingStanza = FriendsManager.CreateStanzaPayload(user.AccountId, "PENDING", "INBOUND", timestamp);

            await Task.WhenAll(
                FriendsManager.SendStanzaAsync(user.AccountId, outboundPendingStanza),
                FriendsManager.SendStanzaAsync(friend.AccountId, inboundPendingStanza)
            );

            return NoContent();
        }
        catch (Exception ex)
        {
            Logger.Error($"Error creating friendship between {accountId} and {friendId}: {ex.Message}");
            return InternalErrors.ServerError.Apply(HttpContext);
        }
    }
}