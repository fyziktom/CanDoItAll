using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Memory.Routing;

public sealed record MemoryDirectiveParseResult(
    bool Success,
    string Query,
    IReadOnlyList<AgentMemoryProviderAlias> ProviderAliases,
    string Diagnostic)
{
    public static MemoryDirectiveParseResult Parsed(
        string query,
        IReadOnlyList<AgentMemoryProviderAlias> providerAliases) =>
        new(true, query, providerAliases, string.Empty);

    public static MemoryDirectiveParseResult Rejected(string diagnostic) =>
        new(false, string.Empty, [], diagnostic);
}

public static class MemoryDirectiveParser
{
    public const string Prefix = "/mem:";

    public static MemoryDirectiveParseResult Parse(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return MemoryDirectiveParseResult.Parsed(string.Empty, []);
        }

        var position = SkipWhitespace(prompt, 0);
        if (!prompt.AsSpan(position).StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return MemoryDirectiveParseResult.Parsed(prompt.Trim(), []);
        }

        var aliases = new List<AgentMemoryProviderAlias>();
        while (position < prompt.Length &&
               prompt.AsSpan(position).StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            var tokenEnd = position;
            while (tokenEnd < prompt.Length && !char.IsWhiteSpace(prompt[tokenEnd]))
            {
                tokenEnd++;
            }

            var aliasText = prompt[(position + Prefix.Length)..tokenEnd];
            if (!AgentMemoryProviderAlias.TryParse(aliasText, out var alias))
            {
                return MemoryDirectiveParseResult.Rejected(
                    $"Memory directive '{prompt[position..tokenEnd]}' has an invalid provider alias.");
            }

            if (aliases.Contains(alias))
            {
                return MemoryDirectiveParseResult.Rejected(
                    $"Memory provider alias '{alias}' is selected more than once.");
            }

            aliases.Add(alias);

            position = SkipWhitespace(prompt, tokenEnd);
        }

        return MemoryDirectiveParseResult.Parsed(prompt[position..].Trim(), aliases);
    }

    private static int SkipWhitespace(string value, int position)
    {
        while (position < value.Length && char.IsWhiteSpace(value[position]))
        {
            position++;
        }

        return position;
    }
}
