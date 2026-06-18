using System.Text.Json;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeDispatchQueueTests
{
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
            now,
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
            now,
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
}
