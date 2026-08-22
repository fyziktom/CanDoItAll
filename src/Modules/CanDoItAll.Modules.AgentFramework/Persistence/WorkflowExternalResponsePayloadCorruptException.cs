namespace CanDoItAll.Modules.AgentFramework;

public sealed class WorkflowExternalResponsePayloadCorruptException : InvalidOperationException
{
    internal WorkflowExternalResponsePayloadCorruptException(
        Guid operationId,
        Exception? innerException = null)
        : base(
            $"Workflow external response operation '{operationId}' has a corrupt protected payload.",
            innerException)
    {
    }
}
