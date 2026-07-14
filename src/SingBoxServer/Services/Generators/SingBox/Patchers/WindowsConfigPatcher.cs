using System.Text.Json.Nodes;

namespace SingBoxServer.Services.Generators.SingBox.Patchers;

internal sealed class WindowsConfigPatcher : IConfigPatcher
{
    public bool CanPatch(string? device)
    {
        return string.Equals(device, "windows", StringComparison.OrdinalIgnoreCase);
    }

    public SingBoxTemplate ApplyPatch(SingBoxTemplate config)
    {
        // 1. Клонируем Inbounds, чтобы не трогать глобальный шаблон
        var inbounds = config.Inbounds?.DeepClone().AsArray();
        if (inbounds != null)
        {
            foreach (var inbound in inbounds)
            {
                if (inbound is JsonObject inboundObj && 
                    inboundObj.TryGetPropertyValue("type", out var typeNode) && 
                    typeNode?.GetValue<string>() == "tun")
                {
                    inboundObj.Remove("auto_redirect");
                }
            }
        }

        // 2. Клонируем Experimental (или создаем новый, если его не было)
        var experimental = config.Experimental?.DeepClone().AsObject() ?? new JsonObject();
        
        if (!experimental.ContainsKey("clash_api"))
        {
            experimental["clash_api"] = new JsonObject
            {
                ["external_controller"] = "127.0.0.1:9090",
                ["external_ui"] = "ui",
                ["default_mode"] = "Rule"
            };
        }

        // 3. Возвращаем новый конфиг с подмененными узлами
        return config with 
        { 
            Inbounds = inbounds ?? config.Inbounds, 
            Experimental = experimental 
        };
    }
}
