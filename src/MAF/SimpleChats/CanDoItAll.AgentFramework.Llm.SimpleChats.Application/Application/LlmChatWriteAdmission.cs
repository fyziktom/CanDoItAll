namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public enum LlmChatWriteResource { Definition, ConversationMetadata, ConversationOperation }

public static class LlmChatWriteAdmission {
    private static readonly AsyncLocal<Admission?> Current = new();

    public static Guid? OriginEditorId => Current.Value is { Active: true } admission ? admission.EditorId : null;

    public static IDisposable Enter(Guid? target, Func<CancellationToken, ValueTask> authorize, Guid? editorId = null,
        LlmChatWriteResource resource = LlmChatWriteResource.Definition) {
        ArgumentNullException.ThrowIfNull(authorize);
        if (Current.Value is not null) {
            throw new InvalidOperationException("A Simple Chat write already has an original opening.");
        }
        var admission = new Admission(target, authorize, editorId, resource);
        Current.Value = admission;
        return new Lease(admission);
    }

    public static async ValueTask RequireTargetAsync(Guid? target, CancellationToken cancellationToken,
        LlmChatWriteResource resource = LlmChatWriteResource.Definition) {
        cancellationToken.ThrowIfCancellationRequested();
        if (Current.Value is not { } admission) {
            return;
        }
        if (admission.Target != target || admission.Resource != resource) {
            throw new UnauthorizedAccessException("The original Simple Chat editor belongs to a different resource or command.");
        }
        await RequireAsync(admission, cancellationToken);
        admission.TargetChecked = true;
    }

    public static async ValueTask RequireCommitAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (Current.Value is not { } admission) {
            return;
        }
        if (!admission.TargetChecked) {
            throw new UnauthorizedAccessException("The Simple Chat mutation did not validate its original target.");
        }
        await RequireAsync(admission, cancellationToken);
    }

    public static async ValueTask RequireCreateAsync(Guid id, CancellationToken cancellationToken,
        LlmChatWriteResource resource = LlmChatWriteResource.Definition) {
        await RequireTargetAsync(null, cancellationToken, resource);
        if (Current.Value is { } admission) {
            admission.Target = id;
        }
    }

    private static async ValueTask RequireAsync(Admission admission, CancellationToken cancellationToken) {
        if (!admission.Active) {
            throw new UnauthorizedAccessException("The original Simple Chat editor was retired.");
        }
        await admission.Authorize(cancellationToken);
        if (!admission.Active || !ReferenceEquals(Current.Value, admission)) {
            throw new UnauthorizedAccessException("The original Simple Chat editor changed during admission.");
        }
    }

    private sealed class Admission(Guid? target, Func<CancellationToken, ValueTask> authorize, Guid? editorId, LlmChatWriteResource resource) {
        public LlmChatWriteResource Resource { get; } = resource;
        public Guid? EditorId { get; } = editorId;
        public Guid? Target { get; set; } = target;
        public Func<CancellationToken, ValueTask> Authorize { get; } = authorize;
        public bool Active { get; set; } = true;
        public bool TargetChecked { get; set; }
    }

    private sealed class Lease(Admission admission) : IDisposable {
        public void Dispose() {
            admission.Active = false;
            if (ReferenceEquals(Current.Value, admission)) {
                Current.Value = null;
            }
        }
    }
}
