namespace CanDoItAll.Mcp.DotNetWatch.Runtime;

public sealed class ServerInstanceIdentity
{
    public string Id { get; } = $"srv_{Guid.NewGuid():N}";
}
