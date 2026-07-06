namespace CanDoItAll.Memory.Mcp;

public sealed class McpMemoryProviderOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public void Validate()
    {
        if (DefaultTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("MCP memory provider default timeout must be positive.");
        }
    }
}
