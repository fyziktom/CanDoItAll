namespace CanDoItAll.AgentFramework.Mcp;

public static class McpEnvironmentVariableNamePolicy
{
    private const string HttpTokenSymbols = "!#$%&'*+-.^_`|~";

    public static bool IsValid(string? value)
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

    public static bool IsValidRuntimeName(string? value)
    {
        return !string.IsNullOrEmpty(value) &&
               !value.Contains('=') &&
               !value.Contains('\0');
    }

    public static bool IsValidHttpHeaderName(string? value)
    {
        return !string.IsNullOrEmpty(value) &&
               value.All(character =>
                   char.IsAsciiLetterOrDigit(character) ||
                   HttpTokenSymbols.Contains(character));
    }
}
