using Helios.Configuration;
using Helios.Configuration.Services;
using Helios.Utilities;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Helios
{
    public class Program
    {
        static void Main(string[] args)
        {
            Constants.dbContext.Initialize();
            
            var builder = WebApplication.CreateBuilder(args);

            ServiceConfiguration.ConfigureServices(builder.Services);
            LoggingConfiguration.ConfigureLogging(builder.Logging, builder.Configuration);
            
            var app = builder.Build();

            app.UseHttpsRedirection();
            app.MapGet("/", () => "Hello, World!");

            var address = builder.Configuration["ASPNETCORE_URLS"];
            Logger.Info($"Helios is running on: {address}");

            app.Run();
        }
    }
}