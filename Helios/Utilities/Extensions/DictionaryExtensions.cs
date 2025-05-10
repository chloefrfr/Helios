namespace Helios.Utilities.Extensions;

public static class DictionaryExtensions
{
    public static object GetSafeValue(this Dictionary<string, object> dict, string key)
    {
        return dict.TryGetValue(key, out var value) ? value : null;
    }
}