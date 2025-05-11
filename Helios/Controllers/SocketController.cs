using Helios.Configuration;
using Helios.Database.Tables.Fortnite;
using Helios.Database.Tables.XMPP;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Helios.Controllers;

[ApiController]
[Route("/api/v1/h/socket")]
public class SocketController : ControllerBase
{
    [HttpGet("connected_users")]
    public async Task<IActionResult> ConnectedUsers()
    {
        var clientSessionsRepo = Constants.repositoryPool.For<ClientSessions>();
        var clients = await clientSessionsRepo.FindAllByTableAsync();
    
        var distinctJids = clients
            .Select(x => x.Jid)     
            .Distinct()
            .ToList();

        return Ok(distinctJids);
    }

    [HttpGet("active_parties")]
    public async Task<IActionResult> ActiveParties()
    {
        var pRepo = Constants.repositoryPool.For<Parties>();
        var parties = await pRepo.FindAllByTableAsync();
        
        var formattedParties = parties.Select(x => new
        {
            Id = x.PartyId,
            Members = JsonSerializer
                .Deserialize<List<PartyMember>>(x.Members)?
                .Select(m => m.AccountId).ToList()
        });

        return Ok(formattedParties);
    }
}