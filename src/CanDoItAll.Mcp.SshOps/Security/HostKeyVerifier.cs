using System.Security.Cryptography;
using CanDoItAll.Mcp.SshOps.Configuration;

namespace CanDoItAll.Mcp.SshOps.Security;

public sealed class HostKeyVerifier
{
    public string ComputeSha256Fingerprint(byte[] hostKeyBytes)
    {
        var hash = SHA256.HashData(hostKeyBytes);
        return $"SHA256:{Convert.ToBase64String(hash).TrimEnd('=')}";
    }

    public void EnsureTrusted(string fingerprintSha256, HostKeyVerificationOptions verification, string targetName)
    {
        if (string.Equals(verification.Mode, "none", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var acceptedValues = verification.Values
            .Concat(string.IsNullOrWhiteSpace(verification.Value) ? [] : [verification.Value])
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToArray();

        if (acceptedValues.Contains(fingerprintSha256, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        throw new ToolInvocationException(
            "HostKeyMismatch",
            $"Host key fingerprint mismatch for target '{targetName}'.",
            new
            {
                expected = acceptedValues,
                observed = fingerprintSha256
            });
    }
}
