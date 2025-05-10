using System.Text.Json;

namespace Helios.Utilities.Extensions;

public static class JsonExtensions
{
    public static T? FromJson<T>(this string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
    
    public static string? GetNestedString(this JsonElement element, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!element.TryGetProperty(key, out var property) || property.ValueKind != JsonValueKind.Object)
                return null;
            element = property;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }
}
