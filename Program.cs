using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SingBoxServer.Extensions;
using SingBoxServer.Models;
using SingBoxServer.Services;
using SingBoxServer.Services.ConfigGenerator;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();

var app = builder.Build();

app.MapGet("/configs/{hash}/{username}.json", async (
    string hash, 
    string username, 
    IConfigurationService configService,
    [FromKeyedServices("sing-box")] IConfigGenerator<SingBoxTemplate> generator, // Достаем твой генератор прямо из DI!
    JsonSerializerOptions options,                                               // Настройки JSON из DI
    ILogger<Program> logger) =>
{
    var settings = configService.Settings;
    var template = configService.Template;

    // --- ШАГ 1: Проверка хэша ---
    var salt = settings.BaseConfig.Salt;
    var expectedHashBytes = SHA1.HashData(Encoding.UTF8.GetBytes($"{username}.{salt}"));
    var expectedHash = Convert.ToHexString(expectedHashBytes).ToLower();

    // Сравниваем хэши (игнорируя регистр)
    if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("Попытка несанкционированного доступа к конфигу: {Username}", username);
        // Возвращаем 404 (NotFound), чтобы злоумышленник даже не понял, что такой юзер есть
        return Results.NotFound(); 
    }
    
    // --- ШАГ 2: Поиск пользователя ---
    if (!settings.Users.TryGetValue(username, out var userProfile))
    {
        return Results.NotFound($"Пользователь {username} не найден.");
    }

    if (userProfile.Outbounds is not { Count: > 0 })
    {
        return Results.BadRequest($"У пользователя {username} нет серверов (outbounds).");
    }

    // --- ШАГ 3: Генерация и ответ ---
    try
    {
        logger.LogInformation("Генерируем конфиг на лету для юзера {Username}", username);
        
        var finalConfig = await generator.GenerateAsync(userProfile, settings.Servers, template);
        
        // Магия .NET: метод Results.Json сам превратит объект в строку, 
        // применит твои options (snake_case и тд) и добавит заголовок Content-Type: application/json
        return Results.Json(finalConfig, options);
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

// 4. Запускаем сервер!
app.Run();

