using System.Text.Json;
using SingBoxServer.Core;
using SingBoxServer.Core.Models;
using SingBoxServer.Logging;
using SingBoxServer.Services.Subscriptions;

namespace SingBoxServer.Services.Generators.SingBox;

internal sealed partial class SingBoxGenerator(
    ILogger<SingBoxGenerator> logger,
    ISubscriptionLoader loader,
    IConfigurationService configService) : IConfigGenerator<SingBoxTemplate>
{
    public async Task<SingBoxTemplate> GenerateAsync(UserProfile user, string? device = null, string? template = null)
    {
        logger.LogStartingConfigGeneration();

        var resolvedTemplate = configService.GetTemplate(template, device);
        var servers = configService.Settings.Servers;

        var expandedOutbounds = new List<string>();
        if (user.Outbounds != null)
        {
            var groups = configService.Settings.OutboundGroups;
            foreach (var ob in user.Outbounds)
            {
                if (groups != null && groups.TryGetValue(ob, out var groupServers))
                {
                    expandedOutbounds.AddRange(groupServers);
                }
                else
                {
                    expandedOutbounds.Add(ob);
                }
            }
            expandedOutbounds = [.. expandedOutbounds.Distinct()];
        }

        // Собираем outbounds (с учетом DPI из кастомных правил)
        var outbounds = await BuildOutboundsAsync(expandedOutbounds, user, servers, user.CustomRules).ConfigureAwait(false);

        var routeNode = resolvedTemplate.Route;
        var dnsNode = resolvedTemplate.Dns;

        if (user.CustomRules is { } customRules)
        {
            routeNode = SingBoxRuleInjector.InjectRouteRules(routeNode, customRules.Route, customRules.Hijack);
            dnsNode = SingBoxRuleInjector.InjectDnsRules(dnsNode, customRules.Dns);
        }

        var route = JsonPlaceholderReplacer.ProcessNode(routeNode);
        var dns = JsonPlaceholderReplacer.ProcessNode(dnsNode);
        var httpclients = JsonPlaceholderReplacer.ProcessNode(resolvedTemplate.HttpClients);

        return resolvedTemplate with
        {
            Outbounds = outbounds,
            Route = route,
            Dns = dns,
            Experimental = user.CustomRules?.Experimental?.DeepClone() ?? resolvedTemplate.Experimental,
            HttpClients = httpclients,
            Inbounds = user.CustomRules?.Inbounds?.DeepClone().AsArray() ?? resolvedTemplate.Inbounds
        };
    }
    private async Task<List<OutboundNode>> BuildOutboundsAsync(
        List<string> expandedOutbounds,
        UserProfile user,
        Dictionary<string, ServerSource>? servers,
        RuleProfile? customRules)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(expandedOutbounds);

        var finalOutbounds = new List<OutboundNode>();
        var allProxies = new List<OutboundNode>();

        logger.LogProcessingUser();

        foreach (var outbound in expandedOutbounds)
        {
            var server = user.Servers?.GetValueOrDefault(outbound)
              ?? servers?.GetValueOrDefault(outbound);

            if (server != null)
            {
                var rawContent = await loader.LoadContentAsync(server).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(rawContent)) continue;

                var extracted = ExtractProxies(rawContent);
                if (server.Tags != null && server.Tags.Count > 0)
                {
                    RenameProxies(extracted, outbound, server.Tags);
                }
                else
                {
                    AutoFormatProxies(extracted, outbound);
                }
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
                DefaultTag = proxyTags.FirstOrDefault(),
                ExtensionData = new Dictionary<string, JsonElement>
                {
                    { "interrupt_exist_connections", JsonDocument.Parse("true").RootElement }
                }
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
        catch (JsonException)
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

    // Matches exactly two Regional Indicator Symbols (A-Z), which form a standard country flag emoji.
    private static readonly System.Text.RegularExpressions.Regex _emojiRegex = new(@"(?:\uD83C[\uDDE6-\uDDFF]){2}", System.Text.RegularExpressions.RegexOptions.Compiled);
    private static void AutoFormatProxies(List<OutboundNode> proxies, string name)
    {
        var counters = new Dictionary<string, int>();

        foreach (var node in proxies)
        {
            string emoji = "🌐";
            if (!string.IsNullOrWhiteSpace(node.Tag))
            {
                // Используем Matches для поиска ВСЕХ флагов в строке
                var matches = _emojiRegex.Matches(node.Tag);
                if (matches.Count > 0)
                {
                    // Собираем все найденные флаги и соединяем их стрелочкой
                    var flags = matches.Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value);
                    emoji = string.Join(" → ", flags);
                }
            }

            if (emoji == "🌐")
            {
                emoji = GetFallbackEmoji(node);
            }

            // Счетчик теперь будет считать разные цепочки отдельно (например, 🇷🇺 и 🇷🇺 → 🇫🇮 будут считаться раздельно)
            if (!counters.TryGetValue(emoji, out int count))
            {
                count = 0;
            }
            count++;
            counters[emoji] = count;

            node.Tag = $"{emoji} {name} {count:D2}";
        }
    }
    private static string GetFallbackEmoji(OutboundNode node)
    {
        string host = string.Empty;
        if (node.ExtensionData != null && node.ExtensionData.TryGetValue("server", out var serverElement) && serverElement.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            host = serverElement.GetString() ?? string.Empty;
        }

        var textsToSearch = new[] { node.Tag ?? string.Empty, host };

        foreach (var text in textsToSearch)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            var lower = text.ToLowerInvariant();

            // 1. Ищем мост: bridge-ru-fi
            var bridgeMatch = System.Text.RegularExpressions.Regex.Match(lower, @"bridge-([a-z]{2})-([a-z]{2})");
            if (bridgeMatch.Success)
                return $"{GetFlagEmoji(bridgeMatch.Groups[1].Value)} → {GetFlagEmoji(bridgeMatch.Groups[2].Value)}";

            // 2. Ищем обычный сервер: de-fra-02
            var match2 = System.Text.RegularExpressions.Regex.Match(lower, @"(?:^|_|-)([a-z]{2})-[a-z]+");
            if (match2.Success)
                return GetFlagEmoji(match2.Groups[1].Value);

            // 3. Ищем формат: fi10.samovargate.com
            var match1 = System.Text.RegularExpressions.Regex.Match(lower, @"^([a-z]{2})[a-z]*\d+");
            if (match1.Success)
            {
                var code = match1.Groups[1].Value;
                if (code is not ("ap" or "cd" or "ww" or "ns"))
                    return GetFlagEmoji(code);
            }
        }

        return "🌐";
    }

    private static string GetFlagEmoji(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length < 2)
            return "🌐";

        var code = countryCode.ToUpperInvariant();
        if (code == "UK") return "🇬🇧";
        if (code == "SW") return "🇸🇪";

        if (code[0] >= 'A' && code[0] <= 'Z' &&
                code[1] >= 'A' && code[1] <= 'Z')
        {
            return char.ConvertFromUtf32(code[0] + 127397) + char.ConvertFromUtf32(code[1] + 127397);
        }

        return "🌐";
    }
}
