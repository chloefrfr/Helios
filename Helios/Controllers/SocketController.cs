using Helios.Configuration;
using Helios.Database.Tables.XMPP;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/api/v1/h/socket")]
public class SocketController : ControllerBase
{
    [HttpGet("connected_users")]
    public async Task<IActionResult> ConnectedUsers()
    {
        var clientSessionsRepo = Constants.repositoryPool.GetRepository<ClientSessions>();
        var clients = await clientSessionsRepo.FindAllByTableAsync();
    
        var distinctJids = clients
            .Select(x => x.Jid)     
            .Distinct()
            .ToList();

        return Ok(distinctJids);
    }
}