using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private const int DefinitionSlugRetryLimit = 3;

    public async Task<Result<Guid>> SaveAsync(
        ProcessDefinitionEditorModel model,
        CancellationToken cancellationToken = default)
    {
        return await SaveAsync(model, importMetadata: null, cancellationToken);
    }

    private async Task<Result<Guid>> SaveAsync(
        ProcessDefinitionEditorModel model,
        ProcessImportMetadata? importMetadata,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateDefinitionEditor(model);
        if (validationError is not null)
        {
            return Result<Guid>.Failure(validationError);
        }

        NormalizeDefinitionEditorForSave(model);
        validationError = ValidateDefinitionEditor(model);
        if (validationError is not null)
        {
            return Result<Guid>.Failure(validationError);
        }

        for (var slugRetryAttempt = 0; ; slugRetryAttempt++)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            ProcessDefinition definition;
            ProcessDefinitionVersion workingVersion;
            Guid outboxId;
            var isNew = false;
            try
            {
                definition = model.Id.HasValue
                    ? await dbContext.Set<ProcessDefinition>().SingleOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
                    : null;

                isNew = definition is null;
                if (definition is null)
                {
                    definition = new ProcessDefinition
                    {
                        CreatedAtUtc = clock.GetUtcNow()
                    };

                    await dbContext.Set<ProcessDefinition>().AddAsync(definition, cancellationToken);
                }
                else if (HasConcurrencyTokenMismatch(model.DefinitionConcurrencyToken, definition.ConcurrencyToken))
                {
                    return Result<Guid>.Failure(CreateDefinitionSaveConflictError());
                }

                definition.ProjectId = model.ProjectId;
                definition.Name = model.Name.Trim();
                definition.Slug = await BuildUniqueSlugAsync(dbContext, model.Name, model.ProjectId, definition.Id, cancellationToken);
                definition.Summary = model.Summary.Trim();
                definition.ValueStatement = model.ValueStatement.Trim();
                definition.CustomerName = model.CustomerName.Trim();
                definition.OwnerName = model.OwnerName.Trim();
                definition.InterfaceContractSummary = model.InterfaceContractSummary.Trim();
                definition.GovernanceNotes = model.GovernanceNotes.Trim();
                definition.Criticality = model.Criticality;
                definition.AutonomyLevel = model.AutonomyLevel;
                definition.Status = model.Status;
                definition.UpdatedAtUtc = clock.GetUtcNow();

                workingVersion = model.WorkingVersionId.HasValue
                    ? await dbContext.Set<ProcessDefinitionVersion>()
                        .SingleOrDefaultAsync(item => item.Id == model.WorkingVersionId.Value, cancellationToken)
                    : null;

                if (workingVersion is null)
                {
                    workingVersion = await GetWorkingVersionAsync(dbContext, definition.Id, cancellationToken);
                }

                if (workingVersion is not null && HasConcurrencyTokenMismatch(model.WorkingVersionConcurrencyToken, workingVersion.ConcurrencyToken))
                {
                    return Result<Guid>.Failure(CreateDefinitionSaveConflictError());
                }

                if (workingVersion is not null && workingVersion.Status == ProcessVersionStatus.Published)
                {
                    return Result<Guid>.Failure(Error.Validation("Published versions are immutable. Save into a draft version instead.", "processes.immutable-published-version"));
                }

                if (workingVersion is null)
                {
                    await EnsureNextVersionNumberAheadOfExistingVersionsAsync(dbContext, definition, cancellationToken);
                    workingVersion = new ProcessDefinitionVersion
                    {
                        ProcessDefinitionId = definition.Id,
                        VersionNumber = AllocateNextVersionNumber(definition),
                        Status = ProcessVersionStatus.Draft,
                        CreatedAtUtc = clock.GetUtcNow()
                    };

                    await dbContext.Set<ProcessDefinitionVersion>().AddAsync(workingVersion, cancellationToken);
                }

                workingVersion.ChangeSummary = model.ChangeSummary.Trim();
                workingVersion.GovernancePolicySummary = model.GovernancePolicySummary.Trim();
                workingVersion.ConstitutionRuleSummary = model.ConstitutionRuleSummary.Trim();
                workingVersion.OperatingModeSummary = model.OperatingModeSummary.Trim();
                workingVersion.SimulationReadinessSummary = model.SimulationReadinessSummary.Trim();
                if (importMetadata is not null)
                {
                    workingVersion.ImportedFrom = importMetadata.SourceFormat;
                    workingVersion.ImportWarnings = importMetadata.WarningSummary;
                }

                workingVersion.UpdatedAtUtc = clock.GetUtcNow();

                await SaveDefinitionChildrenAsync(dbContext, workingVersion.Id, model, cancellationToken);
                outboxId = await processOutboxService.EnqueueDefinitionSaveAsync(
                    dbContext,
                    definition,
                    workingVersion,
                    isNew,
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await processOutboxService.ProcessAsync(outboxId, cancellationToken);
                return Result<Guid>.Success(definition.Id);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<Guid>.Failure(CreateDefinitionSaveConflictError());
            }
            catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception) &&
                                                      IsDefinitionSlugConflict(exception) &&
                                                      slugRetryAttempt < DefinitionSlugRetryLimit - 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                continue;
            }
            catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<Guid>.Failure(CreateDefinitionSaveUniqueConflictError(exception));
            }
        }
    }

    private sealed record ProcessImportMetadata(
        string SourceFormat,
        string WarningSummary);

    private async Task<ProcessDefinitionVersion?> GetWorkingVersionAsync(
        AppDbContext dbContext,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<ProcessDefinitionVersion>()
            .SingleOrDefaultAsync(
                item => item.ProcessDefinitionId == definitionId &&
                item.Status == ProcessVersionStatus.Draft,
                cancellationToken);
    }

    private static async Task EnsureNextVersionNumberAheadOfExistingVersionsAsync(
        AppDbContext dbContext,
        ProcessDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(definition);

        var highestExistingVersionNumber = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == definition.Id)
            .Select(item => (int?)item.VersionNumber)
            .MaxAsync(cancellationToken);
        var nextAvailableVersionNumber = (highestExistingVersionNumber ?? 0) + 1;
        if (definition.NextVersionNumber < nextAvailableVersionNumber)
        {
            definition.NextVersionNumber = nextAvailableVersionNumber;
        }
    }

    private static int AllocateNextVersionNumber(ProcessDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var nextVersionNumber = Math.Max(1, definition.NextVersionNumber);
        definition.NextVersionNumber = nextVersionNumber + 1;
        return nextVersionNumber;
    }

    private static string BuildSlug(string input)
    {
        return FileSafeSlugBuilder.Build(input);
    }

    private static async Task<string> BuildUniqueSlugAsync(
        AppDbContext dbContext,
        string input,
        Guid? projectId,
        Guid currentDefinitionId,
        CancellationToken cancellationToken)
    {
        var baseSlug = BuildSlug(input);
        var scopeSuffix = projectId?.ToString("N")[..8];
        var alternateBaseSlug = projectId.HasValue
            ? $"{baseSlug}-{scopeSuffix}"
            : baseSlug;
        var candidate = baseSlug;

        if (await SlugExistsAsync(dbContext, candidate, currentDefinitionId, cancellationToken))
        {
            candidate = projectId.HasValue ? alternateBaseSlug : $"{baseSlug}-2";
        }

        var suffixBase = projectId.HasValue ? alternateBaseSlug : baseSlug;
        var suffix = projectId.HasValue ? 2 : 3;
        while (await SlugExistsAsync(dbContext, candidate, currentDefinitionId, cancellationToken))
        {
            candidate = $"{suffixBase}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static Task<bool> SlugExistsAsync(
        AppDbContext dbContext,
        string slug,
        Guid currentDefinitionId,
        CancellationToken cancellationToken)
    {
        return dbContext.Set<ProcessDefinition>()
            .AnyAsync(item => item.Id != currentDefinitionId && item.Slug == slug, cancellationToken);
    }

    private static string BuildKey(string input, string fallback)
    {
        var normalized = BuildSlug(input);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
