using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Modules.Processes;

internal enum ProcessDispatchTechnicalAgentBindingOutcome
{
    MissingBinding,
    BoundUnchanged,
    ProjectStructureAccessAlreadyPresent,
    ProjectStructureAccessGrantedAndSaved
}

internal sealed record ProcessDispatchTechnicalAgentBindingResult(
    ProcessDispatchTechnicalAgentBindingOutcome Outcome,
    Guid? TechnicalAgentId,
    AiResourceBindingStatus? BindingStatus,
    AgentEditorModel? AgentEditor)
{
    public bool CanDispatch => TechnicalAgentId.HasValue && AgentEditor is not null;
}

internal static class ProcessDispatchTechnicalAgentBindingCoordinator
{
    public static async Task<ProcessDispatchTechnicalAgentBindingResult> ResolveAsync(
        ProcessRun run,
        ProcessStepRun stepRun,
        Guid executorPartyId,
        IAiTechnicalAgentBridge technicalAgentBridge,
        IProcessAutomationExecutionClient executionClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(stepRun);
        ArgumentNullException.ThrowIfNull(technicalAgentBridge);
        ArgumentNullException.ThrowIfNull(executionClient);

        var summaries = await technicalAgentBridge.GetDirectorySummariesAsync([executorPartyId], cancellationToken);
        var hasTechnicalAgentSummary = summaries.TryGetValue(executorPartyId, out var technicalAgentSummary);
        if (!hasTechnicalAgentSummary ||
            technicalAgentSummary is null ||
            !technicalAgentSummary.TechnicalAgentId.HasValue ||
            technicalAgentSummary.BindingStatus != AiResourceBindingStatus.Bound)
        {
            return new ProcessDispatchTechnicalAgentBindingResult(
                ProcessDispatchTechnicalAgentBindingOutcome.MissingBinding,
                technicalAgentSummary?.TechnicalAgentId,
                technicalAgentSummary?.BindingStatus,
                null);
        }

        var agentEditor = await executionClient.GetAgentEditorAsync(technicalAgentSummary.TechnicalAgentId.Value, cancellationToken);
        if (!TryResolveProjectStructureAccessProjectId(run, out var projectStructureAccessProjectId))
        {
            return new ProcessDispatchTechnicalAgentBindingResult(
                ProcessDispatchTechnicalAgentBindingOutcome.BoundUnchanged,
                technicalAgentSummary.TechnicalAgentId.Value,
                technicalAgentSummary.BindingStatus,
                agentEditor);
        }

        if (!ApplyProjectStructureReadAccess(agentEditor, projectStructureAccessProjectId))
        {
            return new ProcessDispatchTechnicalAgentBindingResult(
                ProcessDispatchTechnicalAgentBindingOutcome.ProjectStructureAccessAlreadyPresent,
                technicalAgentSummary.TechnicalAgentId.Value,
                technicalAgentSummary.BindingStatus,
                agentEditor);
        }

        await executionClient.SaveAgentAsync(agentEditor, cancellationToken);
        return new ProcessDispatchTechnicalAgentBindingResult(
            ProcessDispatchTechnicalAgentBindingOutcome.ProjectStructureAccessGrantedAndSaved,
            technicalAgentSummary.TechnicalAgentId.Value,
            technicalAgentSummary.BindingStatus,
            agentEditor);
    }

    public static bool ApplyProjectStructureReadAccess(AgentEditorModel agentEditor, Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(agentEditor);

        if (projectId == Guid.Empty)
        {
            return false;
        }

        var access = AgentProjectStructureAccessMetadata.Normalize(agentEditor.ProjectStructureAccess);
        if (access.CanRead &&
            (access.AllowAllProjects || access.AllowedProjectIds.Contains(projectId)))
        {
            agentEditor.ProjectStructureAccess = access;
            return false;
        }

        access.CanRead = true;
        if (!access.AllowAllProjects &&
            !access.AllowedProjectIds.Contains(projectId))
        {
            access.AllowedProjectIds.Add(projectId);
        }

        agentEditor.ProjectStructureAccess = AgentProjectStructureAccessMetadata.Normalize(access);
        return true;
    }

    public static bool TryResolveProjectStructureAccessProjectId(ProcessRun run, out Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (ProcessProjectStructureContextFormatter.TryParse(run.TriggerReason, out var projectStructureContext) &&
            projectStructureContext is not null &&
            projectStructureContext.ProjectId != Guid.Empty)
        {
            projectId = projectStructureContext.ProjectId;
            return true;
        }

        if (run.ProjectId.HasValue && run.ProjectId.Value != Guid.Empty)
        {
            projectId = run.ProjectId.Value;
            return true;
        }

        projectId = Guid.Empty;
        return false;
    }
}
