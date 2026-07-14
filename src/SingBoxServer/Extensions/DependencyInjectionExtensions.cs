using SingBoxServer.Core;
using SingBoxServer.Services;
using SingBoxServer.Services.Generators;
using SingBoxServer.Services.Generators.SingBox;
using SingBoxServer.Services.Generators.SingBox.Patchers;
using SingBoxServer.Services.Subscriptions;

namespace SingBoxServer.Extensions;

internal static class DependencyInjectionExtensions
{
    // Ключевое слово "this" делает этот метод расширением для IServiceCollection
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
        });
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IRemoteSubscriptionCache, RemoteSubscriptionCache>();
        services.AddSingleton<ILocalFileCache, LocalFileCache>();
        services.ConfigureOptions<PlatformPath.Setup>();

        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = false;
                options.SingleLine = true;
                options.TimestampFormat = "[HH:mm:ss] ";
            });
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        });

        services.AddHttpClient<ISubscriptionLoader, SubscriptionLoader>();
        services.AddTransient<IConfigGenerator<SingBoxTemplate>, SingBoxGenerator>();
        services.AddSingleton<IConfigPatcher, WindowsConfigPatcher>();
        return services;
    }
}
