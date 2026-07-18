namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentReferenceDataInvalidationHub : IAgentReferenceDataCacheInvalidator
{
    public event EventHandler? Invalidated;

    public void Invalidate()
    {
        EventHandlerNotification.NotifyAll(Invalidated, this);
    }
}
