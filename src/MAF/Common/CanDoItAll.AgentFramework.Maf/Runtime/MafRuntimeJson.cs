using System.Text.Json;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafRuntimeJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    public static TConfiguration? DeserializeConfiguration<TConfiguration>(string? json)
        where TConfiguration : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TConfiguration>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
