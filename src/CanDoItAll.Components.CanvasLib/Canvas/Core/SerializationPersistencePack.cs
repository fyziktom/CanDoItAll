using System.Text.Json;

namespace CanDoItAll.Components.CanvasLib;

public static class SerializationPersistencePack
{
    public static JsonSerializerOptions DefaultOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize<T>(T value)
        => JsonSerializer.Serialize(value, DefaultOptions);

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, DefaultOptions);
        }
        catch
        {
            return default;
        }
    }
}

public sealed class SerializationPersistencePackPreviewSnapshot
{
    public string TestHookId { get; init; } = "serialization-persistence-pack";

    public string Label { get; init; } = "Serialization persistence pack";

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string StatePill { get; init; } = string.Empty;

    public IReadOnlyList<string> Metrics { get; init; } = [];
}

public static class SerializationPersistencePackPreviewFactory
{
    public static SerializationPersistencePackPreviewSnapshot CreateForWorkbench(CanvasWorkbenchSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var json = SerializationPersistencePack.Serialize(surface);
        var restored = SerializationPersistencePack.Deserialize<CanvasWorkbenchSurface>(json) ?? new CanvasWorkbenchSurface();

        return new SerializationPersistencePackPreviewSnapshot
        {
            Title = "Shared serialization now owns canvas state persistence and replay",
            Summary = "Surface payloads, UI state, clipboard snapshots, and preview cards all round-trip through one tolerant JSON pack instead of each boundary hand-rolling its own serializer settings.",
            StatePill = restored.Nodes.Count == surface.Nodes.Count ? "Round-trip" : "Fallback",
            Metrics =
            [
                $"{json.Length} json chars",
                $"{restored.Nodes.Count} nodes restored",
                $"{restored.Links.Count} links restored",
                $"{restored.UiState.SelectedNodeIds.Count} selected restored"
            ]
        };
    }
}


