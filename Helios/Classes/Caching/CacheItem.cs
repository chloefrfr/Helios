namespace Helios.Classes.Caching;

public class CacheItem<T>
{
    public T Value { get; set; }
    public DateTime ExpiresAt { get; set; }   
}