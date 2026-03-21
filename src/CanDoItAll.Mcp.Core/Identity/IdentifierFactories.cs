namespace CanDoItAll.Mcp.Core.Identity;

public static class CorrelationIdFactory
{
    public static string Create(string prefix = "corr")
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }
}

public static class OperationIdFactory
{
    public static string Create(string prefix = "op")
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }
}

public class ServerInstanceIdentity
{
    public string Id { get; } = CorrelationIdFactory.Create("srv");
}
