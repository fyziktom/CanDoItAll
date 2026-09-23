using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Workbench;

/// <summary>
/// Observes, for one project-structure tool invocation, whether the invocation reached a point that may have changed
/// durable state: a leased mutation callback started, or the Workbench store saved domain rows. Lease bookkeeping and
/// operation analytics do not count. Without an active observation nothing is recorded.
/// </summary>
internal sealed class ProjectStructureToolEffectObservation : IDisposable
{
    private static readonly AsyncLocal<ProjectStructureToolEffectObservation?> CurrentObservation = new();

    private readonly ProjectStructureToolEffectObservation? previous;
    private int leasedCallbackStarted;
    private int domainWriteSaved;
    private bool disposed;

    private ProjectStructureToolEffectObservation()
    {
        previous = CurrentObservation.Value;
        CurrentObservation.Value = this;
    }

    public static ProjectStructureToolEffectObservation? Current => CurrentObservation.Value;

    /// <summary>A leased callback may reach other owners or storage, so the invocation may have changed state.</summary>
    public bool MayHaveChangedState => Volatile.Read(ref leasedCallbackStarted) != 0 || DomainWriteSaved;

    /// <summary>The Workbench store saved a project row during this invocation.</summary>
    public bool DomainWriteSaved => Volatile.Read(ref domainWriteSaved) != 0;

    public static ProjectStructureToolEffectObservation Begin() => new();

    public static void RecordLeasedCallbackStarted()
    {
        if (CurrentObservation.Value is { } current)
        {
            Interlocked.Exchange(ref current.leasedCallbackStarted, 1);
        }
    }

    public static void RecordDomainWrite()
    {
        if (CurrentObservation.Value is { } current)
        {
            Interlocked.Exchange(ref current.domainWriteSaved, 1);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (ReferenceEquals(CurrentObservation.Value, this))
        {
            CurrentObservation.Value = previous;
        }
    }
}

internal static class ProjectStructureLeaseRejection
{
    // Operations that create or move whole projects may commit in another owner before they lease a project, so their
    // lease failures keep the unknown effect state.
    private static readonly string[] MultiOwnerOperations =
    [
        "projects.create",
        "projects.subproject-create",
        "projects.subproject-change",
        "structure.nodes-move-to-new-subproject",
        "structure.node-transfer-descendants"
    ];

    /// <summary>
    /// A lease the invocation could not acquire or validate, before any leased callback or domain save, is a proven
    /// no-effect rejection. A conflicting lease stays a conflict; a stale caller token becomes a correctable input.
    /// </summary>
    public static bool TryCreateNoEffect(
        ProjectStructureAgentException exception,
        ProjectStructureToolEffectObservation observation,
        string operationName,
        out ProjectStructureAgentException rejection)
    {
        rejection = exception;
        if (observation.MayHaveChangedState ||
            MultiOwnerOperations.Contains(operationName, StringComparer.Ordinal) ||
            exception.EffectState is not AgentToolEffectState.Unknown)
        {
            return false;
        }

        if (exception is ProjectStructureLeaseConflictException conflict)
        {
            rejection = new ProjectStructureLeaseConflictException(conflict.Conflict, AgentToolEffectState.NotCommitted);
            return true;
        }

        if (string.Equals(exception.ErrorCode, "LeaseMissing", StringComparison.Ordinal))
        {
            rejection = ProjectStructureAgentException.CreateMapped(
                exception.StatusCode,
                exception.ErrorCode,
                $"{exception.Message} Omit leaseToken so the tool leases the project itself, or acquire a new lease, then retry.",
                exception.Details,
                isSafeToExpose: true,
                canRetryWithCorrectedInput: true,
                exception,
                AgentToolEffectState.NotCommitted);
            return true;
        }

        return false;
    }
}
