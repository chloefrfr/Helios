using Helios.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
public class TelemetryController : ControllerBase
{
    [HttpPost("/datarouter/api/v1/public/data")]
    public IActionResult ReceiveData()
    {
        return Ok();
    }
}