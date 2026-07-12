namespace CanDoItAll.Memory.Http;

internal static class HttpMemoryProviderHeaderBindingValidator
{
    private const string HttpTokenSymbols = "!#$%&'*+-.^_`|~";

    public static bool IsHttpToken(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.All(character =>
                   char.IsAsciiLetterOrDigit(character) ||
                   HttpTokenSymbols.Contains(character));
    }

    public static bool IsEnvironmentVariableName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !(char.IsAsciiLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        return value.Skip(1).All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character == '_');
    }
}
