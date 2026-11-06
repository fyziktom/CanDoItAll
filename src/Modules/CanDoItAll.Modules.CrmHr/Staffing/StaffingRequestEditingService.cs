using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr;

public sealed class StaffingRequestEditingService(IDbContextFactory<CrmHrDbContext> contexts,
    ProjectWriteAdmissionService admissions, CoordinatedDatabaseTransaction transactions,
    CrmHrChanges changes, ILogger<StaffingRequestEditingService> logger) : IStaffingRequestEditing {
    public async Task<StaffingRequestEditorModel?> ReadAsync(Guid id, CancellationToken cancellationToken = default) {
        await using var context = await contexts.CreateDbContextAsync(cancellationToken);
        var row = await context.Set<StaffingRequest>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (row is null) {
            return null;
        }
        var editor = StaffingRequestEditValues.Editor(row, admissions.DatabaseProfileId);
        if (row.ProjectId is { } projectId) {
            var current = await admissions.CaptureAsync(projectId, cancellationToken);
            if (current?.LifetimeId == row.ProjectLifetimeId) {
                editor.ExpectedProjectAdmission = current;
            }
        }
        return editor;
    }

    public async Task<Result<Guid>> SaveAsync(StaffingRequestEditorModel model, CancellationToken cancellationToken = default,
        Action<StaffingRequestSaveReceipt>? onCommitted = null) {
        ArgumentNullException.ThrowIfNull(model);
        var draft = model.Snapshot();
        var original = draft.Original;
        if (draft.Id.HasValue && original is null) {
            return Result<Guid>.Failure(Error.Validation("Read the staffing request before replacing it.", StaffingRequestEditingErrors.OriginalRequired));
        }
        if (original is not null && (draft.Id != original.Id || original.Id == Guid.Empty || original.SkillIds.IsDefault)) {
            return Result<Guid>.Failure(Error.Validation("The staffing identity differs from its shown original.", StaffingRequestEditingErrors.IdentityInvalid));
        }
        var expected = draft.ProjectId is { } projectId ? ProjectAssignmentAdmission.Require(projectId, draft.ExpectedProjectAdmission) : null;
        if (expected is null && draft.ExpectedProjectAdmission is not null) {
            throw new InvalidOperationException("A global staffing request cannot carry a project admission.");
        }
        if (Validate(draft) is { } validation) {
            return Result<Guid>.Failure(validation);
        }
        var id = draft.Id ?? Guid.NewGuid();
        var committed = false;
        try {
            await changes.DemandCommitAsync(cancellationToken);
            await using var context = await contexts.CreateDbContextAsync(cancellationToken);
            await using var mutation = await SerializableMutationScope.BeginAsync(context,
                Keys(id, draft.ProjectId, original?.ProjectId, draft.RequestedByPartyId, draft.DeliveryUnitPartyId), cancellationToken);
            using var entry = transactions.Enter(context);
            if (expected is not null) {
                await admissions.RequireForMutationAsync(expected, cancellationToken);
            }
            var row = draft.Id.HasValue
                ? await context.Set<StaffingRequest>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken) : null;
            if (original is not null && (row is null || !StaffingRequestEditValues.Same(StaffingRequestEditValues.Observe(row), original))) {
                return Result<Guid>.Failure(StaffingRequestEditValues.Conflict());
            }
            if (row?.ProjectId is { } sourceProject && (sourceProject != draft.ProjectId || row.ProjectLifetimeId != expected?.LifetimeId)) {
                var reference = draft.ExpectedSourceProjectReference;
                if (reference is null || reference.DatabaseProfileId != admissions.DatabaseProfileId || reference.ProjectId != sourceProject ||
                    reference.LifetimeId is null || reference.LifetimeId != row.ProjectLifetimeId) {
                    return Result<Guid>.Failure(Error.Validation("Changing the staffing project requires its displayed source lifetime.", StaffingRequestEditingErrors.ProjectReferenceInvalid));
                }
            }
            if (draft.RequestedByPartyId is { } requester && !await context.Set<Party>().AnyAsync(party => party.Id == requester, cancellationToken)) {
                return Result<Guid>.Failure(Error.Validation("The requester was not found.", StaffingRequestEditingErrors.RequesterMissing));
            }
            if (draft.DeliveryUnitPartyId is { } unit && !await context.Set<Party>().AnyAsync(party => party.Id == unit, cancellationToken)) {
                return Result<Guid>.Failure(Error.Validation("The delivery unit was not found.", StaffingRequestEditingErrors.DeliveryUnitMissing));
            }
            var skills = draft.SkillIds.Where(skill => skill != Guid.Empty).Distinct().Order().ToArray();
            if (skills.Length != await context.Set<SkillDefinition>().CountAsync(skill => skills.Contains(skill.Id), cancellationToken)) {
                return Result<Guid>.Failure(Error.Validation("One or more selected skills no longer exist.", StaffingRequestEditingErrors.SkillMissing));
            }
            if (row is null) {
                row = new() { Id = id };
                context.Add(row);
            }
            row.ProjectId = draft.ProjectId;
            row.ProjectLifetimeId = expected?.LifetimeId;
            row.RequestedByPartyId = draft.RequestedByPartyId;
            row.DeliveryUnitPartyId = draft.DeliveryUnitPartyId;
            row.Title = draft.Title.Trim();
            row.NeededRole = draft.NeededRole.Trim();
            row.NeededSkillsJson = JsonSerializer.Serialize(skills);
            row.StartDateUtc = StaffingRequestEditValues.Utc(draft.StartDate);
            row.EndDateUtc = StaffingRequestEditValues.Utc(draft.EndDate);
            row.AllocationPercent = draft.AllocationPercent;
            row.Status = draft.Status;
            row.Notes = draft.Notes.Trim();
            await changes.DemandCommitAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            await mutation.CommitAsync(cancellationToken);
            committed = true;
            var accepted = StaffingRequestEditValues.Editor(row, admissions.DatabaseProfileId);
            accepted.ExpectedProjectAdmission = expected;
            onCommitted?.Invoke(new(id, accepted));
            changes.PublishStaffing(id, row.ProjectId, original?.ProjectId);
            return Result<Guid>.Success(id);
        } catch (Exception exception) when (!committed && SerializableMutationScope.IsConflict(exception)) {
            logger.LogInformation("Staffing replacement conflicted for request {RequestId} in project {ProjectId}.", id, draft.ProjectId);
            return Result<Guid>.Failure(StaffingRequestEditValues.Conflict());
        }
    }

    public async Task<Result> DeleteAsync(Guid id, StaffingRequestObservation? original, CancellationToken cancellationToken = default,
        Action<StaffingRequestDeleteReceipt>? onCommitted = null) {
        if (original is null) {
            return Result.Failure(Error.Validation("The displayed staffing row is required before deletion.", StaffingRequestEditingErrors.OriginalRequired));
        }
        if (id == Guid.Empty || id != original.Id || original.SkillIds.IsDefault) {
            return Result.Failure(Error.Validation("The staffing delete identity differs from its original.", StaffingRequestEditingErrors.IdentityInvalid));
        }
        var committed = false;
        try {
            await changes.DemandCommitAsync(cancellationToken);
            await using var context = await contexts.CreateDbContextAsync(cancellationToken);
            await using var mutation = await SerializableMutationScope.BeginAsync(context,
                Keys(id, original.ProjectId, null, original.RequestedByPartyId, original.DeliveryUnitPartyId), cancellationToken);
            var row = await context.Set<StaffingRequest>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (row is null || !StaffingRequestEditValues.Same(StaffingRequestEditValues.Observe(row), original)) {
                return Result.Failure(StaffingRequestEditValues.Conflict());
            }
            context.Remove(row);
            await changes.DemandCommitAsync(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await changes.DemandCommitAsync(cancellationToken);
            await mutation.CommitAsync(cancellationToken);
            committed = true;
            onCommitted?.Invoke(new(id, original));
            changes.PublishStaffing(id, original.ProjectId, kind: CanDoItAll.Editing.EditingChangeKind.Deleted);
            return Result.Success();
        } catch (Exception exception) when (!committed && SerializableMutationScope.IsConflict(exception)) {
            logger.LogInformation("Staffing deletion conflicted for request {RequestId} in project {ProjectId}.", id, original.ProjectId);
            return Result.Failure(StaffingRequestEditValues.Conflict());
        }
    }

    private static string[] Keys(Guid id, Guid? project, Guid? source, Guid? requester, Guid? unit) =>
        new[] { project, source }.OfType<Guid>().Distinct().Select(ProjectMutationScopeKeys.ForProject)
            .Concat(new[] { requester, unit }.OfType<Guid>().Distinct().Select(OpportunityPartyReferences.ScopeKey))
            .Append(StaffingRequestEditValues.ScopeKey(id)).ToArray();

    private static Error? Validate(StaffingRequestEditorModel draft) => draft switch {
        _ when string.IsNullOrWhiteSpace(draft.Title) => Error.Validation("Staffing request title is required.", StaffingRequestEditingErrors.TitleRequired),
        _ when string.IsNullOrWhiteSpace(draft.NeededRole) => Error.Validation("Needed role is required.", StaffingRequestEditingErrors.RoleRequired),
        { AllocationPercent: <= 0m or > 100m } => Error.Validation("Allocation must stay between 0 and 100.", StaffingRequestEditingErrors.AllocationRange),
        _ when draft.StartDate.HasValue && draft.EndDate.HasValue && draft.EndDate.Value < draft.StartDate.Value => Error.Validation("End date must be on or after the start date.", StaffingRequestEditingErrors.DateRange),
        _ when !Enum.IsDefined(draft.Status) || draft.Notes is null => Error.Validation("A supported status and business notes are required."),
        _ => null
    };
}
