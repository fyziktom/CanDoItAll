using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.AgentFramework;

public enum AgentEditorSection {
    Identity,
    Runtime,
    Memory,
    Images,
    ProjectStructureAccess,
    WorkspaceTools,
    Secrets,
    ProcessAccess,
    Capabilities,
    Voice
}

public readonly record struct AgentEditorTarget(Guid? AgentId) {
    public bool IsNew => !AgentId.HasValue;
    public static AgentEditorTarget Create => new(null);
}

public enum AgentEditorLoadState { Loading, Ready, Failed }

public enum AgentEditorMutationKind { Save, CapabilityVerification }

public sealed record AgentEditorPendingRefresh(Guid AgentId, AgentEditorSubmission Submission, AgentEditorMutationKind Kind);

public sealed class AgentEditorSession : IDisposable {
    private readonly CancellationTokenSource cancellation = new();

    public AgentEditorSession(AgentEditorTarget target) {
        Target = target;
        Draft = new();
        Context = new(Draft);
        CancellationToken = cancellation.Token;
    }

    public AgentEditorTarget Target { get; private set; }
    public AgentEditorModel Draft { get; private set; }
    public EditContext Context { get; private set; }
    public CancellationToken CancellationToken { get; }
    public bool IsDisposed { get; private set; }
    public AgentEditorPendingRefresh? PendingRefresh { get; private set; }
    public bool HasUnconfirmedWrite { get; private set; }
    public string? CommitWarning { get; private set; }

    public void SetCommitWarning(string? warning) => CommitWarning = warning;
    public bool CanWrite => !IsDisposed && PendingRefresh is null && !HasUnconfirmedWrite;

    public void AcknowledgeMutation(Guid agentId, AgentEditorSubmission submission, AgentEditorMutationKind kind = AgentEditorMutationKind.Save) {
        BindIdentity(agentId);
        PendingRefresh = new(agentId, submission, kind);
    }

    public void CompleteReconciliation() => PendingRefresh = null;

    public void MarkWriteUnconfirmed() => HasUnconfirmedWrite = true;

    public void Load(AgentEditorModel draft) {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        Draft = draft;
        Context = new(draft);
        Target = new(draft.Id);
    }

    public void BindIdentity(Guid agentId) {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Draft.Id = agentId;
        Target = new(agentId);
    }

    public void Dispose() {
        if (IsDisposed) {
            return;
        }
        IsDisposed = true;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
