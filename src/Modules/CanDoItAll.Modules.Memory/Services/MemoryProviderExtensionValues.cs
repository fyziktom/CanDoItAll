using System.Text.Json;

namespace CanDoItAll.Modules.Memory.Services;

internal static class MemoryProviderExtensionValues
{
    public static string ReadString(
        IReadOnlyDictionary<string, JsonElement> values,
        string key,
        string defaultValue = "")
    {
        if (!values.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return defaultValue;
        }

        return value.GetString()?.Trim() ?? defaultValue;
    }

    public static int ReadInt(
        IReadOnlyDictionary<string, JsonElement> values,
        string key,
        int defaultValue)
    {
        return values.TryGetValue(key, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out var parsed)
            ? parsed
            : defaultValue;
    }

    public static void SetString(
        IDictionary<string, JsonElement> values,
        string key,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            values.Remove(key);
            return;
        }

        values[key] = JsonSerializer.SerializeToElement(value.Trim());
    }

    public static void SetNumber(
        IDictionary<string, JsonElement> values,
        string key,
        int value,
        int minimum)
    {
        if (value < minimum)
        {
            throw new ArgumentOutOfRangeException(key, $"Value must be at least {minimum}.");
        }

        values[key] = JsonSerializer.SerializeToElement(value);
    }
}
