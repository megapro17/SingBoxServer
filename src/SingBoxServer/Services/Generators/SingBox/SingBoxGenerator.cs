using System.Text.Json;
using SingBoxServer.Core;
using SingBoxServer.Logging;
using SingBoxServer.Core.Models;
using SingBoxServer.Services.Subscriptions;

namespace SingBoxServer.Services.Generators.SingBox;

internal sealed partial class SingBoxGenerator(
    ILogger<SingBoxGenerator> logger,
    ISubscriptionLoader loader,
    IConfigurationService configService) : IConfigGenerator<SingBoxTemplate>
{
    public async Task<SingBoxTemplate> GenerateAsync(UserProfile user)
    {
        logger.LogStartingConfigGeneration();

        var template = configService.Template;
        var servers = configService.Settings.Servers;

        // Собираем outbounds (с учетом DPI из кастомных правил)
        var outbounds = await BuildOutboundsAsync(user, servers, user.CustomRules).ConfigureAwait(false);

        // Создаем глубокую копию шаблона и применяем замены
        var route = JsonPlaceholderReplacer.ProcessNode(template.Route);
        var dns = JsonPlaceholderReplacer.ProcessNode(template.Dns);
        var httpclients = JsonPlaceholderReplacer.ProcessNode(template.HttpClients);

        // Применяем кастомные правила пользователя
        if (user.CustomRules is { } customRules)
        {
            route = SingBoxRuleInjector.InjectRouteRules(route, customRules.Route, customRules.Hijack);
            dns = SingBoxRuleInjector.InjectDnsRules(dns, customRules.Dns);
        }

        return template with
        {
            Outbounds = outbounds,
            Route = route,
            Dns = dns,
            Experimental = user.CustomRules?.Experimental ?? template.Experimental,
            HttpClients = httpclients,
            Inbounds = user.CustomRules?.Inbounds ?? template.Inbounds
        };
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
                var rawContent = await loader.LoadContentAsync(server).ConfigureAwait(false);
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
            logger.LogOutboundTag(outbound.Tag ?? string.Empty);
        }

        return finalOutbounds;
    }

    private static List<OutboundNode> ExtractProxies(string rawContent)
    {
        try
        {
            var parsedNodes = JsonSerializer.Deserialize(rawContent, AppJsonContext.Default.SingBoxTemplate);
            if (parsedNodes?.Outbounds == null)
                return [];

            return [.. parsedNodes.Outbounds.Where(o => o.Type is not ("selector" or "urltest" or "direct" or "block" or "dns"))];

        }
        catch
        {
        }

        var content = rawContent.TrimStart();
        var nodes = new List<OutboundNode>();
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.StartsWith("vless://", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("type=xhttp", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("type=ws", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("type=grpc", StringComparison.OrdinalIgnoreCase) &&
            !line.Contains("xtls-rprx-vision-", StringComparison.OrdinalIgnoreCase) &&
            !(line.Contains("security=reality", StringComparison.OrdinalIgnoreCase) && !line.Contains("fp=", StringComparison.OrdinalIgnoreCase)))

            {
                var parsedNode = SingBoxLinkParser.Parse(line, AppJsonContext.Default.Options);
                if (parsedNode != null)
                {
                    nodes.Add(parsedNode);
                }
            }
        }
        return nodes;
    }

    private static void RenameProxies(List<OutboundNode> proxies, string name, List<string>? tags = null)
    {
        bool hasDuplicates = tags != null && tags.Distinct().Count() != tags.Count;
        bool hasInsufficientTags = tags == null || tags.Count < proxies.Count;
        bool needsNumbering = hasDuplicates || hasInsufficientTags;

        for (int i = 0; i < proxies.Count; i++)
        {
            var node = proxies[i];

            string serverName;
            if (tags == null)
            {
                serverName = Constants.ProxyUnknown;
            }
            else
            {
                serverName = i < tags.Count ? tags[i] : name;
            }
            if (needsNumbering)
            {
                string number = (i + 1).ToString("D2");
                node.Tag = $"{serverName} {number}";
            }
            else
            {
                node.Tag = serverName;
            }
        }
    }
}
