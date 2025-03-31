using Helios.Classes.Endpoints.Lightswitch;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/lightswitch/api/service/")]
public class LightswitchController : ControllerBase
{
    private static readonly List<string> DefaultCatalogIds = new() { "a7f138b2e51945ffbfdacc1af0541053" };
    private static readonly List<string> DefaultAllowedActions = new() { "Play", "Download" };
    private static readonly LauncherInfoDTO DefaultLauncherInfo = new()
    {
        AppName = "Fortnite",
        CatalogItemId = "4fe75bbc5a674f4f9b356b5c90567da5",
        Namespace = "fn"
    };
    
    [Route("bulk/status")]
    public async Task<IActionResult> GetAsync()
    {
        return Ok(new List<object>
        {
            new {
                serviceInstanceId = "fortnite",
                status = "Up",
                message = "Servers are UP!",
                overrideCatalogIds = DefaultCatalogIds,
                allowedActions = DefaultAllowedActions,
                banned = false,
                launcherInfoDTO = DefaultLauncherInfo
            }
        });
    }
}