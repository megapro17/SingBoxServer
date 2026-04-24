using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SingBoxServer.Services;
using SingBoxServer.Models;
using SingBoxServer.Services.SubscriptionLoader;
using SingBoxServer.Services.ConfigGenerator;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SingBoxServer.Extensions;

public static class DependencyInjectionExtensions
{
    // Ключевое слово "this" делает этот метод расширением для IServiceCollection
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
            jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        services.AddSingleton(jsonOptions);

        services.AddSingleton<IConfigurationService, ConfigurationService>();
        
        // Кэширование
        services.AddSingleton<IRemoteSubscriptionCache, RemoteSubscriptionCacheService>(); // Для удаленных (TTL + Clear)
        services.AddSingleton<ILocalFileCache, LocalFileCacheService>(); // Для локальных (Watcher)

        services.AddLogging(builder =>


        {
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        services.AddHttpClient<ISubscriptionLoader, SubscriptionLoader>();
        services.AddKeyedTransient<IConfigGenerator<SingBoxTemplate>, SingBoxGenerator>("sing-box");
        return services;
    }
}
