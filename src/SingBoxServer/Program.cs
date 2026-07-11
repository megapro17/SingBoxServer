using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SingBoxServer.Core;
using SingBoxServer.Extensions;
using SingBoxServer.Services;
using SingBoxServer.Services.Generators;
using SingBoxServer.Services.Generators.SingBox;
using SingBoxServer.Services.Subscriptions;

namespace SingBoxServer;

public partial class Program
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
            var expectedHashBytes = SHA1.HashData(Encoding.UTF8.GetBytes($"{username}.{salt}"));
            var expectedHash = Convert.ToHexString(expectedHashBytes).ToLower();

            if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Попытка несанкционированного доступа к конфигу: {Username}", username);
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
                logger.LogInformation("Генерируем конфиг на лету для юзера {Username}", username);
                var finalConfig = await generator.GenerateAsync(userProfile);

                // Теперь фреймворк сам найдет инструкции для сериализации в глобальных настройках
                return Results.Ok(finalConfig);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Критический сбой при генерации для {Username}", username);
                return Results.Problem("Внутренняя ошибка при генерации конфига.");
            }
        });

        // Очистка кэша удаленных подписок (локальные файлы трогать не нужно, они следят за диском)
        app.MapPost("/cache/clear", (IRemoteSubscriptionCache cache, ILogger<Program> logger) =>
        {
            cache.Clear();
            logger.LogInformation("Кэш удаленных подписок очищен по запросу.");
            return Results.Ok(new { message = "Cache cleared" });
        });

        app.Run();
    }
}
