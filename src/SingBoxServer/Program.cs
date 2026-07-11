using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SingBoxServer.Core;
using SingBoxServer.Logging;
using SingBoxServer.Extensions;
using SingBoxServer.Services;
using SingBoxServer.Services.Generators;
using SingBoxServer.Services.Generators.SingBox;
using SingBoxServer.Services.Subscriptions;

namespace SingBoxServer;

internal sealed partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.Services.AddApplicationServices();

        var app = builder.Build();
        app.Services.GetRequiredService<IConfigurationService>();

        app.MapGet("/configs/{hash}/{username}.json", async (string hash, string username, IConfigurationService configService, IConfigGenerator<SingBoxTemplate> generator, ILogger<Program> logger) =>
        {
            var salt = configService.Settings.BaseConfig.Salt;

            [Obsolete("Оставлено только для обратной совместимости старых ссылок")]
#pragma warning disable CA5350
            string GetLegacyHash()
            {
                var hashBytes = SHA1.HashData(Encoding.UTF8.GetBytes($"{username}.{salt}"));
                return Convert.ToHexString(hashBytes).ToLower();
            }
#pragma warning restore CA5350

            string GetHash()
            {
                var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{username}.{salt}"))[..8];
                return Convert.ToHexString(hashBytes).ToLower();
            }

            var legacyHash = GetLegacyHash();
            var newHash = GetHash();

            if (!hash.Equals(legacyHash, StringComparison.OrdinalIgnoreCase) &&
                !hash.Equals(newHash, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogUnauthorizedConfigAccess(username);
                return Results.NotFound();
            }

            if (!configService.Settings.Users.TryGetValue(username, out var userProfile))
            {
                return Results.NotFound($"Пользователь {username} не найден.");
            }
            if (userProfile.Outbounds is not { Count: > 0 })
            {
                return Results.BadRequest($"У пользователя {username} нет серверов (outbounds).");
            }

            try
            {
                logger.LogGeneratingConfigForUser(username);
                var finalConfig = await generator.GenerateAsync(userProfile).ConfigureAwait(false);

                // Теперь фреймворк сам найдет инструкции для сериализации в глобальных настройках
                return Results.Ok(finalConfig);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or HttpRequestException or InvalidOperationException)
            {
                logger.LogCriticalGenerationFailure(ex, username);
                return Results.Problem("Внутренняя ошибка при генерации конфига.");
            }
        });

        // Очистка кэша удаленных подписок (локальные файлы трогать не нужно, они следят за диском)
        app.MapPost("/cache/clear", (IRemoteSubscriptionCache cache, ILogger<Program> logger) =>
        {
            cache.Clear();
            logger.LogRemoteCacheClearedByRequest();
            return Results.Ok(new { message = "Cache cleared" });
        });

        app.Run();
    }
}
