namespace Helios.Database.Repository.String;

public static class StringExtensions
{
    public static string ToSnakeCase(this string text)
    {
        return string.Concat(text.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + c.ToString() : c.ToString())).ToLower();
    }
}