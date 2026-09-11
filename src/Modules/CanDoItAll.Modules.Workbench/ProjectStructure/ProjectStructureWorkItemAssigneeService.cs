using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureTaskAssigneeMutationSnapshot(
    ProjectStructureNode Task,
    IReadOnlyList<ProjectPartyAssignmentDetail> DirectAssignments);

public sealed class ProjectStructureWorkItemAssigneeService(
    IProjectPartyIntegrationBridge partyIntegrationBridge,
    ProjectWorkbenchService projectWorkbenchService,
    IProjectWorkAssignmentCommands workAssignments,
    IDbContextFactory<WorkbenchDbContext> factory,
    ProjectStructureMutationScopeFactory mutationScopes,
    CoordinatedDatabaseTransaction transactions)
{
    private static readonly IReadOnlyList<ProjectPartyAssignmentRole> WorkItemAssignmentRoles =
        [ProjectPartyAssignmentRole.WorkItemAssignee];

    public async Task<IReadOnlyList<ProjectStructureTaskResourceOption>> ListOptionsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        EnsureProjectId(projectId);

        return (await partyIntegrationBridge.ListPartyOptionsAsync(projectId, cancellationToken))
            .Where(IsAssignableParty)
            .Select(MapPartyOption)
            .OrderBy(option => option.Kind)
            .ThenBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.ResourceId)
            .ToList();
    }

    public async Task<ProjectStructureTaskAssigneeMutationSnapshot> ReadAsync(
        Guid projectId,
        string taskNodeId,
        CancellationToken cancellationToken = default,
        ProjectWriteAdmission? expectedProjectAdmission = null)
    {
        EnsureProjectId(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);

        var assignments = await ListDirectAssignmentsAsync(
            projectId,
            taskNodeId,
            cancellationToken, expectedProjectAdmission);
        var task = await GetCanonicalWorkItemAsync(
            projectId,
            taskNodeId,
            cancellationToken, expectedProjectAdmission);
        return new ProjectStructureTaskAssigneeMutationSnapshot(
            task,
            assignments);
    }

    public async Task ReplaceAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection? selection,
        string source,
        CancellationToken cancellationToken = default,
        ProjectStructureAgentContext? mutationOwner = null)
    {
        await ReplaceCoreAsync(
            projectId,
            taskNodeId,
            selection,
            source,
            expectedAssignments: null,
            expectedDirectAssignmentRevision: null,
            cancellationToken, mutationOwner);
    }

    public async Task ReplaceIfUnchangedAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection? selection,
        string source,
        IReadOnlyList<ProjectPartyAssignmentDetail> expectedAssignments,
        long expectedDirectAssignmentRevision,
        CancellationToken cancellationToken = default,
        ProjectStructureAgentContext? mutationOwner = null)
    {
        ArgumentNullException.ThrowIfNull(expectedAssignments);
        await ReplaceCoreAsync(
            projectId,
            taskNodeId,
            selection,
            source,
            expectedAssignments,
            new ProjectWorkItemDirectAssignmentRevision(
                expectedDirectAssignmentRevision),
            cancellationToken, mutationOwner);
    }

    public Task<ProjectStructureTaskAssigneeMutationSnapshot>
        ReplaceIfUnchangedAndReadAsync(
            Guid projectId,
            string taskNodeId,
            ProjectStructureTaskResourceSelection? selection,
            string source,
            IReadOnlyList<ProjectPartyAssignmentDetail> expectedAssignments,
            long expectedDirectAssignmentRevision,
            CancellationToken cancellationToken = default,
        ProjectStructureAgentContext? mutationOwner = null)
    {
        ArgumentNullException.ThrowIfNull(expectedAssignments);
        return ReplaceCoreAsync(
            projectId,
            taskNodeId,
            selection,
            source,
            expectedAssignments,
            new ProjectWorkItemDirectAssignmentRevision(
                expectedDirectAssignmentRevision),
            cancellationToken, mutationOwner);
    }

    public async Task<ProjectStructureTaskAssigneeMutationSnapshot>
        RestoreIfUnchangedAndReadAsync(
            Guid projectId,
            string taskNodeId,
            ProjectPartyAssignmentDetail? previousAssignment,
            IReadOnlyList<ProjectPartyAssignmentDetail> expectedAssignments,
            long expectedDirectAssignmentRevision,
            CancellationToken cancellationToken = default,
        ProjectStructureAgentContext? mutationOwner = null)
    {
        var expected = ProjectAssignmentAdmission.Require(projectId, mutationOwner?.ExpectedProjectAdmission);
        if (previousAssignment is not null && previousAssignment.ProjectLifetimeId != expected.LifetimeId) {
            throw new InvalidOperationException("The previous assignment belongs to a different captured project lifetime.");
        }
        EnsureProjectId(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);
        ArgumentNullException.ThrowIfNull(expectedAssignments);
        expectedAssignments = expectedAssignments.ToArray();
        await GetCanonicalWorkItemAsync(
            projectId,
            taskNodeId,
            cancellationToken, expected);

        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments =
            previousAssignment is null
                ? []
                :
                [
                    new ProjectPartyAssignmentUpsertRequest
                    {
                        AssignmentId = previousAssignment.Id,
                        ProjectId = projectId,
                        ExpectedProjectAdmission = expected,
                        PartyId = previousAssignment.PartyId,
                        PartyAffiliationId =
                            previousAssignment.PartyAffiliationId ??
                            previousAssignment.Affiliation?.AffiliationId,
                        Role = previousAssignment.Role,
                        NodeKey = taskNodeId,
                        IsPrimary = previousAssignment.IsPrimary,
                        AllocationPercent =
                            previousAssignment.AllocationPercent,
                        StartsOn = ToDateOnly(
                            previousAssignment.StartsAtUtc),
                        EndsOn = ToDateOnly(
                            previousAssignment.EndsAtUtc),
                        Source = previousAssignment.Source,
                        Notes = previousAssignment.Notes
                    }
                ];
        var assignmentResult =
            await ReplaceOwnedAsync(
                projectId,
                new ProjectNodeReference(taskNodeId),
                desiredAssignments,
                expectedAssignments
                    .Select(ProjectPartyAssignmentConcurrencySnapshot.From)
                    .ToArray(),
                new ProjectWorkItemDirectAssignmentRevision(
                    expectedDirectAssignmentRevision),
                cancellationToken, mutationOwner);
        if (assignmentResult.IsFailure)
        {
            throw BuildAssignmentException(assignmentResult.Errors);
        }

        var restoredAssignments = await ListDirectAssignmentsAsync(
            projectId,
            taskNodeId,
            cancellationToken, expected);
        if (!MatchesRestoredAssignment(
                restoredAssignments,
                previousAssignment))
        {
            throw new ProjectStructureAgentException(
                409,
                "TaskAssigneeConcurrentChange",
                "The task assignments changed while the previous assignee was being restored. Reload the project before making another change.");
        }

        var restoredTask = await GetCanonicalWorkItemAsync(
            projectId,
            taskNodeId,
            cancellationToken, expected);
        return new ProjectStructureTaskAssigneeMutationSnapshot(
            restoredTask,
            restoredAssignments);
    }

    private async Task<ProjectStructureTaskAssigneeMutationSnapshot> ReplaceCoreAsync(
        Guid projectId,
        string taskNodeId,
        ProjectStructureTaskResourceSelection? selection,
        string source,
        IReadOnlyList<ProjectPartyAssignmentDetail>? expectedAssignments,
        ProjectWorkItemDirectAssignmentRevision?
            expectedDirectAssignmentRevision,
        CancellationToken cancellationToken, ProjectStructureAgentContext? mutationOwner)
    {
        var expected = ProjectAssignmentAdmission.Require(projectId, mutationOwner?.ExpectedProjectAdmission);
        expectedAssignments = expectedAssignments?.ToArray();
        EnsureProjectId(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskNodeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        await GetCanonicalWorkItemAsync(projectId, taskNodeId, cancellationToken, expected);
        var party = selection is null
            ? null
            : await ResolvePartyAsync(projectId, selection, cancellationToken);
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments = party is null
            ? []
            :
            [
                new ProjectPartyAssignmentUpsertRequest
                {
                    ProjectId = projectId,
                    ExpectedProjectAdmission = expected,
                    PartyId = party.PartyId,
                    PartyAffiliationId =
                        party.Affiliation?.AffiliationId,
                    Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                    NodeKey = taskNodeId,
                    IsPrimary = true,
                    Source = source
                }
            ];

        var assignmentResult = expectedAssignments is null
            ? await ReplaceOwnedAsync(
                projectId,
                new ProjectNodeReference(taskNodeId),
                desiredAssignments,
                cancellationToken: cancellationToken, mutationOwner: mutationOwner)
            : await ReplaceOwnedAsync(
                projectId,
                new ProjectNodeReference(taskNodeId),
                desiredAssignments,
                expectedAssignments
                    .Select(ProjectPartyAssignmentConcurrencySnapshot.From)
                    .ToArray(),
                expectedDirectAssignmentRevision,
                cancellationToken, mutationOwner);
        if (assignmentResult.IsFailure)
        {
            throw BuildAssignmentException(assignmentResult.Errors);
        }

        var replacementAssignments = await ListDirectAssignmentsAsync(
            projectId,
            taskNodeId,
            cancellationToken, expected);
        if (!MatchesSelection(replacementAssignments, selection))
        {
            throw new ProjectStructureAgentException(
                409,
                "TaskAssigneeConcurrentChange",
                "The task assignments changed while the assignee was being replaced. Reload the project before making another change.");
        }

        var updatedTask = await GetCanonicalWorkItemAsync(
            projectId,
            taskNodeId,
            cancellationToken, expected);
        return new ProjectStructureTaskAssigneeMutationSnapshot(
            updatedTask,
            replacementAssignments);
    }

    private async Task<Result> ReplaceOwnedAsync(Guid projectId, ProjectNodeReference node,
        IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
        IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? expectedAssignments = null,
        ProjectWorkItemDirectAssignmentRevision? expectedRevision = null, CancellationToken cancellationToken = default,
        ProjectStructureAgentContext? mutationOwner = null) {
        var expected = ProjectAssignmentAdmission.Require(projectId, mutationOwner?.ExpectedProjectAdmission);
        var requests = ProjectAssignmentAdmission.Snapshot(projectId, desiredAssignments, expected);
        var ids = requests.Select(_ => Guid.NewGuid()).ToArray();
        var keys = ids.Concat(requests.Where(item => item.AssignmentId.HasValue).Select(item => item.AssignmentId!.Value))
            .Select(ProjectAssignmentMutationKeys.ForAssignment).Append(ProjectMutationScopeKeys.ForProject(projectId)).ToArray();
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var scope = await mutationScopes.BeginAsync(context, keys, cancellationToken, [expected], mutationOwner?.ProcessMutationAdmission, mutationOwner?.AgentMutationAdmission);
        using var entry = transactions.Enter(context);
        var result = await workAssignments.StageReplaceAsync(projectId, node, requests, ids, expectedAssignments,
            expectedRevision, cancellationToken, expected);
        if (result.IsSuccess) {
            await scope.CommitAsync(cancellationToken);
        }
        return result;
    }

    private async Task<ProjectPartyOption> ResolvePartyAsync(
        Guid projectId,
        ProjectStructureTaskResourceSelection selection,
        CancellationToken cancellationToken)
    {
        ValidateSelection(selection);
        var party = await partyIntegrationBridge.GetPartyOptionAsync(selection.ResourceId, cancellationToken);
        if (party is null)
        {
            throw new ProjectStructureAgentException(
                404,
                "TaskAssigneeNotFound",
                $"Party assignee '{selection.ResourceId:D}' is not available for project '{projectId:D}'.");
        }

        var expectedPartyType = selection.Kind switch
        {
            ProjectStructureTaskResourceKind.Person => ProjectPartyType.Person,
            ProjectStructureTaskResourceKind.Agent => ProjectPartyType.AiAgent,
            _ => throw new InvalidOperationException($"Resource kind '{selection.Kind}' is not a task assignee.")
        };
        if (party.PartyType != expectedPartyType)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskAssigneeTypeMismatch",
                $"Party '{selection.ResourceId:D}' is '{party.PartyType}', not '{expectedPartyType}'.");
        }

        return party;
    }

    private async Task<ProjectStructureNode> GetCanonicalWorkItemAsync(
        Guid projectId,
        string taskNodeId,
        CancellationToken cancellationToken,
        ProjectWriteAdmission? expectedProjectAdmission = null)
    {
        var surface = await projectWorkbenchService.GetStructureAsync(projectId, cancellationToken);
        if (expectedProjectAdmission is not null && surface.ExpectedProjectAdmission != expectedProjectAdmission) {
            throw new ProjectWriteAdmissionRejectedException(expectedProjectAdmission);
        }
        var task = surface.Nodes.FirstOrDefault(node => string.Equals(node.Id, taskNodeId, StringComparison.Ordinal));
        if (task is null)
        {
            throw new ProjectStructureAgentException(404, "WorkItemNotFound", $"Work item '{taskNodeId}' was not found.");
        }

        if (task.IsSystemManaged ||
            task.ObjectType != ProjectObjectType.WorkItem)
        {
            throw new ProjectStructureAgentException(
                400,
                "CanonicalWorkItemRequired",
                $"Node '{taskNodeId}' is not a canonical editable WorkItem node.");
        }

        return task;
    }

    private async Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListDirectAssignmentsAsync(
        Guid projectId,
        string taskNodeId,
        CancellationToken cancellationToken,
        ProjectWriteAdmission? expectedProjectAdmission = null)
        => (await partyIntegrationBridge.ListAssignmentsDetailedAsync(
                projectId,
                WorkItemAssignmentRoles,
                cancellationToken))
            .Where(assignment =>
                (expectedProjectAdmission is null || assignment.ProjectLifetimeId == expectedProjectAdmission.LifetimeId) &&
                string.Equals(assignment.NodeKey, taskNodeId, StringComparison.Ordinal))
            .ToArray();

    private static bool MatchesSelection(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        ProjectStructureTaskResourceSelection? selection)
    {
        if (selection is null)
        {
            return assignments.Count == 0;
        }

        var expectedPartyType = selection.Kind switch
        {
            ProjectStructureTaskResourceKind.Person => ProjectPartyType.Person,
            ProjectStructureTaskResourceKind.Agent => ProjectPartyType.AiAgent,
            _ => (ProjectPartyType?)null
        };
        return expectedPartyType.HasValue &&
            assignments.Count == 1 &&
            assignments[0].PartyId == selection.ResourceId &&
            assignments[0].PartyType == expectedPartyType.Value;
    }

    private static bool MatchesRestoredAssignment(
        IReadOnlyList<ProjectPartyAssignmentDetail> assignments,
        ProjectPartyAssignmentDetail? expected)
    {
        if (expected is null)
        {
            return assignments.Count == 0;
        }

        return assignments.Count == 1 &&
            assignments[0].Id == expected.Id &&
            assignments[0].ProjectId == expected.ProjectId &&
            assignments[0].ProjectLifetimeId == expected.ProjectLifetimeId &&
            assignments[0].PartyId == expected.PartyId &&
            assignments[0].PartyType == expected.PartyType &&
            assignments[0].Role == expected.Role &&
            string.Equals(
                assignments[0].NodeKey,
                expected.NodeKey,
                StringComparison.Ordinal) &&
            assignments[0].IsPrimary == expected.IsPrimary &&
            assignments[0].AllocationPercent ==
                expected.AllocationPercent &&
            ToDateOnly(assignments[0].StartsAtUtc) ==
                ToDateOnly(expected.StartsAtUtc) &&
            ToDateOnly(assignments[0].EndsAtUtc) ==
                ToDateOnly(expected.EndsAtUtc) &&
            string.Equals(
                assignments[0].Source,
                expected.Source,
                StringComparison.Ordinal) &&
            string.Equals(
                assignments[0].Notes,
                expected.Notes,
                StringComparison.Ordinal) &&
            (assignments[0].PartyAffiliationId ??
             assignments[0].Affiliation?.AffiliationId) ==
            (expected.PartyAffiliationId ??
             expected.Affiliation?.AffiliationId);
    }

    private static DateOnly? ToDateOnly(DateTimeOffset? value)
        => value.HasValue
            ? DateOnly.FromDateTime(value.Value.UtcDateTime)
            : null;

    private static bool IsAssignableParty(ProjectPartyOption option)
        => option.PartyType is ProjectPartyType.Person or ProjectPartyType.AiAgent;

    private static ProjectStructureTaskResourceOption MapPartyOption(ProjectPartyOption option)
    {
        var kind = option.PartyType == ProjectPartyType.AiAgent
            ? ProjectStructureTaskResourceKind.Agent
            : ProjectStructureTaskResourceKind.Person;
        var description = option.IsSensitive
            ? string.Empty
            : !string.IsNullOrWhiteSpace(
                option.Affiliation?.PrimaryDisplayText)
                ? option.Affiliation.PrimaryDisplayText
                : string.IsNullOrWhiteSpace(option.PrimaryEmail)
                    ? option.PrimaryPhone
                    : option.PrimaryEmail;
        return new ProjectStructureTaskResourceOption(
            kind,
            option.PartyId,
            VersionId: null,
            option.DisplayName,
            option.Affiliation?.AffiliationLabel ??
                option.PartyTypeLabel,
            description,
            IsFavorite: false,
            option.IsSensitive);
    }

    private static void ValidateSelection(ProjectStructureTaskResourceSelection selection)
    {
        if (selection.Kind is not (ProjectStructureTaskResourceKind.Person or ProjectStructureTaskResourceKind.Agent))
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskAssigneeKindInvalid",
                $"Resource kind '{selection.Kind}' cannot be assigned directly to a task.");
        }

        if (selection.ResourceId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "TaskAssigneeRequired", "A task assignee id is required.");
        }

        if (selection.VersionId.HasValue)
        {
            throw new ProjectStructureAgentException(
                400,
                "TaskAssigneeVersionNotSupported",
                "Person and agent task assignees do not use a resource version.");
        }
    }

    private static ProjectStructureAgentException BuildAssignmentException(IReadOnlyList<Error> errors)
    {
        var message = string.Join(" ", errors.Select(error => error.Message));
        return errors.Any(error =>
                string.Equals(
                    error.Code,
                    ProjectPartyIntegrationErrorCodes.StaleAssignmentSnapshot,
                    StringComparison.Ordinal))
            ? new ProjectStructureAgentException(
                409,
                "TaskAssigneeConcurrentChange",
                message,
                errors)
            : new ProjectStructureAgentException(
                422,
                "TaskAssigneeAssignmentFailed",
                message,
                errors);
    }

    private static void EnsureProjectId(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ProjectStructureAgentException(400, "ProjectIdRequired", "A project id is required.");
        }
    }
}
