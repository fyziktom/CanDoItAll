using System.Globalization;
using System.Text.Json;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed record ProjectPendingReference(ProjectDefinitionOperation Operation, ProjectPendingRevision Revision,
    ProjectPendingStage Stage, Guid? TargetProjectId, bool HadUnconfirmedDispatch, bool HadStarterPlan, long StoredBytes) {
    public string Export() => JsonSerializer.Serialize(new {
        format = "candoitall-project-save-reference", version = 1,
        operation = new { @namespace = Operation.Namespace.Value, callerBinding = Operation.CallerBinding.Value,
            epoch = Operation.Epoch.Value.ToString(CultureInfo.InvariantCulture), id = Operation.Id },
        browserRevision = Revision.Value, recordedStage = Stage.ToString(), targetProjectId = TargetProjectId,
        hadUnconfirmedDispatch = HadUnconfirmedDispatch, hadStarterPlan = HadStarterPlan,
        meaning = "Local reference only. This is not accepted or absent proof and cannot authorize a write."
    });
}

public sealed record ProjectPendingReferencePage(IReadOnlyList<ProjectPendingReference> Items,
    ProjectDefinitionOperation? Next, long StoredCount, long StoredBytes);

public sealed record ProjectPendingReferenceChange(ProjectPendingStoreStatus Status, ProjectPendingReference? Reference);

public interface IProjectPendingReferenceStore {
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingReferencePage> ListReferencesAsync(int take, ProjectDefinitionOperation? after = null, CancellationToken cancellationToken = default);
    ValueTask<ProjectPendingReferenceChange> DiscardEvidenceAsync(ProjectPendingReference reference, CancellationToken cancellationToken = default);
}
