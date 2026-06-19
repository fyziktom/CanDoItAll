using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
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
                ["ParentProcessRunId"] = parentRunId.ToString("D"),
                ["ParentProcessStepId"] = parentStepId.ToString("D")
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
                ["ParentProcessRunId"] = parentRunId.ToString("D"),
                ["ParentProcessStepId"] = parentStepId.ToString("D")
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
                ["ParentProcessRunId"] = parentRunId.ToString("D"),
                ["ParentProcessStepId"] = parentStepId.ToString("D")
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
                ["ParentProcessRunId"] = activeParentRunId.ToString("D"),
                ["ParentProcessStepId"] = activeParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["ParentProcessRunId"] = activeParentRunId.ToString("D"),
                ["ParentProcessStepId"] = activeParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["ParentProcessRunId"] = completedParentRunId.ToString("D"),
                ["ParentProcessStepId"] = completedParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["ParentProcessRunId"] = "not-a-guid",
                ["ParentProcessStepId"] = Guid.NewGuid().ToString("D")
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
                ["ParentProcessRunId"] = parentRunId.ToString("D"),
                ["ParentProcessStepId"] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["ParentProcessRunId"] = waitingParentRunId.ToString("D"),
                ["ParentProcessStepId"] = waitingParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["ParentProcessRunId"] = completedParentRunId.ToString("D"),
                ["ParentProcessStepId"] = completedParentStepId.ToString("D")
            });

        await dbContext.SaveChangesAsync();

        var parentSteps = await ProcessRuntimeChildRunParentQuery.LoadActiveParentStepsAsync(dbContext, childRunId);

        Assert.Equal(2, parentSteps.Count);
        Assert.Contains(parentSteps, parentStep => parentStep.RunId == parentRunId && parentStep.StepInstanceId == parentStepId);
        Assert.Contains(parentSteps, parentStep => parentStep.RunId == waitingParentRunId && parentStep.StepInstanceId == waitingParentStepId);
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
                ["ParentProcessRunId"] = parentRunId.ToString("D"),
                ["ParentProcessStepId"] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["ParentProcessRunId"] = waitingParentRunId.ToString("D"),
                ["ParentProcessStepId"] = waitingParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["ParentProcessRunId"] = closedClaimParentRunId.ToString("D"),
                ["ParentProcessStepId"] = closedClaimParentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            childRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["ParentProcessRunId"] = postTerminalClaimParentRunId.ToString("D"),
                ["ParentProcessStepId"] = postTerminalClaimParentStepId.ToString("D")
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
                ["ParentProcessRunId"] = parentRunId.ToString("D"),
                ["ParentProcessStepId"] = parentStepId.ToString("D")
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
                ["ParentProcessRunId"] = parentRunId.ToString("D"),
                ["ParentProcessStepId"] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            blockedChildRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["ParentProcessRunId"] = parentRunId.ToString("D"),
                ["ParentProcessStepId"] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            activeChildRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>
            {
                ["ParentProcessRunId"] = parentRunId.ToString("D"),
                ["ParentProcessStepId"] = parentStepId.ToString("D")
            });
        AddAssignment(
            dbContext,
            terminalUnlinkedChildRunId,
            Guid.NewGuid(),
            new Dictionary<string, string>());

        await dbContext.SaveChangesAsync();

        var terminalChildRunIds = await ProcessRuntimeChildRunParentQuery.LoadTerminalChildRunIdsWithParentLinksAsync(dbContext);

        var expectedChildRunIds = new[] { blockedChildRunId, completedChildRunId }
            .OrderBy(runId => runId)
            .ToArray();
        Assert.Equal(expectedChildRunIds, terminalChildRunIds);
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
            RunOutcome.Cancelled);
        var recoveredExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(2),
            ExecutionState.Failed,
            RunOutcome.Cancelled);

        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [oldFailedExecution, recoveredExecution],
            candidate);

        Assert.Equal(recoveredExecution.Id, selected?.Id);

        var newerRunningExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(3),
            ExecutionState.Running,
            null);

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
        var oldCompletedExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddMinutes(-3),
            ExecutionState.Completed,
            RunOutcome.Succeeded);
        var completedExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(2),
            ExecutionState.Completed,
            RunOutcome.Succeeded);

        var selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [oldCompletedExecution, completedExecution],
            candidate);

        Assert.Equal(completedExecution.Id, selected?.Id);

        var newerRunningExecution = CreateExecutionRun(
            runId,
            stepId,
            now.AddSeconds(3),
            ExecutionState.Running,
            null);

        selected = AgentFrameworkProcessExecutionClaimRecoveryReconciler.SelectRecoverableExecution(
            [oldCompletedExecution, completedExecution, newerRunningExecution],
            candidate);

        Assert.Null(selected);
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
        RunOutcome? outcome)
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
            MetadataJson: "{}",
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
}
