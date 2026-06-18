using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessRuntimeDispatchQueue : IProcessRuntimeDispatchQueue
{
    private readonly Channel<ProcessRuntimeDispatchQueueRequest> immediateChannel = Channel.CreateUnbounded<ProcessRuntimeDispatchQueueRequest>();
    private readonly Channel<ProcessRuntimeDispatchQueueRequest> recoveryChannel = Channel.CreateUnbounded<ProcessRuntimeDispatchQueueRequest>();
    private readonly ConcurrentDictionary<ProcessRunId, byte> immediateQueuedRunIds = new();
    private readonly ConcurrentDictionary<ProcessRunId, byte> recoveryQueuedRunIds = new();

    public ValueTask EnqueueAsync(
        ProcessRuntimeDispatchQueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var queuedRunIds = request.IsRecovery
            ? recoveryQueuedRunIds
            : immediateQueuedRunIds;
        if (!queuedRunIds.TryAdd(request.RunId, 0))
        {
            return ValueTask.CompletedTask;
        }

        var channel = request.IsRecovery
            ? recoveryChannel
            : immediateChannel;
        return channel.Writer.WriteAsync(request, cancellationToken);
    }

    public bool TryDequeueImmediate(out ProcessRuntimeDispatchQueueRequest request)
    {
        if (immediateChannel.Reader.TryRead(out request!))
        {
            immediateQueuedRunIds.TryRemove(request.RunId, out _);
            return true;
        }

        request = default!;
        return false;
    }

    public bool TryDequeueRecovery(out ProcessRuntimeDispatchQueueRequest request)
    {
        if (recoveryChannel.Reader.TryRead(out request!))
        {
            recoveryQueuedRunIds.TryRemove(request.RunId, out _);
            return true;
        }

        request = default!;
        return false;
    }

}

internal sealed class ProcessRuntimeDispatchQueueOptions
{
    public const string ConfigurationSectionName = "Processes:RuntimeDispatchQueue";

    public bool EnableRecovery { get; set; } = true;
}

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
        var activeRunIds = new HashSet<ProcessRunId>();
        if (recoveryEnabled)
        {
            await EnqueueRecoverableRunsAsync(readyRecoveryStartedAtUtc, stoppingToken).ConfigureAwait(false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ObserveCompletedDispatchesAsync(activeImmediateDispatches, activeRunIds).ConfigureAwait(false);
                await ObserveCompletedDispatchesAsync(activeRecoveryDispatches, activeRunIds).ConfigureAwait(false);

                var nowUtc = DateTimeOffset.UtcNow;
                if (recoveryEnabled && nowUtc >= nextRecoveryAtUtc)
                {
                    await EnqueueRecoverableRunsAsync(readyRecoveryStartedAtUtc, stoppingToken).ConfigureAwait(false);
                    nextRecoveryAtUtc = nowUtc.Add(RecoveryPollInterval);
                }

                while (activeImmediateDispatches.Count < MaxImmediateDispatches &&
                       queue.TryDequeueImmediate(out var request))
                {
                    TryStartDispatch(activeImmediateDispatches, activeRunIds, request, stoppingToken);
                }

                while (activeRecoveryDispatches.Count < MaxRecoveryDispatches &&
                       queue.TryDequeueRecovery(out var request))
                {
                    TryStartDispatch(activeRecoveryDispatches, activeRunIds, request, stoppingToken);
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
                            activeRecoveryDispatches,
                            activeRunIds).ConfigureAwait(false);
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

        await ObserveCompletedDispatchesAsync(activeImmediateDispatches, activeRunIds).ConfigureAwait(false);
        await ObserveCompletedDispatchesAsync(activeRecoveryDispatches, activeRunIds).ConfigureAwait(false);
    }

    private void TryStartDispatch(
        Dictionary<Task, ProcessRunId> activeDispatches,
        HashSet<ProcessRunId> activeRunIds,
        ProcessRuntimeDispatchQueueRequest request,
        CancellationToken cancellationToken)
    {
        if (!activeRunIds.Add(request.RunId))
        {
            logger.LogDebug(
                "Skipping queued process dispatch for run {RunId} because another dispatch is already active.",
                request.RunId.Value);
            return;
        }

        activeDispatches.Add(DispatchAsync(request, cancellationToken), request.RunId);
    }

    private static async Task ObserveCompletedDispatchesAsync(
        Dictionary<Task, ProcessRunId> activeDispatches,
        HashSet<ProcessRunId> activeRunIds)
    {
        var completedTasks = activeDispatches
            .Keys
            .Where(task => task.IsCompleted)
            .ToArray();
        foreach (var completedTask in completedTasks)
        {
            await ObserveDispatchCompletionAsync(completedTask, activeDispatches, activeRunIds)
                .ConfigureAwait(false);
        }
    }

    private static async Task ObserveDispatchCompletionAsync(
        Task dispatchTask,
        Dictionary<Task, ProcessRunId> firstActiveDispatches,
        Dictionary<Task, ProcessRunId> secondActiveDispatches,
        HashSet<ProcessRunId> activeRunIds)
    {
        if (!firstActiveDispatches.Remove(dispatchTask, out var runId))
        {
            secondActiveDispatches.Remove(dispatchTask, out runId);
        }

        if (runId != default)
        {
            activeRunIds.Remove(runId);
        }

        await ObserveDispatchTaskAsync(dispatchTask).ConfigureAwait(false);
    }

    private static async Task ObserveDispatchCompletionAsync(
        Task dispatchTask,
        Dictionary<Task, ProcessRunId> activeDispatches,
        HashSet<ProcessRunId> activeRunIds)
    {
        if (activeDispatches.Remove(dispatchTask, out var runId))
        {
            activeRunIds.Remove(runId);
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
            if (ProcessRuntimeTerminalStates.IsRunTerminal(result.Status))
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ProcessPersistenceDbContext>();
                if (result.Status == ProcessRuntimeStatus.Completed)
                {
                    var operatorService = scope.ServiceProvider.GetRequiredService<ProcessRuntimeOperatorApplicationService>();
                    var parentSteps = await ProcessRuntimeChildRunParentQuery
                        .LoadActiveParentStepsAsync(dbContext, request.RunId.Value, cancellationToken)
                        .ConfigureAwait(false);
                    foreach (var parentStep in parentSteps)
                    {
                        var rework = await operatorService.ExecuteAsync(
                            new ProcessRuntimeOperatorActionCommand(
                                new ProcessRunId(parentStep.RunId),
                                new ProcessStepInstanceId(parentStep.StepInstanceId),
                                ProcessRuntimeOperatorActionKind.RequestRework,
                                NormalizeRequestedBy(request.RequestedBy),
                                $"Child process run '{request.RunId}' completed; parent subprocess step can re-evaluate child evidence."),
                            cancellationToken).ConfigureAwait(false);
                        if (!rework.Succeeded)
                        {
                            logger.LogWarning(
                                "Process background dispatch could not requeue parent step {StepInstanceId} for parent run {ParentRunId} after completed child run {ChildRunId}. Diagnostics={Diagnostics}",
                                parentStep.StepInstanceId,
                                parentStep.RunId,
                                request.RunId.Value,
                                string.Join("; ", rework.Diagnostics));
                        }
                    }
                }

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

            logger.LogInformation(
                "Process background dispatch completed for run {RunId}. Stage={Stage} Status={Status} Diagnostics={DiagnosticCount}",
                request.RunId.Value,
                result.Stage,
                result.Status,
                result.Diagnostics.Count);
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

    private async Task EnqueueRecoverableRunsAsync(
        DateTimeOffset readyUpdatedAfterUtc,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProcessPersistenceDbContext>();
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

        var activeChildParentSteps = await LoadActiveChildParentStepsAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var dispatchableRunIds = dispatchableCandidateRows
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

    private sealed record DispatchableCandidate(
        Guid RunId,
        Guid StepInstanceId,
        DateTimeOffset UpdatedAtUtc);

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
