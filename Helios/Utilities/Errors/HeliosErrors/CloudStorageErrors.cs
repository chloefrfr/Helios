namespace Helios.Utilities.Errors.HeliosErrors;

public static class CloudStorageErrors
{
    private const string Service = "com.helios.cloudstorage";

    public static ApiError FileNotFound => new(
        "errors.com.epicgames.cloudstorage.fileNotFound",
        "File not found", Service, 12004, 404);

    public static ApiError FileTooLarge => new(
        "errors.com.epicgames.cloudstorage.fileTooLarge", 
        "File too large", Service, 12004, 413);

    public static ApiError InvalidAuth => new(
        "errors.com.epicgames.cloudstorage.invalidAuth",
        "Invalid auth", Service, 12004, 401);
}