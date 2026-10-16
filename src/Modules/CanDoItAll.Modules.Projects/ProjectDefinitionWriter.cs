using System.Runtime.ExceptionServices;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Modules.Projects;

internal sealed record ProjectDefinitionCreationContext(Guid ProjectId, Guid? ParentProjectId,
    ProjectCreationReservation? Reservation, ProjectMutationAuthorization? Authorization);

internal sealed record CreatedProjectDefinition(ProjectEditorAcknowledgement Editor, ProjectCreationReceipt Receipt);

public sealed class ProjectDefinitionWriter(
    IDbContextFactory<ProjectsDbContext> factory,
    IClock clock,
    IActivityStream activity,
    ISearchIndexService search,
    CoordinatedDatabaseTransaction transactions,
    ProjectWriteAdmissionService admissions,
    ILogger<ProjectDefinitionWriter> logger) {
    public async Task<Result<ProjectEditorAcknowledgement>> ReplaceAsync(ReplaceProjectDefinitionCommand command,
        CancellationToken cancellationToken = default, ProjectMutationAuthorization? authorization = null) {
        ArgumentNullException.ThrowIfNull(command);
        var prepared = Prepare(command.Expected, command.Scalars, command.Phases, command.Options,
            command.AssignExternalIdentity, creation: null, authorization);
        var result = prepared.IsSuccess
            ? await ExecuteAsync(prepared.Value!, cancellationToken)
            : Result<DefinitionOutcome>.Failure(prepared.Errors);
        return result.IsSuccess ? Result<ProjectEditorAcknowledgement>.Success(ToEditor(result.Value!))
            : Result<ProjectEditorAcknowledgement>.Failure(result.Errors);
    }

    public async Task<Result<ProjectDefinitionScalarsAcknowledgement>> UpdateScalarsAsync(UpdateProjectDefinitionScalarsCommand command,
        CancellationToken cancellationToken = default, ProjectMutationAuthorization? authorization = null) {
        ArgumentNullException.ThrowIfNull(command);
        var prepared = Prepare(command.Expected, command.Scalars, null, null, null, creation: null, authorization);
        var result = prepared.IsSuccess
            ? await ExecuteAsync(prepared.Value!, cancellationToken)
            : Result<DefinitionOutcome>.Failure(prepared.Errors);
        return result.IsSuccess ? Result<ProjectDefinitionScalarsAcknowledgement>.Success(new(
            result.Value!.Definition, result.Value.Scalars, result.Value.ExternalIdentity))
            : Result<ProjectDefinitionScalarsAcknowledgement>.Failure(result.Errors);
    }

    internal async Task<Result<CreatedProjectDefinition>> CreateAsync(CreateProjectDefinitionCommand command,
        ProjectDefinitionCreationContext creation, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(creation);
        if (creation.ProjectId == Guid.Empty || creation.ParentProjectId == Guid.Empty) {
            return Result<CreatedProjectDefinition>.Failure(Error.Validation("Project creation requires nonempty target identities."));
        }
        var prepared = Prepare(null, command.Scalars, command.Phases, command.Options, command.ExternalIdentity,
            creation, creation.Authorization);
        var result = prepared.IsSuccess
            ? await ExecuteAsync(prepared.Value!, cancellationToken)
            : Result<DefinitionOutcome>.Failure(prepared.Errors);
        return result.IsSuccess ? Result<CreatedProjectDefinition>.Success(new(ToEditor(result.Value!),
            result.Value!.CreationReceipt ?? throw new InvalidOperationException("Project creation returned no compensation receipt.")))
            : Result<CreatedProjectDefinition>.Failure(result.Errors);
    }

    private Result<DefinitionWrite> Prepare(ProjectDefinitionExpectation? expected, ProjectDefinitionScalars scalars,
        IReadOnlyList<ProjectDefinitionPhaseInput>? phases, IReadOnlyList<ProjectDefinitionOptionInput>? options,
        ProjectDefinitionExternalIdentity? externalIdentity, ProjectDefinitionCreationContext? creation,
        ProjectMutationAuthorization? authorization) {
        if (creation is null && (expected is null || expected.Project.DatabaseProfileId != admissions.DatabaseProfileId ||
                expected.Revision == Guid.Empty)) {
            return Result<DefinitionWrite>.Failure(Error.Validation("The original definition expectation does not match this database.",
                ProjectErrorCodes.DefinitionExpectationInvalid));
        }
        var normalizedScalars = ProjectDefinitionRules.Normalize(scalars);
        if (normalizedScalars.IsFailure) {
            return Result<DefinitionWrite>.Failure(normalizedScalars.Errors);
        }
        ProjectDefinitionRows? rows = null;
        if (phases is not null && options is not null) {
            var normalizedRows = ProjectDefinitionRules.Normalize(phases, options, creation is not null);
            if (normalizedRows.IsFailure) {
                return Result<DefinitionWrite>.Failure(normalizedRows.Errors);
            }
            rows = normalizedRows.Value!;
        }
        ProjectExternalIdentity? identity = null;
        if (externalIdentity is not null) {
            var normalizedIdentity = ProjectExternalIdentity.Normalize(externalIdentity.Namespace, externalIdentity.Key);
            if (normalizedIdentity.IsFailure) {
                return Result<DefinitionWrite>.Failure(normalizedIdentity.Errors);
            }
            identity = normalizedIdentity.Value!;
        }
        return Result<DefinitionWrite>.Success(new(expected, normalizedScalars.Value!, rows, identity, creation, authorization));
    }

    private async Task<Result<DefinitionOutcome>> ExecuteAsync(DefinitionWrite write, CancellationToken cancellationToken) {
        try {
            return await ExecuteCoreAsync(write, cancellationToken);
        } catch (ProjectWriteAdmissionRejectedException exception) when (write.Expected is not null && exception.Admission == write.Expected.Project) {
            return await ClassifyConflictAsync(write.Expected, cancellationToken);
        } catch (DbUpdateConcurrencyException) {
            return await ClassifyConflictAsync(write.Expected, cancellationToken);
        } catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure) {
            return await ClassifyConflictAsync(write.Expected, cancellationToken);
        } catch (DbUpdateException exception) when (exception.InnerException is PostgresException {
                SqlState: PostgresErrorCodes.SerializationFailure }) {
            return await ClassifyConflictAsync(write.Expected, cancellationToken);
        } catch (DbUpdateException exception) when (write.ExternalIdentity is not null &&
                DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception) &&
                DbUpdateExceptionClassifier.GetConstraintName(exception) == ProjectExternalIdentity.UniqueIndexName) {
            return Result<DefinitionOutcome>.Failure(ExternalIdentityConflict());
        }
    }

    private async Task<Result<DefinitionOutcome>> ExecuteCoreAsync(DefinitionWrite write, CancellationToken cancellationToken) {
        var targetId = write.Expected?.Project.ProjectId ?? write.Creation!.ProjectId;
        var parentId = write.Creation?.ParentProjectId;
        var keys = new List<string> { ProjectMutationScopeKeys.ForProject(targetId) };
        if (parentId is { } parent) {
            keys.Add(ProjectMutationScopeKeys.ForProject(parent));
            keys.Add(ProjectMutationScopeKeys.Hierarchy);
        }
        if (write.ExternalIdentity is { } externalIdentity) {
            keys.Add(externalIdentity.MutationScopeKey);
        }
        var context = await factory.CreateDbContextAsync(cancellationToken);
        ProjectSourceMutationScope? scope = null;
        Exception? primaryFailure = null;
        DefinitionOutcome? accepted = null;
        SearchDocumentInput? projection = null;
        string? parentName = null;
        try {
            scope = await ProjectSourceMutationScope.BeginDefinitionAsync(context, keys,
                write.Expected is not null ? ProjectMutationPurpose.ExistingWrite : parentId.HasValue ? ProjectMutationPurpose.CreateChild : ProjectMutationPurpose.CreateRoot,
                targetId, write.Expected is not null ? [targetId] : parentId.HasValue ? [parentId.Value] : [],
                write.Authorization, admissions, transactions, cancellationToken, write.Creation?.Reservation,
                write.Expected is null ? null : [write.Expected.Project]);
            var entity = await context.Set<Project>().SingleOrDefaultAsync(project => project.Id == targetId, cancellationToken);
            if (write.Expected is { } expected) {
                if (entity is null) {
                    return Result<DefinitionOutcome>.Failure(NotFound());
                }
                if (entity.LifetimeId != expected.Project.LifetimeId) {
                    return Result<DefinitionOutcome>.Failure(LifetimeChanged());
                }
                if (entity.ConcurrencyToken != expected.Revision) {
                    return Result<DefinitionOutcome>.Failure(Conflict());
                }
                context.Entry(entity).Property(project => project.ConcurrencyToken).OriginalValue = expected.Revision;
            } else if (entity is not null) {
                return Result<DefinitionOutcome>.Failure(Error.Failure("The reserved project id is already in use.", ProjectErrorCodes.ReservedIdConflict));
            }
            if (parentId.HasValue) {
                var parentProject = await context.Set<Project>().SingleOrDefaultAsync(project => project.Id == parentId.Value, cancellationToken);
                if (parentProject is null) {
                    return Result<DefinitionOutcome>.Failure(Error.Validation("The selected parent project could not be found."));
                }
                parentName = parentProject.Name;
            }
            if (write.ExternalIdentity is { } identity) {
                if (entity?.ExternalNamespace is not null &&
                        (entity.ExternalNamespace != identity.Namespace || entity.ExternalKey != identity.Key)) {
                    return Result<DefinitionOutcome>.Failure(Error.Failure(
                        "A project's assigned external identity cannot be changed or cleared.", ProjectErrorCodes.ExternalIdentityImmutable));
                }
                if (await context.Set<Project>().AnyAsync(project => project.Id != targetId &&
                        project.ExternalNamespace == identity.Namespace && project.ExternalKey == identity.Key, cancellationToken)) {
                    return Result<DefinitionOutcome>.Failure(ExternalIdentityConflict());
                }
            }
            if (entity is null) {
                var reservation = write.Creation!.Reservation;
                if (reservation is null && await context.Set<ProjectCreationReservationRecord>().AnyAsync(record =>
                        record.ProjectId == targetId && record.State == ProjectCreationReservationState.Reserved && record.ImportedHistory == null, cancellationToken)) {
                    return Result<DefinitionOutcome>.Failure(Error.Failure("The project id belongs to a pending creation reservation.", ProjectErrorCodes.ReservedIdConflict));
                }
                if (reservation is not null && !await admissions.TryConsumeCreationAsync(context, reservation, parentId, cancellationToken)) {
                    return Result<DefinitionOutcome>.Failure(Error.Failure(
                        "The exact project creation reservation is closed or its parent lifetime has changed.", ProjectErrorCodes.ReservationClosed));
                }
                entity = new() { Id = targetId, CreatedAtUtc = clock.GetUtcNow() };
                if (reservation is not null) {
                    entity.BindReservedLifetime(reservation.LifetimeId);
                }
                context.Add(entity);
                if (parentId.HasValue) {
                    context.Add(new ProjectHierarchyLink { ParentProjectId = parentId.Value, ChildProjectId = targetId, CreatedAtUtc = clock.GetUtcNow() });
                }
            }
            if (write.ExternalIdentity is { } assigned) {
                entity.ExternalNamespace = assigned.Namespace;
                entity.ExternalKey = assigned.Key;
            }
            ApplyScalars(entity, write.Scalars);
            entity.UpdatedAtUtc = clock.GetUtcNow();
            entity.ConcurrencyToken = Guid.NewGuid();
            IReadOnlyList<ProjectPhaseAcknowledgement>? phases = null;
            IReadOnlyList<ProjectOptionAcknowledgement>? options = null;
            if (write.Rows is { } replacement) {
                var replaced = await ReplaceChildrenAsync(context, targetId, replacement, cancellationToken);
                if (replaced.IsFailure) {
                    return Result<DefinitionOutcome>.Failure(replaced.Errors);
                }
                (phases, options) = replaced.Value!;
            }
            await context.SaveChangesAsync(cancellationToken);
            var admission = admissions.Capture(entity);
            var receipt = write.Creation is null ? null : new ProjectCreationReceipt(admission, write.Creation.Reservation,
                await ProjectCreationFingerprint.ReadAsync(context, admission, cancellationToken), write.Authorization);
            accepted = new(new(admission, entity.ConcurrencyToken), write.Scalars,
                entity.ExternalNamespace is null ? null : new(entity.ExternalNamespace, entity.ExternalKey!), phases, options, receipt);
            var projectionOptions = options is null
                ? await context.Set<ProjectOptionSelection>().AsNoTracking().Where(option => option.ProjectId == targetId)
                    .OrderBy(option => option.Id).Select(option => new ProjectDefinitionOptionInput(option.Id, option.Category, option.OptionName, option.Notes))
                    .ToArrayAsync(cancellationToken)
                : options.Select(option => new ProjectDefinitionOptionInput(option.Id, option.Category, option.OptionName, option.Notes)).ToArray();
            projection = BuildProjection(targetId, write.Scalars, projectionOptions);
            try {
                await scope.CommitAsync(cancellationToken);
            } catch (Exception exception) when (scope.IsCommitted) {
                LogPostCommit(exception, targetId, "source-cleanup");
            }
        } catch (Exception exception) {
            primaryFailure = exception;
            throw;
        } finally {
            await DisposeResourcesAsync(context, scope, targetId, primaryFailure);
        }
        await RunPostCommitAsync(targetId, "search-index-upsert", () => ProjectAsync(accepted!.Definition, projection!, cancellationToken));
        await RunPostCommitAsync(targetId, "activity-project-save", () => activity.RecordAsync(new ActivityWriteRequest(
            "projects", write.Creation is null ? "update" : "create", write.Creation is null ? "Updated project" : "Created project",
            accepted!.Scalars.Name, ProjectId: targetId, ArtifactKind: "project", ArtifactId: targetId,
            Route: $"/projects?projectId={targetId}"), cancellationToken));
        if (parentId.HasValue) {
            await RunPostCommitAsync(targetId, "activity-subproject-attach", () => activity.RecordAsync(new ActivityWriteRequest(
                "projects", "attach-subproject", "Created subproject", $"{accepted!.Scalars.Name} is now under {parentName}.",
                ProjectId: targetId, ArtifactKind: "project", ArtifactId: targetId, Route: $"/projects?projectId={targetId}"), cancellationToken));
        }
        return Result<DefinitionOutcome>.Success(accepted!);
    }

    private static async Task<Result<(IReadOnlyList<ProjectPhaseAcknowledgement> Phases, IReadOnlyList<ProjectOptionAcknowledgement> Options)>>
        ReplaceChildrenAsync(ProjectsDbContext context, Guid projectId, ProjectDefinitionRows rows, CancellationToken cancellationToken) {
        var existingPhases = await context.Set<ProjectPhase>().Where(phase => phase.ProjectId == projectId).ToDictionaryAsync(phase => phase.Id, cancellationToken);
        var existingOptions = await context.Set<ProjectOptionSelection>().Where(option => option.ProjectId == projectId).ToDictionaryAsync(option => option.Id, cancellationToken);
        if (rows.Phases.Any(phase => phase.Id.HasValue && !existingPhases.ContainsKey(phase.Id.Value)) ||
                rows.Options.Any(option => option.Id.HasValue && !existingOptions.ContainsKey(option.Id.Value))) {
            return Result<(IReadOnlyList<ProjectPhaseAcknowledgement>, IReadOnlyList<ProjectOptionAcknowledgement>)>.Failure(ProjectDefinitionRules.InvalidChild());
        }
        var phases = new List<ProjectPhaseAcknowledgement>(rows.Phases.Count);
        for (var index = 0; index < rows.Phases.Count; index++) {
            var input = rows.Phases[index];
            var phase = input.Id.HasValue ? existingPhases[input.Id.Value] : new ProjectPhase { ProjectId = projectId };
            if (input.Id.HasValue) {
                existingPhases.Remove(input.Id.Value);
            } else {
                context.Add(phase);
            }
            phase.Name = input.Name;
            phase.Goal = input.Goal;
            phase.Status = input.Status;
            phase.OrderIndex = index;
            phase.StartDateUtc = input.StartDateUtc;
            phase.EndDateUtc = input.EndDateUtc;
            phases.Add(new(index, phase.Id, phase.Name, phase.Goal, phase.Status, phase.StartDateUtc, phase.EndDateUtc));
        }
        context.RemoveRange(existingPhases.Values);
        var options = new List<ProjectOptionAcknowledgement>(rows.Options.Count);
        for (var index = 0; index < rows.Options.Count; index++) {
            var input = rows.Options[index];
            if (ProjectDefinitionRules.IsAbsent(input)) {
                continue;
            }
            var option = input.Id.HasValue ? existingOptions[input.Id.Value] : new ProjectOptionSelection { ProjectId = projectId };
            if (input.Id.HasValue) {
                existingOptions.Remove(input.Id.Value);
            } else {
                context.Add(option);
            }
            option.Category = input.Category;
            option.OptionName = input.OptionName;
            option.Notes = input.Notes;
            options.Add(new(index, option.Id, option.Category, option.OptionName, option.Notes));
        }
        context.RemoveRange(existingOptions.Values);
        return Result<(IReadOnlyList<ProjectPhaseAcknowledgement>, IReadOnlyList<ProjectOptionAcknowledgement>)>.Success((phases.AsReadOnly(), options.AsReadOnly()));
    }

    private async Task<Result<DefinitionOutcome>> ClassifyConflictAsync(ProjectDefinitionExpectation? expected, CancellationToken cancellationToken) {
        if (expected is null) {
            return Result<DefinitionOutcome>.Failure(TransactionConflict());
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var current = await context.Set<Project>().AsNoTracking().Where(project => project.Id == expected.Project.ProjectId)
            .Select(project => new {
                project.LifetimeId,
                project.ConcurrencyToken,
                Retired = context.Set<ProjectRetirementRecord>().Any(retirement => retirement.LifetimeId == project.LifetimeId)
            }).SingleOrDefaultAsync(cancellationToken);
        return Result<DefinitionOutcome>.Failure(current is null ? NotFound()
            : current.LifetimeId != expected.Project.LifetimeId || current.Retired ? LifetimeChanged()
            : current.ConcurrencyToken != expected.Revision ? Conflict() : TransactionConflict());
    }

    private async Task ProjectAsync(ProjectDefinitionExpectation definition, SearchDocumentInput projection, CancellationToken cancellationToken) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        await using var scope = await SerializableMutationScope.BeginAsync(context, ProjectMutationScopeKeys.ForProject(definition.Project.ProjectId), cancellationToken);
        await admissions.RequireAsync(context, definition.Project, cancellationToken);
        var currentRevision = await context.Set<Project>().Where(project => project.Id == definition.Project.ProjectId)
            .Select(project => project.ConcurrencyToken).SingleAsync(cancellationToken);
        if (currentRevision != definition.Revision) {
            return;
        }
        using (transactions.Enter(context)) {
            await search.UpsertForMutationAsync(projection, cancellationToken);
        }
        await scope.CommitAsync(cancellationToken);
    }

    private async Task DisposeResourcesAsync(ProjectsDbContext context, ProjectSourceMutationScope? scope, Guid projectId, Exception? primaryFailure) {
        var failures = new List<Exception>();
        if (scope is not null) {
            try {
                await scope.DisposeAsync();
            } catch (Exception exception) {
                failures.Add(exception);
            }
        }
        try {
            await context.DisposeAsync();
        } catch (Exception exception) {
            failures.Add(exception);
        }
        if (scope?.IsCommitted == true) {
            foreach (var failure in failures) {
                LogPostCommit(failure, projectId, "owner-cleanup");
            }
            return;
        }
        if (failures.Count == 0) {
            return;
        }
        if (primaryFailure is not null) {
            failures.Insert(0, primaryFailure);
        }
        if (failures.Count == 1) {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        throw new AggregateException("Project definition mutation and resource cleanup failed.", failures);
    }

    private async Task RunPostCommitAsync(Guid projectId, string action, Func<Task> operation) {
        try {
            await operation();
        } catch (Exception exception) {
            LogPostCommit(exception, projectId, action);
        }
    }

    private void LogPostCommit(Exception exception, Guid projectId, string action)
        => logger.LogError(exception, "Project {ProjectId} committed, but post-commit action {Action} failed.", projectId, action);

    private static void ApplyScalars(Project entity, ProjectDefinitionScalars scalars) {
        entity.Name = scalars.Name;
        entity.Slug = FileSafeSlugBuilder.Build(scalars.Name);
        entity.Description = scalars.Description;
        entity.Objective = scalars.Objective;
        entity.Status = scalars.Status;
        entity.CurrentPhase = scalars.CurrentPhase;
        entity.TargetDateUtc = scalars.TargetDateUtc;
    }

    private static SearchDocumentInput BuildProjection(Guid projectId, ProjectDefinitionScalars scalars, IReadOnlyList<ProjectDefinitionOptionInput> options)
        => new("project", projectId.ToString(), "Projects", scalars.Name, scalars.Description,
            $"{scalars.Objective}\nCurrent phase: {scalars.CurrentPhase}\nOptions: {string.Join(", ", options.Where(option => !string.IsNullOrWhiteSpace(option.OptionName)).Select(option => $"{option.Category}:{option.OptionName}"))}",
            $"/projects?projectId={projectId}", projectId);

    private static ProjectEditorAcknowledgement ToEditor(DefinitionOutcome accepted)
        => new(accepted.Definition, accepted.Scalars.Name, accepted.Scalars.Description, accepted.Scalars.Objective,
            accepted.Scalars.Status, accepted.Scalars.CurrentPhase, accepted.Scalars.TargetDateUtc,
            accepted.Phases ?? throw new InvalidOperationException("A replacement result requires accepted phases."),
            accepted.Options ?? throw new InvalidOperationException("A replacement result requires accepted options."), accepted.ExternalIdentity);

    private static Error Conflict() => Error.Failure("The project definition changed. Read its current definition and reconcile the original draft.", ProjectErrorCodes.DefinitionConflict);
    private static Error TransactionConflict() => Error.Failure("The definition transaction was rolled back because of concurrent database work. No automatic retry was performed.", ProjectErrorCodes.DefinitionTransactionConflict);
    private static Error NotFound() => Error.Failure("The project no longer exists.", ProjectErrorCodes.NotFound);
    private static Error LifetimeChanged() => Error.Failure("This project belongs to a different lifetime. Reload it before saving changes.", ProjectErrorCodes.LifetimeChanged);
    private static Error ExternalIdentityConflict() => Error.Failure("The external identity is already assigned. Read the current binding before another save.", ProjectErrorCodes.ExternalIdentityConflict);

    private sealed record DefinitionWrite(ProjectDefinitionExpectation? Expected, ProjectDefinitionScalars Scalars,
        ProjectDefinitionRows? Rows, ProjectExternalIdentity? ExternalIdentity, ProjectDefinitionCreationContext? Creation,
        ProjectMutationAuthorization? Authorization);

    private sealed record DefinitionOutcome(ProjectDefinitionExpectation Definition, ProjectDefinitionScalars Scalars,
        ProjectDefinitionExternalIdentity? ExternalIdentity, IReadOnlyList<ProjectPhaseAcknowledgement>? Phases,
        IReadOnlyList<ProjectOptionAcknowledgement>? Options, ProjectCreationReceipt? CreationReceipt);
}
