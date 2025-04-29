using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Helios.Socket.Admin;

public class AdminAuthService
{
      private readonly WebSocketConfiguration _config;
    private readonly ConcurrentDictionary<string, DateTime> _validTokens = new();
    
    public AdminAuthService(WebSocketConfiguration config)
    {
        _config = config;
        
        if (string.IsNullOrEmpty(_config.AdminPasswordHash))
        {
            _config.AdminPasswordHash = HashPassword(_config.AdminPassword);
            Logger.Warn("Using default admin password! Please change it in production.");
        }
    }
    
    public string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
    
    public bool VerifyPassword(string password)
    {
        var hash = HashPassword(password);
        return hash == _config.AdminPasswordHash;
    }
    
    public string GenerateAdminToken()
    {
        var tokenBytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(tokenBytes);
        }
        
        var token = Convert.ToBase64String(tokenBytes);
        _validTokens[token] = DateTime.UtcNow.AddMinutes(_config.AdminTokenExpirationMinutes);
        
        return token;
    }
    
    public bool ValidateAdminToken(string token)
    {
        if (string.IsNullOrEmpty(token) || !_validTokens.TryGetValue(token, out var expiry))
            return false;
            
        if (expiry < DateTime.UtcNow)
        {
            _validTokens.TryRemove(token, out _);
            return false;
        }
        
        return true;
    }
    
    public void RevokeToken(string token)
    {
        _validTokens.TryRemove(token, out _);
    }
    
    public void CleanExpiredTokens()
    {
        var now = DateTime.UtcNow;
        var expiredTokens = _validTokens.Where(kvp => kvp.Value < now).Select(kvp => kvp.Key).ToList();
        
        foreach (var token in expiredTokens)
        {
            _validTokens.TryRemove(token, out _);
        }
    }
}