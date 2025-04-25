using System.Security.Cryptography;
using System.Text;
using Helios.Classes;
using Helios.Classes.Errors;
using Helios.Configuration;
using Helios.Database.Repository;
using Helios.Database.Tables.Fortnite;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Controllers;

[ApiController]
[Route("/fortnite/api/cloudstorage/")]
public class CloudStorageController : ControllerBase
{
    private static readonly int CurrentSeason;
    private static readonly string ClientSettingsPath;
    private readonly Repository<CloudStorage> _cloudStorageRepository = Constants.repositoryPool.GetRepository<CloudStorage>();

    static CloudStorageController()
    {
        ClientSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Helios", 
            "ClientSettings");
        
        Directory.CreateDirectory(ClientSettingsPath);
        CurrentSeason = int.Parse(Constants.config.CurrentVersion.Split('.')[0]);
    }
    
    [HttpGet("system")]
    public async Task<IActionResult> GetCloudStorageFilesAsync()
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        var seasonData = UserAgentParser.Parse(userAgent);
        
        if (seasonData == null)
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);

        if (seasonData.Season != CurrentSeason)
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);

        var hotfixes = await _cloudStorageRepository.FindAllByTableAsync();
        var enabledHotfixes = hotfixes.Where(x => x.Enabled).ToList();
        if (enabledHotfixes.Count == 0)
            return MCPErrors.InvalidPayload.WithIntent(Intents.Prod).Apply(HttpContext);

        var uploadedTime = DateTime.UtcNow.ToIsoUtcString();
        
        using var sha1 = SHA1.Create();
        using var sha256 = SHA256.Create();
        
        var result = new List<CloudStorageFile>(enabledHotfixes.Count);
        
        foreach (var row in enabledHotfixes)
        {
            var valueBytes = Encoding.UTF8.GetBytes(row.Value);
            result.Add(new CloudStorageFile
            {
                UniqueFilename = row.Filename,
                Filename = row.Filename,
                Hash = Convert.ToHexString(sha1.ComputeHash(valueBytes)),
                Hash256 = Convert.ToHexString(sha256.ComputeHash(valueBytes)),
                Length = valueBytes.Length,
                ContentType = "application/octet-stream",
                Uploaded = uploadedTime,
                StorageType = "S3",
                DoNotCache = false
            });
        }

        return Ok(result);
    }
    
    [HttpGet("system/{filename}")]
    public async Task<IActionResult> GetFileByNameAsync(string filename)
    {
        var userAgent = Request.Headers["User-Agent"].ToString();
        var seasonData = UserAgentParser.Parse(userAgent);
        
        if (seasonData == null)
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);

        if (seasonData.Season != CurrentSeason)
            return InternalErrors.InvalidUserAgent.Apply(HttpContext);
        
        var hotfixes = await _cloudStorageRepository.FindAllByTableAsync();
        var enabledHotfix = hotfixes?.FirstOrDefault(x => x.Enabled && x.Filename == filename);
        if (enabledHotfix is null)
            return MCPErrors.InvalidPayload.WithIntent(Intents.Prod).Apply(HttpContext);

        return Content(enabledHotfix.Value, "text/plain");
    }
}