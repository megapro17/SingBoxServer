using System.Text.Json.Serialization;

namespace SingBoxServer.Models;

// Корневой файл
public record UserSettings(
    BaseConfig BaseConfig,
    Dictionary<string, UserProfile> Users,
    Dictionary<string, ServerSource>? Servers = null
    //Dictionary<string, string> Mappings,
    //Dictionary<string, RuleProfile> RuleProfiles, // Наши шаблоны
);

public record BaseConfig(string Salt, string Type, string Path);
public record RuleProfile( /* Твои правила route, dns и т.д. */ )
{
    public bool Dpi { get; internal set; }
}

// Профиль пользователя
public record UserProfile(
    bool UseSharedServers = true,
    string? Profile = "standard", // Имя шаблона
    RuleProfile? CustomRules = null,
    Dictionary<string, ServerSource>? Servers = null,
    List<string>? Outbounds = null
);

public record ServerSource(List<string> Tags, ServerType Type, ServerFormat Format, string Path);