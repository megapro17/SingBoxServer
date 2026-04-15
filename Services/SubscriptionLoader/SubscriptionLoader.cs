using System.Net.Http;
using Microsoft.Extensions.Logging;
using SingBoxServer.Models;
using SingBoxServer.Logging;
using System.Text; // Твои общие логи

namespace SingBoxServer.Services.SubscriptionLoader;

public class SubscriptionLoader(HttpClient httpClient, ILogger<SubscriptionLoader> logger) : ISubscriptionLoader
// : ISubscriptionLoader
{
    public async Task<string> LoadContentAsync(ServerSource server, CancellationToken ct = default)
    {
        return server.Type switch
        {
            ServerType.Local => await ReadLocalFileAsync(server, ct),
            ServerType.Remote => await DownloadRemoteFileAsync(server, ct),
            ServerType.LocalV2ray => await ReadLocalV2rayAsync(server, ct),
            _ => throw new NotSupportedException($"Тип {server.Type} не поддерживается")
        };
    }
    private async Task<string> ReadLocalFileAsync(ServerSource server, CancellationToken ct)
    {
        logger.LogLoadingLocalConfig(server.Path);
        return await File.ReadAllTextAsync(server.Path, ct);
    }
    private async Task<string> DownloadRemoteFileAsync(ServerSource server, CancellationToken ct)
    {
        logger.LogDownloadingSubscription("name", server.Path);
        return await httpClient.GetStringAsync(server.Path, ct);
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