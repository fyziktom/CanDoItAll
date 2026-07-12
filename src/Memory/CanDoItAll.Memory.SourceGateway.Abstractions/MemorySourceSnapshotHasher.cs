using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Memory.SourceGateway;

public static class MemorySourceSnapshotHasher
{
    public static string Compute(params string?[] parts)
    {
        var normalized = string.Join(
            '\u001f',
            parts.Select(part => part?.ReplaceLineEndings("\n").Trim() ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
