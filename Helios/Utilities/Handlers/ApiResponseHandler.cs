using System.Text.Json;
using Helios.Classes.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Helios.Utilities.Handlers;

public sealed class ApiResponseHandler
{
    private readonly ApiResponseOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly byte[] _fallbackErrorBytes;

    public ApiResponseHandler(IOptions<ApiResponseOptions> options)
    {
        _options = options.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        
        _fallbackErrorBytes = JsonSerializer.SerializeToUtf8Bytes(
            new StandardApiError { Code = "SERVER_ERROR", Message = "An error occurred" },
            _jsonOptions
        );
    }

    public Task HandleResponseAsync(HttpContext context, IActionResult result)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;

        try
        {
            if (result is ObjectResult objResult)
            {
                context.Response.StatusCode = objResult.StatusCode ?? 200;
                context.Response.ContentType = "application/json";
                return WriteValueDirectAsync(context, objResult.Value);
            }
            
            if (result is ContentResult contentResult)
            {
                context.Response.StatusCode = contentResult.StatusCode ?? 200;
                context.Response.ContentType = contentResult.ContentType ?? "application/json";
                
                if (contentResult.ContentType != "application/json")
                {
                    return context.Response.WriteAsync(contentResult.Content ?? "");
                }
                
                return WriteValueDirectAsync(context, contentResult.Content);
            }
            
            if (result is StatusCodeResult statusResult)
            {
                context.Response.StatusCode = statusResult.StatusCode;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{}");
            }
            
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            return WriteValueDirectAsync(context, new { success = true });
        }
        catch (Exception ex)
        {
            return HandleExceptionAsync(context, ex);
        }
    }

    public Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted) return Task.CompletedTask;

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        
        if (!_options.ShowDetailedErrors)
        {
            return context.Response.Body.WriteAsync(_fallbackErrorBytes, 0, _fallbackErrorBytes.Length);
        }

        var error = new StandardApiError
        {
            Code = GetErrorCode(exception),
            Message = exception.Message,
            TraceId = context.TraceIdentifier
        };
        
        return WriteValueDirectAsync(context, error);
    }

    private Task WriteValueDirectAsync(HttpContext context, object? value)
    {
        if (value == null)
        {
            return context.Response.WriteAsync("null");
        }
        
        if (value is string stringValue)
        {
            if ((stringValue.StartsWith("{") && stringValue.EndsWith("}")) || 
                (stringValue.StartsWith("[") && stringValue.EndsWith("]")))
            {
                return context.Response.WriteAsync(stringValue);
            }
        }
        
        try
        {
            var serialized = JsonSerializer.Serialize(value, _jsonOptions);
            return context.Response.WriteAsync(serialized);
        }
        catch
        {
            return context.Response.Body.WriteAsync(_fallbackErrorBytes, 0, _fallbackErrorBytes.Length);
        }
    }

    private string GetErrorCode(Exception ex) => 
        _options.ErrorCodeMapping.TryGetValue(ex.GetType(), out var code) ? code : "UNKNOWN";
}