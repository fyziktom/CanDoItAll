namespace CanDoItAll.AgentFramework.Mcp;

internal readonly record struct McpPayloadSizeLimit
{
    public const int AbsoluteMaximumBytes = 8 * 1024 * 1024;

    public static McpPayloadSizeLimit Default { get; } = new(AbsoluteMaximumBytes);

    public McpPayloadSizeLimit(int maximumBytes)
    {
        if (maximumBytes is <= 0 or > AbsoluteMaximumBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes),
                $"MCP payload size limit must be between 1 and {AbsoluteMaximumBytes} bytes.");
        }

        MaximumBytes = maximumBytes;
    }

    public int MaximumBytes { get; }

    public void EnsureValid()
    {
        if (MaximumBytes is <= 0 or > AbsoluteMaximumBytes)
        {
            throw new InvalidOperationException(
                $"MCP payload size limit must be between 1 and {AbsoluteMaximumBytes} bytes.");
        }
    }
}
