using Helios.Utilities.Exceptions;
using Helios.Utilities.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace Helios.Utilities.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public ErrorHandlingMiddleware(RequestDelegate next, IServiceScopeFactory serviceScopeFactory)
    {
        _next = next;
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            var responseHandler = scope.ServiceProvider.GetRequiredService<ApiResponseHandler>();

            try
            {
                await _next(context);
            
                if (context.Response.StatusCode >= 400)
                {
                    await responseHandler.HandleResponseAsync(context, 
                        new StatusCodeResult(context.Response.StatusCode));
                }
            }
            catch (ApiErrorException aex)
            {
                context.Response.Clear();
                aex.Error.ApplyToResponse(context);
            }
            catch (Exception ex)
            {
                await responseHandler.HandleExceptionAsync(context, ex);
            }
        }
    }
}