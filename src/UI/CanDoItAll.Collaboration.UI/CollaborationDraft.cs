using System.ComponentModel.DataAnnotations;
using CanDoItAll.Modules.Collaboration;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Collaboration.UI;

public sealed class CollaborationDraft<T>(T model) where T : class {
    public Guid SubmissionId { get; } = Guid.NewGuid();
    public T Model { get; } = model;
    public EditContext Context { get; } = new(model);
    public bool IsSaving { get; private set; }
    public bool OutcomeUnknown { get; private set; }
    public bool IsLocked => IsSaving || OutcomeUnknown;
    public string? Message { get; set; }
    public Guid? SavedThreadId { get; set; }

    public bool TryBegin() {
        if (IsLocked || !Context.Validate() || !Validator.TryValidateObject(Model, new ValidationContext(Model), [], true)) {
            return false;
        }

        IsSaving = true;
        Message = null;
        return true;
    }

    public void Complete(bool unknown = false) {
        IsSaving = false;
        OutcomeUnknown = unknown;
    }
}

public sealed class CollaborationTarget(Guid threadId) {
    public Guid ThreadId { get; } = threadId;
    public bool IsMarkingRead { get; set; }
    public bool OutcomeUnknown { get; set; }
    public string? Message { get; set; }
}

public static class CollaborationReplyPolicy {
    public static bool MustRetain(CollaborationDraft<CollaborationReplyEditorModel> draft) =>
        draft.IsLocked || draft.Context.IsModified() || !string.IsNullOrEmpty(draft.Model.MessageBody);
}

public enum CollaborationDraftStatus { Clean, Unsaved, Submitting, Unconfirmed }

public static class CollaborationDraftLifetime {
    public static CollaborationDraftStatus Status(CollaborationDraft<CollaborationThreadEditorModel> thread,
        CollaborationDraft<CollaborationReplyEditorModel> reply, IReadOnlyCollection<CollaborationDraft<CollaborationReplyEditorModel>> retained,
        CollaborationTarget? target) {
        if (thread.IsSaving || reply.IsSaving || target?.IsMarkingRead == true || retained.Any(draft => draft.IsSaving)) {
            return CollaborationDraftStatus.Submitting;
        }
        if (thread.OutcomeUnknown || reply.OutcomeUnknown || target?.OutcomeUnknown == true || retained.Any(draft => draft.OutcomeUnknown)) {
            return CollaborationDraftStatus.Unconfirmed;
        }
        var model = thread.Model;
        return thread.Context.IsModified() || !string.IsNullOrEmpty(model.Subject) || !string.IsNullOrEmpty(model.MessageBody) ||
            !string.IsNullOrEmpty(model.ContextLabel) || !string.IsNullOrEmpty(model.ContextRoute) ||
            CollaborationReplyPolicy.MustRetain(reply) || retained.Any(CollaborationReplyPolicy.MustRetain)
            ? CollaborationDraftStatus.Unsaved : CollaborationDraftStatus.Clean;
    }
}
