using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Operators.UI.Parties;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectPartyEditorSession(ProjectWriteAdmission admission, ProjectStructureNode node,
    IProjectPartyIntegrationBridge parties, IProjectNodeAssignmentPolicyBridge policy, ProjectWorkbenchService workbench,
    ProjectPartyPresentationWriter presentation, Func<bool> current, Func<bool> originalAuthority, Func<ProjectStructureNode, Task> publish,
    Action<ProjectWriteAdmission, string, PartyCreated> recordCreate, ILogger logger) {
    private ProjectStructureNode expectedNode = node;
    private IReadOnlyList<ProjectPartyAssignmentDetail> assignments = [];
    private IReadOnlyList<ProjectPartyOption> options = [];
    private readonly ProjectNodeAssignmentSemantics semantics = policy.Resolve(node.ObjectType, node.ObjectSubtype);
    private long readGeneration;
    public PartyEditorState State { get; } = new(node.ObjectType == ProjectObjectType.Participant ? PartyEditorKind.Participant : PartyEditorKind.Meeting, node.Title);
    public string NodeId => expectedNode.Id;
    public bool IsCurrent => current() && !State.IsRetired;

    public void Retire() {
        State.IsRetired = true;
        readGeneration++;
    }

    public async Task LoadAsync() {
        var generation = ++readGeneration;
        State.IsLoading = true;
        try {
            await workbench.RequireContentCurrentAsync(admission, expectedNode);
            var loadedOptions = await parties.ListPartyOptionsAsync(admission.ProjectId);
            var loadedAssignments = await parties.ListAssignmentsDetailedAsync(admission.ProjectId);
            if (!IsCurrent || generation != readGeneration) {
                return;
            }
            options = loadedOptions;
            assignments = NodeAssignments(loadedAssignments);
            State.ProjectDefaults = loadedAssignments.Where(item => string.IsNullOrWhiteSpace(item.NodeKey)).Select(item => item.PartyId).Distinct().ToArray();
            State.Draft.Participant = assignments.OrderByDescending(item => item.IsPrimary).FirstOrDefault()?.PartyId;
            State.Draft.KeepLocal = State.Draft.Participant is null;
            State.Draft.MeetingParties.UnionWith(assignments.Select(item => item.PartyId));
            ProjectChoices();
            State.IsUnavailable = false;
        } catch (Exception failure) {
            if (IsCurrent && generation == readGeneration) {
                State.IsUnavailable = true;
                State.Message = "Relationships could not be loaded. Reload before changing assignments.";
            }
            Log(failure, "load");
        } finally {
            if (IsCurrent && generation == readGeneration) {
                State.IsLoading = false;
            }
        }
    }

    public async Task CreateAsync(PartyQuickCreateInput input) {
        if (!IsCurrent || !State.CanEdit || State.Created is not null || State.Kind != PartyEditorKind.Participant) {
            return;
        }
        var request = new ProjectPartyQuickCreateRequest {
            ProjectId = admission.ProjectId, ExpectedProjectAdmission = admission,
            PartyKind = input.Kind switch {
                PartyQuickCreateKind.Person => ProjectPartyQuickCreateKind.Person,
                PartyQuickCreateKind.Organization => ProjectPartyQuickCreateKind.Organization,
                PartyQuickCreateKind.OrganizationUnit => ProjectPartyQuickCreateKind.OrganizationUnit,
                PartyQuickCreateKind.AiAgent => ProjectPartyQuickCreateKind.AiAgent,
                _ => throw new ArgumentOutOfRangeException(nameof(input))
            },
            DisplayName = input.Name, Email = input.Email, Phone = input.Phone, Summary = input.Summary
        };
        State.IsBusy = true;
        var dispatched = false;
        try {
            await workbench.RequireContentCurrentAsync(admission, expectedNode);
            if (!IsCurrent) {
                return;
            }
            dispatched = true;
            var result = await parties.CreatePartyAsync(request);
            if (result.IsFailure) {
                State.Message = result.Errors.FirstOrDefault()?.Message ?? "Unable to create the party.";
                return;
            }
            var accepted = result.Value ?? throw new InvalidOperationException("Party creation returned no accepted identity.");
            State.Created = new(accepted.PartyId, accepted.DisplayName, accepted.ObservationWarning);
            recordCreate(admission, NodeId, State.Created);
            await ObserveCreatedAsync();
        } catch (Exception failure) {
            State.RequiresObservation = dispatched && State.Created is null && !KnownRejection(failure);
            State.Message = State.Created is { } accepted
                ? $"Directory party {accepted.Id:D} was created. Its options could not be refreshed. Retry the read; do not create it again."
                : State.RequiresObservation ? "Directory creation has no confirmed result. Inspect the original directory before creating another party."
                : "The original participant is no longer available for quick-create. Reload its editor.";
            Log(failure, "create");
        } finally {
            State.IsBusy = false;
        }
    }

    public async Task SaveAsync(PartySelection selection) {
        if (!IsCurrent || !State.CanEdit) {
            return;
        }
        var selected = State.Kind == PartyEditorKind.Participant
            ? selection.KeepLocal || selection.Participant is null ? [] : new[] { selection.Participant.Value }
            : selection.MeetingParties.Distinct().ToArray();
        State.IsBusy = true;
        State.Receipt = new(PartySavePhase.NotSubmitted, [], null, "Preparing the original relationship change.");
        var dispatched = false;
        try {
            await workbench.RequireContentCurrentAsync(admission, expectedNode);
            var selectedOptions = new List<ProjectPartyOption>();
            foreach (var id in selected) {
                var option = await parties.GetPartyOptionAsync(id);
                if (option is null && assignments.All(item => item.PartyId != id)) {
                    State.Message = "A selected party could not be loaded. No assignment change was submitted.";
                    return;
                }
                if (option is not null) {
                    selectedOptions.Add(option);
                }
            }
            if (!IsCurrent) {
                return;
            }
            var desired = DesiredAssignments(selected, selectedOptions);
            dispatched = true;
            var result = await parties.ReplaceNodeAssignmentsIfCurrentAsync(admission.ProjectId, new(NodeId), desired,
                semantics.ReplacementRoles, assignments.Select(ProjectPartyAssignmentConcurrencySnapshot.From).ToArray(),
                new ProjectPartyNodeOccurrence(expectedNode.RecordId ?? throw new InvalidOperationException("The original node has no record identity."),
                    expectedNode.ObjectType, expectedNode.ObjectSubtype, expectedNode.ParentId), expectedProjectAdmission: admission);
            if (result.IsFailure) {
                State.Message = result.Errors.FirstOrDefault()?.Message ?? "Unable to save the canonical assignments.";
                State.Receipt = new(PartySavePhase.Rejected, [], null, State.Message);
                return;
            }
            var committed = result.Value ?? throw new InvalidOperationException("Assignment replacement returned no commit receipt.");
            State.Receipt = new(PartySavePhase.AssignmentsCommitted, committed.AssignmentIds, null,
                "Canonical assignments committed. Node metadata is not yet confirmed.");
            logger.LogInformation("Assignments {AssignmentIds} committed for original project {ProjectId}, node {NodeId}, opening {OpeningId}.",
                string.Join(",", committed.AssignmentIds), admission.ProjectId, NodeId, State.OpeningId);
            if (!originalAuthority()) {
                State.RequiresObservation = true;
                State.Message = "Assignments committed under the original authority. Node metadata was not submitted after that authority retired.";
                return;
            }
            var updated = await presentation.SaveAsync(admission, expectedNode,
                State.Kind == PartyEditorKind.Participant ? selectedOptions.SingleOrDefault() : null,
                State.Kind == PartyEditorKind.Meeting ? string.Join(", ", selected.Select(id => selectedOptions.FirstOrDefault(option => option.PartyId == id)?.DisplayName
                    ?? assignments.First(item => item.PartyId == id).PartyDisplayName)) : null, clearParticipant: selected.Length == 0);
            State.Receipt = State.Receipt with { Phase = PartySavePhase.NodeCommitted, NodeId = updated.Id,
                Message = "Canonical assignments and node presentation committed. Read-back is pending." };
            expectedNode = updated;
            State.Title = updated.Title;
            if (IsCurrent) {
                await publish(updated);
                await ObserveSavedAsync();
            }
        } catch (Exception failure) {
            var accepted = State.Receipt?.Phase is PartySavePhase.AssignmentsCommitted or PartySavePhase.NodeCommitted;
            State.RequiresObservation = accepted || dispatched && !KnownRejection(failure);
            if (accepted) {
                State.Message = $"{State.Receipt!.Message} Reload to inspect the original outcome; no accepted write will be repeated.";
            } else {
                State.Message = State.RequiresObservation ? "The assignment result is unconfirmed. Observe the original node before another save."
                    : "The original node changed before Save. Reload its editor.";
                State.Receipt = new(State.RequiresObservation ? PartySavePhase.Unknown : PartySavePhase.Rejected, [], null, State.Message);
            }
            Log(failure, "save");
        } finally {
            State.IsBusy = false;
        }
    }

    public async Task RetryAsync() {
        if (!IsCurrent || State.IsBusy) {
            return;
        }
        if (State.IsUnavailable && State.Created is null && State.Receipt is null) {
            await LoadAsync();
            return;
        }
        State.IsBusy = true;
        try {
            if (State.Receipt is not null) {
                await ObserveSavedAsync();
            } else {
                await ObserveCreatedAsync();
            }
        } catch (Exception failure) {
            State.Message = State.Created is { } accepted
                ? $"Directory party {accepted.Id:D} was created. Metadata is still unavailable; no create was repeated."
                : "Original relationship observation is still unavailable; no write was repeated.";
            Log(failure, "observe");
        } finally {
            State.IsBusy = false;
        }
    }

    private async Task ObserveCreatedAsync() {
        if (!IsCurrent || State.Created is not { } accepted) {
            return;
        }
        var generation = ++readGeneration;
        var loaded = await parties.ListPartyOptionsAsync(admission.ProjectId);
        if (!IsCurrent || generation != readGeneration) {
            return;
        }
        options = loaded;
        State.Draft.Participant = accepted.Id;
        State.Draft.KeepLocal = false;
        ProjectChoices();
        State.Message = $"Directory party {accepted.Id:D} created. Save participant sync to assign it. {accepted.Warning}".TrimEnd();
    }

    private async Task ObserveSavedAsync() {
        var generation = ++readGeneration;
        var loaded = NodeAssignments(await parties.ListAssignmentsDetailedAsync(admission.ProjectId));
        var structure = await workbench.GetStructureAsync(admission.ProjectId);
        if (!IsCurrent || generation != readGeneration) {
            return;
        }
        if (structure.ExpectedProjectAdmission != admission) {
            throw new ProjectWriteAdmissionRejectedException(admission);
        }
        var observed = structure.Nodes.FirstOrDefault(item => item.Id == NodeId);
        if (State.Receipt is not { Phase: PartySavePhase.NodeCommitted or PartySavePhase.Observed } receipt ||
            !loaded.Select(item => item.Id).ToHashSet().SetEquals(receipt.AssignmentIds) || observed is null ||
            observed.RecordId != expectedNode.RecordId || observed.MetadataJson != expectedNode.MetadataJson) {
            State.RequiresObservation = true;
            State.Message = "Original relationships were observed. A partial or competing outcome requires reopening the editor; no write was repeated.";
            return;
        }
        assignments = loaded;
        State.RequiresObservation = false;
        State.Receipt = receipt with { Phase = PartySavePhase.Observed, Message = "Assignments and node presentation saved and observed." };
        State.Message = State.Kind == PartyEditorKind.Meeting ? "Meeting parties saved."
            : assignments.Count == 0 ? "Participant kept project-local only." : "Participant linked to the directory.";
    }

    private IReadOnlyList<ProjectPartyAssignmentUpsertRequest> DesiredAssignments(IReadOnlyList<Guid> selected, IReadOnlyList<ProjectPartyOption> selectedOptions) {
        var desired = assignments.Where(row => selected.Contains(row.PartyId)).Select(row => new ProjectPartyAssignmentUpsertRequest {
            AssignmentId = row.Id, ProjectId = admission.ProjectId, ExpectedProjectAdmission = admission, NodeKey = NodeId,
            PartyId = row.PartyId, PartyAffiliationId = row.PartyAffiliationId ?? row.Affiliation?.AffiliationId, Role = row.Role,
            IsPrimary = row.IsPrimary, AllocationPercent = row.AllocationPercent, Source = row.Source, Notes = row.Notes,
            StartsOn = row.StartsAtUtc.HasValue ? DateOnly.FromDateTime(row.StartsAtUtc.Value.UtcDateTime) : null,
            EndsOn = row.EndsAtUtc.HasValue ? DateOnly.FromDateTime(row.EndsAtUtc.Value.UtcDateTime) : null
        }).ToList();
        foreach (var id in selected.Where(id => desired.All(row => row.PartyId != id))) {
            var option = selectedOptions.Single(item => item.PartyId == id);
            desired.Add(new() { ProjectId = admission.ProjectId, ExpectedProjectAdmission = admission, NodeKey = NodeId,
                PartyId = id, PartyAffiliationId = option.Affiliation?.AffiliationId,
                Role = semantics.PreferredRole ?? throw new InvalidOperationException("The node has no participation role."),
                IsPrimary = State.Kind == PartyEditorKind.Participant, Source = "project-structure" });
        }
        return desired;
    }

    private IReadOnlyList<ProjectPartyAssignmentDetail> NodeAssignments(IReadOnlyList<ProjectPartyAssignmentDetail> rows)
        => rows.Where(row => row.NodeKey == NodeId && semantics.ReplacementRoles.Contains(row.Role)).ToArray();

    private void ProjectChoices() {
        State.Choices = options.Select(option => new PartyChoice(option.PartyId, option.DisplayName, option.PartyTypeLabel,
            option.PartyType switch { ProjectPartyType.Organization => "business", ProjectPartyType.OrganizationUnit => "account_tree", ProjectPartyType.AiAgent => "smart_toy", _ => "person" },
            option.PartyType is ProjectPartyType.Person or ProjectPartyType.AiAgent,
            option.IsSensitive ? "" : !string.IsNullOrWhiteSpace(option.PrimaryEmail) ? option.PrimaryEmail : option.PrimaryPhone,
            option.IsSensitive || string.IsNullOrWhiteSpace(option.PrimaryEmail) ? "" : option.PrimaryPhone, option.IsSensitive))
            .Concat(assignments.Where(row => options.All(option => option.PartyId != row.PartyId)).DistinctBy(row => row.PartyId)
                .Select(row => new PartyChoice(row.PartyId, row.PartyDisplayName, row.PartyTypeLabel, "link_off", false, IsMissing: true)))
            .ToArray();
    }

    private void Log(Exception failure, string phase)
        => logger.LogWarning("Party editor {Phase} for original project {ProjectId}, node {NodeId}, opening {OpeningId} ended with {FailureType}; accepted PartyId {PartyId}.",
            phase, admission.ProjectId, NodeId, State.OpeningId, failure.GetType().Name, State.Created?.Id);

    private static bool KnownRejection(Exception failure)
        => failure is ArgumentException or InvalidDataException or ProjectWriteAdmissionRejectedException or ProjectStructureEditConflictException;
}
