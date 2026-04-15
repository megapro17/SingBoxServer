using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SingBoxServer.Logging;
using SingBoxServer.Models;
using SingBoxServer.Services.SubscriptionLoader;

namespace SingBoxServer.Services.ConfigGenerator;

public partial class SingBoxGenerator(ILogger<SingBoxGenerator> logger, ISubscriptionLoader loader, JsonSerializerOptions jsonOptions) : IConfigGenerator<SingBoxTemplate>
{
    public string Name => "sing-box";

    public async Task<SingBoxTemplate> GenerateAsync(UserProfile user, Dictionary<string, ServerSource> servers, SingBoxTemplate template)
    {
        logger.LogInformation("Начинаем генерацию конфига");
        // Метод Generate превращается в "Оглавление"
        return template with
        {
            //Log = new JsonObject { ["level"] = "debug" },
            Outbounds = await BuildOutboundsAsync(user, servers),
            //Route = BuildRoutes(settings)         // Вынесли логику
        };
    }

    // А вот тут уже кипит реальная работа
    private async Task<List<OutboundNode>> BuildOutboundsAsync(UserProfile user, Dictionary<string, ServerSource> servers)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(user.Outbounds);

        var finalOutbounds = new List<OutboundNode>();
        var allProxies = new List<OutboundNode>();

        logger.LogProcessingUser();

        foreach (var outbound in user.Outbounds ?? [])
        {
            var server = user.Servers?.GetValueOrDefault(outbound)
              ?? servers.GetValueOrDefault(outbound);

            if (server != null)
            {
                //logger.LogServerInfo(string.Join(", ", server.Tags), server.Type, server.Format, server.Path);
                var rawContent = await loader.LoadContentAsync(server);
                if (string.IsNullOrWhiteSpace(rawContent)) continue;

                // Метод теперь просто возвращает список, а мы добавляем его к общему
                var extracted = ExtractProxies(rawContent);
                RenameProxies(extracted, outbound, server.Tags);
                allProxies.AddRange(extracted);
            }
        }

        // 2. Добавляем дефолтные узлы (статика)
        finalOutbounds.Add(new OutboundNode { Type = "direct", Tag = Constants.ProxyDirect });

        // 3. ГЕНЕРАЦИЯ SELECTOR'а (Только если мы нашли хоть один прокси)
        if (allProxies.Count > 0)
        {
            // С помощью LINQ вытаскиваем только имена (Tag) из всех собранных прокси
            var proxyTags = allProxies.Select(p => p.Tag).ToList();

            var selector = new OutboundNode
            {
                Type = "selector",
                Tag = Constants.ProxySelector,                 // Имя нашего селектора
                OutboundsTags = proxyTags,        // Передаем список имен серверов
                DefaultTag = proxyTags.FirstOrDefault() // Ставим первый сервер по умолчанию
            };

            // Добавляем селектор в итоговый массив
            finalOutbounds.Add(selector);
        }

        // 4. КОПИРОВАНИЕ ВСЕХ ПРОКСИ
        // Добавляем сами настройки серверов в конец файла
        finalOutbounds.AddRange(allProxies);

        foreach (var outbound in finalOutbounds)
        {
            logger.LogInformation(outbound.Tag);
        }

        return finalOutbounds;
    }

    // Убрали ref, теперь метод отвечает только за парсинг и фильтрацию одной строки
    private List<OutboundNode> ExtractProxies(string rawContent)
    {
        var content = rawContent.TrimStart(); // Убираем возможные пробелы в начале

        // --- ДОБАВЛЯЕМ НОВУЮ ЛОГИКУ ДЛЯ ССЫЛОК ---
        if (content.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
        {
            var nodes = new List<OutboundNode>();
            // Разбиваем текст по строкам
            var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parsedNode = ParseVlessLink(line);
                if (parsedNode != null)
                {
                    nodes.Add(parsedNode);
                }
            }
            return nodes;
        }
        else
        {
            var parsedNodes = JsonSerializer.Deserialize<SingBoxTemplate>(rawContent, jsonOptions);

            if (parsedNodes?.Outbounds == null)
                return []; // Возвращаем пустой список, если ничего нет

            // Вся логика фильтрации в одном элегантном LINQ-запросе с паттерн-матчингом.
            // Мы говорим: "Оставь только те узлы, чей тип НЕ равен перечисленным"
            return [.. parsedNodes.Outbounds.Where(o => o.Type is not ("selector" or "urltest" or "direct" or "block" or "dns"))];
        }
    }

    private void RenameProxies(List<OutboundNode> proxies, string name, List<string>? tags = null)
    {
        for (int i = 0; i < proxies.Count; i++)
        {
            var node = proxies[i];
            string number = (i + 1).ToString("D2");
            string serverName = (tags?.Count > i) ? tags[i] : name;
            node.Tag = $"{serverName} {number}";
        }
    }

    private OutboundNode? ParseVlessLink(string link)
    {
        // 1. Проверяем, что это вообще правильная ссылка VLESS
        if (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "vless")
            return null;

        // 2. Достаем параметры запроса (всё, что после знака ?)
        var queryParams = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

        // 3. Достаем имя (всё, что после знака #), и раскодируем %D0%93... в буквы
        var tag = Uri.UnescapeDataString(uri.Fragment.TrimStart('#'));

        // 4. Собираем объект Sing-box через гибкий JsonObject
        var proxyNode = new JsonObject
        {
            ["type"] = "vless",
            ["tag"] = string.IsNullOrWhiteSpace(tag) ? "vless-out" : tag,
            ["server"] = uri.IdnHost,
            ["server_port"] = uri.Port,
            ["uuid"] = uri.UserInfo
        };

        if (queryParams.TryGetValue("flow", out var flow)) proxyNode["flow"] = flow;
        if (queryParams.TryGetValue("type", out var network)) proxyNode["network"] = network;

        // 5. Собираем секцию TLS и Reality (если есть)
        var security = queryParams.GetValueOrDefault("security");
        if (security is "tls" or "reality")
        {
            var tlsNode = new JsonObject { ["enabled"] = true };

            if (queryParams.TryGetValue("sni", out var sni)) tlsNode["server_name"] = sni;
            if (queryParams.TryGetValue("fp", out var fp))
                tlsNode["utls"] = new JsonObject { ["enabled"] = true, ["fingerprint"] = fp };

            if (security == "reality")
            {
                var realityNode = new JsonObject { ["enabled"] = true };
                if (queryParams.TryGetValue("pbk", out var pbk)) realityNode["public_key"] = pbk;
                if (queryParams.TryGetValue("sid", out var sid)) realityNode["short_id"] = sid;
                tlsNode["reality"] = realityNode;
            }

            proxyNode["tls"] = tlsNode;
        }

        // 6. МАГИЯ: Десериализуем наш собранный JsonObject в строгую OutboundNode.
        // Все нестандартные поля автоматически упадут в ExtensionData!
        return proxyNode.Deserialize<OutboundNode>(jsonOptions);
    }
}