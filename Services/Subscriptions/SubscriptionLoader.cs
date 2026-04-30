using SingBoxServer.Models;
using SingBoxServer.Logging;
using System.Text;
using SingBoxServer.Core.Models.Enums;

namespace SingBoxServer.Services.Subscriptions;

public class SubscriptionLoader(
    HttpClient httpClient, 
    ILogger<SubscriptionLoader> logger,
    IRemoteSubscriptionCache remoteCache,
    ILocalFileCache localFileCache) : ISubscriptionLoader
{
    public async Task<string> LoadContentAsync(ServerSource server, CancellationToken ct = default)
    {
        return server.Type switch
        {
            ServerType.Local => await HandleLocalLoadAsync(server, ct),
            ServerType.Remote => await DownloadRemoteFileAsync(server, ct),
            _ => throw new NotSupportedException($"Тип {server.Type} не поддерживается")
        };
    }

    private async Task<string> HandleLocalLoadAsync(ServerSource server, CancellationToken ct)
    {
        return server.Format switch
        {
            ServerFormat.SingBox => await ReadLocalFileAsync(server, ct),
            ServerFormat.V2ray => await ReadLocalV2rayAsync(server, ct),
            _ => throw new NotSupportedException($"Формат {server.Format} не поддерживается")
        };
    }

    private async Task<string> ReadLocalFileAsync(ServerSource server, CancellationToken ct)
    {
        // Используем кэш с FileSystemWatcher (читает диск только при первом запросе или изменении)
        return await localFileCache.GetContentAsync(server.Path, ct);
    }

    private async Task<string> DownloadRemoteFileAsync(ServerSource server, CancellationToken ct)
    {
        // Определяем TTL: по умолчанию 5 минут, если 0 — бесконечно
        var ttlMinutes = server.CacheTtl ?? 5;
        
        return await remoteCache.GetOrCreateAsync(server.Path, async () =>
        {
            logger.LogDownloadingSubscription("name", server.Path);
            return await httpClient.GetStringAsync(server.Path, ct) ?? throw new InvalidOperationException("Пустой ответ от сервера");
        }, ttlMinutes);
    }

    private async Task<string> ReadLocalV2rayAsync(ServerSource server, CancellationToken ct)
    {
        var base64String = await ReadLocalFileAsync(server, ct);
        base64String = base64String.Trim();
        byte[] data = Convert.FromBase64String(base64String);
        string decodedString = Encoding.UTF8.GetString(data);
        return decodedString;
    }
}
