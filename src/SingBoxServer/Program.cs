using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SingBoxServer.Core;
using SingBoxServer.Extensions;
using SingBoxServer.Logging;
using SingBoxServer.Services;
using SingBoxServer.Services.Generators;
using SingBoxServer.Services.Generators.SingBox;

namespace SingBoxServer;

internal sealed partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        builder.Services.AddApplicationServices();

        var app = builder.Build();
        app.Services.GetRequiredService<IConfigurationService>();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        bool IsAuthorized(HttpContext context, IConfiguration config)
        {
            var expectedToken = config["AdminToken"];
            if (string.IsNullOrEmpty(expectedToken)) return false;
            
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader)) return false;
            var headerValue = authHeader.ToString();
            if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
            
            var token = headerValue["Bearer ".Length..].Trim();
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token), 
                Encoding.UTF8.GetBytes(expectedToken)
            );
        }

        app.MapGet("/configs/{hash}/{username}.json", async (
            string hash, 
            string username, 
            string? device,
            string? template,
            IConfigurationService configService, 
            IConfigGenerator<SingBoxTemplate> generator, 
            ILogger<Program> logger) =>
        {
            var salt = configService.Settings.BaseConfig.Salt;

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{username}.{salt}"))[..8];
            var expectedHash = Convert.ToHexString(hashBytes).ToLower();

            if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
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
                var effectiveTemplate = template ?? userProfile.Profile;
                var finalConfig = await generator.GenerateAsync(userProfile, device, effectiveTemplate).ConfigureAwait(false);

                return Results.Ok(finalConfig);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                logger.LogCriticalGenerationFailure(ex, username);
                return Results.Problem("Внутренняя ошибка при генерации конфига.");
            }
        });

        app.MapPost("/api/auth/verify", (HttpContext context, IConfiguration appConfig) => 
        {
            if (!IsAuthorized(context, appConfig)) return Results.Unauthorized();
            return Results.Ok(new SuccessResponse(true));
        });

        app.MapGet("/api/config", async (HttpContext context, IOptions<PlatformPath> paths, IConfiguration appConfig) => 
        {
            if (!IsAuthorized(context, appConfig)) return Results.Unauthorized();
            try {
                var json = await File.ReadAllTextAsync(paths.Value.SettingsPath).ConfigureAwait(false);
                return Results.Content(json, "application/json");
            } catch (IOException ex) {
                return Results.Problem("Failed to read config: " + ex.Message);
            }
        });

        app.MapPost("/api/config", async (HttpContext context, IOptions<PlatformPath> paths, IConfiguration appConfig) => 
        {
            if (!IsAuthorized(context, appConfig)) return Results.Unauthorized();
            using var reader = new StreamReader(context.Request.Body);
            var json = await reader.ReadToEndAsync().ConfigureAwait(false);
            try {
                JsonDocument.Parse(json); // Validate JSON
                await File.WriteAllTextAsync(paths.Value.SettingsPath, json).ConfigureAwait(false);
                return Results.Ok(new SuccessResponse(true));
            } catch (JsonException) {
                return Results.BadRequest("Invalid JSON format.");
            } catch (IOException ex) {
                return Results.Problem("Failed to save config: " + ex.Message);
            }
        });

        app.Run();
    }
}
