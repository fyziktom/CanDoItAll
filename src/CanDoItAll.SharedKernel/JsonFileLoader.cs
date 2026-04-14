using System.Text.Json;

namespace CanDoItAll.SharedKernel;

public static class JsonFileLoader
{
    private static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static T ReadRequired<T>(string path, JsonSerializerOptions? options = null)
        where T : class, new()
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions) ?? new T();
    }

    public static T ReadOptional<T>(string path, JsonSerializerOptions? options = null)
        where T : new()
    {
        if (!File.Exists(path))
        {
            return new T();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions) ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }
}
