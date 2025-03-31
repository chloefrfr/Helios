namespace Helios.Classes.Response;

public class ApiResponseOptions
{
    public bool ShowDetailedErrors { get; set; } = false;
    public bool EnableDebugMode { get; set; } = false;
    public ResponseContentType ResponseContentType { get; set; } = ResponseContentType.Json;
    public Dictionary<Type, string> ErrorCodeMapping { get; set; } = new();
}