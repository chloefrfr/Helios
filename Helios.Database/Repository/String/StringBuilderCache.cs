using System.Text;

namespace Helios.Database.Repository.String;

internal static class StringBuilderCache
{
    [ThreadStatic]
    private static StringBuilder _cachedInstance;
        
    public static StringBuilder Acquire(int capacity = 16)
    {
        var sb = _cachedInstance;
        if (sb == null) return new StringBuilder(capacity);
            
        _cachedInstance = null;
        sb.Clear();
        if (sb.Capacity < capacity) sb.Capacity = capacity;
        return sb;
    }
        
    public static string GetStringAndRelease(StringBuilder sb)
    {
        string result = sb.ToString();
        _cachedInstance = sb;
        return result;
    }
}