using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
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
