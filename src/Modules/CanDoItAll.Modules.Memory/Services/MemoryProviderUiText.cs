namespace CanDoItAll.Modules.Memory.Services;

internal static class MemoryProviderUiText
{
    public static string Normalize(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty.", parameterName);
        }

        return value.Trim();
    }
}
