using System.Text.Json;
using System.Text.Json.Nodes;
using SingBoxServer.Core;

namespace SingBoxServer.Services.Generators.SingBox;

internal static class JsonPlaceholderReplacer
{
    public static JsonNode? ProcessNode(JsonNode? node)
    {
        if (node == null) return null;

        // Создаем глубокую копию, чтобы не менять исходный шаблон
        var clone = node.DeepClone();
        ReplacePlaceholders(clone);
        return clone;
    }

    public static void ReplacePlaceholders(JsonNode? node)
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
}
