namespace Helios.Utilities.Errors.HeliosErrors;

public static class MatchmakingErrors
{
    private const string Service = "com.helios.matchmaking";

    public static ApiError UnknownSession => new(
        "errors.com.epicgames.matchmaking.unknownSession",
        "Unknown session", Service, 12101, 404);

    public static ApiError MissingCookie => new(
        "errors.com.epicgames.matchmaking.missingCookie",
        "Missing cookie", Service, 1001, 400);

    public static ApiError InvalidBucketId => new(
        "errors.com.epicgames.matchmaking.invalidBucketId",
        "Invalid bucket", Service, 16102, 400);

    public static ApiError InvalidPartyPlayers => new(
        "errors.com.epicgames.matchmaking.invalidPartyPlayers",
        "Invalid party", Service, 16103, 400);

    public static ApiError InvalidPlatform => new(
        "errors.com.epicgames.matchmaking.invalidPlatform",
        "Invalid platform", Service, 16104, 400);

    public static ApiError NotAllowedIngame => new(
        "errors.com.epicgames.matchmaking.notAllowedIngame",
        "Unauthorized items", Service, 16105, 400);
}