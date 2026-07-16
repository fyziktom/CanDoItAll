using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.AgentFramework.Core;

internal static class AgentChatContextDigest
{
    public static string Compute(string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            .ToLowerInvariant();
    }
}
