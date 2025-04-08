using System.Collections.Concurrent;
using Helios.Classes.Caching;

namespace Helios.Utilities.Caching;

public class HeliosFastCache
{
    private static readonly ConcurrentDictionary<string, object> _cache = new();
    private static readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(15);
    
    /// <summary>
    /// Gets an item from the cache if it exists and hasn't expired
    /// </summary>
    public static bool TryGet<T>(string key, out T value)
    {
        value = default;
            
        if (_cache.TryGetValue(key, out var item) && item is CacheItem<T> typedItem)
        {
            if (DateTime.UtcNow < typedItem.ExpiresAt)
            {
                value = typedItem.Value;
                return true;
            }
                
            // Remove expired item from cache
            _cache.TryRemove(key, out _);
        }
            
        return false;
    }
    
    /// <summary>
    /// Gets a value from cache or computes and adds it if not present
    /// </summary>
    public static T GetOrAdd<T>(string key, Func<T> valueFactory, TimeSpan? expiration = null)
    {
        if (TryGet<T>(key, out var value))
            return value;

        value = valueFactory();
            
        Set(key, value, expiration);
            
        return value;
    }
    
    /// <summary>
    /// Gets a value from cache asynchronously or computes and adds it if not present
    /// </summary>
    public static async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> valueFactory, TimeSpan? expiration = null)
    {
        if (TryGet<T>(key, out var value))
            return value;

        // For async operations, we need to handle the case where multiple threads
        // might try to compute the value simultaneously
        var lockKey = string.Intern($"lock_{key}");
            
        lock (lockKey)
        {
            if (TryGet<T>(key, out value))
                return value;
                
            value = valueFactory().GetAwaiter().GetResult();
                
            Set(key, value, expiration);
                
            return value;
        }
    }
    
    /// <summary>
    /// Sets a value in the cache with optional expiration
    /// </summary>
    public static void Set<T>(string key, T value, TimeSpan? expiration = null)
    {
        var expiresAt = DateTime.UtcNow.Add(expiration ?? _defaultExpiration);
            
        _cache[key] = new CacheItem<T>
        {
            Value = value,
            ExpiresAt = expiresAt
        };
    }
    
    
    /// <summary>
    /// Removes an item from the cache
    /// </summary>
    public static bool Remove(string key)
    {
        return _cache.TryRemove(key, out _);
    }

    /// <summary>
    /// Clears all items from the cache
    /// </summary>
    public static void Clear()
    {
        _cache.Clear();
    }
}