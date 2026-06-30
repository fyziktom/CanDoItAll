using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Factory;

public sealed partial class PromptFactoryService
{
    public async Task<Result<PromptFactoryEditorModel>> RestoreSessionStateAsync(
        PromptFactoryEditorModel model,
        CancellationToken cancellationToken = default)
    {
        if (!model.ProjectId.HasValue)
        {
            return Result<PromptFactoryEditorModel>.Failure(Error.Validation("Select a project before restoring a prompt session."));
        }

        await EnsureSeedsAsync(cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var session = model.SessionId.HasValue
            ? await dbContext.Set<PromptBuildSession>().FirstOrDefaultAsync(item => item.Id == model.SessionId.Value, cancellationToken)
            : null;
        if (session is null)
        {
            session = new PromptBuildSession();
            await dbContext.Set<PromptBuildSession>().AddAsync(session, cancellationToken);
        }

        var project = await projectsService.GetAsync(model.ProjectId, cancellationToken);
        var phase = string.IsNullOrWhiteSpace(model.Phase) ? project.CurrentPhase : model.Phase.Trim();

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
        session.SelectedBlockIdsJson = SerializeIds(model.SelectedBlockIds);
        session.SelectedResourceIdsJson = SerializeIds(model.SelectedResourceIds);
        session.GeneratedPrompt = model.GeneratedPrompt ?? string.Empty;
        session.WarningSummary = string.IsNullOrWhiteSpace(model.WarningSummary)
            ? string.Join('\n', model.Warnings)
            : model.WarningSummary;
        session.CanvasUiStateJson = string.IsNullOrWhiteSpace(model.CanvasUiStateJson) ? "{}" : model.CanvasUiStateJson;
        session.ComponentCustomizationsJson = SerializeJson(model.ComponentCustomizations);
        session.SessionAttachmentsJson = SerializeJson(model.SessionAttachments);
        session.WizardStepIndex = model.WizardStepIndex;
        session.HasCustomizedBlocks = model.HasCustomizedBlocks;
        session.SelectedPromptRunNodeId = model.SelectedNodeId;
        session.UpdatedAtUtc = clock.GetUtcNow();

        if (model.Nodes.Count > 0 || model.PromptRunId.HasValue)
        {
            var run = await EnsureRestorableRunAsync(
                dbContext,
                session,
                model.PromptRunId,
                model.ProjectId.Value,
                phase,
                model.FlowTemplateId,
                cancellationToken);
            await RestoreRunNodesAsync(dbContext, run.Id, model.Nodes, cancellationToken);
        }
        else if (session.PromptRunId.HasValue)
        {
            var runNodes = await dbContext.Set<PromptRunNode>()
                .Where(item => item.PromptRunId == session.PromptRunId.Value)
                .ToListAsync(cancellationToken);
            dbContext.RemoveRange(runNodes);

            var run = await dbContext.Set<PromptRun>().FirstOrDefaultAsync(item => item.Id == session.PromptRunId.Value, cancellationToken);
            if (run is not null)
            {
                dbContext.Remove(run);
            }

            session.PromptRunId = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<PromptFactoryEditorModel>.Success(await GetEditorAsync(session.Id, cancellationToken));
    }

    private async Task<PromptRun> EnsureRestorableRunAsync(
        AppDbContext dbContext,
        PromptBuildSession session,
        Guid? promptRunId,
        Guid projectId,
        string phase,
        Guid? flowTemplateId,
        CancellationToken cancellationToken)
    {
        var run = session.PromptRunId.HasValue
            ? await dbContext.Set<PromptRun>().FirstOrDefaultAsync(item => item.Id == session.PromptRunId.Value, cancellationToken)
            : null;
        if (run is null && promptRunId.HasValue)
        {
            run = await dbContext.Set<PromptRun>().FirstOrDefaultAsync(item => item.Id == promptRunId.Value, cancellationToken);
        }

        if (run is null)
        {
            var resolvedFlowTemplateId = flowTemplateId ?? await dbContext.Set<PromptFlowTemplate>().Select(item => item.Id).FirstAsync(cancellationToken);
            run = new PromptRun
            {
                Id = promptRunId ?? Guid.NewGuid(),
                ProjectId = projectId,
                FlowTemplateId = resolvedFlowTemplateId,
                Name = BuildRunName(phase),
                Phase = phase,
                CreatedAtUtc = clock.GetUtcNow(),
                UpdatedAtUtc = clock.GetUtcNow()
            };

            await dbContext.Set<PromptRun>().AddAsync(run, cancellationToken);
        }
        else
        {
            run.ProjectId = projectId;
            run.FlowTemplateId = flowTemplateId ?? run.FlowTemplateId;
            run.Name = BuildRunName(phase);
            run.Phase = phase;
            run.UpdatedAtUtc = clock.GetUtcNow();
        }

        session.PromptRunId = run.Id;
        return run;
    }

    private static async Task RestoreRunNodesAsync(
        AppDbContext dbContext,
        Guid runId,
        IReadOnlyList<PromptRunNodeSummary> desiredNodes,
        CancellationToken cancellationToken)
    {
        var existingNodes = await dbContext.Set<PromptRunNode>()
            .Where(item => item.PromptRunId == runId)
            .ToListAsync(cancellationToken);
        var desiredNodeIds = desiredNodes.Select(item => item.Id).ToHashSet();

        dbContext.RemoveRange(existingNodes.Where(item => !desiredNodeIds.Contains(item.Id)));

        foreach (var desiredNode in desiredNodes.OrderBy(item => item.Sequence))
        {
            var node = existingNodes.FirstOrDefault(item => item.Id == desiredNode.Id);
            if (node is null)
            {
                node = new PromptRunNode
                {
                    Id = desiredNode.Id,
                    PromptRunId = runId
                };

                await dbContext.Set<PromptRunNode>().AddAsync(node, cancellationToken);
                existingNodes.Add(node);
            }

            node.PromptRunId = runId;
            node.Title = desiredNode.Title;
            node.BranchKey = desiredNode.BranchKey;
            node.BranchLabel = desiredNode.BranchLabel;
            node.Sequence = desiredNode.Sequence;
            node.State = desiredNode.State;
            node.PromptArtifactId = desiredNode.PromptArtifactId;
            node.ParentPromptRunNodeId = desiredNode.ParentNodeId;
            node.PromptBlockDefinitionId = desiredNode.PromptBlockDefinitionId;
            node.Notes = desiredNode.Notes ?? string.Empty;
        }
    }
}


