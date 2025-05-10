using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/party/api/v1/")]
public class PartyController : ControllerBase
{
    [HttpGet("Fortnite/user/{accountId}")]
    public async Task<IActionResult> GetUser(string accountId)
    {
        return NoContent();
    }
}