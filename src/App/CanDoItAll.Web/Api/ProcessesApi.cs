using CanDoItAll.Modules.Workspace.ApiAccess;
using System.Security.Claims;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Web.Api;

internal static class ProcessesApi
{
    public static RouteGroupBuilder MapProcessesApi(this RouteGroupBuilder group)
    {
        var processes = group.MapGroup("/processes").WithApiSection(ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.WriteProcesses)
            .WithTags("Processes")
            .DisableAntiforgery();
        processes.MapProcessRunRecordsApi();

        processes.MapGet("/contract", GetContract)
            .WithName("GetProcessesApiContract")
            .Produces<ProcessApiContractResponse>();

        processes.MapProcessDefinitionsApi();

        processes.MapPost("/launch/check", CheckLaunchAsync)
            .WithName("CheckProcessLaunch").WithApiPermission(ApiAccessScopeNames.ExecuteProcesses)
            .Produces<ProcessLaunchApiResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status409Conflict);

        processes.MapPost("/launch", LaunchAsync)
            .WithName("LaunchProcess").WithApiPermission(ApiAccessScopeNames.ExecuteProcesses)
            .Produces<ProcessLaunchApiResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status409Conflict);

        processes.MapGet("/launch/{admissionId:guid}", GetLaunchStatusAsync)
            .WithName("GetProcessLaunchStatus")
            .Produces<ProcessLaunchObservationApiView>()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        processes.MapPost("/runs/{runId:guid}/dispatch", DispatchRunAsync)
            .WithName("DispatchProcessRun").WithApiPermission(ApiAccessScopeNames.ExecuteProcesses)
            .Produces<ProcessDispatchApiResponse>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        processes.MapPost("/runs/{runId:guid}/cancel", CancelRunAsync)
            .WithName("CancelProcessRun").WithApiPermission(ApiAccessScopeNames.ExecuteProcesses)
            .Produces<ProcessRuntimeCancelApiResponse>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        processes.MapPost("/runs/{runId:guid}/steps/{stepInstanceId:guid}/rework", RequestStepReworkAsync)
            .WithName("RequestProcessStepRework").WithApiPermission(ApiAccessScopeNames.ExecuteProcesses)
            .Produces<ProcessRuntimeReworkApiResponse>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        processes.MapGet("/live", ListLiveAsync)
            .WithName("ListLiveProcesses")
            .Produces<ProcessLiveApiResponse>();

        processes.MapGet("/runs/{runId:guid}", GetRunAsync)
            .WithName("GetProcessRun")
            .Produces<ProcessRunDetailApiView>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        processes.MapGet("/runs/{runId:guid}/history", GetRunHistoryAsync)
            .WithName("GetProcessRunHistory")
            .Produces<ProcessHistoryApiResponse>();

        return group;
    }

    /// <summary>
    /// List the HTTP routes of the process API.
    /// </summary>
    /// <remarks>
    /// Returns a fixed list of the process routes that this host serves, as <c>METHOD /path</c> text, and a short
    /// informational note. The list is compiled into the server: it does not depend on authorization, configuration
    /// or runtime readiness and says nothing about request or response shapes, which this OpenAPI document describes.
    /// The operation reads and changes no process state.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; otherwise the route
    /// is open.
    /// </remarks>
    /// <response code="200">The route list and the informational note.</response>
    internal static IResult GetContract()
        => Results.Ok(new ProcessApiContractResponse(
            [
                "GET /api/processes/contract",
                "GET /api/processes/definitions",
                "GET /api/processes/definitions/{definitionKey}",
                "GET /api/processes/definitions/{definitionKey}/roles",
                "GET /api/processes/definitions/{definitionKey}/steps",
                "POST /api/processes/launch/check",
                "POST /api/processes/launch",
                "GET /api/processes/launch/{admissionId}",
                "POST /api/processes/runs/{runId}/dispatch",
                "POST /api/processes/runs/{runId}/cancel",
                "POST /api/processes/runs/{runId}/steps/{stepInstanceId}/rework",
                "GET /api/processes/live",
                "GET /api/processes/runs",
                "GET /api/processes/runs/analytics",
                "GET /api/processes/runs/{runId}",
                "GET /api/processes/runs/{runId}/summary",
                "GET /api/processes/runs/{runId}/graph",
                "GET /api/processes/runs/{runId}/history",
                "GET /api/processes/events/stream",
                "GET /api/processes/runs/{runId}/events/stream"
            ],
            "Runtime/core/dispatch remain generic. Module adapters resolve CanDoItAll agent execution through process driver strategies."));

    /// <summary>
    /// Prepare a process launch for review and save it, without creating a run.
    /// </summary>
    /// <remarks>
    /// Selects the process definition and live-run profile, compiles an immutable launch plan (steps, assigned agents
    /// or workflows, branch gates) and evaluates its readiness, then saves the result as a durable preparation. No run
    /// is created and nothing is dispatched, even when <c>execute</c> is true. Use it to review what a launch would
    /// do; <c>POST /api/processes/launch</c> is the operation that accepts a run.
    ///
    /// Results, all with HTTP 200:
    ///
    /// - Prepared: <c>stage</c> <c>Planned</c>, <c>runId</c> null, and <c>observation</c> with the
    /// <c>admissionId</c> of the saved preparation (<c>continuationState</c> <c>Prepared</c>). Keep the admission
    /// identifier for <c>GET /api/processes/launch/{admissionId}</c>; only a repeated check or launch with the same
    /// intent and input returns it again.
    /// - Blocked: <c>stage</c> <c>Blocked</c>, <c>observation</c> null, and the reasons in <c>warnings</c> and
    /// <c>launchPlan.readinessFindings</c>. Nothing was saved.
    /// - Already launched: when the preparation identified by <c>callerIntentId</c> or <c>preparedAdmissionId</c> has
    /// since been launched, the response reports its run (<c>runId</c>, <c>stage</c>) instead of preparing again.
    ///
    /// Retries: generate a <c>callerIntentId</c> once per intended launch and send it with the check, the launch and
    /// every retry. A repeated check with the same intent and identical input returns the same preparation; a launch
    /// with the same intent and input then accepts exactly this reviewed plan. Different input under the same intent
    /// is rejected with HTTP 409. Without an intent, every check saves a new preparation with a new plan.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy. The preparation is
    /// bound to the token's subject: only the same subject can launch or observe it, and the launch must happen before
    /// the token used here expires. With authorization disabled, every API caller acts as the same local operator. A
    /// <c>projectId</c> must name an existing project.
    /// </remarks>
    /// <param name="request">
    /// The launch request: definition, optional profile, project and node, variables, readiness option and retry
    /// identifiers.
    /// </param>
    /// <response code="200">
    /// The preparation was saved (<c>stage</c> <c>Planned</c>), readiness blocked it (<c>stage</c> <c>Blocked</c>,
    /// nothing saved), or the identified preparation had already been launched.
    /// </response>
    /// <response code="400">
    /// The launch could not be prepared (<c>process.launch_check_failed</c>; the cause is logged on the server, not
    /// returned). Typical causes: an unknown <c>definitionKey</c> or <c>liveRunProfileKey</c>, a profile of another
    /// definition, an unknown <c>projectNodeId</c>, an all-zero identifier, an unknown <c>preparedAdmissionId</c>,
    /// or a <c>callerIntentId</c> or <c>preparedAdmissionId</c> that belongs to another caller. A body the framework
    /// cannot bind (missing, malformed or not JSON) is rejected with HTTP 400 without this envelope.
    /// </response>
    /// <response code="403">
    /// Launch authority was rejected: the bearer token has no <c>sub</c> or <c>exp</c> claim, or the project named
    /// by <c>projectId</c> does not exist. No JSON error envelope; the host may return its HTML status page.
    /// </response>
    /// <response code="409">
    /// <c>process.launch_intent_conflict</c>: the <c>callerIntentId</c> or <c>preparedAdmissionId</c> already
    /// identifies a preparation with different input. Nothing new was saved. Resend the original input, or use a new
    /// intent for a different launch.
    /// </response>
    internal static async Task<IResult> CheckLaunchAsync(
        ProcessLaunchApiRequest request,
        HttpContext context,
        IProcessLaunchOperatorAuthoritySource authoritySource,
        IProcessPreparedLaunchStore preparations,
        ProcessLaunchApplicationService launchService,
        IProcessLaunchVariablePreparer launchVariablePreparationService,
        ProjectStructureProcessNodeService projectStructureProcessNodeService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
        => await ExecuteLaunchOperationAsync(
            request,
            context,
            authoritySource,
            preparations,
            launchService,
            launchVariablePreparationService,
            projectStructureProcessNodeService,
            loggerFactory,
            previewOnly: true,
            cancellationToken);

    /// <summary>
    /// Accept a process run, preparing the launch first or reusing a reviewed preparation.
    /// </summary>
    /// <remarks>
    /// Prepares the launch exactly like <c>POST /api/processes/launch/check</c>, or reuses the preparation identified
    /// by <c>callerIntentId</c> or <c>preparedAdmissionId</c>, and then commits the run: run state, immutable plan,
    /// step assignments and the first runtime events are saved together and the admission becomes accepted. After
    /// the commit the run is activated and its ready steps are scheduled.
    ///
    /// - <c>execute</c> true also queues the accepted run for background dispatch. The response does not wait for
    /// any step to execute.
    /// - <c>execute</c> false (the default) accepts and activates the run without queuing it. When the host runs its
    /// background dispatch recovery scan, an active run with ready steps can still be dispatched later, so do not use
    /// it to keep a run paused; use the check operation to review a launch without a run.
    ///
    /// Results, all with HTTP 200: <c>runId</c>, <c>route</c> and <c>observation</c> describe the accepted run.
    /// <c>stage</c> is usually <c>Running</c>. <c>Planned</c> means that the run was accepted but activation and
    /// scheduling are still pending; the host's continuation worker or a retry with the same intent completes them.
    /// <c>Failed</c> means that the commit was rejected (<c>runId</c> null) or that the run could not be activated.
    /// <c>Blocked</c> means that readiness blocked the launch and nothing was saved. <c>warnings</c> carries the
    /// reasons.
    ///
    /// Retries: a launch repeated with the same <c>callerIntentId</c> (or <c>preparedAdmissionId</c>) and identical
    /// input returns the same run and creates nothing new. Different input, or a different <c>execute</c> value
    /// after the run was accepted, is rejected with HTTP 409. Without these identifiers every call creates a new
    /// preparation and a new run, and after a lost response you cannot tell whether a run was created: retry only
    /// with the same intent, or read <c>GET /api/processes/launch/{admissionId}</c> when you have the admission
    /// identifier. An HTTP 400 or 403 does not prove that no preparation was saved.
    ///
    /// Next: follow the run with <c>GET /api/processes/runs/{runId}</c>, <c>GET /api/processes/live</c> or
    /// <c>GET /api/processes/runs/{runId}/events/stream</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy. The preparation is
    /// bound to the token's subject, and the authority saved with it (including the expiry of the token that created
    /// it) is checked again when the run is accepted. With authorization disabled, every API caller acts as the same
    /// local operator. A <c>projectId</c> must name an existing project whose lifetime has not changed since the
    /// preparation.
    /// </remarks>
    /// <param name="request">
    /// The launch request: definition, optional profile, project and node, variables, readiness option, the
    /// <c>execute</c> choice and retry identifiers.
    /// </param>
    /// <response code="200">
    /// The run was accepted (<c>runId</c> set), or the result explains why not (<c>stage</c> <c>Blocked</c> or
    /// <c>Failed</c>).
    /// </response>
    /// <response code="400">
    /// The launch could not be completed (<c>process.launch_failed</c>; the cause is logged on the server, not
    /// returned). Typical causes: an unknown <c>definitionKey</c> or <c>liveRunProfileKey</c>, a profile of another
    /// definition, an unknown <c>projectNodeId</c>, an all-zero identifier, an unknown <c>preparedAdmissionId</c>, an
    /// intent or admission of another caller, or a project whose lifetime changed after the preparation. A body the
    /// framework cannot bind (missing, malformed or not JSON) is rejected with HTTP 400 without this envelope.
    /// </response>
    /// <response code="403">
    /// Launch authority was rejected: the bearer token has no <c>sub</c> or <c>exp</c> claim, the project named by
    /// <c>projectId</c> does not exist, or the authority saved with the preparation is no longer valid (for example
    /// its token expired or the API authorization settings changed). No JSON error envelope; the host may return its
    /// HTML status page.
    /// </response>
    /// <response code="409">
    /// <c>process.launch_intent_conflict</c>: the <c>callerIntentId</c> or <c>preparedAdmissionId</c> already
    /// identifies a preparation with different input, or an accepted run with a different <c>execute</c> value.
    /// Nothing new was saved. Resend the original input, or use a new intent for a different launch.
    /// </response>
    internal static async Task<IResult> LaunchAsync(
        ProcessLaunchApiRequest request,
        HttpContext context,
        IProcessLaunchOperatorAuthoritySource authoritySource,
        IProcessPreparedLaunchStore preparations,
        ProcessLaunchApplicationService launchService,
        IProcessLaunchVariablePreparer launchVariablePreparationService,
        ProjectStructureProcessNodeService projectStructureProcessNodeService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
        => await ExecuteLaunchOperationAsync(
            request,
            context,
            authoritySource,
            preparations,
            launchService,
            launchVariablePreparationService,
            projectStructureProcessNodeService,
            loggerFactory,
            previewOnly: false,
            cancellationToken);

    /// <summary>
    /// Read whether a launch preparation was accepted and how far its continuation got.
    /// </summary>
    /// <remarks>
    /// Returns the saved state of a preparation created by <c>POST /api/processes/launch/check</c> or
    /// <c>POST /api/processes/launch</c>: whether a run was accepted (<c>acceptedRunId</c>), how far activation and
    /// scheduling got (<c>continuationState</c>), the Project Structure link delivery state, and the current runtime
    /// status of the accepted run. It is a pure read: it launches, resumes, dispatches and relinks nothing. Use it
    /// after a lost launch response or to confirm an acceptance; use <c>GET /api/processes/runs/{runId}</c> for the
    /// run itself.
    ///
    /// Only the caller that prepared the launch can read it: another token subject, or with authorization disabled a
    /// preparation made in the user interface, gets HTTP 403.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy, with the
    /// preparation's subject; otherwise the route is open to the local operator.
    /// </remarks>
    /// <param name="admissionId">
    /// Identifier of the preparation: <c>observation.admissionId</c> from the check or launch response.
    /// </param>
    /// <response code="200">The saved launch state.</response>
    /// <response code="403">
    /// The preparation belongs to another caller or its saved state could not be read. No JSON error envelope; the
    /// host may return its HTML status page.
    /// </response>
    /// <response code="404">No preparation has this identifier (<c>process.launch_not_found</c>).</response>
    internal static async Task<IResult> GetLaunchStatusAsync(
        Guid admissionId,
        HttpContext context,
        IProcessLaunchOperatorAuthoritySource authoritySource,
        IProcessPreparedLaunchStore preparations,
        ProcessLaunchApplicationService launchService,
        CancellationToken cancellationToken)
    {
        var caller = await ResolveOperatorAsync(context, authoritySource, projectId: null, cancellationToken);
        if (await preparations.GetAsync(new(admissionId), cancellationToken) is null) {
            return ApiEndpointResults.NotFound("The process launch admission was not found.", "process.launch_not_found");
        }
        try {
            return Results.Ok(MapLaunchObservation(
                await launchService.GetLaunchStatusAsync(new(admissionId), caller, cancellationToken)));
        } catch (InvalidOperationException) {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
    }

    /// <summary>
    /// Execute the ready steps of a process run synchronously, inside this request.
    /// </summary>
    /// <remarks>
    /// Drives one run in the foreground: activates a run that has not started, expires lapsed step claims, and then
    /// claims and executes every ready step one after another. It repeats while further steps become ready (at most
    /// 200 passes), so a single call can run a whole sequence of steps. The call returns when no step is ready any
    /// more, when the run is blocked or has ended, or when a step is handed over to a workflow run or child run that
    /// continues on its own. Each agent or workflow step is awaited, bounded by the host's step execution timeout (60
    /// minutes by default; host setting <c>Processes:RuntimeDispatch:StepExecutionTimeout</c>), so the request can
    /// take a long time.
    ///
    /// Use it only when the run has ready work that nothing is progressing, for example a current step in status
    /// <c>Ready</c> in <c>GET /api/processes/live</c> on a host without the background dispatch worker. The call does
    /// not go through that worker's queue and does not take its per-run lock, so do not call it while the worker is
    /// progressing the same run.
    ///
    /// Nothing is executed for a run that has ended (Completed, Failed, Cancelled), is Blocked or is CancelRequested;
    /// the response then just reports its current state. An active run with no runnable path left is marked Blocked,
    /// and the runtime may then attempt its automatic blocked-run recovery. A step failure is reported in the body
    /// (for example <c>status</c> Failed), not as an HTTP error.
    ///
    /// Completed work is not executed again, but every call can execute newly ready steps, so a repeat is not free of
    /// effects: read the run before calling again. If the client disconnects, the step being executed is cancelled
    /// and its claim released; effects that the step had already caused are not rolled back. An unexpected runtime
    /// failure, such as a stored plan that no longer matches the run, is not mapped to an error envelope.
    ///
    /// Before responding, the server projects one bounded batch of pending runtime events into the read models. Read
    /// <c>GET /api/processes/runs/{runId}</c> and <c>/history</c> afterwards; that read-back, not this response, is
    /// the evidence of what happened.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the process run, for example <c>runId</c> returned by <c>POST /api/processes/launch</c>.
    /// </param>
    /// <param name="request">
    /// Optional label of the caller. The JSON object is required: send <c>{}</c> to use the default label.
    /// </param>
    /// <response code="200">
    /// The dispatch finished. <c>stage</c> and <c>status</c> describe the run afterwards and <c>diagnostics</c>
    /// explain what happened.
    /// </response>
    /// <response code="404">No process run has this identifier (<c>process.run_not_found</c>).</response>
    internal static async Task<IResult> DispatchRunAsync(
        Guid runId,
        ProcessDispatchApiRequest request,
        ProcessRuntimeDispatchApplicationService dispatchService,
        ProcessRuntimeProjectionCatchupService projectionCatchupService,
        CancellationToken cancellationToken)
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
        catch (ProcessRuntimeDispatchRunNotFoundException)
        {
            return ApiEndpointResults.NotFound(
                $"Process run '{runId:D}' was not found.",
                "process.run_not_found");
        }
    }

    /// <summary>
    /// Cancel a process run, including its child runs.
    /// </summary>
    /// <remarks>
    /// Cancels within this request rather than queuing a request. For a top-level run: the run becomes
    /// CancelRequested, every step that has not finished is cancelled together with its open claims, agent executions
    /// of the run that are active in this server are told to stop, the child runs (subprocesses) are cancelled, and
    /// the run finally becomes Cancelled. A child run is cancelled on its own, without its parent or siblings.
    /// Cancellation does not roll back what agents, workflows or tools have already done.
    ///
    /// Once the run exists, the outcome is reported in the body with HTTP 200:
    ///
    /// - <c>Applied</c>: the run is now Cancelled.
    /// - <c>Rejected</c>: nothing more was done. The run had already ended (Completed, Failed or Cancelled),
    /// concurrent changes kept the cancellation from being saved, or a child run could not be cancelled yet; in the
    /// last case the top-level run stays CancelRequested, <c>diagnostics</c> name the child runs, and you can call
    /// again.
    ///
    /// The <c>reason</c> is not stored with the run. Before responding, the server projects one bounded batch of
    /// pending runtime events into the read models; confirm the result with <c>GET /api/processes/runs/{runId}</c>
    /// and, when the run record exists, <c>GET /api/processes/runs/{runId}/summary</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the process run to cancel, for example <c>runId</c> returned by
    /// <c>POST /api/processes/launch</c>. For a child run, only that run is cancelled.
    /// </param>
    /// <param name="request">
    /// Optional caller label and reason. The JSON object is required: send <c>{}</c> to use the defaults.
    /// </param>
    /// <response code="200">
    /// The request was processed. Check <c>outcome</c>: only <c>Applied</c> means that the run was cancelled.
    /// </response>
    /// <response code="404">No process run has this identifier (<c>process.run_not_found</c>).</response>
    internal static async Task<IResult> CancelRunAsync(
        Guid runId,
        ProcessRuntimeCancelApiRequest request,
        ProcessRuntimeOperatorApplicationService operatorService,
        ProcessRuntimeProjectionCatchupService projectionCatchupService,
        CancellationToken cancellationToken)
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
        catch (ProcessRuntimeOperatorRunNotFoundException)
        {
            return ApiEndpointResults.NotFound(
                $"Process run '{runId:D}' was not found.",
                "process.run_not_found");
        }
    }

    /// <summary>
    /// Send one step of a process run back for rework, with an instruction for its executor.
    /// </summary>
    /// <remarks>
    /// Resets the step to Ready and makes the run Active again within this request. A run that had failed or was
    /// blocked is reactivated; a Completed or Cancelled run cannot be reworked. Only the named step is reset: steps
    /// that depend on it are not reset. The <c>reason</c> becomes the step's rework instruction and is given to the
    /// agent or workflow that executes the step next, so write a concrete correction and never include secrets. The
    /// step is then queued for background dispatch; it is not executed inside this request.
    ///
    /// Once the run exists, the outcome is reported in the body with HTTP 200:
    ///
    /// - <c>Applied</c>: the step was reset for rework.
    /// - <c>Duplicate</c>: the step was already Ready in an active run; the instruction was still updated and the
    /// step queued.
    /// - <c>Rejected</c>: nothing changed, and <c>diagnostics</c> say why: the step is not part of this run or is not
    /// executable, the run has completed or was cancelled, the step is claimed or running, the step is not Waiting,
    /// Blocked or Failed, its prerequisites are not satisfied, or another step of the run holds an open claim.
    ///
    /// Before responding, the server projects one bounded batch of pending runtime events into the read models; follow
    /// the step with <c>GET /api/processes/runs/{runId}</c> and <c>/history</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the process run that owns the step, as shown in <c>runId</c> of the step in the run's detail or
    /// live projection.
    /// </param>
    /// <param name="stepInstanceId">
    /// Identifier of the step within the run: <c>stepInstanceId</c> from the launch plan, the current step of
    /// <c>GET /api/processes/live</c> or the run's diagnostics and lineage.
    /// </param>
    /// <param name="request">
    /// Optional caller label and the rework instruction. The JSON object is required; send a specific
    /// <c>reason</c>.
    /// </param>
    /// <response code="200">
    /// The request was processed. Check <c>outcome</c>: <c>Applied</c> and <c>Duplicate</c> mean that the step is
    /// queued for rework; <c>Rejected</c> means that nothing changed.
    /// </response>
    /// <response code="404">No process run has this identifier (<c>process.run_not_found</c>).</response>
    internal static async Task<IResult> RequestStepReworkAsync(
        Guid runId,
        Guid stepInstanceId,
        ProcessRuntimeReworkApiRequest request,
        ProcessRuntimeOperatorApplicationService operatorService,
        ProcessRuntimeProjectionCatchupService projectionCatchupService,
        CancellationToken cancellationToken)
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
        catch (ProcessRuntimeOperatorRunNotFoundException)
        {
            return ApiEndpointResults.NotFound(
                $"Process run '{runId:D}' was not found.",
                "process.run_not_found");
        }
    }

    /// <summary>
    /// List process runs with recent activity, including their current step, from the live projection.
    /// </summary>
    /// <remarks>
    /// Returns the live projection of the process runs whose last runtime event occurred within the last
    /// <c>windowMinutes</c>, most recent activity first, at most <c>take</c> runs. Runs that have ended are included
    /// while their last event is inside the window. Child runs (subprocesses) are separate entries with their own
    /// <c>runId</c> and the <c>rootRunId</c> of their tree. For each run it shows the status, up to 20 recent events,
    /// the step that is being worked on or is next, the child runs it waits for, and current diagnostics.
    ///
    /// The live projection is updated as runtime events are projected and can lag behind the runtime; see
    /// <c>freshness</c>. It is not a durable run record: use <c>GET /api/processes/runs</c> for ended runs outside
    /// the window, and <c>GET /api/processes/runs/{runId}</c> for one run with its result lineage. The read changes
    /// nothing.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy, and every run is
    /// visible to that caller; otherwise the route is open.
    /// </remarks>
    /// <param name="take">
    /// Maximum number of runs, from 1 through 500. Omitted means 50; values outside the range are clamped.
    /// </param>
    /// <param name="windowMinutes">
    /// How long ago, in minutes, the last event of a run may have occurred, from 1 through 43,200 (30 days). Omitted
    /// means 240 (4 hours); values outside the range are clamped.
    /// </param>
    /// <response code="200">
    /// The runs, most recent activity first. An empty <c>runs</c> array means that no run had an event within the
    /// window.
    /// </response>
    internal static async Task<IResult> ListLiveAsync(
        int? take,
        int? windowMinutes,
        ProcessRuntimeProjectionQueryService queryService,
        CancellationToken cancellationToken)
    {
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
    }

    /// <summary>
    /// Read the live detail of one process run: status, recent events, current diagnostics and result lineage.
    /// </summary>
    /// <remarks>
    /// Returns the projected detail of a run of any age, including a child run: its status, first and last event
    /// times, up to 20 most recent events, the current diagnostics (from the latest results of steps that are now
    /// Blocked or Failed), and the lineage of every applied step result with its produced artifacts and recovery
    /// decision. Use it to follow a run after launch, dispatch, cancellation or rework. For an ended run,
    /// <c>GET /api/processes/runs/{runId}/summary</c> returns the durable record (hard facts, usage, costs and
    /// narrative); <c>/history</c> returns the event timeline and <c>/graph</c> the step dependencies.
    ///
    /// The detail is a projection, updated as runtime events are projected; <c>freshness</c> shows how current it is.
    /// A run that was just accepted can be missing until its first events are projected. The read changes nothing.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the process run: <c>runId</c> from <c>POST /api/processes/launch</c>, a <c>runs[].runId</c> of
    /// <c>GET /api/processes/live</c>, or a <c>childRunId</c> of a waiting step.
    /// </param>
    /// <response code="200">The run detail.</response>
    /// <response code="404">
    /// No projected detail exists for this identifier (<c>process.run_not_found</c>): the run does not exist, or its
    /// first events have not been projected yet.
    /// </response>
    internal static async Task<IResult> GetRunAsync(
        Guid runId,
        ProcessRuntimeProjectionQueryService queryService,
        CancellationToken cancellationToken)
    {
        var detail = await queryService
            .GetRunDetailAsync(new ProcessRunDetailQuery(new ProcessRunId(runId)), cancellationToken)
            .ConfigureAwait(false);

        return detail is null
            ? ApiEndpointResults.NotFound($"Process run '{runId:D}' was not found.", "process.run_not_found")
            : Results.Ok(MapRunDetail(detail));
    }

    /// <summary>
    /// Read the projected runtime events of one process run within a time window, oldest first.
    /// </summary>
    /// <remarks>
    /// Returns the events of this run whose <c>occurredAtUtc</c> is at or after <c>fromUtc</c> and before
    /// <c>toUtc</c>, in ascending <c>globalSequence</c> order. Events of child runs are not included; read each child
    /// run separately. By default the window is the 24 hours before now.
    ///
    /// At most <c>take</c> events are returned, and they are the oldest ones in the window. There is no continuation
    /// value: when <c>take</c> events are returned, more can exist, so request again with <c>fromUtc</c> set to the
    /// <c>occurredAtUtc</c> of the last event you received and skip events you already have by <c>eventId</c>.
    ///
    /// Restricted events are included with a generic summary. An unknown run returns an empty list, not HTTP 404.
    /// <c>freshness</c> is derived from the last returned event. The read changes nothing.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; otherwise the route
    /// is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the process run, for example <c>runId</c> from <c>POST /api/processes/launch</c>.
    /// </param>
    /// <param name="fromUtc">
    /// Inclusive start of the window, an instant with offset (converted to UTC). Omitted means 24 hours before
    /// <c>toUtc</c>. It must be earlier than <c>toUtc</c>; otherwise the request currently fails with a generic HTTP
    /// 500 that has no error envelope.
    /// </param>
    /// <param name="toUtc">Exclusive end of the window, an instant with offset. Omitted means now.</param>
    /// <param name="take">
    /// Maximum number of events, from 1 through 1,000. Omitted means 100; values outside the range are clamped.
    /// </param>
    /// <response code="200">
    /// The events in the window, oldest first. An empty <c>events</c> array means that there were none, or that the
    /// run is unknown.
    /// </response>
    internal static async Task<IResult> GetRunHistoryAsync(
        Guid runId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? take,
        ProcessRuntimeProjectionQueryService queryService,
        CancellationToken cancellationToken)
    {
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
    }

    private static async Task<IResult> ExecuteLaunchOperationAsync(
        ProcessLaunchApiRequest request,
        HttpContext context,
        IProcessLaunchOperatorAuthoritySource authoritySource,
        IProcessPreparedLaunchStore preparations,
        ProcessLaunchApplicationService launchService,
        IProcessLaunchVariablePreparer launchVariablePreparationService,
        ProjectStructureProcessNodeService projectStructureProcessNodeService,
        ILoggerFactory loggerFactory,
        bool previewOnly,
        CancellationToken cancellationToken)
    {
        try
        {
            var launchRequest = await MapLaunchRequestAsync(
                request,
                context,
                authoritySource,
                preparations,
                launchVariablePreparationService,
                projectStructureProcessNodeService,
                cancellationToken).ConfigureAwait(false);
            var result = await ExecuteMappedLaunchAsync(launchRequest, launchService, preparations, previewOnly, cancellationToken);

            return Results.Ok(MapLaunchResult(result));
        }
        catch (ProcessLaunchIntentConflictException) {
            return Results.Conflict(new ApiErrorResponse([
                new("process.launch_intent_conflict", "The process launch intent no longer matches its original input.", CanDoItAll.SharedKernel.ErrorSeverity.Error)
            ]));
        }
        catch (ProcessLaunchAuthorityRejectedException) {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loggerFactory
                .CreateLogger("CanDoItAll.Web.Api.ProcessesApi")
                .LogError(
                    "Process launch operation failed. Operation={Operation} DefinitionKey={DefinitionKey} LiveRunProfileKey={LiveRunProfileKey} ProjectId={ProjectId} ProjectNodeId={ProjectNodeId} Execute={Execute} RunReadiness={RunReadiness} FailureType={FailureType} Failure={Failure}",
                    previewOnly ? "check" : "launch",
                    request.DefinitionKey,
                    request.LiveRunProfileKey,
                    request.ProjectId,
                    request.ProjectNodeId,
                    request.Execute,
                    request.RunReadiness,
                    exception.GetType().Name,
                    ProcessPublicReceiptTextPolicy.NormalizePublicMessages([exception.Message]).SingleOrDefault()
                        ?? "No public diagnostic was available.");
            return ApiEndpointResults.BadRequest(
                previewOnly
                    ? "Process launch validation could not be completed."
                    : "Process launch could not be completed.",
                previewOnly ? "process.launch_check_failed" : "process.launch_failed");
        }
    }

    private static async Task<ProcessLaunchResult> ExecuteMappedLaunchAsync(ProcessLaunchRequest request,
        ProcessLaunchApplicationService service, IProcessPreparedLaunchStore preparations, bool previewOnly, CancellationToken cancellationToken) {
        try {
            return previewOnly ? await service.PreviewAsync(request with { Execute = false }, cancellationToken)
                : await service.LaunchAsync(request, cancellationToken);
        } catch (ProcessLaunchIntentConflictException) {
            var winner = await ProcessLaunchProducerRequests.FindConcurrentReplayAsync(request, preparations, cancellationToken);
            if (winner is null) {
                throw;
            }
            return previewOnly ? await service.PreviewAsync(winner with { Execute = false }, cancellationToken)
                : await service.LaunchAsync(winner, cancellationToken);
        }
    }

    private static async Task<ProcessLaunchRequest> MapLaunchRequestAsync(
        ProcessLaunchApiRequest request,
        HttpContext context,
        IProcessLaunchOperatorAuthoritySource authoritySource,
        IProcessPreparedLaunchStore preparations,
        IProcessLaunchVariablePreparer launchVariablePreparationService,
        ProjectStructureProcessNodeService projectStructureProcessNodeService,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var variables = request.Variables ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var input = new ProcessLaunchRequest(request.DefinitionKey,
            request.ProcessDefinitionId is { } definitionId ? new ProcessDefinitionId(definitionId) : null,
            request.LiveRunProfileKey, request.ProjectId, request.ProjectNodeId,
            string.IsNullOrWhiteSpace(request.RequestedBy) ? "process-api" : request.RequestedBy,
            variables, request.RunReadiness, request.Execute) {
            CallerIntentId = request.CallerIntentId is { } intent ? new(intent) : null,
            PreparedAdmissionId = request.PreparedAdmissionId is { } admission ? new(admission) : null
        };
        var caller = await ResolveOperatorAsync(context, authoritySource, projectId: null, cancellationToken);
        var replay = await ProcessLaunchProducerRequests.FindReplayAsync(input, caller, preparations, cancellationToken);
        if (replay is not null) {
            return replay;
        }
        var authority = input.ProjectId is null ? caller
            : await ResolveOperatorAsync(context, authoritySource, input.ProjectId, cancellationToken);
        var producerFingerprint = ProcessLaunchProducerRequests.InputFingerprint(input, caller);
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
                    launchVariablePreparationService,
                    cancellationToken).ConfigureAwait(false),
                StringComparer.Ordinal);
        }

        return input with {
            Variables = variables,
            Authority = authority,
            ProjectAdmission = authority.ProjectAdmission,
            ProducerInputFingerprint = producerFingerprint
        };
    }

    private static Task<ProcessLaunchAuthority> ResolveOperatorAsync(HttpContext context,
        IProcessLaunchOperatorAuthoritySource source, Guid? projectId, CancellationToken cancellationToken) {
        if (context.User.Identity?.IsAuthenticated != true) {
            return source.CaptureLocalAsync(projectId, ProcessLaunchOperatorSurface.Api, cancellationToken);
        }
        var subject = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(subject) || !long.TryParse(context.User.FindFirst("exp")?.Value,
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var expiry)) {
            throw new ProcessLaunchAuthorityRejectedException("The authenticated process request requires its validated subject and token expiry.");
        }
        return source.CaptureAuthenticatedAsync(projectId, subject, DateTimeOffset.FromUnixTimeSeconds(expiry), cancellationToken);
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
            result.Warnings) { Observation = result.Observation is { } observation ? MapLaunchObservation(observation) : null };
    }

    private static ProcessLaunchObservationApiView MapLaunchObservation(ProcessLaunchObservation observation)
        => new(observation.AdmissionId.Value, observation.AcceptedRunId?.Value, observation.ContinuationState.ToString(),
            observation.LinkDeliveryState.ToString(), observation.RuntimeStatus?.ToString(), observation.PublicFailure);

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
                : new ProcessRuntimeBranchGateApiView(step.BranchGate.SourceStepKey, step.BranchGate.RequiredOutcomeKey),
            step.WorkflowBinding is null
                ? null
                : new ProcessWorkflowBindingApiView(
                    step.WorkflowBinding.WorkflowId.Value,
                    step.WorkflowBinding.WorkflowVersionId?.Value,
                    step.WorkflowBinding.OutputMapping.ToString()));
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
            OrEmpty(run.RecentEvents).Select(MapLiveEvent).ToArray(),
            MapCurrentStep(run.CurrentStep),
            NormalizeChildRunWaits(run).Select(MapChildRunWait).ToArray(),
            OrEmpty(run.Diagnostics).Select(MapDiagnostic).ToArray());
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
            OrEmpty(detail.RecentEvents).Select(MapLiveEvent).ToArray(),
            OrEmpty(detail.Diagnostics).Select(MapDiagnostic).ToArray(),
            OrEmpty(detail.ResultLineage).Select(MapResultLineage).ToArray());
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
            runtimeEvent.RestrictedDiagnosticReference,
            OrEmpty(runtimeEvent.Diagnostics).Select(MapDiagnostic).ToArray());
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
                step.Summary,
                OrEmpty(step.Diagnostics).Select(MapDiagnostic).ToArray(),
                OrEmpty(step.ProducedArtifacts).Select(MapArtifactLineage).ToArray());
    }

    private static IReadOnlyList<ProcessRuntimeChildRunWaitProjection> NormalizeChildRunWaits(ProcessLiveProcessSnapshot run)
        => OrEmpty(run.WaitingOnChildRuns);

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
            runtimeEvent.RestrictedDiagnosticReference,
            OrEmpty(runtimeEvent.Diagnostics).Select(MapDiagnostic).ToArray());
    }

    private static ProcessRuntimeDiagnosticApiView MapDiagnostic(ProcessRuntimeDiagnosticProjection diagnostic)
    {
        return new ProcessRuntimeDiagnosticApiView(
            diagnostic.RunId,
            diagnostic.StepInstanceId,
            diagnostic.StepKey,
            diagnostic.StrategyId,
            diagnostic.ResultHash,
            diagnostic.Code,
            diagnostic.Category,
            diagnostic.SafeSummary,
            diagnostic.Sensitivity,
            diagnostic.RetrySafety,
            diagnostic.Idempotency,
            diagnostic.RestrictedDiagnosticReference,
            diagnostic.OperatorDetails is null
                ? null
                : new ProcessRuntimeOperatorDiagnosticDetailsApiView(
                    diagnostic.OperatorDetails.GateId,
                    diagnostic.OperatorDetails.BranchOutcomeKey,
                    diagnostic.OperatorDetails.RouteTargetBranchOutcomeKey,
                    diagnostic.OperatorDetails.FailedCriteriaIds,
                    diagnostic.OperatorDetails.ReceiptRuleIds,
                    diagnostic.OperatorDetails.NextAction));
    }

    private static ProcessRuntimeArtifactLineageApiView MapArtifactLineage(ProcessRuntimeArtifactLineageProjection artifact)
    {
        return new ProcessRuntimeArtifactLineageApiView(
            artifact.SlotId,
            artifact.ArtifactId,
            artifact.ContentHash);
    }

    private static ProcessRuntimeResultLineageApiView MapResultLineage(ProcessRuntimeResultLineageProjection result)
    {
        return new ProcessRuntimeResultLineageApiView(
            result.RunId,
            result.StepInstanceId,
            result.StepKey,
            result.StrategyId,
            result.IdempotencyKey,
            result.Outcome,
            result.AppliedStepStatus,
            result.ResultHash,
            OrEmpty(result.Diagnostics).Select(MapDiagnostic).ToArray(),
            OrEmpty(result.ProducedArtifacts).Select(MapArtifactLineage).ToArray(),
            result.RecoveryDecision is null
                ? null
                : new ProcessRuntimeRecoveryDecisionApiView(
                    result.RecoveryDecision.FailureCategory,
                    result.RecoveryDecision.DecisionKind,
                    result.RecoveryDecision.SourceDiagnosticCode,
                    result.RecoveryDecision.Policy,
                    result.RecoveryDecision.SafeReason));
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

    private static IReadOnlyList<T> OrEmpty<T>(IReadOnlyList<T>? items)
        => items ?? [];
}

/// <summary>
/// Fixed description of the process HTTP API returned by <c>GET /api/processes/contract</c>: which routes exist, not
/// their schemas.
/// </summary>
/// <param name="Endpoints">
/// Every process route as <c>METHOD /path</c> text with route parameters in braces, for example
/// <c>POST /api/processes/runs/{runId}/dispatch</c>. The order is fixed; it carries no meaning.
/// </param>
/// <param name="BoundarySummary">
/// Human-readable note on how the generic process runtime hands step execution to product adapters. Informational
/// text for operators; do not parse it.
/// </param>
internal sealed record ProcessApiContractResponse(
    IReadOnlyList<string> Endpoints,
    string BoundarySummary);

/// <summary>
/// Launch request of <c>POST /api/processes/launch/check</c> and <c>POST /api/processes/launch</c>. Every member is
/// optional, but the JSON object is required; send at least <c>definitionKey</c> and a <c>callerIntentId</c>. A retry
/// under the same intent must repeat every member except <c>execute</c> exactly.
/// </summary>
/// <param name="DefinitionKey">
/// Key of the process definition to launch, for example <c>blazor-app-delivery</c>; matched case-insensitively after
/// trimming, and it takes precedence over <c>processDefinitionId</c>. When neither member selects a definition, the
/// server falls back to the definition of the requested or first configured live-run profile, so always send one. An
/// unknown key fails the request.
/// </param>
/// <param name="ProcessDefinitionId">
/// Identifier of the process definition (<c>definitionId</c> of an earlier launch response), used only when
/// <c>definitionKey</c> is omitted or blank. An identifier that matches no definition is not rejected: the fallback
/// described for <c>definitionKey</c> applies. The all-zero GUID fails the request.
/// </param>
/// <param name="LiveRunProfileKey">
/// Key of a live-run profile, which chooses the roles, executor kinds and workflow bindings of the definition's steps,
/// for example <c>generic-blazor-wasm-pwa-app</c>; matched case-insensitively. It must belong to the selected
/// definition. Omitted means the first profile configured for that definition, if any; the applied key is returned
/// in <c>launchPlan.liveRunProfileKey</c>.
/// </param>
/// <param name="ProjectId">
/// Identifier of the project the run belongs to; omit it for a run without a project. The project must exist. The
/// run is bound to the project's current lifetime, the value becomes the <c>ProjectId</c> launch variable, and
/// <c>route</c> points to the project's page. The all-zero GUID fails the request.
/// </param>
/// <param name="ProjectNodeId">
/// String identifier of the Project Structure node the run is started for (<c>nodes[].id</c> of a structure read; not
/// a GUID in general). With <c>projectId</c>, the server reads the node and sets project and node variables such as
/// <c>ProjectName</c>, <c>ProjectNodeTitle</c> and <c>ProjectNodeStatus</c>, replacing caller values with the same
/// keys; an unknown node fails the request. Without <c>projectId</c> it is only stored as the <c>ProjectNodeId</c>
/// variable.
/// </param>
/// <param name="RequestedBy">
/// Free-text label of the requester, recorded on the run's events after reduction to letters, digits, <c>-</c>,
/// <c>_</c> and <c>.</c> (at most 128 characters). Omitted or blank means <c>process-api</c>. It is part of the retry
/// comparison but is not an identity: authority comes from the bearer token or the local caller.
/// </param>
/// <param name="Variables">
/// Launch variables as a JSON object of string values, for example <c>{"Goal":"Add a settings page"}</c>. Keys are
/// case-sensitive; blank keys are dropped and keys and values are trimmed. A value can reference another variable as
/// <c>{{Key}}</c>, <c>${Key}</c> or <c>{Key}</c>; an unresolved or cyclic reference can block the launch with a
/// readiness error. The server sets or replaces reserved keys, among them <c>ProjectId</c>, <c>ProjectNodeId</c>,
/// <c>ProcessDefinitionKey</c>, <c>ProcessDefinitionName</c> and the run and artifact-root variables. Omitted or null
/// means no caller variables.
/// </param>
/// <param name="RunReadiness">
/// True (default): readiness errors, for example a step without a suitable agent, block the launch. False: readiness
/// is still evaluated and reported, but ordinary executor errors do not block, and steps without an executor are
/// launched unassigned. Compile errors, step assignment errors and findings that must always block still block.
/// </param>
/// <param name="Execute">
/// Used by the launch operation only. True: queue the accepted run for background dispatch right away. False
/// (default): accept and activate the run without queuing it. A launch retried after the run was accepted must send
/// the original value. The check operation ignores it.
/// </param>
/// <param name="CallerIntentId">
/// Idempotency key that you generate once per intended launch (a new GUID) and send unchanged with the check, the
/// launch and every retry. The same intent with identical input returns the same preparation and, once launched, the
/// same run; different input under the same intent is rejected with HTTP 409. Omitted means that every call is a new
/// intent. The all-zero GUID fails the request.
/// </param>
/// <param name="PreparedAdmissionId">
/// Identifier of an existing preparation (<c>observation.admissionId</c>) to reuse; when present it is used instead of
/// <c>callerIntentId</c> to find the preparation. The input must be identical to the saved one. An unknown identifier
/// fails the request with HTTP 400.
/// </param>
internal sealed record ProcessLaunchApiRequest(
    string? DefinitionKey = null,
    Guid? ProcessDefinitionId = null,
    string? LiveRunProfileKey = null,
    Guid? ProjectId = null,
    string? ProjectNodeId = null,
    string RequestedBy = "process-api",
    Dictionary<string, string>? Variables = null,
    bool RunReadiness = true,
    bool Execute = false,
    Guid? CallerIntentId = null,
    Guid? PreparedAdmissionId = null);

/// <summary>
/// Body of <c>POST /api/processes/runs/{runId}/dispatch</c>. Every member is optional, but the JSON object itself is
/// required: send <c>{}</c> to use the defaults.
/// </summary>
/// <param name="RequestedBy">
/// Free-text label of the caller, used to label follow-up work that this dispatch queues. Omitted or blank means
/// <c>process-api</c>. It is not an identity: the runtime records its own actor and authorizes nothing by this value.
/// </param>
internal sealed record ProcessDispatchApiRequest(string RequestedBy = "process-api");

/// <summary>
/// Body of <c>POST /api/processes/runs/{runId}/cancel</c>. Every member is optional, but the JSON object itself is
/// required: send <c>{}</c> to use the defaults.
/// </summary>
/// <param name="RequestedBy">
/// Free-text label of the caller. Omitted or blank means <c>process-api</c>. It is not an identity: the runtime
/// records its own actor and authorizes nothing by this value.
/// </param>
/// <param name="Reason">
/// Why the run is cancelled. Omitted or blank means a generic default text. It is handed to the components that stop
/// active executions but is not stored with the run.
/// </param>
internal sealed record ProcessRuntimeCancelApiRequest(
    string RequestedBy = "process-api",
    string Reason = "Operator requested process run cancellation.");

/// <summary>
/// Body of <c>POST /api/processes/runs/{runId}/steps/{stepInstanceId}/rework</c>. The JSON object is required; its
/// members are optional, but a rework without a specific <c>reason</c> gives the step's executor no guidance.
/// </summary>
/// <param name="RequestedBy">
/// Free-text label of the caller, used to label the dispatch that is queued after the rework. Omitted or blank means
/// <c>process-api</c>. It is not an identity: the runtime records its own actor and authorizes nothing by this value.
/// </param>
/// <param name="Reason">
/// The rework instruction: the concrete correction the step's executor must make, citing current evidence. It
/// replaces the step's previous operator rework instruction and is given to the agent or workflow that executes the
/// step next. Only surrounding whitespace is removed; there is no length limit and no redaction, so never include
/// credentials or other secrets. Omitted or blank means a generic default text.
/// </param>
internal sealed record ProcessRuntimeReworkApiRequest(
    string RequestedBy = "process-api",
    string Reason = "Operator requested process step rework.");

/// <summary>
/// Result of a launch check or launch: the reviewed plan, the accepted run if there is one, and the saved admission
/// to observe.
/// </summary>
/// <param name="DefinitionId">
/// Identifier of the launched process definition, derived from its key; it stays the same across versions of the
/// definition.
/// </param>
/// <param name="LaunchPlanId">
/// Identifier of the immutable launch plan of this preparation; equals <c>launchPlan.planId</c>. A repeated check or
/// launch with the same intent returns the same plan.
/// </param>
/// <param name="RunId">
/// Identifier of the accepted run, for the <c>/api/processes/runs/{runId}</c> operations. Null for a check that has
/// not been launched, for a blocked result, and when the commit was rejected.
/// </param>
/// <param name="Stage">
/// Outcome stage, as text: <c>Planned</c> (the check succeeded, or a launch was accepted but activation and scheduling
/// are still pending), <c>Blocked</c> (readiness blocked the launch; nothing was saved), <c>Running</c> (the run is
/// active, waiting or being cancelled; also after a launch with <c>execute</c> false), <c>Completed</c>, or
/// <c>Failed</c> (the commit was rejected, the run could not be activated, or the run failed or was cancelled).
/// </param>
/// <param name="Route">
/// Relative path of the run's page in the web application, for example <c>/processes/live?runId={runId}</c> or
/// <c>/projects/{projectId}/processes/live?runId={runId}</c>; empty when there is no run. It is not an API route.
/// </param>
/// <param name="LaunchPlan">The reviewed plan: definition, steps with their executors, and readiness findings.</param>
/// <param name="Warnings">
/// Messages of readiness findings that are not informational (for a blocked result, the reasons), rejection
/// diagnostics of a failed commit, and a note when the continuation is pending or needs reconciliation. Text that
/// looks like credentials, URL query strings and local file paths is removed; at most 32 entries of at most 2,048
/// characters.
/// </param>
internal sealed record ProcessLaunchApiResponse(
    Guid DefinitionId,
    Guid LaunchPlanId,
    Guid? RunId,
    string Stage,
    string Route,
    ProcessLaunchPlanApiView LaunchPlan,
    IReadOnlyList<string> Warnings) {
    /// <summary>
    /// Saved admission state of the preparation, including the <c>admissionId</c> needed for
    /// <c>GET /api/processes/launch/{admissionId}</c>. Null when readiness blocked the launch and nothing was saved.
    /// </summary>
    public ProcessLaunchObservationApiView? Observation { get; init; }
}

/// <summary>
/// Saved state of a launch preparation (launch admission), returned by the launch check, the launch, and
/// <c>GET /api/processes/launch/{admissionId}</c>.
/// </summary>
/// <param name="AdmissionId">
/// Identifier of the saved preparation. Keep it to observe the launch and to reuse the preparation as
/// <c>preparedAdmissionId</c>.
/// </param>
/// <param name="AcceptedRunId">
/// Identifier of the run once a launch accepted the preparation; null while it is only prepared.
/// </param>
/// <param name="ContinuationState">
/// How far the launch got, as text: <c>Prepared</c> (saved by a check; no run), <c>Accepted</c> (the run is
/// committed; activation and scheduling are pending), <c>Continuing</c> (a worker is activating and scheduling the
/// run), <c>Started</c> (activation and scheduling finished), <c>Failed</c> (activation or scheduling failed; read the
/// run), or <c>ReconciliationRequired</c> (the saved launch authority was rejected, for example because its token
/// expired; the launch is not continued automatically).
/// </param>
/// <param name="LinkDeliveryState">
/// Delivery of the Project Structure link for launches started from a structure node, as text: <c>NotRequested</c>,
/// <c>Pending</c>, <c>Delivered</c>, <c>Removed</c> or <c>Conflict</c>. Launches through this API never request a
/// link, so it is <c>NotRequested</c> for them.
/// </param>
/// <param name="RuntimeStatus">
/// Runtime status of the accepted run when it was read, as text, for example <c>Active</c>, <c>Blocked</c>,
/// <c>Completed</c>, <c>Failed</c> or <c>Cancelled</c>. Null when no run was accepted or its state could not be read;
/// usually null in check and launch responses.
/// </param>
/// <param name="PublicFailure">
/// Fixed message shown when the saved launch authority needs reconciliation; null otherwise. It never contains
/// exception details.
/// </param>
internal sealed record ProcessLaunchObservationApiView(
    Guid AdmissionId, Guid? AcceptedRunId, string ContinuationState, string LinkDeliveryState, string? RuntimeStatus, string? PublicFailure);

/// <summary>
/// Reviewed launch plan of a preparation: which definition version is launched, with which steps and executors, and
/// whether it is ready.
/// </summary>
/// <param name="PlanId">
/// Identifier of the immutable plan; equals <c>launchPlanId</c>. Every new preparation compiles a new plan with new
/// step identifiers.
/// </param>
/// <param name="DefinitionId">Identifier of the process definition, derived from its key.</param>
/// <param name="DefinitionVersionId">
/// Identifier of the exact definition version; it changes when the definition's content changes.
/// </param>
/// <param name="DefinitionKey">Key of the launched definition, in the definition's own spelling.</param>
/// <param name="DefinitionName">Display name of the definition.</param>
/// <param name="DefinitionSummary">Short description of the definition.</param>
/// <param name="LiveRunProfileKey">
/// Key of the live-run profile that was applied, also when the server chose it; null when no profile was applied.
/// </param>
/// <param name="PlanHash">
/// Content hash of the compiled plan: <c>sha256:</c> followed by 64 lowercase hexadecimal characters; empty when the
/// plan could not be compiled. Equal hashes mean identical plan content.
/// </param>
/// <param name="Steps">Every step of the plan in plan order, including steps that are not executable.</param>
/// <param name="ReadinessFindings">
/// Readiness results, from informational notes to errors that block the launch.
/// </param>
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

/// <summary>
/// One step of a launch plan with the executor assigned to it.
/// </summary>
/// <param name="StepInstanceId">
/// Identifier of this step in this plan; it becomes the step's identifier in the accepted run (for example for step
/// rework). A new plan has new step identifiers.
/// </param>
/// <param name="StepKey">Key of the step in the process definition, for example <c>resolve-blazor-contract</c>.</param>
/// <param name="Title">Display title of the step; the step key when the definition gives no title.</param>
/// <param name="RoleKey">Key of the process role responsible for the step, for example <c>blazor-engineer</c>.</param>
/// <param name="ExecutorKind">
/// Kind of the assigned executor: <c>agent</c> or <c>workflow</c>; empty when no executor could be assigned.
/// </param>
/// <param name="ExecutorId">
/// Identifier of the assigned agent or workflow as a GUID string; empty when no executor is assigned.
/// </param>
/// <param name="ExecutorDisplayName">
/// Name of the assigned agent or workflow; empty when no executor is assigned.
/// </param>
/// <param name="IsBlocked">True when a readiness error concerns this step.</param>
/// <param name="BlockedReason">Message of that readiness error; null when the step is not blocked.</param>
/// <param name="BranchGate">
/// Present when the step runs only for one outcome of an earlier branching step; such a step waits blocked until that
/// outcome is chosen. Null otherwise.
/// </param>
/// <param name="WorkflowBinding">Workflow that executes the step; null for steps that are not workflow steps.</param>
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
    ProcessRuntimeBranchGateApiView? BranchGate,
    ProcessWorkflowBindingApiView? WorkflowBinding);

/// <summary>
/// Workflow that executes a process step.
/// </summary>
/// <param name="WorkflowId">Identifier of the workflow definition, as used by <c>/api/workflows</c>.</param>
/// <param name="WorkflowVersionId">
/// Exact workflow version that runs the step; null means the workflow's latest active version at execution time.
/// </param>
/// <param name="OutputMapping">
/// How the workflow result is used, as text. The only value is <c>ProcessStepOutcome</c>: the workflow's output JSON
/// is validated as the step's outcome.
/// </param>
internal sealed record ProcessWorkflowBindingApiView(
    Guid WorkflowId,
    Guid? WorkflowVersionId,
    string OutputMapping);

/// <summary>
/// Branch condition of a process step: the step runs only when an earlier step reports a specific outcome.
/// </summary>
/// <param name="SourceStepKey">Key of the earlier step whose reported outcome decides the branch.</param>
/// <param name="RequiredOutcomeKey">
/// Outcome key that the source step must report for this step to run, for example <c>repair-required</c>.
/// </param>
internal sealed record ProcessRuntimeBranchGateApiView(
    string SourceStepKey,
    string RequiredOutcomeKey);

/// <summary>
/// One readiness result of a launch plan.
/// </summary>
/// <param name="Severity">
/// <c>Info</c>, <c>Warning</c> or <c>Error</c>. Errors can block the launch (see <c>runReadiness</c> of the request);
/// informational findings never do.
/// </param>
/// <param name="Code">
/// Stable finding code, for example <c>process.launch.readiness_ok</c>, <c>process.launch.agent_missing</c>,
/// <c>process.launch.workflow_not_runnable</c> or <c>agent.readiness.tools-disabled</c>; at most 128 characters of
/// letters, digits, <c>-</c>, <c>_</c>, <c>.</c> and <c>:</c>.
/// </param>
/// <param name="Message">
/// Human-readable explanation. Text that looks like credentials, URL query strings and local file paths is removed;
/// at most 2,048 characters.
/// </param>
/// <param name="StepKey">Key of the step the finding concerns; null for findings about the whole plan.</param>
/// <param name="RoleKey">Key of the role the finding concerns; null when it concerns no specific role.</param>
internal sealed record ProcessLaunchReadinessFindingApiView(
    string Severity,
    string Code,
    string Message,
    string? StepKey,
    string? RoleKey);

/// <summary>
/// Result of <c>POST /api/processes/runs/{runId}/dispatch</c>: the state of the run when the dispatch stopped. It is
/// not a durable receipt; read the run to confirm what happened.
/// </summary>
/// <param name="RunId">Identifier of the dispatched process run (the route value).</param>
/// <param name="Stage">
/// Coarse stage of the run after the dispatch, as text: <c>Completed</c>; <c>Failed</c>, which is also used for a
/// cancelled run; <c>Blocked</c>; or <c>Running</c> for every other status, including a run that is still active,
/// waits for a workflow or child run, or is being cancelled. Dispatch never returns <c>Planned</c>.
/// </param>
/// <param name="Status">
/// Runtime status of the run after the dispatch, as text: <c>Created</c> (not started), <c>Active</c> (steps can
/// run), <c>Blocked</c> (no runnable path is left; needs attention), <c>CancelRequested</c> (cancellation of a
/// top-level run is in progress), or one of the terminal statuses <c>Completed</c>, <c>Failed</c> and
/// <c>Cancelled</c>. <c>Waiting</c>, <c>Escalated</c> and <c>WaitingForUser</c> are defined but not set by the
/// current runtime.
/// </param>
/// <param name="Diagnostics">
/// Human-readable sentences about what the dispatch did or why it stopped, for example a failed step, a hand-over to
/// a workflow or child run, or a retried concurrent change. They are not stable codes; do not parse them. Text that
/// looks like credentials, URL query strings and local file paths is removed; at most 32 entries of at most 2,048
/// characters. Empty when there is nothing to report.
/// </param>
internal sealed record ProcessDispatchApiResponse(
    Guid RunId,
    string Stage,
    string Status,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Result of <c>POST /api/processes/runs/{runId}/steps/{stepInstanceId}/rework</c>. <c>outcome</c> says whether the
/// step was reset; the HTTP status is 200 in every case once the run exists.
/// </summary>
/// <param name="RunId">Identifier of the process run (the route value).</param>
/// <param name="StepInstanceId">Identifier of the step within the run (the route value).</param>
/// <param name="Kind">The operator action that was processed: always <c>RequestRework</c> for this operation.</param>
/// <param name="Outcome">
/// <c>Applied</c> (the step was reset to Ready and queued), <c>Duplicate</c> (the step was already Ready in an active
/// run; its instruction was still updated and the run queued) or <c>Rejected</c> (nothing changed; see
/// <c>diagnostics</c>).
/// </param>
/// <param name="Status">
/// Runtime status of the run after the request, as text, with the values described for the dispatch response; after
/// a successful rework it is <c>Active</c>.
/// </param>
/// <param name="Diagnostics">
/// Human-readable explanations, in particular the reason for a rejection. Not stable codes; text that looks like
/// credentials, URL query strings and local file paths is removed; at most 32 entries.
/// </param>
internal sealed record ProcessRuntimeReworkApiResponse(
    Guid RunId,
    Guid StepInstanceId,
    string Kind,
    string Outcome,
    string Status,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Result of <c>POST /api/processes/runs/{runId}/cancel</c>. <c>outcome</c> says whether the run was cancelled; the
/// HTTP status is 200 in every case once the run exists.
/// </summary>
/// <param name="RunId">Identifier of the process run (the route value).</param>
/// <param name="Kind">The operator action that was processed: always <c>CancelRun</c> for this operation.</param>
/// <param name="Outcome">
/// <c>Applied</c> (the run is now Cancelled) or <c>Rejected</c> (the run had already ended, concurrent changes kept
/// the cancellation from being saved, or child runs could not be cancelled yet; see <c>diagnostics</c>).
/// </param>
/// <param name="Status">
/// Runtime status of the run after the request, as text, with the values described for the dispatch response:
/// <c>Cancelled</c> after success, <c>CancelRequested</c> while child runs still block the cancellation, or the
/// unchanged status after other rejections.
/// </param>
/// <param name="Diagnostics">
/// Human-readable explanations, for example how many active agent executions were told to stop or which child runs
/// are still pending. Not stable codes; text that looks like credentials, URL query strings and local file paths is
/// removed; at most 32 entries.
/// </param>
internal sealed record ProcessRuntimeCancelApiResponse(
    Guid RunId,
    string Kind,
    string Outcome,
    string Status,
    IReadOnlyList<string> Diagnostics);

/// <summary>
/// Response of <c>GET /api/processes/live</c>: process runs with recent activity, from the live projection.
/// </summary>
/// <param name="Runs">
/// Runs whose last event lies within the requested window, ordered by last event time, newest first; at most
/// <c>take</c> entries.
/// </param>
/// <param name="Freshness">
/// Freshness of the most recently projected run in the list; null when the list is empty.
/// </param>
internal sealed record ProcessLiveApiResponse(
    IReadOnlyList<ProcessLiveRunApiView> Runs,
    ProcessProjectionFreshnessApiView? Freshness);

/// <summary>
/// Live projection of one process run in <c>GET /api/processes/live</c>.
/// </summary>
/// <param name="RootRunId">
/// Identifier of the top-level run of the run's subprocess tree; equals <c>runId</c> for a top-level run.
/// </param>
/// <param name="RunId">Identifier of this run, for the <c>/api/processes/runs/{runId}</c> operations.</param>
/// <param name="Status">
/// Projected status, as text, derived from the latest projected events: <c>Active</c> (running or waiting),
/// <c>NeedsAttention</c> (the run or a step is blocked, rework was requested, or the process manager raised an
/// incident, escalated a loop budget, denied a recovery or rejected a branch decision), <c>Completed</c>,
/// <c>Failed</c> or <c>Cancelled</c>. <c>Unknown</c> is defined but not produced.
/// </param>
/// <param name="IsActive">
/// True for an <c>Active</c> run. For a <c>NeedsAttention</c> run, true only while one of its steps holds an
/// unexpired claim, that is, while work is actually in progress. False for ended runs.
/// </param>
/// <param name="FirstEventAtUtc">Instant of the first projected runtime event of this run.</param>
/// <param name="LastEventAtUtc">
/// Instant of the most recently projected runtime event of this run; the time window of the request applies to it.
/// </param>
/// <param name="Freshness">How current this run's projection is.</param>
/// <param name="RecentEvents">Up to 20 most recent events of this run, oldest first.</param>
/// <param name="CurrentStep">
/// The step that is executing or next in line; null when the run is neither active nor in need of attention, or when
/// no step qualifies.
/// </param>
/// <param name="WaitingOnChildRuns">
/// Child runs that steps of this run are waiting for and that have not ended or become blocked. Empty when there are
/// none, or when the run is neither active nor in need of attention.
/// </param>
/// <param name="Diagnostics">
/// Current diagnostics of the run: those of the latest results of steps that are now Blocked or Failed. Empty when
/// there are none, or when the run is neither active nor in need of attention.
/// </param>
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
    IReadOnlyList<ProcessChildRunWaitApiView> WaitingOnChildRuns,
    IReadOnlyList<ProcessRuntimeDiagnosticApiView> Diagnostics);

/// <summary>
/// The step of a live run that is executing or next in line, computed from the runtime state when the live projection
/// is read.
/// </summary>
/// <param name="RunId">Identifier of the run that owns the step.</param>
/// <param name="StepInstanceId">
/// Identifier of the step within the run; use it with the step rework operation.
/// </param>
/// <param name="StepKey">
/// Key of the step in the process definition, or the step identifier as text when the step has no assignment.
/// </param>
/// <param name="StepStatus">
/// Runtime status of the step, as text: <c>Running</c>, <c>Claimed</c>, <c>Waiting</c>, <c>Blocked</c> (after at
/// least one attempt), <c>Ready</c> or <c>Pending</c>. When several steps qualify, the first status in this order is
/// chosen.
/// </param>
/// <param name="RoleKey">Key of the process role assigned to the step, or <c>unassigned</c>.</param>
/// <param name="RoleDisplayName">Display name of that role; the role key when no name is known.</param>
/// <param name="ExecutorDisplayName">
/// Display name of the assigned agent or workflow, or <c>Unassigned executor</c>.
/// </param>
/// <param name="AttemptNumber">
/// Execution attempt of the step: 0 before its first dispatch, then counted from 1.
/// </param>
/// <param name="IsWorking">True while the step is Claimed or Running under an open, unexpired claim.</param>
/// <param name="IsLeaseExpired">True when the step's active claim has passed its expiry time.</param>
/// <param name="UpdatedAtUtc">
/// When the run's runtime state last changed; it is not specific to this step.
/// </param>
/// <param name="ClaimedAtUtc">When the step's active claim was created; null without an active claim.</param>
/// <param name="LeaseExpiresAtUtc">
/// When the active claim expires unless it is renewed; null without an active claim. A step result submitted after
/// expiry is rejected.
/// </param>
/// <param name="Summary">
/// Generated one-line English description of the step's state for display. Times in it use the server's local time
/// zone; do not parse it.
/// </param>
/// <param name="Diagnostics">
/// Diagnostics of the latest result of this step that matches its current status; empty when there is none.
/// </param>
/// <param name="ProducedArtifacts">Artifacts produced by the applied results of this step, in result order.</param>
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
    string Summary,
    IReadOnlyList<ProcessRuntimeDiagnosticApiView> Diagnostics,
    IReadOnlyList<ProcessRuntimeArtifactLineageApiView> ProducedArtifacts);

/// <summary>
/// A child run (subprocess) that a step of a live run is waiting for.
/// </summary>
/// <param name="ParentRunId">Identifier of the waiting run, the run listed.</param>
/// <param name="ParentStepInstanceId">Identifier of the waiting step within that run.</param>
/// <param name="ParentStepKey">Key of the waiting step in the process definition.</param>
/// <param name="ParentStepStatus">
/// Runtime status of the waiting step, as text: <c>Waiting</c>, <c>Blocked</c>, <c>Ready</c>, <c>Claimed</c> or
/// <c>Running</c>.
/// </param>
/// <param name="ChildRunId">
/// Identifier of the child run; read it with <c>GET /api/processes/runs/{runId}</c>.
/// </param>
/// <param name="ChildRunStatus">
/// Runtime status of the child run, as text, for example <c>Created</c>, <c>Active</c> or <c>CancelRequested</c>.
/// Child runs that have ended or are blocked are not listed.
/// </param>
/// <param name="ChildStepKey">Key of the child run's current step; null when it has no current step.</param>
/// <param name="ChildStepStatus">Runtime status of the child run's current step; null when it has none.</param>
/// <param name="Summary">Generated one-line English description for display; do not parse it.</param>
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

/// <summary>
/// Projected detail of one process run, returned by <c>GET /api/processes/runs/{runId}</c>.
/// </summary>
/// <param name="RootRunId">
/// Identifier of the top-level run of the run's subprocess tree; equals <c>runId</c> for a top-level run.
/// </param>
/// <param name="RunId">Identifier of the run (the route value).</param>
/// <param name="Status">
/// Projected status, as text, derived from the latest projected events: <c>Active</c>, <c>NeedsAttention</c> (the
/// run or a step is blocked, rework was requested, or the process manager intervened), <c>Completed</c>,
/// <c>Failed</c> or <c>Cancelled</c>. <c>Unknown</c> is defined but not produced.
/// </param>
/// <param name="FirstEventAtUtc">Instant of the first projected runtime event of the run.</param>
/// <param name="LastEventAtUtc">Instant of the most recently projected runtime event of the run.</param>
/// <param name="Freshness">How current the projection is.</param>
/// <param name="RecentEvents">Up to 20 most recent events of the run, oldest first.</param>
/// <param name="Diagnostics">
/// Current diagnostics: those of the latest results of steps that are now Blocked or Failed. Empty when there are
/// none.
/// </param>
/// <param name="ResultLineage">
/// Every applied step result of the run, in the order in which the results were applied, each with its
/// diagnostics, produced artifacts and recovery decision. Empty before the first result.
/// </param>
internal sealed record ProcessRunDetailApiView(
    Guid RootRunId,
    Guid RunId,
    string Status,
    DateTimeOffset FirstEventAtUtc,
    DateTimeOffset LastEventAtUtc,
    ProcessProjectionFreshnessApiView? Freshness,
    IReadOnlyList<ProcessLiveEventApiView> RecentEvents,
    IReadOnlyList<ProcessRuntimeDiagnosticApiView> Diagnostics,
    IReadOnlyList<ProcessRuntimeResultLineageApiView> ResultLineage);

/// <summary>
/// Response of <c>GET /api/processes/runs/{runId}/history</c>: the run's projected events in the requested window.
/// </summary>
/// <param name="Events">
/// Events of the run in the window, ascending by <c>globalSequence</c>; at most <c>take</c> entries, and when more
/// events exist these are the oldest ones.
/// </param>
/// <param name="Freshness">
/// Derived from the last returned event: <c>observedAtUtc</c> is that event's <c>occurredAtUtc</c>, both sequence
/// members are its <c>globalSequence</c>, and the backlog is 0. Null when no event is returned.
/// </param>
internal sealed record ProcessHistoryApiResponse(
    IReadOnlyList<ProcessTimelineEventApiView> Events,
    ProcessProjectionFreshnessApiView? Freshness);

/// <summary>
/// One projected runtime event of a process run, as listed in the recent events of the live and detail projections.
/// </summary>
/// <param name="EventId">Unique identifier of the runtime event.</param>
/// <param name="GlobalSequence">
/// Position of the event in the durable runtime event log shared by all runs; it grows with every stored event.
/// </param>
/// <param name="RootRunId">
/// Identifier of the top-level run of the subprocess tree; equals <c>runId</c> for a top-level run.
/// </param>
/// <param name="RunId">Identifier of the run the event belongs to.</param>
/// <param name="EventType">
/// Runtime event type name. Run events: <c>ProcessRunCreated</c>, <c>ProcessRunActivated</c>,
/// <c>ProcessRunCancelRequested</c>, <c>ProcessRunCancelled</c>, <c>ProcessRunCompleted</c>, <c>ProcessRunFailed</c>,
/// <c>ProcessRunBlocked</c>, <c>ProcessRunReactivated</c>. Step events: <c>StepReady</c>, <c>StepWaiting</c>,
/// <c>StepClaimed</c>, <c>StepRunning</c>, <c>StepCompleted</c>, <c>StepFailed</c>, <c>StepBlocked</c>,
/// <c>StepCancelled</c>, <c>StepSkipped</c>, <c>StepReworkRequested</c>. Dispatch claim events:
/// <c>DispatchClaimCreated</c>, <c>DispatchLeaseRenewed</c>, <c>DispatchClaimExpired</c>,
/// <c>DispatchClaimReleased</c>, <c>DispatchClaimReclaimed</c>, <c>DispatchClaimCompleted</c>. Process manager
/// events: <c>ManagerIncidentRaised</c>, <c>ManagerRecoveryApproved</c>, <c>ManagerRecoveryDenied</c>,
/// <c>ManagerBranchDecisionRecorded</c>, <c>ManagerBranchDecisionRejected</c>, <c>ManagerLoopBudgetEscalated</c>,
/// <c>ManagerSubprocessMessageQueued</c>.
/// </param>
/// <param name="OccurredAtUtc">When the event occurred.</param>
/// <param name="Sensitivity">
/// <c>Normal</c> or <c>Restricted</c>. For a restricted event the summary is a generic text and
/// <c>restrictedDiagnosticReference</c> is set; the type, identifiers and time stay visible.
/// </param>
/// <param name="Summary">
/// The event type name for a normal event, or <c>Restricted runtime event</c>. For an attention event
/// (<c>ProcessRunBlocked</c>, <c>StepBlocked</c>, <c>ProcessRunFailed</c>, <c>StepFailed</c>,
/// <c>ManagerIncidentRaised</c>, <c>ManagerLoopBudgetEscalated</c>, <c>ManagerRecoveryDenied</c> or
/// <c>ManagerBranchDecisionRejected</c>), the run's first current diagnostic is appended as
/// <c>: {category} - {safeSummary}</c>.
/// </param>
/// <param name="RestrictedDiagnosticReference">
/// For a restricted event, <c>runtime-event:</c> followed by the event identifier, for correlation with operator
/// records; no API operation resolves it. Null for a normal event.
/// </param>
/// <param name="Diagnostics">
/// For an attention event (see <c>summary</c>): the run's current diagnostics at the time of the read, not
/// necessarily those recorded with this event. Empty for other events.
/// </param>
internal sealed record ProcessLiveEventApiView(
    Guid EventId,
    long GlobalSequence,
    Guid RootRunId,
    Guid RunId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string Sensitivity,
    string Summary,
    string? RestrictedDiagnosticReference,
    IReadOnlyList<ProcessRuntimeDiagnosticApiView> Diagnostics);

/// <summary>
/// One projected runtime event of a process run in the history timeline.
/// </summary>
/// <param name="EventId">Unique identifier of the runtime event.</param>
/// <param name="GlobalSequence">
/// Position of the event in the durable runtime event log shared by all runs; it grows with every stored event.
/// </param>
/// <param name="RootRunId">
/// Identifier of the top-level run of the subprocess tree; equals <c>runId</c> for a top-level run.
/// </param>
/// <param name="RunId">Identifier of the run the event belongs to.</param>
/// <param name="EventType">
/// Runtime event type name. Run events: <c>ProcessRunCreated</c>, <c>ProcessRunActivated</c>,
/// <c>ProcessRunCancelRequested</c>, <c>ProcessRunCancelled</c>, <c>ProcessRunCompleted</c>, <c>ProcessRunFailed</c>,
/// <c>ProcessRunBlocked</c>, <c>ProcessRunReactivated</c>. Step events: <c>StepReady</c>, <c>StepWaiting</c>,
/// <c>StepClaimed</c>, <c>StepRunning</c>, <c>StepCompleted</c>, <c>StepFailed</c>, <c>StepBlocked</c>,
/// <c>StepCancelled</c>, <c>StepSkipped</c>, <c>StepReworkRequested</c>. Dispatch claim events:
/// <c>DispatchClaimCreated</c>, <c>DispatchLeaseRenewed</c>, <c>DispatchClaimExpired</c>,
/// <c>DispatchClaimReleased</c>, <c>DispatchClaimReclaimed</c>, <c>DispatchClaimCompleted</c>. Process manager
/// events: <c>ManagerIncidentRaised</c>, <c>ManagerRecoveryApproved</c>, <c>ManagerRecoveryDenied</c>,
/// <c>ManagerBranchDecisionRecorded</c>, <c>ManagerBranchDecisionRejected</c>, <c>ManagerLoopBudgetEscalated</c>,
/// <c>ManagerSubprocessMessageQueued</c>.
/// </param>
/// <param name="OccurredAtUtc">When the event occurred.</param>
/// <param name="Sensitivity">
/// <c>Normal</c> or <c>Restricted</c>. For a restricted event the summary is a generic text and
/// <c>restrictedDiagnosticReference</c> is set; the type, identifiers and time stay visible.
/// </param>
/// <param name="Summary">
/// The event type name for a normal event, or <c>Restricted runtime event</c>. For an attention event
/// (<c>ProcessRunBlocked</c>, <c>StepBlocked</c>, <c>ProcessRunFailed</c>, <c>StepFailed</c>,
/// <c>ManagerIncidentRaised</c>, <c>ManagerLoopBudgetEscalated</c>, <c>ManagerRecoveryDenied</c> or
/// <c>ManagerBranchDecisionRejected</c>), the run's first current diagnostic is appended as
/// <c>: {category} - {safeSummary}</c>.
/// </param>
/// <param name="RestrictedDiagnosticReference">
/// For a restricted event, <c>runtime-event:</c> followed by the event identifier, for correlation with operator
/// records; no API operation resolves it. Null for a normal event.
/// </param>
/// <param name="Diagnostics">
/// For an attention event (see <c>summary</c>): the run's current diagnostics at the time of the read, not
/// necessarily those recorded with this event. Empty for other events.
/// </param>
internal sealed record ProcessTimelineEventApiView(
    Guid EventId,
    long GlobalSequence,
    Guid RootRunId,
    Guid RunId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string Sensitivity,
    string Summary,
    string? RestrictedDiagnosticReference,
    IReadOnlyList<ProcessRuntimeDiagnosticApiView> Diagnostics);

/// <summary>
/// A diagnostic reported with a step result, as shown by the live, detail and history projections.
/// </summary>
/// <param name="RunId">Identifier of the run whose step produced the result.</param>
/// <param name="StepInstanceId">Identifier of that step within the run.</param>
/// <param name="StepKey">Key of the step in the process definition, or the step identifier as text.</param>
/// <param name="StrategyId">Identifier of the execution strategy (driver) that produced the result.</param>
/// <param name="ResultHash">
/// Hash of the step result: <c>sha256:</c> followed by 64 lowercase hexadecimal characters.
/// </param>
/// <param name="Code">
/// Stable diagnostic code, for example <c>process.adapter.required_tool_receipt_missing</c>. A blocked or failed result
/// without diagnostics is reported with <c>process.runtime.blocked_without_diagnostics</c>.
/// </param>
/// <param name="Category">
/// Category derived from the code when it is read: <c>Runtime</c> (<c>process.runtime.</c> codes), <c>Adapter</c>
/// (<c>process.adapter.</c> codes), <c>Capability</c>, <c>Artifact</c>, <c>Provider</c> or <c>Strategy</c> (by
/// keywords in the code), <c>Unknown</c> for an empty code, and <c>MissingDiagnostics</c> for the substitute
/// diagnostic of a result without diagnostics.
/// </param>
/// <param name="SafeSummary">
/// Human-readable explanation that was checked when the result was accepted: at most 2,048 characters, without text
/// that looks like credentials, URL query strings or local file paths.
/// </param>
/// <param name="Sensitivity">
/// <c>Normal</c> or <c>Restricted</c>. A restricted diagnostic may point to restricted evidence through
/// <c>restrictedDiagnosticReference</c>; its safe summary is shown either way.
/// </param>
/// <param name="RetrySafety">
/// Whether repeating the step is considered safe: <c>SafeToRetry</c>, <c>UnsafeToRetry</c> or <c>Unknown</c>.
/// </param>
/// <param name="Idempotency">
/// Whether the step's effect is idempotent: <c>Idempotent</c>, <c>NonIdempotent</c> or <c>Unknown</c>.
/// </param>
/// <param name="RestrictedDiagnosticReference">
/// Opaque reference to restricted evidence, of the form <c>restricted://kind/value</c>, or null. No API operation
/// resolves it.
/// </param>
/// <param name="OperatorDetails">
/// Structured guidance for known gate diagnostics (<c>process.adapter.</c> codes and a few runtime codes); null for
/// other codes.
/// </param>
internal sealed record ProcessRuntimeDiagnosticApiView(
    Guid RunId,
    Guid StepInstanceId,
    string StepKey,
    string StrategyId,
    string ResultHash,
    string Code,
    string Category,
    string SafeSummary,
    string Sensitivity,
    string RetrySafety,
    string Idempotency,
    string? RestrictedDiagnosticReference,
    ProcessRuntimeOperatorDiagnosticDetailsApiView? OperatorDetails);

/// <summary>
/// Operator guidance derived from a gate diagnostic: which gate failed, which branch or criteria were involved, and
/// what to do next.
/// </summary>
/// <param name="GateId">Identifier of the gate that reported the diagnostic, derived from its code.</param>
/// <param name="BranchOutcomeKey">
/// Branch outcome key named in the diagnostic's summary; empty when none is named.
/// </param>
/// <param name="RouteTargetBranchOutcomeKey">
/// For a completion issue routed to another branch, the target branch outcome key; empty otherwise.
/// </param>
/// <param name="FailedCriteriaIds">
/// Acceptance criteria identifiers named in the summary, such as <c>AC-012</c>; upper-case, distinct and sorted.
/// </param>
/// <param name="ReceiptRuleIds">
/// Receipt rule identifiers (snake_case names) found in the summary; distinct and sorted.
/// </param>
/// <param name="NextAction">
/// Fixed English guidance for the gate, for example to inspect the diagnostic and retry only after the gate is
/// satisfied.
/// </param>
internal sealed record ProcessRuntimeOperatorDiagnosticDetailsApiView(
    string GateId,
    string BranchOutcomeKey,
    string RouteTargetBranchOutcomeKey,
    IReadOnlyList<string> FailedCriteriaIds,
    IReadOnlyList<string> ReceiptRuleIds,
    string NextAction);

/// <summary>
/// An artifact produced by a step result.
/// </summary>
/// <param name="SlotId">Identifier of the artifact slot of the plan that the artifact fills.</param>
/// <param name="ArtifactId">Identifier of the produced artifact instance.</param>
/// <param name="ContentHash">
/// Hash of the artifact content: <c>sha256:</c> followed by 64 lowercase hexadecimal characters.
/// </param>
internal sealed record ProcessRuntimeArtifactLineageApiView(
    Guid SlotId,
    Guid ArtifactId,
    string ContentHash);

/// <summary>
/// One applied step result of a run, with what it produced and how the runtime reacted to it.
/// </summary>
/// <param name="RunId">Identifier of the run that owns the step.</param>
/// <param name="StepInstanceId">Identifier of the step within the run.</param>
/// <param name="StepKey">Key of the step in the process definition, or the step identifier as text.</param>
/// <param name="StrategyId">Identifier of the execution strategy (driver) that produced the result.</param>
/// <param name="IdempotencyKey">
/// Key of the result submission: a second result with the same step, strategy and key is ignored as a duplicate.
/// </param>
/// <param name="Outcome">
/// Outcome reported by the strategy, as text: <c>Succeeded</c>, <c>Failed</c>, <c>Waiting</c>,
/// <c>NeedsManager</c> or <c>Canceled</c>.
/// </param>
/// <param name="AppliedStepStatus">
/// Step status that the runtime applied for the result, as text: <c>Completed</c> for Succeeded, <c>Failed</c> for
/// Failed, <c>Blocked</c> for Waiting or NeedsManager, <c>Cancelled</c> for Canceled, and <c>Ready</c> when a blocked
/// result was classified as safe to retry.
/// </param>
/// <param name="ResultHash">
/// Hash of the result: <c>sha256:</c> followed by 64 lowercase hexadecimal characters.
/// </param>
/// <param name="Diagnostics">Diagnostics reported with the result.</param>
/// <param name="ProducedArtifacts">Artifacts produced by the result.</param>
/// <param name="RecoveryDecision">
/// How the runtime classified a blocked or failed result; null for other results.
/// </param>
internal sealed record ProcessRuntimeResultLineageApiView(
    Guid RunId,
    Guid StepInstanceId,
    string StepKey,
    string StrategyId,
    Guid IdempotencyKey,
    string Outcome,
    string AppliedStepStatus,
    string ResultHash,
    IReadOnlyList<ProcessRuntimeDiagnosticApiView> Diagnostics,
    IReadOnlyList<ProcessRuntimeArtifactLineageApiView> ProducedArtifacts,
    ProcessRuntimeRecoveryDecisionApiView? RecoveryDecision);

/// <summary>
/// The runtime's recovery classification of a blocked or failed step result.
/// </summary>
/// <param name="FailureCategory">
/// Failure category, as text: <c>Unknown</c>, <c>MissingDiagnostics</c>, <c>MissingArtifact</c>,
/// <c>MissingCapability</c>, <c>DeniedCapability</c>, <c>PolicyViolation</c>, <c>Timeout</c>,
/// <c>ProviderFailure</c>, <c>ChildRunBlocked</c>, <c>InstructionNonCompliance</c>, <c>ProductCompletionGate</c> or
/// <c>AdapterRetryable</c>.
/// </param>
/// <param name="DecisionKind">
/// Recovery decision, as text: <c>SafeRetry</c> (the step can be retried automatically), <c>ManagerRequired</c> (a
/// process manager or operator must decide), <c>TerminalBlocked</c> (no automatic recovery) or <c>None</c>.
/// </param>
/// <param name="SourceDiagnosticCode">Code of the diagnostic the decision is based on.</param>
/// <param name="Policy">
/// Recovery policy that was applied, for example <c>process.manager-review-required</c> or
/// <c>process.terminal-failure</c>.
/// </param>
/// <param name="SafeReason">English explanation of the decision for operators.</param>
internal sealed record ProcessRuntimeRecoveryDecisionApiView(
    string FailureCategory,
    string DecisionKind,
    string SourceDiagnosticCode,
    string Policy,
    string SafeReason);

/// <summary>
/// How current a projection is. It is recorded when the projection is updated and is not recalculated when it is
/// read.
/// </summary>
/// <param name="ObservedAtUtc">When the projector processed the event that last updated the projection.</param>
/// <param name="SourceGlobalSequence">
/// <c>globalSequence</c> of the runtime event that last updated the projection.
/// </param>
/// <param name="LatestKnownGlobalSequence">
/// Highest <c>globalSequence</c> known to the projector when it processed that event; it can be lower than the
/// newest event in the store.
/// </param>
/// <param name="LastProcessedGlobalSequence">
/// <c>globalSequence</c> of the last event the projector had processed; currently always equal to
/// <c>sourceGlobalSequence</c>.
/// </param>
/// <param name="BacklogEventCount">
/// Number of known events not yet projected at that time (the difference of the two sequence numbers); 0 means that
/// the projection was up to date when it was written.
/// </param>
internal sealed record ProcessProjectionFreshnessApiView(
    DateTimeOffset ObservedAtUtc,
    long SourceGlobalSequence,
    long LatestKnownGlobalSequence,
    long LastProcessedGlobalSequence,
    int BacklogEventCount);
