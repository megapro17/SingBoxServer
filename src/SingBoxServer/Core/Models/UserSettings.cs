using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SingBoxServer.Core.Models.Enums;

namespace SingBoxServer.Core.Models;

// Корневой файл
internal sealed record UserSettings(
    BaseConfig BaseConfig,
    Dictionary<string, UserProfile> Users,
    Dictionary<string, ServerSource>? Servers = null,
    Dictionary<string, List<string>>? OutboundGroups = null
//Dictionary<string, string> Mappings,
//Dictionary<string, RuleProfile> RuleProfiles, // Наши шаблоны
);

internal sealed record BaseConfig(string Salt, string Type, TemplatePaths Path);

[JsonConverter(typeof(TemplatePathsConverter))]
internal sealed class TemplatePaths : Dictionary<string, string>
{
    public TemplatePaths() : base(StringComparer.OrdinalIgnoreCase) { }
    public TemplatePaths(IDictionary<string, string> dictionary) : base(dictionary, StringComparer.OrdinalIgnoreCase) { }
}

internal sealed class TemplatePathsConverter : JsonConverter<TemplatePaths>
{
    public override TemplatePaths Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var singlePath = reader.GetString();
            return new TemplatePaths { ["default"] = singlePath ?? "template.json" };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var dict = JsonSerializer.Deserialize(ref reader, AppJsonContext.Default.DictionaryStringString);
            return dict != null ? new TemplatePaths(dict) : new TemplatePaths();
        }

        throw new JsonException("Ожидалась строка или объект для base_config.path");
    }

    public override void Write(Utf8JsonWriter writer, TemplatePaths value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (IDictionary<string, string>)value, AppJsonContext.Default.DictionaryStringString);
    }
}

/// <summary>
/// Кастомные правила пользователя для инъекции в шаблон sing-box.
/// </summary>
internal sealed record RuleProfile
{
    /// <summary>
    /// Включить DPI bypass (добавляет socks-прокси на 127.0.0.1:1080)
    /// </summary>
    [JsonPropertyName("dpi")]
    public bool Dpi { get; set; }

    /// <summary>
    /// Правила маршрутизации для вставки в route.rules
    /// </summary>
    [JsonPropertyName("route")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonArray? Route { get; set; }

    /// <summary>
    /// DNS-правила для вставки в dns.rules
    /// </summary>
    [JsonPropertyName("dns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonArray? Dns { get; set; }

    /// <summary>
    /// Заменяет первую запись в route.rules (DNS hijack)
    /// </summary>
    [JsonPropertyName("hijack")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Hijack { get; set; }

    /// <summary>
    /// Полностью заменяет секцию experimental
    /// </summary>
    [JsonPropertyName("experimental")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Experimental { get; set; }

    /// <summary>
    /// Полностью заменяет секцию inbounds
    /// </summary>
    [JsonPropertyName("inbounds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonArray? Inbounds { get; set; }
}

// Профиль пользователя
internal sealed record UserProfile(
    bool UseSharedServers = true,
    string? Profile = null, // Имя шаблона
    RuleProfile? CustomRules = null,
    Dictionary<string, ServerSource>? Servers = null,
    List<string>? Outbounds = null
);

internal sealed record ServerSource(
    List<string>? Tags,
    ServerType Type,
    ServerFormat Format,
    string Path,
    int? CacheTtl = null // Минуты. 0 = бесконечно (до перезапуска). null = 5 минут (по умолчанию).
);
