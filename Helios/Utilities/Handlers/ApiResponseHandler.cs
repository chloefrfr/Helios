using System.Text.Json;
using Helios.Classes.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Helios.Utilities.Handlers;

public class ApiResponseHandler
{
    private readonly ApiResponseOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private static readonly JsonSerializerOptions _errorSerializerOptions = new() 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    };

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
            Logger.Warn("Response already started, aborting handling");
            return;
        }

        try
        {
            var (statusCode, value) = result switch
            {
                ContentResult content => (content.StatusCode ?? StatusCodes.Status200OK, content.Content),
                ObjectResult obj => (obj.StatusCode ?? StatusCodes.Status200OK, obj.Value),
                StatusCodeResult status => (status.StatusCode, null),
                _ => (StatusCodes.Status500InternalServerError, "Unknown result type")
            };

            context.Response.StatusCode = statusCode;
            await SerializeResponse(context, value);
        }
        catch (Exception ex)
        {
            Logger.Error("Response handling failed", ex);
            await HandleExceptionAsync(context, ex);
        }
    }

    public async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            Logger.Warn("Response already started, aborting error handling");
            return;
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await SerializeResponse(context, CreateStandardError(context, exception));
    }

    private async Task SerializeResponse(HttpContext context, object? value)
    {
        context.Response.ContentType = "application/json";
        
        try
        {
            await JsonSerializer.SerializeAsync(context.Response.Body, value, _jsonOptions);
        }
        catch (Exception ex)
        {
            Logger.Error("Response serialization failed", ex);
            
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await JsonSerializer.SerializeAsync(
                    context.Response.Body,
                    CreateFallbackError(context, ex),
                    _errorSerializerOptions
                );
            }
        }
    }

    private StandardApiError CreateStandardError(HttpContext context, Exception exception)
    {
        var error = new StandardApiError
        {
            Code = GetErrorCode(exception),
            Message = _options.ShowDetailedErrors ? exception.Message : "An error occurred",
            TraceId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow
        };

        if (_options.ShowDetailedErrors)
        {
            error.Details = exception.StackTrace;
            error.InnerError = exception.InnerException?.Message;
        }

        Logger.Error($"API Error {error.Code}: {error.Message}", exception);
        return error;
    }

    private StandardApiError CreateFallbackError(HttpContext context, Exception exception) =>
        new()
        {
            Code = "SERIALIZATION_FAILURE",
            Message = "Failed to process response",
            TraceId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow
        };

    private string GetErrorCode(Exception exception)
    {
        var exceptionType = exception.GetType();
        return _options.ErrorCodeMapping.TryGetValue(exceptionType, out var code) 
            ? code 
            : "UNKNOWN_ERROR";
    }
}