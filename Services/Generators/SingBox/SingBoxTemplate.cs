using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SingBoxServer.Services.Generators.SingBox;

public record SingBoxTemplate(
    [property: JsonPropertyName("log")] JsonNode? Log = null,
    [property: JsonPropertyName("dns")] JsonNode? Dns = null,
    [property: JsonPropertyName("ntp")] JsonNode? Ntp = null,
    [property: JsonPropertyName("certificate")] JsonNode? Certificate = null,
    [property: JsonPropertyName("certificate_providers")] JsonArray? CertificateProviders = null,
    [property: JsonPropertyName("http_clients")] JsonNode? HttpClients = null,
    [property: JsonPropertyName("endpoints")] JsonArray? Endpoints = null,
    [property: JsonPropertyName("inbounds")] JsonArray? Inbounds = null,
    // В будущем, когда захочешь строго типизировать Outbounds, поменяешь JsonArray на List<Outbound>
    [property: JsonPropertyName("outbounds")] List<OutboundNode>? Outbounds = null,
    [property: JsonPropertyName("route")] JsonNode? Route = null,
    [property: JsonPropertyName("services")] JsonArray? Services = null,
    [property: JsonPropertyName("experimental")] JsonNode? Experimental = null
)
{
    // 🔥 ГЛАВНОЕ УХИЩРЕНИЕ 🔥
    // Если разработчики sing-box в новой версии добавят какое-то новое поле 
    // (например, "new_feature": {}), оно не потеряется! 
    // Сериализатор сложит всё неизвестное в этот словарь, а при сохранении вернет обратно.
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public record OutboundNode
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    // Эти поля нужны ТОЛЬКО для Selector'а. 
    // JsonIgnore говорит: "Если они null, вообще не пиши их в итоговый JSON"
    [JsonPropertyName("outbounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? OutboundsTags { get; set; }

    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultTag { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
