namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentDirectoryProjectionSynchronizationException(
    Guid agentId,
    Exception innerException)
    : Exception(
        $"Agent '{agentId:D}' was saved, but its CRM/HR directory projection could not be synchronized.",
        innerException)
{
    public Guid AgentId { get; } = agentId;
}
