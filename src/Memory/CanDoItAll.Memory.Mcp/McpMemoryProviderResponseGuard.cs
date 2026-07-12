using System.Text;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Mcp;

internal static class McpMemoryProviderResponseGuard
{
    public static void EnsureWithinLimit(
        string responseJson,
        MemoryProviderResponseSizeLimit sizeLimit)
    {
        ArgumentNullException.ThrowIfNull(responseJson);
        sizeLimit.EnsureValid();

        if (responseJson.Length > sizeLimit.MaximumBytes ||
            Encoding.UTF8.GetByteCount(responseJson) > sizeLimit.MaximumBytes)
        {
            throw new McpMemoryProviderResponseTooLargeException(sizeLimit);
        }
    }
}

internal sealed class McpMemoryProviderResponseTooLargeException(
    MemoryProviderResponseSizeLimit sizeLimit) : Exception
{
    public MemoryProviderResponseSizeLimit SizeLimit { get; } = sizeLimit;
}
