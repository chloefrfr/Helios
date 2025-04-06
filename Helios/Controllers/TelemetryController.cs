using Helios.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
public class TelemetryController : ControllerBase
{
    [HttpPost("/datarouter/api/v1/public/data")]
    public IActionResult ReceiveData()
    {
        var sessionId = HttpContext.Request.Query["SessionID"].ToString();
        var appId = HttpContext.Request.Query["AppID"].ToString();
        var appVersion = HttpContext.Request.Query["AppVersion"].ToString();
        var userId = HttpContext.Request.Query["UserID"].ToString();
        var appEnvironment = HttpContext.Request.Query["AppEnvironment"].ToString();
        var uploadType = HttpContext.Request.Query["UploadType"].ToString();

        Logger.Info($"Received telemetry data: SessionID: {sessionId}, AppID: {appId}, AppVersion: {appVersion}, UserID: {userId}, AppEnvironment: {appEnvironment}, UploadType: {uploadType}");
        
        return Ok();
    }
}