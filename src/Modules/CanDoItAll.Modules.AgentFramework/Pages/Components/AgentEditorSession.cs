using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Editor.UI;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.AgentFramework;

public readonly record struct AgentEditorTarget(Guid? AgentId) {
    public bool IsNew => !AgentId.HasValue;
    public static AgentEditorTarget Create => new(null);
}

public enum AgentEditorMutationKind { Save, CapabilityVerification }

public sealed record AgentEditorPendingRefresh(Guid AgentId, AgentEditorSubmission Submission, AgentEditorMutationKind Kind);

public sealed class AgentEditorSession : IDisposable {
    private readonly CancellationTokenSource cancellation = new();

    public AgentEditorSession(AgentEditorTarget target) {
        Target = target;
        Draft = new();
        Context = new(Draft);
        Context.OnFieldChanged += FieldChanged;
        Access = new(Origin, Draft);
        Memory = new(Draft.MemoryAccess);
        CancellationToken = cancellation.Token;
    }

    public AgentEditorOrigin Origin { get; } = new(Guid.NewGuid());
    public AgentEditorTarget Target { get; private set; }
    public AgentEditorModel Draft { get; private set; }
    public AgentEditorModel? Original { get; private set; }
    public DateTimeOffset? OriginalCreatedAtUtc { get; private set; }
    public EditContext Context { get; private set; }
    public AgentEditorAccessState Access { get; }
    public AgentMemoryEditorState Memory { get; }
    public AgentRootEntry RootEntry { get; } = new();
    public string PendingTagInput { get; set; } = string.Empty;
    public string EntryFormId => $"agent-entry-{Origin.Value:N}";
    public CancellationToken CancellationToken { get; }
    public bool IsDisposed { get; private set; }
    public long InputRevision { get; private set; }
    public void NoteInput() => InputRevision++;
    private void FieldChanged(object? sender, FieldChangedEventArgs args) => NoteInput();
    public AgentEditorPendingRefresh? PendingRefresh { get; private set; }
    public bool HasUnconfirmedWrite { get; private set; }
    public string? CommitWarning { get; private set; }
    public AgentEditorVerification? Verification { get; set; }

    public void SetCommitWarning(string? warning) => CommitWarning = warning;
    public bool CanWrite => !IsDisposed && PendingRefresh is null && !HasUnconfirmedWrite && Verification?.BlocksWrites != true;

    public void AcknowledgeMutation(Guid agentId, AgentEditorSubmission submission, AgentEditorMutationKind kind = AgentEditorMutationKind.Save) {
        BindIdentity(agentId);
        PendingRefresh = new(agentId, submission, kind);
    }

    public void CompleteReconciliation() => PendingRefresh = null;

    public void MarkWriteUnconfirmed() => HasUnconfirmedWrite = true;

    public void Load(AgentEditorModel draft, DateTimeOffset? createdAtUtc = null) {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        ArgumentNullException.ThrowIfNull(draft);
        Draft = draft;
        Original = AgentEditorDraftPolicy.Copy(draft);
        OriginalCreatedAtUtc = createdAtUtc;
        Access.Draft = draft;
        Memory.UpdateValue(draft.MemoryAccess);
        Context.OnFieldChanged -= FieldChanged;
        Context = new(draft);
        Context.OnFieldChanged += FieldChanged;
        Target = new(draft.Id);
    }

    public void BindIdentity(Guid agentId) {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        Draft.Id = agentId;
        Target = new(agentId);
    }

    public void AcceptOriginal(AgentEditorModel accepted, DateTimeOffset? createdAtUtc = null) {
        Original = AgentEditorDraftPolicy.Copy(accepted);
        OriginalCreatedAtUtc = createdAtUtc ?? OriginalCreatedAtUtc;
    }

    public void Dispose() {
        if (IsDisposed) {
            return;
        }
        IsDisposed = true;
        Context.OnFieldChanged -= FieldChanged;
        cancellation.Cancel();
        cancellation.Dispose();
    }
}
