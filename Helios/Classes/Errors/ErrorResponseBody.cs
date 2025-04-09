using System.Text.Json.Serialization;

namespace Helios.Classes.Errors;

public class ErrorResponseBody
{
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; }
    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; }
    [JsonPropertyName("messageVars")]
    public List<string> MessageVars { get; set; } = [];

    [JsonPropertyName("numericErrorCode")]
    public int NumericErrorCode { get; set; }
    [JsonPropertyName("originatingService")]
    public string OriginatingService { get; set; }
    [JsonPropertyName("intent")]
    public string Intent { get; set; }
    [JsonPropertyName("validationFailures")]
    public object? ValidationFailures { get; set; }
}