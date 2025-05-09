using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Microsoft.AspNetCore.Mvc;

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
            return AccountErrors.AccountNotFound(accountId)
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
                return AccountErrors.AccountNotFound(accountId)
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
}