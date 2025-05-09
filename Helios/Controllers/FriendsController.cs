using Helios.Configuration;
using Helios.Database.Tables.Account;
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
        return Ok(new
        {
            acceptInvites = "public"
        });
    }
}