using System.Text.Json;
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
summary: Builds prompt sessions from centralized blueprints, flow templates, shared blocks, project context, and provider settings.
owns: prompt-build-sessions, prompt-run nodes, prompt export/send flows
deps: AppDbContext, ProjectsService, ResourcesService, WorkspaceService, ProviderExecutionService, PromptsService, IManagedArtifactStore, IBackgroundJobTracker
risks: missing-provider, stale-resource-selection, weak-defaults
tests: unit:PromptFactoryServiceTests, integration:PromptFactoryPersistenceTests
inputs: PromptFactoryEditorModel
outputs: generated prompt text, saved prompt ids, provider responses
*/
public sealed class PromptFactoryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ProjectsService projectsService,
    ResourcesService resourcesService,
    WorkspaceService workspaceService,
    ProviderExecutionService providerExecutionService,
    PromptsService promptsService,
    IManagedArtifactStore managedArtifactStore,
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
            BlueprintId = defaults.BlueprintId,
            FlowTemplateId = defaults.FlowTemplateId,
            ProviderProfileId = settings.DefaultProviderProfileId,
            SelectedBlockIds = defaults.BlockIds.ToList()
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
            Nodes = session.PromptRunId.HasValue ? (await LoadRunNodesAsync(session.PromptRunId.Value, cancellationToken)).ToList() : []
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
            ProjectId = run.ProjectId,
            Phase = run.Phase,
            FlowTemplateId = run.FlowTemplateId,
            SelectedBlockIds = nodes
                .Where(item => item.PromptBlockDefinitionId.HasValue)
                .Select(item => item.PromptBlockDefinitionId!.Value)
                .Distinct()
                .ToList(),
            Nodes = nodes
                .Select(item => new PromptRunNodeSummary(item.Id, item.Title, item.BranchKey, item.Sequence, item.State, item.PromptArtifactId))
                .ToList()
        };
    }

    public async Task<IReadOnlyList<PromptBlockSummary>> ListBlocksAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeedsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PromptBlockDefinition>()
            .OrderBy(item => item.BlockKind)
            .ThenBy(item => item.Name)
            .Select(item => new PromptBlockSummary(item.Id, item.Name, item.BlockKind, item.Summary, item.IsRecommendedByDefault))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PromptFlowTemplateSummary>> ListTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeedsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var templates = await dbContext.Set<PromptFlowTemplate>().OrderBy(item => item.Name).ToListAsync(cancellationToken);
        return templates.Select(item => new PromptFlowTemplateSummary(item.Id, item.Name, item.Summary, DeserializeIds(item.BlockIdsJson))).ToList();
    }

    public async Task<IReadOnlyList<PromptBlueprintSummary>> ListBlueprintsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeedsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PromptBlueprint>()
            .OrderBy(item => item.Name)
            .Select(item => new PromptBlueprintSummary(item.Id, item.Name, item.PromptType, item.Summary, item.Guidance, item.RecommendedFlowTemplateId))
            .ToListAsync(cancellationToken);
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

        var blocks = await LoadBlocksAsync(model.SelectedBlockIds, model.FlowTemplateId, cancellationToken);
        var blueprint = (await ListBlueprintsAsync(cancellationToken)).FirstOrDefault(item => item.Id == model.BlueprintId);
        var warnings = BuildWarnings(model, project, selectedResources);
        var prompt = ComposePrompt(model, project, blueprint, blocks, selectedResources);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = model.SessionId.HasValue
            ? await dbContext.Set<PromptBuildSession>().FirstOrDefaultAsync(item => item.Id == model.SessionId.Value, cancellationToken)
            : null;

        if (session is null)
        {
            session = new PromptBuildSession();
            await dbContext.Set<PromptBuildSession>().AddAsync(session, cancellationToken);
        }

        session.ProjectId = model.ProjectId;
        session.Phase = string.IsNullOrWhiteSpace(model.Phase) ? project.CurrentPhase : model.Phase.Trim();
        session.BlueprintId = model.BlueprintId;
        session.FlowTemplateId = model.FlowTemplateId;
        session.ProviderProfileId = model.ProviderProfileId;
        session.RepositoryName = model.RepositoryName?.Trim() ?? string.Empty;
        session.BranchName = model.BranchName?.Trim() ?? string.Empty;
        session.CommitSha = model.CommitSha?.Trim() ?? string.Empty;
        session.SelectedBlockIdsJson = SerializeIds(blocks.Select(item => item.Id));
        session.SelectedResourceIdsJson = SerializeIds(selectedResources.Select(item => item.Id));
        session.GeneratedPrompt = prompt;
        session.WarningSummary = string.Join('\n', warnings);
        session.UpdatedAtUtc = clock.GetUtcNow();

        if (!session.PromptRunId.HasValue)
        {
            session.PromptRunId = await EnsureRunAsync(dbContext, model.ProjectId.Value, session.Phase, model.FlowTemplateId, blocks, cancellationToken);
        }
        else
        {
            await SyncRunNodesAsync(dbContext, session.PromptRunId.Value, blocks, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await searchIndexService.UpsertAsync(new SearchDocumentInput(
            "prompt-session",
            session.Id.ToString(),
            "Prompt Factory",
            BuildDraftTitle(session.Phase),
            string.Join(" ", warnings),
            prompt,
            $"/prompt-factory?sessionId={session.Id}",
            session.ProjectId), cancellationToken);
        await activityStream.RecordAsync(new ActivityWriteRequest(
            "factory",
            "build",
            "Built prompt session",
            BuildDraftTitle(session.Phase),
            ProjectId: session.ProjectId,
            ArtifactKind: "prompt-session",
            ArtifactId: session.Id,
            Route: $"/prompt-factory?sessionId={session.Id}"), cancellationToken);

        var updated = await GetEditorAsync(session.Id, cancellationToken);
        updated.GeneratedPrompt = prompt;
        updated.WarningSummary = string.Join('\n', warnings);
        updated.Warnings = warnings;
        updated.DraftTitle = BuildDraftTitle(session.Phase);
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
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<PromptRunNode>()
            .Where(item => item.PromptRunId == promptRunId)
            .OrderBy(item => item.BranchKey)
            .ThenBy(item => item.Sequence)
            .Select(item => new PromptRunNodeSummary(item.Id, item.Title, item.BranchKey, item.Sequence, item.State, item.PromptArtifactId))
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureSeedsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Set<PromptBlockDefinition>().AnyAsync(cancellationToken))
        {
            return;
        }

        var blocks = new[]
        {
            new PromptBlockDefinition { Name = "Delivery Constraints", BlockKind = PromptBlockKind.Constraint, Summary = "Keep scope disciplined and user-visible.", Content = "Stay inside the requested scope. Prefer typed, testable changes. Preserve module boundaries.", IsRecommendedByDefault = true },
            new PromptBlockDefinition { Name = "Architecture Review", BlockKind = PromptBlockKind.Validation, Summary = "Call out architecture tradeoffs and risks.", Content = "Describe architecture choices, dependencies, and migration risks before implementation detail.", IsRecommendedByDefault = true },
            new PromptBlockDefinition { Name = "Security Checks", BlockKind = PromptBlockKind.Security, Summary = "Protect secrets and outbound data.", Content = "Do not expose secrets. Highlight approvals, redaction needs, and sensitive egress paths.", IsRecommendedByDefault = true },
            new PromptBlockDefinition { Name = "Testing Expectations", BlockKind = PromptBlockKind.Testing, Summary = "Demand evidence and coverage.", Content = "Include tests, expected verification, and evidence that should prove the change works.", IsRecommendedByDefault = true },
            new PromptBlockDefinition { Name = "Implementation Detail", BlockKind = PromptBlockKind.Delivery, Summary = "Turn the plan into code changes.", Content = "Produce concrete implementation steps, affected files, and follow-up validation guidance.", IsRecommendedByDefault = true }
        };

        await dbContext.Set<PromptBlockDefinition>().AddRangeAsync(blocks, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var defaultTemplate = new PromptFlowTemplate
        {
            Name = "Implementation flow",
            Summary = "Architecture to implementation to validation.",
            BlockIdsJson = SerializeIds(blocks.Select(item => item.Id))
        };

        await dbContext.Set<PromptFlowTemplate>().AddAsync(defaultTemplate, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Set<PromptBlueprint>().AddRangeAsync(
            [
                new PromptBlueprint
                {
                    Name = "Architecture Definition",
                    PromptType = "Architecture",
                    Summary = "Define the implementation architecture for the current project phase.",
                    Guidance = "Focus on modules, persistence, external integrations, and concrete slice sequencing.",
                    RecommendedFlowTemplateId = defaultTemplate.Id
                },
                new PromptBlueprint
                {
                    Name = "Implementation Plan",
                    PromptType = "Plan",
                    Summary = "Create an implementation plan for the current phase.",
                    Guidance = "Break the work into milestones, dependencies, acceptance criteria, and validation steps.",
                    RecommendedFlowTemplateId = defaultTemplate.Id
                },
                new PromptBlueprint
                {
                    Name = "Validation Follow-Up",
                    PromptType = "Validation",
                    Summary = "Respond to findings and produce follow-up actions.",
                    Guidance = "Explain the finding, proposed code changes, test impact, and evidence expectations.",
                    RecommendedFlowTemplateId = defaultTemplate.Id
                }
            ],
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<(Guid? BlueprintId, Guid? FlowTemplateId, IReadOnlyList<Guid> BlockIds)> GetSeedDefaultsAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var blueprint = await dbContext.Set<PromptBlueprint>().OrderBy(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        var template = await dbContext.Set<PromptFlowTemplate>().OrderBy(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        var blocks = await dbContext.Set<PromptBlockDefinition>().Where(item => item.IsRecommendedByDefault).Select(item => item.Id).ToListAsync(cancellationToken);
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

        return blocks.Where(item => ids.Contains(item.Id)).OrderBy(item => item.BlockKind).ThenBy(item => item.Name).ToList();
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
            Name = $"Prompt flow · {phase}",
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
            var node = nodes.FirstOrDefault(item => item.PromptBlockDefinitionId == block.Id);
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

        if (resources.Count == 0)
        {
            warnings.Add("No project resources were selected. Context assembly will rely on project metadata only.");
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
        IReadOnlyCollection<PromptBlockDefinition> blocks,
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
        {string.Join(Environment.NewLine + Environment.NewLine, blocks.Select(block => $"### {block.Name}{Environment.NewLine}{block.Content}"))}

        ## Stack profile
        {string.Join(Environment.NewLine, optionLines.DefaultIfEmpty("- No stack profile notes selected."))}

        ## Linked resources
        {string.Join(Environment.NewLine, resourceLines.DefaultIfEmpty("- No linked resources selected."))}

        ## Requested output
        Produce a phase-aware response that explains the recommended approach, the concrete implementation steps, and the tests or verification needed to close the work safely.
        """;
    }

    private static string SerializeIds(IEnumerable<Guid> ids) => JsonSerializer.Serialize(ids.Distinct().ToArray());

    private static IReadOnlyList<Guid> DeserializeIds(string json)
        => JsonSerializer.Deserialize<List<Guid>>(string.IsNullOrWhiteSpace(json) ? "[]" : json) ?? [];

    private static string BuildDraftTitle(string phase)
        => string.IsNullOrWhiteSpace(phase) ? "Prompt Factory Draft" : $"Prompt Factory · {phase}";

    private static List<string> SplitWarnings(string warningSummary)
        => warningSummary.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
