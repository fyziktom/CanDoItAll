using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.SharedKernel;

public static class StableContentHash
{
    public static string ComputeSha256Hex(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ComputeShortSha256Hex(string text, int byteCount = 6)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (byteCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount), byteCount, "Hash byte count must be positive.");
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        if (byteCount > bytes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount), byteCount, "Hash byte count cannot exceed the SHA-256 byte length.");
        }

        return Convert.ToHexString(bytes, 0, byteCount).ToLowerInvariant();
    }
}
