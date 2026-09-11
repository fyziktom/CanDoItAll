using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkbenchCommandService(
    IDbContextFactory<WorkbenchDbContext> dbContextFactory,
    ProjectStructureMutationScopeFactory mutationScopes,
    IClock clock,
    IPromptGalleryMutationService promptGalleryService,
    IPromptArtifactProjectionQueryService prompts,
    ProjectRecordQueryService projects,
    CoordinatedDatabaseTransaction transactions,
    ProjectStructureAssemblyService projectStructureAssemblyService)
{
    public async Task<ArtifactReference?> ExecuteNodeCommandAsync(
        Guid projectId,
        string nodeKey,
        ProjectStructureCommandKind commandKind,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ProjectWorkbenchSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        await using var mutationScope =
            await mutationScopes.BeginBindingWriteAsync(
                dbContext,
                ProjectStructureSerializableMutationScope.ForProject(projectId),
                cancellationToken);
        var node = await projectStructureAssemblyService.FindNodeAsync(dbContext, projectId, nodeKey, cancellationToken);
        if (node is null)
        {
            return null;
        }

        if (IsPromptObject(node.ObjectType) &&
            commandKind is ProjectStructureCommandKind.Open or ProjectStructureCommandKind.Wizard)
        {
            var prepared = await EnsurePromptGalleryArtifactAsync(dbContext, projectId, node, cancellationToken);
            if (!node.IsSystemManaged)
            {
                await ProjectNodeBindingStorage.PersistAsync(dbContext, node, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await mutationScope.CommitAsync(cancellationToken);
            await mutationScope.DisposeAsync();
            await CompletePromptCreationAsync(prepared, cancellationToken);
            return prepared.Artifact;
        }

        await mutationScope.CommitAsync(cancellationToken);

        return commandKind switch
        {
            ProjectStructureCommandKind.Test => new ArtifactReference("test-plan", null, "Test Lab", $"/test-lab?projectId={projectId}", "Project test planning workspace", projectId),
            ProjectStructureCommandKind.Open => BuildArtifactReference(node, projectId),
            _ => BuildArtifactReference(node, projectId)
        };
    }

    internal async Task<ProjectPromptBindingPreparation> EnsurePromptGalleryArtifactAsync(
        WorkbenchDbContext dbContext,
        Guid projectId,
        ProjectObjectRecord node,
        CancellationToken cancellationToken)
    {
        using var coordination = transactions.Enter(dbContext);
        var binding = ProjectNodeBindingStorage.ResolveForRuntime(node);
        if (string.Equals(binding.ExternalArtifactKind, "prompt", StringComparison.OrdinalIgnoreCase) &&
            binding.ExternalArtifactId.HasValue)
        {
            var prompt = await prompts.GetBindingFactForMutationAsync(binding.ExternalArtifactId.Value, cancellationToken);
            if (prompt is null)
            {
                throw new InvalidOperationException(
                    $"Project prompt node '{node.NodeKey}' references missing Gallery prompt '{binding.ExternalArtifactId.Value}'.");
            }

            if (prompt.ProjectId != projectId)
            {
                throw new InvalidOperationException(
                    $"Project prompt node '{node.NodeKey}' references Gallery prompt '{binding.ExternalArtifactId.Value}' owned by another project.");
            }

            if (prompt.Kind != PromptGalleryItemKind.FullPrompt)
            {
                throw new InvalidOperationException(
                    $"Project prompt node '{node.NodeKey}' references Gallery item '{binding.ExternalArtifactId.Value}' with kind '{prompt.Kind}' instead of a full prompt.");
            }

            ApplyPromptArtifactBinding(node, binding.ExternalArtifactId.Value);
            node.UpdatedAtUtc = clock.GetUtcNow();
            return new(BuildArtifactReference(node, projectId), null);
        }

        var phase = await ResolvePromptPhaseAsync(dbContext, projectId, node, cancellationToken);
        var saveResult = await promptGalleryService.StageCreateDraftAsync(
            new PromptGalleryDraft(
                Id: null,
                ProjectId: projectId,
                CollectionId: null,
                Title: node.Title,
                Summary: node.Notes,
                Kind: PromptGalleryItemKind.FullPrompt,
                Phase: phase,
                Content: string.Empty,
                SupportedConsumers: [PromptGalleryConsumer.ProjectWorkbench]),
            cancellationToken);
        if (saveResult.IsFailure || saveResult.Value is null)
        {
            throw new InvalidOperationException(
                $"Could not create a Gallery prompt for project node '{node.NodeKey}': {string.Join(" ", saveResult.Errors.Select(error => error.Message))}");
        }

        ApplyPromptArtifactBinding(node, saveResult.Value.Receipt.PromptArtifactId);
        node.UpdatedAtUtc = clock.GetUtcNow();
        return new(BuildArtifactReference(node, projectId), saveResult.Value);
    }

    internal Task CompletePromptCreationAsync(ProjectPromptBindingPreparation preparation, CancellationToken cancellationToken) =>
        preparation.Creation is { } creation
            ? promptGalleryService.CompleteDraftCreationAsync(creation, cancellationToken)
            : Task.CompletedTask;

    private static void ApplyPromptArtifactBinding(ProjectObjectRecord node, Guid promptArtifactId)
    {
        node.Binding = node.Binding with
        {
            Route = $"/prompt-gallery?promptId={promptArtifactId}",
            ExternalArtifactKind = "prompt",
            ExternalArtifactId = promptArtifactId
        };
    }

    private static ArtifactReference? BuildArtifactReference(ProjectObjectRecord node, Guid projectId)
    {
        var binding = ProjectNodeBindingStorage.ResolveForRuntime(node);
        if (string.IsNullOrWhiteSpace(binding.Route))
        {
            return null;
        }

        var tabKind = node.ObjectType switch
        {
            ProjectObjectType.ProjectRoot => WorkbenchTabKinds.ProjectOverview,
            ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession or ProjectObjectType.PromptStep => WorkbenchTabKinds.PromptDetail,
            ProjectObjectType.ProcessDefinition or ProjectObjectType.ProcessRun => WorkbenchTabKinds.Processes,
            ProjectObjectType.WorkflowDefinition or ProjectObjectType.WorkflowRun => WorkbenchTabKinds.Workflows,
            ProjectObjectType.ValidationRun => WorkbenchTabKinds.ValidationRun,
            ProjectObjectType.TestPlan or ProjectObjectType.TestEvidence => WorkbenchTabKinds.TestPlan,
            _ when binding.Route.EndsWith("/structure", StringComparison.OrdinalIgnoreCase) => WorkbenchTabKinds.ProjectStructure,
            _ when binding.Route.EndsWith("/calendar", StringComparison.OrdinalIgnoreCase) => WorkbenchTabKinds.ProjectCalendar,
            _ => WorkbenchTabKinds.Page
        };

        return new ArtifactReference(
            binding.ExternalArtifactKind,
            binding.ExternalArtifactId,
            node.Title,
            binding.Route,
            node.Notes,
            projectId,
            node.NodeKey,
            TabKind: tabKind);
    }

    private static bool IsPromptObject(ProjectObjectType objectType)
        => objectType is ProjectObjectType.PromptFlow or ProjectObjectType.PromptSession or ProjectObjectType.PromptStep;

    private async Task<string> ResolvePromptPhaseAsync(
        WorkbenchDbContext dbContext,
        Guid projectId,
        ProjectObjectRecord node,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(node.ParentNodeKey))
        {
            var records = await dbContext.Set<ProjectObjectRecord>()
                .Where(item => item.ProjectId == projectId)
                .ToDictionaryAsync(item => item.NodeKey, cancellationToken);
            var currentParentKey = node.ParentNodeKey;
            var visited = new HashSet<string>(StringComparer.Ordinal);

            while (!string.IsNullOrWhiteSpace(currentParentKey) &&
                   visited.Add(currentParentKey) &&
                   records.TryGetValue(currentParentKey, out var parentNode))
            {
                if (parentNode.ObjectType == ProjectObjectType.Phase &&
                    !string.IsNullOrWhiteSpace(parentNode.Title))
                {
                    return parentNode.Title.Trim();
                }

                currentParentKey = parentNode.ParentNodeKey;
            }
        }

        return (await projects.GetForMutationAsync(projectId, cancellationToken))?.CurrentPhase.Trim() ?? string.Empty;
    }
}

internal sealed record ProjectPromptBindingPreparation(ArtifactReference? Artifact, PromptDraftCreationPreparation? Creation);
