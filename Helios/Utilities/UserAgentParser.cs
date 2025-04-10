using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Helios.Classes.UserAgent;
using Helios.Utilities.Caching;

namespace Helios.Utilities;

public class UserAgentParser
{
    private const string CacheKeyPrefix = "UA_PARSE_";
    
    private static readonly Regex BuildIdRegex1 = new Regex(@"CL-(\d+)", RegexOptions.Compiled);
    private static readonly Regex BuildIdRegex2 = new Regex(@"version=\d+\.\d+\.\d+-(\d+)", RegexOptions.Compiled);
    private static readonly Regex BuildIdRegex3 = new Regex(@"build=(\d+)", RegexOptions.Compiled);
    private static readonly Regex BuildStringRegex1 = new Regex(@"build=([^\s,]+)", RegexOptions.Compiled);
    private static readonly Regex BuildStringRegex2 = new Regex(@"\+\+Fortnite\+Release-([^+-]+)", RegexOptions.Compiled);

    /// <summary>
    /// Parses a user agent string and returns season information with caching
    /// </summary>
    public static SeasonInfo Parse(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            Logger.Error("User agent string is null or empty.");
            return null;
        }

        string cacheKey = $"{CacheKeyPrefix}{userAgent.GetHashCode()}";

        return HeliosFastCache.GetOrAdd(cacheKey, () => ParseInternal(userAgent));
    }

    /// <summary>
    /// Async version of Parse method
    /// </summary>
    public static Task<SeasonInfo> ParseAsync(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            Logger.Error("User agent string is null or empty.");
            return Task.FromResult<SeasonInfo>(null);
        }

        string cacheKey = $"{CacheKeyPrefix}{userAgent.GetHashCode()}";

        return HeliosFastCache.GetOrAddAsync(cacheKey, async () => 
        {
            return ParseInternal(userAgent);
        });
    }

    /// <summary>
    /// Internal parsing logic without caching
    /// </summary>
    private static SeasonInfo ParseInternal(string userAgent)
    {
        string buildId = GetBuildID(userAgent);
        string buildString = GetBuildString(userAgent);

        if (string.IsNullOrEmpty(buildId))
        {
            Logger.Error($"Failed to extract Build ID from user agent: {userAgent}");
            return null;
        }

        var seasonInfo = HandleValidBuild(new UserAgentInfo
        {
            BuildId = buildId,
            BuildString = buildString ?? string.Empty
        });

        return seasonInfo;
    }

    /// <summary>
    /// Extracts the Build ID (CL-XXXXXXX) dynamically from any user agent format.
    /// </summary>
    private static string GetBuildID(string userAgent)
    {
        var match = BuildIdRegex1.Match(userAgent);
        if (match.Success) return match.Groups[1].Value;

        match = BuildIdRegex2.Match(userAgent);
        if (match.Success) return match.Groups[1].Value;

        match = BuildIdRegex3.Match(userAgent);
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    /// <summary>
    /// Extracts the Build String dynamically, even if `build=` is missing.
    /// </summary>
    private static string GetBuildString(string userAgent)
    {
        var match = BuildStringRegex1.Match(userAgent);
        if (match.Success) return match.Groups[1].Value;

        match = BuildStringRegex2.Match(userAgent);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Determines the Fortnite season based on Build ID.
    /// </summary>
    private static SeasonInfo HandleValidBuild(UserAgentInfo userAgentInfo)
    {
        if (!int.TryParse(userAgentInfo.BuildId, out int netcl))
        {
            Logger.Error($"Invalid CL value: {userAgentInfo.BuildId}");
            return null;
        }

        int build = ParseBuildString(userAgentInfo.BuildString);

        string buildUpdate = string.Empty;
        var buildUpdateParts = userAgentInfo.BuildString.Split('-');

        if (buildUpdateParts.Length > 1)
        {
            buildUpdate = buildUpdateParts[1].Split('+')[0];
        }

        int season = build;
        string lobby = string.Empty;
        string background = string.Empty;

        if (double.IsNaN(netcl))
        {
            lobby = "LobbySeason0";
            season = 0;
            build = 0;
        }
        else if (netcl < 3724489)
        {
            lobby = "Season0";
            season = 0;
            build = 0;
        }
        else if (netcl <= 3790078)
        {
            lobby = "LobbySeason1";
            season = 1;
            build = 1;
        }
        else if (buildUpdate == userAgentInfo.BuildId || buildUpdate == "Cert")
        {
            season = 2;
            build = 2;
            lobby = "LobbyWinterDecor";
        }
        else if (season == 6)
        {
            background = "fortnitemares";
            lobby = $"Lobby{season}";
        }
        else if (season == 10)
        {
            background = "seasonx";
            lobby = $"Lobby{season}";
        }
        else
        {
            lobby = $"Lobby{season}";
            background = $"season{season}";
        }

        return new SeasonInfo
        {
            Season = season,
            Build = season,
            Netcl = netcl.ToString(),
            Lobby = lobby,
            Background = background,
            BuildUpdate = buildUpdate
        };
    }

    /// <summary>
    /// Parses the Build String to extract the build number.
    /// </summary>
    private static int ParseBuildString(string buildString)
    {
        return (int)Math.Floor(double.TryParse(buildString, out double build) ? build : 0);
    }
}