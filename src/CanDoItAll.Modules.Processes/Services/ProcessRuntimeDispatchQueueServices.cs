using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRuntimeDispatchQueueWorker(
    ProcessRuntimeDispatchQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptions<ProcessRuntimeDispatchQueueOptions> options,
    ILogger<ProcessRuntimeDispatchQueueWorker> logger) : BackgroundService
{
    private const int MaxImmediateDispatches = 2;
    private const int MaxRecoveryDispatches = 2;
    private static readonly TimeSpan RecoveryPollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(1);
    private const string RecoveryRequestedBy = "process-background-dispatcher";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var recoveryEnabled = options.Value.EnableRecovery;
        var readyRecoveryStartedAtUtc = DateTimeOffset.UtcNow;
        var nextRecoveryAtUtc = DateTimeOffset.MinValue;
        var activeImmediateDispatches = new Dictionary<Task, ProcessRunId>();
        var activeRecoveryDispatches = new Dictionary<Task, ProcessRunId>();
        if (recoveryEnabled)
        {
            await EnqueueRecoverableRunsAsync(readyRecoveryStartedAtUtc, stoppingToken).ConfigureAwait(false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ObserveCompletedDispatchesAsync(activeImmediateDispatches).ConfigureAwait(false);
                await ObserveCompletedDispatchesAsync(activeRecoveryDispatches).ConfigureAwait(false);

                var nowUtc = DateTimeOffset.UtcNow;
                if (recoveryEnabled && nowUtc >= nextRecoveryAtUtc)
                {
                    await EnqueueRecoverableRunsAsync(readyRecoveryStartedAtUtc, stoppingToken).ConfigureAwait(false);
                    nextRecoveryAtUtc = nowUtc.Add(RecoveryPollInterval);
                }

                while (activeImmediateDispatches.Count < MaxImmediateDispatches &&
                       queue.TryDequeueImmediate(out var request))
                {
                    TryStartDispatch(activeImmediateDispatches, request, stoppingToken);
                }

                while (activeRecoveryDispatches.Count < MaxRecoveryDispatches &&
                       queue.TryDequeueRecovery(out var request))
                {
                    TryStartDispatch(activeRecoveryDispatches, request, stoppingToken);
                }

                if (activeImmediateDispatches.Count > 0 || activeRecoveryDispatches.Count > 0)
                {
                    var idleDelay = Task.Delay(IdlePollInterval, stoppingToken);
                    var completed = await Task
                        .WhenAny(activeImmediateDispatches.Keys.Concat(activeRecoveryDispatches.Keys).Append(idleDelay))
                        .ConfigureAwait(false);
                    if (completed != idleDelay)
                    {
                        await ObserveDispatchCompletionAsync(
                            completed,
                            activeImmediateDispatches,
                            activeRecoveryDispatches).ConfigureAwait(false);
                    }

                    continue;
                }

                await Task.Delay(IdlePollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Process background dispatch worker failed while polling.");
                await Task.Delay(RecoveryPollInterval, stoppingToken).ConfigureAwait(false);
            }
        }

        await ObserveCompletedDispatchesAsync(activeImmediateDispatches).ConfigureAwait(false);
        await ObserveCompletedDispatchesAsync(activeRecoveryDispatches).ConfigureAwait(false);
    }

    private void TryStartDispatch(
        Dictionary<Task, ProcessRunId> activeDispatches,
        ProcessRuntimeDispatchQueueRequest request,
        CancellationToken cancellationToken)
    {
        if (!queue.TryMarkActive(request.RunId))
        {
            logger.LogDebug(
                "Skipping queued process dispatch for run {RunId} because another dispatch is already active.",
                request.RunId.Value);
            return;
        }

        activeDispatches.Add(DispatchAsync(request, cancellationToken), request.RunId);
    }

    private async Task ObserveCompletedDispatchesAsync(
        Dictionary<Task, ProcessRunId> activeDispatches)
    {
        var completedTasks = activeDispatches
            .Keys
            .Where(task => task.IsCompleted)
            .ToArray();
        foreach (var completedTask in completedTasks)
        {
            await ObserveDispatchCompletionAsync(completedTask, activeDispatches)
                .ConfigureAwait(false);
        }
    }

    private async Task ObserveDispatchCompletionAsync(
        Task dispatchTask,
        Dictionary<Task, ProcessRunId> firstActiveDispatches,
        Dictionary<Task, ProcessRunId> secondActiveDispatches)
    {
        if (!firstActiveDispatches.Remove(dispatchTask, out var runId))
        {
            secondActiveDispatches.Remove(dispatchTask, out runId);
        }

        if (runId != default)
        {
            queue.MarkInactive(runId);
        }

        await ObserveDispatchTaskAsync(dispatchTask).ConfigureAwait(false);
    }

    private async Task ObserveDispatchCompletionAsync(
        Task dispatchTask,
        Dictionary<Task, ProcessRunId> activeDispatches)
    {
        if (activeDispatches.Remove(dispatchTask, out var runId))
        {
            queue.MarkInactive(runId);
        }

        await ObserveDispatchTaskAsync(dispatchTask).ConfigureAwait(false);
    }

    private static async Task ObserveDispatchTaskAsync(Task dispatchTask)
    {
        try
        {
            await dispatchTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task DispatchAsync(
        ProcessRuntimeDispatchQueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatchService = scope.ServiceProvider.GetRequiredService<ProcessRuntimeDispatchApplicationService>();
            var projectionCatchupService = scope.ServiceProvider.GetRequiredService<ProcessRuntimeProjectionCatchupService>();
            var result = await dispatchService
                .ExecuteReadyAsync(request.RunId, NormalizeRequestedBy(request.RequestedBy), cancellationToken)
                .ConfigureAwait(false);
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
            LogDispatchResult(request, result);
            if (ProcessRuntimeChildRunParentQuery.IsStoppedChildStatus(result.Status))
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ProcessPersistenceDbContext>();
                var releasedParentRunIds = await ReleaseActiveParentClaimsForStoppedChildAsync(
                    scope.ServiceProvider,
                    request.RunId.Value,
                    NormalizeRequestedBy(request.RequestedBy),
                    cancellationToken).ConfigureAwait(false);
                foreach (var releasedParentRunId in releasedParentRunIds)
                {
                    await queue.EnqueueAsync(
                        new ProcessRuntimeDispatchQueueRequest(
                            new ProcessRunId(releasedParentRunId),
                            NormalizeRequestedBy(request.RequestedBy)),
                        cancellationToken).ConfigureAwait(false);
                }

                await ReworkActiveParentStepsForStoppedChildAsync(
                    scope.ServiceProvider,
                    dbContext,
                    request.RunId.Value,
                    result.Status,
                    NormalizeRequestedBy(request.RequestedBy),
                    cancellationToken).ConfigureAwait(false);

                var parentRunIds = await ProcessRuntimeChildRunParentQuery
                    .LoadActiveParentRunIdsAsync(dbContext, request.RunId.Value, cancellationToken)
                    .ConfigureAwait(false);
                foreach (var parentRunId in parentRunIds)
                {
                    await queue.EnqueueAsync(
                        new ProcessRuntimeDispatchQueueRequest(
                            new ProcessRunId(parentRunId),
                            NormalizeRequestedBy(request.RequestedBy)),
                        cancellationToken).ConfigureAwait(false);
                }

                if (parentRunIds.Count > 0)
                {
                    logger.LogInformation(
                        "Process background dispatch queued {ParentRunCount} parent run(s) after terminal child run {RunId}.",
                        parentRunIds.Count,
                        request.RunId.Value);
                }
            }

            if (result.Diagnostics.Count == 0)
            {
                logger.LogInformation(
                    "Process background dispatch completed for run {RunId}. Stage={Stage} Status={Status} Diagnostics=0",
                    request.RunId.Value,
                    result.Stage,
                    result.Status);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Process background dispatch failed for run {RunId}.",
                request.RunId.Value);
        }
    }

    private async Task<IReadOnlyList<Guid>> ReleaseActiveParentClaimsForStoppedChildAsync(
        IServiceProvider serviceProvider,
        Guid childRunId,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        var dbContext = serviceProvider.GetRequiredService<ProcessPersistenceDbContext>();
        var candidates = await ProcessRuntimeChildRunParentQuery
            .LoadActiveParentClaimsAsync(dbContext, childRunId, cancellationToken)
            .ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            return [];
        }

        var stateStore = serviceProvider.GetRequiredService<IProcessRuntimeStateStore>();
        var unitOfWork = serviceProvider.GetRequiredService<IProcessRuntimeUnitOfWork>();
        var projectionCatchupService = serviceProvider.GetRequiredService<ProcessRuntimeProjectionCatchupService>();
        var releasedRunIds = new HashSet<Guid>();
        foreach (var candidate in candidates)
        {
            if (await TryReleaseActiveParentClaimAsync(
                stateStore,
                unitOfWork,
                candidate,
                childRunId,
                requestedBy,
                cancellationToken).ConfigureAwait(false))
            {
                releasedRunIds.Add(candidate.RunId);
            }
        }

        if (releasedRunIds.Count > 0)
        {
            await projectionCatchupService.CatchUpAsync(cancellationToken).ConfigureAwait(false);
        }

        return releasedRunIds
            .OrderBy(runId => runId)
            .ToArray();
    }

    private async Task<IReadOnlyList<Guid>> ReworkActiveParentStepsForStoppedChildAsync(
        IServiceProvider serviceProvider,
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        ProcessRuntimeStatus childStatus,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        var operatorService = serviceProvider.GetRequiredService<ProcessRuntimeOperatorApplicationService>();
        var parentSteps = await ProcessRuntimeChildRunParentQuery
            .LoadActiveParentStepsAsync(dbContext, childRunId, cancellationToken)
            .ConfigureAwait(false);
        if (parentSteps.Count == 0)
        {
            return [];
        }

        var reworkedParentRunIds = new HashSet<Guid>();
        foreach (var parentStep in parentSteps)
        {
            var rework = await operatorService.ExecuteAsync(
                new ProcessRuntimeOperatorActionCommand(
                    new ProcessRunId(parentStep.RunId),
                    new ProcessStepInstanceId(parentStep.StepInstanceId),
                    ProcessRuntimeOperatorActionKind.RequestRework,
                    NormalizeRequestedBy(requestedBy),
                    $"Child process run '{childRunId:D}' stopped with status '{childStatus}'. Treat that child as historical evidence, not an active wait. If required child evidence is already available, complete from that evidence. If required child evidence is missing and this subprocess step can execute external action, call the subprocess launch tool again so the launch service can create or reuse a non-stopped child run. Do not return Blocked only because the stopped child exists."),
                cancellationToken).ConfigureAwait(false);
            if (rework.Succeeded)
            {
                reworkedParentRunIds.Add(parentStep.RunId);
                continue;
            }

            logger.LogWarning(
                "Process background dispatch could not requeue parent step {StepInstanceId} for parent run {ParentRunId} after stopped child run {ChildRunId} with status {ChildStatus}. Diagnostics={Diagnostics}",
                parentStep.StepInstanceId,
                parentStep.RunId,
                childRunId,
                childStatus,
                string.Join("; ", rework.Diagnostics));
        }

        return reworkedParentRunIds
            .OrderBy(runId => runId)
            .ToArray();
    }

    private async Task<bool> TryReleaseActiveParentClaimAsync(
        IProcessRuntimeStateStore stateStore,
        IProcessRuntimeUnitOfWork unitOfWork,
        ProcessRuntimeChildRunParentQuery.ParentClaim candidate,
        Guid childRunId,
        string requestedBy,
        CancellationToken cancellationToken)
    {
        var engine = new ProcessRuntimeEngine(unitOfWork);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var state = await stateStore
                    .LoadAsync(new ProcessRunId(candidate.RunId), cancellationToken)
                    .ConfigureAwait(false);
                if (state is null || ProcessRuntimeTerminalStates.IsRunTerminal(state.Status))
                {
                    return false;
                }

                var releaseCommit = await engine.ReleaseClaimAsync(
                    state,
                    CreateStoppedChildRecoveryContext(requestedBy, childRunId),
                    new ReleaseDispatchClaimCommand(
                        new ProcessStepInstanceId(candidate.StepInstanceId),
                        new DispatcherOwnerId(candidate.OwnerId),
                        new DispatchClaimToken(candidate.ClaimToken)),
                    cancellationToken).ConfigureAwait(false);
                if (!releaseCommit.Succeeded)
                {
                    logger.LogWarning(
                        "Stopped child run {ChildRunId} could not release active parent claim {ClaimToken} for parent run {ParentRunId}, step {ParentStepId}. Diagnostics={Diagnostics}",
                        childRunId,
                        candidate.ClaimToken,
                        candidate.RunId,
                        candidate.StepInstanceId,
                        string.Join("; ", releaseCommit.Diagnostics.Select(diagnostic => diagnostic.Message)));
                    return false;
                }

                logger.LogInformation(
                    "Stopped child run {ChildRunId} released active parent claim {ClaimToken} for parent run {ParentRunId}, step {ParentStepId}.",
                    childRunId,
                    candidate.ClaimToken,
                    candidate.RunId,
                    candidate.StepInstanceId);
                return true;
            }
            catch (ProcessRuntimeOptimisticConcurrencyException) when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }

        logger.LogWarning(
            "Stopped child run {ChildRunId} could not release active parent claim {ClaimToken} for parent run {ParentRunId}, step {ParentStepId} after concurrency retries.",
            childRunId,
            candidate.ClaimToken,
            candidate.RunId,
            candidate.StepInstanceId);
        return false;
    }

    private static RuntimeCommandContext CreateStoppedChildRecoveryContext(
        string requestedBy,
        Guid childRunId)
    {
        return new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(ProcessEventActorKind.System, new ProcessActorId(NormalizeRequestedBy(requestedBy))),
            new ProcessCorrelationId($"stopped-child-{childRunId:N}-{Guid.NewGuid():N}"),
            DateTimeOffset.UtcNow);
    }

    private void LogDispatchResult(
        ProcessRuntimeDispatchQueueRequest request,
        ProcessRuntimeDispatchResult result)
    {
        if (result.Diagnostics.Count == 0)
        {
            return;
        }

        var diagnostics = string.Join(" | ", result.Diagnostics.Select(FormatDiagnostic));
        if (ShouldLogDispatchDiagnosticsAsWarning(result))
        {
            logger.LogWarning(
                "Process background dispatch completed for run {RunId}. Stage={Stage} Status={Status} Diagnostics={Diagnostics}",
                request.RunId.Value,
                result.Stage,
                result.Status,
                diagnostics);
            return;
        }

        logger.LogInformation(
            "Process background dispatch completed for run {RunId}. Stage={Stage} Status={Status} Diagnostics={Diagnostics}",
            request.RunId.Value,
            result.Stage,
            result.Status,
            diagnostics);
    }

    internal static bool ShouldLogDispatchDiagnosticsAsWarning(ProcessRuntimeDispatchResult result)
    {
        if (result.Diagnostics.Count == 0)
        {
            return false;
        }

        if (result.Status == ProcessRuntimeStatus.Completed &&
            result.Diagnostics.All(IsRoutineDispatchDiagnostic))
        {
            return false;
        }

        if (ProcessRuntimeTerminalStates.IsRunTerminal(result.Status))
        {
            return true;
        }

        return !result.Diagnostics.All(IsRoutineDispatchDiagnostic);
    }

    private static bool IsRoutineDispatchDiagnostic(string diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic))
        {
            return false;
        }

        return diagnostic.Contains("changed concurrently while", StringComparison.OrdinalIgnoreCase) ||
               diagnostic.Contains("is waiting for active child process run", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDiagnostic(string diagnostic)
    {
        var normalized = string.IsNullOrWhiteSpace(diagnostic)
            ? "empty diagnostic"
            : diagnostic.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 500
            ? normalized
            : normalized[..500];
    }

    private async Task EnqueueRecoverableRunsAsync(
        DateTimeOffset readyUpdatedAfterUtc,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProcessPersistenceDbContext>();
        var terminalChildRunIds = await ProcessRuntimeChildRunParentQuery
            .LoadTerminalChildRunIdsWithParentLinksAsync(dbContext, cancellationToken)
            .ConfigureAwait(false);
        foreach (var terminalChildRunId in terminalChildRunIds)
        {
            var childStatus = await ProcessRuntimeChildRunParentQuery
                .LoadStoppedChildStatusAsync(dbContext, terminalChildRunId, cancellationToken)
                .ConfigureAwait(false);
            if (childStatus is null)
            {
                continue;
            }

            var releasedParentRunIds = await ReleaseActiveParentClaimsForStoppedChildAsync(
                scope.ServiceProvider,
                terminalChildRunId,
                RecoveryRequestedBy,
                cancellationToken).ConfigureAwait(false);
            foreach (var releasedParentRunId in releasedParentRunIds)
            {
                await queue.EnqueueAsync(
                    new ProcessRuntimeDispatchQueueRequest(new ProcessRunId(releasedParentRunId), RecoveryRequestedBy, IsRecovery: true),
                    cancellationToken).ConfigureAwait(false);
            }

            var reworkedParentRunIds = await ReworkActiveParentStepsForStoppedChildAsync(
                scope.ServiceProvider,
                dbContext,
                terminalChildRunId,
                childStatus.Value,
                RecoveryRequestedBy,
                cancellationToken).ConfigureAwait(false);
            foreach (var reworkedParentRunId in reworkedParentRunIds)
            {
                await queue.EnqueueAsync(
                    new ProcessRuntimeDispatchQueueRequest(new ProcessRunId(reworkedParentRunId), RecoveryRequestedBy, IsRecovery: true),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        var activeRunIds = await ProcessRuntimeDispatchRecoveryRunQuery
            .LoadRecoverableRunIdsAsync(
                dbContext,
                DateTimeOffset.UtcNow,
                readyUpdatedAfterUtc,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var runId in activeRunIds)
        {
            await queue.EnqueueAsync(
                new ProcessRuntimeDispatchQueueRequest(new ProcessRunId(runId), RecoveryRequestedBy, IsRecovery: true),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string NormalizeRequestedBy(string requestedBy)
        => string.IsNullOrWhiteSpace(requestedBy)
            ? RecoveryRequestedBy
            : requestedBy.Trim();
}

internal static class ProcessRuntimeChildRunParentQuery
{
    public sealed record ParentStep(Guid RunId, Guid StepInstanceId);
    public sealed record ParentClaim(Guid RunId, Guid StepInstanceId, Guid ClaimToken, string OwnerId);

    public static bool IsStoppedChildStatus(ProcessRuntimeStatus status)
    {
        return ProcessRuntimeTerminalStates.IsRunTerminal(status) || status == ProcessRuntimeStatus.Blocked;
    }

    public static async Task<ProcessRuntimeStatus?> LoadStoppedChildStatusAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var status = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.RunId == childRunId)
            .Select(state => (ProcessRuntimeStatus?)state.Status)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return status is not null && IsStoppedChildStatus(status.Value)
            ? status
            : null;
    }

    public static async Task<IReadOnlyList<Guid>> LoadActiveParentRunIdsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var parentRunKeySnippet = JsonSerializer.Serialize("ParentProcessRunId");
        var childLaunchVariables = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.RunId == childRunId &&
                assignment.LaunchVariablesJson.Contains(parentRunKeySnippet))
            .Select(assignment => assignment.LaunchVariablesJson)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var parentRunIds = childLaunchVariables
            .Select(TryReadParentRunId)
            .Where(parentRunId => parentRunId.HasValue)
            .Select(parentRunId => parentRunId!.Value)
            .Distinct()
            .ToArray();
        if (parentRunIds.Length == 0)
        {
            return [];
        }

        return await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                parentRunIds.Contains(state.RunId) &&
                state.Status == ProcessRuntimeStatus.Active)
            .OrderBy(state => state.UpdatedAtUtc)
            .Select(state => state.RunId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<ParentStep>> LoadActiveParentStepsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var parentRunKeySnippet = JsonSerializer.Serialize("ParentProcessRunId");
        var childLaunchVariables = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.RunId == childRunId &&
                assignment.LaunchVariablesJson.Contains(parentRunKeySnippet))
            .Select(assignment => assignment.LaunchVariablesJson)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var requestedParentSteps = childLaunchVariables
            .Select(TryReadParentStep)
            .Where(parentStep => parentStep is not null)
            .Select(parentStep => parentStep!)
            .Distinct()
            .ToArray();
        if (requestedParentSteps.Length == 0)
        {
            return [];
        }

        var parentRunIds = requestedParentSteps
            .Select(parentStep => parentStep.RunId)
            .Distinct()
            .ToArray();
        var parentStepIds = requestedParentSteps
            .Select(parentStep => parentStep.StepInstanceId)
            .Distinct()
            .ToArray();

        var candidates = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                parentRunIds.Contains(state.RunId) &&
                state.Status == ProcessRuntimeStatus.Active)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, Step = step })
            .Where(item =>
                parentStepIds.Contains(item.Step.StepInstanceId) &&
                item.Step.IsExecutable &&
                item.Step.ActiveClaimToken == null &&
                (item.Step.Status == ProcessRuntimeStepStatus.Waiting ||
                 item.Step.Status == ProcessRuntimeStepStatus.Blocked ||
                 item.Step.Status == ProcessRuntimeStepStatus.Failed ||
                 item.Step.Status == ProcessRuntimeStepStatus.Ready))
            .Select(item => new ParentStep(item.RunId, item.Step.StepInstanceId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates
            .Distinct()
            .OrderBy(parentStep => parentStep.RunId)
            .ThenBy(parentStep => parentStep.StepInstanceId)
            .ToArray();
    }

    public static async Task<IReadOnlyList<ParentClaim>> LoadActiveParentClaimsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var childTerminalAtUtc = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                state.RunId == childRunId &&
                (state.Status == ProcessRuntimeStatus.Completed ||
                 state.Status == ProcessRuntimeStatus.Failed ||
                 state.Status == ProcessRuntimeStatus.Cancelled ||
                 state.Status == ProcessRuntimeStatus.Blocked))
            .Select(state => (DateTimeOffset?)state.UpdatedAtUtc)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (childTerminalAtUtc is null)
        {
            return [];
        }

        var requestedParentSteps = await LoadRequestedParentStepsAsync(
            dbContext,
            childRunId,
            cancellationToken).ConfigureAwait(false);
        if (requestedParentSteps.Count == 0)
        {
            return [];
        }

        var parentRunIds = requestedParentSteps
            .Select(parentStep => parentStep.RunId)
            .Distinct()
            .ToArray();
        var parentStepIds = requestedParentSteps
            .Select(parentStep => parentStep.StepInstanceId)
            .Distinct()
            .ToArray();

        var candidates = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                parentRunIds.Contains(state.RunId) &&
                state.Status == ProcessRuntimeStatus.Active)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, Step = step })
            .Where(item =>
                parentStepIds.Contains(item.Step.StepInstanceId) &&
                item.Step.IsExecutable &&
                item.Step.ActiveClaimToken != null &&
                (item.Step.Status == ProcessRuntimeStepStatus.Claimed ||
                 item.Step.Status == ProcessRuntimeStepStatus.Running))
            .Join(
                dbContext.DispatchClaims.AsNoTracking(),
                item => item.RunId,
                claim => claim.RunId,
                (item, claim) => new { item.RunId, item.Step, Claim = claim })
            .Where(item =>
                item.Step.ActiveClaimToken == item.Claim.ClaimToken &&
                item.Claim.CreatedAtUtc < childTerminalAtUtc.Value &&
                (item.Claim.Status == DispatchClaimStatus.Claimed ||
                 item.Claim.Status == DispatchClaimStatus.LeaseRenewed ||
                 item.Claim.Status == DispatchClaimStatus.Reclaimed))
            .Select(item => new ParentClaim(
                item.RunId,
                item.Step.StepInstanceId,
                item.Claim.ClaimToken,
                item.Claim.OwnerId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates
            .Distinct()
            .OrderBy(parentClaim => parentClaim.RunId)
            .ThenBy(parentClaim => parentClaim.StepInstanceId)
            .ToArray();
    }

    public static async Task<IReadOnlyList<Guid>> LoadTerminalChildRunIdsWithParentLinksAsync(
        ProcessPersistenceDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var parentRunKeySnippet = JsonSerializer.Serialize("ParentProcessRunId");
        return await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state =>
                state.Status == ProcessRuntimeStatus.Completed ||
                state.Status == ProcessRuntimeStatus.Failed ||
                state.Status == ProcessRuntimeStatus.Cancelled ||
                state.Status == ProcessRuntimeStatus.Blocked)
            .Join(
                dbContext.RuntimeStepAssignments.AsNoTracking(),
                state => state.RunId,
                assignment => assignment.RunId,
                (state, assignment) => new { state.RunId, assignment.LaunchVariablesJson })
            .Where(item => item.LaunchVariablesJson.Contains(parentRunKeySnippet))
            .Select(item => item.RunId)
            .Distinct()
            .OrderBy(runId => runId)
            .Take(250)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<ParentStep>> LoadRequestedParentStepsAsync(
        ProcessPersistenceDbContext dbContext,
        Guid childRunId,
        CancellationToken cancellationToken)
    {
        var parentRunKeySnippet = JsonSerializer.Serialize("ParentProcessRunId");
        var childLaunchVariables = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.RunId == childRunId &&
                assignment.LaunchVariablesJson.Contains(parentRunKeySnippet))
            .Select(assignment => assignment.LaunchVariablesJson)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        return childLaunchVariables
            .Select(TryReadParentStep)
            .Where(parentStep => parentStep is not null)
            .Select(parentStep => parentStep!)
            .Distinct()
            .ToArray();
    }

    private static Guid? TryReadParentRunId(string launchVariablesJson)
    {
        if (string.IsNullOrWhiteSpace(launchVariablesJson))
        {
            return null;
        }

        try
        {
            var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(launchVariablesJson);
            return variables is not null &&
                   variables.TryGetValue("ParentProcessRunId", out var parentRunId) &&
                   Guid.TryParse(parentRunId, out var runId)
                ? runId
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ParentStep? TryReadParentStep(string launchVariablesJson)
    {
        if (string.IsNullOrWhiteSpace(launchVariablesJson))
        {
            return null;
        }

        try
        {
            var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(launchVariablesJson);
            return variables is not null &&
                   variables.TryGetValue("ParentProcessRunId", out var parentRunId) &&
                   variables.TryGetValue("ParentProcessStepId", out var parentStepId) &&
                   Guid.TryParse(parentRunId, out var runId) &&
                   Guid.TryParse(parentStepId, out var stepInstanceId)
                ? new ParentStep(runId, stepInstanceId)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal static class ProcessRuntimeDispatchRecoveryRunQuery
{
    private const int MaxDispatchableRuns = 500;
    private const int MaxDispatchableCandidateRows = MaxDispatchableRuns * 8;
    private const int MaxExpiredClaimRuns = 250;

    public static async Task<IReadOnlyList<Guid>> LoadRecoverableRunIdsAsync(
        ProcessPersistenceDbContext dbContext,
        DateTimeOffset nowUtc,
        DateTimeOffset? readyUpdatedAfterUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (nowUtc.Offset != TimeSpan.Zero)
        {
            nowUtc = nowUtc.ToUniversalTime();
        }

        if (readyUpdatedAfterUtc is { } cutoff && cutoff.Offset != TimeSpan.Zero)
        {
            readyUpdatedAfterUtc = cutoff.ToUniversalTime();
        }

        var dispatchableCandidateRows = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.Status == ProcessRuntimeStatus.Active)
            .Where(state => readyUpdatedAfterUtc == null || state.UpdatedAtUtc >= readyUpdatedAfterUtc)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, state.UpdatedAtUtc, Step = step })
            .Where(item =>
                item.Step.IsExecutable &&
                item.Step.Status == ProcessRuntimeStepStatus.Ready &&
                item.Step.ActiveClaimToken == null)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Step.AttemptNumber)
            .ThenBy(item => item.Step.StepInstanceId)
            .Select(item => new DispatchableCandidate(
                item.RunId,
                item.Step.StepInstanceId,
                item.UpdatedAtUtc))
            .Take(MaxDispatchableCandidateRows)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var schedulablePendingCandidateRows = await LoadSchedulablePendingCandidateRowsAsync(
            dbContext,
            cancellationToken).ConfigureAwait(false);

        var activeChildParentSteps = await LoadActiveChildParentStepsAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var dispatchableRunIds = dispatchableCandidateRows
            .Concat(schedulablePendingCandidateRows)
            .GroupBy(candidate => candidate.RunId)
            .OrderByDescending(group => group.Max(candidate => candidate.UpdatedAtUtc))
            .Where(group => group.Any(candidate => !activeChildParentSteps.Contains((candidate.RunId, candidate.StepInstanceId))))
            .Select(group => group.Key)
            .Take(MaxDispatchableRuns)
            .ToArray();

        var expiredClaimRunIds = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.Status == ProcessRuntimeStatus.Active)
            .Join(
                dbContext.DispatchClaims.AsNoTracking(),
                state => state.RunId,
                claim => claim.RunId,
                (state, claim) => new { state.RunId, state.UpdatedAtUtc, Claim = claim })
            .Where(item =>
                item.Claim.ExpiresAtUtc <= nowUtc &&
                (item.Claim.Status == DispatchClaimStatus.Claimed ||
                 item.Claim.Status == DispatchClaimStatus.LeaseRenewed ||
                 item.Claim.Status == DispatchClaimStatus.Reclaimed))
            .GroupBy(item => item.RunId)
            .Select(group => new
            {
                RunId = group.Key,
                UpdatedAtUtc = group.Max(item => item.UpdatedAtUtc)
            })
            .OrderByDescending(item => item.UpdatedAtUtc)
            .Select(item => item.RunId)
            .Take(MaxExpiredClaimRuns)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var runIds = new List<Guid>(dispatchableRunIds.Length + expiredClaimRunIds.Length);
        var seen = new HashSet<Guid>();
        AddUnique(runIds, seen, dispatchableRunIds);
        AddUnique(runIds, seen, expiredClaimRunIds);
        return runIds;
    }

    private static async Task<IReadOnlyList<DispatchableCandidate>> LoadSchedulablePendingCandidateRowsAsync(
        ProcessPersistenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var pendingCandidateRows = await dbContext.RuntimeStates
            .AsNoTracking()
            .Where(state => state.Status == ProcessRuntimeStatus.Active || state.Status == ProcessRuntimeStatus.Created)
            .Join(
                dbContext.RuntimeSteps.AsNoTracking(),
                state => state.RunId,
                step => step.RunId,
                (state, step) => new { state.RunId, state.UpdatedAtUtc, Step = step })
            .Where(item =>
                item.Step.IsExecutable &&
                item.Step.Status == ProcessRuntimeStepStatus.Pending &&
                item.Step.ActiveClaimToken == null)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.Step.AttemptNumber)
            .ThenBy(item => item.Step.StepInstanceId)
            .Select(item => new PendingStepCandidate(
                item.RunId,
                item.Step.StepInstanceId,
                item.UpdatedAtUtc,
                item.Step.DependencyStepIds,
                item.Step.RequiredArtifactSlotIds))
            .Take(MaxDispatchableCandidateRows)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (pendingCandidateRows.Length == 0)
        {
            return [];
        }

        var candidateRunIds = pendingCandidateRows
            .Select(candidate => candidate.RunId)
            .Distinct()
            .ToArray();
        var stepRows = await dbContext.RuntimeSteps
            .AsNoTracking()
            .Where(step => candidateRunIds.Contains(step.RunId))
            .Select(step => new RuntimeStepStatusRow(
                step.RunId,
                step.StepInstanceId,
                step.Status))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var availableSlots = await dbContext.AvailableArtifactSlots
            .AsNoTracking()
            .Where(slot => candidateRunIds.Contains(slot.RunId))
            .Select(slot => new AvailableArtifactSlotRow(slot.RunId, slot.SlotId))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var stepsByRun = stepRows
            .GroupBy(step => step.RunId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(step => step.StepInstanceId, step => step.Status));
        var slotsByRun = availableSlots
            .GroupBy(slot => slot.RunId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(slot => slot.SlotId).ToHashSet());
        var candidates = new List<DispatchableCandidate>();
        foreach (var pendingCandidate in pendingCandidateRows)
        {
            if (!stepsByRun.TryGetValue(pendingCandidate.RunId, out var stepStatuses))
            {
                continue;
            }

            slotsByRun.TryGetValue(pendingCandidate.RunId, out var runAvailableSlots);
            runAvailableSlots ??= [];
            if (DependenciesSatisfied(pendingCandidate.DependencyStepIds, stepStatuses) &&
                RequiredArtifactsAvailable(pendingCandidate.RequiredArtifactSlotIds, runAvailableSlots))
            {
                candidates.Add(new DispatchableCandidate(
                    pendingCandidate.RunId,
                    pendingCandidate.StepInstanceId,
                    pendingCandidate.UpdatedAtUtc));
            }
        }

        return candidates;
    }

    private static async Task<HashSet<(Guid RunId, Guid StepInstanceId)>> LoadActiveChildParentStepsAsync(
        ProcessPersistenceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var parentRunKeySnippet = JsonSerializer.Serialize("ParentProcessRunId");
        var parentStepKeySnippet = JsonSerializer.Serialize("ParentProcessStepId");
        var activeChildLaunchVariables = await dbContext.RuntimeStepAssignments
            .AsNoTracking()
            .Join(
                dbContext.RuntimeStates.AsNoTracking(),
                assignment => assignment.RunId,
                state => state.RunId,
                (assignment, state) => new { assignment.LaunchVariablesJson, state.Status })
            .Where(item =>
                item.Status != ProcessRuntimeStatus.Completed &&
                item.Status != ProcessRuntimeStatus.Failed &&
                item.Status != ProcessRuntimeStatus.Cancelled &&
                item.Status != ProcessRuntimeStatus.Blocked &&
                item.LaunchVariablesJson.Contains(parentRunKeySnippet) &&
                item.LaunchVariablesJson.Contains(parentStepKeySnippet))
            .Select(item => item.LaunchVariablesJson)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var parentSteps = new HashSet<(Guid RunId, Guid StepInstanceId)>();
        foreach (var launchVariablesJson in activeChildLaunchVariables)
        {
            if (!TryReadParentStep(launchVariablesJson, out var runId, out var stepInstanceId))
            {
                continue;
            }

            parentSteps.Add((runId, stepInstanceId));
        }

        return parentSteps;
    }

    private static bool TryReadParentStep(
        string launchVariablesJson,
        out Guid runId,
        out Guid stepInstanceId)
    {
        runId = Guid.Empty;
        stepInstanceId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(launchVariablesJson))
        {
            return false;
        }

        try
        {
            var variables = JsonSerializer.Deserialize<Dictionary<string, string>>(launchVariablesJson);
            return variables is not null &&
                   variables.TryGetValue("ParentProcessRunId", out var parentRunId) &&
                   variables.TryGetValue("ParentProcessStepId", out var parentStepId) &&
                   Guid.TryParse(parentRunId, out runId) &&
                   Guid.TryParse(parentStepId, out stepInstanceId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool DependenciesSatisfied(
        string dependencyStepIds,
        IReadOnlyDictionary<Guid, ProcessRuntimeStepStatus> stepStatuses)
    {
        foreach (var dependencyId in SplitGuids(dependencyStepIds))
        {
            if (!stepStatuses.TryGetValue(dependencyId, out var dependencyStatus) ||
                !ProcessRuntimeTerminalStates.IsStepTerminal(dependencyStatus) ||
                dependencyStatus is ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Cancelled)
            {
                return false;
            }
        }

        return true;
    }

    private static bool RequiredArtifactsAvailable(
        string requiredArtifactSlotIds,
        IReadOnlySet<Guid> availableSlots)
    {
        foreach (var slotId in SplitGuids(requiredArtifactSlotIds))
        {
            if (!availableSlots.Contains(slotId))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<Guid> SplitGuids(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Guid.Parse(part);
        }
    }

    private sealed record DispatchableCandidate(
        Guid RunId,
        Guid StepInstanceId,
        DateTimeOffset UpdatedAtUtc);

    private sealed record PendingStepCandidate(
        Guid RunId,
        Guid StepInstanceId,
        DateTimeOffset UpdatedAtUtc,
        string DependencyStepIds,
        string RequiredArtifactSlotIds);

    private sealed record RuntimeStepStatusRow(
        Guid RunId,
        Guid StepInstanceId,
        ProcessRuntimeStepStatus Status);

    private sealed record AvailableArtifactSlotRow(
        Guid RunId,
        Guid SlotId);

    private static void AddUnique(
        List<Guid> target,
        HashSet<Guid> seen,
        IEnumerable<Guid> runIds)
    {
        foreach (var runId in runIds)
        {
            if (seen.Add(runId))
            {
                target.Add(runId);
            }
        }
    }
}
