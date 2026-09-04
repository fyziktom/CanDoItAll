namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentToolCommittedEffect(
    string SourceKind,
    string SourceId);

public sealed class AgentToolInvocationEffectScope : IDisposable
{
    private static readonly AsyncLocal<EffectCapture?> CurrentCapture = new();
    private readonly EffectCapture? previousCapture;
    private readonly EffectCapture capture = new();
    private bool disposed;

    private AgentToolInvocationEffectScope()
    {
        previousCapture = CurrentCapture.Value;
        CurrentCapture.Value = capture;
    }

    public AgentToolCommittedEffect? CommittedEffect => capture.CommittedEffect;

    public static AgentToolInvocationEffectScope Begin()
    {
        return new AgentToolInvocationEffectScope();
    }

    public static void RecordCommitted(string sourceKind, string sourceId)
    {
        if (CurrentCapture.Value is not { } current ||
            string.IsNullOrWhiteSpace(sourceKind) ||
            string.IsNullOrWhiteSpace(sourceId))
        {
            return;
        }

        current.CommittedEffect = new AgentToolCommittedEffect(
            sourceKind.Trim(),
            sourceId.Trim());
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (ReferenceEquals(CurrentCapture.Value, capture))
        {
            CurrentCapture.Value = previousCapture;
        }
    }

    private sealed class EffectCapture
    {
        public AgentToolCommittedEffect? CommittedEffect { get; set; }
    }
}
