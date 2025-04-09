using Helios.Configuration;
using Helios.Database.Tables.Account;
using Helios.Utilities;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Helios.Managers;

namespace Helios.Controllers;

[ApiController]
public class TestController : ControllerBase
{
    public class NewRegisteredUser
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string Username { get; set; }
        public string DiscordId { get; set; }
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync()
    {
        NewRegisteredUser? body;
        try
        {
            body = await HttpContext.Request.ReadFromJsonAsync<NewRegisteredUser>();
        }
        catch
        {
            Logger.Warn("Failed to deserialize request body.");
            return BadRequest(new { error = "failed to deserialize body." });
        }

        if (body is null || 
            string.IsNullOrWhiteSpace(body.Email) || 
            string.IsNullOrWhiteSpace(body.Password) || 
            string.IsNullOrWhiteSpace(body.Username) || 
            string.IsNullOrWhiteSpace(body.DiscordId))
        {
            return BadRequest(new { error = "invalid request" });
        }

        var userRepository = Constants.repositoryPool.GetRepository<User>();

        if (await userRepository.FindAsync(new User { Username = body.Username }) is not null)
        {
            return BadRequest(new { error = "user already exists" });
        }

        try
        {
            var (password, _) = PasswordHasher.HashPassword(body.Password);
            var user = new User
            {
                Email = body.Email,
                Password = password,
                Username = body.Username,
                DiscordId = body.DiscordId,
                AccountId = Guid.NewGuid().ToString(),
                Banned = false,
                AllItemsGranted = false
            };

            await userRepository.SaveAsync(user);

            var profiles = new List<string> { "athena", "common_core" };
            foreach (var profileId in profiles)
            {
                var profile = await ProfileManager.CreateProfileAsync(profileId, user.AccountId);
                if (profile != null)
                {
                    Logger.Info($"Successfully created {profileId} profile for account {user.AccountId}");
                }   
            }

            return Ok(new
            {
                Success = true,
                Message = "Successfully created account",
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Error creating account: {ex.Message}");
            return StatusCode(500);
        }
    }
}
