using System.Text.Json;

namespace CanDoItAll.ComponentKit.Canvas;

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
