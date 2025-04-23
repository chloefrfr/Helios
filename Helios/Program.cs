using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Helios.Configuration;
using Helios.Configuration.Services;
using Helios.Database.Tables.Account;
using Helios.Managers;
using Helios.Managers.Unreal;
using Helios.Services;
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
            var dbInitTask = Task.Run(() => Constants.dbContext.Initialize());
            
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ApplicationName = typeof(Program).Assembly.FullName,
                WebRootPath = "wwwroot"
            });

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.AddServerHeader = false;
                options.AllowSynchronousIO = false;
            });

            ServiceConfiguration.ConfigureServices(builder.Services, builder.Environment);
            LoggingConfiguration.ConfigureLogging(builder.Logging, builder.Configuration);
            WebhostConfiguration.ConfigureWebhosts(builder.WebHost);

            await dbInitTask;

            var app = builder.Build();

            Task fileProviderTask = InitializeFileProvider(app);

            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseAuthorization();

            ConfigureErrorHandling(app);
            
            app.UseMiddleware<RequestLoggingMiddleware>();
            app.UseMiddleware<ErrorHandlingMiddleware>();
            
            app.MapControllers();

            try 
            {
                await fileProviderTask;
            }
            catch (Exception ex)
            {
                Logger.Error($"[UnrealAssetProvider] Initialization completed with errors: {ex}");
            }
            
            var hotfixesCsv = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "hotfixes.csv");
            var importService = new CloudStorageImportService();
            await importService.ImportOrUpdateFromCsvAsync(hotfixesCsv);

            Logger.Info($"Helios is running on: {builder.Configuration["ASPNETCORE_URLS"]}");

            await app.RunAsync();
        }

        private static async Task InitializeFileProvider(WebApplication app)
        {
            try
            {
                Constants.FileProvider = app.Services.GetRequiredService<UnrealAssetProvider>();
                await Constants.FileProvider.InitializeAsync();
                await Constants.FileProvider.LoadAllCosmeticsAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Error($"[UnrealAssetProvider] Initialization failed: {ex}");
                throw;
            }
        }

        private static void ConfigureErrorHandling(WebApplication app)
        {
            var notFoundResponse = JsonSerializer.Serialize(BasicErrors.NotFound.Response);
            var methodNotAllowedResponse = JsonSerializer.Serialize(BasicErrors.MethodNotAllowed.Response);
            var serverErrorResponse = JsonSerializer.Serialize(InternalErrors.ServerError.Response);

            app.UseExceptionHandler(appBuilder => appBuilder.Run(async context =>
            {
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(serverErrorResponse);
            }));

            app.UseStatusCodePages(async context =>
            {
                var http = context.HttpContext;
                var response = http.Response;

                if (response.HasStarted) return;

                response.ContentType = "application/json";

                switch (response.StatusCode)
                {
                    case 404:
                        var path = http.Request.Path;
                        if (path.ToString().Contains("/fortnite/api/game/v2/profile") && path.HasValue)
                        {
                            var operationName = path.Value.Split('/').Last();
                            var errorResponse = JsonSerializer.Serialize(
                                MCPErrors.OperationNotFound.WithMessage($"Operation {operationName} not found.").Response);
                            await response.WriteAsync(errorResponse);
                        }
                        else
                        {
                            await response.WriteAsync(notFoundResponse);
                        }
                        break;
                    case 405:
                        await response.WriteAsync(methodNotAllowedResponse);
                        break;
                }
            });
        }
    }
}