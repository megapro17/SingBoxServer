using System.Text.Json;
using System.Text.Json.Nodes;

namespace SingBoxServer.Services.Generators.SingBox;

public static class SingBoxRuleInjector
{
    /// <summary>
    /// Вставляет пользовательские правила маршрутизации в шаблон.
    /// Ищет якорь (clash_mode: Global или Direct) и вставляет после него.
    /// </summary>
    public static JsonNode? InjectRouteRules(JsonNode? route, JsonArray? customRules, JsonNode? hijack)
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
    public static JsonNode? InjectDnsRules(JsonNode? dns, JsonArray? customRules)
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
}
