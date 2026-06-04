using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessAgentRuntimeToolProvider
{
    private Task<IReadOnlyList<ProcessTemplateCatalogItem>> ProcessesTemplatesListAsync(
        ProcessAccessState accessState,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        return Task.FromResult(templateCatalogService.ListProcessTemplates());
    }

    private Task<InternalProcessTemplateDetailToolData> ProcessesTemplateGetAsync(
        ProcessAccessState accessState,
        string processKey,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        var pack = templatePackLoader.Load();
        if (!pack.Processes.TryGetValue(processKey, out var process))
        {
            throw new ProcessToolException(
                "ProcessTemplateNotFound",
                $"Process template '{processKey}' was not found.");
        }

        var summary = templateCatalogService.ListProcessTemplates()
            .Single(item => string.Equals(item.Key, processKey, StringComparison.OrdinalIgnoreCase));
        var supportingFiles = templateMermaidExporter.Export(processKey).SupportingFiles;

        return Task.FromResult(
            new InternalProcessTemplateDetailToolData(
                summary,
                process,
                templateProjectionService.GetCompatibilityReportMarkdown(processKey),
                supportingFiles));
    }

    private Task<ProcessTemplateMermaidDocument> ProcessesTemplateMermaidGetAsync(
        ProcessAccessState accessState,
        string processKey,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        try
        {
            return Task.FromResult(templateMermaidExporter.Export(processKey));
        }
        catch (InvalidOperationException)
        {
            throw new ProcessToolException(
                "ProcessTemplateNotFound",
                $"Process template '{processKey}' was not found.");
        }
    }

    private async Task<ProcessTemplateImportResult> ProcessesTemplateImportAsync(
        ProcessAccessState accessState,
        InternalProcessTemplateImportRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWriteAllowed(accessState);

        ProcessImportExportEnvelope envelope;
        try
        {
            envelope = templateProjectionService.GetProjectedEnvelope(
                request.ProcessKey,
                request.ProjectId,
                request.DefinitionName);
        }
        catch (InvalidOperationException)
        {
            throw new ProcessToolException(
                "ProcessTemplateNotFound",
                $"Process template '{request.ProcessKey}' was not found.");
        }

        var definitionId = EnsureSuccess(await processesService.ImportAsync(envelope, cancellationToken));
        if (request.AutoPublish)
        {
            EnsureSuccess(await processesService.PublishAsync(definitionId, cancellationToken));
        }

        GrantDefinitionAccess(accessState, definitionId);
        return new ProcessTemplateImportResult(request.ProcessKey, definitionId, envelope.Warnings);
    }

    private Task<IReadOnlyList<ProcessTemplateBaselineScenarioSummary>> ProcessesTemplateBaselineScenariosListAsync(
        ProcessAccessState accessState,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        var pack = templatePackLoader.Load();
        return Task.FromResult<IReadOnlyList<ProcessTemplateBaselineScenarioSummary>>(
            pack.BaselineScenarios
                .Select(item => new ProcessTemplateBaselineScenarioSummary(
                    item.Key,
                    item.ProcessTemplateKey,
                    item.RunName,
                    item.OperatingMode,
                    item.Assignments.Count,
                    item.Transitions.Count,
                    item.Artifacts.Count,
                    item.Transitions.Count(transition => !string.IsNullOrWhiteSpace(transition.SelectedBranchOutcomeKey)),
                    item.Transitions.Count(transition => string.Equals(transition.TargetStatus, ProcessStepRunStatus.Blocked.ToString(), StringComparison.OrdinalIgnoreCase)),
                    item.ContractExercises.Count,
                    item.RecoveryExercises.Count))
                .ToList());
    }

    private Task<IReadOnlyList<ProcessTemplateLiveRunProfileSummary>> ProcessesTemplateLiveRunProfilesListAsync(
        ProcessAccessState accessState,
        CancellationToken cancellationToken)
    {
        EnsureReadAllowed(accessState);
        var pack = templatePackLoader.Load();
        return Task.FromResult<IReadOnlyList<ProcessTemplateLiveRunProfileSummary>>(
            pack.LiveRunProfiles
                .Select(item => new ProcessTemplateLiveRunProfileSummary(
                    item.Key,
                    item.ProcessTemplateKey,
                    item.RunNameTemplate,
                    item.Summary,
                    item.OperatingMode,
                    item.TriggerReasonTemplate,
                    item.FreshRunPolicy,
                    item.Assignments.Count,
                    item.AcceptanceCriteria.Count,
                    item.RequiredProofKinds.Count))
                .ToList());
    }
}
