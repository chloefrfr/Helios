using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Helios.Configuration;
using Helios.Configuration.Services;
using Helios.Database.Tables.Account;
using Helios.Managers;
using Helios.Managers.Unreal;
using Helios.Utilities;
using Helios.Utilities.Errors.HeliosErrors;
using Helios.Utilities.Middleware;
using Microsoft.AspNetCore.Diagnostics;

namespace Helios
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Constants.dbContext.Initialize();
            
            var builder = WebApplication.CreateBuilder(args);
            
            ServiceConfiguration.ConfigureServices(builder.Services, builder.Environment);
            LoggingConfiguration.ConfigureLogging(builder.Logging, builder.Configuration);
            WebhostConfiguration.ConfigureWebhosts(builder.WebHost);
            
            var app = builder.Build();

            try
            {
                var assetProvider = app.Services.GetRequiredService<UnrealAssetProvider>();
                await assetProvider.InitializeAsync();
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize UnrealAssetProvider: {ex}");
            }
            
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
                var http = context.HttpContext;
                var response = http.Response;
                var path = http.Request.Path.ToString();

                if (response.HasStarted)
                    return;

                response.ContentType = "application/json";

                if (response.StatusCode == 404)
                {
        
                    string operation = path.Contains("/fortnite/api/game/v2/profile")
                        ? path.Split('/').Last()
                        : null;

                    var error = operation != null
                        ? MCPErrors.OperationNotFound.WithMessage($"Operation {operation} not found.")
                        : BasicErrors.NotFound;

                    await response.WriteAsync(JsonSerializer.Serialize(error.Response));
                }
                else if (response.StatusCode == 405)
                {
                    var error = BasicErrors.MethodNotAllowed;

                    await response.WriteAsync(JsonSerializer.Serialize(error.Response));
                }
            });
            
            var address = builder.Configuration["ASPNETCORE_URLS"];
            Logger.Info($"Helios is running on: {address}");

            app.Run();
        }
    }
}