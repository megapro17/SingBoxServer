using SingBoxServer.Core.Models;
using SingBoxServer.Logging;
using System.Text;
using SingBoxServer.Core.Models.Enums;

namespace SingBoxServer.Services.Subscriptions;

internal sealed class SubscriptionLoader(
    HttpClient httpClient,
    ILogger<SubscriptionLoader> logger,
    IRemoteSubscriptionCache remoteCache,
    ILocalFileCache localFileCache) : ISubscriptionLoader
{
    public async Task<string> LoadContentAsync(ServerSource server, CancellationToken ct = default)
    {
        // Шаг 1: Получаем "сырые" данные в зависимости от ИСТОЧНИКА (Type)
        string rawContent = server.Type switch
        {
            ServerType.Local => await localFileCache.GetContentAsync(server.Path, ct).ConfigureAwait(false),
            ServerType.Remote => await DownloadRemoteFileAsync(server, ct).ConfigureAwait(false),
            ServerType.Inline => server.Path,
            _ => throw new NotSupportedException($"Тип источника {server.Type} не поддерживается")
        };

        // Шаг 2: Обрабатываем строку в зависимости от ФОРМАТА (Format)
        return server.Format switch
        {
            ServerFormat.SingBox => rawContent, // Для SingBox отдаем как есть
            ServerFormat.V2ray => DecodeV2rayBase64(rawContent), // V2ray всегда нужно декодировать из Base64
            ServerFormat.Vless => rawContent,
            _ => throw new NotSupportedException($"Формат данных {server.Format} не поддерживается")
        };
    }
    private async Task<string> DownloadRemoteFileAsync(ServerSource server, CancellationToken ct)
    {
        // Определяем TTL: по умолчанию 5 минут, если 0 — бесконечно
        var ttlMinutes = server.CacheTtl ?? 5;

        return await remoteCache.GetOrCreateAsync(server.Path, async () =>
        {
            logger.LogDownloadingSubscription("name", server.Path);
            return await httpClient.GetStringAsync(new Uri(server.Path), ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Пустой ответ от сервера");
        }, ttlMinutes).ConfigureAwait(false);
    }

    private static string DecodeV2rayBase64(string base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String))
            return base64String;

        base64String = base64String.Trim();
        byte[] data = Convert.FromBase64String(base64String);
        return Encoding.UTF8.GetString(data);
    }
    private async Task<string> ReadLocalFileAsync(ServerSource server, CancellationToken ct)
    {
        // Используем кэш с FileSystemWatcher (читает диск только при первом запросе или изменении)
        return await localFileCache.GetContentAsync(server.Path, ct).ConfigureAwait(false);
    }

    private async Task<string> ReadLocalV2rayAsync(ServerSource server, CancellationToken ct)
    {
        var base64String = await ReadLocalFileAsync(server, ct).ConfigureAwait(false);
        base64String = base64String.Trim();
        byte[] data = Convert.FromBase64String(base64String);
        string decodedString = Encoding.UTF8.GetString(data);
        return decodedString;
    }
}
