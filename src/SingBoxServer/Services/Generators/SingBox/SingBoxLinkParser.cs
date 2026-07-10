using System.Text.Json;
using System.Text.Json.Nodes;
using SingBoxServer.Core;

namespace SingBoxServer.Services.Generators.SingBox;
public static class SingBoxLinkParser
{
    public static OutboundNode? Parse(string link, JsonSerializerOptions jsonOptions)
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

        return JsonSerializer.Deserialize(proxyNode, AppJsonContext.Default.OutboundNode);
    }
}
