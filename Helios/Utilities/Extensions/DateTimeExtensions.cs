namespace Helios.Utilities.Extensions;

public static class DateTimeExtensions
{
    public static string ToIsoUtcString(this DateTime dateTime)
    {
        return dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }
}