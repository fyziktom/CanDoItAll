using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;

namespace CanDoItAll.Modules.CrmHr;

public sealed class CrmHrChanges(EditingChangeFeed feed, EditingScope scope, EditingOperationContext operations) {
    private readonly AsyncLocal<Func<CancellationToken, ValueTask>?> commitAdmission = new();
    public EditingScope Scope { get; } = scope;
    public IEditingChangeSubscription Subscribe(EditingOwner owner, Guid? id = null) => feed.Subscribe(new(Scope, owner, id));
    public IDisposable Enter(EditingOperationIdentity operation) => operations.Enter(operation);

    public IDisposable EnterUiWrite(Func<CancellationToken, ValueTask> admission) {
        var previous = commitAdmission.Value;
        commitAdmission.Value = admission;
        return new AdmissionScope(() => commitAdmission.Value = previous);
    }

    internal ValueTask DemandCommitAsync(CancellationToken cancellationToken) =>
        commitAdmission.Value?.Invoke(cancellationToken) ?? ValueTask.CompletedTask;

    public void PublishParty(Guid id, EditingChangeKind kind = EditingChangeKind.EditableContentChanged) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.CrmHrParty, id), kind, origin?.EditorId, origin?.OperationId);
        feed.Publish(new(Scope, EditingOwner.CrmAccountProfile, id), kind, origin?.EditorId, origin?.OperationId);
        feed.Publish(new(Scope, EditingOwner.CrmWorkforceProfile, id), kind, origin?.EditorId, origin?.OperationId);
        feed.Publish(new(Scope, EditingOwner.CrmAiStewardProfile, id), kind, origin?.EditorId, origin?.OperationId);
    }

    public void PublishAffiliations(Guid personId) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.CrmHrAffiliations, personId), EditingChangeKind.EditableContentChanged, origin?.EditorId, origin?.OperationId);
        feed.Publish(new(Scope, EditingOwner.CrmHrParty, personId), EditingChangeKind.MetadataChanged, origin?.EditorId, origin?.OperationId);
    }

    public void PublishOpportunity(Guid id, EditingChangeKind kind = EditingChangeKind.EditableContentChanged) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.CrmOpportunity, id), kind, origin?.EditorId, origin?.OperationId);
    }

    public void PublishAccountRecords(Guid accountId, EditingChangeKind kind) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.CrmAccountRecords, accountId), kind, origin?.EditorId, origin?.OperationId);
    }

    public void PublishWorkforceRecords(Guid partyId, EditingChangeKind kind) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.CrmWorkforceRecords, partyId), kind, origin?.EditorId, origin?.OperationId);
    }

    public void PublishSkillCatalog(Guid skillId, EditingChangeKind kind) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.CrmSkillCatalog, skillId), kind, origin?.EditorId, origin?.OperationId);
    }

    public void PublishStaffing(Guid id, Guid? projectId, Guid? previousProjectId = null,
        EditingChangeKind kind = EditingChangeKind.EditableContentChanged) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.CrmStaffingRequest, id), kind, origin?.EditorId, origin?.OperationId);
        foreach (var project in new[] { projectId, previousProjectId }.OfType<Guid>().Distinct()) {
            feed.Publish(new(Scope, EditingOwner.CrmStaffingRecords, project), EditingChangeKind.EditableContentChanged, origin?.EditorId, origin?.OperationId);
        }
    }

    public void PublishRelationships(IEnumerable<Guid> partyIds) {
        var origin = operations.Current;
        foreach (var id in partyIds.Distinct()) {
            feed.Publish(new(Scope, EditingOwner.CrmHrRelationships, id), EditingChangeKind.EditableContentChanged, origin?.EditorId, origin?.OperationId);
            feed.Publish(new(Scope, EditingOwner.CrmHrParty, id), EditingChangeKind.MetadataChanged, origin?.EditorId, origin?.OperationId);
        }
    }

    public void PublishRecruitment(Guid id) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.CrmRecruitmentApplication, id), EditingChangeKind.EditableContentChanged, origin?.EditorId, origin?.OperationId);
    }

    public void PublishRecruitmentRecords(Guid id) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.CrmRecruitmentInterviews, id), EditingChangeKind.EditableContentChanged, origin?.EditorId, origin?.OperationId);
    }

    public void PublishRecruitmentTasks(Guid id) {
        var origin = operations.Current;
        feed.Publish(new(Scope, EditingOwner.CrmRecruitmentTasks, id), EditingChangeKind.EditableContentChanged, origin?.EditorId, origin?.OperationId);
    }

    private sealed class AdmissionScope(Action restore) : IDisposable {
        public void Dispose() => restore();
    }
}
