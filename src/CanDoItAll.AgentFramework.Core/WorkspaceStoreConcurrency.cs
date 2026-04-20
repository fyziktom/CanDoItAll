using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed record SandboxWorkspaceDocumentSnapshot(
    SandboxWorkspaceDocument Document,
    long Revision);

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
