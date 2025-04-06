namespace Helios.Utilities.Errors.HeliosErrors;

public static class InternalErrors
{
    private const string Service = "com.helios.internal";

    public static ApiError ValidationFailed(string fields) => new(
        "errors.com.epicgames.internal.validationFailed",
        "Validation failed: {0}", Service, 1040, 400, fields);

    public static ApiError UnknownRoute => new(
        "errors.com.epicgames.common.not_found",
        "Route not found", Service, 1004, 404);

    public static ApiError InvalidUserAgent => new(
        "errors.com.epicgames.internal.invalidUserAgent",
        "Invalid User-Agent", Service, 16183, 400);

    public static ApiError ServerError => new(
        "errors.com.epicgames.internal.serverError",
        "Server error", Service, 1000, 500);

    public static ApiError JsonParsingFailed => new(
        "errors.com.epicgames.internal.jsonParsingFailed",
        "JSON parse failed", Service, 1020, 400);

    public static ApiError RequestTimedOut => new(
        "errors.com.epicgames.internal.requestTimedOut",
        "Timeout", Service, 1001, 408);

    public static ApiError UnsupportedMediaType => new(
        "errors.com.epicgames.internal.unsupportedMediaType",
        "Bad media type", Service, 1006, 415);

    public static ApiError NotImplemented => new(
        "errors.com.epicgames.internal.notImplemented",
        "Not implemented", Service, 1001, 501);

    public static ApiError DatabaseError => new(
        "errors.com.epicgames.internal.databaseError",
        "DB error", Service, 1001, 500);

    public static ApiError UnknownError => new(
        "errors.com.epicgames.internal.unknownError",
        "Unknown error", Service, 1001, 500);

    public static ApiError EosError => new(
        "errors.com.epicgames.internal.EosError",
        "EOS error", Service, 1001, 500);
}