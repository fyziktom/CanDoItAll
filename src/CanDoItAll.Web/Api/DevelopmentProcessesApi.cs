using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class DevelopmentProcessesApi
{
    public static RouteGroupBuilder MapDevelopmentProcessesApi(this RouteGroupBuilder group)
    {
        var processes = group.MapGroup("/processes")
            .WithTags("Development Processes");

        processes.MapGet("/definitions", async (
                Guid? projectId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.ListDefinitionsAsync(projectId, cancellationToken)))
            .WithName("ListDevelopmentProcessDefinitions");

        processes.MapGet("/definitions/{definitionId:guid}", async (
                Guid definitionId,
                Guid? projectId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.GetEditorAsync(definitionId, projectId, cancellationToken)))
            .WithName("GetDevelopmentProcessDefinitionEditor");

        processes.MapPost("/definitions", async (
                ProcessDefinitionEditorModel request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.SaveAsync(request, cancellationToken)))
            .WithName("SaveDevelopmentProcessDefinition");

        processes.MapPost("/definitions/{definitionId:guid}/publish", async (
                Guid definitionId,
                Guid? definitionConcurrencyToken,
                Guid? draftVersionConcurrencyToken,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.PublishAsync(
                new ProcessDefinitionPublishRequest
                {
                    DefinitionId = definitionId,
                    DefinitionConcurrencyToken = definitionConcurrencyToken,
                    DraftVersionConcurrencyToken = draftVersionConcurrencyToken
                },
                cancellationToken)))
            .WithName("PublishDevelopmentProcessDefinition");

        processes.MapDelete("/definitions/{definitionId:guid}", async (
                Guid definitionId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                await processesService.DeleteAsync(definitionId, cancellationToken);
                return Results.Ok(new DevelopmentApiAck(true));
            })
            .WithName("DeleteDevelopmentProcessDefinition");

        processes.MapGet("/definitions/{definitionId:guid}/export", async (
                Guid definitionId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.ExportAsync(definitionId, cancellationToken)))
            .WithName("ExportDevelopmentProcessDefinition");

        processes.MapPost("/definitions/import", async (
                ProcessImportExportEnvelope request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.ImportAsync(request, cancellationToken)))
            .WithName("ImportDevelopmentProcessDefinition");

        processes.MapGet("/runs", async (
                [AsParameters] ProcessRunListApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                var runs = await processesService.ListRunsAsync(query.DefinitionId, query.ProjectId, cancellationToken);
                return Results.Ok(FilterRuns(runs, query));
            })
            .WithName("ListDevelopmentProcessRuns");

        processes.MapGet("/runs/{runId:guid}", async (
                Guid runId,
                [AsParameters] ProcessRunDetailApiQuery query,
                ProcessesService processesService,
                ProcessWorkspaceRunDetailsLoader runDetailsLoader,
                CancellationToken cancellationToken) =>
            {
                var run = await processesService.GetRunAsync(runId, cancellationToken);
                if (run is null)
                {
                    return DevelopmentApiEndpointResults.NotFound("Process run was not found.", "processes.run-not-found");
                }

                var details = await runDetailsLoader.LoadAsync(runId, cancellationToken);
                return Results.Ok(BuildFilteredRunDetail(run, details, query));
            })
            .WithName("GetDevelopmentProcessRunDetail");

        processes.MapGet("/runs/{runId:guid}/steps", async (
                Guid runId,
                [AsParameters] ProcessRunDetailApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(FilterStepRuns(await processesService.ListStepRunsAsync(runId, cancellationToken), query)))
            .WithName("ListDevelopmentProcessRunSteps");

        processes.MapGet("/runs/{runId:guid}/artifacts", async (
                Guid runId,
                [AsParameters] ProcessRunDetailApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(FilterArtifacts(await processesService.ListArtifactsAsync(runId, cancellationToken), query)))
            .WithName("ListDevelopmentProcessRunArtifacts");

        processes.MapGet("/runs/{runId:guid}/assignments", async (
                Guid runId,
                [AsParameters] ProcessRunDetailApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(FilterAssignments(await processesService.ListAssignmentsAsync(runId, cancellationToken), query)))
            .WithName("ListDevelopmentProcessRunAssignments");

        processes.MapGet("/analytics", async (
                Guid? definitionId,
                Guid? projectId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.GetAnalyticsAsync(definitionId, projectId, cancellationToken)))
            .WithName("GetDevelopmentProcessAnalytics");

        processes.MapPost("/runs/start", async (
                ProcessRunStartRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.StartRunAsync(request, cancellationToken)))
            .WithName("StartDevelopmentProcessRun");

        processes.MapPost("/runs/stop", async (
                ProcessRunStopRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.StopBlockedRunAsync(request, cancellationToken)))
            .WithName("StopDevelopmentProcessRun");

        processes.MapPost("/runs/manager-directives", async (
                ProcessManagerDirectiveRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.RecordManagerDirectiveAsync(request, cancellationToken)))
            .WithName("RecordDevelopmentProcessManagerDirective");

        processes.MapPost("/steps/transition", async (
                ProcessStepTransitionRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.TransitionStepAsync(request, cancellationToken)))
            .WithName("TransitionDevelopmentProcessStep");

        processes.MapPost("/steps/rerun-agent", async (
                ProcessAgentStepRerunRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.RerunAgentStepAsync(request, cancellationToken)))
            .WithName("RerunDevelopmentProcessAgentStep");

        processes.MapPost("/assignments/resolve", async (
                ProcessAssignmentResolutionRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.ResolveAssignmentAsync(request, cancellationToken)))
            .WithName("ResolveDevelopmentProcessAssignment");

        processes.MapPost("/artifacts", async (
                ProcessArtifactRecordRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.RecordArtifactAsync(request, cancellationToken)))
            .WithName("RecordDevelopmentProcessArtifact");

        processes.MapPost("/direct-messages", async (
                ProcessDirectMessageRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.SendDirectMessageAsync(request, cancellationToken)))
            .WithName("SendDevelopmentProcessDirectMessage");

        MapLaunchPlanEndpoints(processes);
        MapTemplateEndpoints(processes);
        MapRegistryEndpoints(processes);

        return group;
    }

    private static void MapLaunchPlanEndpoints(RouteGroupBuilder processes)
    {
        processes.MapGet("/launch-plans", async (
                Guid? definitionId,
                Guid? projectId,
                ProcessLaunchPlanStatus? status,
                int? take,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                var launchPlans = await processesService.ListLaunchPlansAsync(definitionId, projectId, cancellationToken);
                if (status.HasValue)
                {
                    launchPlans = launchPlans
                        .Where(item => item.Status == status.Value)
                        .ToList();
                }

                return Results.Ok(launchPlans
                    .OrderByDescending(item => item.UpdatedAtUtc)
                    .Take(NormalizeTake(take))
                    .ToList());
            })
            .WithName("ListDevelopmentProcessLaunchPlans");

        processes.MapGet("/launch-plans/{launchPlanId:guid}", async (
                Guid launchPlanId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
        {
            var launchPlan = await processesService.GetLaunchPlanAsync(launchPlanId, cancellationToken);
            return launchPlan is null
                ? DevelopmentApiEndpointResults.NotFound("Launch plan was not found.", "processes.launch.not-found")
                : Results.Ok(launchPlan);
        })
        .WithName("GetDevelopmentProcessLaunchPlan");

        processes.MapPost("/launch-plans", async (
                ProcessLaunchCreateRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.CreateLaunchPlanAsync(request, cancellationToken)))
            .WithName("CreateDevelopmentProcessLaunchPlan");

        processes.MapPost("/launch-plans/{launchPlanId:guid}/hr-match", async (
                Guid launchPlanId,
                string? requestedBy,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.MatchLaunchPlanWithHrManagerAsync(
                launchPlanId,
                NormalizeActor(requestedBy),
                cancellationToken)))
            .WithName("MatchDevelopmentProcessLaunchPlanWithHr");

        processes.MapPost("/launch-plans/{launchPlanId:guid}/submit-approval", async (
                Guid launchPlanId,
                string? requestedBy,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.SubmitLaunchPlanForApprovalAsync(
                launchPlanId,
                NormalizeActor(requestedBy),
                cancellationToken)))
            .WithName("SubmitDevelopmentProcessLaunchPlanApproval");

        processes.MapPost("/launch-plans/approval-decisions", async (
                ProcessLaunchApprovalDecisionRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.DecideLaunchPlanApprovalAsync(request, cancellationToken)))
            .WithName("DecideDevelopmentProcessLaunchPlanApproval");

        processes.MapPost("/launch-plans/{launchPlanId:guid}/provision", async (
                Guid launchPlanId,
                string? requestedBy,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.ProvisionLaunchPlanAsync(
                launchPlanId,
                NormalizeActor(requestedBy),
                cancellationToken)))
            .WithName("ProvisionDevelopmentProcessLaunchPlan");

        processes.MapPost("/launch-plans/execute", async (
                ProcessLaunchExecutionRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.ExecuteLaunchPlanAsync(request, cancellationToken)))
            .WithName("ExecuteDevelopmentProcessLaunchPlan");

        processes.MapPost("/launch-plans/candidate-selections", async (
                ProcessLaunchCandidateSelectionRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await processesService.SelectLaunchCandidateAsync(request, cancellationToken)))
            .WithName("SelectDevelopmentProcessLaunchCandidate");
    }

    private static void MapTemplateEndpoints(RouteGroupBuilder processes)
    {
        processes.MapGet("/templates", (
                ProcessTemplateCatalogService catalogService) =>
            Results.Ok(catalogService.ListProcessTemplates()))
            .WithName("ListDevelopmentProcessTemplates");

        processes.MapGet("/templates/{processKey}", (
                string processKey,
                ProcessTemplatePackLoader packLoader) =>
        {
            var pack = packLoader.Load();
            return pack.Processes.TryGetValue(processKey, out var template)
                ? Results.Ok(template)
                : DevelopmentApiEndpointResults.NotFound($"Process template '{processKey}' was not found.", "processes.template-not-found");
        })
        .WithName("GetDevelopmentProcessTemplate");

        processes.MapGet("/templates/{processKey}/envelope", (
                string processKey,
                Guid? projectId,
                string? definitionName,
                ProcessTemplateProjectionService projectionService) =>
        {
            try
            {
                return Results.Ok(projectionService.GetProjectedEnvelope(processKey, projectId, definitionName));
            }
            catch (InvalidOperationException exception)
            {
                return DevelopmentApiEndpointResults.BadRequest(exception.Message, "processes.template-projection-failed");
            }
        })
        .WithName("ProjectDevelopmentProcessTemplateEnvelope");

        processes.MapGet("/templates/{processKey}/mermaid", (
                string processKey,
                ProcessTemplateMermaidExporter mermaidExporter) =>
        {
            try
            {
                return Results.Ok(mermaidExporter.Export(processKey));
            }
            catch (InvalidOperationException exception)
            {
                return DevelopmentApiEndpointResults.BadRequest(exception.Message, "processes.template-mermaid-failed");
            }
        })
        .WithName("ExportDevelopmentProcessTemplateMermaid");

        processes.MapPost("/templates/{processKey}/import", async (
                string processKey,
                ProcessTemplateImportApiRequest request,
                ProcessTemplateProjectionService projectionService,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
        {
            try
            {
                var envelope = projectionService.GetProjectedEnvelope(
                    processKey,
                    request.ProjectId,
                    request.DefinitionName);
                return DevelopmentApiEndpointResults.FromResult(await processesService.ImportAsync(envelope, cancellationToken));
            }
            catch (InvalidOperationException exception)
            {
                return DevelopmentApiEndpointResults.BadRequest(exception.Message, "processes.template-import-failed");
            }
        })
        .WithName("ImportDevelopmentProcessTemplate");
    }

    private static void MapRegistryEndpoints(RouteGroupBuilder processes)
    {
        processes.MapGet("/executor-options", async (
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.ListExecutorOptionsAsync(cancellationToken)))
            .WithName("ListDevelopmentProcessExecutorOptions");

        processes.MapGet("/manager-agent-options", async (
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.ListManagerAgentOptionsAsync(cancellationToken)))
            .WithName("ListDevelopmentProcessManagerAgentOptions");

        processes.MapGet("/party-options/{projectId:guid}", async (
                Guid projectId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.ListPartyOptionsAsync(projectId, cancellationToken)))
            .WithName("ListDevelopmentProcessPartyOptions");
    }

    private static IReadOnlyList<ProcessRunListItem> FilterRuns(
        IReadOnlyList<ProcessRunListItem> runs,
        ProcessRunListApiQuery query)
    {
        var filtered = runs.AsEnumerable();
        if (query.Status.HasValue)
        {
            filtered = filtered.Where(item => item.Status == query.Status.Value);
        }

        if (query.OperatingMode.HasValue)
        {
            filtered = filtered.Where(item => item.OperatingMode == query.OperatingMode.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(item => Contains(item.Name, query.Search) ||
                                             Contains(item.ManagerAgentName, query.Search));
        }

        return filtered
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(NormalizeTake(query.Take))
            .ToList();
    }

    private static DevelopmentProcessRunDetail BuildFilteredRunDetail(
        ProcessRunListItem run,
        ProcessWorkspaceRunDetails details,
        ProcessRunDetailApiQuery query)
    {
        return new DevelopmentProcessRunDetail(
            run,
            FilterStepRuns(details.StepRuns, query),
            ShouldInclude(query.IncludeDecisions) ? details.Decisions : [],
            ShouldInclude(query.IncludeArtifacts) ? FilterArtifacts(details.Artifacts, query) : [],
            ShouldInclude(query.IncludeOutboxRecords) ? FilterOutboxRecords(details.OutboxRecords, query) : [],
            ShouldInclude(query.IncludeAssignments) ? FilterAssignments(details.Assignments, query) : [],
            ShouldInclude(query.IncludeWorkBriefs) ? FilterWorkBriefs(details.WorkBriefs, query) : [],
            ShouldInclude(query.IncludeConformanceObservations) ? FilterConformanceObservations(details.ConformanceObservations, query) : [],
            ShouldInclude(query.IncludeDirectMessages) ? FilterDirectMessageThreads(details.DirectMessageThreads, query) : [],
            ShouldInclude(query.IncludeExecutionRuns) ? FilterExecutionRuns(details.ExecutionRuns, query) : [],
            ShouldInclude(query.IncludeEscalations) ? details.Escalations : [],
            ShouldInclude(query.IncludeOperatorApprovals) ? details.OperatorApprovals : [],
            ShouldInclude(query.IncludeAttemptTimeline) ? details.AttemptTimeline : [],
            details.Health);
    }

    private static IReadOnlyList<ProcessStepRunViewModel> FilterStepRuns(
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        ProcessRunDetailApiQuery query)
    {
        var filtered = stepRuns.AsEnumerable();
        if (query.StepRunId.HasValue)
        {
            filtered = filtered.Where(item => item.Id == query.StepRunId.Value);
        }

        if (query.StepDefinitionId.HasValue)
        {
            filtered = filtered.Where(item => item.StepDefinitionId == query.StepDefinitionId.Value);
        }

        if (query.StepStatus.HasValue)
        {
            filtered = filtered.Where(item => item.Status == query.StepStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(item => Contains(item.Title, query.Search) ||
                                             Contains(item.CurrentExecutorName, query.Search) ||
                                             Contains(item.DecisionSummary, query.Search) ||
                                             Contains(item.BlockedReason, query.Search));
        }

        return filtered
            .OrderBy(item => item.Sequence)
            .ToList();
    }

    private static IReadOnlyList<ProcessArtifactViewModel> FilterArtifacts(
        IReadOnlyList<ProcessArtifactViewModel> artifacts,
        ProcessRunDetailApiQuery query)
    {
        var filtered = artifacts.AsEnumerable();
        if (query.ArtifactId.HasValue)
        {
            filtered = filtered.Where(item => item.Id == query.ArtifactId.Value);
        }

        if (query.StepRunId.HasValue)
        {
            filtered = filtered.Where(item => item.StepRunId == query.StepRunId.Value);
        }

        if (query.ArtifactExpectationId.HasValue)
        {
            filtered = filtered.Where(item => item.ArtifactExpectationId == query.ArtifactExpectationId.Value);
        }

        if (query.ArtifactKind.HasValue)
        {
            filtered = filtered.Where(item => item.ArtifactKind == query.ArtifactKind.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(item => Contains(item.Title, query.Search) ||
                                             Contains(item.ManagedStoragePath, query.Search) ||
                                             Contains(item.ExternalReferenceKey, query.Search) ||
                                             Contains(item.ProvenanceSummary, query.Search));
        }

        return filtered
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(NormalizeTake(query.Take))
            .ToList();
    }

    private static IReadOnlyList<ProcessRunAssignmentViewModel> FilterAssignments(
        IReadOnlyList<ProcessRunAssignmentViewModel> assignments,
        ProcessRunDetailApiQuery query)
    {
        var filtered = assignments.AsEnumerable();
        if (query.StepDefinitionId.HasValue)
        {
            filtered = filtered.Where(item => item.StepDefinitionId == query.StepDefinitionId.Value);
        }

        if (query.RoleRequirementId.HasValue)
        {
            filtered = filtered.Where(item => item.RoleRequirementId == query.RoleRequirementId.Value);
        }

        if (query.PartyId.HasValue)
        {
            filtered = filtered.Where(item => item.PartyId == query.PartyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(item => Contains(item.DisplayName, query.Search) ||
                                             Contains(item.ExecutorKind, query.Search) ||
                                             Contains(item.BindingReason, query.Search) ||
                                             Contains(item.RoleDisplayName, query.Search));
        }

        return filtered
            .OrderBy(item => item.DisplayName)
            .ToList();
    }

    private static IReadOnlyList<ProcessWorkBriefViewModel> FilterWorkBriefs(
        IReadOnlyList<ProcessWorkBriefViewModel> workBriefs,
        ProcessRunDetailApiQuery query)
    {
        var filtered = workBriefs.AsEnumerable();
        if (query.StepRunId.HasValue)
        {
            filtered = filtered.Where(item => item.StepRunId == query.StepRunId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(item => Contains(item.Title, query.Search) ||
                                             Contains(item.WorkBriefText, query.Search) ||
                                             Contains(item.ExpectedOutcome, query.Search));
        }

        return filtered
            .OrderBy(item => item.CreatedAtUtc)
            .Take(NormalizeTake(query.Take))
            .ToList();
    }

    private static IReadOnlyList<ProcessConformanceObservationViewModel> FilterConformanceObservations(
        IReadOnlyList<ProcessConformanceObservationViewModel> observations,
        ProcessRunDetailApiQuery query)
    {
        var filtered = observations.AsEnumerable();
        if (query.StepRunId.HasValue)
        {
            filtered = filtered.Where(item => item.StepRunId == query.StepRunId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(item => Contains(item.Category, query.Search) ||
                                             Contains(item.Observation, query.Search) ||
                                             Contains(item.DeviationReason, query.Search));
        }

        return filtered
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(NormalizeTake(query.Take))
            .ToList();
    }

    private static IReadOnlyList<ProcessDirectMessageThreadViewModel> FilterDirectMessageThreads(
        IReadOnlyList<ProcessDirectMessageThreadViewModel> threads,
        ProcessRunDetailApiQuery query)
    {
        var filtered = threads.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(item => Contains(item.Subject, query.Search) ||
                                             Contains(item.Route, query.Search) ||
                                             Contains(item.ParticipantSummary, query.Search) ||
                                             item.Messages.Any(message => Contains(message.Body, query.Search)));
        }

        return filtered
            .OrderByDescending(item => item.LastActivityAtUtc)
            .Take(NormalizeTake(query.Take))
            .ToList();
    }

    private static IReadOnlyList<ProcessOutboxRecordViewModel> FilterOutboxRecords(
        IReadOnlyList<ProcessOutboxRecordViewModel> outboxRecords,
        ProcessRunDetailApiQuery query)
    {
        var filtered = outboxRecords.AsEnumerable();
        if (query.StepRunId.HasValue)
        {
            filtered = filtered.Where(item => item.StepRunId == query.StepRunId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(item => Contains(item.CommandKey, query.Search) ||
                                             Contains(item.LastError, query.Search) ||
                                             Contains(item.Trigger, query.Search));
        }

        return filtered
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(NormalizeTake(query.Take))
            .ToList();
    }

    private static IReadOnlyList<ProcessExecutionRunViewModel> FilterExecutionRuns(
        IReadOnlyList<ProcessExecutionRunViewModel> executionRuns,
        ProcessRunDetailApiQuery query)
    {
        var filtered = executionRuns.AsEnumerable();
        if (query.StepRunId.HasValue)
        {
            filtered = filtered.Where(item => item.StepRunId == query.StepRunId.Value);
        }

        if (query.AgentId.HasValue)
        {
            filtered = filtered.Where(item => item.AgentId == query.AgentId.Value);
        }

        if (query.ExecutionState.HasValue)
        {
            filtered = filtered.Where(item => item.State == query.ExecutionState.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(item => Contains(item.Title, query.Search) ||
                                             Contains(item.AgentName, query.Search) ||
                                             Contains(item.InputSummary, query.Search) ||
                                             Contains(item.ResultSummary, query.Search));
        }

        return filtered
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Take(NormalizeTake(query.Take))
            .ToList();
    }

    private static bool Contains(string value, string? search)
    {
        return !string.IsNullOrWhiteSpace(search) &&
               value.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldInclude(bool? include)
    {
        return include.GetValueOrDefault(true);
    }

    private static int NormalizeTake(int? take)
    {
        return Math.Clamp(take.GetValueOrDefault(50), 1, 500);
    }

    private static string NormalizeActor(string? requestedBy)
    {
        return string.IsNullOrWhiteSpace(requestedBy)
            ? "development-api"
            : requestedBy.Trim();
    }
}

internal sealed class ProcessRunListApiQuery
{
    public Guid? DefinitionId { get; set; }

    public Guid? ProjectId { get; set; }

    public ProcessRunStatus? Status { get; set; }

    public ProcessOperatingMode? OperatingMode { get; set; }

    public string? Search { get; set; }

    public int? Take { get; set; }
}

internal sealed class ProcessRunDetailApiQuery
{
    public Guid? StepRunId { get; set; }

    public Guid? StepDefinitionId { get; set; }

    public Guid? RoleRequirementId { get; set; }

    public Guid? PartyId { get; set; }

    public Guid? ArtifactId { get; set; }

    public Guid? ArtifactExpectationId { get; set; }

    public Guid? AgentId { get; set; }

    public ProcessStepRunStatus? StepStatus { get; set; }

    public ProcessArtifactKind? ArtifactKind { get; set; }

    public ExecutionState? ExecutionState { get; set; }

    public string? Search { get; set; }

    public int? Take { get; set; }

    public bool? IncludeDecisions { get; set; } = true;

    public bool? IncludeArtifacts { get; set; } = true;

    public bool? IncludeOutboxRecords { get; set; } = true;

    public bool? IncludeAssignments { get; set; } = true;

    public bool? IncludeWorkBriefs { get; set; } = true;

    public bool? IncludeConformanceObservations { get; set; } = true;

    public bool? IncludeDirectMessages { get; set; } = true;

    public bool? IncludeExecutionRuns { get; set; } = true;

    public bool? IncludeEscalations { get; set; } = true;

    public bool? IncludeOperatorApprovals { get; set; } = true;

    public bool? IncludeAttemptTimeline { get; set; } = true;
}

internal sealed record DevelopmentProcessRunDetail(
    ProcessRunListItem Run,
    IReadOnlyList<ProcessStepRunViewModel> StepRuns,
    IReadOnlyList<ProcessDecisionViewModel> Decisions,
    IReadOnlyList<ProcessArtifactViewModel> Artifacts,
    IReadOnlyList<ProcessOutboxRecordViewModel> OutboxRecords,
    IReadOnlyList<ProcessRunAssignmentViewModel> Assignments,
    IReadOnlyList<ProcessWorkBriefViewModel> WorkBriefs,
    IReadOnlyList<ProcessConformanceObservationViewModel> ConformanceObservations,
    IReadOnlyList<ProcessDirectMessageThreadViewModel> DirectMessageThreads,
    IReadOnlyList<ProcessExecutionRunViewModel> ExecutionRuns,
    IReadOnlyList<ProcessEscalationViewModel> Escalations,
    IReadOnlyList<ProcessOperatorApprovalViewModel> OperatorApprovals,
    IReadOnlyList<ProcessAttemptTimelineEntryViewModel> AttemptTimeline,
    ProcessRunHealthSummaryViewModel Health);

internal sealed record ProcessTemplateImportApiRequest(
    Guid? ProjectId,
    string? DefinitionName);
