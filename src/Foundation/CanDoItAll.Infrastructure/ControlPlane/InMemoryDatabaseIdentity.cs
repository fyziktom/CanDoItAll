using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Infrastructure.ControlPlane;

internal static class InMemoryDatabaseIdentity
{
    private const string DefaultDatabaseName = "candoitall";

    public static string ResolveOverrideName(string? configuredConnection)
    {
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            return DefaultDatabaseName;
        }

        string candidate = configuredConnection.Trim();
        return LooksLikeExternalConnection(candidate)
            ? DefaultDatabaseName
            : candidate;
    }

    public static string CreateFingerprint(string? databaseName)
    {
        string normalized = string.IsNullOrWhiteSpace(databaseName)
            ? DefaultDatabaseName
            : databaseName.Trim();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"inmemory:{Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant()}";
    }

    private static bool LooksLikeExternalConnection(string value)
        => value.Contains('=') ||
           value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
           value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
}
