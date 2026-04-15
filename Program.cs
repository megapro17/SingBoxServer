using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SingBoxServer.Extensions;
using SingBoxServer.Models;
using SingBoxServer.Services.ConfigGenerator;
// Твои using'и для моделей и сервисов...

// 1. Создаем строитель ВЕБ-ПРИЛОЖЕНИЯ (он внутри себя уже имеет ServiceCollection)
var builder = WebApplication.CreateBuilder(args);

// 2. Добавляем твои сервисы (тот самый метод расширения)
builder.Services.AddApplicationServices();

var app = builder.Build();

// Наша секретная соль для хэшей
const string SALT = "JeYu=VS6xtCdz$Rs"; 

// 3. Создаем тот самый маршрут
app.MapGet("/configs/{hash}/{username}.json", async (
    string hash, 
    string username, 
    [FromKeyedServices("sing-box")] IConfigGenerator<SingBoxTemplate> generator, // Достаем твой генератор прямо из DI!
    JsonSerializerOptions options,                                               // Настройки JSON из DI
    ILogger<Program> logger) =>
{
    // --- ШАГ 1: Проверка хэша ---
    var expectedHashBytes = SHA1.HashData(Encoding.UTF8.GetBytes($"{username}.{SALT}"));
    var expectedHash = Convert.ToHexString(expectedHashBytes).ToLower();

    // Сравниваем хэши (игнорируя регистр)
    if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
    {
        logger.LogWarning("Попытка несанкционированного доступа к конфигу: {Username}", username);
        // Возвращаем 404 (NotFound), чтобы злоумышленник даже не понял, что такой юзер есть
        return Results.NotFound(); 
    }

    // --- ШАГ 2: Чтение данных (в будущем можно вынести в кэш/Singleton) ---
    var settingsInput = await File.ReadAllTextAsync(@"C:\Users\megapro17\SourceCode\SingBoxServer\settings.json");
    var templateInput = await File.ReadAllTextAsync(@"C:\Users\megapro17\SourceCode\sing-box\latest_whitelist.json");

    var settings = JsonSerializer.Deserialize<UserSettings>(settingsInput, options);
    var template = JsonSerializer.Deserialize<SingBoxTemplate>(templateInput, options);

    if (settings == null || template == null)
        return Results.Problem("Ошибка чтения файлов конфигурации сервера.");

    // --- ШАГ 3: Поиск пользователя ---
    if (!settings.Users.TryGetValue(username, out var userProfile))
    {
        return Results.NotFound($"Пользователь {username} не найден.");
    }

    if (userProfile.Outbounds is not { Count: > 0 })
    {
        return Results.BadRequest($"У пользователя {username} нет серверов (outbounds).");
    }

    // --- ШАГ 4: Генерация и ответ ---
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

// 4. Запускаем сервер!
app.Run();