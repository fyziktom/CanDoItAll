using System.Text.Json;

namespace CanDoItAll.Memory.Persistence;

internal static class MemoryPersistenceJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    public static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize persisted {typeof(T).Name}.");
    }
}
