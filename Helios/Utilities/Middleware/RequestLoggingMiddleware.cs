using System.Diagnostics;

namespace Helios.Utilities.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/favicon.ico"))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        var statusCode = context.Response.StatusCode;

        Logger.Info($"[{context.Request.Method}] {context.Request.Path} responded {statusCode} in {sw.ElapsedMilliseconds}ms");
    }
}