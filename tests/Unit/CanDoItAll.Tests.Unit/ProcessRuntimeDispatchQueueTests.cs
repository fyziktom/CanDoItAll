using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeDispatchQueueTests
{
    [Fact]
    public void Queue_active_run_gate_is_shared_by_run_id()
    {
        var queue = new ProcessRuntimeDispatchQueue();
        var runId = ProcessRunId.New();

        Assert.True(queue.TryMarkActive(runId));
        Assert.False(queue.TryMarkActive(runId));

        queue.MarkInactive(runId);

        Assert.True(queue.TryMarkActive(runId));
    }

    [Fact]
    public async Task Queue_releases_dedupe_marker_when_enqueue_is_cancelled()
    {
        var queue = new ProcessRuntimeDispatchQueue(new ProcessRuntimeDispatchQueueOptions
        {
            ImmediateQueueCapacity = 1,
            RecoveryQueueCapacity = 1
        });
        var firstRunId = ProcessRunId.New();
        var cancelledRunId = ProcessRunId.New();

        await queue.EnqueueAsync(new ProcessRuntimeDispatchQueueRequest(firstRunId, "unit-test"));

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await queue.EnqueueAsync(
                new ProcessRuntimeDispatchQueueRequest(cancelledRunId, "unit-test"),
                cancelled.Token));

        Assert.True(queue.TryDequeueImmediate(out var firstRequest));
        Assert.Equal(firstRunId, firstRequest.RunId);

        await queue.EnqueueAsync(new ProcessRuntimeDispatchQueueRequest(cancelledRunId, "unit-test"));

        Assert.True(queue.TryDequeueImmediate(out var retriedRequest));
        Assert.Equal(cancelledRunId, retriedRequest.RunId);
    }

    [Fact]
    public async Task Queue_deduplicates_pending_run_until_dequeued()
    {
        var queue = new ProcessRuntimeDispatchQueue();
        var runId = ProcessRunId.New();

        await queue.EnqueueAsync(new ProcessRuntimeDispatchQueueRequest(runId, "unit-test"));
        await queue.EnqueueAsync(new ProcessRuntimeDispatchQueueRequest(runId, "unit-test"));

        Assert.True(queue.TryDequeueImmediate(out var request));
        Assert.Equal(runId, request.RunId);
        Assert.False(queue.TryDequeueImmediate(out _));

        await queue.EnqueueAsync(new ProcessRuntimeDispatchQueueRequest(runId, "unit-test"));

        Assert.True(queue.TryDequeueImmediate(out var requeuedRequest));
        Assert.Equal(runId, requeuedRequest.RunId);
    }

    [Fact]
    public async Task Queue_defers_nonblocking_follow_up_when_recovery_channel_is_full()
    {
        var queue = new ProcessRuntimeDispatchQueue(new ProcessRuntimeDispatchQueueOptions
        {
            ImmediateQueueCapacity = 1,
            RecoveryQueueCapacity = 1
        });
        var queuedRunId = ProcessRunId.New();
        var followUpRunId = ProcessRunId.New();

        await queue.EnqueueAsync(
            new ProcessRuntimeDispatchQueueRequest(
                queuedRunId,
                "recovery-scan",
                IsRecovery: true));

        queue.EnqueueOrDefer(
            new ProcessRuntimeDispatchQueueRequest(
                followUpRunId,
                "completed-child",
                IsRecovery: true));

        Assert.True(queue.TryDequeueRecovery(out var queuedRequest));
        Assert.Equal(queuedRunId, queuedRequest.RunId);
        Assert.Equal(1, queue.FlushDeferredRequests());
        Assert.True(queue.TryDequeueRecovery(out var followUpRequest));
        Assert.Equal(followUpRunId, followUpRequest.RunId);
        Assert.Equal("completed-child", followUpRequest.RequestedBy);
    }

    [Fact]
    public async Task Queue_defers_nonblocking_follow_up_when_immediate_channel_is_full()
    {
        var queue = new ProcessRuntimeDispatchQueue(new ProcessRuntimeDispatchQueueOptions
        {
            ImmediateQueueCapacity = 1,
            RecoveryQueueCapacity = 1
        });
        var queuedRunId = ProcessRunId.New();
        var followUpRunId = ProcessRunId.New();

        await queue.EnqueueAsync(
            new ProcessRuntimeDispatchQueueRequest(queuedRunId, "active-child"));

        queue.EnqueueOrDefer(
            new ProcessRuntimeDispatchQueueRequest(followUpRunId, "released-parent"));

        Assert.True(queue.TryDequeueImmediate(out var queuedRequest));
        Assert.Equal(queuedRunId, queuedRequest.RunId);
        Assert.Equal(1, queue.FlushDeferredRequests());
        Assert.True(queue.TryDequeueImmediate(out var followUpRequest));
        Assert.Equal(followUpRunId, followUpRequest.RunId);
        Assert.Equal("released-parent", followUpRequest.RequestedBy);
    }

    [Fact]
    public void Queue_retains_failed_dispatch_until_its_bounded_retry_delay_elapses()
    {
        var queue = new ProcessRuntimeDispatchQueue(new ProcessRuntimeDispatchQueueOptions
        {
            ImmediateQueueCapacity = 1,
            RecoveryQueueCapacity = 1
        });
        var failedAtUtc = new DateTimeOffset(2026, 7, 26, 20, 0, 0, TimeSpan.Zero);
        var request = new ProcessRuntimeDispatchQueueRequest(
            ProcessRunId.New(),
            "recovery-scan",
            IsRecovery: true);

        Assert.True(queue.TryMarkActive(request.RunId));

        queue.DeferAfterFailure(request, failedAtUtc);
        queue.MarkInactive(request.RunId);

        Assert.Equal(0, queue.FlushDeferredRequests(failedAtUtc.AddMilliseconds(999)));
        Assert.False(queue.TryDequeueRecovery(out _));
        Assert.Equal(1, queue.FlushDeferredRequests(failedAtUtc.AddSeconds(1)));
        Assert.True(queue.TryDequeueRecovery(out var retry));
        Assert.Equal(request, retry);
    }

    [Fact]
    public void Queue_successful_dispatch_resets_failure_backoff()
    {
        var queue = new ProcessRuntimeDispatchQueue(new ProcessRuntimeDispatchQueueOptions
        {
            ImmediateQueueCapacity = 1,
            RecoveryQueueCapacity = 1
        });
        var firstFailureUtc = new DateTimeOffset(2026, 7, 26, 20, 0, 0, TimeSpan.Zero);
        var secondFailureUtc = firstFailureUtc.AddMinutes(1);
        var request = new ProcessRuntimeDispatchQueueRequest(ProcessRunId.New(), "dispatcher");

        queue.DeferAfterFailure(request, firstFailureUtc);
        Assert.Equal(1, queue.FlushDeferredRequests(firstFailureUtc.AddSeconds(1)));
        Assert.True(queue.TryDequeueImmediate(out _));

        queue.MarkDispatchSucceeded(request.RunId);
        queue.DeferAfterFailure(request, secondFailureUtc);

        Assert.Equal(0, queue.FlushDeferredRequests(secondFailureUtc.AddMilliseconds(999)));
        Assert.Equal(1, queue.FlushDeferredRequests(secondFailureUtc.AddSeconds(1)));
        Assert.True(queue.TryDequeueImmediate(out var retry));
        Assert.Equal(request, retry);
    }

    [Fact]
    public async Task Queue_retains_redispatch_enqueued_while_same_run_is_active()
    {
        var queue = new ProcessRuntimeDispatchQueue();
        var runId = ProcessRunId.New();
        var initialRequest = new ProcessRuntimeDispatchQueueRequest(runId, "initial-dispatch");
        var redispatchRequest = new ProcessRuntimeDispatchQueueRequest(runId, "automatic-recovery");

        await queue.EnqueueAsync(initialRequest);
        Assert.True(queue.TryDequeueImmediate(out var dequeuedInitialRequest));
        Assert.True(queue.TryMarkActiveOrDefer(dequeuedInitialRequest));

        await queue.EnqueueAsync(redispatchRequest);
        Assert.True(queue.TryDequeueImmediate(out var dequeuedRedispatchRequest));
        Assert.False(queue.TryMarkActiveOrDefer(dequeuedRedispatchRequest));
        Assert.False(queue.TryDequeueImmediate(out _));

        queue.MarkInactive(runId);

        Assert.Equal(1, queue.FlushDeferredRequests());
        Assert.True(queue.TryDequeueImmediate(out var retainedRedispatchRequest));
        Assert.Equal(runId, retainedRedispatchRequest.RunId);
        Assert.Equal("automatic-recovery", retainedRedispatchRequest.RequestedBy);
        Assert.True(queue.TryMarkActiveOrDefer(retainedRedispatchRequest));
    }

    [Fact]
    public async Task Queue_prefers_immediate_redispatch_when_recovery_and_immediate_requests_are_deferred()
    {
        var queue = new ProcessRuntimeDispatchQueue();
        var runId = ProcessRunId.New();
        var activeRequest = new ProcessRuntimeDispatchQueueRequest(runId, "active-dispatch");
        var recoveryRequest = new ProcessRuntimeDispatchQueueRequest(
            runId,
            "recovery-poll",
            IsRecovery: true);
        var immediateRequest = new ProcessRuntimeDispatchQueueRequest(runId, "operator-rework");

        Assert.True(queue.TryMarkActiveOrDefer(activeRequest));
        Assert.False(queue.TryMarkActiveOrDefer(recoveryRequest));
        Assert.False(queue.TryMarkActiveOrDefer(immediateRequest));

        queue.MarkInactive(runId);

        Assert.Equal(1, queue.FlushDeferredRequests());
        Assert.True(queue.TryDequeueImmediate(out var retainedRequest));
        Assert.Equal("operator-rework", retainedRequest.RequestedBy);
        Assert.False(queue.TryDequeueRecovery(out _));
    }

    [Fact]
    public void Queue_rejects_non_positive_capacity()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ProcessRuntimeDispatchQueue(
            new ProcessRuntimeDispatchQueueOptions
            {
                ImmediateQueueCapacity = 0
            }));

        Assert.Contains(nameof(ProcessRuntimeDispatchQueueOptions.ImmediateQueueCapacity), exception.Message);
    }

    [Fact]
    public void Dispatch_diagnostics_treat_routine_coordination_as_information()
    {
        var result = new ProcessRuntimeDispatchResult(
            ProcessRunId.New(),
            ProcessLaunchStage.Running,
            ProcessRuntimeStatus.Active,
            [
                "Process run '11111111-1111-1111-1111-111111111111' changed concurrently while creating a dispatch claim; retrying with the latest runtime state.",
                "Step 'capture-ui-screenshots' is waiting for active child process run '22222222-2222-2222-2222-222222222222'."
            ]);

        Assert.False(ProcessRuntimeDispatchQueueWorker.ShouldLogDispatchDiagnosticsAsWarning(result));
    }

    [Fact]
    public void Dispatch_diagnostics_treat_completed_routine_coordination_as_information()
    {
        var result = new ProcessRuntimeDispatchResult(
            ProcessRunId.New(),
            ProcessLaunchStage.Completed,
            ProcessRuntimeStatus.Completed,
            [
                "Process run '11111111-1111-1111-1111-111111111111' changed concurrently while creating a dispatch claim; retrying with the latest runtime state."
            ]);

        Assert.False(ProcessRuntimeDispatchQueueWorker.ShouldLogDispatchDiagnosticsAsWarning(result));
    }

    [Fact]
    public void Dispatch_diagnostics_keep_actionable_messages_as_warnings()
    {
        var result = new ProcessRuntimeDispatchResult(
            ProcessRunId.New(),
            ProcessLaunchStage.Running,
            ProcessRuntimeStatus.Active,
            ["Step 'implementation' exceeded the dispatch retry limit of 20 attempts."]);

        Assert.True(ProcessRuntimeDispatchQueueWorker.ShouldLogDispatchDiagnosticsAsWarning(result));
    }

    [Fact]
    public async Task Recovery_query_excludes_stale_active_runs_without_dispatchable_work()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var readyRunId = Guid.NewGuid();
        var staleRunId = Guid.NewGuid();
        var completedRunId = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            readyRunId,
            ProcessRuntimeStatus.Active,
            now.AddMinutes(-30),
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            staleRunId,
            ProcessRuntimeStatus.Active,
            now.AddMinutes(-1),
            ProcessRuntimeStepStatus.Running,
            activeClaimToken: Guid.NewGuid());
        AddRuntimeState(
            dbContext,
            completedRunId,
            ProcessRuntimeStatus.Completed,
            now,
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);

        for (var index = 0; index < 150; index++)
        {
            AddRuntimeState(
                dbContext,
                Guid.NewGuid(),
                ProcessRuntimeStatus.Active,
                now.AddSeconds(-index),
                ProcessRuntimeStepStatus.Running,
                activeClaimToken: Guid.NewGuid());
        }

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);

        Assert.Contains(readyRunId, runIds);
        Assert.DoesNotContain(staleRunId, runIds);
        Assert.DoesNotContain(completedRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_pages_all_ready_runs_instead_of_truncating_at_legacy_cap()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 7, 26, 20, 0, 0, TimeSpan.Zero);
        var expectedRunIds = new HashSet<Guid>();
        for (var index = 0; index < 520; index++)
        {
            var runId = Guid.NewGuid();
            expectedRunIds.Add(runId);
            AddRuntimeState(
                dbContext,
                runId,
                ProcessRuntimeStatus.Active,
                now.AddMilliseconds(-index),
                ProcessRuntimeStepStatus.Ready,
                activeClaimToken: null);
        }

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);

        Assert.Equal(expectedRunIds.Count, runIds.Count);
        Assert.True(expectedRunIds.SetEquals(runIds));
    }

    [Fact]
    public async Task Recovery_query_includes_runs_with_expired_active_claims()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var expiredRunId = Guid.NewGuid();
        var staleRunId = Guid.NewGuid();
        var activeClaimToken = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            staleRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Running,
            activeClaimToken: Guid.NewGuid());
        AddRuntimeState(
            dbContext,
            expiredRunId,
            ProcessRuntimeStatus.Active,
            now.AddMinutes(-10),
            ProcessRuntimeStepStatus.Running,
            activeClaimToken);
        dbContext.DispatchClaims.Add(new ProcessDispatchClaimEntity
        {
            RunId = expiredRunId,
            ClaimToken = activeClaimToken,
            StepInstanceId = Guid.NewGuid(),
            OwnerId = "unit-test",
            Status = DispatchClaimStatus.Claimed,
            AttemptNumber = 1,
            CreatedAtUtc = now.AddHours(-1),
            ExpiresAtUtc = now.AddMinutes(-1)
        });

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);

        Assert.Contains(expiredRunId, runIds);
        Assert.DoesNotContain(staleRunId, runIds);
    }

    [Fact]
    public async Task Blocked_recovery_query_includes_only_blocked_runs()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var blockedRunId = Guid.NewGuid();
        var failedRunId = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            blockedRunId,
            ProcessRuntimeStatus.Blocked,
            now.AddMinutes(-10),
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            failedRunId,
            ProcessRuntimeStatus.Failed,
            now.AddMinutes(-5),
            ProcessRuntimeStepStatus.Failed,
            activeClaimToken: null);

        await dbContext.SaveChangesAsync();

        var blockedRuns = await ProcessRuntimeDispatchRecoveryRunQuery.LoadBlockedRunsPageAsync(dbContext);
        var runIds = blockedRuns.Select(candidate => candidate.RunId).ToArray();

        Assert.Contains(blockedRunId, runIds);
        Assert.DoesNotContain(failedRunId, runIds);
    }

    [Fact]
    public async Task Blocked_recovery_recurring_window_discovers_run_blocked_after_startup()
    {
        await using var dbContext = CreateDbContext();
        var startupWatermarkUtc = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var historicalBlockedRunId = Guid.NewGuid();
        var newlyBlockedRunId = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            historicalBlockedRunId,
            ProcessRuntimeStatus.Blocked,
            startupWatermarkUtc.AddMinutes(-1),
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            newlyBlockedRunId,
            ProcessRuntimeStatus.Blocked,
            startupWatermarkUtc.AddSeconds(1),
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);

        await dbContext.SaveChangesAsync();

        var blockedRuns = await ProcessRuntimeDispatchRecoveryRunQuery.LoadBlockedRunsPageAsync(
            dbContext,
            updatedAtOrAfterUtc: startupWatermarkUtc);

        var blockedRun = Assert.Single(blockedRuns);
        Assert.Equal(newlyBlockedRunId, blockedRun.RunId);
    }

    [Fact]
    public async Task Ready_recovery_query_excludes_blocked_runs_during_recurring_scan()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var blockedRunId = Guid.NewGuid();
        var readyRunId = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            blockedRunId,
            ProcessRuntimeStatus.Blocked,
            now,
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            readyRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(
            dbContext,
            now,
            readyUpdatedAfterUtc: now.AddMinutes(-5));

        Assert.Contains(readyRunId, runIds);
        Assert.DoesNotContain(blockedRunId, runIds);
    }

    [Fact]
    public async Task Blocked_recovery_query_pages_every_run_in_deterministic_recency_order()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var expectedRunIds = new List<Guid>();

        for (var index = 0; index < 260; index++)
        {
            var runId = Guid.NewGuid();
            AddRuntimeState(
                dbContext,
                runId,
                ProcessRuntimeStatus.Blocked,
                now.AddSeconds(-index),
                ProcessRuntimeStepStatus.Blocked,
                activeClaimToken: null);

            expectedRunIds.Add(runId);
        }

        await dbContext.SaveChangesAsync();

        var firstPage = await ProcessRuntimeDispatchRecoveryRunQuery.LoadBlockedRunsPageAsync(dbContext);
        var secondPage = await ProcessRuntimeDispatchRecoveryRunQuery.LoadBlockedRunsPageAsync(
            dbContext,
            firstPage[^1].Cursor);
        var runIds = firstPage
            .Concat(secondPage)
            .Select(candidate => candidate.RunId)
            .ToArray();

        Assert.Equal(ProcessRuntimeDispatchRecoveryRunQuery.BlockedRunPageSize, firstPage.Count);
        Assert.Equal(10, secondPage.Count);
        Assert.Equal(expectedRunIds, runIds);
    }

    [Fact]
    public async Task Blocked_recovery_keyset_cursor_does_not_skip_older_runs_when_newer_rows_change()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var originalRunIds = Enumerable
            .Range(0, 260)
            .Select(_ => Guid.NewGuid())
            .OrderByDescending(runId => runId)
            .ToArray();
        foreach (var runId in originalRunIds)
        {
            AddRuntimeState(
                dbContext,
                runId,
                ProcessRuntimeStatus.Blocked,
                now,
                ProcessRuntimeStepStatus.Blocked,
                activeClaimToken: null);
        }

        await dbContext.SaveChangesAsync();

        var firstPage = await ProcessRuntimeDispatchRecoveryRunQuery.LoadBlockedRunsPageAsync(dbContext);
        foreach (var runId in firstPage.Take(20).Select(candidate => candidate.RunId))
        {
            dbContext.RuntimeStates.Single(state => state.RunId == runId).Status =
                ProcessRuntimeStatus.Active;
        }

        var newlyBlockedRunId = Guid.NewGuid();
        AddRuntimeState(
            dbContext,
            newlyBlockedRunId,
            ProcessRuntimeStatus.Blocked,
            now.AddMinutes(1),
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        await dbContext.SaveChangesAsync();

        var secondPage = await ProcessRuntimeDispatchRecoveryRunQuery.LoadBlockedRunsPageAsync(
            dbContext,
            firstPage[^1].Cursor);

        Assert.Equal(originalRunIds.Skip(250), secondPage.Select(candidate => candidate.RunId));
        Assert.DoesNotContain(secondPage, candidate => candidate.RunId == newlyBlockedRunId);
    }

    [Fact]
    public async Task Recovery_query_includes_active_runs_with_schedulable_pending_steps()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var recoverableRunId = Guid.NewGuid();
        var blockedRunId = Guid.NewGuid();

        AddTwoStepRuntimeState(
            dbContext,
            recoverableRunId,
            now,
            includeRequiredArtifact: true);
        AddTwoStepRuntimeState(
            dbContext,
            blockedRunId,
            now.AddMinutes(-1),
            includeRequiredArtifact: false);

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);

        Assert.Contains(recoverableRunId, runIds);
        Assert.DoesNotContain(blockedRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_ignores_malformed_pending_candidate_without_blocking_other_runs()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var recoverableRunId = Guid.NewGuid();
        var malformedRunId = Guid.NewGuid();

        AddTwoStepRuntimeState(
            dbContext,
            recoverableRunId,
            now,
            includeRequiredArtifact: true);
        AddMalformedPendingRuntimeState(
            dbContext,
            malformedRunId,
            now.AddMinutes(-1));

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);

        Assert.Contains(recoverableRunId, runIds);
        Assert.DoesNotContain(malformedRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_includes_created_runs_with_schedulable_pending_steps()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var createdRunId = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            createdRunId,
            ProcessRuntimeStatus.Created,
            now,
            ProcessRuntimeStepStatus.Pending,
            activeClaimToken: null);

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);

        Assert.Contains(createdRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_excludes_ready_runs_older_than_ready_cutoff()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var staleReadyRunId = Guid.NewGuid();
        var recentReadyRunId = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            staleReadyRunId,
            ProcessRuntimeStatus.Active,
            now.AddMinutes(-30),
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            recentReadyRunId,
            ProcessRuntimeStatus.Active,
            now.AddMinutes(-1),
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(
            dbContext,
            now,
            readyUpdatedAfterUtc: now.AddMinutes(-5));

        Assert.Contains(recentReadyRunId, runIds);
        Assert.DoesNotContain(staleReadyRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_includes_schedulable_pending_runs_older_than_ready_cutoff()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var recoverableRunId = Guid.NewGuid();

        AddTwoStepRuntimeState(
            dbContext,
            recoverableRunId,
            now.AddHours(-1),
            includeRequiredArtifact: true);

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(
            dbContext,
            now,
            readyUpdatedAfterUtc: now.AddMinutes(-5));

        Assert.Contains(recoverableRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_includes_expired_claims_older_than_ready_cutoff()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var expiredRunId = Guid.NewGuid();
        var activeClaimToken = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            expiredRunId,
            ProcessRuntimeStatus.Active,
            now.AddHours(-1),
            ProcessRuntimeStepStatus.Running,
            activeClaimToken);
        dbContext.DispatchClaims.Add(new ProcessDispatchClaimEntity
        {
            RunId = expiredRunId,
            ClaimToken = activeClaimToken,
            StepInstanceId = Guid.NewGuid(),
            OwnerId = "unit-test",
            Status = DispatchClaimStatus.Claimed,
            AttemptNumber = 1,
            CreatedAtUtc = now.AddHours(-1),
            ExpiresAtUtc = now.AddMinutes(-1)
        });

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(
            dbContext,
            now,
            readyUpdatedAfterUtc: now.AddMinutes(-5));

        Assert.Contains(expiredRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_includes_expired_claims_newer_than_ready_cutoff()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var expiredRunId = Guid.NewGuid();
        var activeClaimToken = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            expiredRunId,
            ProcessRuntimeStatus.Active,
            now.AddMinutes(-1),
            ProcessRuntimeStepStatus.Running,
            activeClaimToken);
        dbContext.DispatchClaims.Add(new ProcessDispatchClaimEntity
        {
            RunId = expiredRunId,
            ClaimToken = activeClaimToken,
            StepInstanceId = Guid.NewGuid(),
            OwnerId = "unit-test",
            Status = DispatchClaimStatus.Claimed,
            AttemptNumber = 1,
            CreatedAtUtc = now.AddMinutes(-2),
            ExpiresAtUtc = now.AddSeconds(-10)
        });

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(
            dbContext,
            now,
            readyUpdatedAfterUtc: now.AddMinutes(-5));

        Assert.Contains(expiredRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_excludes_runs_with_non_expired_active_claims()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var claimedRunId = Guid.NewGuid();
        var readyRunId = Guid.NewGuid();
        var activeClaimToken = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            claimedRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Running,
            activeClaimToken);
        AddRuntimeState(
            dbContext,
            readyRunId,
            ProcessRuntimeStatus.Active,
            now.AddMinutes(-1),
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);
        dbContext.DispatchClaims.Add(new ProcessDispatchClaimEntity
        {
            RunId = claimedRunId,
            ClaimToken = activeClaimToken,
            StepInstanceId = Guid.NewGuid(),
            OwnerId = "unit-test",
            Status = DispatchClaimStatus.Claimed,
            AttemptNumber = 1,
            CreatedAtUtc = now.AddSeconds(-10),
            ExpiresAtUtc = now.AddMinutes(5)
        });

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);

        Assert.Contains(readyRunId, runIds);
        Assert.DoesNotContain(claimedRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_excludes_parent_ready_steps_while_child_run_is_active()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var parentRunId = Guid.NewGuid();
        var childRunId = Guid.NewGuid();

        var parentStepId = AddRuntimeState(
            dbContext,
            parentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            childRunId,
            ProcessRuntimeStatus.Active,
            now.AddMinutes(-1),
            ProcessRuntimeStepStatus.Running,
            activeClaimToken: Guid.NewGuid());
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);

        Assert.DoesNotContain(parentRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_includes_parent_ready_steps_after_child_run_is_terminal()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var parentRunId = Guid.NewGuid();
        var childRunId = Guid.NewGuid();

        var parentStepId = AddRuntimeState(
            dbContext,
            parentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            childRunId,
            ProcessRuntimeStatus.Completed,
            now.AddMinutes(-1),
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);

        Assert.Contains(parentRunId, runIds);
    }

    [Fact]
    public async Task Recovery_query_includes_parent_ready_steps_after_child_run_is_blocked()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var parentRunId = Guid.NewGuid();
        var childRunId = Guid.NewGuid();

        var parentStepId = AddRuntimeState(
            dbContext,
            parentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            childRunId,
            ProcessRuntimeStatus.Blocked,
            now.AddMinutes(-1),
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });

        await dbContext.SaveChangesAsync();

        var runIds = await ProcessRuntimeDispatchRecoveryRunQuery.LoadRecoverableRunIdsAsync(dbContext, now);

        Assert.Contains(parentRunId, runIds);
    }

    [Fact]
    public async Task Child_parent_query_includes_only_active_parent_runs_for_terminal_child_requeue()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var activeParentRunId = Guid.NewGuid();
        var completedParentRunId = Guid.NewGuid();
        var childRunId = Guid.NewGuid();

        var activeParentStepId = AddRuntimeState(
            dbContext,
            activeParentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);
        var completedParentStepId = AddRuntimeState(
            dbContext,
            completedParentRunId,
            ProcessRuntimeStatus.Completed,
            now,
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            childRunId,
            ProcessRuntimeStatus.Completed,
            now.AddMinutes(1),
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = activeParentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = activeParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = activeParentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = activeParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = completedParentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = completedParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = "not-a-guid",
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = Guid.NewGuid().ToString("D")
            });

        await dbContext.SaveChangesAsync();

        var parentRunIds = await ProcessRuntimeChildRunParentQuery.LoadActiveParentRunIdsAsync(dbContext, childRunId);

        Assert.Equal([activeParentRunId], parentRunIds);
    }

    [Fact]
    public async Task Child_parent_query_returns_waiting_and_blocked_parent_steps_for_completed_child_rework()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var parentRunId = Guid.NewGuid();
        var waitingParentRunId = Guid.NewGuid();
        var completedParentRunId = Guid.NewGuid();
        var childRunId = Guid.NewGuid();

        var parentStepId = AddRuntimeState(
            dbContext,
            parentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        var waitingParentStepId = AddRuntimeState(
            dbContext,
            waitingParentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Waiting,
            activeClaimToken: null);
        var completedParentStepId = AddRuntimeState(
            dbContext,
            completedParentRunId,
            ProcessRuntimeStatus.Completed,
            now,
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            childRunId,
            ProcessRuntimeStatus.Completed,
            now.AddMinutes(1),
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = waitingParentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = waitingParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = completedParentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = completedParentStepId.ToString("D")
            });

        await dbContext.SaveChangesAsync();

        var parentSteps = await ProcessRuntimeChildRunParentQuery.LoadActiveParentStepsAsync(dbContext, childRunId);

        Assert.Equal(2, parentSteps.Count);
        Assert.Contains(parentSteps, parentStep => parentStep.RunId == parentRunId && parentStep.StepInstanceId == parentStepId);
        Assert.Contains(parentSteps, parentStep => parentStep.RunId == waitingParentRunId && parentStep.StepInstanceId == waitingParentStepId);
    }

    [Fact]
    public async Task Child_parent_query_skips_parent_steps_waiting_on_newer_active_child()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var parentRunId = Guid.NewGuid();
        var readyParentRunId = Guid.NewGuid();
        var stoppedChildRunId = Guid.NewGuid();
        var activeChildRunId = Guid.NewGuid();

        var parentStepId = AddRuntimeState(
            dbContext,
            parentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Waiting,
            activeClaimToken: null);
        var readyParentStepId = AddRuntimeState(
            dbContext,
            readyParentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Ready,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            stoppedChildRunId,
            ProcessRuntimeStatus.Completed,
            now.AddMinutes(1),
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            activeChildRunId,
            ProcessRuntimeStatus.Active,
            now.AddMinutes(2),
            ProcessRuntimeStepStatus.Running,
            activeClaimToken: Guid.NewGuid());
        AddAssignment(
            dbContext,
            stoppedChildRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            stoppedChildRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = readyParentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = readyParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            activeChildRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });

        await dbContext.SaveChangesAsync();

        var parentSteps = await ProcessRuntimeChildRunParentQuery.LoadActiveParentStepsAsync(dbContext, stoppedChildRunId);

        Assert.Empty(parentSteps);
    }

    [Fact]
    public async Task Child_parent_query_returns_active_parent_claims_for_completed_child_claim_release()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var parentRunId = Guid.NewGuid();
        var waitingParentRunId = Guid.NewGuid();
        var closedClaimParentRunId = Guid.NewGuid();
        var postTerminalClaimParentRunId = Guid.NewGuid();
        var childRunId = Guid.NewGuid();
        var parentClaimToken = Guid.NewGuid();
        var closedClaimToken = Guid.NewGuid();
        var postTerminalClaimToken = Guid.NewGuid();

        var parentStepId = AddRuntimeState(
            dbContext,
            parentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Running,
            parentClaimToken);
        var waitingParentStepId = AddRuntimeState(
            dbContext,
            waitingParentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Waiting,
            activeClaimToken: null);
        var closedClaimParentStepId = AddRuntimeState(
            dbContext,
            closedClaimParentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Running,
            closedClaimToken);
        var postTerminalClaimParentStepId = AddRuntimeState(
            dbContext,
            postTerminalClaimParentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Running,
            postTerminalClaimToken);
        AddDispatchClaim(dbContext, parentRunId, parentStepId, parentClaimToken, DispatchClaimStatus.Claimed, now);
        AddDispatchClaim(dbContext, closedClaimParentRunId, closedClaimParentStepId, closedClaimToken, DispatchClaimStatus.Completed, now);
        AddDispatchClaim(dbContext, postTerminalClaimParentRunId, postTerminalClaimParentStepId, postTerminalClaimToken, DispatchClaimStatus.Claimed, now.AddMinutes(2));
        AddRuntimeState(
            dbContext,
            childRunId,
            ProcessRuntimeStatus.Completed,
            now.AddMinutes(1),
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = waitingParentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = waitingParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = closedClaimParentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = closedClaimParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = postTerminalClaimParentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = postTerminalClaimParentStepId.ToString("D")
            });

        await dbContext.SaveChangesAsync();

        var parentClaims = await ProcessRuntimeChildRunParentQuery.LoadActiveParentClaimsAsync(dbContext, childRunId);

        var parentClaim = Assert.Single(parentClaims);
        Assert.Equal(parentRunId, parentClaim.RunId);
        Assert.Equal(parentStepId, parentClaim.StepInstanceId);
        Assert.Equal(parentClaimToken, parentClaim.ClaimToken);
        Assert.Equal("unit-test", parentClaim.OwnerId);
    }

    [Fact]
    public async Task Child_parent_query_returns_active_parent_claims_for_blocked_child_claim_release()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var parentRunId = Guid.NewGuid();
        var childRunId = Guid.NewGuid();
        var parentClaimToken = Guid.NewGuid();

        var parentStepId = AddRuntimeState(
            dbContext,
            parentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Running,
            parentClaimToken);
        AddDispatchClaim(dbContext, parentRunId, parentStepId, parentClaimToken, DispatchClaimStatus.Claimed, now);
        AddRuntimeState(
            dbContext,
            childRunId,
            ProcessRuntimeStatus.Blocked,
            now.AddMinutes(1),
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });

        await dbContext.SaveChangesAsync();

        var parentClaims = await ProcessRuntimeChildRunParentQuery.LoadActiveParentClaimsAsync(dbContext, childRunId);

        var parentClaim = Assert.Single(parentClaims);
        Assert.Equal(parentRunId, parentClaim.RunId);
        Assert.Equal(parentStepId, parentClaim.StepInstanceId);
        Assert.Equal(parentClaimToken, parentClaim.ClaimToken);
    }

    [Fact]
    public async Task Child_parent_query_returns_terminal_child_runs_with_parent_links_for_recovery()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var completedChildRunId = Guid.NewGuid();
        var blockedChildRunId = Guid.NewGuid();
        var activeChildRunId = Guid.NewGuid();
        var terminalUnlinkedChildRunId = Guid.NewGuid();
        var parentRunId = Guid.NewGuid();
        var parentStepId = Guid.NewGuid();

        AddRuntimeState(
            dbContext,
            completedChildRunId,
            ProcessRuntimeStatus.Completed,
            now,
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            blockedChildRunId,
            ProcessRuntimeStatus.Blocked,
            now,
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            activeChildRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            terminalUnlinkedChildRunId,
            ProcessRuntimeStatus.Completed,
            now,
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddAssignment(
            dbContext,
            completedChildRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            blockedChildRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            activeChildRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                [ProcessRuntimeLaunchVariables.ParentProcessRunId] = parentRunId.ToString("D"),
                [ProcessRuntimeLaunchVariables.ParentProcessStepId] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            terminalUnlinkedChildRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>());

        await dbContext.SaveChangesAsync();

        var terminalChildRuns = await ProcessRuntimeChildRunParentQuery.LoadTerminalChildRunsPageAsync(dbContext);

        var expectedChildRunIds = new[] { blockedChildRunId, completedChildRunId }
            .OrderByDescending(runId => runId)
            .ToArray();
        Assert.Equal(expectedChildRunIds, terminalChildRuns.Select(candidate => candidate.RunId));
    }

    [Fact]
    public async Task Child_parent_query_returns_only_blocked_parents_with_the_exact_linked_step()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var childRunId = Guid.NewGuid();
        var blockedParentRunId = Guid.NewGuid();
        var activeParentRunId = Guid.NewGuid();
        var completedParentRunId = Guid.NewGuid();
        var wrongStepParentRunId = Guid.NewGuid();
        var blockedParentStepId = AddRuntimeState(
            dbContext,
            blockedParentRunId,
            ProcessRuntimeStatus.Blocked,
            now,
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        var activeParentStepId = AddRuntimeState(
            dbContext,
            activeParentRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Waiting,
            activeClaimToken: null);
        var completedParentStepId = AddRuntimeState(
            dbContext,
            completedParentRunId,
            ProcessRuntimeStatus.Completed,
            now,
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            wrongStepParentRunId,
            ProcessRuntimeStatus.Blocked,
            now,
            ProcessRuntimeStepStatus.Blocked,
            activeClaimToken: null);
        AddRuntimeState(
            dbContext,
            childRunId,
            ProcessRuntimeStatus.Completed,
            now,
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                new ProcessRunId(blockedParentRunId),
                new ProcessStepInstanceId(blockedParentStepId)));
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                new ProcessRunId(activeParentRunId),
                new ProcessStepInstanceId(activeParentStepId)));
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                new ProcessRunId(completedParentRunId),
                new ProcessStepInstanceId(completedParentStepId)));
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                new ProcessRunId(wrongStepParentRunId),
                ProcessStepInstanceId.New()));

        await dbContext.SaveChangesAsync();

        var parentRunIds = await ProcessRuntimeChildRunParentQuery
            .LoadBlockedParentRunIdsAsync(dbContext, childRunId);

        Assert.Equal([blockedParentRunId], parentRunIds);
    }

    [Fact]
    public async Task Child_parent_query_pages_every_terminal_child_run_with_parent_links()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var parentRunId = Guid.NewGuid();
        var parentStepId = Guid.NewGuid();
        var expected = new List<(Guid RunId, DateTimeOffset UpdatedAtUtc)>();
        for (var index = 0; index < 260; index++)
        {
            var childRunId = Guid.NewGuid();
            var updatedAtUtc = now;
            expected.Add((childRunId, updatedAtUtc));
            AddRuntimeState(
                dbContext,
                childRunId,
                ProcessRuntimeStatus.Completed,
                updatedAtUtc,
                ProcessRuntimeStepStatus.Completed,
                activeClaimToken: null);
            AddAssignment(
                dbContext,
                childRunId,
                Guid.NewGuid(),
                ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                    new ProcessRunId(parentRunId),
                    new ProcessStepInstanceId(parentStepId)));
        }

        await dbContext.SaveChangesAsync();

        var firstPage = await ProcessRuntimeChildRunParentQuery.LoadTerminalChildRunsPageAsync(dbContext);
        var secondPage = await ProcessRuntimeChildRunParentQuery.LoadTerminalChildRunsPageAsync(
            dbContext,
            firstPage[^1].Cursor);
        var actual = firstPage
            .Concat(secondPage)
            .Select(candidate => candidate.RunId)
            .ToArray();
        var expectedRunIds = expected
            .OrderByDescending(candidate => candidate.UpdatedAtUtc)
            .ThenByDescending(candidate => candidate.RunId)
            .Select(candidate => candidate.RunId)
            .ToArray();

        Assert.Equal(ProcessRuntimeChildRunParentQuery.TerminalChildRunPageSize, firstPage.Count);
        Assert.Equal(10, secondPage.Count);
        Assert.Equal(expectedRunIds, actual);
    }

    [Fact]
    public async Task Child_parent_query_recurring_window_excludes_history_and_discovers_new_terminal_rows()
    {
        await using var dbContext = CreateDbContext();
        var recurringWatermarkUtc = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var parentRunId = Guid.NewGuid();
        var parentStepId = Guid.NewGuid();
        var historicalChildRunId = Guid.NewGuid();
        var newChildRunIds = new[]
        {
            Guid.NewGuid(),
            Guid.NewGuid()
        };

        AddRuntimeState(
            dbContext,
            historicalChildRunId,
            ProcessRuntimeStatus.Completed,
            recurringWatermarkUtc.AddMinutes(-1),
            ProcessRuntimeStepStatus.Completed,
            activeClaimToken: null);
        AddAssignment(
            dbContext,
            historicalChildRunId,
            Guid.NewGuid(),
            ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                new ProcessRunId(parentRunId),
                new ProcessStepInstanceId(parentStepId)));
        foreach (var newChildRunId in newChildRunIds)
        {
            AddRuntimeState(
                dbContext,
                newChildRunId,
                ProcessRuntimeStatus.Completed,
                recurringWatermarkUtc.AddSeconds(1),
                ProcessRuntimeStepStatus.Completed,
                activeClaimToken: null);
            AddAssignment(
                dbContext,
                newChildRunId,
                Guid.NewGuid(),
                ProcessRuntimeLaunchVariables.CreateParentStepLookup(
                    new ProcessRunId(parentRunId),
                    new ProcessStepInstanceId(parentStepId)));
        }

        await dbContext.SaveChangesAsync();

        var terminalChildRuns = await ProcessRuntimeChildRunParentQuery.LoadTerminalChildRunsPageAsync(
            dbContext,
            updatedAtOrAfterUtc: recurringWatermarkUtc);

        Assert.Equal(
            newChildRunIds.OrderByDescending(runId => runId),
            terminalChildRuns.Select(candidate => candidate.RunId));
        Assert.DoesNotContain(terminalChildRuns, candidate => candidate.RunId == historicalChildRunId);
    }

    [Fact]
    public async Task Claim_recovery_query_includes_only_active_claimed_or_running_steps_with_open_claims()
    {
        await using var dbContext = CreateDbContext();
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var runningRunId = Guid.NewGuid();
        var claimedRunId = Guid.NewGuid();
        var completedRunId = Guid.NewGuid();
        var mismatchedClaimRunId = Guid.NewGuid();
        var terminalRunId = Guid.NewGuid();
        var runningToken = Guid.NewGuid();
        var claimedToken = Guid.NewGuid();
        var completedToken = Guid.NewGuid();
        var mismatchedStepToken = Guid.NewGuid();
        var terminalToken = Guid.NewGuid();

        var runningStepId = AddRuntimeState(
            dbContext,
            runningRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Running,
            runningToken);
        var claimedStepId = AddRuntimeState(
            dbContext,
            claimedRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Claimed,
            claimedToken);
        var completedStepId = AddRuntimeState(
            dbContext,
            completedRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Completed,
            completedToken);
        var mismatchedClaimStepId = AddRuntimeState(
            dbContext,
            mismatchedClaimRunId,
            ProcessRuntimeStatus.Active,
            now,
            ProcessRuntimeStepStatus.Running,
            mismatchedStepToken);
        var terminalStepId = AddRuntimeState(
            dbContext,
            terminalRunId,
            ProcessRuntimeStatus.Completed,
            now,
            ProcessRuntimeStepStatus.Running,
            terminalToken);
        AddDispatchClaim(dbContext, runningRunId, runningStepId, runningToken, DispatchClaimStatus.Claimed, now);
        AddDispatchClaim(dbContext, claimedRunId, claimedStepId, claimedToken, DispatchClaimStatus.LeaseRenewed, now);
        AddDispatchClaim(dbContext, completedRunId, completedStepId, completedToken, DispatchClaimStatus.Claimed, now);
        AddDispatchClaim(dbContext, mismatchedClaimRunId, mismatchedClaimStepId, Guid.NewGuid(), DispatchClaimStatus.Claimed, now);
        AddDispatchClaim(dbContext, terminalRunId, terminalStepId, terminalToken, DispatchClaimStatus.Claimed, now);

        await dbContext.SaveChangesAsync();

        var candidates = await AgentFrameworkProcessExecutionClaimRecoveryReconciler
            .LoadActiveClaimCandidatesAsync(dbContext);
        var candidateRunIds = candidates.Select(candidate => candidate.RunId).ToArray();

        Assert.Contains(runningRunId, candidateRunIds);
        Assert.Contains(claimedRunId, candidateRunIds);
        Assert.DoesNotContain(completedRunId, candidateRunIds);
        Assert.DoesNotContain(mismatchedClaimRunId, candidateRunIds);
        Assert.DoesNotContain(terminalRunId, candidateRunIds);
    }

    [Fact]
    public void Claim_recovery_selects_only_latest_matching_failed_execution_for_current_claim()
    {
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
            runId,
            stepId,
            Guid.NewGuid(),
            "dispatcher",
            now,
            now.AddMinutes(25));
        var oldFailedExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddMinutes(-3),
            ExecutionState.Failed,
            RunOutcome.Cancelled,
            candidate.ClaimToken);
        var recoveredExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(2),
            ExecutionState.Failed,
            RunOutcome.Cancelled,
            candidate.ClaimToken);

        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [oldFailedExecution, recoveredExecution],
            candidate);

        Assert.Equal(recoveredExecution.Id, selected?.Id);

        var newerRunningExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(3),
            ExecutionState.Running,
            null,
            candidate.ClaimToken);

        selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [oldFailedExecution, recoveredExecution, newerRunningExecution],
            candidate);

        Assert.Null(selected);
    }

    [Fact]
    public void Claim_recovery_selects_latest_matching_completed_execution_for_current_claim()
    {
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
            runId,
            stepId,
            Guid.NewGuid(),
            "dispatcher",
            now,
            now.AddMinutes(25));
        var previousClaimCompletedExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(-10),
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            Guid.NewGuid());
        var oldCompletedExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddMinutes(-3),
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            candidate.ClaimToken);
        var completedExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(2),
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            candidate.ClaimToken);

        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [oldCompletedExecution, previousClaimCompletedExecution, completedExecution],
            candidate);

        Assert.Equal(completedExecution.Id, selected?.Id);

        var newerRunningExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(3),
            ExecutionState.Running,
            null,
            candidate.ClaimToken);

        selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [oldCompletedExecution, completedExecution, newerRunningExecution],
            candidate);

        Assert.Null(selected);
    }

    [Fact]
    public void Claim_recovery_selects_exact_claim_identity_when_another_claim_execution_is_newer()
    {
        var now = new DateTimeOffset(2026, 7, 30, 14, 0, 0, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
            runId,
            stepId,
            Guid.NewGuid(),
            "dispatcher",
            now,
            now.AddMinutes(25));
        var exactExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(1),
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            candidate.ClaimToken);
        var newerOtherClaimExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(2),
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            Guid.NewGuid());

        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [newerOtherClaimExecution, exactExecution],
            candidate);

        Assert.Equal(exactExecution.Id, selected?.Id);
    }

    [Fact]
    public void Claim_recovery_query_filters_exact_identity_before_bounded_take()
    {
        var now = new DateTimeOffset(2026, 7, 30, 14, 30, 0, TimeSpan.Zero);
        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "dispatcher",
            now,
            now.AddMinutes(25));
        var exactExecution = CreateExecutionRun(
            candidate.RunId,
            candidate.StepInstanceId,
            now.AddSeconds(1),
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            candidate.ClaimToken);
        var unrelatedExecutions = Enumerable
            .Range(1, 20)
            .Select(index => CreateExecutionRun(
                candidate.RunId,
                candidate.StepInstanceId,
                now.AddSeconds(index + 1),
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                Guid.NewGuid()))
            .ToArray();
        var query = AgentFrameworkProcessExecutionClaimRecoveryReconciler
            .CreateExecutionRunQuery(candidate);

        var matchingExecutions = unrelatedExecutions
            .Append(exactExecution)
            .Where(executionRun =>
                AgentFrameworkWorkspaceExecutionService.MatchesMetadataStringValues(
                    executionRun.MetadataJson,
                    query.MetadataStringEquals))
            .OrderByDescending(executionRun => executionRun.UpdatedAtUtc)
            .Take(query.Take)
            .ToArray();

        Assert.Equal(
            candidate.ClaimToken.ToString("D"),
            Assert.Single(query.MetadataStringEquals).Value);
        Assert.Equal(exactExecution.Id, Assert.Single(matchingExecutions).Id);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{not-json")]
    [InlineData("""{"agentProcessDispatchClaimIdentity":"not-a-guid"}""")]
    public void Claim_recovery_rejects_missing_or_malformed_dispatch_claim_metadata(
        string metadataJson)
    {
        var executionRun = CreateExecutionRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            Guid.NewGuid()) with
        {
            MetadataJson = metadataJson
        };

        Assert.False(
            AgentFrameworkProcessExecutionClaimRecoveryCoordinator.IsExecutionBoundToClaim(
                executionRun,
                Guid.NewGuid()));
    }

    [Fact]
    public void Claim_recovery_selects_terminal_execution_created_more_than_two_minutes_into_claim_lease()
    {
        var claimCreatedAtUtc = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
            runId,
            stepId,
            Guid.NewGuid(),
            "dispatcher",
            claimCreatedAtUtc,
            claimCreatedAtUtc.AddMinutes(25));
        var completedExecution = CreateExecutionRun(
            runId,
            stepId,
            claimCreatedAtUtc.AddMinutes(3),
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            candidate.ClaimToken);

        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [completedExecution],
            candidate);

        Assert.Equal(completedExecution.Id, selected?.Id);
    }

    [Fact]
    public void Claim_recovery_rejects_terminal_execution_outside_half_open_claim_lease()
    {
        var claimCreatedAtUtc = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var claimExpiresAtUtc = claimCreatedAtUtc.AddMinutes(25);
        var runId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
            runId,
            stepId,
            Guid.NewGuid(),
            "dispatcher",
            claimCreatedAtUtc,
            claimExpiresAtUtc);
        var beforeClaim = CreateExecutionRun(
            runId,
            stepId,
            claimCreatedAtUtc.AddTicks(-1),
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            candidate.ClaimToken);
        var atExpiry = CreateExecutionRun(
            runId,
            stepId,
            claimExpiresAtUtc,
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            candidate.ClaimToken);
        var afterExpiry = CreateExecutionRun(
            runId,
            stepId,
            claimExpiresAtUtc.AddTicks(1),
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            candidate.ClaimToken);

        Assert.Null(AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [beforeClaim],
            candidate));
        Assert.Null(AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [atExpiry],
            candidate));
        Assert.Null(AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [afterExpiry],
            candidate));
    }

    [Theory]
    [InlineData(ExecutionState.Preparing)]
    [InlineData(ExecutionState.Running)]
    [InlineData(ExecutionState.WaitingOnTool)]
    [InlineData(ExecutionState.Persisting)]
    public void Claim_recovery_does_not_treat_active_matching_execution_as_terminal(
        ExecutionState activeState)
    {
        var claimCreatedAtUtc = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
            runId,
            stepId,
            Guid.NewGuid(),
            "dispatcher",
            claimCreatedAtUtc,
            claimCreatedAtUtc.AddMinutes(25));
        var activeExecution = CreateExecutionRun(
            runId,
            stepId,
            claimCreatedAtUtc.AddMinutes(3),
            activeState,
            null,
            candidate.ClaimToken);

        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [activeExecution],
            candidate);

        Assert.Null(selected);
    }

    [Fact]
    public async Task Expired_running_claim_without_execution_blocks_instead_of_replaying()
    {
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var claimToken = DispatchClaimToken.New();
        var ownerId = new DispatcherOwnerId("dispatcher");
        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
            runId.Value,
            stepId.Value,
            claimToken.Value,
            ownerId.Value,
            now.AddMinutes(-25),
            now.AddTicks(-1));

        Assert.Null(AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [],
            candidate));

        var state = new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Running,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: claimToken,
                    CompletedResultKey: null)
            ],
            [
                new DispatchClaimState(
                    claimToken,
                    stepId,
                    ownerId,
                    DispatchClaimStatus.Claimed,
                    AttemptNumber: 1,
                    candidate.CreatedAtUtc,
                    candidate.ExpiresAtUtc,
                    RenewedAtUtc: null,
                    ResultIdempotencyKey: null)
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            candidate.CreatedAtUtc);
        var engine = new ProcessRuntimeEngine(new RecordingRuntimeUnitOfWork());
        var context = new RuntimeCommandContext(
            RuntimeCommandId.New(),
            new ProcessEventActor(
                ProcessEventActorKind.System,
                new ProcessActorId("claim-expiration-test")),
            new ProcessCorrelationId("claim-expiration-test"),
            now);

        var expired = await engine.ExpireClaimsAsync(
            state,
            context,
            new ExpireDispatchClaimsCommand(now));

        Assert.True(expired.Succeeded);
        Assert.Equal(
            DispatchClaimStatus.Expired,
            Assert.Single(expired.State.Claims).Status);
        Assert.Equal(
            ProcessRuntimeStepStatus.Blocked,
            Assert.Single(expired.State.Steps).Status);
        Assert.Equal(ProcessRuntimeStatus.Blocked, expired.State.Status);
        Assert.Contains(
            expired.Events,
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimExpired);
        Assert.Contains(
            expired.Events,
            runtimeEvent =>
                runtimeEvent.EventType == ProcessRuntimeEventTypes.StepBlocked &&
                runtimeEvent.PayloadHash == ProcessRuntimeDiagnosticCodes.RunningClaimExpiredReplayUnsafe);
        Assert.Contains(
            expired.Events,
            runtimeEvent =>
                runtimeEvent.EventType == ProcessRuntimeEventTypes.ProcessRunBlocked &&
                runtimeEvent.PayloadHash == ProcessRuntimeDiagnosticCodes.RunningClaimExpiredReplayUnsafe);
        Assert.DoesNotContain(
            expired.Events,
            runtimeEvent => runtimeEvent.EventType == ProcessRuntimeEventTypes.DispatchClaimReleased);
    }

    [Fact]
    public void Claim_recovery_does_not_select_completed_execution_from_previous_claim()
    {
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var candidate = new AgentFrameworkProcessExecutionClaimRecoveryReconciler.ActiveProcessClaimCandidate(
            runId,
            stepId,
            Guid.NewGuid(),
            "dispatcher",
            now,
            now.AddMinutes(25));
        var previousClaimCompletedExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(2),
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            Guid.NewGuid());

        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [previousClaimCompletedExecution],
            candidate);

        Assert.Null(selected);
    }

    [Fact]
    public void Execution_recovery_observer_skips_old_recovered_run_when_newer_active_execution_exists()
    {
        var now = new DateTimeOffset(2026, 6, 17, 18, 0, 0, TimeSpan.Zero);
        var runId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var claimToken = Guid.NewGuid();
        var recoveredExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddMinutes(-5),
            ExecutionState.Failed,
            RunOutcome.Cancelled,
            claimToken);
        var newerActiveExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddMinutes(-1),
            ExecutionState.Running,
            null,
            claimToken);

        Assert.True(AgentFrameworkProcessExecutionRecoveryObserver.HasNewerActiveExecutionRun(
            [recoveredExecution, newerActiveExecution],
            recoveredExecution));

        var newerActiveDifferentClaimExecution = CreateExecutionRun(
            runId,
            stepId,
            now,
            ExecutionState.Running,
            null,
            Guid.NewGuid());
        Assert.False(AgentFrameworkProcessExecutionRecoveryObserver.HasNewerActiveExecutionRun(
            [recoveredExecution, newerActiveDifferentClaimExecution],
            recoveredExecution));

        var newerCompletedExecution = CreateExecutionRun(
            runId,
            stepId,
            now,
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            claimToken);
        Assert.False(AgentFrameworkProcessExecutionRecoveryObserver.HasNewerActiveExecutionRun(
            [recoveredExecution, newerCompletedExecution],
            recoveredExecution));

        var newerMismatchedExecution = CreateExecutionRun(
            Guid.NewGuid(),
            stepId,
            now,
            ExecutionState.Running,
            null,
            claimToken);
        Assert.False(AgentFrameworkProcessExecutionRecoveryObserver.HasNewerActiveExecutionRun(
            [recoveredExecution, newerMismatchedExecution],
            recoveredExecution));
    }

    [Fact]
    public void Claim_recovery_associates_execution_created_within_claim_lease()
    {
        var executionCreatedAtUtc = new DateTimeOffset(2026, 6, 17, 18, 10, 0, TimeSpan.Zero);
        var claimCreatedAtUtc = executionCreatedAtUtc.AddMinutes(-10);
        var claimExpiresAtUtc = executionCreatedAtUtc.AddMinutes(15);

        Assert.True(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
            claimCreatedAtUtc,
            claimExpiresAtUtc,
            executionCreatedAtUtc));
        Assert.True(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
            executionCreatedAtUtc,
            claimExpiresAtUtc,
            executionCreatedAtUtc));
        Assert.False(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
            executionCreatedAtUtc.AddTicks(1),
            claimExpiresAtUtc,
            executionCreatedAtUtc));
        Assert.True(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
            executionCreatedAtUtc.AddMinutes(-3),
            executionCreatedAtUtc.AddMinutes(2),
            executionCreatedAtUtc));
        Assert.False(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
            executionCreatedAtUtc.AddMinutes(-3),
            executionCreatedAtUtc.AddTicks(-1),
            executionCreatedAtUtc));
        Assert.False(AgentFrameworkProcessExecutionClaimRecoveryCoordinator.CanAssociateClaimWithRecoveredExecution(
            executionCreatedAtUtc.AddMinutes(-3),
            executionCreatedAtUtc,
            executionCreatedAtUtc));
    }

    private static ProcessPersistenceDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase($"process-runtime-dispatch-queue-{Guid.NewGuid():N}")
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private static Guid AddRuntimeState(
        ProcessPersistenceDbContext dbContext,
        Guid runId,
        ProcessRuntimeStatus status,
        DateTimeOffset updatedAtUtc,
        ProcessRuntimeStepStatus stepStatus,
        Guid? activeClaimToken)
    {
        var stepInstanceId = Guid.NewGuid();
        var state = new ProcessRuntimeStateEntity
        {
            RunId = runId,
            RootRunId = runId,
            PlanId = Guid.NewGuid(),
            PlanHash = "sha256:plan",
            Status = status,
            UpdatedAtUtc = updatedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        state.Steps.Add(new ProcessRuntimeStepEntity
        {
            RunId = runId,
            StepInstanceId = stepInstanceId,
            StepDefinitionId = Guid.NewGuid(),
            Status = stepStatus,
            IsExecutable = true,
            AttemptNumber = 0,
            DependencyStepIds = string.Empty,
            RequiredArtifactSlotIds = string.Empty,
            ActiveClaimToken = activeClaimToken
        });
        dbContext.RuntimeStates.Add(state);
        return stepInstanceId;
    }

    private static void AddTwoStepRuntimeState(
        ProcessPersistenceDbContext dbContext,
        Guid runId,
        DateTimeOffset updatedAtUtc,
        bool includeRequiredArtifact)
    {
        var producerStepId = Guid.NewGuid();
        var consumerStepId = Guid.NewGuid();
        var requiredSlotId = Guid.NewGuid();
        var state = new ProcessRuntimeStateEntity
        {
            RunId = runId,
            RootRunId = runId,
            PlanId = Guid.NewGuid(),
            PlanHash = "sha256:plan",
            Status = ProcessRuntimeStatus.Active,
            UpdatedAtUtc = updatedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        state.Steps.Add(new ProcessRuntimeStepEntity
        {
            RunId = runId,
            StepInstanceId = producerStepId,
            StepDefinitionId = Guid.NewGuid(),
            Status = ProcessRuntimeStepStatus.Completed,
            IsExecutable = true,
            AttemptNumber = 1,
            DependencyStepIds = string.Empty,
            RequiredArtifactSlotIds = string.Empty,
            ActiveClaimToken = null
        });
        state.Steps.Add(new ProcessRuntimeStepEntity
        {
            RunId = runId,
            StepInstanceId = consumerStepId,
            StepDefinitionId = Guid.NewGuid(),
            Status = ProcessRuntimeStepStatus.Pending,
            IsExecutable = true,
            AttemptNumber = 0,
            DependencyStepIds = producerStepId.ToString("D"),
            RequiredArtifactSlotIds = requiredSlotId.ToString("D"),
            ActiveClaimToken = null
        });
        if (includeRequiredArtifact)
        {
            state.AvailableArtifactSlots.Add(new ProcessRuntimeAvailableArtifactSlotEntity
            {
                RunId = runId,
                SlotId = requiredSlotId
            });
        }

        dbContext.RuntimeStates.Add(state);
    }

    private static void AddMalformedPendingRuntimeState(
        ProcessPersistenceDbContext dbContext,
        Guid runId,
        DateTimeOffset updatedAtUtc)
    {
        var state = new ProcessRuntimeStateEntity
        {
            RunId = runId,
            RootRunId = runId,
            PlanId = Guid.NewGuid(),
            PlanHash = "sha256:plan",
            Status = ProcessRuntimeStatus.Active,
            UpdatedAtUtc = updatedAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        state.Steps.Add(new ProcessRuntimeStepEntity
        {
            RunId = runId,
            StepInstanceId = Guid.NewGuid(),
            StepDefinitionId = Guid.NewGuid(),
            Status = ProcessRuntimeStepStatus.Pending,
            IsExecutable = true,
            AttemptNumber = 0,
            DependencyStepIds = "not-a-guid",
            RequiredArtifactSlotIds = string.Empty,
            ActiveClaimToken = null
        });
        dbContext.RuntimeStates.Add(state);
    }

    private static void AddAssignment(
        ProcessPersistenceDbContext dbContext,
        Guid runId,
        Guid stepInstanceId,
        IReadOnlyDictionary<string, string> launchVariables)
    {
        dbContext.RuntimeStepAssignments.Add(new ProcessRuntimeStepAssignmentEntity
        {
            RunId = runId,
            StepInstanceId = stepInstanceId,
            PlanId = Guid.NewGuid(),
            StepKey = "child-step",
            RoleKey = "software-engineer",
            ExecutorKind = "agent",
            ExecutorId = "agent-1",
            ExecutorDisplayName = ".NET Application Developer",
            Prompt = "Prompt",
            ReadinessHash = "sha256:readiness",
            AssignmentReason = "Unit test",
            ProducedArtifactSlotIds = string.Empty,
            RequiredArtifactSlotIds = string.Empty,
            AllowedOperations = string.Empty,
            OperationTargetScope = string.Empty,
            LaunchVariablesJson = JsonSerializer.Serialize(launchVariables),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
    }

    private static void AddDispatchClaim(
        ProcessPersistenceDbContext dbContext,
        Guid runId,
        Guid stepInstanceId,
        Guid claimToken,
        DispatchClaimStatus status,
        DateTimeOffset createdAtUtc)
    {
        dbContext.DispatchClaims.Add(new ProcessDispatchClaimEntity
        {
            RunId = runId,
            ClaimToken = claimToken,
            StepInstanceId = stepInstanceId,
            OwnerId = "unit-test",
            Status = status,
            AttemptNumber = 1,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.AddMinutes(25)
        });
    }

    private static ExecutionRunRecord CreateExecutionRun(
        Guid runId,
        Guid stepId,
        DateTimeOffset createdAtUtc,
        ExecutionState state,
        RunOutcome? outcome,
        Guid dispatchClaimToken)
    {
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Unit test execution",
            SourceKind: "process",
            SourceId: runId.ToString("D"),
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: Guid.NewGuid().ToString("N"),
            RequestedBy: "unit-test",
            RequestedByKind: "test",
            MetadataJson: JsonSerializer.Serialize(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessDispatchClaimExecutionMetadata.MetadataKey] =
                        dispatchClaimToken.ToString("D")
                }),
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "provider",
            Model: "model",
            State: state,
            Outcome: outcome,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: createdAtUtc.AddSeconds(1),
            StartedAtUtc: createdAtUtc,
            CompletedAtUtc: state == ExecutionState.Failed ? createdAtUtc.AddSeconds(1) : null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: runId.ToString("D"),
            ProcessStepId: stepId.ToString("D"));
    }

    private sealed class RecordingRuntimeUnitOfWork : IProcessRuntimeUnitOfWork
    {
        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ProcessRuntimeCommitResult.FromMutation(request.Mutation));
        }
    }
}
