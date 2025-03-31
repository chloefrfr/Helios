using System.Diagnostics;
using Helios.Configuration;
using Helios.Configuration.Services;
using Helios.Database.Tables.Account;
using Helios.Utilities;
using Helios.Utilities.Middleware;  

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
            
            var address = builder.Configuration["ASPNETCORE_URLS"];
            Logger.Info($"Helios is running on: {address}");

            app.Run();
        }
    }
}