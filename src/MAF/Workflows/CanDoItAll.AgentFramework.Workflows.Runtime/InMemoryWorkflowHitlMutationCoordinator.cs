namespace CanDoItAll.AgentFramework.Core;

internal sealed class InMemoryWorkflowHitlMutationCoordinator
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task EnterAsync(CancellationToken cancellationToken)
        => gate.WaitAsync(cancellationToken);

    public void Enter(CancellationToken cancellationToken = default)
        => gate.Wait(cancellationToken);

    public void Exit()
        => gate.Release();
}
