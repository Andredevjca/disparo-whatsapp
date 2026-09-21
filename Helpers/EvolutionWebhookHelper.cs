using System.Text.Json;
namespace DisparoApi.Helpers;

public static class EvolutionWebhookHelper
{
    public static List<JsonElement> ExtrairMensagens(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToList();
        if (root.ValueKind != JsonValueKind.Object) return new();
        if (root.TryGetProperty("key", out _) || root.TryGetProperty("messageId", out _)) return new() { root };
        if (root.TryGetProperty("messages", out var messages)) return ExtrairMensagens(messages);
        if (root.TryGetProperty("data", out var data)) return ExtrairMensagens(data);
        return new();
    }

    public static string? Texto(JsonElement root, string property)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
