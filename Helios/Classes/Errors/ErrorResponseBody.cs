namespace Helios.Classes.Errors;

public class ErrorResponseBody
{
    public string ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
    public List<string> MessageVars { get; set; } = new();
    public int NumericErrorCode { get; set; }
    public string OriginatingService { get; set; }
    public string Intent { get; set; }
    public Dictionary<string, object> ValidationFailures { get; set; }
}