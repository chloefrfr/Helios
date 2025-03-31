namespace Helios.Utilities.Errors.HeliosErrors;

public static class BasicErrors
{
    private const string Service = "com.helios.basic";

    public static ApiError BadRequest => new(
        "errors.com.epicgames.basic.badRequest",
        "Bad request", Service, 1001, 400);

    public static ApiError NotFound => new(
        "errors.com.epicgames.basic.notFound",
        "The resource you were trying to find could not be found.", Service, 1004, 404);

    public static ApiError NotAcceptable => new(
        "errors.com.epicgames.basic.notAcceptable",
        "Not acceptable", Service, 1008, 406);

    public static ApiError MethodNotAllowed => new(
        "errors.com.epicgames.basic.methodNotAllowed",
        "Method invalid", Service, 1009, 405);

    public static ApiError JsonMappingFailed => new(
        "errors.com.epicgames.basic.jsonMappingFailed",
        "JSON mapping failed", Service, 1019, 400);

    public static ApiError Throttled => new(
        "errors.com.epicgames.basic.throttled",
        "Rate limited", Service, 1041, 429);
}