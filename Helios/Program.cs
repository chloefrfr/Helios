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
                Constants.FileProvider = app.Services.GetRequiredService<UnrealAssetProvider>();
                await Constants.FileProvider.InitializeAsync();
                await Constants.FileProvider.LoadAllCosmeticsAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Error($"[UnrealAssetProvider] Initialization failed: {ex}");
            }

            app.UseMiddleware<RequestLoggingMiddleware>();
            app.UseMiddleware<ErrorHandlingMiddleware>();

            app.UseExceptionHandler(appBuilder => appBuilder.Run(async context =>
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(InternalErrors.ServerError.Response));
            }));

            app.UseStatusCodePages(async context =>
            {
                var http = context.HttpContext;
                var path = http.Request.Path;
                var response = http.Response;

                if (response.HasStarted) return;

                response.ContentType = "application/json";

                object error = response.StatusCode switch
                {
                    404 => path.ToString().Contains("/fortnite/api/game/v2/profile") && path.HasValue
                        ? MCPErrors.OperationNotFound.WithMessage($"Operation {path.Value.Split('/').Last()} not found.").Response
                        : BasicErrors.NotFound.Response,
                    405 => BasicErrors.MethodNotAllowed.Response,
                    _ => null
                };

                if (error != null)
                    await response.WriteAsync(JsonSerializer.Serialize(error));
            });

            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseAuthorization();
            app.MapControllers();

            Logger.Info($"Helios is running on: {builder.Configuration["ASPNETCORE_URLS"]}");

            await app.RunAsync();
        }
    }
}