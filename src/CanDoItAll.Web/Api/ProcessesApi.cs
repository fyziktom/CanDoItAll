using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.Processes;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class ProcessesApi
{
    public static RouteGroupBuilder MapProcessesApi(this RouteGroupBuilder group)
    {
        var processes = group.MapGroup("/processes")
            .WithTags("Processes")
            .DisableAntiforgery();

        processes.MapGet("/definitions", async (
                Guid? projectId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.ListDefinitionsAsync(projectId, cancellationToken)))
            .WithName("ListProcessDefinitions");

        processes.MapGet("/definitions/{definitionId:guid}", async (
                Guid definitionId,
                Guid? projectId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.GetEditorAsync(definitionId, projectId, cancellationToken)))
            .WithName("GetProcessDefinitionEditor");

        processes.MapPost("/definitions", async (
                ProcessDefinitionEditorModel request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.SaveAsync(request, cancellationToken)))
            .WithName("SaveProcessDefinition");

        processes.MapPost("/definitions/{definitionId:guid}/publish", async (
                Guid definitionId,
                Guid? definitionConcurrencyToken,
                Guid? draftVersionConcurrencyToken,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.PublishAsync(
                new ProcessDefinitionPublishRequest
                {
                    DefinitionId = definitionId,
                    DefinitionConcurrencyToken = definitionConcurrencyToken,
                    DraftVersionConcurrencyToken = draftVersionConcurrencyToken
                },
                cancellationToken)))
            .DisableAntiforgery()
            .WithName("PublishProcessDefinition");

        processes.MapDelete("/definitions/{definitionId:guid}", async (
                Guid definitionId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                await processesService.DeleteAsync(definitionId, cancellationToken);
                return Results.Ok(new ApiAck(true));
            })
            .WithName("DeleteProcessDefinition");

        processes.MapGet("/definitions/{definitionId:guid}/export", async (
                Guid definitionId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.ExportAsync(definitionId, cancellationToken)))
            .WithName("ExportProcessDefinition");

        processes.MapPost("/definitions/import", async (
                ProcessImportExportEnvelope request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.ImportAsync(request, cancellationToken)))
            .WithName("ImportProcessDefinition");

        processes.MapGet("/runs", async (
                [AsParameters] ProcessRunListApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                var runs = await processesService.ListRunsAsync(query.DefinitionId, query.ProjectId, cancellationToken);
                return Results.Ok(FilterRuns(runs, query));
            })
            .WithName("ListProcessRuns");

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
                    return ApiEndpointResults.NotFound("Process run was not found.", "processes.run-not-found");
                }

                var details = await runDetailsLoader.LoadAsync(runId, cancellationToken);
                return Results.Ok(BuildFilteredRunDetail(run, details, query));
            })
            .WithName("GetProcessRunDetail");

        processes.MapGet("/runs/{runId:guid}/steps", async (
                Guid runId,
                [AsParameters] ProcessRunDetailApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(FilterStepRuns(await processesService.ListStepRunsAsync(runId, cancellationToken), query)))
            .WithName("ListProcessRunSteps");

        processes.MapGet("/runs/{runId:guid}/artifacts", async (
                Guid runId,
                [AsParameters] ProcessRunDetailApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(FilterArtifacts(await processesService.ListArtifactsAsync(runId, cancellationToken), query)))
            .WithName("ListProcessRunArtifacts");

        processes.MapGet("/runs/{runId:guid}/assignments", async (
                Guid runId,
                [AsParameters] ProcessRunDetailApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(FilterAssignments(await processesService.ListAssignmentsAsync(runId, cancellationToken), query)))
            .WithName("ListProcessRunAssignments");

        processes.MapGet("/runs/{runId:guid}/steps/{stepRunId:guid}", async (
                Guid runId,
                Guid stepRunId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                var stepRun = await FindStepRunAsync(runId, stepRunId, processesService, cancellationToken);
                return stepRun is null
                    ? ApiEndpointResults.NotFound("Process step run was not found.", "processes.step-run-not-found")
                    : Results.Ok(stepRun);
            })
            .WithName("GetProcessRunStep");

        processes.MapGet("/runs/{runId:guid}/steps/{stepRunId:guid}/artifacts", async (
                Guid runId,
                Guid stepRunId,
                [AsParameters] ProcessRunDetailApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                query.StepRunId = stepRunId;
                return Results.Ok(FilterArtifacts(await processesService.ListArtifactsAsync(runId, cancellationToken), query));
            })
            .WithName("ListProcessRunStepArtifacts");

        processes.MapGet("/runs/{runId:guid}/steps/{stepRunId:guid}/assignments", async (
                Guid runId,
                Guid stepRunId,
                [AsParameters] ProcessRunDetailApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                var stepRun = await FindStepRunAsync(runId, stepRunId, processesService, cancellationToken);
                if (stepRun is null)
                {
                    return ApiEndpointResults.NotFound("Process step run was not found.", "processes.step-run-not-found");
                }

                query.StepDefinitionId = stepRun.StepDefinitionId;
                return Results.Ok(FilterAssignments(await processesService.ListAssignmentsAsync(runId, cancellationToken), query));
            })
            .WithName("ListProcessRunStepAssignments");

        processes.MapGet("/runs/{runId:guid}/artifacts/{artifactId:guid}", async (
                Guid runId,
                Guid artifactId,
                [AsParameters] ProcessRunDetailApiQuery query,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                query.ArtifactId = artifactId;
                var artifact = FilterArtifacts(await processesService.ListArtifactsAsync(runId, cancellationToken), query)
                    .SingleOrDefault();
                return artifact is null
                    ? ApiEndpointResults.NotFound("Process artifact was not found.", "processes.artifact-not-found")
                    : Results.Ok(artifact);
            })
            .WithName("GetProcessRunArtifact");

        processes.MapGet("/analytics", async (
                Guid? definitionId,
                Guid? projectId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.GetAnalyticsAsync(definitionId, projectId, cancellationToken)))
            .WithName("GetProcessAnalytics");

        processes.MapPost("/runs/start", async (
                ProcessRunStartRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.StartRunAsync(request, cancellationToken)))
            .WithName("StartProcessRun");

        processes.MapPost("/runs/stop", async (
                ProcessRunStopRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.StopBlockedRunAsync(request, cancellationToken)))
            .WithName("StopProcessRun");

        processes.MapPost("/runs/manager-directives", async (
                ProcessManagerDirectiveRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.RecordManagerDirectiveAsync(request, cancellationToken)))
            .WithName("RecordProcessManagerDirective");

        processes.MapPost("/runs/{runId:guid}/manager-directives", async (
                Guid runId,
                ProcessManagerDirectiveApiRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.RecordManagerDirectiveAsync(
                new ProcessManagerDirectiveRequest
                {
                    ProcessRunId = runId,
                    Directive = request.Directive,
                    InstructedBy = NormalizeActor(request.InstructedBy)
                },
                cancellationToken)))
            .WithName("RecordProcessRunManagerDirective");

        processes.MapPost("/steps/transition", async (
                ProcessStepTransitionRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.TransitionStepAsync(request, cancellationToken)))
            .WithName("TransitionProcessStep");

        processes.MapPost("/runs/{runId:guid}/steps/{stepRunId:guid}/transition", async (
                Guid runId,
                Guid stepRunId,
                ProcessStepTransitionApiRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                var stepRun = await FindStepRunAsync(runId, stepRunId, processesService, cancellationToken);
                if (stepRun is null)
                {
                    return ApiEndpointResults.NotFound("Process step run was not found.", "processes.step-run-not-found");
                }

                return ApiEndpointResults.FromResult(await processesService.TransitionStepAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = stepRunId,
                        StepRunConcurrencyToken = request.StepRunConcurrencyToken,
                        TargetStatus = request.TargetStatus,
                        Reason = request.Reason,
                        BlockCause = request.BlockCause,
                        SelectedBranchOutcomeId = request.SelectedBranchOutcomeId,
                        DecidedBy = NormalizeActor(request.DecidedBy),
                        SuppressAutomationDispatch = request.SuppressAutomationDispatch
                    },
                    cancellationToken));
            })
            .WithName("TransitionProcessRunStep");

        processes.MapPost("/steps/rerun-agent", async (
                ProcessAgentStepRerunRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.RerunAgentStepAsync(request, cancellationToken)))
            .WithName("RerunProcessAgentStep");

        processes.MapPost("/runs/{runId:guid}/steps/{stepRunId:guid}/rerun-agent", async (
                Guid runId,
                Guid stepRunId,
                ProcessAgentStepRerunApiRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                var stepRun = await FindStepRunAsync(runId, stepRunId, processesService, cancellationToken);
                if (stepRun is null)
                {
                    return ApiEndpointResults.NotFound("Process step run was not found.", "processes.step-run-not-found");
                }

                return ApiEndpointResults.FromResult(await processesService.RerunAgentStepAsync(
                    new ProcessAgentStepRerunRequest
                    {
                        StepRunId = stepRunId,
                        StepRunConcurrencyToken = request.StepRunConcurrencyToken,
                        OperatorReason = request.OperatorReason
                    },
                    cancellationToken));
            })
            .WithName("RerunProcessRunAgentStep");

        processes.MapPost("/assignments/resolve", async (
                ProcessAssignmentResolutionRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.ResolveAssignmentAsync(request, cancellationToken)))
            .WithName("ResolveProcessAssignment");

        processes.MapPost("/runs/{runId:guid}/assignments/resolve", async (
                Guid runId,
                ProcessAssignmentResolutionApiRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.ResolveAssignmentAsync(
                new ProcessAssignmentResolutionRequest
                {
                    ProcessRunId = runId,
                    RoleRequirementId = request.RoleRequirementId,
                    StepDefinitionId = request.StepDefinitionId,
                    PartyId = request.PartyId,
                    DisplayName = request.DisplayName,
                    ExecutorKind = request.ExecutorKind,
                    WorkflowDefinitionId = request.WorkflowDefinitionId,
                    WorkflowVersionId = request.WorkflowVersionId,
                    BindingReason = request.BindingReason,
                    IsFallback = request.IsFallback,
                    AllowsDirectMessaging = request.AllowsDirectMessaging
                },
                cancellationToken)))
            .WithName("ResolveProcessRunAssignment");

        processes.MapGet("/runs/{runId:guid}/escalations", async (
                Guid runId,
                IProcessEscalationService escalationService,
                CancellationToken cancellationToken) =>
            Results.Ok(await escalationService.ListAsync(runId, cancellationToken)))
            .WithName("ListProcessRunEscalations");

        processes.MapPost("/runs/{runId:guid}/escalations", async (
                Guid runId,
                ProcessEscalationCreateApiRequest request,
                IProcessEscalationService escalationService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await escalationService.CreateAsync(
                new ProcessEscalationCreateRequest
                {
                    ProcessRunId = runId,
                    StepRunId = request.StepRunId,
                    Kind = request.Kind,
                    Severity = request.Severity,
                    Title = request.Title,
                    Reason = request.Reason,
                    Owner = request.Owner,
                    DueAtUtc = request.DueAtUtc,
                    SourceExecutionRunId = request.SourceExecutionRunId,
                    SourceApprovalId = request.SourceApprovalId,
                    SourceToolName = request.SourceToolName,
                    CreatedBy = NormalizeActor(request.CreatedBy)
                },
                cancellationToken)))
            .WithName("CreateProcessRunEscalation");

        processes.MapPost("/runs/{runId:guid}/escalations/{escalationId:guid}/assign", async (
                Guid runId,
                Guid escalationId,
                ProcessEscalationAssignmentApiRequest request,
                IProcessEscalationService escalationService,
                CancellationToken cancellationToken) =>
        {
            var routeValidation = await ValidateEscalationRouteAsync(runId, escalationId, escalationService, cancellationToken);
            if (routeValidation is not null)
            {
                return routeValidation;
            }

            return ApiEndpointResults.FromResult(await escalationService.AssignAsync(
                new ProcessEscalationAssignmentRequest
                {
                    EscalationId = escalationId,
                    Owner = request.Owner,
                    AssignedBy = NormalizeActor(request.AssignedBy)
                },
                cancellationToken));
        })
        .WithName("AssignProcessRunEscalation");

        processes.MapPost("/runs/{runId:guid}/escalations/{escalationId:guid}/resolve", async (
                Guid runId,
                Guid escalationId,
                ProcessEscalationResolutionApiRequest request,
                IProcessEscalationService escalationService,
                CancellationToken cancellationToken) =>
        {
            var routeValidation = await ValidateEscalationRouteAsync(runId, escalationId, escalationService, cancellationToken);
            if (routeValidation is not null)
            {
                return routeValidation;
            }

            return ApiEndpointResults.FromResult(await escalationService.ResolveAsync(
                new ProcessEscalationResolutionRequest
                {
                    EscalationId = escalationId,
                    Resolution = request.Resolution,
                    ResolvedBy = NormalizeActor(request.ResolvedBy)
                },
                cancellationToken));
        })
        .WithName("ResolveProcessRunEscalation");

        processes.MapPost("/runs/{runId:guid}/escalations/{escalationId:guid}/reopen", async (
                Guid runId,
                Guid escalationId,
                ProcessEscalationReopenApiRequest request,
                IProcessEscalationService escalationService,
                CancellationToken cancellationToken) =>
        {
            var routeValidation = await ValidateEscalationRouteAsync(runId, escalationId, escalationService, cancellationToken);
            if (routeValidation is not null)
            {
                return routeValidation;
            }

            return ApiEndpointResults.FromResult(await escalationService.ReopenAsync(
                new ProcessEscalationReopenRequest
                {
                    EscalationId = escalationId,
                    Reason = request.Reason,
                    ReopenedBy = NormalizeActor(request.ReopenedBy)
                },
                cancellationToken));
        })
        .WithName("ReopenProcessRunEscalation");

        processes.MapPost("/runs/{runId:guid}/escalations/{escalationId:guid}/rework", async (
                Guid runId,
                Guid escalationId,
                ProcessEscalationReworkApiRequest request,
                IProcessEscalationService escalationService,
                CancellationToken cancellationToken) =>
        {
            var routeValidation = await ValidateEscalationRouteAsync(runId, escalationId, escalationService, cancellationToken);
            if (routeValidation is not null)
            {
                return routeValidation;
            }

            return ApiEndpointResults.FromResult(await escalationService.RequestReworkAsync(
                new ProcessEscalationReworkRequest
                {
                    EscalationId = escalationId,
                    StepRunConcurrencyToken = request.StepRunConcurrencyToken,
                    Directive = request.Directive,
                    RequestedBy = NormalizeActor(request.RequestedBy)
                },
                cancellationToken));
        })
        .WithName("RequestProcessRunEscalationRework");

        processes.MapPost("/runs/{runId:guid}/operator-approvals/decisions", async (
                Guid runId,
                ProcessOperatorApprovalDecisionApiRequest request,
                IProcessEscalationService escalationService,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            if (request.ExecutionRunId.HasValue)
            {
                try
                {
                    await workspaceService.ContinueExecutionRunAsync(
                        request.ExecutionRunId.Value,
                        request.Status == ProcessOperatorApprovalStatus.Approved,
                        request.AutoApprovePendingToolCalls,
                        cancellationToken);
                }
                catch (InvalidOperationException exception)
                {
                    return ApiEndpointResults.FromException(exception);
                }
            }

            return ApiEndpointResults.FromResult(await escalationService.RecordApprovalDecisionAsync(
                new ProcessOperatorApprovalDecisionRequest
                {
                    ProcessRunId = runId,
                    StepRunId = request.StepRunId,
                    ExecutionRunId = request.ExecutionRunId,
                    LaunchPlanId = request.LaunchPlanId,
                    ExternalApprovalId = request.ExternalApprovalId,
                    Status = request.Status,
                    Summary = request.Summary,
                    DecidedBy = NormalizeActor(request.DecidedBy)
                },
                cancellationToken));
        })
        .WithName("DecideProcessRunOperatorApproval");

        processes.MapPost("/artifacts", async (
                ProcessArtifactRecordRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.RecordArtifactAsync(request, cancellationToken)))
            .WithName("RecordProcessArtifact");

        processes.MapPost("/runs/{runId:guid}/steps/{stepRunId:guid}/artifacts", async (
                Guid runId,
                Guid stepRunId,
                ProcessArtifactRecordApiRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            {
                var stepRun = await FindStepRunAsync(runId, stepRunId, processesService, cancellationToken);
                if (stepRun is null)
                {
                    return ApiEndpointResults.NotFound("Process step run was not found.", "processes.step-run-not-found");
                }

                return ApiEndpointResults.FromResult(await processesService.RecordArtifactAsync(
                    new ProcessArtifactRecordRequest
                    {
                        ProcessRunId = runId,
                        StepRunId = stepRunId,
                        ArtifactExpectationId = request.ArtifactExpectationId,
                        ArtifactKind = request.ArtifactKind,
                        Title = request.Title,
                        TrustStatus = request.TrustStatus,
                        SensitivityLevel = request.SensitivityLevel,
                        ProvenanceSummary = request.ProvenanceSummary,
                        AllowedFutureUsageSummary = request.AllowedFutureUsageSummary,
                        ReviewSummary = request.ReviewSummary,
                        ManagedStoragePath = request.ManagedStoragePath,
                        ExternalReferenceKey = request.ExternalReferenceKey,
                        ProjectionLineage = request.ProjectionLineage
                    },
                    cancellationToken));
            })
            .WithName("RecordProcessRunStepArtifact");

        processes.MapPost("/direct-messages", async (
                ProcessDirectMessageRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.SendDirectMessageAsync(request, cancellationToken)))
            .WithName("SendProcessDirectMessage");

        processes.MapPost("/runs/{runId:guid}/direct-messages", async (
                Guid runId,
                ProcessDirectMessageApiRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.SendDirectMessageAsync(
                new ProcessDirectMessageRequest
                {
                    ProcessRunId = runId,
                    SourceRoleRequirementId = request.SourceRoleRequirementId,
                    TargetRoleRequirementId = request.TargetRoleRequirementId,
                    MessageBody = request.MessageBody
                },
                cancellationToken)))
            .WithName("SendProcessRunDirectMessage");

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
            .WithName("ListProcessLaunchPlans");

        processes.MapGet("/launch-plans/{launchPlanId:guid}", async (
                Guid launchPlanId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
        {
            var launchPlan = await processesService.GetLaunchPlanAsync(launchPlanId, cancellationToken);
            return launchPlan is null
                ? ApiEndpointResults.NotFound("Launch plan was not found.", "processes.launch.not-found")
                : Results.Ok(launchPlan);
        })
        .WithName("GetProcessLaunchPlan");

        processes.MapPost("/launch-plans", async (
                ProcessLaunchCreateRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.CreateLaunchPlanAsync(request, cancellationToken)))
            .WithName("CreateProcessLaunchPlan");

        processes.MapPost("/launch-plans/{launchPlanId:guid}/hr-match", async (
                Guid launchPlanId,
                Guid? agentTeamId,
                string? requestedBy,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.MatchLaunchPlanWithHrManagerAsync(
                launchPlanId,
                agentTeamId,
                NormalizeActor(requestedBy),
                cancellationToken)))
            .WithName("MatchProcessLaunchPlanWithHr");

        processes.MapPost("/launch-plans/{launchPlanId:guid}/submit-approval", async (
                Guid launchPlanId,
                string? requestedBy,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.SubmitLaunchPlanForApprovalAsync(
                launchPlanId,
                NormalizeActor(requestedBy),
                cancellationToken)))
            .WithName("SubmitProcessLaunchPlanApproval");

        processes.MapPost("/launch-plans/approval-decisions", async (
                ProcessLaunchApprovalDecisionRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.DecideLaunchPlanApprovalAsync(request, cancellationToken)))
            .WithName("DecideProcessLaunchPlanApproval");

        processes.MapPost("/launch-plans/{launchPlanId:guid}/provision", async (
                Guid launchPlanId,
                string? requestedBy,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.ProvisionLaunchPlanAsync(
                launchPlanId,
                NormalizeActor(requestedBy),
                cancellationToken)))
            .WithName("ProvisionProcessLaunchPlan");

        processes.MapPost("/launch-plans/execute", async (
                ProcessLaunchExecutionRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.ExecuteLaunchPlanAsync(request, cancellationToken)))
            .WithName("ExecuteProcessLaunchPlan");

        processes.MapPost("/launch-plans/candidate-selections", async (
                ProcessLaunchCandidateSelectionRequest request,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await processesService.SelectLaunchCandidateAsync(request, cancellationToken)))
            .WithName("SelectProcessLaunchCandidate");
    }

    private static void MapTemplateEndpoints(RouteGroupBuilder processes)
    {
        processes.MapGet("/templates", (
                ProcessTemplateCatalogService catalogService) =>
            Results.Ok(catalogService.ListProcessTemplates()))
            .WithName("ListProcessTemplates");

        processes.MapGet("/templates/baseline-scenarios", (
                ProcessTemplatePackLoader packLoader) =>
        {
            var pack = packLoader.Load();
            return Results.Ok(pack.BaselineScenarios
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
        })
        .WithName("ListProcessTemplateBaselineScenarios");

        processes.MapGet("/templates/live-run-profiles", (
                ProcessTemplatePackLoader packLoader) =>
        {
            var pack = packLoader.Load();
            return Results.Ok(pack.LiveRunProfiles
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
        })
        .WithName("ListProcessTemplateLiveRunProfiles");

        processes.MapGet("/templates/{processKey}", (
                string processKey,
                ProcessTemplatePackLoader packLoader) =>
        {
            var pack = packLoader.Load();
            return pack.Processes.TryGetValue(processKey, out var template)
                ? Results.Ok(template)
                : ApiEndpointResults.NotFound($"Process template '{processKey}' was not found.", "processes.template-not-found");
        })
        .WithName("GetProcessTemplate");

        processes.MapGet("/templates/{processKey}/detail", (
                string processKey,
                ProcessTemplatePackLoader packLoader,
                ProcessTemplateCatalogService catalogService,
                ProcessTemplateProjectionService projectionService,
                ProcessTemplateMermaidExporter mermaidExporter) =>
        {
            try
            {
                var pack = packLoader.Load();
                if (!pack.Processes.TryGetValue(processKey, out var template))
                {
                    return ApiEndpointResults.NotFound($"Process template '{processKey}' was not found.", "processes.template-not-found");
                }

                var summary = catalogService.ListProcessTemplates()
                    .FirstOrDefault(item => string.Equals(item.Key, processKey, StringComparison.OrdinalIgnoreCase));
                if (summary is null)
                {
                    return ApiEndpointResults.NotFound($"Process template '{processKey}' was not found in the template catalog.", "processes.template-not-found");
                }

                return Results.Ok(new ProcessTemplateDetailApiResponse(
                    summary,
                    template,
                    projectionService.GetCompatibilityReportMarkdown(processKey),
                    mermaidExporter.Export(processKey).SupportingFiles));
            }
            catch (InvalidOperationException exception)
            {
                return ApiEndpointResults.BadRequest(exception.Message, "processes.template-detail-failed");
            }
        })
        .WithName("GetProcessTemplateDetail");

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
                return ApiEndpointResults.BadRequest(exception.Message, "processes.template-projection-failed");
            }
        })
        .WithName("ProjectProcessTemplateEnvelope");

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
                return ApiEndpointResults.BadRequest(exception.Message, "processes.template-mermaid-failed");
            }
        })
        .WithName("ExportProcessTemplateMermaid");

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
                return ApiEndpointResults.FromResult(await processesService.ImportAsync(envelope, cancellationToken));
            }
            catch (InvalidOperationException exception)
            {
                return ApiEndpointResults.BadRequest(exception.Message, "processes.template-import-failed");
            }
        })
        .WithName("ImportProcessTemplate");
    }

    private static void MapRegistryEndpoints(RouteGroupBuilder processes)
    {
        processes.MapGet("/executor-options", async (
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.ListExecutorOptionsAsync(cancellationToken)))
            .WithName("ListProcessExecutorOptions");

        processes.MapGet("/manager-agent-options", async (
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.ListManagerAgentOptionsAsync(cancellationToken)))
            .WithName("ListProcessManagerAgentOptions");

        processes.MapGet("/party-options/{projectId:guid}", async (
                Guid projectId,
                ProcessesService processesService,
                CancellationToken cancellationToken) =>
            Results.Ok(await processesService.ListPartyOptionsAsync(projectId, cancellationToken)))
            .WithName("ListProcessPartyOptions");
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

    private static ProcessRunDetail BuildFilteredRunDetail(
        ProcessRunListItem run,
        ProcessWorkspaceRunDetails details,
        ProcessRunDetailApiQuery query)
    {
        return new ProcessRunDetail(
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
            ShouldInclude(query.IncludeWorkflowRuns) ? FilterWorkflowRuns(details.WorkflowRuns, query) : [],
            ShouldInclude(query.IncludeEscalations) ? details.Escalations : [],
            ShouldInclude(query.IncludeOperatorApprovals) ? details.OperatorApprovals : [],
            ShouldInclude(query.IncludeAttemptTimeline) ? details.AttemptTimeline : [],
            details.Health,
            details.UsageSummary);
    }

    private static async Task<ProcessStepRunViewModel?> FindStepRunAsync(
        Guid runId,
        Guid stepRunId,
        ProcessesService processesService,
        CancellationToken cancellationToken)
    {
        var query = new ProcessRunDetailApiQuery { StepRunId = stepRunId };
        return FilterStepRuns(await processesService.ListStepRunsAsync(runId, cancellationToken), query)
            .SingleOrDefault();
    }

    private static async Task<IResult?> ValidateEscalationRouteAsync(
        Guid runId,
        Guid escalationId,
        IProcessEscalationService escalationService,
        CancellationToken cancellationToken)
    {
        var escalation = (await escalationService.ListAsync(runId, cancellationToken))
            .SingleOrDefault(item => item.Id == escalationId);

        return escalation is null
            ? ApiEndpointResults.NotFound("Process escalation was not found for this run.", "processes.escalation-not-found")
            : null;
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

    private static IReadOnlyList<ProcessWorkflowRunViewModel> FilterWorkflowRuns(
        IReadOnlyList<ProcessWorkflowRunViewModel> workflowRuns,
        ProcessRunDetailApiQuery query)
    {
        var filtered = workflowRuns.AsEnumerable();
        if (query.StepRunId.HasValue)
        {
            filtered = filtered.Where(item => item.StepRunId == query.StepRunId.Value);
        }

        if (query.WorkflowRunId.HasValue)
        {
            filtered = filtered.Where(item => item.WorkflowRunId == query.WorkflowRunId.Value);
        }

        if (query.WorkflowDefinitionId.HasValue)
        {
            filtered = filtered.Where(item => item.WorkflowDefinitionId == query.WorkflowDefinitionId.Value);
        }

        if (query.WorkflowVersionId.HasValue)
        {
            filtered = filtered.Where(item => item.WorkflowVersionId == query.WorkflowVersionId.Value);
        }

        if (query.WorkflowState.HasValue)
        {
            filtered = filtered.Where(item => item.State == query.WorkflowState.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            filtered = filtered.Where(item => Contains(item.WorkflowName, query.Search) ||
                                             Contains(item.StepTitle, query.Search) ||
                                             Contains(item.AssignmentDisplayName, query.Search) ||
                                             Contains(item.Summary, query.Search));
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
            ? "api"
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

    public Guid? WorkflowRunId { get; set; }

    public Guid? WorkflowDefinitionId { get; set; }

    public Guid? WorkflowVersionId { get; set; }

    public ProcessStepRunStatus? StepStatus { get; set; }

    public ProcessArtifactKind? ArtifactKind { get; set; }

    public ExecutionState? ExecutionState { get; set; }

    public WorkflowRunState? WorkflowState { get; set; }

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

    public bool? IncludeWorkflowRuns { get; set; } = true;

    public bool? IncludeEscalations { get; set; } = true;

    public bool? IncludeOperatorApprovals { get; set; } = true;

    public bool? IncludeAttemptTimeline { get; set; } = true;
}

internal sealed record ProcessRunDetail(
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
    IReadOnlyList<ProcessWorkflowRunViewModel> WorkflowRuns,
    IReadOnlyList<ProcessEscalationViewModel> Escalations,
    IReadOnlyList<ProcessOperatorApprovalViewModel> OperatorApprovals,
    IReadOnlyList<ProcessAttemptTimelineEntryViewModel> AttemptTimeline,
    ProcessRunHealthSummaryViewModel Health,
    ProcessRunUsageSummaryViewModel UsageSummary);

internal sealed record ProcessTemplateImportApiRequest(
    Guid? ProjectId,
    string? DefinitionName);

internal sealed record ProcessTemplateDetailApiResponse(
    ProcessTemplateCatalogItem Summary,
    ProcessTemplateDefinition Template,
    string CompatibilityReportMarkdown,
    IReadOnlyList<string> SupportingFiles);

internal sealed record ProcessManagerDirectiveApiRequest(
    string Directive,
    string? InstructedBy);

internal sealed record ProcessDirectMessageApiRequest(
    Guid SourceRoleRequirementId,
    Guid TargetRoleRequirementId,
    string MessageBody);

internal sealed class ProcessStepTransitionApiRequest
{
    public Guid? StepRunConcurrencyToken { get; set; }

    public ProcessStepRunStatus TargetStatus { get; set; } = ProcessStepRunStatus.InProgress;

    public string Reason { get; set; } = string.Empty;

    public ProcessStepBlockCause? BlockCause { get; set; }

    public Guid? SelectedBranchOutcomeId { get; set; }

    public string? DecidedBy { get; set; }

    public bool SuppressAutomationDispatch { get; set; }
}

internal sealed class ProcessAgentStepRerunApiRequest
{
    public Guid? StepRunConcurrencyToken { get; set; }

    public string OperatorReason { get; set; } = string.Empty;
}

internal sealed class ProcessAssignmentResolutionApiRequest
{
    public Guid RoleRequirementId { get; set; }

    public Guid? StepDefinitionId { get; set; }

    public Guid? PartyId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string ExecutorKind { get; set; } = string.Empty;

    public Guid? WorkflowDefinitionId { get; set; }

    public Guid? WorkflowVersionId { get; set; }

    public string BindingReason { get; set; } = string.Empty;

    public bool IsFallback { get; set; }

    public bool AllowsDirectMessaging { get; set; } = true;
}

internal sealed class ProcessEscalationCreateApiRequest
{
    public Guid? StepRunId { get; set; }

    public ProcessEscalationKind Kind { get; set; } = ProcessEscalationKind.BlockedStep;

    public ProcessEscalationSeverity Severity { get; set; } = ProcessEscalationSeverity.Moderate;

    public string Title { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public DateTimeOffset? DueAtUtc { get; set; }

    public string SourceExecutionRunId { get; set; } = string.Empty;

    public string SourceApprovalId { get; set; } = string.Empty;

    public string SourceToolName { get; set; } = string.Empty;

    public string? CreatedBy { get; set; }
}

internal sealed class ProcessEscalationAssignmentApiRequest
{
    public string Owner { get; set; } = string.Empty;

    public string? AssignedBy { get; set; }
}

internal sealed class ProcessEscalationResolutionApiRequest
{
    public string Resolution { get; set; } = string.Empty;

    public string? ResolvedBy { get; set; }
}

internal sealed class ProcessEscalationReopenApiRequest
{
    public string Reason { get; set; } = string.Empty;

    public string? ReopenedBy { get; set; }
}

internal sealed class ProcessEscalationReworkApiRequest
{
    public Guid? StepRunConcurrencyToken { get; set; }

    public string Directive { get; set; } = string.Empty;

    public string? RequestedBy { get; set; }
}

internal sealed class ProcessOperatorApprovalDecisionApiRequest
{
    public Guid? StepRunId { get; set; }

    public Guid? ExecutionRunId { get; set; }

    public Guid? LaunchPlanId { get; set; }

    public string ExternalApprovalId { get; set; } = string.Empty;

    public ProcessOperatorApprovalStatus Status { get; set; } = ProcessOperatorApprovalStatus.Approved;

    public string Summary { get; set; } = string.Empty;

    public string? DecidedBy { get; set; }

    public bool AutoApprovePendingToolCalls { get; set; }
}

internal sealed class ProcessArtifactRecordApiRequest
{
    public Guid? ArtifactExpectationId { get; set; }

    public ProcessArtifactKind ArtifactKind { get; set; } = ProcessArtifactKind.Evidence;

    public string Title { get; set; } = string.Empty;

    public ProcessArtifactTrustStatus TrustStatus { get; set; } = ProcessArtifactTrustStatus.ReviewRequired;

    public ProcessSensitivityLevel SensitivityLevel { get; set; } = ProcessSensitivityLevel.Internal;

    public string ProvenanceSummary { get; set; } = string.Empty;

    public string AllowedFutureUsageSummary { get; set; } = string.Empty;

    public string ReviewSummary { get; set; } = string.Empty;

    public string ManagedStoragePath { get; set; } = string.Empty;

    public string ExternalReferenceKey { get; set; } = string.Empty;

    public ProcessArtifactProjectionLineage? ProjectionLineage { get; set; }
}
