using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessDevelopmentSeedService
{
    private readonly ProcessesService processesService;
    private readonly ProcessTemplateProjectionService projectionService;
    private readonly ProcessTemplatePackLoader packLoader;
    private readonly IWorkspacePathResolver workspacePathResolver;

    public ProcessDevelopmentSeedService(
        ProcessesService processesService,
        ProcessTemplateProjectionService projectionService,
        ProcessTemplatePackLoader packLoader,
        IWorkspacePathResolver workspacePathResolver)
    {
        this.processesService = processesService;
        this.projectionService = projectionService;
        this.packLoader = packLoader;
        this.workspacePathResolver = workspacePathResolver;
    }

    public async Task<Result<ProcessSeedReport>> SeedBaselineAsync(
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        var pack = packLoader.Load();
        var seededDefinitionIds = new List<Guid>();
        var seededRunIds = new List<Guid>();
        Guid primaryDefinitionId = Guid.Empty;
        Guid secondaryDefinitionId = Guid.Empty;

        foreach (var scenario in GetBaselineScenarios(pack))
        {
            var result = await EnsureBaselineDefinitionAsync(
                pack,
                scenario,
                projectId,
                seededDefinitionIds,
                seededRunIds,
                cancellationToken);
            if (result.IsFailure)
            {
                return Result<ProcessSeedReport>.Failure(result.Errors.ToArray());
            }

            if (primaryDefinitionId == Guid.Empty)
            {
                primaryDefinitionId = result.Value;
            }
            else if (secondaryDefinitionId == Guid.Empty)
            {
                secondaryDefinitionId = result.Value;
            }
        }

        return Result<ProcessSeedReport>.Success(
            new ProcessSeedReport(
                seededDefinitionIds,
                seededRunIds,
                primaryDefinitionId,
                secondaryDefinitionId));
    }

    private async Task<Result<Guid>> EnsureBaselineDefinitionAsync(
        ProcessTemplatePack pack,
        ProcessTemplateBaselineScenario scenario,
        Guid? projectId,
        ICollection<Guid> seededDefinitionIds,
        ICollection<Guid> seededRunIds,
        CancellationToken cancellationToken)
    {
        if (!pack.Processes.TryGetValue(scenario.ProcessTemplateKey, out var process))
        {
            return Result<Guid>.Failure(
                Error.Validation(
                    $"Template process '{scenario.ProcessTemplateKey}' was not found in the process template pack.",
                    "processes.seed-template-not-found"));
        }

        var dependencyResult = await EnsureTemplateDependencyDefinitionsAsync(
            pack,
            process,
            projectId,
            seededDefinitionIds,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
        if (dependencyResult.IsFailure)
        {
            return Result<Guid>.Failure(dependencyResult.Errors.ToArray());
        }

        var existingDefinition = (await processesService.ListDefinitionsAsync(projectId, cancellationToken))
            .FirstOrDefault(item =>
                string.Equals(item.Name, process.DisplayName, StringComparison.OrdinalIgnoreCase) &&
                item.ProjectId == projectId);

        Guid definitionId;
        if (existingDefinition is null)
        {
            var envelope = projectionService.GetProjectedEnvelope(process.Key, projectId);
            var importResult = await processesService.ImportAsync(envelope, cancellationToken);
            if (importResult.IsFailure)
            {
                return Result<Guid>.Failure(importResult.Errors.ToArray());
            }

            definitionId = importResult.Value;
        }
        else
        {
            definitionId = existingDefinition.Id;
        }

        seededDefinitionIds.Add(definitionId);

        var refreshedDefinition = (await processesService.ListDefinitionsAsync(projectId, cancellationToken))
            .Single(item => item.Id == definitionId);
        if (!refreshedDefinition.HasPublishedVersion)
        {
            var publishResult = await processesService.PublishAsync(definitionId, cancellationToken);
            if (publishResult.IsFailure)
            {
                return Result<Guid>.Failure(publishResult.Errors.ToArray());
            }
        }

        var existingRun = (await processesService.ListRunsAsync(definitionId, projectId, cancellationToken))
            .FirstOrDefault(item => string.Equals(item.Name, scenario.RunName, StringComparison.OrdinalIgnoreCase));

        Guid runId;
        if (existingRun is null)
        {
            var runResult = await processesService.StartRunAsync(
                new ProcessRunStartRequest
                {
                    ProcessDefinitionId = definitionId,
                    ProjectId = projectId,
                    RunName = scenario.RunName,
                    OperatingMode = ParseEnum(scenario.OperatingMode, ProcessOperatingMode.AssistedExecution),
                    TriggerReason = string.IsNullOrWhiteSpace(scenario.TriggerReason)
                        ? $"Template-pack baseline scenario / {scenario.Key}"
                        : scenario.TriggerReason
                },
                cancellationToken);
            if (runResult.IsFailure)
            {
                return Result<Guid>.Failure(runResult.Errors.ToArray());
            }

            runId = runResult.Value;
        }
        else
        {
            runId = existingRun.Id;
        }

        seededRunIds.Add(runId);

        var runtimeResult = await EnsureScenarioRuntimeStateAsync(scenario, runId, cancellationToken);
        return runtimeResult.IsFailure
            ? Result<Guid>.Failure(runtimeResult.Errors.ToArray())
            : Result<Guid>.Success(definitionId);
    }

    private async Task<Result> EnsureTemplateDependencyDefinitionsAsync(
        ProcessTemplatePack pack,
        ProcessTemplateDefinition process,
        Guid? projectId,
        ICollection<Guid> seededDefinitionIds,
        ISet<string> visitedProcessKeys,
        CancellationToken cancellationToken)
    {
        if (!visitedProcessKeys.Add(process.Key))
        {
            return Result.Success();
        }

        foreach (var subprocessKey in process.Steps
                     .Select(step => step.SubprocessProcessKey)
                     .Where(key => !string.IsNullOrWhiteSpace(key))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!pack.Processes.TryGetValue(subprocessKey, out var subprocess))
            {
                return Result.Failure(
                    Error.Validation(
                        $"Template process '{process.Key}' references subprocess '{subprocessKey}', but that process was not found in the process template pack.",
                        "processes.seed-subprocess-template-not-found"));
            }

            var nestedResult = await EnsureTemplateDependencyDefinitionsAsync(
                pack,
                subprocess,
                projectId,
                seededDefinitionIds,
                visitedProcessKeys,
                cancellationToken);
            if (nestedResult.IsFailure)
            {
                return nestedResult;
            }

            var ensureResult = await EnsureTemplateDefinitionImportedAndPublishedAsync(
                subprocess,
                projectId,
                cancellationToken);
            if (ensureResult.IsFailure)
            {
                return Result.Failure(ensureResult.Errors.ToArray());
            }

            if (!seededDefinitionIds.Contains(ensureResult.Value))
            {
                seededDefinitionIds.Add(ensureResult.Value);
            }
        }

        return Result.Success();
    }

    private async Task<Result<Guid>> EnsureTemplateDefinitionImportedAndPublishedAsync(
        ProcessTemplateDefinition process,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var existingDefinition = (await processesService.ListDefinitionsAsync(projectId, cancellationToken))
            .FirstOrDefault(item =>
                string.Equals(item.Name, process.DisplayName, StringComparison.OrdinalIgnoreCase) &&
                item.ProjectId == projectId);

        var definitionId = existingDefinition?.Id;
        if (!definitionId.HasValue)
        {
            var envelope = projectionService.GetProjectedEnvelope(process.Key, projectId);
            var importResult = await processesService.ImportAsync(envelope, cancellationToken);
            if (importResult.IsFailure)
            {
                return Result<Guid>.Failure(importResult.Errors.ToArray());
            }

            definitionId = importResult.Value;
        }

        var refreshedDefinition = (await processesService.ListDefinitionsAsync(projectId, cancellationToken))
            .Single(item => item.Id == definitionId.Value);
        if (!refreshedDefinition.HasPublishedVersion)
        {
            var publishResult = await processesService.PublishAsync(definitionId.Value, cancellationToken);
            if (publishResult.IsFailure)
            {
                return Result<Guid>.Failure(publishResult.Errors.ToArray());
            }
        }

        return Result<Guid>.Success(definitionId.Value);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
    {
        return EnumValueParser.ParseOrDefault(value, fallback);
    }

    private static ProcessArtifactTrustStatus ParseArtifactTrustStatus(string? value)
    {
        if (string.Equals(value, "HumanApproved", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactTrustStatus.Approved;
        }

        return ParseEnum(value, ProcessArtifactTrustStatus.ReviewRequired);
    }
}

public sealed record ProcessSeedReport(
    IReadOnlyCollection<Guid> SeededDefinitionIds,
    IReadOnlyCollection<Guid> SeededRunIds,
    Guid PrimaryDefinitionId,
    Guid SecondaryDefinitionId);
