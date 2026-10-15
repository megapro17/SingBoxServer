using System.Text;
using SingBoxServer.Core.Models;
using SingBoxServer.Core.Models.Enums;

namespace SingBoxServer.Services.Subscriptions;

internal sealed class SubscriptionLoader(ILocalFileCache localFileCache) : ISubscriptionLoader
{
    public async Task<string> LoadContentAsync(ServerSource server, CancellationToken ct = default)
    {
        // Шаг 1: Получаем "сырые" данные в зависимости от ИСТОЧНИКА (Type)
        string rawContent = server.Type switch
        {
            ServerType.Local => await localFileCache.GetContentAsync(server.Path, ct).ConfigureAwait(false),
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

    private static string DecodeV2rayBase64(string base64String)
    {
        if (string.IsNullOrWhiteSpace(base64String))
            return base64String;

        base64String = base64String.Trim();
        byte[] data = Convert.FromBase64String(base64String);
        return Encoding.UTF8.GetString(data);
    }
}
