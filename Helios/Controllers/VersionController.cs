using Helios.Configuration;
using Helios.Utilities.Errors.HeliosErrors;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/fortnite/api/")]
public class VersionController : ControllerBase
{
    [HttpGet("versioncheck")]
    [HttpGet("v2/versioncheck")]
    public IActionResult Check()
    {
        string route = Constants.ExtractSanitiztedRoute(Request.Path);
        
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        if (string.IsNullOrEmpty(userAgent))
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);
        
        return Ok(new
        {
            type = "NO_UPDATE"
        });
    }

    [HttpGet("v2/versioncheck/{version}")]
    public IActionResult CheckByVersion(string version)
    {
        Response.ContentType = "application/json";

        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        if (string.IsNullOrEmpty(userAgent))
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);
        
        return Ok(new
        {
            type = "NO_UPDATE"
        });
    }
}