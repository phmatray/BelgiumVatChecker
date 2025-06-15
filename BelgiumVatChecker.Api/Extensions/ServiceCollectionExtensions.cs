using BelgiumVatChecker.Core.Interfaces;
using BelgiumVatChecker.Core.Services;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;

namespace BelgiumVatChecker.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Application Services
        services.AddScoped<IVatValidationService, VatValidationService>();
        
        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Belgium VAT Checker API",
                Version = "v1",
                Description = "API for validating Belgian and EU VAT numbers using the VIES service",
                Contact = new OpenApiContact
                {
                    Name = "API Support",
                    Email = "support@belgiumvatchecker.com"
                },
                License = new OpenApiLicense
                {
                    Name = "MIT",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            // Add security definition if needed in the future
            // c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { ... });
        });

        return services;
    }

    public static IServiceCollection AddHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient<IViesClient, ViesClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "BelgiumVatChecker/1.0");
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    public static IServiceCollection AddCorsConfiguration(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });

            // Add named policy for production use
            options.AddPolicy("Production", policy =>
            {
                policy.WithOrigins("https://yourdomain.com")
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Console.WriteLine(
                        $"Retry {retryCount} after {timespan.TotalMilliseconds}ms due to {outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.GetType().Name ?? "Unknown"}"
                    );
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(1),
                onBreak: (_, duration) =>
                {
                    Console.WriteLine($"Circuit breaker opened for {duration.TotalSeconds} seconds due to consecutive failures");
                },
                onReset: () =>
                {
                    Console.WriteLine("Circuit breaker reset, normal operation resumed");
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine("Circuit breaker is half-open, testing if service has recovered");
                });
    }
}