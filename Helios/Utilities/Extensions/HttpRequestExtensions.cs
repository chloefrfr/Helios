using System.Buffers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Utilities.Extensions;

public static class HttpRequestExtensions
{
    public static async Task<JsonDocument?> TryReadJsonAsync(this HttpRequest request, JsonDocumentOptions options, Func<HttpContext, IActionResult> onError)
    {
        try
        {
            if (request.ContentLength.HasValue && request.ContentLength.Value < 4096)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent((int)request.ContentLength.Value);
                try
                {
                    int bytesRead = await request.Body.ReadAsync(buffer.AsMemory(0, (int)request.ContentLength.Value));
                    return JsonDocument.Parse(buffer.AsMemory(0, bytesRead), options);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            else
            {
                using var memoryStream = new MemoryStream();
                await request.Body.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                return JsonDocument.Parse(memoryStream, options);
            }
        }
        catch (JsonException)
        {
            onError(request.HttpContext);
            return null;
        }
    }
}