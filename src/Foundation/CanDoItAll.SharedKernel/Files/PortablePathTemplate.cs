using System.Text;

namespace CanDoItAll.SharedKernel;

public enum PortablePathTemplateCompatibility
{
    Canonical,
    LegacyWindowsEnvironmentTokens
}

public enum PortablePathTemplateFailure
{
    InvalidTemplate,
    HomeDirectoryUnavailable,
    UnsetVariable,
    ExpansionLimitExceeded
}

public sealed class PortablePathTemplateException : InvalidOperationException
{
    internal PortablePathTemplateException(
        PortablePathTemplateFailure failure,
        string message,
        string? variableName = null)
        : base(message)
    {
        Failure = failure;
        VariableName = variableName;
    }

    public PortablePathTemplateFailure Failure { get; }

    public string? VariableName { get; }
}

public static class PortablePathTemplate
{
    private const int MaximumExpansionPasses = 8;
    private const char EscapedDollarMarker = '\uE000';
    private const char EscapedPercentMarker = '\uE001';

    public static string Expand(
        string template,
        string? homeDirectory,
        Func<string, string?> variableResolver,
        PortablePathTemplateCompatibility compatibility)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(variableResolver);
        if (template.Contains(EscapedDollarMarker) || template.Contains(EscapedPercentMarker))
        {
            throw new PortablePathTemplateException(
                PortablePathTemplateFailure.InvalidTemplate,
                "The configured path contains reserved template characters.");
        }

        var expanded = ExpandHome(template, homeDirectory, compatibility);
        for (var pass = 0; pass < MaximumExpansionPasses; pass++)
        {
            expanded = ExpandVariables(expanded, variableResolver, compatibility, out var expandedAny);
            if (!expandedAny)
            {
                return RestoreEscapedTokens(expanded);
            }
        }

        ExpandVariables(expanded, variableResolver, compatibility, out var stillExpanding);
        if (stillExpanding)
        {
            throw new PortablePathTemplateException(
                PortablePathTemplateFailure.ExpansionLimitExceeded,
                $"Configured path expansion exceeded {MaximumExpansionPasses} passes.");
        }

        return RestoreEscapedTokens(expanded);
    }

    private static string ExpandHome(
        string template,
        string? homeDirectory,
        PortablePathTemplateCompatibility compatibility)
    {
        var usesLegacySeparator =
            compatibility == PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens &&
            template.StartsWith(@"~\", StringComparison.Ordinal);
        if (!string.Equals(template, "~", StringComparison.Ordinal) &&
            !template.StartsWith("~/", StringComparison.Ordinal) &&
            !usesLegacySeparator)
        {
            return template;
        }

        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            throw new PortablePathTemplateException(
                PortablePathTemplateFailure.HomeDirectoryUnavailable,
                "The configured path uses '~', but the user home directory is unavailable.");
        }

        if (template.Length == 1)
        {
            return homeDirectory;
        }

        return usesLegacySeparator
            ? homeDirectory.TrimEnd('/', '\\') + '/' + template[2..]
            : homeDirectory.TrimEnd('/', '\\') + template[1..];
    }

    private static string ExpandVariables(
        string value,
        Func<string, string?> variableResolver,
        PortablePathTemplateCompatibility compatibility,
        out bool expandedAny)
    {
        var result = new StringBuilder(value.Length);
        expandedAny = false;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '$' && index + 2 < value.Length && value[index + 1] == '$' && value[index + 2] == '{')
            {
                result.Append(EscapedDollarMarker);
                index++;
                continue;
            }

            if (value[index] == '$' && index + 1 < value.Length && value[index + 1] == '{')
            {
                var endIndex = value.IndexOf('}', index + 2);
                if (endIndex < 0)
                {
                    throw InvalidTemplate("An explicit variable token is missing its closing '}'.");
                }

                var variableName = value[(index + 2)..endIndex];
                ValidateVariableName(variableName);
                result.Append(ResolveVariable(variableName, variableResolver));
                expandedAny = true;
                index = endIndex;
                continue;
            }

            if (compatibility == PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens &&
                value[index] == '%' &&
                index + 1 < value.Length &&
                value[index + 1] == '%')
            {
                result.Append(EscapedPercentMarker);
                index++;
                continue;
            }

            if (compatibility == PortablePathTemplateCompatibility.LegacyWindowsEnvironmentTokens && value[index] == '%')
            {
                var endIndex = value.IndexOf('%', index + 1);
                if (endIndex < 0)
                {
                    throw InvalidTemplate("A legacy Windows variable token is missing its closing '%'.");
                }

                var variableName = value[(index + 1)..endIndex];
                ValidateVariableName(variableName);
                result.Append(ResolveVariable(variableName, variableResolver));
                expandedAny = true;
                index = endIndex;
                if (index + 1 < value.Length && value[index + 1] == '\\')
                {
                    result.Append('/');
                    index++;
                }

                continue;
            }

            result.Append(value[index]);
        }

        return result.ToString();
    }

    private static string ResolveVariable(string variableName, Func<string, string?> variableResolver)
    {
        var value = variableResolver(variableName);
        if (value is not null)
        {
            return value;
        }

        throw new PortablePathTemplateException(
            PortablePathTemplateFailure.UnsetVariable,
            $"Configured path variable '{variableName}' is not set.",
            variableName);
    }

    private static void ValidateVariableName(string variableName)
    {
        if (variableName.Length == 0 ||
            !(char.IsAsciiLetter(variableName[0]) || variableName[0] == '_') ||
            variableName.Skip(1).Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_')))
        {
            throw InvalidTemplate("Configured path variable names may contain only ASCII letters, digits, and underscores and may not start with a digit.");
        }
    }

    private static PortablePathTemplateException InvalidTemplate(string message)
    {
        return new PortablePathTemplateException(PortablePathTemplateFailure.InvalidTemplate, message);
    }

    private static string RestoreEscapedTokens(string value)
    {
        return value
            .Replace(EscapedDollarMarker, '$')
            .Replace(EscapedPercentMarker, '%');
    }
}
