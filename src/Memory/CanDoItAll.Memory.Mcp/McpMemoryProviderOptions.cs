using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Mcp;

public sealed class McpMemoryProviderOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public MemoryProviderResponseSizeLimit ResponseSizeLimit { get; set; } =
        MemoryProviderResponseSizeLimit.Default;

    public void Validate()
    {
        if (DefaultTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("MCP memory provider default timeout must be positive.");
        }

        ResponseSizeLimit.EnsureValid();
    }
}
