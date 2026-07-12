using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Memory.Services;

public readonly record struct MemoryProviderCredentialReference
{
    private static readonly Regex EnvironmentVariablePattern = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private MemoryProviderCredentialReference(string environmentVariableName)
    {
        EnvironmentVariableName = environmentVariableName;
    }

    public string EnvironmentVariableName { get; }

    public static MemoryProviderCredentialReference? ParseOptional(
        string? value,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (!EnvironmentVariablePattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                "Credential reference must be a valid environment variable name, not a secret value.",
                parameterName);
        }

        return new MemoryProviderCredentialReference(normalized);
    }
}
