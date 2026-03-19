namespace CanDoItAll.ComponentKit.Components;

public sealed record TuningBoundaryRequest(
    string CapsuleKey,
    string ComponentName,
    string? ContextSummary,
    string? Route,
    Guid? ProjectId,
    string? TabId,
    string? SelectionId);

public sealed class TuningCoordinator
{
    public event Action? Changed;

    public TuningBoundaryRequest? CurrentRequest { get; private set; }

    public void Open(TuningBoundaryRequest request)
    {
        CurrentRequest = request;
        Changed?.Invoke();
    }

    public void Clear()
    {
        CurrentRequest = null;
        Changed?.Invoke();
    }
}
