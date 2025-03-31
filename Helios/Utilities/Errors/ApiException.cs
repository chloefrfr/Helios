using System.Runtime.Serialization;

namespace Helios.Utilities.Errors;

[Serializable]
public class ApiException : Exception
{
    public int StatusCode { get; }
    public string JsonContent { get; }

    public ApiException(int statusCode, string jsonContent) : base(jsonContent)
    {
        StatusCode = statusCode;
        JsonContent = jsonContent;
    }

    protected ApiException(SerializationInfo info, StreamingContext context) 
        : base(info, context)
    {
        StatusCode = info.GetInt32(nameof(StatusCode));
        JsonContent = info.GetString(nameof(JsonContent));
    }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(StatusCode), StatusCode);
        info.AddValue(nameof(JsonContent), JsonContent);
    }
}