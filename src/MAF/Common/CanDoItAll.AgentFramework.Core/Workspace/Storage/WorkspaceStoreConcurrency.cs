using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record SandboxWorkspaceCatalogSnapshot(
    SandboxWorkspaceCatalog Catalog,
    CatalogDataRevision Revision);

public sealed record SandboxWorkspaceDocumentSnapshot(
    SandboxWorkspaceDocument Document,
    long Revision);

public static class ExecutionRunSessionConcurrencyPolicy
{
    public static bool BlocksSession(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return run.PendingApprovals.Count > 0 ||
               BlocksSession(run.State);
    }

    public static bool BlocksSession(ExecutionState state)
        => state is ExecutionState.Preparing
            or ExecutionState.Running
            or ExecutionState.WaitingOnTool
            or ExecutionState.Persisting;
}

public sealed class SandboxWorkspaceCatalogConcurrencyException : InvalidOperationException
{
    public SandboxWorkspaceCatalogConcurrencyException(
        CatalogDataRevision expectedRevision,
        CatalogDataRevision actualRevision)
        : base(
            $"Catalog update rejected because expected data revision {expectedRevision.Value} did not match current data revision {actualRevision.Value}. Reload the catalog snapshot and retry the operation.")
    {
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    public CatalogDataRevision ExpectedRevision { get; }

    public CatalogDataRevision ActualRevision { get; }
}

public sealed class SandboxWorkspaceConcurrencyException : InvalidOperationException
{
    public SandboxWorkspaceConcurrencyException(long expectedRevision, long actualRevision)
        : base(
            $"Workspace update rejected because expected revision {expectedRevision} did not match current revision {actualRevision}. Reload the workspace snapshot and retry the mutation.")
    {
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    public long ExpectedRevision { get; }

    public long ActualRevision { get; }
}
