using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Helios.HTTP.Utilities.Classes;
using Helios.HTTP.Utilities.Interfaces;

namespace Helios.HTTP.Utilities.Parsers;

public class BodyParser : IBodyParser
{
    private readonly ParserOptions _options;
    private readonly int _maxBufferSize;

    private readonly ILogger<BodyParser> _logger;

    public BodyParser(ILogger<BodyParser> logger = null, ParserOptions options = null)
    {
        _logger = logger;
        _options = options ?? new ParserOptions();
        _maxBufferSize = _options.MaxBufferSize;
    }

    public async Task<IReadOnlyDictionary<string, string>> ParseAsync(HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        try
        {
            // Enable request buffering to allow multiple reads
            request.EnableBuffering();

            var contentType = request.ContentType?.ToLowerInvariant() ?? string.Empty;


            if (contentType.Contains("application/json"))
            {
                return await ParseJsonBodyAsync(request, cancellationToken);
            }
            else if (contentType.Contains("application/x-www-form-urlencoded"))
            {
                return await ParseFormUrlEncodedBodyAsync(request, cancellationToken);
            }
            else if (contentType.Contains("multipart/form-data"))
            {
                return await ParseMultipartFormDataAsync(request, cancellationToken);
            }
            else
            {
                return await ParseQueryStringBodyAsync(request, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error occurred while parsing request body");

            if (_options.ThrowOnError)
            {
                throw;
            }

            return new Dictionary<string, string>();
        }
        finally
        {
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
        }
    }


    private async Task<IReadOnlyDictionary<string, string>> ParseJsonBodyAsync(HttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (request.ContentLength == 0 || request.Body == null)
        {
            return result;
        }

        using var jsonDoc = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
        ExtractJsonProperties(jsonDoc.RootElement, string.Empty, result);

        return result;
    }

    private void ExtractJsonProperties(JsonElement element, string prefix, Dictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyName = string.IsNullOrEmpty(prefix)
                        ? property.Name
                        : $"{prefix}.{property.Name}";

                    ExtractJsonProperties(property.Value, propertyName, result);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var arrayItemName = $"{prefix}[{index}]";
                    ExtractJsonProperties(item, arrayItemName, result);
                    index++;
                }

                break;

            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                result[prefix] = element.ToString();
                break;
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> ParseFormUrlEncodedBodyAsync(HttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var pipe = PipeReader.Create(request.Body);

        try
        {
            while (true)
            {
                var readResult = await pipe.ReadAsync(cancellationToken);
                var buffer = readResult.Buffer;

                ProcessFormUrlEncodedBuffer(buffer, result);

                if (readResult.IsCompleted)
                {
                    break;
                }

                // Mark everything as consumed
                pipe.AdvanceTo(buffer.End);
            }

            return result;
        }
        finally
        {
            await pipe.CompleteAsync();
        }
    }

    private void ProcessFormUrlEncodedBuffer(ReadOnlySequence<byte> buffer, Dictionary<string, string> result)
    {
        var bodyAsString = buffer.ToArray().AsSpan();
        var form = System.Web.HttpUtility.ParseQueryString(Encoding.UTF8.GetString(bodyAsString));

        foreach (string key in form.AllKeys)
        {
            if (!string.IsNullOrEmpty(key))
            {
                result[key] = form[key];
            }
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> ParseMultipartFormDataAsync(HttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!request.HasFormContentType)
        {
            return result;
        }

        var form = await request.ReadFormAsync(cancellationToken);

        foreach (var key in form.Keys)
        {
            if (form.TryGetValue(key, out var values))
            {
                result[key] = values.ToString();
            }
        }

        return result;
    }

    private async Task<IReadOnlyDictionary<string, string>> ParseQueryStringBodyAsync(HttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var memoryStream = _options.UsePooledMemory
            ? new MemoryStream(ArrayPool<byte>.Shared.Rent(_maxBufferSize), 0, _maxBufferSize, true, true)
            : new MemoryStream();

        await request.Body.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        using var reader = new StreamReader(memoryStream);
        var bodyContent = await reader.ReadToEndAsync();

        var form = System.Web.HttpUtility.ParseQueryString(bodyContent);

        foreach (string key in form.AllKeys)
        {
            if (!string.IsNullOrEmpty(key))
            {
                result[key] = form[key];
            }
        }

        return result;
    }
}