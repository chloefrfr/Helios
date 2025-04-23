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
        
        Logger.Info($"[{context.Request.Method}] {context.Request.Path} - {sw.ElapsedMilliseconds}ms");
    }
}