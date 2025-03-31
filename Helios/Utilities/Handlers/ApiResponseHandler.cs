using System.Text.Json;
using Helios.Classes.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Helios.Utilities.Handlers;

public class ApiResponseHandler
{
    private readonly ApiResponseOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public ApiResponseHandler(IOptions<ApiResponseOptions> options)
    {
        _options = options.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _options.EnableDebugMode
        };
    }

    public async Task HandleResponseAsync(HttpContext context, IActionResult result)
    {
        
        if (context.Response.HasStarted)
        {
            Logger.Warn("Response has already started, aborting response handling");
            return;
        }

        try
        {
            var (statusCode, value) = result switch
            {
                ObjectResult obj => (obj.StatusCode ?? StatusCodes.Status500InternalServerError, obj.Value),
                JsonResult json => (json.StatusCode ?? StatusCodes.Status400BadRequest, json.Value),
                ContentResult content => (content.StatusCode ?? StatusCodes.Status200OK, content.Content),
                StatusCodeResult status => (status.StatusCode, (object)null),
                _ => (StatusCodes.Status500InternalServerError, "Unknown result type")
            };

            context.Response.StatusCode = statusCode;
            await SerializeResponse(context, value);
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to handle API response", ex);
            await HandleException(context, ex);
        }
    }
    
    public async Task HandleException(HttpContext context, Exception exception)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        var error = CreateStandardError(context, exception);
        await SerializeResponse(context, error);
    }

    private async Task SerializeResponse(HttpContext context, object value)
    {
        context.Response.ContentType = _options.ResponseContentType switch
        {
            ResponseContentType.Json => "application/json",
            ResponseContentType.Xml => "application/xml",
            _ => "application/json"
        };

        if (value is string strValue)
        {
            await context.Response.WriteAsync(strValue);
            return;
        }

        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, value, _jsonOptions);
        stream.Position = 0;
        await stream.CopyToAsync(context.Response.Body);
    }
    
    private StandardApiError CreateStandardError(HttpContext context, Exception exception)
    {
        var error = new StandardApiError
        {
            Code = _options.ErrorCodeMapping.TryGetValue(exception.GetType(), out var code) 
                ? code 
                : "UNKNOWN_ERROR",
            Message = _options.ShowDetailedErrors ? exception.Message : "An error occurred",
            TraceId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow
        };

        if (_options.ShowDetailedErrors)
        {
            error.Details = exception.StackTrace;
            error.InnerError = exception.InnerException?.Message;
        }

        Logger.Error($"API Error: {error.Code} - {error.Message}", exception);
        return error;
    }
}