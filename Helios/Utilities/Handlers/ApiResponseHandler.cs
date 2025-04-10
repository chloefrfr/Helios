using System.Text.Json;
using System.Text.Json.Serialization;
using Helios.Classes.Response;
using Helios.Utilities.Handlers.Wrappers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Helios.Utilities.Handlers;

public sealed class ApiResponseHandler : IDisposable
{
    private readonly ApiResponseOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerContext _serializerContext;
    private readonly byte[] _fallbackErrorBytes;
    private bool _disposed;

    public ApiResponseHandler(IOptions<ApiResponseOptions> options)
    {
        _options = options.Value;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _options.EnableDebugMode
        };
        
        _serializerContext = new SourceGenerationContext(_jsonOptions);
        
        _fallbackErrorBytes = JsonSerializer.SerializeToUtf8Bytes(
            new StandardApiError { Code = "SERVER_ERROR", Message = "An error occurred" },
            _serializerContext.Options
        );
    }

    public async ValueTask HandleResponseAsync(HttpContext context, IActionResult result)
    {
        if (context.Response.HasStarted) return;

        try
        {
            int statusCode;
            object? value;

            switch (result)
            {
                case ContentResult contentResult:
                    statusCode = contentResult.StatusCode ?? 200;
                    value = contentResult.Content;
                    break;

                case ObjectResult objectResult:
                    statusCode = objectResult.StatusCode ?? 200;
                    value = objectResult.Value;
                    break;

                case StatusCodeResult statusCodeResult:
                    statusCode = statusCodeResult.StatusCode;
                    value = null;
                    break;

                default:
                    statusCode = 500;
                    value = "Invalid result type";
                    break;
            }

            context.Response.StatusCode = statusCode;
            await Serialize(context, value);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    public async ValueTask HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = 500;
        await Serialize(context, CreateErrorPayload(context, exception));
    }

    private async ValueTask Serialize(HttpContext context, object? value)
    {
        context.Response.ContentType = "application/json";
        
        try
        {
            if (value is string str)
            {
                await context.Response.WriteAsync(str);
                return;
            }

            using var bufferWriter = new PooledByteBufferWriter(4096);
            using (var writer = new Utf8JsonWriter(bufferWriter))
            {
                JsonSerializer.Serialize(writer, value, value?.GetType() ?? typeof(object), _serializerContext);
            }

            await context.Response.Body.WriteAsync(bufferWriter.WrittenMemory);
        }
        catch
        {
            if (!context.Response.HasStarted)
            {
                await context.Response.Body.WriteAsync(_fallbackErrorBytes);
            }
        }
    }

    private StandardApiError CreateErrorPayload(HttpContext context, Exception exception)
    {
        var error = new StandardApiError
        {
            Code = GetErrorCode(exception),
            TraceId = context.TraceIdentifier,
            Timestamp = DateTime.UtcNow
        };

        if (_options.ShowDetailedErrors)
        {
            error.Message = exception.Message;
            error.Details = exception.StackTrace;
            error.InnerError = exception.InnerException?.Message;
        }
        else
        {
            error.Message = "An error occurred";
        }

        return error;
    }

    private string GetErrorCode(Exception ex) => 
        _options.ErrorCodeMapping.TryGetValue(ex.GetType(), out var code) ? code : "UNKNOWN";

    public void Dispose()
    {
        if (!_disposed)
        {
            (_serializerContext as IDisposable)?.Dispose();
            _disposed = true;
        }
    }
}
