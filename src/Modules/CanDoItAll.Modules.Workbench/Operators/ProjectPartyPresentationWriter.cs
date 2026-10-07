using System.Text.Json.Nodes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectPartyPresentationWriter(IDbContextFactory<WorkbenchDbContext> contexts,
    ProjectStructureMutationScopeFactory mutations, IClock clock) {
    public async Task<ProjectStructureNode> SaveAsync(ProjectWriteAdmission admission, ProjectStructureNode expected,
        ProjectPartyOption? participant, string? meetingSummary, bool clearParticipant = false, CancellationToken cancellationToken = default) {
        if (expected.ObjectType is not (ProjectObjectType.Participant or ProjectObjectType.Meeting)) {
            throw new ArgumentException("Party presentation requires a participant or meeting.", nameof(expected));
        }
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        await using var scope = await mutations.BeginBindingWriteAsync(context,
            ProjectStructureSerializableMutationScope.ForProject(admission.ProjectId), cancellationToken, [admission]);
        var current = await context.Set<ProjectObjectRecord>().SingleOrDefaultAsync(row =>
            row.ProjectId == admission.ProjectId && row.NodeKey == expected.Id, cancellationToken)
            ?? throw new ProjectStructureEditConflictException();
        await ProjectNodeBindingStorage.LoadAsync(context, [current], cancellationToken);
        ProjectStructureNodeExpectations.EnsureContentCurrent(expected, current);
        if (participant is not null && current.Title != expected.Title) {
            throw new ProjectStructureEditConflictException();
        }
        var root = JsonNode.Parse(current.MetadataJson) as JsonObject
            ?? throw new InvalidDataException("Party metadata must be a JSON object.");
        if (current.ObjectType == ProjectObjectType.Participant) {
            var section = Section(root, ParticipantSection);
            if (participant is not null || clearParticipant) {
                section[LinkedPartyName] = participant?.DisplayName ?? string.Empty;
            }
            if (participant is not null) {
                section[Email] = participant.IsSensitive ? string.Empty : participant.PrimaryEmail;
                section[Phone] = participant.IsSensitive ? string.Empty : participant.PrimaryPhone;
                if (string.IsNullOrWhiteSpace(section[Organization]?.GetValue<string>())) {
                    section[Organization] = participant.IsSensitive ? participant.PartyTypeLabel : participant.Affiliation?.OrganizationName ?? participant.PartyTypeLabel;
                }
                current.Title = participant.DisplayName;
            }
        } else {
            Section(root, MeetingSection)[RelatedPartyNames] = meetingSummary ?? string.Empty;
        }
        current.MetadataJson = root.ToJsonString();
        current.UpdatedAtUtc = clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        await scope.CommitAsync(cancellationToken);
        return ProjectWorkbenchNodeMapper.MapStructureNode(current);
    }

    private static JsonObject Section(JsonObject root, string name) {
        if (root[name] is JsonObject section) {
            return section;
        }
        if (root[name] is not null) {
            throw new InvalidDataException($"The '{name}' metadata section must be a JSON object.");
        }
        var created = new JsonObject();
        root[name] = created;
        return created;
    }

    private const string ParticipantSection = "participant";
    private const string MeetingSection = "meeting";
    private const string LinkedPartyName = "linkedPartyName";
    private const string RelatedPartyNames = "relatedPartyNames";
    private const string Email = "email";
    private const string Phone = "phone";
    private const string Organization = "organization";
}
