using SingBoxServer.Services;
using SingBoxServer.Services.Generators;
using System.Text.Json;
using System.Text.Json.Serialization;
using SingBoxServer.Services.Subscriptions;
using SingBoxServer.Services.Generators.SingBox;

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
        services.AddSingleton<IRemoteSubscriptionCache, RemoteSubscriptionCache>();
        services.AddSingleton<ILocalFileCache, LocalFileCache>();

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
        services.AddTransient<IConfigGenerator<SingBoxTemplate>, SingBoxGenerator>();
        return services;
    }
}
