namespace Helios.Utilities.Extensions;

public static class DateTimeExtensions
{
    public static string ToIsoUtcString(this DateTime dateTime)
    {
        return dateTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }

    public static long ToUnixTimeSeconds(this DateTime dateTime)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (long)(dateTime.ToUniversalTime() - epoch).TotalSeconds;
    }
    
    public static long ToUnixTimeMilliseconds(this DateTime dateTime)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return (long)(dateTime.ToUniversalTime() - epoch).TotalMilliseconds;
    }
    
    public static DateTime FromUnixTimeMilliseconds(long unixTimeMilliseconds)
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddMilliseconds(unixTimeMilliseconds);
    }
}