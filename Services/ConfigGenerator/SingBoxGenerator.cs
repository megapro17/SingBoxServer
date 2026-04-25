using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SingBoxServer.Logging;
using SingBoxServer.Models;
using SingBoxServer.Services.SubscriptionLoader;

namespace SingBoxServer.Services.ConfigGenerator;

public partial class SingBoxGenerator(
    ILogger<SingBoxGenerator> logger,
    ISubscriptionLoader loader,
    JsonSerializerOptions jsonOptions,
    IConfigurationService configService) : IConfigGenerator<SingBoxTemplate>
{
    public async Task<SingBoxTemplate> GenerateAsync(UserProfile user)
    {
        logger.LogInformation("Начинаем генерацию конфига");

        var template = configService.Template;
        var servers = configService.Settings.Servers;

        // Собираем outbounds (с учетом DPI из кастомных правил)
        var outbounds = await BuildOutboundsAsync(user, servers, user.CustomRules);

        // Создаем глубокую копию шаблона и применяем замены
        var route = ProcessNode(template.Route);
        var dns = ProcessNode(template.Dns);

        // Применяем кастомные правила пользователя
        if (user.CustomRules is { } customRules)
        {
            route = InjectRouteRules(route, customRules.Route, customRules.Hijack);
            dns = InjectDnsRules(dns, customRules.Dns);
        }

        return template with
        {
            Outbounds = outbounds,
            Route = route,
            Dns = dns,
            Experimental = user.CustomRules?.Experimental ?? template.Experimental,
            Inbounds = user.CustomRules?.Inbounds ?? template.Inbounds
        };
    }

    /// <summary>
    /// Вставляет пользовательские правила маршрутизации в шаблон.
    /// Ищет якорь (clash_mode: Global или Direct) и вставляет после него.
    /// </summary>
    private static JsonNode? InjectRouteRules(JsonNode? route, JsonArray? customRules, JsonNode? hijack)
    {
        if (customRules is null || route is not JsonObject routeObj)
            return route;

        if (routeObj["rules"] is not JsonArray rules)
            return route;

        // Определяем позицию для вставки
        int insertIndex = FindInsertIndex(rules);

        // Вставляем правила (в обратном порядке, чтобы сохранить порядок)
        for (int i = customRules.Count - 1; i >= 0; i--)
        {
            rules.Insert(insertIndex, customRules[i]!.DeepClone());
        }

        // Применяем hijack (заменяем первое правило)
        if (hijack is not null && rules.Count > 0)
        {
            rules[0] = hijack.DeepClone();
        }

        return routeObj;
    }

    /// <summary>
    /// Ищет якорь для вставки: сначала clash_mode: Global, затем Direct, иначе 0.
    /// </summary>
    private static int FindInsertIndex(JsonArray rules)
    {
        // Ищем Global
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i] is JsonObject rule &&
                rule["clash_mode"]?.GetValueKind() == JsonValueKind.String &&
                rule["clash_mode"]!.GetValue<string>() == "Global")
            {
                return i + 1;
            }
        }

        // Ищем Direct
        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i] is JsonObject rule &&
                rule["clash_mode"]?.GetValueKind() == JsonValueKind.String &&
                rule["clash_mode"]!.GetValue<string>() == "Direct")
            {
                return i + 1;
            }
        }

        // Если якоря нет — вставляем в начало
        return 0;
    }

    /// <summary>
    /// Вставляет DNS-правила пользователя в начало списка.
    /// </summary>
    private static JsonNode? InjectDnsRules(JsonNode? dns, JsonArray? customRules)
    {
        if (customRules is null || dns is not JsonObject dnsObj)
            return dns;

        if (dnsObj["rules"] is not JsonArray rules)
            return dns;

        // Вставляем в начало (в обратном порядке)
        for (int i = customRules.Count - 1; i >= 0; i--)
        {
            rules.Insert(0, customRules[i]!.DeepClone());
        }

        return dnsObj;
    }

    private JsonNode? ProcessNode(JsonNode? node)
    {
        if (node == null) return null;

        // Создаем глубокую копию, чтобы не менять исходный шаблон
        var clone = node.DeepClone();
        ReplacePlaceholders(clone);
        return clone;
    }

    private static void ReplacePlaceholders(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var prop in obj.ToArray())
            {
                if (prop.Value is JsonValue val && val.GetValueKind() == JsonValueKind.String)
                {
                    string currentVal = val.GetValue<string>();
                    if (currentVal == Constants.ProxyOut)
                        obj[prop.Key] = JsonValue.Create(Constants.ProxySelector);
                    else if (currentVal == Constants.ProxyOutDirect)
                        obj[prop.Key] = JsonValue.Create(Constants.ProxyDirect);
                    else
                        ReplacePlaceholders(prop.Value);
                }
                else
                {
                    ReplacePlaceholders(prop.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                ReplacePlaceholders(item);
            }
        }
    }

    private async Task<List<OutboundNode>> BuildOutboundsAsync(
        UserProfile user,
        Dictionary<string, ServerSource>? servers,
        RuleProfile? customRules)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(user.Outbounds);

        var finalOutbounds = new List<OutboundNode>();
        var allProxies = new List<OutboundNode>();

        logger.LogProcessingUser();

        foreach (var outbound in user.Outbounds ?? [])
        {
            var server = user.Servers?.GetValueOrDefault(outbound)
              ?? servers?.GetValueOrDefault(outbound);

            if (server != null)
            {
                var rawContent = await loader.LoadContentAsync(server);
                if (string.IsNullOrWhiteSpace(rawContent)) continue;

                var extracted = ExtractProxies(rawContent);
                RenameProxies(extracted, outbound, server.Tags);
                allProxies.AddRange(extracted);
            }
        }

        // Добавляем DPI bypass, если включен в кастомных правилах
        if (customRules?.Dpi == true)
        {
            finalOutbounds.Add(new OutboundNode
            {
                Type = "socks",
                Tag = Constants.ProxyDpi,
                ExtensionData = new Dictionary<string, JsonElement>
                {
                    ["server"] = JsonDocument.Parse("\"127.0.0.1\"").RootElement,
                    ["server_port"] = JsonDocument.Parse("1080").RootElement
                }
            });
        }

        // Добавляем direct
        finalOutbounds.Add(new OutboundNode { Type = "direct", Tag = Constants.ProxyDirect });

        // Генерация selector (только если нашли прокси)
        if (allProxies.Count > 0)
        {
            var proxyTags = allProxies.Select(p => p.Tag).ToList();

            var selector = new OutboundNode
            {
                Type = "selector",
                Tag = Constants.ProxySelector,
                OutboundsTags = proxyTags,
                DefaultTag = proxyTags.FirstOrDefault()
            };

            finalOutbounds.Add(selector);
        }

        // Добавляем все прокси
        finalOutbounds.AddRange(allProxies);

        foreach (var outbound in finalOutbounds)
        {
            logger.LogInformation("Outbound: {Tag}", outbound.Tag);
        }

        return finalOutbounds;
    }

    private List<OutboundNode> ExtractProxies(string rawContent)
    {
        var content = rawContent.TrimStart();

        if (content.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
        {
            var nodes = new List<OutboundNode>();
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
                return [];

            return [.. parsedNodes.Outbounds.Where(o => o.Type is not ("selector" or "urltest" or "direct" or "block" or "dns"))];
        }
    }

    private static void RenameProxies(List<OutboundNode> proxies, string name, List<string>? tags = null)
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
        if (!Uri.TryCreate(link.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "vless")
            return null;

        var queryParams = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

        var tag = Uri.UnescapeDataString(uri.Fragment.TrimStart('#'));

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

        return proxyNode.Deserialize<OutboundNode>(jsonOptions);
    }
}
