namespace CanDoItAll.Memory.Mcp;

internal static class McpMemoryProviderHeaderBindingValidator
{
    private const string HttpTokenSymbols = "!#$%&'*+-.^_`|~";

    public static bool IsHttpHeaderName(string? value)
    {
        return !string.IsNullOrEmpty(value) &&
            value.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                HttpTokenSymbols.Contains(character));
    }

    public static bool IsEnvironmentVariableName(string? value)
    {
        if (string.IsNullOrEmpty(value) ||
            !(char.IsAsciiLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        return value.Skip(1).All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character == '_');
    }
}
