using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/fortnite/api/storefront/v2/")]
public class StorefrontController : ControllerBase
{
    [HttpGet("keychain")]
    public async Task<IActionResult> GetKeychain()
    {
        Response.ContentType = "application/json";

        var keychainFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "keychain.json");
        if (!System.IO.File.Exists(keychainFile))
        {
            return NotFound();
        }

        var json = System.IO.File.ReadAllText(keychainFile);
        return Content(json);
    }
}