using System.Diagnostics;
using System.Net;
using Helios.Configuration;
using Helios.Configuration.Services;
using Helios.Database.Tables.Account;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Middleware;
using Microsoft.AspNetCore.Diagnostics;

namespace Helios
{
    public class Program
    {
        static void Main(string[] args)
        {
            Constants.dbContext.Initialize();
            
            var builder = WebApplication.CreateBuilder(args);
            
            ServiceConfiguration.ConfigureServices(builder.Services, builder.Environment);
            LoggingConfiguration.ConfigureLogging(builder.Logging, builder.Configuration);
            WebhostConfiguration.ConfigureWebhosts(builder.WebHost);
            
            var app = builder.Build();

            app.UseCors("AllowAll");
            app.UseHttpsRedirection();
            app.UseAuthorization();
            
            app.MapControllers();
            app.UseHttpsRedirection();
            app.UseMiddleware<RequestLoggingMiddleware>();
            app.UseMiddleware<ErrorHandlingMiddleware>();
            app.UseExceptionHandler(err => err.Run(async context =>
            {
                context.Response.StatusCode = 500;
                InternalErrors.ServerError.Apply(context);
            }));

            app.UseStatusCodePages(async context =>
            {
                var response = context.HttpContext.Response;
                var requestPath = context.HttpContext.Request.Path.ToString();

                if (response.StatusCode == 404)
                {
                    (requestPath.Contains("/fortnite/api/game/v2/profile") ? MCPErrors.OperationNotFound : BasicErrors.NotFound)
                        .Apply(context.HttpContext);
                }
                else if (response.StatusCode == 405)
                {
                    BasicErrors.MethodNotAllowed.Apply(context.HttpContext);
                }
            });
            
            var address = builder.Configuration["ASPNETCORE_URLS"];
            Logger.Info($"Helios is running on: {address}");

            app.Run();
        }
    }
}