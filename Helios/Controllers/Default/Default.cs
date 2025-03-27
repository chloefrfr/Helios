using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers.Default;

[ApiController]
[Route("[controller]")]
public class Default : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return new JsonResult(new { success = true, message = "Default" }, new JsonSerializerOptions { WriteIndented = false });
    }
}