using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Web.Api;

internal static class ProcessesApi
{
    public static RouteGroupBuilder MapProcessesApi(this RouteGroupBuilder group)
    {
        var processes = group.MapGroup("/processes")
            .WithTags("Processes")
            .DisableAntiforgery();

        processes.MapGet("/contract", () => Results.Ok(new ProcessApiContractResponse(
            [
                "GET /api/processes/contract",
                "POST /api/processes/launch/check",
                "POST /api/processes/launch",
                "POST /api/processes/runs/{runId}/dispatch",
                "POST /api/processes/runs/{runId}/cancel",
                "POST /api/processes/runs/{runId}/steps/{stepInstanceId}/rework",
                "GET /api/processes/live",
                "GET /api/processes/runs/{runId}",
                "GET /api/processes/runs/{runId}/history"
            ],
            "Runtime/core/dispatch remain generic. Module adapters resolve CanDoItAll agent execution through process driver strategies.")))
            .WithName("GetProcessesApiContract");

        processes.MapPost("/launch/check", async (
                ProcessLaunchApiRequest request,
                ProcessLaunchApplicationService launchService,
                ProjectStructureProcessNodeService projectStructureProcessNodeService,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            await ExecuteLaunchOperationAsync(
                request,
                launchService,
                projectStructureProcessNodeService,
                loggerFactory,
                previewOnly: true,
                cancellationToken))
        .WithName("CheckProcessLaunch");

        processes.MapPost("/launch", async (
                ProcessLaunchApiRequest request,
                ProcessLaunchApplicationService launchService,
                ProjectStructureProcessNodeService projectStructureProcessNodeService,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            await ExecuteLaunchOperationAsync(
                request,
                launchService,
                projectStructureProcessNodeService,
                loggerFactory,
                previewOnly: false,
                cancellationToken))
        .WithName("LaunchProcess");

        processes.MapPost("/runs/{runId:guid}/dispatch", async (
                Guid runId,
                ProcessDispatchApiRequest request,
                ProcessRuntimeDispatchApplicationService dispatchService,
                ProcessRuntimeProjectionCatchupService projectionCatchupService,
                CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await dispatchService
                    .ExecuteReadyAsync(
                        new ProcessRunId(runId),
                        string.IsNullOrWhiteSpace(request.RequestedBy) ? "process-api" : request.RequestedBy,
                        cancellationToken)
                    .ConfigureAwait(false);
                await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

                return Results.Ok(new ProcessDispatchApiResponse(
                    result.RunId.Value,
                    result.Stage.ToString(),
                    result.Status.ToString(),
                    result.Diagnostics));
            }
            catch (InvalidOperationException exception)
            {
                return ApiEndpointResults.NotFound(exception.Message, "process.run_not_found");
            }
        })
        .WithName("DispatchProcessRun");

        processes.MapPost("/runs/{runId:guid}/cancel", async (
                Guid runId,
                ProcessRuntimeCancelApiRequest request,
                ProcessRuntimeOperatorApplicationService operatorService,
                ProcessRuntimeProjectionCatchupService projectionCatchupService,
                CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await operatorService
                    .RequestCancellationAsync(
                        new ProcessRuntimeRunCancellationCommand(
                            new ProcessRunId(runId),
                            string.IsNullOrWhiteSpace(request.RequestedBy) ? "process-api" : request.RequestedBy,
                            request.Reason),
                        cancellationToken)
                    .ConfigureAwait(false);
                await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

                return Results.Ok(new ProcessRuntimeCancelApiResponse(
                    result.RunId.Value,
                    result.Kind.ToString(),
                    result.Outcome.ToString(),
                    result.Status.ToString(),
                    result.Diagnostics));
            }
            catch (InvalidOperationException exception)
            {
                return ApiEndpointResults.NotFound(exception.Message, "process.run_not_found");
            }
        })
        .WithName("CancelProcessRun");

        processes.MapPost("/runs/{runId:guid}/steps/{stepInstanceId:guid}/rework", async (
                Guid runId,
                Guid stepInstanceId,
                ProcessRuntimeReworkApiRequest request,
                ProcessRuntimeOperatorApplicationService operatorService,
                ProcessRuntimeProjectionCatchupService projectionCatchupService,
                CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await operatorService
                    .ExecuteAsync(
                        new ProcessRuntimeOperatorActionCommand(
                            new ProcessRunId(runId),
                            new ProcessStepInstanceId(stepInstanceId),
                            ProcessRuntimeOperatorActionKind.RequestRework,
                            string.IsNullOrWhiteSpace(request.RequestedBy) ? "process-api" : request.RequestedBy,
                            request.Reason),
                        cancellationToken)
                    .ConfigureAwait(false);
                await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);

                return Results.Ok(new ProcessRuntimeReworkApiResponse(
                    result.RunId.Value,
                    result.StepInstanceId.Value,
                    result.Kind.ToString(),
                    result.Outcome.ToString(),
                    result.Status.ToString(),
                    result.Diagnostics));
            }
            catch (InvalidOperationException exception)
            {
                return ApiEndpointResults.NotFound(exception.Message, "process.run_not_found");
            }
        })
        .WithName("RequestProcessStepRework");

        processes.MapGet("/live", async (
                int? take,
                int? windowMinutes,
                ProcessRuntimeProjectionCatchupService projectionCatchupService,
                ProcessRuntimeProjectionQueryService queryService,
                CancellationToken cancellationToken) =>
        {
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
            var result = await queryService
                .GetLiveProcessesAsync(
                    new ProcessLiveProcessesQuery(
                        DateTimeOffset.UtcNow,
                        TimeSpan.FromMinutes(Math.Clamp(windowMinutes ?? 240, 1, 60 * 24 * 30)),
                        Math.Clamp(take ?? 50, 1, 500)),
                    cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new ProcessLiveApiResponse(
                result.Runs.Select(MapLiveRun).ToArray(),
                MapFreshness(result.Freshness)));
        })
        .WithName("ListLiveProcesses");

        processes.MapGet("/runs/{runId:guid}", async (
                Guid runId,
                ProcessRuntimeProjectionCatchupService projectionCatchupService,
                ProcessRuntimeProjectionQueryService queryService,
                CancellationToken cancellationToken) =>
        {
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
            var detail = await queryService
                .GetRunDetailAsync(new ProcessRunDetailQuery(new ProcessRunId(runId)), cancellationToken)
                .ConfigureAwait(false);

            return detail is null
                ? ApiEndpointResults.NotFound($"Process run '{runId:D}' was not found.", "process.run_not_found")
                : Results.Ok(MapRunDetail(detail));
        })
        .WithName("GetProcessRun");

        processes.MapGet("/runs/{runId:guid}/history", async (
                Guid runId,
                DateTimeOffset? fromUtc,
                DateTimeOffset? toUtc,
                int? take,
                ProcessRuntimeProjectionCatchupService projectionCatchupService,
                ProcessRuntimeProjectionQueryService queryService,
                CancellationToken cancellationToken) =>
        {
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
            var effectiveTo = NormalizeUtc(toUtc ?? DateTimeOffset.UtcNow);
            var effectiveFrom = NormalizeUtc(fromUtc ?? effectiveTo.AddHours(-24));
            var result = await queryService
                .GetRunHistoryAsync(
                    new ProcessRunHistoryQuery(
                        new ProcessRunId(runId),
                        effectiveFrom,
                        effectiveTo,
                        Math.Clamp(take ?? 100, 1, 1000)),
                    cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new ProcessHistoryApiResponse(
                result.Events.Select(MapTimelineEvent).ToArray(),
                MapFreshness(result.Freshness)));
        })
        .WithName("GetProcessRunHistory");

        return group;
    }

    private static async Task<IResult> ExecuteLaunchOperationAsync(
        ProcessLaunchApiRequest request,
        ProcessLaunchApplicationService launchService,
        ProjectStructureProcessNodeService projectStructureProcessNodeService,
        ILoggerFactory loggerFactory,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        try
        {
            var launchRequest = await MapLaunchRequestAsync(
                request,
                projectStructureProcessNodeService,
                cancellationToken).ConfigureAwait(false);
            var result = previewOnly
                ? await launchService.PreviewAsync(launchRequest with { Execute = false }, cancellationToken).ConfigureAwait(false)
                : await launchService.LaunchAsync(launchRequest, cancellationToken).ConfigureAwait(false);

            return Results.Ok(MapLaunchResult(result));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loggerFactory
                .CreateLogger("CanDoItAll.Web.Api.ProcessesApi")
                .LogError(
                    exception,
                    "Process launch operation failed. Operation={Operation} DefinitionKey={DefinitionKey} LiveRunProfileKey={LiveRunProfileKey} ProjectId={ProjectId} ProjectNodeId={ProjectNodeId} Execute={Execute} RunReadiness={RunReadiness}",
                    previewOnly ? "check" : "launch",
                    request.DefinitionKey,
                    request.LiveRunProfileKey,
                    request.ProjectId,
                    request.ProjectNodeId,
                    request.Execute,
                    request.RunReadiness);
            return ApiEndpointResults.BadRequest(exception.Message, previewOnly ? "process.launch_check_failed" : "process.launch_failed");
        }
    }

    private static async Task<ProcessLaunchRequest> MapLaunchRequestAsync(
        ProcessLaunchApiRequest request,
        ProjectStructureProcessNodeService projectStructureProcessNodeService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var variables = request.Variables ?? new Dictionary<string, string>(StringComparer.Ordinal);
        if (request.ProjectId is { } projectId &&
            projectId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(request.ProjectNodeId))
        {
            variables = new Dictionary<string, string>(
                await projectStructureProcessNodeService.BuildProjectScopedLaunchVariablesAsync(
                    new ProjectStructureProcessLaunchVariableBuildRequest(
                        projectId,
                        request.ProjectNodeId,
                        request.DefinitionKey,
                        request.ProcessDefinitionId is { } scopedDefinitionId ? new ProcessDefinitionId(scopedDefinitionId) : null,
                        string.IsNullOrWhiteSpace(request.RequestedBy) ? "process-api" : request.RequestedBy,
                        variables),
                    cancellationToken).ConfigureAwait(false),
                StringComparer.Ordinal);
        }

        return new ProcessLaunchRequest(
            request.DefinitionKey,
            request.ProcessDefinitionId is { } definitionId ? new ProcessDefinitionId(definitionId) : null,
            request.LiveRunProfileKey,
            request.ProjectId,
            request.ProjectNodeId,
            string.IsNullOrWhiteSpace(request.RequestedBy) ? "process-api" : request.RequestedBy,
            variables,
            request.RunReadiness,
            request.Execute);
    }

    private static ProcessLaunchApiResponse MapLaunchResult(ProcessLaunchResult result)
    {
        return new ProcessLaunchApiResponse(
            result.DefinitionId.Value,
            result.LaunchPlanId.Value,
            result.RunId?.Value,
            result.Stage.ToString(),
            result.Route,
            new ProcessLaunchPlanApiView(
                result.LaunchPlan.PlanId.Value,
                result.LaunchPlan.DefinitionId.Value,
                result.LaunchPlan.DefinitionVersionId.Value,
                result.LaunchPlan.DefinitionKey,
                result.LaunchPlan.DefinitionName,
                result.LaunchPlan.DefinitionSummary,
                result.LaunchPlan.LiveRunProfileKey,
                result.LaunchPlan.PlanHash,
                result.LaunchPlan.Steps.Select(MapLaunchStep).ToArray(),
                result.LaunchPlan.ReadinessFindings.Select(MapReadinessFinding).ToArray()),
            result.Warnings);
    }

    private static ProcessLaunchStepApiView MapLaunchStep(ProcessLaunchStepView step)
    {
        return new ProcessLaunchStepApiView(
            step.StepInstanceId.Value,
            step.StepKey,
            step.Title,
            step.RoleKey,
            step.ExecutorKind,
            step.ExecutorId,
            step.ExecutorDisplayName,
            step.IsBlocked,
            step.BlockedReason,
            step.BranchGate is null
                ? null
                : new ProcessRuntimeBranchGateApiView(step.BranchGate.SourceStepKey, step.BranchGate.RequiredOutcomeKey));
    }

    private static ProcessLaunchReadinessFindingApiView MapReadinessFinding(ProcessLaunchReadinessFinding finding)
    {
        return new ProcessLaunchReadinessFindingApiView(
            finding.Severity.ToString(),
            finding.Code,
            finding.Message,
            finding.StepKey,
            finding.RoleKey);
    }

    private static ProcessLiveRunApiView MapLiveRun(ProcessLiveProcessSnapshot run)
    {
        return new ProcessLiveRunApiView(
            run.RootRunId.Value,
            run.RunId.Value,
            run.Status.ToString(),
            run.IsActive,
            run.FirstEventAtUtc,
            run.LastEventAtUtc,
            MapFreshness(run.Freshness),
            run.RecentEvents.Select(MapLiveEvent).ToArray(),
            MapCurrentStep(run.CurrentStep),
            NormalizeChildRunWaits(run).Select(MapChildRunWait).ToArray());
    }

    private static ProcessRunDetailApiView MapRunDetail(ProcessRunDetailProjection detail)
    {
        return new ProcessRunDetailApiView(
            detail.RootRunId.Value,
            detail.RunId.Value,
            detail.Status.ToString(),
            detail.FirstEventAtUtc,
            detail.LastEventAtUtc,
            MapFreshness(detail.Freshness),
            detail.RecentEvents.Select(MapLiveEvent).ToArray());
    }

    private static ProcessLiveEventApiView MapLiveEvent(ProcessLiveRunEventProjection runtimeEvent)
    {
        return new ProcessLiveEventApiView(
            runtimeEvent.EventId.Value,
            runtimeEvent.GlobalSequence,
            runtimeEvent.RootRunId.Value,
            runtimeEvent.RunId.Value,
            runtimeEvent.EventType,
            runtimeEvent.OccurredAtUtc,
            runtimeEvent.Sensitivity.ToString(),
            runtimeEvent.Summary,
            runtimeEvent.RestrictedDiagnosticReference);
    }

    private static ProcessChildRunWaitApiView MapChildRunWait(ProcessRuntimeChildRunWaitProjection wait)
    {
        return new ProcessChildRunWaitApiView(
            wait.ParentRunId,
            wait.ParentStepInstanceId,
            wait.ParentStepKey,
            wait.ParentStepStatus,
            wait.ChildRunId,
            wait.ChildRunStatus,
            wait.ChildStepKey,
            wait.ChildStepStatus,
            wait.Summary);
    }

    private static ProcessCurrentStepApiView? MapCurrentStep(ProcessRuntimeCurrentStepProjection? step)
    {
        return step is null
            ? null
            : new ProcessCurrentStepApiView(
                step.RunId,
                step.StepInstanceId,
                step.StepKey,
                step.StepStatus,
                step.RoleKey,
                step.RoleDisplayName,
                step.ExecutorDisplayName,
                step.AttemptNumber,
                step.IsWorking,
                step.IsLeaseExpired,
                step.UpdatedAtUtc,
                step.ClaimedAtUtc,
                step.LeaseExpiresAtUtc,
                step.Summary);
    }

    private static IReadOnlyList<ProcessRuntimeChildRunWaitProjection> NormalizeChildRunWaits(ProcessLiveProcessSnapshot run)
        => run.WaitingOnChildRuns ?? [];

    private static ProcessTimelineEventApiView MapTimelineEvent(ProcessTimelineEventProjection runtimeEvent)
    {
        return new ProcessTimelineEventApiView(
            runtimeEvent.EventId.Value,
            runtimeEvent.GlobalSequence,
            runtimeEvent.RootRunId.Value,
            runtimeEvent.RunId.Value,
            runtimeEvent.EventType,
            runtimeEvent.OccurredAtUtc,
            runtimeEvent.Sensitivity.ToString(),
            runtimeEvent.Summary,
            runtimeEvent.RestrictedDiagnosticReference);
    }

    private static ProcessProjectionFreshnessApiView? MapFreshness(ProcessProjectionFreshness? freshness)
    {
        return freshness is null
            ? null
            : new ProcessProjectionFreshnessApiView(
                freshness.ObservedAtUtc,
                freshness.SourceGlobalSequence,
                freshness.Lag.LatestKnownGlobalSequence,
                freshness.Lag.LastProcessedGlobalSequence,
                freshness.Lag.BacklogEventCount);
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
}

internal sealed record ProcessApiContractResponse(
    IReadOnlyList<string> Endpoints,
    string BoundarySummary);

internal sealed record ProcessLaunchApiRequest(
    string? DefinitionKey = null,
    Guid? ProcessDefinitionId = null,
    string? LiveRunProfileKey = null,
    Guid? ProjectId = null,
    string? ProjectNodeId = null,
    string RequestedBy = "process-api",
    Dictionary<string, string>? Variables = null,
    bool RunReadiness = true,
    bool Execute = false);

internal sealed record ProcessDispatchApiRequest(string RequestedBy = "process-api");

internal sealed record ProcessRuntimeCancelApiRequest(
    string RequestedBy = "process-api",
    string Reason = "Operator requested process run cancellation.");

internal sealed record ProcessRuntimeReworkApiRequest(
    string RequestedBy = "process-api",
    string Reason = "Operator requested process step rework.");

internal sealed record ProcessLaunchApiResponse(
    Guid DefinitionId,
    Guid LaunchPlanId,
    Guid? RunId,
    string Stage,
    string Route,
    ProcessLaunchPlanApiView LaunchPlan,
    IReadOnlyList<string> Warnings);

internal sealed record ProcessLaunchPlanApiView(
    Guid PlanId,
    Guid DefinitionId,
    Guid DefinitionVersionId,
    string DefinitionKey,
    string DefinitionName,
    string DefinitionSummary,
    string? LiveRunProfileKey,
    string PlanHash,
    IReadOnlyList<ProcessLaunchStepApiView> Steps,
    IReadOnlyList<ProcessLaunchReadinessFindingApiView> ReadinessFindings);

internal sealed record ProcessLaunchStepApiView(
    Guid StepInstanceId,
    string StepKey,
    string Title,
    string RoleKey,
    string ExecutorKind,
    string ExecutorId,
    string ExecutorDisplayName,
    bool IsBlocked,
    string? BlockedReason,
    ProcessRuntimeBranchGateApiView? BranchGate);

internal sealed record ProcessRuntimeBranchGateApiView(
    string SourceStepKey,
    string RequiredOutcomeKey);

internal sealed record ProcessLaunchReadinessFindingApiView(
    string Severity,
    string Code,
    string Message,
    string? StepKey,
    string? RoleKey);

internal sealed record ProcessDispatchApiResponse(
    Guid RunId,
    string Stage,
    string Status,
    IReadOnlyList<string> Diagnostics);

internal sealed record ProcessRuntimeReworkApiResponse(
    Guid RunId,
    Guid StepInstanceId,
    string Kind,
    string Outcome,
    string Status,
    IReadOnlyList<string> Diagnostics);

internal sealed record ProcessRuntimeCancelApiResponse(
    Guid RunId,
    string Kind,
    string Outcome,
    string Status,
    IReadOnlyList<string> Diagnostics);

internal sealed record ProcessLiveApiResponse(
    IReadOnlyList<ProcessLiveRunApiView> Runs,
    ProcessProjectionFreshnessApiView? Freshness);

internal sealed record ProcessLiveRunApiView(
    Guid RootRunId,
    Guid RunId,
    string Status,
    bool IsActive,
    DateTimeOffset FirstEventAtUtc,
    DateTimeOffset LastEventAtUtc,
    ProcessProjectionFreshnessApiView? Freshness,
    IReadOnlyList<ProcessLiveEventApiView> RecentEvents,
    ProcessCurrentStepApiView? CurrentStep,
    IReadOnlyList<ProcessChildRunWaitApiView> WaitingOnChildRuns);

internal sealed record ProcessCurrentStepApiView(
    Guid RunId,
    Guid StepInstanceId,
    string StepKey,
    string StepStatus,
    string RoleKey,
    string RoleDisplayName,
    string ExecutorDisplayName,
    int AttemptNumber,
    bool IsWorking,
    bool IsLeaseExpired,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ClaimedAtUtc,
    DateTimeOffset? LeaseExpiresAtUtc,
    string Summary);

internal sealed record ProcessChildRunWaitApiView(
    Guid ParentRunId,
    Guid ParentStepInstanceId,
    string ParentStepKey,
    string ParentStepStatus,
    Guid ChildRunId,
    string ChildRunStatus,
    string? ChildStepKey,
    string? ChildStepStatus,
    string Summary);

internal sealed record ProcessRunDetailApiView(
    Guid RootRunId,
    Guid RunId,
    string Status,
    DateTimeOffset FirstEventAtUtc,
    DateTimeOffset LastEventAtUtc,
    ProcessProjectionFreshnessApiView? Freshness,
    IReadOnlyList<ProcessLiveEventApiView> RecentEvents);

internal sealed record ProcessHistoryApiResponse(
    IReadOnlyList<ProcessTimelineEventApiView> Events,
    ProcessProjectionFreshnessApiView? Freshness);

internal sealed record ProcessLiveEventApiView(
    Guid EventId,
    long GlobalSequence,
    Guid RootRunId,
    Guid RunId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string Sensitivity,
    string Summary,
    string? RestrictedDiagnosticReference);

internal sealed record ProcessTimelineEventApiView(
    Guid EventId,
    long GlobalSequence,
    Guid RootRunId,
    Guid RunId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string Sensitivity,
    string Summary,
    string? RestrictedDiagnosticReference);

internal sealed record ProcessProjectionFreshnessApiView(
    DateTimeOffset ObservedAtUtc,
    long SourceGlobalSequence,
    long LatestKnownGlobalSequence,
    long LastProcessedGlobalSequence,
    int BacklogEventCount);
