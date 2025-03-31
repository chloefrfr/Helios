namespace Helios.Classes.Response;

public class StandardApiError
{
    public string Code { get; set; }
    public string Message { get; set; }
    public string Details { get; set; }
    public string InnerError { get; set; }
    public string TraceId { get; set; }
    public DateTime Timestamp { get; set; }
}