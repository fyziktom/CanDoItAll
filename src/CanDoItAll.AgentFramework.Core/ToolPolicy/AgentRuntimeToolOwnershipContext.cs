namespace CanDoItAll.AgentFramework.Core;

public static class AgentRuntimeToolOwnershipContext
{
    private static readonly AsyncLocal<AgentRuntimeToolOwnership?> CurrentOwnership = new();

    public static AgentRuntimeToolOwnership? Current => CurrentOwnership.Value;

    public static IDisposable BeginScope(AgentRuntimeToolOwnership? ownership)
    {
        var previous = CurrentOwnership.Value;
        CurrentOwnership.Value = ownership;
        return new Scope(previous);
    }

    private sealed class Scope(AgentRuntimeToolOwnership? previous) : IDisposable
    {
        public void Dispose()
        {
            CurrentOwnership.Value = previous;
        }
    }
}

public sealed record AgentRuntimeToolOwnership(
    string ProviderKey,
    string ProviderName,
    string ToolName);
