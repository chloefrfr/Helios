using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
public class WaitingRoomController : ControllerBase
{
    [HttpGet("/waitingroom/api/waitingroom")]
    public IActionResult WaitingRoom()
    {
        // return Ok(new
        // {
        //     retryTime = 4,
        //     expectedWait = 4
        // });
        return NoContent();
    }

    [HttpGet("/launcher-resources/waitingroom/Fortnite/retryconfig.json")]
    [HttpGet("/launcher-resources/waitingroom/retryconfig.json")]
    public IActionResult GetRetryConfig()
    {
        return Ok(new
        {
            maxRetryCount = 1,
            retryInterval = 10,
            retryJitter = 69,
            failAction = "ABORT"
        });
    }
}