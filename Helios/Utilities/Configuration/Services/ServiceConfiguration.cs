using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;
using Helios.Classes.Response;
using Helios.HTTP.Utilities.Extensions;
using Helios.Managers;
using Helios.Managers.Unreal;
using Helios.Services;
using Helios.Utilities.Exceptions;
using Helios.Utilities.Handlers;
using Helios.XMPP;
using Serilog;

namespace Helios.Configuration.Services;

public static class ServiceConfiguration
{
    public static void ConfigureServices(IServiceCollection services, IWebHostEnvironment env)
    {
        services.AddMemoryCache();
        services.AddControllers();
        services.AddEndpointsApiExplorer();
        services.AddHttpContextAccessor();
        services.AddBodyParser(options =>
        {
            options.MaxBufferSize = 2 * 1024 * 1024; 
            options.UsePooledMemory = true;
            options.ThrowOnError = true;
        });
        
        services.AddScoped<ApiResponseHandler>();
        services.AddSingleton<XmppClient>();
        services.AddSingleton<PartyManager>();
        
        services.AddLogging(builder => builder.AddSerilog());

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context =>
                {
                    var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        clientIp,
                        partition => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 40,
                            Window = TimeSpan.FromSeconds(10),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0,
                        }
                    );
                }
            );

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                await context.HttpContext.Response.WriteAsync(
                    "Too many requests, Try again later!",
                    token
                );
            };
        });
        
        services.Configure<ApiResponseOptions>(options =>
        {
            options.ShowDetailedErrors = env.IsDevelopment();
            options.ErrorCodeMapping.Add(typeof(ValidationException), "VALIDATION_ERROR");
            options.ErrorCodeMapping.Add(typeof(NotFoundException), "NOT_FOUND");
        });
        
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        services.AddSingleton<UnrealAssetProvider>();
    }

    public static void ConfigureLogging(IServiceCollection services)
    {
        
    }
}
