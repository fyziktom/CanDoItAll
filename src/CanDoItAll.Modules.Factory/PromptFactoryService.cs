using System.Text.Json;
using CanDoItAll.ComponentKit.Canvas;
using CanDoItAll.Infrastructure.BackgroundJobs;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Factory;

/* codex-capsule
kind: service
name: PromptFactoryService
summary: Builds prompt sessions from centralized blueprints, flow templates, shared blocks, project context, provider settings, and prompt-library pack state.
owns: prompt-build-sessions, prompt-run nodes, prompt export/send flows, prompt-library import
deps: AppDbContext, ProjectsService, ResourcesService, WorkspaceService, ProviderExecutionService, PromptsService, IManagedArtifactStore, IFileStore, PromptLibraryPackLoader, IBackgroundJobTracker
risks: missing-provider, stale-resource-selection, weak-defaults, missing-pack-files
tests: unit:PromptFactoryServiceTests, integration:PromptFactoryPersistenceTests
inputs: PromptFactoryEditorModel
outputs: generated prompt text, saved prompt ids, provider responses
*/
public sealed partial class PromptFactoryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ProjectsService projectsService,
    ResourcesService resourcesService,
    WorkspaceService workspaceService,
    ProviderExecutionService providerExecutionService,
    PromptsService promptsService,
    IManagedArtifactStore managedArtifactStore,
    IFileStore fileStore,
    PromptLibraryPackLoader promptLibraryPackLoader,
    IBackgroundJobTracker backgroundJobTracker,
    IActivityStream activityStream,
    ISearchIndexService searchIndexService,
    IClock clock)
{
    public Task<PromptFactoryEditorModel> GetEditorAsync(Guid? sessionId, CancellationToken cancellationToken = default)
        => GetEditorAsync(sessionId, null, cancellationToken);

    public async Task<PromptFactoryEditorModel> GetEditorAsync(Guid? sessionId, Guid? promptRunId, CancellationToken cancellationToken = default)
    {
        await EnsureSeedsAsync(cancellationToken);
        if (sessionId.HasValue)
        {
            return await GetEditorFromSessionAsync(sessionId.Value, cancellationToken);
        }

        if (promptRunId.HasValue)
        {
            return await GetEditorFromRunAsync(promptRunId.Value, cancellationToken);
        }

        var defaults = await GetSeedDefaultsAsync(cancellationToken);
        var settings = await workspaceService.GetSettingsAsync(cancellationToken);
        return new PromptFactoryEditorModel
        {
            SessionName = "Prompt session",
            BlueprintId = defaults.BlueprintId,
            FlowTemplateId = defaults.FlowTemplateId,
            ProviderProfileId = settings.DefaultProviderProfileId,
            SelectedBlockIds = defaults.BlockIds.ToList(),
            CanvasUiStateJson = "{}",
            DraftTitle = "Prompt Factory Draft",
            ComponentCustomizations = [],
            SessionAttachments = []
        };
    }

    private async Task<PromptFactoryEditorModel> GetEditorFromSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.Set<PromptBuildSession>().FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return await GetEditorAsync(null, null, cancellationToken);
        }

        return new PromptFactoryEditorModel
        {
            SessionId = session.Id,
            PromptRunId = session.PromptRunId,
            SessionName = session.Name,
            ProjectId = session.ProjectId,
            Phase = session.Phase,
            BlueprintId = session.BlueprintId,
            FlowTemplateId = session.FlowTemplateId,
            ProviderProfileId = session.ProviderProfileId,
            RepositoryName = session.RepositoryName,
            BranchName = session.BranchName,
            CommitSha = session.CommitSha,
            SelectedBlockIds = DeserializeIds(session.SelectedBlockIdsJson).ToList(),
            SelectedResourceIds = DeserializeIds(session.SelectedResourceIdsJson).ToList(),
            GeneratedPrompt = session.GeneratedPrompt,
            WarningSummary = session.WarningSummary,
            DraftTitle = BuildDraftTitle(session.Phase),
            Warnings = SplitWarnings(session.WarningSummary),
            CanvasUiStateJson = string.IsNullOrWhiteSpace(session.CanvasUiStateJson) ? "{}" : session.CanvasUiStateJson,
            ComponentCustomizations = DeserializeJson<List<PromptSessionComponentCustomization>>(session.ComponentCustomizationsJson),
            SessionAttachments = DeserializeJson<List<PromptSessionAttachmentSummary>>(session.SessionAttachmentsJson),
            Nodes = session.PromptRunId.HasValue ? (await LoadRunNodesAsync(session.PromptRunId.Value, cancellationToken)).ToList() : [],
            HasCustomizedBlocks = session.HasCustomizedBlocks,
            WizardStepIndex = session.WizardStepIndex,
            SelectedNodeId = session.SelectedPromptRunNodeId
        };
    }

    private async Task<PromptFactoryEditorModel> GetEditorFromRunAsync(Guid promptRunId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sessions = await dbContext.Set<PromptBuildSession>()
            .Where(item => item.PromptRunId == promptRunId)
            .ToListAsync(cancellationToken);
        var session = sessions
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefault();

        if (session is not null)
        {
            return await GetEditorFromSessionAsync(session.Id, cancellationToken);
        }

        var run = await dbContext.Set<PromptRun>().FirstOrDefaultAsync(item => item.Id == promptRunId, cancellationToken);
        if (run is null)
        {
            return await GetEditorAsync(null, null, cancellationToken);
        }

        var nodes = await dbContext.Set<PromptRunNode>()
            .Where(item => item.PromptRunId == promptRunId)
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);

        return new PromptFactoryEditorModel
        {
            PromptRunId = run.Id,
            SessionName = BuildSessionName(run.Name, run.Phase),
            ProjectId = run.ProjectId,
            Phase = run.Phase,
            FlowTemplateId = run.FlowTemplateId,
            SelectedBlockIds = nodes
                .Where(item => item.PromptBlockDefinitionId.HasValue)
                .Select(item => item.PromptBlockDefinitionId!.Value)
                .Distinct()
                .ToList(),
            CanvasUiStateJson = "{}",
            ComponentCustomizations = [],
            SessionAttachments = [],
            Nodes = nodes
                .Select(MapRunNodeSummary)
                .ToList()
        };
    }

    public async Task<IReadOnlyList<PromptBlockSummary>> ListBlocksAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeedsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var blocks = await dbContext.Set<PromptBlockDefinition>().ToListAsync(cancellationToken);
        return blocks
            .OrderBy(item => IsPackManaged(item) ? 0 : 1)
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.BlockKind)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapBlockSummary)
            .ToList();
    }

    public async Task<IReadOnlyList<PromptFlowTemplateSummary>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeedsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var templates = await dbContext.Set<PromptFlowTemplate>().ToListAsync(cancellationToken);
        return templates
            .OrderBy(item => IsPackManaged(item) ? 0 : 1)
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapTemplateSummary)
            .ToList();
    }

    public async Task<IReadOnlyList<PromptBlueprintSummary>> ListBlueprintsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeedsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var blueprints = await dbContext.Set<PromptBlueprint>().ToListAsync(cancellationToken);
        return blueprints
            .OrderBy(item => IsPackManaged(item) ? 0 : 1)
            .ThenBy(item => item.OrderIndex)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapBlueprintSummary)
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> GetRecommendedBlockIdsAsync(
        Guid? blueprintId,
        Guid? flowTemplateId,
        string? phase,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var blocks = await dbContext.Set<PromptBlockDefinition>().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var templates = await dbContext.Set<PromptFlowTemplate>().ToListAsync(cancellationToken);
        var blueprints = await dbContext.Set<PromptBlueprint>().ToListAsync(cancellationToken);

        var blueprint = blueprints.FirstOrDefault(item => item.Id == blueprintId);
        var promptType = blueprint?.PromptType ?? string.Empty;
        var selectedTemplate = flowTemplateId.HasValue
            ? templates.FirstOrDefault(item => item.Id == flowTemplateId.Value)
            : null;

        var recommendedIds = new HashSet<Guid>();
        if (selectedTemplate is not null)
        {
            foreach (var id in DeserializeIds(selectedTemplate.BlockIdsJson))
            {
                recommendedIds.Add(id);
            }
        }

        foreach (var block in blocks)
        {
            if (block.IsRecommendedByDefault ||
                RuleMatches(block.PromptTypeRules, promptType) ||
                RuleMatches(block.BlueprintRules, blueprint?.Key, blueprint?.Name) ||
                RuleMatches(block.PhaseRules, phase) ||
                RuleMatches(selectedTemplate?.PromptTypeRules, promptType))
            {
                recommendedIds.Add(block.Id);
            }
        }

        return recommendedIds.ToList();
    }

    public async Task<PromptBlockEditorModel> GetBlockEditorAsync(Guid? blockId, CancellationToken cancellationToken = default)
    {
        if (!blockId.HasValue)
        {
            return new PromptBlockEditorModel();
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var block = await dbContext.Set<PromptBlockDefinition>().FirstOrDefaultAsync(item => item.Id == blockId.Value, cancellationToken);
        if (block is null)
        {
            return new PromptBlockEditorModel();
        }

        return new PromptBlockEditorModel
        {
            Id = block.Id,
            Name = block.Name,
            BlockKind = block.BlockKind,
            Summary = block.Summary,
            Content = block.Content,
            IsRecommendedByDefault = block.IsRecommendedByDefault,
            PromptTypes = block.PromptTypeRules,
            Blueprints = block.BlueprintRules,
            Phases = block.PhaseRules
        };
    }

    public async Task<PromptFlowTemplateEditorModel> GetTemplateEditorAsync(Guid? templateId, CancellationToken cancellationToken = default)
    {
        if (!templateId.HasValue)
        {
            return new PromptFlowTemplateEditorModel();
        }

        await EnsureSchemaAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var template = await dbContext.Set<PromptFlowTemplate>().FirstOrDefaultAsync(item => item.Id == templateId.Value, cancellationToken);
        if (template is null)
        {
            return new PromptFlowTemplateEditorModel();
        }

        return new PromptFlowTemplateEditorModel
        {
            Id = template.Id,
            Name = template.Name,
            Summary = template.Summary,
            SelectedBlockIds = DeserializeIds(template.BlockIdsJson).ToList(),
            RecommendedPromptTypes = template.PromptTypeRules
        };
    }

public async Task<Result<PromptFactoryEditorModel>> SaveSessionStateAsync(PromptFactoryEditorModel model, CancellationToken cancellationToken = default)
{
        if (!model.ProjectId.HasValue)
        {
            return Result<PromptFactoryEditorModel>.Failure(Error.Validation("Select a project before saving a prompt session."));
        }

        await EnsureSeedsAsync(cancellationToken);
var sessionId = await UpsertSessionAsync(model, model.GeneratedPrompt, model.Warnings, cancellationToken);
return Result<PromptFactoryEditorModel>.Success(await GetEditorAsync(sessionId, cancellationToken));
}

public async Task SaveCanvasUiStateAsync(Guid sessionId, string stateJson, Guid? selectedPromptRunNodeId, CancellationToken cancellationToken = default)
{
await EnsureSchemaAsync(cancellationToken);
await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
var session = await dbContext.Set<PromptBuildSession>().FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
if (session is null)
{
return;
}

session.CanvasUiStateJson = string.IsNullOrWhiteSpace(stateJson) ? "{}" : stateJson;
session.SelectedPromptRunNodeId = selectedPromptRunNodeId;
session.UpdatedAtUtc = clock.GetUtcNow();
await dbContext.SaveChangesAsync(cancellationToken);
}

    public async Task<Result<Guid>> SaveBlockAsync(PromptBlockEditorModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<Guid>.Failure(Error.Validation("Block name is required."));
        }

        if (string.IsNullOrWhiteSpace(model.Content))
        {
            return Result<Guid>.Failure(Error.Validation("Block content is required."));
        }

        await EnsureSeedsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = model.Id.HasValue
            ? await dbContext.Set<PromptBlockDefinition>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new PromptBlockDefinition();
            await dbContext.Set<PromptBlockDefinition>().AddAsync(entity, cancellationToken);
        }

        entity.Name = model.Name.Trim();
        entity.Key = string.IsNullOrWhiteSpace(entity.Key) ? BuildKey(entity.Name) : entity.Key;
        entity.BlockKind = model.BlockKind;
        entity.Summary = model.Summary?.Trim() ?? string.Empty;
        entity.Content = model.Content.Trim();
        entity.IsRecommendedByDefault = model.IsRecommendedByDefault;
        entity.PromptTypeRules = NormalizeTokens(model.PromptTypes);
        entity.BlueprintRules = NormalizeTokens(model.Blueprints);
        entity.PhaseRules = NormalizeTokens(model.Phases);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

    public async Task<Result<Guid>> SaveTemplateAsync(PromptFlowTemplateEditorModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result<Guid>.Failure(Error.Validation("Template name is required."));
        }

        await EnsureSeedsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = model.Id.HasValue
            ? await dbContext.Set<PromptFlowTemplate>().FirstOrDefaultAsync(item => item.Id == model.Id.Value, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new PromptFlowTemplate();
            await dbContext.Set<PromptFlowTemplate>().AddAsync(entity, cancellationToken);
        }

        entity.Name = model.Name.Trim();
        entity.Key = string.IsNullOrWhiteSpace(entity.Key) ? BuildKey(entity.Name) : entity.Key;
        entity.Summary = model.Summary?.Trim() ?? string.Empty;
        entity.BlockIdsJson = SerializeIds(model.SelectedBlockIds);
        entity.PromptTypeRules = NormalizeTokens(model.RecommendedPromptTypes);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(entity.Id);
    }

public async Task<Result<PromptRunNodeSummary>> BranchNodeAsync(Guid promptRunNodeId, string? branchLabel = null, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var source = await dbContext.Set<PromptRunNode>().FirstOrDefaultAsync(item => item.Id == promptRunNodeId, cancellationToken);
        if (source is null)
        {
            return Result<PromptRunNodeSummary>.Failure(Error.Failure("The selected prompt step could not be found."));
        }

        var branchName = string.IsNullOrWhiteSpace(branchLabel)
            ? BuildBranchLabel(source.Title)
            : branchLabel.Trim();
        var branchKey = BuildBranchKey(branchName);
        var nextSequence = await dbContext.Set<PromptRunNode>()
            .Where(item => item.PromptRunId == source.PromptRunId)
            .MaxAsync(item => (int?)item.Sequence, cancellationToken) ?? -1;

        var node = new PromptRunNode
        {
            PromptRunId = source.PromptRunId,
            PromptBlockDefinitionId = source.PromptBlockDefinitionId,
            ParentPromptRunNodeId = source.Id,
            Title = $"{source.Title} follow-up",
            BranchKey = branchKey,
            BranchLabel = branchName,
            Sequence = nextSequence + 1,
            State = PromptRunNodeState.Pending,
            Notes = $"Branched from {source.Title}."
        };

        await dbContext.Set<PromptRunNode>().AddAsync(node, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
return Result<PromptRunNodeSummary>.Success(MapRunNodeSummary(node));
}

public async Task<Result<PromptRunNodeSummary>> UpdateNodeAsync(Guid promptRunNodeId, string title, string notes, CancellationToken cancellationToken = default)
{
await EnsureSchemaAsync(cancellationToken);
await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
var node = await dbContext.Set<PromptRunNode>().FirstOrDefaultAsync(item => item.Id == promptRunNodeId, cancellationToken);
if (node is null)
{
return Result<PromptRunNodeSummary>.Failure(Error.Failure("The selected prompt step could not be found."));
}

node.Title = string.IsNullOrWhiteSpace(title) ? node.Title : title.Trim();
node.Notes = notes?.Trim() ?? string.Empty;
await dbContext.SaveChangesAsync(cancellationToken);
return Result<PromptRunNodeSummary>.Success(MapRunNodeSummary(node));
}

public async Task<Result<PromptRunNodeSummary>> SetNodeStateAsync(Guid promptRunNodeId, PromptRunNodeState state, CancellationToken cancellationToken = default)
{
await EnsureSchemaAsync(cancellationToken);
await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
var node = await dbContext.Set<PromptRunNode>().FirstOrDefaultAsync(item => item.Id == promptRunNodeId, cancellationToken);
if (node is null)
{
return Result<PromptRunNodeSummary>.Failure(Error.Failure("The selected prompt step could not be found."));
}

node.State = state;
await dbContext.SaveChangesAsync(cancellationToken);
return Result<PromptRunNodeSummary>.Success(MapRunNodeSummary(node));
}

    public async Task<Result<PromptFactoryEditorModel>> BuildAsync(PromptFactoryEditorModel model, CancellationToken cancellationToken = default)
    {
        if (!model.ProjectId.HasValue)
        {
            return Result<PromptFactoryEditorModel>.Failure(Error.Validation("Select a project before building a prompt."));
        }

        await EnsureSeedsAsync(cancellationToken);

        var project = await projectsService.GetAsync(model.ProjectId, cancellationToken);
        var resources = await resourcesService.ListAsync(cancellationToken);
        var selectedResources = resources
            .Where(resource => resource.ProjectId == model.ProjectId.Value &&
                               (model.SelectedResourceIds.Count == 0 || model.SelectedResourceIds.Contains(resource.Id)))
            .ToList();

        var effectiveBlockIds = await ResolveEffectiveBlockIdsAsync(model, cancellationToken);
        var blocks = await LoadResolvedBlocksAsync(effectiveBlockIds, model, model.FlowTemplateId, cancellationToken);
        var blueprint = (await ListBlueprintsAsync(cancellationToken)).FirstOrDefault(item => item.Id == model.BlueprintId);
        var warnings = BuildWarnings(model, project, selectedResources);
        var prompt = ComposePrompt(model, project, blueprint, blocks, selectedResources);

        model.SelectedBlockIds = effectiveBlockIds.ToList();
        var sessionId = await UpsertSessionAsync(model, prompt, warnings, cancellationToken);
        await searchIndexService.UpsertAsync(new SearchDocumentInput(
            "prompt-session",
            sessionId.ToString(),
            "Prompt Factory",
            BuildDraftTitle(string.IsNullOrWhiteSpace(model.Phase) ? project.CurrentPhase : model.Phase),
            string.Join(" ", warnings),
            prompt,
            $"/prompt-factory?sessionId={sessionId}",
            model.ProjectId), cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "factory",
            "build",
            "Built prompt session",
            BuildDraftTitle(string.IsNullOrWhiteSpace(model.Phase) ? project.CurrentPhase : model.Phase),
            ProjectId: model.ProjectId,
            ArtifactKind: "prompt-session",
            ArtifactId: sessionId,
            Route: $"/prompt-factory?sessionId={sessionId}"), cancellationToken);

        var updated = await GetEditorAsync(sessionId, cancellationToken);
        updated.GeneratedPrompt = prompt;
        updated.WarningSummary = string.Join('\n', warnings);
        updated.Warnings = warnings;
        updated.DraftTitle = BuildDraftTitle(string.IsNullOrWhiteSpace(model.Phase) ? project.CurrentPhase : model.Phase);
        return Result<PromptFactoryEditorModel>.Success(updated);
    }

    public async Task<Result<Guid>> SaveDraftAsync(PromptFactoryEditorModel model, CancellationToken cancellationToken = default)
    {
        var build = await BuildAsync(model, cancellationToken);
        if (build.IsFailure)
        {
            return Result<Guid>.Failure(build.Errors);
        }

        var promptEditor = new PromptEditorModel
        {
            ProjectId = build.Value!.ProjectId,
            Title = string.IsNullOrWhiteSpace(model.DraftTitle) ? BuildDraftTitle(build.Value.Phase) : model.DraftTitle.Trim(),
            Phase = build.Value.Phase,
            DraftText = build.Value.GeneratedPrompt,
            FinalizationReason = "Saved from Prompt Factory"
        };

        var saved = await promptsService.SaveDraftAsync(promptEditor, cancellationToken);
        if (saved.IsSuccess && build.Value.SessionId.HasValue)
        {
            await LinkSessionToPromptAsync(build.Value.SessionId.Value, saved.Value, cancellationToken);
        }

        return saved;
    }

    public async Task<Result<Guid>> SaveFinalAsync(PromptFactoryEditorModel model, CancellationToken cancellationToken = default)
    {
        var build = await BuildAsync(model, cancellationToken);
        if (build.IsFailure)
        {
            return Result<Guid>.Failure(build.Errors);
        }

        var promptEditor = new PromptEditorModel
        {
            ProjectId = build.Value!.ProjectId,
            Title = string.IsNullOrWhiteSpace(model.DraftTitle) ? BuildDraftTitle(build.Value.Phase) : model.DraftTitle.Trim(),
            Phase = build.Value.Phase,
            DraftText = build.Value.GeneratedPrompt,
            FinalizationReason = "Finalized from Prompt Factory"
        };

        var saved = await promptsService.FinalizeAsync(promptEditor, cancellationToken);
        if (saved.IsSuccess && build.Value.SessionId.HasValue)
        {
            await LinkSessionToPromptAsync(build.Value.SessionId.Value, saved.Value, cancellationToken);
        }

        return saved;
    }

    public async Task<Result<string>> ExportAsync(PromptFactoryEditorModel model, CancellationToken cancellationToken = default)
    {
        var build = await BuildAsync(model, cancellationToken);
        if (build.IsFailure)
        {
            return Result<string>.Failure(build.Errors);
        }

        var jobId = await backgroundJobTracker.EnqueueTrackedAsync("prompt-export", "Export generated prompt", cancellationToken: cancellationToken);
        await backgroundJobTracker.MarkRunningAsync(jobId, cancellationToken);

        try
        {
            var fileName = $"prompt-{build.Value!.SessionId ?? Guid.NewGuid():N}.md";
            var fullPath = await managedArtifactStore.SaveTextAsync("exports", fileName, build.Value.GeneratedPrompt, cancellationToken);
            await backgroundJobTracker.MarkSucceededAsync(jobId, cancellationToken);
            await activityStream.RecordAsync(new ActivityWriteRequest(
                "factory",
                "export",
                "Exported prompt session",
                fullPath,
                ProjectId: build.Value.ProjectId,
                ArtifactKind: "prompt-session",
                ArtifactId: build.Value.SessionId,
                Route: $"/prompt-factory?sessionId={build.Value.SessionId}"), cancellationToken);
            return Result<string>.Success(fullPath);
        }
        catch (Exception ex)
        {
            await backgroundJobTracker.MarkFailedAsync(jobId, ex.Message, cancellationToken);
            return Result<string>.Failure(Error.Failure(ex.Message));
        }
    }

    public async Task<Result<ProviderExecutionResponse>> SendAsync(PromptFactoryEditorModel model, CancellationToken cancellationToken = default)
    {
        var build = await BuildAsync(model, cancellationToken);
        if (build.IsFailure)
        {
            return Result<ProviderExecutionResponse>.Failure(build.Errors);
        }

        if (!build.Value!.ProviderProfileId.HasValue)
        {
            return Result<ProviderExecutionResponse>.Failure(Error.Validation("Select a provider profile before sending."));
        }

        var promptIdResult = await SaveDraftAsync(build.Value, cancellationToken);
        if (promptIdResult.IsFailure)
        {
            return Result<ProviderExecutionResponse>.Failure(promptIdResult.Errors);
        }

        var jobId = await backgroundJobTracker.EnqueueTrackedAsync("prompt-send", "Send generated prompt to provider", cancellationToken: cancellationToken);
        await backgroundJobTracker.MarkRunningAsync(jobId, cancellationToken);

        var result = await providerExecutionService.SendAsync(new ProviderExecutionRequest(
            build.Value.ProviderProfileId.Value,
            build.Value.GeneratedPrompt,
            OutputFormat: "Markdown",
            ContainsSensitiveContent: build.Value.Warnings.Any(warning => warning.Contains("sensitive", StringComparison.OrdinalIgnoreCase))), cancellationToken);

        if (result.IsSuccess)
        {
            await promptsService.RecordUsageAsync(
                promptIdResult.Value,
                null,
                build.Value.ProjectId,
                build.Value.Phase,
                result.Value!.ProviderName,
                build.Value.RepositoryName,
                build.Value.BranchName,
                build.Value.CommitSha,
                string.Empty,
                "Sent from Prompt Factory",
                cancellationToken);
            await backgroundJobTracker.MarkSucceededAsync(jobId, cancellationToken);
        }
        else
        {
            await backgroundJobTracker.MarkFailedAsync(jobId, string.Join(" ", result.Errors.Select(error => error.Message)), cancellationToken);
        }

        return result;
    }

    public async Task<IReadOnlyList<PromptRunNodeSummary>> LoadRunNodesAsync(Guid promptRunId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PromptRunNode>()
            .Where(item => item.PromptRunId == promptRunId)
            .OrderBy(item => item.BranchKey)
            .ThenBy(item => item.Sequence)
            .Select(item => new PromptRunNodeSummary(
                item.Id,
                item.Title,
                item.BranchKey,
                item.BranchLabel,
                item.Sequence,
                item.State,
                item.PromptArtifactId,
                item.ParentPromptRunNodeId,
                item.Notes))
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureSeedsAsync(CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken);
        var pack = promptLibraryPackLoader.Load();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var blocks = await dbContext.Set<PromptBlockDefinition>().ToListAsync(cancellationToken);
        var templates = await dbContext.Set<PromptFlowTemplate>().ToListAsync(cancellationToken);
        var blueprints = await dbContext.Set<PromptBlueprint>().ToListAsync(cancellationToken);

        foreach (var seed in pack.Components)
        {
            var entity = blocks.FirstOrDefault(item => item.Id == seed.Id) ??
                         blocks.FirstOrDefault(item =>
                             string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(item.Key, seed.Key, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                entity = new PromptBlockDefinition { Id = seed.Id };
                await dbContext.Set<PromptBlockDefinition>().AddAsync(entity, cancellationToken);
                blocks.Add(entity);
            }

            entity.Id = seed.Id;
            entity.Key = seed.Key;
            entity.Name = seed.Name;
            entity.BlockKind = MapPromptBlockKind(seed.BlockKind);
            entity.Summary = seed.Summary;
            entity.Content = seed.Content;
            entity.IsRecommendedByDefault = seed.IsRecommendedByDefault;
            entity.PromptTypeRules = NormalizeTokens(seed.PromptTypeRules);
            entity.BlueprintRules = NormalizeTokens(seed.BlueprintRules);
            entity.PhaseRules = NormalizeTokens(seed.PhaseRules);
            entity.GroupKey = seed.Group;
            entity.TagsJson = SerializeJson(seed.Tags);
            entity.StackTagsJson = SerializeJson(seed.StackTags);
            entity.TemplateTokensJson = SerializeJson(seed.TemplateTokens);
            entity.ToolboxEligible = seed.ToolboxEligible;
            entity.OrderIndex = seed.OrderIndex;
            entity.CatalogSource = PromptLibraryPackLoader.CatalogSource;
        }

        var componentIds = pack.Components.Select(item => item.Id).ToHashSet();
        dbContext.RemoveRange(blocks.Where(item =>
            string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase) &&
            !componentIds.Contains(item.Id)));

        foreach (var seed in pack.Flows)
        {
            var entity = templates.FirstOrDefault(item => item.Id == seed.Id) ??
                         templates.FirstOrDefault(item =>
                             string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(item.Key, seed.Key, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                entity = new PromptFlowTemplate { Id = seed.Id };
                await dbContext.Set<PromptFlowTemplate>().AddAsync(entity, cancellationToken);
                templates.Add(entity);
            }

            entity.Id = seed.Id;
            entity.Key = seed.Key;
            entity.Name = seed.Name;
            entity.Summary = seed.Summary;
            entity.BlockIdsJson = SerializeIds(DeserializeIds(seed.BlockIdsJson));
            entity.BlockKeysJson = SerializeJson(seed.BlockKeys);
            entity.AgentSequenceJson = SerializeJson(seed.AgentSequence);
            entity.PromptTypeRules = NormalizeTokens(seed.PromptTypeRules);
            entity.OrderIndex = seed.OrderIndex;
            entity.CatalogSource = PromptLibraryPackLoader.CatalogSource;
        }

        var templateIds = pack.Flows.Select(item => item.Id).ToHashSet();
        dbContext.RemoveRange(templates.Where(item =>
            string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase) &&
            !templateIds.Contains(item.Id)));

        foreach (var seed in pack.Blueprints)
        {
            var entity = blueprints.FirstOrDefault(item => item.Id == seed.Id) ??
                         blueprints.FirstOrDefault(item =>
                             string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(item.Key, seed.Key, StringComparison.OrdinalIgnoreCase));
            if (entity is null)
            {
                entity = new PromptBlueprint { Id = seed.Id };
                await dbContext.Set<PromptBlueprint>().AddAsync(entity, cancellationToken);
                blueprints.Add(entity);
            }

            entity.Id = seed.Id;
            entity.Key = seed.Key;
            entity.Name = seed.Name;
            entity.PromptType = seed.PromptType;
            entity.Summary = seed.Summary;
            entity.Guidance = seed.Guidance;
            entity.RecommendedFlowTemplateId = seed.RecommendedFlowTemplateId;
            entity.RecommendedFlowKey = seed.RecommendedFlowKey;
            entity.RecommendedBlockKeysJson = SerializeJson(seed.RecommendedBlockKeys);
            entity.OrderIndex = seed.OrderIndex;
            entity.CatalogSource = PromptLibraryPackLoader.CatalogSource;
        }

        var blueprintIds = pack.Blueprints.Select(item => item.Id).ToHashSet();
        dbContext.RemoveRange(blueprints.Where(item =>
            string.Equals(item.CatalogSource, PromptLibraryPackLoader.CatalogSource, StringComparison.OrdinalIgnoreCase) &&
            !blueprintIds.Contains(item.Id)));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await PromptFactorySchemaInitializer.EnsureAsync(dbContext, cancellationToken);
    }

    private async Task<(Guid? BlueprintId, Guid? FlowTemplateId, IReadOnlyList<Guid> BlockIds)> GetSeedDefaultsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var blueprint = await dbContext.Set<PromptBlueprint>()
            .Where(item => item.CatalogSource == PromptLibraryPackLoader.CatalogSource)
            .OrderBy(item => item.OrderIndex)
            .ThenBy(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);
        var template = await dbContext.Set<PromptFlowTemplate>()
            .Where(item => item.CatalogSource == PromptLibraryPackLoader.CatalogSource)
            .OrderBy(item => item.OrderIndex)
            .ThenBy(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);
        var blocks = await dbContext.Set<PromptBlockDefinition>()
            .Where(item => item.IsRecommendedByDefault &&
                           item.CatalogSource == PromptLibraryPackLoader.CatalogSource)
            .OrderBy(item => item.OrderIndex)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        return (blueprint?.Id, template?.Id, blocks);
    }

    private async Task<List<PromptBlockDefinition>> LoadBlocksAsync(IReadOnlyCollection<Guid> selectedBlockIds, Guid? flowTemplateId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var blocks = await dbContext.Set<PromptBlockDefinition>().ToListAsync(cancellationToken);
        var ids = selectedBlockIds.ToHashSet();

        if (ids.Count == 0 && flowTemplateId.HasValue)
        {
            var template = await dbContext.Set<PromptFlowTemplate>().FirstOrDefaultAsync(item => item.Id == flowTemplateId.Value, cancellationToken);
            if (template is not null)
            {
                ids = DeserializeIds(template.BlockIdsJson).ToHashSet();
            }
        }

        if (ids.Count == 0)
        {
            ids = blocks.Where(item => item.IsRecommendedByDefault).Select(item => item.Id).ToHashSet();
        }

        return blocks
            .Where(item => ids.Contains(item.Id))
            .OrderBy(item => item.OrderIndex)
            .ThenBy(item => item.BlockKind)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<Guid>> ResolveEffectiveBlockIdsAsync(PromptFactoryEditorModel model, CancellationToken cancellationToken)
    {
        if (model.HasCustomizedBlocks && model.SelectedBlockIds.Count > 0)
        {
            return model.SelectedBlockIds.Distinct().ToList();
        }

        return await GetRecommendedBlockIdsAsync(model.BlueprintId, model.FlowTemplateId, model.Phase, cancellationToken);
    }

    private async Task<Guid> UpsertSessionAsync(
        PromptFactoryEditorModel model,
        string generatedPrompt,
        IReadOnlyCollection<string> warnings,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = model.SessionId.HasValue
            ? await dbContext.Set<PromptBuildSession>().FirstOrDefaultAsync(item => item.Id == model.SessionId.Value, cancellationToken)
            : null;

        if (session is null)
        {
            session = new PromptBuildSession();
            await dbContext.Set<PromptBuildSession>().AddAsync(session, cancellationToken);
        }

        if (!model.ProjectId.HasValue)
        {
            throw new InvalidOperationException("A prompt session requires a project.");
        }

        var project = await projectsService.GetAsync(model.ProjectId, cancellationToken);
        var phase = string.IsNullOrWhiteSpace(model.Phase) ? project.CurrentPhase : model.Phase.Trim();
        var effectiveBlockIds = await ResolveEffectiveBlockIdsAsync(model, cancellationToken);
        var blocks = await LoadBlocksAsync(effectiveBlockIds, model.FlowTemplateId, cancellationToken);

        session.Name = string.IsNullOrWhiteSpace(model.SessionName)
            ? BuildSessionName(project.Name, phase)
            : model.SessionName.Trim();
        session.ProjectId = model.ProjectId;
        session.Phase = phase;
        session.BlueprintId = model.BlueprintId;
        session.FlowTemplateId = model.FlowTemplateId;
        session.ProviderProfileId = model.ProviderProfileId;
        session.RepositoryName = model.RepositoryName?.Trim() ?? string.Empty;
        session.BranchName = model.BranchName?.Trim() ?? string.Empty;
        session.CommitSha = model.CommitSha?.Trim() ?? string.Empty;
        session.SelectedBlockIdsJson = SerializeIds(effectiveBlockIds);
        session.SelectedResourceIdsJson = SerializeIds(model.SelectedResourceIds);
        session.GeneratedPrompt = generatedPrompt ?? string.Empty;
        session.WarningSummary = string.Join('\n', warnings);
        session.CanvasUiStateJson = string.IsNullOrWhiteSpace(model.CanvasUiStateJson) ? "{}" : model.CanvasUiStateJson;
        session.ComponentCustomizationsJson = SerializeJson(model.ComponentCustomizations);
        session.SessionAttachmentsJson = SerializeJson(model.SessionAttachments);
        session.WizardStepIndex = model.WizardStepIndex;
        session.HasCustomizedBlocks = model.HasCustomizedBlocks;
        session.SelectedPromptRunNodeId = model.SelectedNodeId;
        session.UpdatedAtUtc = clock.GetUtcNow();

        if (!session.PromptRunId.HasValue)
        {
            session.PromptRunId = await EnsureRunAsync(dbContext, model.ProjectId.Value, phase, model.FlowTemplateId, blocks, cancellationToken);
        }
        else
        {
            await SyncRunNodesAsync(dbContext, session.PromptRunId.Value, blocks, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return session.Id;
    }

    private async Task<Guid> EnsureRunAsync(
        AppDbContext dbContext,
        Guid projectId,
        string phase,
        Guid? flowTemplateId,
        IReadOnlyList<PromptBlockDefinition> blocks,
        CancellationToken cancellationToken)
    {
        var flowTemplateIdValue = flowTemplateId ?? await dbContext.Set<PromptFlowTemplate>().Select(item => item.Id).FirstAsync(cancellationToken);
        var run = new PromptRun
        {
            ProjectId = projectId,
            FlowTemplateId = flowTemplateIdValue,
            Name = BuildRunName(phase),
            Phase = phase,
            CreatedAtUtc = clock.GetUtcNow(),
            UpdatedAtUtc = clock.GetUtcNow()
        };

        await dbContext.Set<PromptRun>().AddAsync(run, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await SyncRunNodesAsync(dbContext, run.Id, blocks, cancellationToken);
        return run.Id;
    }

    private static async Task SyncRunNodesAsync(AppDbContext dbContext, Guid runId, IReadOnlyList<PromptBlockDefinition> blocks, CancellationToken cancellationToken)
    {
        var nodes = await dbContext.Set<PromptRunNode>().Where(item => item.PromptRunId == runId).ToListAsync(cancellationToken);
        dbContext.RemoveRange(nodes.Where(node => blocks.All(block => block.Id != node.PromptBlockDefinitionId)));

        for (var index = 0; index < blocks.Count; index++)
        {
            var block = blocks[index];
            var node = nodes.FirstOrDefault(item =>
                item.PromptBlockDefinitionId == block.Id &&
                item.ParentPromptRunNodeId is null &&
                string.Equals(item.BranchKey, "main", StringComparison.OrdinalIgnoreCase));
            if (node is null)
            {
                node = new PromptRunNode
                {
                    PromptRunId = runId,
                    PromptBlockDefinitionId = block.Id
                };
                await dbContext.Set<PromptRunNode>().AddAsync(node, cancellationToken);
            }

            node.Title = block.Name;
            node.BranchKey = "main";
            node.BranchLabel = "Main";
            node.Sequence = index;
            node.State = PromptRunNodeState.Prepared;
            node.Notes = block.Summary;
        }
    }

    private async Task LinkSessionToPromptAsync(Guid sessionId, Guid promptArtifactId, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await dbContext.Set<PromptBuildSession>().FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.PromptArtifactId = promptArtifactId;
        if (session.PromptRunId.HasValue)
        {
            var node = await dbContext.Set<PromptRunNode>()
                .Where(item => item.PromptRunId == session.PromptRunId.Value)
                .OrderByDescending(item => item.Sequence)
                .FirstOrDefaultAsync(cancellationToken);

            if (node is not null)
            {
                node.PromptArtifactId = promptArtifactId;
                node.State = PromptRunNodeState.Used;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static List<string> BuildWarnings(PromptFactoryEditorModel model, ProjectEditorModel project, IReadOnlyCollection<ResourceSummary> resources)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(model.Phase) && string.IsNullOrWhiteSpace(project.CurrentPhase))
        {
            warnings.Add("No explicit phase is selected. The generated prompt may be too generic.");
        }

        if (resources.Count == 0 && model.SessionAttachments.Count == 0)
        {
            warnings.Add("No project resources or prompt-session inputs were selected. Context assembly will rely on project metadata only.");
        }

        if (resources.Any(resource => resource.Sensitivity != ResourceSensitivity.Normal))
        {
            warnings.Add("Selected resources include sensitive or restricted items. Review outbound provider sends carefully.");
        }

        if (!model.ProviderProfileId.HasValue)
        {
            warnings.Add("No provider is selected. Save/export can continue, but send is blocked.");
        }

        if (string.IsNullOrWhiteSpace(model.RepositoryName))
        {
            warnings.Add("Repository metadata is empty. Usage history will be less traceable.");
        }

        return warnings;
    }

    private static string ComposePrompt(
        PromptFactoryEditorModel model,
        ProjectEditorModel project,
        PromptBlueprintSummary? blueprint,
        IReadOnlyCollection<ResolvedPromptBlock> blocks,
        IReadOnlyCollection<ResourceSummary> resources)
    {
        var phase = string.IsNullOrWhiteSpace(model.Phase) ? project.CurrentPhase : model.Phase;
        var optionLines = project.Options
            .Where(option => !string.IsNullOrWhiteSpace(option.OptionName) || !string.IsNullOrWhiteSpace(option.Notes))
            .Select(option => $"- {option.Category}: {option.OptionName} {option.Notes}".Trim())
            .ToList();
        var resourceLines = resources
            .Select(resource => $"- {resource.ResourceKind}: {resource.Name} ({resource.LocationOrIdentifier})")
            .ToList();
        var attachmentLines = model.SessionAttachments
            .Select(BuildAttachmentLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return $"""
        # Prompt Request
        Blueprint: {blueprint?.Name ?? "General delivery prompt"}
        Prompt type: {blueprint?.PromptType ?? "General"}
        Phase: {phase}
        Project: {project.Name}

        ## Project objective
        {project.Objective}

        ## Project description
        {project.Description}

        ## Guidance
        {blueprint?.Guidance ?? "Produce an implementation-ready output that respects the existing architecture and constraints."}

        ## Shared prompt blocks
        {string.Join(Environment.NewLine + Environment.NewLine, blocks.Select(block => $"### {block.Definition.Name}{Environment.NewLine}{block.RenderedContent}"))}

        ## Stack profile
        {string.Join(Environment.NewLine, optionLines.DefaultIfEmpty("- No stack profile notes selected."))}

        ## Linked resources
        {string.Join(Environment.NewLine, resourceLines.DefaultIfEmpty("- No linked resources selected."))}

        ## Prompt session inputs
        {string.Join(Environment.NewLine, attachmentLines.DefaultIfEmpty("- No prompt-session inputs selected."))}

        ## Requested output
        Produce a phase-aware response that explains the recommended approach, the concrete implementation steps, and the tests or verification needed to close the work safely.
        """;
    }

    private static string SerializeIds(IEnumerable<Guid> ids) => JsonSerializer.Serialize(ids.Distinct().ToArray(), SerializerOptions);

    private static IReadOnlyList<Guid> DeserializeIds(string json)
        => DeserializeJson<List<Guid>>(json);

    private static PromptRunNodeSummary MapRunNodeSummary(PromptRunNode node)
        => new(
            node.Id,
            node.Title,
            node.BranchKey,
            node.BranchLabel,
            node.Sequence,
            node.State,
            node.PromptArtifactId,
            node.ParentPromptRunNodeId,
            node.Notes);

    private static IReadOnlyList<string> SplitTokens(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static string NormalizeTokens(string? value)
        => string.Join(',', SplitTokens(value).Distinct(StringComparer.OrdinalIgnoreCase));

    private static bool RuleMatches(string? rules, params string?[] candidates)
    {
        if (string.IsNullOrWhiteSpace(rules))
        {
            return false;
        }

        var normalizedRules = SplitTokens(rules);
        return candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Any(candidate => normalizedRules.Any(rule => string.Equals(rule, candidate, StringComparison.OrdinalIgnoreCase)));
    }

    private static string BuildSessionName(string projectName, string phase)
        => string.IsNullOrWhiteSpace(phase) ? $"{projectName} prompt session" : $"{projectName} - {phase} prompt session";

    private static string BuildRunName(string phase)
        => string.IsNullOrWhiteSpace(phase) ? "Prompt flow" : $"Prompt flow - {phase}";

    private static string BuildBranchLabel(string title)
        => string.IsNullOrWhiteSpace(title) ? "Follow-up" : $"{title} follow-up";

    private static string BuildBranchKey(string branchLabel)
    {
        var key = branchLabel.Trim().ToLowerInvariant();
        key = new string(key.Where(character => char.IsLetterOrDigit(character) || character == ' ' || character == '-').ToArray());
        key = key.Replace(' ', '-');
        return string.IsNullOrWhiteSpace(key) ? $"branch-{Guid.NewGuid():N}" : key;
    }

    private static string BuildDraftTitle(string phase)
        => string.IsNullOrWhiteSpace(phase) ? "Prompt Factory Draft" : $"Prompt Factory - {phase}";

    private static List<string> SplitWarnings(string warningSummary)
        => warningSummary.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
