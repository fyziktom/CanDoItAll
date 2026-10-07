using System.ComponentModel.DataAnnotations;
using CanDoItAll.Modules.Collaboration;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Collaboration.UI;

public sealed class CollaborationDraft<T>(T model) where T : class {
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
