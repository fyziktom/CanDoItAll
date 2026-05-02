using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRunAutomationDispatchServiceTests
{
    [Fact]
    public void HasBlockingAutomationExecutionRun_ignores_failed_manual_debug_runs()
    {
        var hasBlockingRun = ProcessRunAutomationDispatchService.HasBlockingAutomationExecutionRun(
        [
            CreateExecutionRun("agent-run-debug", ExecutionState.Failed, RunOutcome.Failed)
        ]);

        Assert.False(hasBlockingRun);
    }

    [Fact]
    public void HasBlockingAutomationExecutionRun_ignores_completed_automation_runs()
    {
        var hasBlockingRun = ProcessRunAutomationDispatchService.HasBlockingAutomationExecutionRun(
        [
            CreateExecutionRun("process-automation-dispatch", ExecutionState.Completed, RunOutcome.Succeeded)
        ]);

        Assert.False(hasBlockingRun);
    }

    [Fact]
    public void HasBlockingAutomationExecutionRun_blocks_active_automation_runs()
    {
        var hasBlockingRun = ProcessRunAutomationDispatchService.HasBlockingAutomationExecutionRun(
        [
            CreateExecutionRun("process-automation-dispatch", ExecutionState.Running, null)
        ]);

        Assert.True(hasBlockingRun);
    }

    [Fact]
    public void ResolveBlockingAutomationExecutionRunId_returns_latest_fresh_active_automation_run()
    {
        var olderRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Preparing, null) with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2)
        };
        var latestRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Running, null) with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        var blockingRunId = ProcessRunAutomationDispatchService.ResolveBlockingAutomationExecutionRunId(
            [olderRun, latestRun],
            DateTimeOffset.UtcNow);

        Assert.Equal(latestRun.Id, blockingRunId);
    }

    [Fact]
    public void ResolveBlockingAutomationExecutionRunId_ignores_manual_or_stale_runs()
    {
        var staleCreatedAtUtc = DateTimeOffset.Parse("2026-04-19T09:18:25+00:00");
        var blockingRunId = ProcessRunAutomationDispatchService.ResolveBlockingAutomationExecutionRunId(
        [
            CreateExecutionRun("agent-run-debug", ExecutionState.Running, null) with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            CreateExecutionRun("process-automation-dispatch", ExecutionState.Preparing, null) with
            {
                CreatedAtUtc = staleCreatedAtUtc,
                StartedAtUtc = staleCreatedAtUtc,
                UpdatedAtUtc = staleCreatedAtUtc
            }
        ],
            staleCreatedAtUtc.AddMinutes(11));

        Assert.Null(blockingRunId);
    }

    [Fact]
    public void HasBlockingAutomationExecutionRun_ignores_stale_active_automation_runs()
    {
        var createdAtUtc = DateTimeOffset.Parse("2026-04-19T09:18:25+00:00");
        var staleRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Preparing, null) with
        {
            CreatedAtUtc = createdAtUtc,
            StartedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc
        };

        var hasBlockingRun = ProcessRunAutomationDispatchService.HasBlockingAutomationExecutionRun(
            [staleRun],
            createdAtUtc.AddMinutes(11));

        Assert.False(hasBlockingRun);
    }

    [Fact]
    public void HasBlockingAutomationExecutionRun_keeps_silent_automation_runs_blocking_for_longer_recovery_window()
    {
        var createdAtUtc = DateTimeOffset.Parse("2026-04-19T09:18:25+00:00");
        var quietRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Running, null) with
        {
            CreatedAtUtc = createdAtUtc,
            StartedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc.AddMinutes(4)
        };

        var hasBlockingRun = ProcessRunAutomationDispatchService.HasBlockingAutomationExecutionRun(
            [quietRun],
            createdAtUtc.AddMinutes(8));

        Assert.True(hasBlockingRun);
    }

    [Fact]
    public async Task LoadLatestManualRecoveryDirective_filters_started_at_with_sqlite()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(TestApplicationBootstrap.ModuleAssemblies);
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=False");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var runId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var stepStartedAtUtc = DateTimeOffset.Parse("2026-04-27T12:00:00+00:00");

        dbContext.Set<ProcessJournalEntry>().AddRange(
            new ProcessJournalEntry
            {
                ProcessRunId = runId,
                StepRunId = stepRunId,
                EventType = ProcessRuntimeEventTypes.ManualAgentStepRerun,
                Title = "Old directive",
                Description = "old",
                OccurredAtUtc = stepStartedAtUtc.AddMinutes(-1)
            },
            new ProcessJournalEntry
            {
                ProcessRunId = runId,
                StepRunId = stepRunId,
                EventType = ProcessRuntimeEventTypes.ManualAgentStepRerun,
                Title = "New directive",
                Description = "new",
                OccurredAtUtc = stepStartedAtUtc.AddMinutes(1)
            },
            new ProcessJournalEntry
            {
                ProcessRunId = runId,
                StepRunId = stepRunId,
                EventType = "other-event",
                Title = "Other event",
                Description = "wrong",
                OccurredAtUtc = stepStartedAtUtc.AddMinutes(2)
            });
        await dbContext.SaveChangesAsync();

        var loadLatestManualRecoveryDirective = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "LoadLatestManualRecoveryDirectiveAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("LoadLatestManualRecoveryDirectiveAsync method was not found.");
        var directiveTask = (Task<string>)loadLatestManualRecoveryDirective.Invoke(
            null,
            [dbContext, runId, stepRunId, stepStartedAtUtc, CancellationToken.None])!;

        var directive = await directiveTask;

        Assert.Equal("new", directive);
    }

    [Fact]
    public void BuildCanonicalProjectStructureGroundingSql_uses_postgresql_safe_identifiers_and_values()
    {
        var sql = InvokeBuildCanonicalProjectStructureGroundingSql(isPostgreSql: true);

        Assert.Contains("FROM \"Workbench_ProjectObjects\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"ProjectId\" = @projectId", sql, StringComparison.Ordinal);
        Assert.Contains("\"IsSystemManaged\" = FALSE", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("$projectId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("= 0", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCanonicalProjectStructureGroundingSql_uses_sqlite_safe_parameter_name()
    {
        var sql = InvokeBuildCanonicalProjectStructureGroundingSql(isPostgreSql: false);

        Assert.Contains("FROM \"Workbench_ProjectObjects\"", sql, StringComparison.Ordinal);
        Assert.Contains("lower(\"ProjectId\") = lower(@projectId)", sql, StringComparison.Ordinal);
        Assert.Contains("\"IsSystemManaged\" = 0", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("$projectId", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveExternalTargetHintFromProjectStructureGrounding_trims_sentence_tail_from_output_root()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "TryResolveExternalTargetHintFromProjectStructureGrounding",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveExternalTargetHintFromProjectStructureGrounding method was not found.");
        object?[] arguments =
        [
            "Build a small app. Output root: C:\\programovani\\dotnet\\ReadingTimeBudgeter. Acceptance: restore, build, test, run, and capture browser proof.",
            string.Empty,
            string.Empty
        ];

        var resolved = (bool)method.Invoke(null, arguments)!;

        Assert.True(resolved);
        Assert.Equal("C:\\programovani\\dotnet\\ReadingTimeBudgeter", arguments[1]);
        Assert.Equal("external-target/C/programovani/dotnet/ReadingTimeBudgeter", arguments[2]);
    }

    [Fact]
    public void TrySplitExternalTargetAliasForScaffold_returns_parent_alias_and_project_name()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "TrySplitExternalTargetAliasForScaffold",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TrySplitExternalTargetAliasForScaffold method was not found.");
        object?[] arguments =
        [
            "external-target/C/programovani/dotnet/ReadingTimeBudgeter",
            string.Empty,
            string.Empty
        ];

        var split = (bool)method.Invoke(null, arguments)!;

        Assert.True(split);
        Assert.Equal("external-target/C/programovani/dotnet", arguments[1]);
        Assert.Equal("ReadingTimeBudgeter", arguments[2]);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_removes_ancestor_roots()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/dotnet",
            "external-target/C/programovani/dotnet/ReadingTimeBudgeter"
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/dotnet/ReadingTimeBudgeter", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_removes_ambiguous_prefix_aliases()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/dotnet/ReadingTimeBudgeter",
            "external-target/C/programova"
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/dotnet/ReadingTimeBudgeter", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_inline_project_structure_notes()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/dotnet/ReadingTimeBudgeter; notes: Authoritative external product root for the generated app."
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/dotnet/ReadingTimeBudgeter", alias);
    }

    [Fact]
    public void ResolveOutOfScopeExternalTargetReferenceSummary_rejects_sibling_product_references()
    {
        var summary = ProcessRunAutomationDispatchService.ResolveOutOfScopeExternalTargetReferenceSummary(
            """
            References and evidence reviewed:
            - external-target/C/programovani/dotnet/ReadingTimeBudgeter/Program.cs
            - external-target/C/programovani/dotnet/UnrelatedSample/Program.cs
            """,
            ["external-target/C/programovani/dotnet/ReadingTimeBudgeter"]);

        Assert.Contains("outside the current grounded product root", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact stale paths are omitted", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UnrelatedSample/Program.cs", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ReadingTimeBudgeter/Program.cs", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveOutOfScopeExternalTargetReferenceSummary_ignores_malformed_allowed_prefix_aliases()
    {
        var summary = ProcessRunAutomationDispatchService.ResolveOutOfScopeExternalTargetReferenceSummary(
            """
            References and evidence reviewed:
            - external-target/C/programovani/dotnet/ReadingTimeBudgeter/Program.cs
            - external-target/C/programovani/dotnet/UnrelatedSample/Program.cs
            """,
            [
                "external-target/C/programovani/dotnet/ReadingTimeBudgeter",
                "external-target/C/programova"
            ]);

        Assert.Contains("outside the current grounded product root", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exact stale paths are omitted", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UnrelatedSample/Program.cs", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ReadingTimeBudgeter/Program.cs", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveOutOfScopeExternalTargetReferenceSummary_allows_documented_scaffold_parent()
    {
        var summary = ProcessRunAutomationDispatchService.ResolveOutOfScopeExternalTargetReferenceSummary(
            """
            Use scaffold parent parentDirectory: external-target/C/programovani/dotnet with name ReadingTimeBudgeter.
            Product root: external-target/C/programovani/dotnet/ReadingTimeBudgeter.
            """,
            ["external-target/C/programovani/dotnet/ReadingTimeBudgeter"]);

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveOutOfScopeExternalTargetReferenceSummary_allows_unrelated_absolute_managed_workspace_paths()
    {
        var summary = ProcessRunAutomationDispatchService.ResolveOutOfScopeExternalTargetReferenceSummary(
            """
            Architecture context:
            - C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\workspace\project-structure-context-brief\architecture-decision-record.md
            Product root:
            - external-target/C/programovani/dotnet/ReadingTimeBudgeter
            """,
            ["external-target/C/programovani/dotnet/ReadingTimeBudgeter"]);

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveOutOfScopeExternalTargetReferenceSummary_rejects_absolute_sibling_product_references()
    {
        var summary = ProcessRunAutomationDispatchService.ResolveOutOfScopeExternalTargetReferenceSummary(
            """
            References and evidence reviewed:
            - C:\programovani\dotnet\ReadingTimeBudgeter\Program.cs
            - C:\programovani\dotnet\UnrelatedSample\Program.cs
            """,
            ["external-target/C/programovani/dotnet/ReadingTimeBudgeter"]);

        Assert.Contains("outside the current grounded product root", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("artifacts/scopes/organization/demo/project-structure-context-brief.md", true)]
    [InlineData("artifacts/project-structure-context-brief.md", true)]
    [InlineData("artifacts/scopes/organization/demo/process-runs/0001/01-scope-boundary-packet.md", false)]
    [InlineData("artifacts/scopes/organization/demo/deliveries/app/process/implementation/implementation-change-set.md", false)]
    [InlineData("artifacts/process-runs/0001/01-scope-boundary-packet.md", false)]
    public void IsShallowSharedManagedArtifactPath_classifies_collision_prone_paths(
        string path,
        bool expected)
    {
        var actual = ProcessRunAutomationDispatchService.IsShallowSharedManagedArtifactPath(path);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveRecoverableAutomationExecutionRunId_returns_latest_terminal_automation_run_for_in_progress_step()
    {
        var attemptStartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        var olderRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Completed, RunOutcome.Succeeded) with
        {
            CreatedAtUtc = attemptStartedAtUtc.AddMinutes(1),
            StartedAtUtc = attemptStartedAtUtc.AddMinutes(1),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var latestRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Failed, RunOutcome.Failed) with
        {
            CreatedAtUtc = attemptStartedAtUtc.AddMinutes(2),
            StartedAtUtc = attemptStartedAtUtc.AddMinutes(2),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };

        var recoverableRunId = ProcessRunAutomationDispatchService.ResolveRecoverableAutomationExecutionRunId(
            CreateStepRun(ProcessStepRunStatus.InProgress, attemptStartedAtUtc),
            [olderRun, latestRun]);

        Assert.Equal(latestRun.Id, recoverableRunId);
    }

    private static string InvokeBuildCanonicalProjectStructureGroundingSql(bool isPostgreSql)
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "BuildCanonicalProjectStructureGroundingSql",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCanonicalProjectStructureGroundingSql method was not found.");

        return (string)method.Invoke(null, [isPostgreSql])!;
    }

    [Fact]
    public void ResolveRecoverableAutomationExecutionRunId_ignores_terminal_runs_when_step_is_not_in_progress()
    {
        var recoverableRunId = ProcessRunAutomationDispatchService.ResolveRecoverableAutomationExecutionRunId(
            CreateStepRun(ProcessStepRunStatus.Ready, null),
            [
                CreateExecutionRun("process-automation-dispatch", ExecutionState.Completed, RunOutcome.Succeeded)
            ]);

        Assert.Null(recoverableRunId);
    }

    [Fact]
    public void ResolveReusableAutomationChatSessionId_returns_latest_terminal_chat_backed_automation_run()
    {
        var olderSessionId = Guid.NewGuid();
        var latestSessionId = Guid.NewGuid();
        var olderRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Completed, RunOutcome.Succeeded) with
        {
            ChatSessionId = olderSessionId,
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var latestRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Running, null) with
        {
            ChatSessionId = latestSessionId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var chatSessionId = ProcessRunAutomationDispatchService.ResolveReusableAutomationChatSessionId(
            [olderRun, latestRun]);

        Assert.Equal(olderSessionId, chatSessionId);
    }

    [Fact]
    public void ResolveReusableAutomationChatSessionId_ignores_manual_or_sessionless_runs()
    {
        var chatSessionId = ProcessRunAutomationDispatchService.ResolveReusableAutomationChatSessionId(
        [
            CreateExecutionRun("agent-run-debug", ExecutionState.Running, null) with
            {
                ChatSessionId = Guid.NewGuid(),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            CreateExecutionRun("process-automation-dispatch", ExecutionState.Running, null) with
            {
                ChatSessionId = null,
                UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(1)
            }
        ]);

        Assert.Null(chatSessionId);
    }

    [Fact]
    public void ResolveReusableAutomationChatSessionId_ignores_active_automation_runs_to_avoid_session_collisions()
    {
        var completedSessionId = Guid.NewGuid();
        var activeSessionId = Guid.NewGuid();
        var completedRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Completed, RunOutcome.Succeeded) with
        {
            ChatSessionId = completedSessionId,
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var activeRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Running, null) with
        {
            ChatSessionId = activeSessionId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var chatSessionId = ProcessRunAutomationDispatchService.ResolveReusableAutomationChatSessionId(
            [completedRun, activeRun]);

        Assert.Equal(completedSessionId, chatSessionId);
    }

    [Theory]
    [InlineData("This session already has an active execution run. Wait for it to finish before sending a new prompt.")]
    [InlineData("  This session has pending tool approvals. Approve or reject them before sending a new prompt.  ")]
    public void IsConcurrentAutomationSessionBusyException_recognizes_workspace_execution_session_collision_messages(string message)
    {
        var isSessionBusy = ProcessRunAutomationDispatchService.IsConcurrentAutomationSessionBusyException(
            new InvalidOperationException(message));

        Assert.True(isSessionBusy);
    }

    [Fact]
    public void IsConcurrentAutomationSessionBusyException_ignores_unrelated_failures()
    {
        Assert.False(ProcessRunAutomationDispatchService.IsConcurrentAutomationSessionBusyException(
            new InvalidOperationException("The provider profile could not be resolved.")));
        Assert.False(ProcessRunAutomationDispatchService.IsConcurrentAutomationSessionBusyException(
            new Exception("This session already has an active execution run. Wait for it to finish before sending a new prompt.")));
    }

    [Fact]
    public void ResolveRecoverableAutomationExecutionRunId_returns_cancelled_current_attempt_restart_runs()
    {
        var attemptStartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-30);
        var interruptedRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Failed, RunOutcome.Cancelled) with
        {
            CreatedAtUtc = attemptStartedAtUtc.AddMinutes(20),
            StartedAtUtc = attemptStartedAtUtc.AddMinutes(20),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
        var previousCompletedRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Completed, RunOutcome.Succeeded) with
        {
            CreatedAtUtc = attemptStartedAtUtc.AddMinutes(5),
            StartedAtUtc = attemptStartedAtUtc.AddMinutes(5),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        var recoverableRunId = ProcessRunAutomationDispatchService.ResolveRecoverableAutomationExecutionRunId(
            CreateStepRun(ProcessStepRunStatus.InProgress, attemptStartedAtUtc),
            [interruptedRun, previousCompletedRun]);

        Assert.Equal(interruptedRun.Id, recoverableRunId);
    }

    [Fact]
    public void ResolveRecoverableAutomationExecutionRunId_ignores_terminal_runs_from_previous_attempt_windows()
    {
        var attemptStartedAtUtc = DateTimeOffset.UtcNow;
        var previousAttemptRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Completed, RunOutcome.Succeeded) with
        {
            CreatedAtUtc = attemptStartedAtUtc.AddMinutes(-20),
            StartedAtUtc = attemptStartedAtUtc.AddMinutes(-20),
            UpdatedAtUtc = attemptStartedAtUtc.AddMinutes(-10),
            CompletedAtUtc = attemptStartedAtUtc.AddMinutes(-10)
        };
        var currentAttemptRun = CreateExecutionRun("process-automation-dispatch", ExecutionState.Completed, RunOutcome.Succeeded) with
        {
            CreatedAtUtc = attemptStartedAtUtc.AddMinutes(1),
            StartedAtUtc = attemptStartedAtUtc.AddMinutes(1),
            UpdatedAtUtc = attemptStartedAtUtc.AddMinutes(2),
            CompletedAtUtc = attemptStartedAtUtc.AddMinutes(2)
        };

        var recoverableRunId = ProcessRunAutomationDispatchService.ResolveRecoverableAutomationExecutionRunId(
            CreateStepRun(ProcessStepRunStatus.InProgress, attemptStartedAtUtc),
            [previousAttemptRun, currentAttemptRun]);

        Assert.Equal(currentAttemptRun.Id, recoverableRunId);
    }

    [Theory]
    [InlineData(ProcessStepRunStatus.Failed, ProcessStepRunStatus.Completed, true)]
    [InlineData(ProcessStepRunStatus.Completed, ProcessStepRunStatus.Failed, true)]
    [InlineData(ProcessStepRunStatus.Blocked, ProcessStepRunStatus.Completed, true)]
    [InlineData(ProcessStepRunStatus.Ready, ProcessStepRunStatus.Completed, true)]
    [InlineData(ProcessStepRunStatus.InProgress, ProcessStepRunStatus.Completed, false)]
    [InlineData(ProcessStepRunStatus.WaitingApproval, ProcessStepRunStatus.Completed, false)]
    public void ShouldSkipAutomationCompletionTransition_skips_only_after_step_leaves_active_execution_lane(
        ProcessStepRunStatus currentStatus,
        ProcessStepRunStatus requestedStatus,
        bool expected)
    {
        var shouldSkip = ProcessRunAutomationDispatchService.ShouldSkipAutomationCompletionTransition(currentStatus, requestedStatus);

        Assert.Equal(expected, shouldSkip);
    }

    [Theory]
    [InlineData(ProcessStepRunStatus.InProgress, null, "2026-04-19T02:00:00+00:00", "2026-04-19T02:01:00+00:00", "step-transition:Completed", false)]
    [InlineData(ProcessStepRunStatus.InProgress, null, "2026-04-19T02:00:00+00:00", "2026-04-19T02:01:00+00:00", "step-transition:InProgress", false)]
    [InlineData(ProcessStepRunStatus.InProgress, null, "2026-04-19T02:00:00+00:00", "2026-04-19T02:01:00+00:00", "runtime-recovery-scan", true)]
    [InlineData(ProcessStepRunStatus.InProgress, null, "2026-04-19T02:00:00+00:00", "2026-04-19T02:03:00+00:00", "runtime-recovery-scan", true)]
    [InlineData(ProcessStepRunStatus.InProgress, null, "2026-04-19T02:00:00+00:00", "2026-04-19T02:11:00+00:00", "runtime-recovery-scan", false)]
    [InlineData(ProcessStepRunStatus.Ready, null, null, "2026-04-19T02:01:00+00:00", "step-transition:Completed", false)]
    public void ShouldSkipFreshAutomationDispatch_skips_early_redispatches_for_fresh_inprogress_steps(
        ProcessStepRunStatus currentStatus,
        string? recoverableExecutionRunIdText,
        string? currentAttemptStartedAtUtcText,
        string nowText,
        string trigger,
        bool expected)
    {
        var recoverableExecutionRunId = string.IsNullOrWhiteSpace(recoverableExecutionRunIdText)
            ? (Guid?)null
            : Guid.Parse(recoverableExecutionRunIdText);
        var currentAttemptStartedAtUtc = string.IsNullOrWhiteSpace(currentAttemptStartedAtUtcText)
            ? (DateTimeOffset?)null
            : DateTimeOffset.Parse(currentAttemptStartedAtUtcText);
        var now = DateTimeOffset.Parse(nowText);

        var shouldSkip = ProcessRunAutomationDispatchService.ShouldSkipFreshAutomationDispatch(
            currentStatus,
            recoverableExecutionRunId,
            currentAttemptStartedAtUtc,
            now,
            trigger);

        Assert.Equal(expected, shouldSkip);
    }

    [Fact]
    public void ShouldSkipFreshAutomationDispatch_allows_recovery_of_existing_execution_run()
    {
        var shouldSkip = ProcessRunAutomationDispatchService.ShouldSkipFreshAutomationDispatch(
            ProcessStepRunStatus.InProgress,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow,
            "step-transition:Completed");

        Assert.False(shouldSkip);
    }

    [Fact]
    public void BuildWorkBrief_includes_step_notes_in_runtime_brief()
    {
        var buildWorkBrief = typeof(ProcessesService).GetMethod("BuildWorkBrief", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildWorkBrief method was not found.");

        var definition = new ProcessDefinition
        {
            Name = "Software delivery",
            ValueStatement = "Deliver the requested feature.",
            OwnerName = "Showcase owner"
        };
        var step = new ProcessStepDefinition
        {
            Title = "Implement feature",
            InputContractSummary = "Approved scope and architecture inputs.",
            OutputContractSummary = "A buildable implementation.",
            EvidenceContractSummary = "Build and change evidence.",
            Notes = "Call workspace_pwsh_run_script first, then call workspace_dotnet_build before writing summary evidence."
        };

        var workBrief = buildWorkBrief.Invoke(null, [definition, step, "Showcase Lead Engineer", null]) as string;

        Assert.NotNull(workBrief);
        Assert.Contains("Instructions:", workBrief, StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", workBrief, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_build", workBrief, StringComparison.Ordinal);
    }

    [Fact]
    public void MatchExpectedArtifactId_matches_brief_markdown_by_slug_when_kind_guess_differs()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Brief,
                "Scope boundary packet",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Must capture the clarified release boundary.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-workflow/evidence/process/feature-intake/scope-boundary-packet.md",
            "text/markdown",
            "workspace",
            "Durable scope packet",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_matches_text_artifact_by_content_when_path_is_project_specific()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Brief,
                "Scope boundary packet",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Must capture no-go constraints, user or operational impact, and acceptance boundary in typed form.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/scopes/organization/demo/scopes/garden-tool-sharpening-queue-scope-packet.md",
            "text/markdown",
            "workspace",
            "Workspace file written by the agent.",
            DateTimeOffset.UtcNow);
        const string artifactContent = """
            ## Scope boundary packet

            No-go constraints:
            - No external service integration.

            User or operational impact:
            - The app must support a local delivery validation workflow.

            Acceptance boundary:
            - Build, test, run, and browser proof must be captured before release readiness.
            """;

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(
            expectedArtifacts,
            artifact,
            artifactContent);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_does_not_bind_unrelated_artifact_when_only_expectation_is_present()
    {
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                Guid.NewGuid(),
                ProcessArtifactKind.Brief,
                "Scope boundary packet",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Must capture the clarified release boundary.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "tool-log",
            string.Empty,
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-workflow/logs/stdout.log",
            "text/plain",
            "workspace",
            "Command output",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Null(matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_keeps_explicit_path_requirement_even_when_content_matches()
    {
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                Guid.NewGuid(),
                ProcessArtifactKind.Brief,
                "Scope boundary packet",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Create this artifact at artifacts/process/feature-intake/scope-boundary-packet.md using workspace create/write file tools.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/scopes/organization/demo/scopes/garden-tool-sharpening-queue-scope-packet.md",
            "text/markdown",
            "workspace",
            "Workspace file written by the agent.",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(
            expectedArtifacts,
            artifact,
            "## Scope boundary packet\n\nAcceptance boundary and no-go constraints are captured.");

        Assert.Null(matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_ignores_playwright_scratch_artifact_even_when_name_matches()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Evidence,
                "Workflow proof",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Imported browser screenshot is required.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "showcases/blazor-ssr-workflow/.playwright-mcp/qa-validation/workflow-proof.png",
            "image/png",
            "workspace",
            "workflow-proof.png",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Null(matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_matches_imported_ui_screenshot_by_slug()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Evidence,
                "Workflow proof",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Imported browser screenshot is required.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-workflow/evidence/ui/qa-validation/workflow-proof.png",
            "image/png",
            "workspace",
            "workflow-proof.png",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_requires_exact_path_when_validation_summary_declares_one()
    {
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                Guid.NewGuid(),
                ProcessArtifactKind.Deliverable,
                "Implementation change set",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Create this artifact at artifacts/showcases/blazor-ssr-workflow/evidence/process/implementation/implementation-change-set.md using workspace create/write file tools.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/showcases/blazor-ssr-workflow/evidence/process/implementation/implementation-change-set/implementation-change-set.md",
            "text/markdown",
            "workspace",
            "Implementation change set",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Null(matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_matches_exact_path_even_when_title_differs()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Deliverable,
                "Workflow app project",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Create this artifact at showcases/blazor-ssr-workflow/app/SimpleWorkflowApp/SimpleWorkflowApp.csproj using workspace create/write file tools.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp/SimpleWorkflowApp.csproj",
            "text/xml",
            "workspace",
            "SimpleWorkflowApp.csproj",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_matches_scoped_managed_path_when_validation_summary_uses_unscoped_root()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Evidence,
                "Workflow proof",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "The durable screenshot must exist at artifacts/showcases/blazor-ssr-workflow/evidence/ui/qa-validation/workflow-proof.png.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-workflow/evidence/ui/qa-validation/workflow-proof.png",
            "image/png",
            "workspace",
            "workflow-proof.png",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
    }

    [Fact]
    public void WorkspaceWrittenFileMatchesExpectedArtifact_matches_required_artifact_write()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifact = new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
            expectedArtifactId,
            ProcessArtifactKind.Brief,
            "Project structure context brief",
            true,
            ProcessArtifactTrustRequirement.ReviewRequired,
            ProcessSensitivityLevel.Internal,
            "Create this artifact at artifacts/process-runs/11111111-1111-1111-1111-111111111111/02-project-structure-context-brief.md using workspace create/write file tools.",
            string.Empty);
        var expectedArtifacts = new[] { expectedArtifact };

        var matches = ProcessRunAutomationDispatchService.WorkspaceWrittenFileMatchesExpectedArtifact(
            expectedArtifacts,
            expectedArtifact,
            "artifacts/process-runs/11111111-1111-1111-1111-111111111111/02-project-structure-context-brief.md",
            """
            Project structure context brief

            Current grounded product root:
            external-target/C/work/products/RequestedApplication
            """);

        Assert.True(matches);
    }

    [Fact]
    public void WorkspaceWrittenFileMatchesExpectedArtifact_matches_scoped_required_artifact_write()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifact = new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
            expectedArtifactId,
            ProcessArtifactKind.Brief,
            "Project structure context brief",
            true,
            ProcessArtifactTrustRequirement.ReviewRequired,
            ProcessSensitivityLevel.Internal,
            "Create this artifact at artifacts/process-runs/11111111-1111-1111-1111-111111111111/02-project-structure-context-brief.md using workspace create/write file tools.",
            string.Empty);
        var expectedArtifacts = new[] { expectedArtifact };

        var matches = ProcessRunAutomationDispatchService.WorkspaceWrittenFileMatchesExpectedArtifact(
            expectedArtifacts,
            expectedArtifact,
            "artifacts/scopes/organization/demo/process-runs/11111111-1111-1111-1111-111111111111/02-project-structure-context-brief.md",
            "Project structure context brief with acceptance boundary and release evidence.");

        Assert.True(matches);
    }

    [Fact]
    public void ExistingManagedArtifactFileMatches_expected_fallback_run_artifact()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolvePaths = serviceType.GetMethod(
            "ResolveExpectedManagedArtifactRelativePaths",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveExpectedManagedArtifactRelativePaths method was not found.");
        var existingFileMatches = serviceType.GetMethod(
            "ExistingManagedArtifactFileMatches",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ExistingManagedArtifactFileMatches method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "Clarify scope and release boundary",
            "Capture scope, impact, acceptance boundary, assumptions, exclusions, and validation hooks.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Scope boundary packet", true, "Must capture no-go constraints, user or operational impact, and acceptance boundary in typed form."));
        var expectedArtifacts = candidate.GetType().GetProperty("ExpectedArtifacts")?.GetValue(candidate)
            ?? throw new InvalidOperationException("DispatchCandidate.ExpectedArtifacts was not available.");
        var expectedArtifact = ((IEnumerable)expectedArtifacts).Cast<object>().Single();
        var workspaceScope = WorkspaceScopeDescriptor.Organization("demo");
        var paths = resolvePaths.Invoke(null, [candidate, workspaceScope, expectedArtifact]) as IReadOnlyList<string>;

        Assert.NotNull(paths);
        var relativePath = Assert.Single(paths);
        Assert.StartsWith("artifacts/scopes/organization/demo/process-runs/", relativePath, StringComparison.Ordinal);
        Assert.EndsWith("/01-scope-boundary-packet.md", relativePath, StringComparison.Ordinal);

        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"candoitall-artifact-match-{Guid.NewGuid():N}");
        var fullPath = Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        try
        {
            File.WriteAllText(
                fullPath,
                """
                # Scope boundary packet

                No-go constraints, user impact, operational impact, acceptance boundary, assumptions, exclusions, and validation hooks are captured for this run.
                """);

            var matches = existingFileMatches.Invoke(
                null,
                [expectedArtifacts, expectedArtifact, workspaceRoot, relativePath]) as bool?;

            Assert.True(matches);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildExecutionPrompt_warns_against_unrelated_side_actions()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPrompt = serviceType.GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");

        var candidate = CreateDispatchCandidate(
            "Review the delivered change set and confirm whether it is ready for QA.",
            ProcessStepKind.Review);

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains(
            "Do not execute helper scripts, app launches, browser proof, release rollout, or other side actions unless the current step contract or required artifacts explicitly call for them.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Treat run-level paths and planned solution targets as context unless the current step contract explicitly tells you to create, inspect, build, test, launch, or review them.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "missing solution or project files are expected pre-bootstrap state, not a blocker",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not claim that planned scaffold targets are missing deliverables when the current step contract explicitly tells you to create, bootstrap, or scaffold them in this step.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not ask for confirmation, permission, or a follow-up reply before writing required artifacts.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "If a required build, test, launch, browser check, or artifact import fails, inspect the real diagnostics, fix the underlying problem, and rerun the same required validation before you conclude.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProcessStepOutcomeResult",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Use status Blocked when unresolved defects, missing proof, rejected approval, or required remediation mean the next step must not proceed and no available branch outcome represents the needed repair",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not stop after inspection, reconnaissance, bootstrap confirmation, or a next-steps summary if required tools, concrete deliverables, or required artifacts are still missing.",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_tells_scope_steps_to_proceed_with_bounded_assumptions()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate(
            "Clarify scope and release boundary for the requested generated deliverable.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Scope boundary packet", true, "Must capture no-go constraints and acceptance boundary in typed form."));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Assumption-forward execution rule", prompt, StringComparison.Ordinal);
        Assert.Contains(
            "proceed with bounded assumptions instead of stopping for optional preferences",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not use status Blocked for ambiguity that can be handled by explicit assumptions",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_explicitly_treats_missing_scaffold_targets_as_bootstrap_work()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate(
            "Implement the requested application.\nInstructions: Use workspace_pwsh_run_script to run Bootstrap-RequestedApplication.ps1 before substantial edits.");

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains(
            "greenfield implementation or gives you a bootstrap or init script",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Run the bootstrap or init step first, then inspect the scaffolded files and continue.",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolveDeclaredStepOutcome_rejects_legacy_markdown_comment()
    {
        var tryResolveOutcome = ResolveTryResolveDeclaredStepOutcomeMethod();
        object?[] arguments =
        [
            "Review complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Code review passed.\"} -->",
            null
        ];

        var parsed = Assert.IsType<bool>(tryResolveOutcome.Invoke(null, arguments));

        Assert.False(parsed);
    }

    [Fact]
    public void TryResolveDeclaredStepOutcome_accepts_valid_structured_json()
    {
        var tryResolveOutcome = ResolveTryResolveDeclaredStepOutcomeMethod();
        object?[] arguments =
        [
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Code review passed.",
                evidenceRefs: ["execution://tool/workspace_read_file"]),
            null
        ];

        var parsed = Assert.IsType<bool>(tryResolveOutcome.Invoke(null, arguments));

        Assert.True(parsed);
    }

    [Fact]
    public void BuildExecutionPromptCore_includes_prefetched_project_structure_grounding_when_available()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var projectId = Guid.NewGuid();
        var candidate = CreateProjectStructureDispatchCandidate(
            "Clarify the scope and write the release boundary brief.",
            new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-basic-app",
                ParentNodeTitle = "Create basic app"
            },
            ProcessStepKind.Start);

        var prompt = buildExecutionPromptCore.Invoke(
            null,
            [candidate, null, "Ancestor path to the target work node:\n- Workflow request (root-request)", null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Live project structure grounding:", prompt, StringComparison.Ordinal);
        Assert.Contains("Workflow request (root-request)", prompt, StringComparison.Ordinal);
        Assert.Contains(
            "The dispatcher already fetched a live project-structure snapshot for this run",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_applies_grounded_external_target_rules_without_serialized_context()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var candidate = CreateDispatchCandidate("Implement the requested browser-visible delivery and prove it builds.");

        var prompt = buildExecutionPromptCore.Invoke(
            null,
            [
                candidate,
                null,
                """
                Dispatcher fetched the live project structure for `Validation project` and focused this prompt on the selected work branch.
                Grounded external target paths from the selected project structure:
                - `C:\programovani\dotnet\CommunityGardenMap` mapped to `external-target/C/programovani/dotnet/CommunityGardenMap` from Delivery target (node-target)
                """,
                null
            ]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Selected process node: not serialized on launch", prompt, StringComparison.Ordinal);
        Assert.Contains("The dispatcher already fetched a live project-structure snapshot for this run", prompt, StringComparison.Ordinal);
        Assert.Contains("create and edit the deliverable under `external-target/C/programovani/dotnet/CommunityGardenMap`", prompt, StringComparison.Ordinal);
        Assert.Contains("parentDirectory` set to `external-target/C/programovani/dotnet`", prompt, StringComparison.Ordinal);
        Assert.Contains("name` set to `CommunityGardenMap`", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not cite a file, path, tool result, example, or source artifact as evidence unless it was grounded by the current-run project structure", prompt, StringComparison.Ordinal);
        Assert.Contains("Never write `contextual example files`, `source files reviewed`, or similar evidence claims unless the exact files were inspected by current-run tool calls", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_lists_current_run_artifact_root_for_pathless_required_artifacts()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService)
            .GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate(
            "Clarify scope and produce durable evidence.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Scope boundary packet", true, "Must capture the delivery boundary."));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Current-run managed artifact root:", prompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/process-runs/", prompt, StringComparison.Ordinal);
        Assert.Contains("Managed path: artifacts/process-runs/", prompt, StringComparison.Ordinal);
        Assert.Contains("01-scope-boundary-packet.md", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("artifacts/scopes/<scope>/<id>/scope-boundary-packet.md", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryResolveProjectLevelProjectStructureContext_prefers_external_local_path_node()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var groundingNodeType = serviceType.GetNestedType("ProjectStructureGroundingNodeData", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProjectStructureGroundingNodeData type was not found.");
        var nodes = Array.CreateInstance(groundingNodeType, 2);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "node-notes",
                string.Empty,
                "Note",
                string.Empty,
                "General notes",
                string.Empty,
                string.Empty,
                "Use standard delivery quality checks.",
                "{}"),
            0);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "node-target",
                string.Empty,
                "Folder",
                "folder",
                "Delivery target",
                @"C:\programovani\dotnet\CommunityGardenMap",
                string.Empty,
                "External output location for the generated deliverable.",
                """
                {"repository":{"repositoryMode":"localFolder","localPath":"C:\\programovani\\dotnet\\CommunityGardenMap","relativePath":"."}}
                """),
            1);
        var method = serviceType.GetMethod("TryResolveProjectLevelProjectStructureContext", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveProjectLevelProjectStructureContext method was not found.");
        var projectId = Guid.NewGuid();

        var context = method.Invoke(null, [projectId, "Delivery process", nodes]) as ProcessProjectStructureContext;

        Assert.NotNull(context);
        Assert.Equal(projectId, context.ProjectId);
        Assert.Equal("node-target", context.ResolveTargetNodeId());
        Assert.Equal("Delivery target", context.ResolveTargetNodeTitle());
    }

    [Fact]
    public void BuildProjectStructureGroundingSummary_includes_external_target_alias_from_focus_node()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var groundingNodeType = serviceType.GetNestedType("ProjectStructureGroundingNodeData", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProjectStructureGroundingNodeData type was not found.");
        var nodes = Array.CreateInstance(groundingNodeType, 1);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "node-target",
                string.Empty,
                "Folder",
                "folder",
                "Delivery target",
                @"C:\programovani\dotnet\CommunityGardenMap",
                string.Empty,
                "External output location for the generated deliverable.",
                "{}"),
            0);
        var method = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(candidate =>
            {
                if (candidate.Name != "BuildProjectStructureGroundingSummary")
                {
                    return false;
                }

                var parameters = candidate.GetParameters();
                return parameters.Length == 3 &&
                       parameters[0].ParameterType == typeof(string) &&
                       parameters[2].ParameterType == typeof(ProcessProjectStructureContext);
            });

        var summary = method.Invoke(
            null,
            [
                "Validation project",
                nodes,
                new ProcessProjectStructureContext
                {
                    ProjectId = Guid.NewGuid(),
                    NodeId = "node-target",
                    NodeTitle = "Delivery target"
                }
            ]) as string;

        Assert.NotNull(summary);
        Assert.Contains("Grounded external target paths from the selected project structure:", summary, StringComparison.Ordinal);
        Assert.Contains(@"C:\programovani\dotnet\CommunityGardenMap", summary, StringComparison.Ordinal);
        Assert.Contains("external-target/C/programovani/dotnet/CommunityGardenMap", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_includes_prefetched_artifact_inspection_grounding_when_available()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var candidate = CreateDispatchCandidateCore(
            "Review architecture and canonical-model impact.",
            ProcessStepKind.Review,
            [],
            false,
            [],
            [
                (
                    "Clarify scope and release boundary",
                    "Scope boundary packet",
                    [
                        (
                            "Scope boundary packet",
                            "Brief",
                            "artifacts/scopes/organization/demo/process-runs/0001/01-scope-boundary-packet.md",
                            "Captured the workflow scope and boundary.",
                            "Projected from the prior governed step.")
                    ])
            ]);

        var prompt = buildExecutionPromptCore.Invoke(
            null,
            [
                candidate,
                null,
                null,
                "Dispatcher pre-inspected recorded upstream durable artifacts before this step started:\n- `artifacts/scopes/organization/demo/process-runs/0001/01-scope-boundary-packet.md`"
            ]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Prefetched governed artifact grounding:", prompt, StringComparison.Ordinal);
        Assert.Contains("scope-boundary-packet.md", prompt, StringComparison.Ordinal);
        Assert.Contains(
            "The dispatcher already inspected upstream governed artifact files",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProjectStructureGroundingSummary_includes_project_level_planning_context_without_sibling_work_items()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildProjectStructureGroundingSummary = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method =>
                string.Equals(method.Name, "BuildProjectStructureGroundingSummary", StringComparison.Ordinal) &&
                method.GetParameters().Length == 2 &&
                method.GetParameters()[1].ParameterType == typeof(ProcessProjectStructureContext))
            ?? throw new InvalidOperationException("BuildProjectStructureGroundingSummary method was not found.");
        var projectId = Guid.NewGuid();
        var context = new ProcessProjectStructureContext
        {
            ProjectId = projectId,
            NodeId = "process-definition:software-delivery",
            NodeTitle = "Multi-team software delivery and release governance",
            ParentNodeId = "task:create-main-application",
            ParentNodeTitle = "Create main application"
        };
        var surface = new
        {
            ProjectName = "Workflow",
            Nodes = new object[]
            {
                new
                {
                    Id = $"project:{projectId:D}",
                    ParentId = string.Empty,
                    ObjectType = "ProjectRoot",
                    ObjectSubtype = string.Empty,
                    Title = "Workflow",
                    Subtitle = string.Empty,
                    Status = "Active",
                    Notes = string.Empty,
                    MetadataJson = "{}"
                },
                new
                {
                    Id = "task:create-main-application",
                    ParentId = $"project:{projectId:D}",
                    ObjectType = "WorkItem",
                    ObjectSubtype = "task",
                    Title = "Create main application",
                    Subtitle = string.Empty,
                    Status = "Draft",
                    Notes = string.Empty,
                    MetadataJson = "{}"
                },
                new
                {
                    Id = "process-definition:software-delivery",
                    ParentId = "task:create-main-application",
                    ObjectType = "ProcessDefinition",
                    ObjectSubtype = string.Empty,
                    Title = "Multi-team software delivery and release governance",
                    Subtitle = "Published · 7 role(s) · 9 step(s)",
                    Status = "Published",
                    Notes = "Governed delivery.",
                    MetadataJson = "{}"
                },
                new
                {
                    Id = "block:architecture",
                    ParentId = $"project:{projectId:D}",
                    ObjectType = "ProjectBlock",
                    ObjectSubtype = "architecture",
                    Title = "Main architecture",
                    Subtitle = string.Empty,
                    Status = "Draft",
                    Notes = string.Empty,
                    MetadataJson = "{}"
                },
                new
                {
                    Id = "block:features",
                    ParentId = $"project:{projectId:D}",
                    ObjectType = "ProjectBlock",
                    ObjectSubtype = "feature",
                    Title = "Main features",
                    Subtitle = string.Empty,
                    Status = "Draft",
                    Notes = string.Empty,
                    MetadataJson = "{}"
                },
                new
                {
                    Id = "note:output-path",
                    ParentId = "block:architecture",
                    ObjectType = "ProjectBlock",
                    ObjectSubtype = "note",
                    Title = @"output must be placed in C:\programovani\csharp\workflow",
                    Subtitle = string.Empty,
                    Status = "Draft",
                    Notes = string.Empty,
                    MetadataJson = "{}"
                },
                new
                {
                    Id = "feature:blazor",
                    ParentId = "block:features",
                    ObjectType = "ProjectBlock",
                    ObjectSubtype = "feature",
                    Title = "Blazor SSR",
                    Subtitle = string.Empty,
                    Status = "Draft",
                    Notes = string.Empty,
                    MetadataJson = "{}"
                },
                new
                {
                    Id = "feature:buttons",
                    ParentId = "block:features",
                    ObjectType = "ProjectBlock",
                    ObjectSubtype = "feature",
                    Title = "status buttons for queue, pause, resume, complete",
                    Subtitle = string.Empty,
                    Status = "Draft",
                    Notes = string.Empty,
                    MetadataJson = "{}"
                },
                new
                {
                    Id = "feature:history",
                    ParentId = "block:features",
                    ObjectType = "ProjectBlock",
                    ObjectSubtype = "feature",
                    Title = "activity history list",
                    Subtitle = string.Empty,
                    Status = "Draft",
                    Notes = string.Empty,
                    MetadataJson = "{}"
                },
                new
                {
                    Id = "process-run:previous",
                    ParentId = "task:create-main-application",
                    ObjectType = "ProcessRun",
                    ObjectSubtype = string.Empty,
                    Title = "Previous run",
                    Subtitle = "Completed",
                    Status = "Completed",
                    Notes = "Noise only.",
                    MetadataJson = "{}"
                },
                new
                {
                    Id = "task:unrelated-app",
                    ParentId = $"project:{projectId:D}",
                    ObjectType = "WorkItem",
                    ObjectSubtype = "task",
                    Title = "Build unrelated sample app",
                    Subtitle = string.Empty,
                    Status = "Draft",
                    Notes = @"Target directory: C:\programovani\dotnet\UnrelatedSample.",
                    MetadataJson = "{}"
                }
            }
        };

        var summary = buildProjectStructureGroundingSummary.Invoke(null, [surface, context]) as string;

        Assert.NotNull(summary);
        Assert.Contains("Project-level planning context under the target parent:", summary, StringComparison.Ordinal);
        Assert.Contains("Requirements from project-level planning context:", summary, StringComparison.Ordinal);
        Assert.Contains(@"output must be placed in C:\programovani\csharp\workflow", summary, StringComparison.Ordinal);
        Assert.Contains("Blazor SSR", summary, StringComparison.Ordinal);
        Assert.Contains("status buttons for queue, pause, resume, complete", summary, StringComparison.Ordinal);
        Assert.Contains("activity history list", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Previous run", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("UnrelatedSample", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_rejects_browser_proof_when_it_cannot_be_captured()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the workflow app.",
            ProcessStepKind.Review);

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("This step requires runnable browser proof or screenshots", prompt, StringComparison.Ordinal);
        Assert.Contains("inspect the concrete host, launch instructions, prior validation receipts, or reviewed artifacts", prompt, StringComparison.Ordinal);
        Assert.Contains("start it using the launch path and toolchain appropriate for the assigned agent", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not assume a fixed URL", prompt, StringComparison.Ordinal);
        Assert.Contains("Use browser tools after launch", prompt, StringComparison.Ordinal);
        Assert.Contains("After browser inspection, review the captured snapshot or screenshot content", prompt, StringComparison.Ordinal);
        Assert.Contains("perform a representative user sequence", prompt, StringComparison.Ordinal);
        Assert.Contains("do not approve the proof", prompt, StringComparison.Ordinal);
        Assert.Contains("no available branch outcome represents the needed repair", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not reframe missing browser proof", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_dotnet_run", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Blazor render-mode", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_keeps_implementation_bootstrap_guidance_domain_neutral()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the workflow and prove the build passes.",
            new ProcessProjectStructureContext
            {
                ProjectId = Guid.NewGuid(),
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-main-application",
                ParentNodeTitle = "Create main application"
            });

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("create the real deliverable now", prompt, StringComparison.Ordinal);
        Assert.Contains("Follow the current step contract, assigned agent instructions, available skills", prompt, StringComparison.Ordinal);
        Assert.Contains("Inspect existing files before creating or replacing scaffolds", prompt, StringComparison.Ordinal);
        Assert.Contains("Repair an existing deliverable in place", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_dotnet_new", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("net10.0", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_requires_explicit_db_free_migration_rollout_checklist()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Migration and rollout preparation checklist", prompt, StringComparison.Ordinal);
        Assert.Contains("No data migration required", prompt, StringComparison.Ordinal);
        Assert.Contains("no schema migration, seed update, backfill, or data rollback is required", prompt, StringComparison.Ordinal);
        Assert.Contains("operational preconditions", prompt, StringComparison.Ordinal);
        Assert.Contains("rollback steps", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_blocks_when_required_upstream_artifact_input_is_missing()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidateWithArtifactInputs(
            "Implement the workflow as a Blazor app and prove the build passes.",
            (
                "Write workflow architecture",
                "Workflow architecture artifact",
                []));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Upstream artifact gate:", prompt, StringComparison.Ordinal);
        Assert.Contains("Write workflow architecture", prompt, StringComparison.Ordinal);
        Assert.Contains("Workflow architecture artifact", prompt, StringComparison.Ordinal);
        Assert.Contains("Return `Blocked`", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not fabricate an upstream artifact", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_fails_when_required_step_tools_were_not_executed()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the requested application.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Implementation and required validation completed."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_write_file",
                    "MutatingWorkspace",
                    "NotRequired",
                    "Workspace-root-only file service.",
                    "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp/Program.cs",
                    ".",
                    "Succeeded: Created file.",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement feature"]) as string;

        Assert.Equal(ProcessStepRunStatus.Failed, status);
        Assert.NotNull(reason);
        Assert.Contains("workspace_pwsh_run_script", reason, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_build", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_completion_when_required_step_tools_succeed()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the requested application.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Required tools succeeded across recovery attempts."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_stat_path",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_pwsh_run_script",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Apply-WorkflowShowcaseApp.ps1",
                    "showcases/blazor-ssr-workflow",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "SimpleWorkflowApp.csproj",
                    "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement feature"]) as string;

        Assert.True(status == ProcessStepRunStatus.Completed, reason);
    }

    [Fact]
    public void ResolveCompletionStatusWithCarryForward_allows_completion_when_required_step_tools_succeed_across_attempts()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveCompletionStatusWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 3)
            ?? throw new InvalidOperationException("ResolveCompletionStatusWithCarryForward method was not found.");
        var buildCompletionReason = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "BuildCompletionReasonWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 4)
            ?? throw new InvalidOperationException("BuildCompletionReasonWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the requested application.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation retry",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Required implementation tools succeeded."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Units.slnx",
                    "deliveries/workflow-suite/src",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var priorSuccessfulTools = new[] { "workspace_pwsh_run_script" };
        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail, priorSuccessfulTools]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement feature", priorSuccessfulTools]) as string;

        Assert.NotNull(reason);
        Assert.True(status == ProcessStepRunStatus.Completed, reason);
        Assert.Contains("completed step", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspace_pwsh_run_script", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatusWithCarryForward_allows_completion_for_governed_review_when_structured_outcome_and_required_artifact_sections_are_present()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveCompletionStatusWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 4)
            ?? throw new InvalidOperationException("ResolveCompletionStatusWithCarryForward method was not found.");
        var buildCompletionReason = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "BuildCompletionReasonWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 5)
            ?? throw new InvalidOperationException("BuildCompletionReasonWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidateWithStepTitle(
            "Review architecture and canonical-model impact",
            "Review the grounded project structure and produce the architecture decision record.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Decision, "Architecture decision record", true, "Must capture selected option, rejected options, source-of-truth choice, and migration ownership."),
            (ProcessArtifactKind.Brief, "Project structure context brief", true, "Must capture the originating project-structure node, resolved working directory, touched modules or routes, dependency boundaries, and downstream artifact expectations."));
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Architecture decision record and project structure context brief completed.",
            summaryMarkdown: """
## Architecture decision record
Selected option: build the workflow as a single Blazor app under the grounded project output.
Rejected options: a console-only tool and a split service/UI package were rejected because they add unnecessary seams.
Source-of-truth choice: workflow state remains in the UI host with no extra persistence layer.
Migration ownership: the programming workspace analyst owns the bootstrap and follow-up implementation.

## Project structure context brief
Originating project-structure node: Create main application.
Resolved working directory: external-target/C/programovani/csharp/workflow.
Touched modules or routes: workflow shell, keypad interactions, result display, and history surface.
Dependency boundaries: keep the workflow self-contained and avoid billing/process module coupling.
Downstream artifact expectations: implementation change set, migration checklist, peer review note, and browser-proof evidence.
""");
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Architecture review",
                "process-step",
                "step-architecture",
                "corr-architecture",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "Remote Ollama",
                "gptoss32k:latest",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists."))),
                []),
            null,
            [],
            []);

        var priorSuccessfulTools = new[] { "project_structure_read", "workspace_stat_path", "workspace_read_file" };
        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail, priorSuccessfulTools, responseText]);
        var reason = buildCompletionReason.Invoke(
            null,
            [candidate, detail, "Review architecture and canonical-model impact", priorSuccessfulTools, responseText]) as string;

        Assert.True(status == ProcessStepRunStatus.Completed, reason);
        Assert.NotNull(reason);
        Assert.Contains("completed step", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatusWithCarryForward_accepts_workspace_write_receipts_for_required_managed_artifacts()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveCompletionStatusWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 4)
            ?? throw new InvalidOperationException("ResolveCompletionStatusWithCarryForward method was not found.");
        var buildCompletionReason = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "BuildCompletionReasonWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 5)
            ?? throw new InvalidOperationException("BuildCompletionReasonWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidateWithStepTitle(
            "Review architecture and canonical-model impact",
            "Review the grounded project structure and produce the architecture decision record.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Decision, "Architecture decision record", true, "Must capture selected option, rejected options, source-of-truth choice, and migration ownership."),
            (ProcessArtifactKind.Brief, "Project structure context brief", true, "Must capture the originating project-structure node, resolved working directory, touched modules or routes, dependency boundaries, and downstream artifact expectations."));
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Architecture review completed and required files were written through workspace file tools.");
        var requiredBriefPath = "artifacts/scopes/organization/demo/process-runs/11111111-1111-1111-1111-111111111111/02-project-structure-context-brief.md";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Architecture review",
                "process-step",
                "step-architecture",
                "corr-architecture",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "Remote Ollama",
                "gptoss32k:latest",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                "{}",
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    requiredBriefPath,
                    ".",
                    $"Succeeded: Overwrote '{requiredBriefPath}' with 2400 characters.",
                    now)
            ]
        };

        var priorSuccessfulTools = new[] { "project_structure_read", "workspace_stat_path", "workspace_read_file" };
        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail, priorSuccessfulTools, responseText]);
        var reason = buildCompletionReason.Invoke(
            null,
            [candidate, detail, "Review architecture and canonical-model impact", priorSuccessfulTools, responseText]) as string;

        Assert.True(status == ProcessStepRunStatus.Completed, reason);
        Assert.NotNull(reason);
        Assert.DoesNotContain("required artifacts still could not be recorded", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_completed_run_that_only_missed_required_tools()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate("Implement the requested application and prove the build passes.");

        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "I bootstrapped the workspace and listed next steps.",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Units.slnx",
                    "deliveries/workflow-suite/src",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, detail.Run.ResultSummary, new[] { "workspace_write_file", "workspace_dotnet_build" }, 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_completed_run_with_unresolved_critical_tool_failure()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate("Implement the requested application and prove the build passes.");

        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Implementation complete according to the assistant summary.",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "NotRequired",
                    "PolicyOnlyLocal",
                    "build deliveries/workflow-suite/WorkflowSuite.slnx -c Debug",
                    ".",
                    "Failed (exit 1)",
                    now,
                now)
            ]
        };

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, detail.Run.ResultSummary, Array.Empty<string>(), 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_governed_review_that_only_missed_structured_outcome()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate(
            "Review the implementation evidence and architecture decision record.",
            ProcessStepKind.Review);

        var now = DateTimeOffset.UtcNow;
        const string responseText = "The architecture review is complete, but the assistant forgot the structured outcome.";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Architecture review run",
                "process-step",
                "step-2",
                "corr-2",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_stat_path",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "artifacts/architecture",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_read_file",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "artifacts/architecture/ArchitectureDecisionRecord.md",
                    ".",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, Array.Empty<string>(), 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_completed_run_with_stale_external_target_evidence()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate(
            "Review architecture and canonical-model impact for the generated application.",
            ProcessStepKind.Review);

        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Architecture review completed.",
            summaryMarkdown:
                """
                Architecture review evidence:
                - Current product root: external-target/C/programovani/dotnet/ReadingTimeBudgeter
                - Stale sibling reference: external-target/C/programovani/dotnet/UnrelatedSample/Program.cs
                """,
            evidenceRefs: ["execution://tool/workspace_read_file"]);
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Architecture review run",
                "process-step",
                "step-2",
                "corr-2",
                "run-start",
                "process-automation-dispatch",
                "system",
                BuildAllowedExternalTargetMetadata("external-target/C/programovani/dotnet/ReadingTimeBudgeter"),
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateTextContent(responseText)])),
                []),
            null,
            [],
            []);

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, Array.Empty<string>(), 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_false_for_valid_blocked_governed_browser_outcome()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var buildCompletionReasonCore = serviceType.GetMethod("BuildCompletionReasonCore", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReasonCore method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the workflow app.",
            ProcessStepKind.Review);
        var missingBrowserProofTools = new[]
        {
            "browser_console_messages",
            "browser_snapshot",
            "browser_take_screenshot"
        };

        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Application is not running.",
            summaryMarkdown: "QA validation and browser proof cannot proceed because the application is not running and no screenshots can be captured.");
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA run",
                "process-step",
                "step-4",
                "corr-4",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            []);

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [
                candidate,
                detail,
                responseText,
                missingBrowserProofTools,
                1,
                3
            ]);
        var reason = buildCompletionReasonCore.Invoke(
            null,
            [candidate, detail, "Run QA validation and browser proof", missingBrowserProofTools, responseText]) as string;

        Assert.IsType<bool>(shouldRetryResult);
        Assert.False((bool)shouldRetryResult);
        Assert.NotNull(reason);
        Assert.Contains("blocked step", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("did not execute the required step tools", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvePreferredExecutionResponseText_prefers_recovered_chat_message_when_it_restores_structured_outcome()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolvePreferredResponseText = serviceType.GetMethod("ResolvePreferredExecutionResponseText", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolvePreferredExecutionResponseText method was not found.");
        var candidate = CreateDispatchCandidate(
            "Review the implementation evidence and architecture decision record.",
            ProcessStepKind.Review);

        var now = DateTimeOffset.UtcNow;
        var recoveredAssistantMessage = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Architecture decision recorded.",
            summaryMarkdown: "Review complete.");
        var chatSession = new ChatSessionRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Architecture review",
            now,
            now,
            [
                new ChatMessageRecord(
                    Guid.NewGuid(),
                    ChatMessageRole.Assistant,
                    recoveredAssistantMessage,
                    now,
                    recoveredAssistantMessage.Length)
            ]);
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                chatSession.Id,
                "Architecture review run",
                "process-step",
                "step-2",
                "corr-2",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Review complete, but the fresh provider summary omitted the structured outcome.",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            chatSession,
            [],
            []);

        var resolvedResponseText = resolvePreferredResponseText.Invoke(
            null,
            [candidate, "Review complete, but the fresh provider summary omitted the structured outcome.", detail]) as string;

        Assert.NotNull(resolvedResponseText);
        Assert.Equal(recoveredAssistantMessage, resolvedResponseText);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_false_after_final_attempt()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate("Implement the requested application and prove the build passes.");

        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "I bootstrapped the workspace and listed next steps.",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            []);

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, detail.Run.ResultSummary, new[] { "workspace_write_file", "workspace_dotnet_build" }, 3, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.False((bool)shouldRetryResult);
    }

    [Fact]
    public void ResolveRequiredToolNames_ignores_negated_tool_references_and_keeps_affirmative_requirements()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveRequiredToolNames = serviceType.GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the requested application.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude. Do not use workspace_append_file for canonical deliverables.");

        var requiredToolNames = resolveRequiredToolNames.Invoke(null, [candidate]) as IReadOnlyList<string>;

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_pwsh_run_script", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_dotnet_build", requiredToolNames, StringComparer.Ordinal);
        Assert.DoesNotContain("workspace_append_file", requiredToolNames, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveRequiredToolNames_keeps_browser_evidence_tools_and_ignores_browser_interaction_verbs()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveRequiredToolNames = serviceType.GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");

        var candidate = CreateDispatchCandidate(
            "Validate the UI.\nInstructions: Use browser_resize, browser_navigate, browser_fill_form, browser_select_option, browser_take_screenshot, browser_snapshot, and browser_console_messages before you conclude.");

        var requiredToolNames = resolveRequiredToolNames.Invoke(null, [candidate]) as IReadOnlyList<string>;

        Assert.NotNull(requiredToolNames);
        Assert.DoesNotContain("browser_resize", requiredToolNames, StringComparer.Ordinal);
        Assert.DoesNotContain("browser_navigate", requiredToolNames, StringComparer.Ordinal);
        Assert.DoesNotContain("browser_fill_form", requiredToolNames, StringComparer.Ordinal);
        Assert.DoesNotContain("browser_select_option", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("browser_take_screenshot", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("browser_snapshot", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("browser_console_messages", requiredToolNames, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveRequiredToolNames_adds_implicit_browser_evidence_tools_for_browser_proof_step()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveRequiredToolNames = serviceType.GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");

        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the workflow app.",
            ProcessStepKind.Review);

        var requiredToolNames = resolveRequiredToolNames.Invoke(null, [candidate]) as IReadOnlyList<string>;

        Assert.NotNull(requiredToolNames);
        Assert.Contains("browser_take_screenshot", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("browser_snapshot", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("browser_console_messages", requiredToolNames, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveMissingRequiredToolExecutionsWithCarryForward_allows_prefetched_project_structure_grounding_to_satisfy_required_read()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredToolExecutionsWithCarryForward = serviceType.GetMethod(
            "ResolveMissingRequiredToolExecutionsWithCarryForward",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredToolExecutionsWithCarryForward method was not found.");

        var candidate = CreateProjectStructureDispatchCandidate(
            "Clarify the scope and release boundary for the workflow delivery.",
            new ProcessProjectStructureContext
            {
                ProjectId = Guid.NewGuid(),
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-basic-app",
                ParentNodeTitle = "Create basic app"
            },
            ProcessStepKind.Start);
        var detail = new ExecutionRunDetail(
            CreateExecutionRun("process-automation-dispatch", ExecutionState.Completed, RunOutcome.Succeeded),
            null,
            [],
            []);

        var missingRequiredTools = resolveMissingRequiredToolExecutionsWithCarryForward.Invoke(
            null,
            [candidate, detail, new[] { "project_structure_read" }]) as IReadOnlyList<string>;

        Assert.NotNull(missingRequiredTools);
        Assert.DoesNotContain("project_structure_read", missingRequiredTools, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveMissingRequiredToolExecutionsWithCarryForward_allows_prefetched_artifact_inspection_to_satisfy_governed_review_reads()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredToolExecutionsWithCarryForward = serviceType.GetMethod(
            "ResolveMissingRequiredToolExecutionsWithCarryForward",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredToolExecutionsWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidateCore(
            "Review architecture and canonical-model impact.",
            ProcessStepKind.Review,
            [],
            false,
            [],
            [
                (
                    "Clarify scope and release boundary",
                    "Scope boundary packet",
                    [
                        (
                            "Scope boundary packet",
                            "Brief",
                            "artifacts/scopes/organization/demo/process-runs/0001/01-scope-boundary-packet.md",
                            "Captured the workflow scope and boundary.",
                            "Projected from the prior governed step.")
                    ])
            ]);
        var detail = new ExecutionRunDetail(
            CreateExecutionRun("process-automation-dispatch", ExecutionState.Completed, RunOutcome.Succeeded),
            null,
            [],
            []);

        var missingRequiredTools = resolveMissingRequiredToolExecutionsWithCarryForward.Invoke(
            null,
            [candidate, detail, new[] { "workspace_stat_path", "workspace_read_file" }]) as IReadOnlyList<string>;

        Assert.NotNull(missingRequiredTools);
        Assert.DoesNotContain("workspace_stat_path", missingRequiredTools, StringComparer.Ordinal);
        Assert.DoesNotContain("workspace_read_file", missingRequiredTools, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_ignores_negated_tool_references_when_required_step_tools_succeed()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the requested application.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude. Do not use workspace_append_file for canonical deliverables.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Required implementation tools succeeded without blocked follow-up."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_pwsh_run_script",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Apply-WorkflowShowcaseApp.ps1",
                    "showcases/blazor-ssr-workflow",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "SimpleWorkflowApp.csproj",
                    "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement feature"]) as string;

        Assert.True(status == ProcessStepRunStatus.Completed, reason);
    }

    [Fact]
    public void ResolveCompletionStatus_ignores_failed_workspace_git_diff_in_non_git_delivery_workspaces()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate(
            "Review the implementation evidence and architecture decision record.",
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Code review run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Required browser evidence was captured."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Artifact path verified.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Artifact contents reviewed."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_git_diff",
                    "LocalExecution",
                    "NotRequired",
                    "PolicyOnlyLocal",
                    "RequestedApplication.slnx",
                    "deliveries/blazor-ssr-requested-application",
                    "Failed: Not a git repository.",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Complete governed code review"]) as string;

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.NotNull(reason);
        Assert.Contains("completed step", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspace_git_diff", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_keeps_failed_workspace_dotnet_build_as_a_critical_tool_failure()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate("Review the implementation evidence and architecture decision record.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Build verification run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Required browser evidence was captured."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "NotRequired",
                    "PolicyOnlyLocal",
                    "RequestedApplication.slnx",
                    "deliveries/blazor-ssr-requested-application",
                    "Failed: Build error.",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement feature"]) as string;

        Assert.Equal(ProcessStepRunStatus.Failed, status);
        Assert.NotNull(reason);
        Assert.Contains("workspace_dotnet_build", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_when_agent_declares_a_governed_blocking_outcome()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate(
            "Review the delivered application and block progression if the required feature is missing.",
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Blocked,
                    "Critical defect: the stock scaffold still renders and the units conversion flow does not exist.",
                    summaryMarkdown: "Critical defect: the stock scaffold still renders and the units conversion flow does not exist."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Artifact path verified.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Artifact contents reviewed."))),
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Run QA validation and browser proof"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("blocked step", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("units conversion flow does not exist", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_completed_browser_proof_step_when_response_reports_missing_proof()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the workflow app.",
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Residual risk captured.",
                    summaryMarkdown: "QA validation and browser proof cannot proceed because the application is not running and no screenshots can be captured."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Artifact path verified.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Artifact contents reviewed.")),
                    ("browser_console_messages", CreateProviderNativeTextResult("Console inspected.")),
                    ("browser_snapshot", CreateProviderNativeTextResult("Snapshot captured.")),
                    ("browser_take_screenshot", CreateProviderNativeTextResult("Screenshot captured."))),
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Run QA validation and browser proof"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("missing required browser proof", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("browser proof could not proceed", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_fails_when_run_completed_but_provider_outcome_failed()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate("Implement the workflow and prove the build passes.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "The run ended with an execution error.",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Failed,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement feature"]) as string;

        Assert.Equal(ProcessStepRunStatus.Failed, status);
        Assert.NotNull(reason);
        Assert.Contains("failed", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildStorageRelativePath_preserves_the_real_managed_artifact_relative_path()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildStorageRelativePath = serviceType.GetMethod("BuildStorageRelativePath", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildStorageRelativePath method was not found.");

        var candidate = CreateDispatchCandidate("Review the implementation evidence.");
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            "Implementation change set",
            "artifacts/scopes/organization/demo/deliveries/blazor-ssr-workflow-suite/process/implementation/implementation-change-set.md",
            "text/markdown",
            "workspace",
            "Durable implementation evidence",
            DateTimeOffset.UtcNow);

        var relativePath = buildStorageRelativePath.Invoke(null, [candidate, artifact]) as string;

        Assert.Equal(
            "artifacts/scopes/organization/demo/deliveries/blazor-ssr-workflow-suite/process/implementation/implementation-change-set.md",
            relativePath);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_completion_when_required_browser_tools_are_confirmed_by_session_state()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");

        var candidate = CreateDispatchCandidate(
            "Validate the UI.\nInstructions: Call browser_take_screenshot, browser_snapshot, and browser_console_messages before you conclude.",
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Required browser evidence was captured."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Artifact path verified.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Artifact contents reviewed.")),
                    ("browser_take_screenshot", CreateProviderNativeTextResult("Screenshot saved.")),
                    ("browser_snapshot", CreateProviderNativeTextResult("Snapshot saved.")),
                    ("browser_console_messages", CreateProviderNativeTextResult("Console log saved."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_pwsh_run_script",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Import-PlaywrightEvidence.ps1",
                    "showcases/blazor-ssr-workflow",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
    }

    [Fact]
    public void ResolveCompletionStatus_ignores_suggested_browser_input_tools_when_browser_proof_succeeds()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");

        var candidate = CreateDispatchCandidate(
            "Validate the UI.\nInstructions: Use browser_resize, browser_navigate, browser_fill_form, browser_select_option, browser_take_screenshot, browser_snapshot, and browser_console_messages before you conclude.",
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Required browser evidence was captured."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Artifact path verified.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Artifact contents reviewed.")),
                    ("browser_take_screenshot", CreateProviderNativeTextResult("Screenshot saved.")),
                    ("browser_snapshot", CreateProviderNativeTextResult("Snapshot saved.")),
                    ("browser_console_messages", CreateProviderNativeTextResult("Console log saved."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_pwsh_run_script",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Launch-UnitsApp.ps1",
                    "deliveries/workflow-suite",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
    }

    [Fact]
    public void ResolveSuccessfulSessionToolOutputFiles_returns_browser_filenames_for_successful_calls()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveSuccessfulSessionToolOutputFiles = serviceType.GetMethod("ResolveSuccessfulSessionToolOutputFiles", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveSuccessfulSessionToolOutputFiles method was not found.");

        var serializedSessionState = BuildSerializedSessionState(
            ("browser_take_screenshot", new Dictionary<string, object?> { ["filename"] = "execute-release-rollout/workflow-proof.png" }, CreateProviderNativeTextResult("Screenshot saved.")),
            ("browser_snapshot", new Dictionary<string, object?> { ["filename"] = "execute-release-rollout/workflow-page.yml" }, CreateProviderNativeTextResult("Snapshot saved.")),
            ("browser_console_messages", new Dictionary<string, object?> { ["filename"] = "execute-release-rollout/workflow-console.log" }, CreateProviderNativeTextResult("Console log saved.")));

        var outputFilesByToolName = resolveSuccessfulSessionToolOutputFiles.Invoke(null, [serializedSessionState]) as IReadOnlyDictionary<string, IReadOnlyList<string>>;

        Assert.NotNull(outputFilesByToolName);
        Assert.Contains(outputFilesByToolName.Keys, item => string.Equals(item, "browser_take_screenshot", StringComparison.Ordinal));
        Assert.Contains(outputFilesByToolName.Keys, item => string.Equals(item, "browser_snapshot", StringComparison.Ordinal));
        Assert.Contains(outputFilesByToolName.Keys, item => string.Equals(item, "browser_console_messages", StringComparison.Ordinal));
        Assert.Contains("execute-release-rollout/workflow-proof.png", outputFilesByToolName["browser_take_screenshot"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains("execute-release-rollout/workflow-page.yml", outputFilesByToolName["browser_snapshot"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains("execute-release-rollout/workflow-console.log", outputFilesByToolName["browser_console_messages"], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Response_text_projection_only_targets_markdown_and_text_artifacts()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var isResponseProjectableTextArtifact = serviceType.GetMethod("IsResponseProjectableTextArtifact", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("IsResponseProjectableTextArtifact method was not found.");

        Assert.True((bool)(isResponseProjectableTextArtifact.Invoke(null, ["artifacts/deliveries/units/process/peer-review/peer-review-note.md"]) ?? false));
        Assert.True((bool)(isResponseProjectableTextArtifact.Invoke(null, ["artifacts/deliveries/units/process/release/release-note.txt"]) ?? false));
        Assert.False((bool)(isResponseProjectableTextArtifact.Invoke(null, ["artifacts/deliveries/units/ui/qa-validation/proof.png"]) ?? true));
    }

    [Fact]
    public void Response_text_projection_external_reference_key_normalizes_scoped_managed_paths()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildResponseTextArtifactExternalReferenceKey = serviceType.GetMethod("BuildResponseTextArtifactExternalReferenceKey", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildResponseTextArtifactExternalReferenceKey method was not found.");
        var executionRunId = Guid.NewGuid();

        var scopedKey = buildResponseTextArtifactExternalReferenceKey.Invoke(
            null,
            [executionRunId, "artifacts/scopes/organization/demo/deliveries/blazor-ssr-workflow-suite/process/peer-review/peer-review-note.md"]) as string;
        var unscopedKey = buildResponseTextArtifactExternalReferenceKey.Invoke(
            null,
            [executionRunId, "artifacts/deliveries/blazor-ssr-workflow-suite/process/peer-review/peer-review-note.md"]) as string;

        Assert.Equal(unscopedKey, scopedKey);
    }

    [Theory]
    [InlineData(ExecutionState.Completed, RunOutcome.Succeeded, true)]
    [InlineData(ExecutionState.Completed, RunOutcome.Failed, false)]
    [InlineData(ExecutionState.Failed, RunOutcome.Failed, false)]
    [InlineData(ExecutionState.WaitingOnTool, null, false)]
    public void ShouldProjectFinalAssistantResponse_only_allows_completed_successful_runs(
        ExecutionState state,
        RunOutcome? outcome,
        bool expected)
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldProjectFinalAssistantResponse = serviceType.GetMethod("ShouldProjectFinalAssistantResponse", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldProjectFinalAssistantResponse method was not found.");

        var result = (bool)(shouldProjectFinalAssistantResponse.Invoke(null, [CreateExecutionRun("process-automation-dispatch", state, outcome)]) ?? false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ExecutionState.Completed, RunOutcome.Succeeded, ProcessStepRunStatus.Completed, true)]
    [InlineData(ExecutionState.Completed, RunOutcome.Succeeded, ProcessStepRunStatus.Failed, false)]
    [InlineData(ExecutionState.Completed, RunOutcome.Succeeded, ProcessStepRunStatus.WaitingApproval, false)]
    [InlineData(ExecutionState.Completed, RunOutcome.Failed, ProcessStepRunStatus.Completed, false)]
    public void ShouldProjectResponseTextArtifacts_requires_a_completed_process_step(
        ExecutionState state,
        RunOutcome? outcome,
        ProcessStepRunStatus completionStatus,
        bool expected)
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldProjectResponseTextArtifacts = serviceType.GetMethod("ShouldProjectResponseTextArtifacts", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldProjectResponseTextArtifacts method was not found.");

        var result = (bool)(shouldProjectResponseTextArtifacts.Invoke(null, [CreateExecutionRun("process-automation-dispatch", state, outcome), completionStatus]) ?? false);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CanProjectResponseTextArtifactWithoutDeclaredPath_allows_pathless_implementation_change_set_deliverable()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var canProjectResponseTextArtifactWithoutDeclaredPath = serviceType.GetMethod(
            "CanProjectResponseTextArtifactWithoutDeclaredPath",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CanProjectResponseTextArtifactWithoutDeclaredPath method was not found.");
        var expectedArtifact = new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
            Guid.NewGuid(),
            ProcessArtifactKind.Deliverable,
            "Implementation change set",
            true,
            ProcessArtifactTrustRequirement.ReviewRequired,
            ProcessSensitivityLevel.Internal,
            "Must be linked to tests, migration notes, and touched-surface inventory.",
            string.Empty);

        var result = (bool)(canProjectResponseTextArtifactWithoutDeclaredPath.Invoke(null, [expectedArtifact]) ?? false);

        Assert.True(result);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_start_step_when_required_artifact_response_is_conversational()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "Clarify scope and release boundary",
            "Clarify the workflow scope, acceptance checks, and release boundary.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Scope boundary packet", true, "Must capture in-scope behavior, out-of-scope behavior, acceptance checks, and release boundary."));
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Scope clarification",
                "process-step",
                "step-scope",
                "corr-scope",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "I'm ready to help with the QA tasks. Please let me know what specific area or step you'd like me to review or test.",
                "Remote Ollama",
                "gptoss32k:latest",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists."))),
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Clarify scope and release boundary"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("Scope boundary packet", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_start_step_when_required_artifact_response_is_wrong_domain()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "Clarify scope and release boundary",
            "Clarify the workflow scope, acceptance checks, and release boundary.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Scope boundary packet", true, "Must capture in-scope behavior, out-of-scope behavior, acceptance checks, tenant impact, and release boundary."));
        var now = DateTimeOffset.UtcNow;
        var responseText = """
            ## Project layout

            C:\programovani\csharp\workflow

            Create a WorkflowApp folder with a Blazor WebAssembly project, add a Workflow.razor component,
            wire up queue, pause, resume, and complete buttons, and then run dotnet build. The executable or published output can
            be copied to the requested folder after the template is created.
            """;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Scope clarification",
                "process-step",
                "step-scope",
                "corr-scope",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "Remote Ollama",
                "gptoss32k:latest",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Clarify scope and release boundary"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("Scope boundary packet", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_completes_start_step_when_required_artifact_response_matches_contract()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "Clarify scope and release boundary",
            "Clarify the workflow scope, acceptance checks, and release boundary.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Scope boundary packet", true, "Must capture in-scope behavior, out-of-scope behavior, acceptance checks, tenant impact, and release boundary."));
        var now = DateTimeOffset.UtcNow;
        var responseText = """
            ## Scope boundary packet

            In-scope behavior: deliver a Blazor SSR workflow in C:\programovani\csharp\workflow with status controls, queue/pause/resume/complete actions, and an activity history list.
            Out-of-scope behavior: authentication, persistence beyond the in-memory history list, multi-tenant administration, and deployment automation are excluded from this run.
            Acceptance checks: the app builds, the workflow controls update visible state, invalid transitions are handled, and completed actions append to history.
            Tenant impact: local single-user demo only; no tenant data, secrets, or external integrations are touched.
            Release boundary: runnable source and validation evidence are required before downstream review proceeds.
            """;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Scope clarification",
                "process-step",
                "step-scope",
                "corr-scope",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "Remote Ollama",
                "gptoss32k:latest",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
    }

    [Fact]
    public void BuildExecutionPrompt_adds_governed_inspection_rules_for_review_steps()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate(
            "Review the delivered change set and write the peer review note.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Peer review note", true, "Create this artifact at artifacts/deliveries/units/process/peer-review/peer-review-note.md."));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("workspace_stat_path and workspace_read_file", prompt, StringComparison.Ordinal);
        Assert.Contains("EvidenceRefs must name only current-run tool-backed paths, durable artifacts, or attached skill/template resources", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_stat_path on these governed output paths", prompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/deliveries/units/process/peer-review/peer-review-note.md", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_read_file on these text-based governed artifacts", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_requires_exact_response_sections_for_required_artifacts()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "Review architecture and canonical-model impact",
            "Review the grounded project structure and produce the architecture decision record.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Decision, "Architecture decision record", true, "Must capture selected option, rejected options, source-of-truth choice, and migration ownership."),
            (ProcessArtifactKind.Brief, "Project structure context brief", true, "Must capture the originating project-structure node, resolved working directory, touched modules or routes, dependency boundaries, and downstream artifact expectations."));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Required display summary structure:", prompt, StringComparison.Ordinal);
        Assert.Contains("## Architecture decision record", prompt, StringComparison.Ordinal);
        Assert.Contains("## Project structure context brief", prompt, StringComparison.Ordinal);
        Assert.Contains("HumanReadableSummaryMarkdown", prompt, StringComparison.Ordinal);
        Assert.Contains("keep those exact section titles", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildExecutionPrompt_guides_implementation_review_to_use_prior_validation_evidence_and_avoid_transient_output_assumptions()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "Complete peer review and integration readiness",
            "Review the delivered workflow app and confirm integration readiness.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Peer review note", true, "Create this artifact at artifacts/deliveries/units/process/peer-review/peer-review-note.md."));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("inspect actual changed files, durable artifacts, records, or outputs", prompt, StringComparison.Ordinal);
        Assert.Contains("Successful upstream validation receipts", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not require fresh transient outputs", prompt, StringComparison.Ordinal);
        Assert.Contains("grounded external target", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_dotnet_build", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(".sln", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("net8.0", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_keeps_greenfield_implementation_guidance_domain_neutral()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate("Implement the requested application and prove the build passes.");

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("create the real deliverable now", prompt, StringComparison.Ordinal);
        Assert.Contains("Follow the current step contract, assigned agent instructions, available skills", prompt, StringComparison.Ordinal);
        Assert.Contains("choose the correct project shape, folder structure, tools, and validation path", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_dotnet_new", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(".csproj", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("net10.0", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_requires_upstream_artifact_inspection_and_runnable_host_for_browser_ui_implementation()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidateWithArtifactInputs(
            "Implement the workflow as a Blazor app and prove the build passes.",
            (
                "Document the workflow architecture",
                "Workflow architecture",
                [
                    (
                        "Workflow architecture",
                        "evidence",
                        "artifacts/scopes/organization/demo/architecture/Workflow-Architecture.md",
                        "Blazor Server app with workflow UI.",
                        "Approved architecture note.")
                ]));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("inspect the upstream durable artifacts directly", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_stat_path", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_read_file", prompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/scopes/organization/demo/architecture/Workflow-Architecture.md", prompt, StringComparison.Ordinal);
        Assert.Contains("browser-visible UI", prompt, StringComparison.Ordinal);
        Assert.Contains("runnable or reviewable browser surface", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("browser-validated Blazor or web app", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_requires_grounded_project_structure_features_now_for_implementation_steps()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var projectId = Guid.NewGuid();
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the workflow as a Blazor app and prove the build passes.",
            new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-main-application",
                ParentNodeTitle = "Create main application"
            });
        const string projectStructureGrounding = """
Dispatcher fetched the live project structure for `Workflow` and focused this prompt on the selected work branch.
Requirements from project-level planning context:
- Blazor SSR (feature:blazor); type: ProjectBlock/feature
- status buttons for queue, pause, resume, complete (feature:buttons); type: ProjectBlock/feature
- activity history list (feature:history); type: ProjectBlock/feature
- output must be placed in C:\programovani\csharp\workflow (note:output-path); type: ProjectBlock/note
""";

        var prompt = buildExecutionPromptCore.Invoke(null, [candidate, null, projectStructureGrounding, null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Concrete feature and constraint nodes from the live project structure are required scope for this implementation step.", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not defer grounded features, UI behavior, acceptance notes, or output constraints", prompt, StringComparison.Ordinal);
        Assert.Contains("replace placeholder output with the requested product, document, analysis, workflow, or other concrete deliverable", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not write implementation artifacts that say the requested behavior, analysis, artifacts, tests", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Hello, world!", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkflowEngine", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_does_not_tell_scope_steps_to_scaffold_grounded_external_targets()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var projectId = Guid.NewGuid();
        var projectStructureContext = new ProcessProjectStructureContext
        {
            ProjectId = projectId,
            NodeId = "process-definition:software-delivery",
            NodeTitle = "Multi-team software delivery and release governance",
            ParentNodeId = "task:create-main-application",
            ParentNodeTitle = "Create main application"
        };
        var candidate = CreateDispatchCandidateCore(
            "Clarify scope, operational impact, acceptance boundary, known dependencies, and explicit exclusions before delivery commits.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Brief, "Scope boundary packet", true, "Must capture no-go constraints, user or operational impact, and acceptance boundary in typed form.")],
            [],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver the generated application showcase.",
                projectStructureContext),
            "Clarify scope and release boundary");
        const string projectStructureGrounding = """
Dispatcher fetched the live project structure for `Workflow` and focused this prompt on the selected work branch.
Grounded external target paths from the selected project structure:
- `C:\programovani\dotnet\WorkflowBoard` mapped to `external-target/C/programovani/dotnet/WorkflowBoard` from Workflow
""";

        var prompt = buildExecutionPromptCore.Invoke(null, [candidate, null, projectStructureGrounding, null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("keep that directory as the authoritative product boundary for this run", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("scaffold and implement in that exact location during this step", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("call the scaffold tool with parent directory", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("For this implementation, create and edit the deliverable", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_dotnet_new", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_surfaces_grounded_external_target_and_scaffold_mapping_for_implementation_steps()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var projectId = Guid.NewGuid();
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the workflow as a Blazor app and prove the build passes.",
            new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-main-application",
                ParentNodeTitle = "Create main application"
            });
        const string projectStructureGrounding = """
Dispatcher fetched the live project structure for `Workflow` and focused this prompt on the selected work branch.
Requirements from project-level planning context:
- Blazor SSR (feature:blazor); type: ProjectBlock/feature
- output must be placed in C:\programovani\csharp\workflow (note:output-path); type: ProjectBlock/note
""";

        var prompt = buildExecutionPromptCore.Invoke(null, [candidate, null, projectStructureGrounding, null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains(
            "The grounded project structure already identifies the external output root `C:\\programovani\\csharp\\workflow` mapped to `external-target/C/programovani/csharp/workflow`.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "For this implementation, create and edit the deliverable under `external-target/C/programovani/csharp/workflow`.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not build a shadow product in `artifacts/`, `output/`, `data/`, or other managed evidence folders",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "For .NET scaffolding into the grounded external product root",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "use `workspace_dotnet_new` with `parentDirectory` set to `external-target/C/programovani/csharp` and `name` set to `workflow`",
            prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain("runnable app", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProjectStructureGroundingSummary_merges_canonical_workbench_nodes_when_surface_omits_siblings()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var groundingNodeType = serviceType.GetNestedType("ProjectStructureGroundingNodeData", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProjectStructureGroundingNodeData type was not found.");
        var buildGroundingSummary = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method =>
                method.Name == "BuildProjectStructureGroundingSummary" &&
                method.GetParameters().Length == 4 &&
                method.GetParameters()[0].ParameterType == typeof(string));
        var projectId = Guid.NewGuid();
        var context = new ProcessProjectStructureContext
        {
            ProjectId = projectId,
            NodeId = "process-definition:software-delivery",
            NodeTitle = "Multi-team software delivery and release governance",
            ParentNodeId = "custom:create-main-application",
            ParentNodeTitle = "Create main application"
        };

        var surfaceNodes = Array.CreateInstance(groundingNodeType, 3);
        surfaceNodes.SetValue(CreateProjectStructureGroundingNode(
            groundingNodeType,
            $"project:{projectId:D}",
            string.Empty,
            "ProjectRoot",
            string.Empty,
            "Workflow",
            string.Empty,
            "Active",
            string.Empty,
            "{}"), 0);
        surfaceNodes.SetValue(CreateProjectStructureGroundingNode(
            groundingNodeType,
            "custom:create-main-application",
            $"project:{projectId:D}",
            "WorkItem",
            "task",
            "Create main application",
            string.Empty,
            "Draft",
            string.Empty,
            "{}"), 1);
        surfaceNodes.SetValue(CreateProjectStructureGroundingNode(
            groundingNodeType,
            "process-definition:software-delivery",
            "custom:create-main-application",
            "ProcessDefinition",
            string.Empty,
            "Multi-team software delivery and release governance",
            "Published · 9 step(s)",
            "Published",
            "Universal delivery template.",
            "{}"), 2);

        var canonicalNodes = Array.CreateInstance(groundingNodeType, 4);
        canonicalNodes.SetValue(CreateProjectStructureGroundingNode(
            groundingNodeType,
            "custom:main-architecture",
            $"project:{projectId:D}",
            "ProjectBlock",
            "architecture",
            "Main architecture",
            string.Empty,
            "Draft",
            string.Empty,
            "{}"), 0);
        canonicalNodes.SetValue(CreateProjectStructureGroundingNode(
            groundingNodeType,
            "custom:main-features",
            $"project:{projectId:D}",
            "ProjectBlock",
            "feature",
            "Main features",
            string.Empty,
            "Draft",
            string.Empty,
            "{}"), 1);
        canonicalNodes.SetValue(CreateProjectStructureGroundingNode(
            groundingNodeType,
            "custom:blazor-ssr",
            "custom:main-architecture",
            "ProjectBlock",
            "feature",
            "Blazor SSR",
            string.Empty,
            "Draft",
            "Blazor SSR",
            "{}"), 2);
        canonicalNodes.SetValue(CreateProjectStructureGroundingNode(
            groundingNodeType,
            "custom:output-path",
            "custom:main-architecture",
            "Note",
            string.Empty,
            @"output must be placed in C:\programovani\csharp\workflow",
            string.Empty,
            "Draft",
            @"output must be placed in C:\programovani\csharp\workflow",
            "{}"), 3);

        var summary = buildGroundingSummary.Invoke(
            null,
            [
                "Workflow",
                surfaceNodes,
                canonicalNodes,
                context
            ]) as string;

        Assert.NotNull(summary);
        Assert.Contains("Project-level planning context under the target parent:", summary, StringComparison.Ordinal);
        Assert.Contains("Main architecture", summary, StringComparison.Ordinal);
        Assert.Contains("Main features", summary, StringComparison.Ordinal);
        Assert.Contains("Requirements from project-level planning context:", summary, StringComparison.Ordinal);
        Assert.Contains("Blazor SSR", summary, StringComparison.Ordinal);
        Assert.Contains(@"output must be placed in C:\programovani\csharp\workflow", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_requires_tests_now_when_implementation_step_contract_mentions_tests()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate("Implement feature, tests, and migration notes for the workflow app.");

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("This implementation step explicitly includes tests.", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not defer implementation-owned tests to a later QA-only step", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRequiredToolNames_adds_governed_review_and_text_artifact_tools()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate(
            "Review the delivered change set and write the peer review note.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Peer review note", true, "Create this artifact at artifacts/deliveries/units/process/peer-review/peer-review-note.md."));

        var requiredToolNames = resolveRequiredToolNames.Invoke(null, [candidate]) as IReadOnlyList<string>;

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_stat_path", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_read_file", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_write_file", requiredToolNames, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_fails_governed_step_without_declared_outcome()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate(
            "Review the delivered change set and write the peer review note.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Peer review note", true, "Create this artifact at artifacts/deliveries/units/process/peer-review/peer-review-note.md."));
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Review run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Peer review note written.",
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete.")),
                    ("workspace_write_file", CreateProviderNativeTextResult("File written."))),
                []),
            null,
            [],
            [])
        {
            Artifacts =
            [
                new ExecutionArtifactRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "peer-review-note.md",
                    "artifacts/deliveries/units/process/peer-review/peer-review-note.md",
                    "text/markdown",
                    "workspace_write_file",
                    "Peer review note written.",
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Complete governed code review"]) as string;

        Assert.NotNull(reason);
        Assert.True(status == ProcessStepRunStatus.Failed, reason);
        Assert.Contains("valid structured ProcessStepOutcomeResult", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRequiredToolNames_adds_file_inspection_proof_tools_for_work_steps()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate("Implement the generated application and inspect the concrete deliverable files.");

        var requiredToolNames = resolveRequiredToolNames.Invoke(null, [candidate]) as IReadOnlyList<string>;

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_stat_path", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_read_file", requiredToolNames, StringComparer.Ordinal);
        Assert.DoesNotContain("workspace_dotnet_build", requiredToolNames, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_fails_work_step_without_declared_outcome()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidate("Implement the requested application and prove the build passes.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Implementation complete.",
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Units.slnx",
                    "deliveries/workflow-suite/src",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement the requested application"]) as string;

        Assert.Equal(ProcessStepRunStatus.Failed, status);
        Assert.NotNull(reason);
        Assert.Contains("valid structured ProcessStepOutcomeResult", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_work_step_when_completed_response_defers_feature_work()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Blazor SSR app scaffolded, build succeeded, and required artifacts written per scope and architecture requirements.",
            summaryMarkdown: """
            - Read and validated upstream architecture and scope artifacts.
            - Confirmed the required output directory and Blazor SSR stack.
            - Successfully scaffolded a Blazor SSR app in `external-target/C/programovani/csharp/workflow/WorkflowApp` targeting .NET 10.0.
            - Verified the presence and content of key files (Program.cs, Home.razor).
            - Ran a successful build for the scaffolded project.
            - Created the required implementation change set and migration/rollout checklist artifacts, including evidence of the build and next steps for feature implementation.

            The main application is now scaffolded and buildable in the required location, ready for feature implementation.
            """);
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "WorkflowApp.csproj",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_test",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp.Tests/WorkflowApp.Tests.csproj",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp.Tests",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement feature, tests, and migration notes"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("deferred required implementation work", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_retries_scaffold_only_completed_implementation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Blazor SSR app scaffolded, build succeeded, and required artifacts written per scope and architecture requirements.",
            summaryMarkdown: """
            Scaffolded a Blazor SSR app in the required location, verified the default pages, and wrote the implementation artifact.
            The main application is now scaffolded and buildable in the required location, ready for feature implementation.
            """);
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "WorkflowApp.csproj",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_test",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp.Tests/WorkflowApp.Tests.csproj",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp.Tests",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, Array.Empty<string>(), 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ResolveCompletionStatus_honors_blocked_outcome_when_validation_tool_failed()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate(
            "Validate the generated web app with browser proof and route repair when the runtime smoke fails.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Runtime validation evidence", true, "Record startup, browser proof, and any repair-blocking defects."));
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Runtime startup smoke failed with HTTP 500 before browser proof could be captured.",
            summaryMarkdown: "The validation step is blocked because the app did not start successfully.");
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Runtime validation run",
                "process-step",
                "step-validation",
                "corr-validation",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_run",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "external-target/C/programovani/dotnet/GeneratedWebApp/GeneratedWebApp.csproj",
                    "external-target/C/programovani/dotnet/GeneratedWebApp",
                    "Failed (exit 1)",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Validate generated web app"]) as string;
        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, new[] { "browser_snapshot", "browser_take_screenshot" }, 1, 3]);

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("Runtime startup smoke failed", reason, StringComparison.Ordinal);
        Assert.IsType<bool>(shouldRetryResult);
        Assert.False((bool)shouldRetryResult);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_work_step_when_response_lists_next_required_actions()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        const string responseText = """
            **Summary of current state and actions taken:**
            - The Workflow app is scaffolded and builds successfully.
            - The main route is still untouched template output ("Hello, world!").
            - No required workflow UI or logic is present yet.
            - A test project (`WorkflowApp.Tests`) was created using xUnit.

            **Next required actions:**
            - Replace the template `Home.razor` with a workflow UI.
            - Implement minimal business logic.
            - Add at least one meaningful automated test.
            - Prepare and write the required migration/rollout checklist artifact.
            - Prepare and write the required implementation change set artifact.

            **Proceeding to implement the workflow UI and logic, update tests, and write required artifacts.**
            """;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "WorkflowApp.csproj",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_test",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp.Tests/WorkflowApp.Tests.csproj",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp.Tests",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement feature, tests, and migration notes"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("deferred required implementation work", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_retries_when_response_still_has_next_required_actions()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        const string responseText = """
            Summary of current state:
            - The app is scaffolded and builds successfully.
            - No required workflow UI or logic is present yet.

            Next required actions:
            - Replace the template page with a workflow UI.
            - Implement the required logic and tests.

            Proceeding to implement the workflow UI and logic.
            """;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_build",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "WorkflowApp.csproj",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, Array.Empty<string>(), 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void BuildExecutionPrompt_lists_available_branch_outcomes_when_present()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidateWithBranchOutcomes(
            "Review the implementation and route the next step honestly.",
            true,
            ("approved", "Approved", "Continue to QA."),
            ("changes_requested", "Changes requested", "Route back to implementation."));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Available branch outcomes:", prompt, StringComparison.Ordinal);
        Assert.Contains("approved (Approved)", prompt, StringComparison.Ordinal);
        Assert.Contains("set BranchOutcomeKey to the exact branchOutcomeKey", prompt, StringComparison.Ordinal);
        Assert.Contains("Branch outcomes are governed dispositions", prompt, StringComparison.Ordinal);
        Assert.Contains("repair, remediation, rework, changes required, or rejected validation", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_add_dotnet_validation_from_test_word_alone()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate("Implement the monthly workbook and include test notes.");

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_stat_path", requiredToolNames);
        Assert.Contains("workspace_read_file", requiredToolNames);
        Assert.DoesNotContain("workspace_dotnet_build", requiredToolNames);
        Assert.DoesNotContain("workspace_dotnet_test", requiredToolNames);
    }

    [Fact]
    public void ContainsRunnableApplicationContractSignal_does_not_match_app_inside_approval()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod("ContainsRunnableApplicationContractSignal", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ContainsRunnableApplicationContractSignal method was not found.");
        var candidate = CreateDispatchCandidate("Prepare the release approval package for a document deliverable.");

        var hasSignal = method.Invoke(null, [candidate]);

        Assert.IsType<bool>(hasSignal);
        Assert.False((bool)hasSignal);
    }

    [Theory]
    [InlineData("Implement the requested web app and prove startup.")]
    [InlineData("Implement the requested API and prove startup.")]
    [InlineData("Run the .csproj host and capture startup proof.")]
    public void ContainsRunnableApplicationContractSignal_matches_explicit_runnable_app_terms(string workBriefText)
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod("ContainsRunnableApplicationContractSignal", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ContainsRunnableApplicationContractSignal method was not found.");
        var candidate = CreateDispatchCandidate(workBriefText);

        var hasSignal = method.Invoke(null, [candidate]);

        Assert.IsType<bool>(hasSignal);
        Assert.True((bool)hasSignal);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_completed_dotnet_web_implementation_without_runtime_startup_proof()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var productRoot = Path.Combine(Path.GetTempPath(), $"candoitall-runtime-proof-{Guid.NewGuid():N}");
        try
        {
            var projectPath = Path.Combine(productRoot, "WorkflowSuite.Web.csproj");
            var sourcePath = Path.Combine(productRoot, "Program.cs");
            Directory.CreateDirectory(productRoot);
            File.WriteAllText(
                projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(sourcePath, "var builder = WebApplication.CreateBuilder(args);");

            var rootAlias = ToExternalTargetAlias(productRoot);
            var projectAlias = ToExternalTargetAlias(projectPath);
            var sourceAlias = ToExternalTargetAlias(sourcePath);
            var candidate = CreateDispatchCandidate("Implement the requested Blazor application and prove build, tests, and startup smoke.");
            var now = DateTimeOffset.UtcNow;
            var responseText = StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Implementation completed and tests passed.");
            var detail = new ExecutionRunDetail(
                new ExecutionRunRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "Implementation run",
                    "process-step",
                    "step-1",
                    "corr-1",
                    "run-start",
                    "process-automation-dispatch",
                    "system",
                    BuildAllowedExternalTargetMetadata(rootAlias),
                    "Prompt",
                    responseText,
                    "OpenAI chat completions",
                    "gpt-4.1",
                    ExecutionState.Completed,
                    RunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Read complete.")),
                        ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed."))),
                    []),
                null,
                [],
                [])
            {
                ToolReceipts =
                [
                    CreateToolReceipt("workspace-file", "workspace_stat_path", rootAlias, ".", "Succeeded", now),
                    CreateToolReceipt("workspace-file", "workspace_write_file", sourceAlias, ".", "Succeeded", now.AddSeconds(1)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_build", projectAlias, rootAlias, "Succeeded", now.AddSeconds(2)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(3))
                ]
            };

            var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
            var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement feature, tests, and migration notes"]) as string;

            Assert.Equal(ProcessStepRunStatus.Blocked, status);
            Assert.NotNull(reason);
            Assert.Contains("runnable application proof is missing", reason, StringComparison.Ordinal);
            Assert.Contains("run tool", reason, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(productRoot))
            {
                Directory.Delete(productRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveCompletionStatus_accepts_dotnet_web_implementation_with_runtime_startup_proof_after_mutation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var productRoot = Path.Combine(Path.GetTempPath(), $"candoitall-runtime-proof-{Guid.NewGuid():N}");
        try
        {
            var projectPath = Path.Combine(productRoot, "WorkflowSuite.Web.csproj");
            var sourcePath = Path.Combine(productRoot, "Program.cs");
            Directory.CreateDirectory(productRoot);
            File.WriteAllText(
                projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(sourcePath, "var builder = WebApplication.CreateBuilder(args);");

            var rootAlias = ToExternalTargetAlias(productRoot);
            var projectAlias = ToExternalTargetAlias(projectPath);
            var sourceAlias = ToExternalTargetAlias(sourcePath);
            var candidate = CreateDispatchCandidate("Implement the requested Blazor application and prove build, tests, and startup smoke.");
            var now = DateTimeOffset.UtcNow;
            var responseText = StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Implementation completed and startup smoke passed.");
            var detail = new ExecutionRunDetail(
                new ExecutionRunRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "Implementation run",
                    "process-step",
                    "step-1",
                    "corr-1",
                    "run-start",
                    "process-automation-dispatch",
                    "system",
                    BuildAllowedExternalTargetMetadata(rootAlias),
                    "Prompt",
                    responseText,
                    "OpenAI chat completions",
                    "gpt-4.1",
                    ExecutionState.Completed,
                    RunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Read complete.")),
                        ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                        ("workspace_dotnet_run", CreateProviderNativeTextResult("Started http://127.0.0.1:5000."))),
                    []),
                null,
                [],
                [])
            {
                ToolReceipts =
                [
                    CreateToolReceipt("workspace-file", "workspace_stat_path", rootAlias, ".", "Succeeded", now),
                    CreateToolReceipt("workspace-file", "workspace_write_file", sourceAlias, ".", "Succeeded", now.AddSeconds(1)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_build", projectAlias, rootAlias, "Succeeded", now.AddSeconds(2)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_run", projectAlias, rootAlias, "Succeeded", now.AddSeconds(3)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(4))
                ]
            };

            var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

            Assert.Equal(ProcessStepRunStatus.Completed, status);
        }
        finally
        {
            if (Directory.Exists(productRoot))
            {
                Directory.Delete(productRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_mixed_blazor_hosting_shape_even_with_runtime_startup_proof()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var productRoot = Path.Combine(Path.GetTempPath(), $"candoitall-mixed-blazor-{Guid.NewGuid():N}");
        try
        {
            var projectPath = Path.Combine(productRoot, "WorkflowSuite.Web.csproj");
            var sourcePath = Path.Combine(productRoot, "Program.cs");
            Directory.CreateDirectory(Path.Combine(productRoot, "Components"));
            Directory.CreateDirectory(Path.Combine(productRoot, "Pages"));
            File.WriteAllText(
                projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(
                sourcePath,
                """
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddServerSideBlazor();
                var app = builder.Build();
                app.MapBlazorHub();
                app.MapFallbackToPage("/_Host");
                app.Run();
                """);
            File.WriteAllText(Path.Combine(productRoot, "Components", "App.razor"), "<Routes />");
            File.WriteAllText(Path.Combine(productRoot, "Components", "Routes.razor"), "<Router AppAssembly=\"typeof(Program).Assembly\" />");
            File.WriteAllText(Path.Combine(productRoot, "Pages", "_Host.cshtml"), "@page \"/_Host\"");

            var rootAlias = ToExternalTargetAlias(productRoot);
            var projectAlias = ToExternalTargetAlias(projectPath);
            var sourceAlias = ToExternalTargetAlias(sourcePath);
            var candidate = CreateDispatchCandidate("Implement the requested Blazor app and prove build, tests, and startup smoke.");
            var now = DateTimeOffset.UtcNow;
            var responseText = StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Implementation completed and startup smoke passed.");
            var detail = new ExecutionRunDetail(
                new ExecutionRunRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "Implementation run",
                    "process-step",
                    "step-1",
                    "corr-1",
                    "run-start",
                    "process-automation-dispatch",
                    "system",
                    BuildAllowedExternalTargetMetadata(rootAlias),
                    "Prompt",
                    responseText,
                    "OpenAI chat completions",
                    "gpt-4.1",
                    ExecutionState.Completed,
                    RunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Read complete.")),
                        ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                        ("workspace_dotnet_run", CreateProviderNativeTextResult("Started http://127.0.0.1:5000."))),
                    []),
                null,
                [],
                [])
            {
                ToolReceipts =
                [
                    CreateToolReceipt("workspace-file", "workspace_stat_path", rootAlias, ".", "Succeeded", now),
                    CreateToolReceipt("workspace-file", "workspace_write_file", sourceAlias, ".", "Succeeded", now.AddSeconds(1)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_build", projectAlias, rootAlias, "Succeeded", now.AddSeconds(2)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_run", projectAlias, rootAlias, "Succeeded", now.AddSeconds(3)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(4))
                ]
            };

            var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
            var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implementation completed"]) as string;

            Assert.Equal(ProcessStepRunStatus.Blocked, status);
            Assert.NotNull(reason);
            Assert.Contains("mixed Blazor hosting shape", reason, StringComparison.Ordinal);
            Assert.Contains("Pages/_Host.cshtml", reason, StringComparison.Ordinal);
            Assert.Contains("MapFallbackToPage", reason, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(productRoot))
            {
                Directory.Delete(productRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveCompletionStatus_accepts_runtime_startup_proof_when_run_command_wraps_project_path()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var productRoot = Path.Combine(Path.GetTempPath(), $"candoitall-runtime-proof-{Guid.NewGuid():N}");
        try
        {
            var projectPath = Path.Combine(productRoot, "WorkflowSuite.Web.csproj");
            var sourcePath = Path.Combine(productRoot, "Program.cs");
            Directory.CreateDirectory(productRoot);
            File.WriteAllText(
                projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(sourcePath, "var builder = WebApplication.CreateBuilder(args);");

            var rootAlias = ToExternalTargetAlias(productRoot);
            var projectAlias = ToExternalTargetAlias(projectPath);
            var sourceAlias = ToExternalTargetAlias(sourcePath);
            var candidate = CreateDispatchCandidate("Implement the requested web application and prove build, tests, and startup smoke.");
            var now = DateTimeOffset.UtcNow;
            var responseText = StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Implementation completed and startup smoke passed.");
            var detail = new ExecutionRunDetail(
                new ExecutionRunRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "Implementation run",
                    "process-step",
                    "step-1",
                    "corr-1",
                    "run-start",
                    "process-automation-dispatch",
                    "system",
                    BuildAllowedExternalTargetMetadata(rootAlias),
                    "Prompt",
                    responseText,
                    "OpenAI chat completions",
                    "gpt-4.1",
                    ExecutionState.Completed,
                    RunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Read complete.")),
                        ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                        ("workspace_dotnet_run", CreateProviderNativeTextResult("Started http://127.0.0.1:5000."))),
                    []),
                null,
                [],
                [])
            {
                ToolReceipts =
                [
                    CreateToolReceipt("workspace-file", "workspace_stat_path", rootAlias, ".", "Succeeded", now),
                    CreateToolReceipt("workspace-file", "workspace_write_file", sourceAlias, ".", "Succeeded", now.AddSeconds(1)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_build", projectAlias, rootAlias, "Succeeded", now.AddSeconds(2)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_run", "-NoLogo -NoProfile -EncodedCommand AAAA", rootAlias, "Succeeded", now.AddSeconds(3)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(4))
                ]
            };

            var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

            Assert.Equal(ProcessStepRunStatus.Completed, status);
        }
        finally
        {
            if (Directory.Exists(productRoot))
            {
                Directory.Delete(productRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveMissingConcreteImplementationProofSummary_blocks_required_validation_before_latest_mutation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingProof = serviceType.GetMethod("ResolveMissingConcreteImplementationProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummary method was not found.");
        var candidate = CreateDispatchCandidate("Implement the deliverable. Instructions: call workspace_quality_validate before you conclude.");
        var now = DateTimeOffset.UtcNow;
        var validationTime = now.AddSeconds(1);
        var mutationTime = now.AddSeconds(2);
        var readTime = now.AddSeconds(3);
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Validation ran before the final deliverable mutation.",
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete.")),
                    ("workspace_quality_validate", CreateProviderNativeTextResult("Validation passed.")),
                    ("workspace_write_file", CreateProviderNativeTextResult("File written."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/work/monthly-report/report.md",
                    ".",
                    "Succeeded",
                    readTime,
                    readTime),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_quality_validate",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "external-target/C/work/monthly-report/report.md",
                    ".",
                    "Succeeded",
                    validationTime,
                    validationTime),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_write_file",
                    "MutatingWorkspace",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/work/monthly-report/report.md",
                    ".",
                    "Succeeded",
                    mutationTime,
                    mutationTime)
            ]
        };

        var summary = resolveMissingProof.Invoke(null, [candidate, detail]) as string;

        Assert.NotNull(summary);
        Assert.Contains("workspace_quality_validate", summary, StringComparison.Ordinal);
        Assert.Contains("latest concrete product mutation", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveMissingConcreteImplementationProofSummary_allows_non_code_deliverable_validation_after_mutation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingProof = serviceType.GetMethod("ResolveMissingConcreteImplementationProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummary method was not found.");
        var candidate = CreateDispatchCandidate("Implement the monthly workbook. Instructions: call workspace_quality_validate before you conclude.");
        var now = DateTimeOffset.UtcNow;
        var mutationTime = now.AddSeconds(1);
        var readTime = now.AddSeconds(2);
        var validationTime = now.AddSeconds(3);
        var workbookPath = "external-target/C/work/monthly-report/forecast.xlsx";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Workbook implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Workbook was created and validated.",
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_write_file", CreateProviderNativeTextResult("Workbook written.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Workbook reviewed.")),
                    ("workspace_quality_validate", CreateProviderNativeTextResult("Validation passed."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_write_file",
                    "MutatingWorkspace",
                    "Required",
                    "Workspace-root-only file service.",
                    workbookPath,
                    ".",
                    "Succeeded",
                    mutationTime,
                    mutationTime),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    workbookPath,
                    ".",
                    "Succeeded",
                    readTime,
                    readTime),
                new ToolExecutionReceiptRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_quality_validate",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    workbookPath,
                    ".",
                    "Succeeded",
                    validationTime,
                    validationTime)
            ]
        };

        var summary = resolveMissingProof.Invoke(null, [candidate, detail]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void BuildRecoveryDirective_requires_upstream_artifact_inspection_and_runnable_host_for_browser_ui_retry()
    {
        var buildRecoveryDirective = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildRecoveryDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRecoveryDirective method was not found.");
        var candidate = CreateDispatchCandidateWithArtifactInputs(
            "Implement the workflow as a Blazor app and prove the build passes.",
            (
                "Document the workflow architecture",
                "Workflow architecture",
                [
                    (
                        "Workflow architecture",
                        "evidence",
                        "artifacts/scopes/organization/demo/architecture/Workflow-Architecture.md",
                        "Blazor Server app with workflow UI.",
                        "Approved architecture note.")
                ]));
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation retry",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "I added Razor files but only left a library project.",
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Artifact path verified.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Artifact contents reviewed."))),
                []),
            null,
            [],
            []);

        var directive = buildRecoveryDirective.Invoke(
            null,
            [
                candidate,
                detail,
                "I added Razor files but only left a library project.",
                new List<string> { "workspace_dotnet_build" },
                Array.Empty<ToolExecutionReceiptRecord>(),
                1
            ]) as string;

        Assert.NotNull(directive);
        Assert.Contains("Inspect the inherited durable artifacts directly on this retry", directive, StringComparison.Ordinal);
        Assert.Contains("artifacts/scopes/organization/demo/architecture/Workflow-Architecture.md", directive, StringComparison.Ordinal);
        Assert.Contains("runnable host/project", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRecoveryDirective_requires_process_step_outcome_for_governed_review_retry()
    {
        var buildRecoveryDirective = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildRecoveryDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRecoveryDirective method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the generated application.",
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA retry",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "The browser proof is ready, but the response forgot the structured step outcome.",
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Screenshot path verified.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("QA notes reviewed."))),
                []),
            null,
            [],
            []);

        var directive = buildRecoveryDirective.Invoke(
            null,
            [
                candidate,
                detail,
                "The browser proof is ready, but the response forgot the structured step outcome.",
                Array.Empty<string>(),
                Array.Empty<ToolExecutionReceiptRecord>(),
                2
            ]) as string;

        Assert.NotNull(directive);
        Assert.Contains("Do not conclude this governed retry without returning a valid structured ProcessStepOutcomeResult.", directive, StringComparison.Ordinal);
        Assert.Contains("Use the configured structured output format.", directive, StringComparison.Ordinal);
        Assert.DoesNotContain("PROCESS_STEP_OUTCOME", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRecoveryDirective_guides_browser_proof_retry_to_launch_grounded_host_and_capture_evidence()
    {
        var buildRecoveryDirective = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildRecoveryDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRecoveryDirective method was not found.");
        var candidate = CreateProjectStructureDispatchCandidate(
            "Run QA validation and browser proof for the workflow app.",
            new ProcessProjectStructureContext
            {
                ProjectId = Guid.NewGuid(),
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-main-application",
                ParentNodeTitle = "Create main application"
            },
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        const string responseText = "QA validation and browser proof cannot proceed because the application is not running and no screenshots can be captured.";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA retry",
                "process-step",
                "step-4",
                "corr-4",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            []);

        var directive = buildRecoveryDirective.Invoke(
            null,
            [
                candidate,
                detail,
                responseText,
                Array.Empty<string>(),
                Array.Empty<ToolExecutionReceiptRecord>(),
                1
            ]) as string;

        Assert.NotNull(directive);
        Assert.Contains("This retry is still the QA/browser-proof step.", directive, StringComparison.Ordinal);
        Assert.Contains("project_structure_read now, resolve the exact reviewed host", directive, StringComparison.Ordinal);
        Assert.Contains("Do not assume the app must be reachable at `http://localhost:5000/`", directive, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_run", directive, StringComparison.Ordinal);
        Assert.Contains("Do not write a one-off path-translation launch helper", directive, StringComparison.Ordinal);
        Assert.Contains("missing launch-tool access is a platform blocker", directive, StringComparison.Ordinal);
        Assert.Contains("fill or change representative controls", directive, StringComparison.Ordinal);
        Assert.Contains("routing, rendering, static-content, or client-interaction defect", directive, StringComparison.Ordinal);
        Assert.Contains("browser_take_screenshot", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRecoveryDirective_warns_about_stale_external_target_evidence_and_restates_current_grounding()
    {
        var buildRecoveryDirective = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildRecoveryDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRecoveryDirective method was not found.");
        var candidate = CreateProjectStructureDispatchCandidate(
            "Review architecture and canonical-model impact for the generated application.",
            new ProcessProjectStructureContext
            {
                ProjectId = Guid.NewGuid(),
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-main-application",
                ParentNodeTitle = "Create main application"
            },
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Architecture review completed.",
            summaryMarkdown:
                """
                Architecture review evidence:
                - Current product root: external-target/C/programovani/dotnet/ReadingTimeBudgeter
                - Stale sibling reference: external-target/C/programovani/dotnet/UnrelatedSample/Program.cs
                """);
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Architecture retry",
                "process-step",
                "step-2",
                "corr-2",
                "run-start",
                "process-automation-dispatch",
                "system",
                BuildAllowedExternalTargetMetadata("external-target/C/programovani/dotnet/ReadingTimeBudgeter"),
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateTextContent(responseText)])),
                []),
            null,
            [],
            []);

        var directive = buildRecoveryDirective.Invoke(
            null,
            [
                candidate,
                detail,
                responseText,
                Array.Empty<string>(),
                Array.Empty<ToolExecutionReceiptRecord>(),
                1
            ]) as string;

        Assert.NotNull(directive);
        Assert.Contains("Generated evidence referenced stale or ungrounded product paths", directive, StringComparison.Ordinal);
        Assert.Contains("Exact stale paths are omitted", directive, StringComparison.Ordinal);
        Assert.Contains("[stale external-target path omitted]", directive, StringComparison.Ordinal);
        Assert.DoesNotContain("external-target/C/programovani/dotnet/UnrelatedSample", directive, StringComparison.Ordinal);
        Assert.Contains("Current grounded external-target root(s):", directive, StringComparison.Ordinal);
        Assert.Contains("external-target/C/programovani/dotnet/ReadingTimeBudgeter", directive, StringComparison.Ordinal);
        Assert.Contains("Use only the current grounded product root and current-run artifacts", directive, StringComparison.Ordinal);
        Assert.Contains("project_structure_read now", directive, StringComparison.Ordinal);
        Assert.Contains("current grounded product root", directive, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildRecoveryDirective_requires_reusing_existing_scaffold_after_dotnet_new_overwrite_conflicts()
    {
        var buildRecoveryDirective = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildRecoveryDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRecoveryDirective method was not found.");
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the generated application and prove the build passes.",
            new ProcessProjectStructureContext
            {
                ProjectId = Guid.NewGuid(),
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-main-application",
                ParentNodeTitle = "Create main application"
            });
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation retry",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "The bootstrap failed because dotnet new reported an overwrite conflict.",
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Target directory inspected.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Existing project file reviewed."))),
                []),
            null,
            [],
            []);

        var directive = buildRecoveryDirective.Invoke(
            null,
            [
                candidate,
                detail,
                "The bootstrap failed because dotnet new reported an overwrite conflict.",
                Array.Empty<string>(),
                new[]
                {
                    new ToolExecutionReceiptRecord(
                        Id: Guid.NewGuid(),
                        ExecutionRunId: detail.Run.Id,
                        ToolFamily: "workspace-process",
                        ToolName: "workspace_dotnet_new",
                        RiskClass: "LocalExecution",
                        ApprovalMode: "NotRequired",
                        IsolationGuarantee: "Workspace-root-only process execution.",
                        RequestSummary: "new blazor -n GeneratedApp",
                        WorkingDirectory: ".",
                        ExitSummary: "Failed (exit 73)",
                        StartedAtUtc: now,
                        CompletedAtUtc: now)
                },
                1
            ]) as string;

        Assert.NotNull(directive);
        Assert.Contains("files already existed", directive, StringComparison.Ordinal);
        Assert.Contains("Reuse the existing scaffold", directive, StringComparison.Ordinal);
        Assert.Contains("validate that concrete project", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_fails_completed_branching_step_without_branch_outcome_selection()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateWithBranchOutcomes(
            "Review the implementation and route the next step honestly.",
            true,
            ("approved", "Approved", "Continue to QA."),
            ("changes_requested", "Changes requested", "Route back to implementation."));
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Review run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Code review passed."),
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Complete governed code review"]) as string;

        Assert.Equal(ProcessStepRunStatus.Failed, status);
        Assert.NotNull(reason);
        Assert.Contains("required branch outcome", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_completed_branching_step_with_valid_branch_outcome_selection()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var candidate = CreateDispatchCandidateWithBranchOutcomes(
            "Review the implementation and route the next step honestly.",
            true,
            ("approved", "Approved", "Continue to QA."),
            ("changes_requested", "Changes requested", "Route back to implementation."));
        var now = DateTimeOffset.UtcNow;
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Review run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                StructuredOutcome(
                    ProcessStepOutcomeStatus.Completed,
                    "Code review passed.",
                    "approved"),
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read complete."))),
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_process_mock_completed_step_with_required_artifact_projection()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var candidate = CreateDispatchCandidate(
            "Write application scope.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Scope acceptance criteria", true, "Scope artifact must describe requested behavior and acceptance criteria."));
        var detail = CreateProcessMockExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Application scope and acceptance criteria were written."),
            ProcessMockAgentRoleKeys.ProductOwner);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_process_mock_required_artifact_when_metadata_does_not_match_expectation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidate(
            "Write application scope.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Unrelated compliance packet", true, "Compliance packet must include unrelated governance metadata."));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Application scope and acceptance criteria were written.");
        var detail = CreateProcessMockExecutionDetail(responseText, ProcessMockAgentRoleKeys.ProductOwner);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, responseText]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("Unrelated compliance packet", reason, StringComparison.Ordinal);
        Assert.Contains("required artifacts", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_process_mock_implementation_when_rollout_checklist_is_missing()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Implementation change set was written.",
            summaryMarkdown: """
            ## Implementation change set
            Touched surface inventory: ValidationEngine owns the sample validation behavior.
            Tests and validation: deterministic process mock validation covers the implementation lane and links the change set to test proof.
            Migration notes: no schema or data migration is introduced by the implementation.
            """);
        var detail = CreateProcessMockExecutionDetail(
            responseText,
            ProcessMockAgentRoleKeys.Developer,
            artifacts:
            [
                (
                    "artifacts/process-mock/mockrun001/03-implementation-change-set.md",
                    "implementation change set tests migration notes touched surface inventory")
            ]);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, responseText]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("Migration and rollout preparation checklist", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_process_mock_implementation_with_db_free_rollout_checklist()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Implementation and DB-free rollout checklist were written.",
            summaryMarkdown: """
            ## Implementation change set
            Touched surface inventory: ValidationEngine owns name normalization and blank-input validation behavior.
            Tests and validation: deterministic process mock validation covers the implementation lane and links the change set to test proof.
            Migration notes: no schema, persistent data, or backfill changes are part of this implementation.

            ## Migration and rollout preparation checklist
            Data changes: no data migration required; no schema migration, seed update, backfill, or data rollback is needed.
            Operational preconditions: implementation validation must pass and QA must verify name normalization plus blank-input behavior.
            Rollback steps: revert the implementation change set or restore the previous project state; no data rollback is required.
            """);
        var detail = CreateProcessMockExecutionDetail(
            responseText,
            ProcessMockAgentRoleKeys.Developer,
            artifacts:
            [
                (
                    "artifacts/process-mock/mockrun001/03-implementation-change-set.md",
                    "implementation change set tests migration notes touched surface inventory"),
                (
                    "artifacts/process-mock/mockrun001/03-migration-rollout-preparation-checklist.md",
                    "migration rollout preparation checklist data changes operational preconditions rollback steps no data migration required")
            ]);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, responseText]) as string;

        Assert.True(status == ProcessStepRunStatus.Completed, reason);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_does_not_retry_downstream_step_for_missing_upstream_artifact_block()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidateWithArtifactInputs(
            "Implement the workflow as a Blazor app and prove the build passes.",
            (
                "Write workflow architecture",
                "Workflow architecture artifact",
                []));
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Write workflow architecture must provide Workflow architecture artifact before implementation can proceed.",
            summaryMarkdown: """
            Upstream artifact is missing.
            """);
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation blocked by upstream artifact",
                "process-step",
                "step-implementation",
                "corr-implementation",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            []);

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, new[] { "workspace_dotnet_build", "workspace_dotnet_test" }, 1, 5]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.False((bool)shouldRetryResult);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_process_mock_qa_rejection_branch_and_artifact()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var resolveSelectedBranchOutcomeId = ResolveSelectedBranchOutcomeIdMethod(serviceType);
        var candidate = CreateDispatchCandidateCore(
            "Review the first generated application implementation and route the next step.",
            ProcessStepKind.Review,
            [
                (ProcessMockAgentCatalog.BranchRepairsRequired, "Repairs required", "Route the implementation through defect repair."),
                (ProcessMockAgentCatalog.BranchApproved, "Approved", "Route directly to release notes when no repair is required.")
            ],
            true,
            [(ProcessArtifactKind.Evidence, "QA rejection finding", true, "QA first review artifact must record the branch reason.")],
            []);
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Divide-by-zero handling is missing; repair is required.",
            ProcessMockAgentCatalog.BranchRepairsRequired,
            summaryMarkdown: "QA rejection.");
        var detail = CreateProcessMockExecutionDetail(
            responseText,
            ProcessMockAgentRoleKeys.Qa,
            branchOutcomeKey: ProcessMockAgentCatalog.BranchRepairsRequired);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var selectedBranchOutcomeId = (Guid?)resolveSelectedBranchOutcomeId.Invoke(null, [candidate, ProcessStepRunStatus.Completed, responseText]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, responseText]) as string;

        Assert.True(status == ProcessStepRunStatus.Completed, reason);
        Assert.Equal(ResolveBranchOutcomeId(candidate, ProcessMockAgentCatalog.BranchRepairsRequired), selectedBranchOutcomeId);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_process_mock_qa_approval_branch_and_artifact()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var resolveSelectedBranchOutcomeId = ResolveSelectedBranchOutcomeIdMethod(serviceType);
        var candidate = CreateDispatchCandidateCore(
            "Recheck the repaired generated application implementation and approve the release path.",
            ProcessStepKind.Review,
            [(ProcessMockAgentCatalog.BranchApproved, "Approved", "Route repaired implementation to release notes.")],
            true,
            [(ProcessArtifactKind.Evidence, "QA approval evidence", true, "QA recheck artifact must record approval for release.")],
            []);
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Repaired workflow implementation passed QA.",
            ProcessMockAgentCatalog.BranchApproved,
            summaryMarkdown: "QA approval.");
        var detail = CreateProcessMockExecutionDetail(
            responseText,
            ProcessMockAgentRoleKeys.Qa,
            branchOutcomeKey: ProcessMockAgentCatalog.BranchApproved);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var selectedBranchOutcomeId = (Guid?)resolveSelectedBranchOutcomeId.Invoke(null, [candidate, ProcessStepRunStatus.Completed, responseText]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, responseText]) as string;

        Assert.True(status == ProcessStepRunStatus.Completed, reason);
        Assert.Equal(ResolveBranchOutcomeId(candidate, ProcessMockAgentCatalog.BranchApproved), selectedBranchOutcomeId);
    }

    [Fact]
    public void BuildMissingTechnicalAgentBindingDiagnostic_includes_actionable_state()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildDiagnostic = serviceType.GetMethod("BuildMissingTechnicalAgentBindingDiagnostic", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildMissingTechnicalAgentBindingDiagnostic method was not found.");
        var processRunId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var stepRunId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var partyId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var diagnostic = buildDiagnostic.Invoke(
            null,
            [processRunId, stepRunId, "Review first workflow implementation", partyId, AiResourceBindingStatus.Unbound, null]) as string;

        Assert.NotNull(diagnostic);
        Assert.Contains("Review first workflow implementation", diagnostic, StringComparison.Ordinal);
        Assert.Contains(processRunId.ToString("D"), diagnostic, StringComparison.Ordinal);
        Assert.Contains(stepRunId.ToString("D"), diagnostic, StringComparison.Ordinal);
        Assert.Contains(partyId.ToString("D"), diagnostic, StringComparison.Ordinal);
        Assert.Contains(AiResourceBindingStatus.Unbound.ToString(), diagnostic, StringComparison.Ordinal);
        Assert.Contains("technical agent ID: none", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_provider_failure_that_returned_no_text()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate("Implement the generated application and prove the build passes.");

        var now = DateTimeOffset.UtcNow;
        const string responseText = "The provider completed without returning text.";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateAssistantErrorContent("insufficient_quota", "You exceeded your current quota.")])
                ),
                []),
            null,
            [],
            []);

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, Array.Empty<string>(), 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryRecoverableFailedRun_returns_true_for_missing_governed_outcome_after_finalizer_validation_failure()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryRecoverableFailedRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryRecoverableFailedRun method was not found.");
        var candidate = CreateDispatchCandidate("Implement the generated application and prove build and tests pass.");

        var now = DateTimeOffset.UtcNow;
        const string responseText = "The implementation was updated and tests passed, but the governed finalizer was not emitted.";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Failed,
                RunOutcome.Failed,
                now,
                now,
                now,
                now,
                "Finalizer tool 'submit_process_step_outcome' in Required mode failed validation.",
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateTextContent(responseText)])
                ),
                []),
            null,
            [],
            []);

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [
                candidate,
                detail,
                responseText,
                Array.Empty<string>(),
                Array.Empty<ToolExecutionReceiptRecord>(),
                1,
                5
            ]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryRecoverableFailedRun_returns_true_for_provider_transport_failure_on_non_implementation_step()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryRecoverableFailedRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryRecoverableFailedRun method was not found.");
        var candidate = CreateDispatchCandidate(
            "Review architecture and canonical model impact.",
            ProcessStepKind.Work,
            (ProcessArtifactKind.Decision, "Architecture decision record", true, "Must summarize canonical-model impact."));

        var now = DateTimeOffset.UtcNow;
        const string responseText = "The response ended prematurely. (ResponseEnded)";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Architecture run",
                "process-step",
                "step-2",
                "corr-2",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Failed,
                RunOutcome.Failed,
                now,
                now,
                now,
                now,
                responseText,
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateTextContent(responseText)])
                ),
                []),
            null,
            [],
            []);

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [
                candidate,
                detail,
                responseText,
                Array.Empty<string>(),
                Array.Empty<ToolExecutionReceiptRecord>(),
                1,
                3
            ]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryRecoverableFailedRun_returns_true_for_host_restart_interruption_on_non_implementation_step()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryRecoverableFailedRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryRecoverableFailedRun method was not found.");
        var candidate = CreateDispatchCandidate(
            "Clarify scope and release boundary.",
            ProcessStepKind.Work,
            (ProcessArtifactKind.Decision, "Release boundary decision", true, "Must define concrete scope."));

        var now = DateTimeOffset.UtcNow;
        const string responseText = "Execution interrupted because the CanDoItAll host restarted before the run completed.";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Scope run",
                "process-step",
                "step-0",
                "corr-3",
                "runtime-recovery-scan",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Failed,
                RunOutcome.Cancelled,
                now,
                now,
                now,
                now,
                responseText,
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateTextContent(responseText)])
                ),
                []),
            null,
            [],
            []);

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [
                candidate,
                detail,
                responseText,
                Array.Empty<string>(),
                Array.Empty<ToolExecutionReceiptRecord>(),
                1,
                3
            ]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ResolveCompletionStatus_returns_failed_for_non_governed_step_when_provider_failure_is_detected()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var candidate = CreateDispatchCandidate("Implement the generated application and prove the build passes.");

        var now = DateTimeOffset.UtcNow;
        const string responseText = "The provider completed without returning text.";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateAssistantErrorContent("insufficient_quota", "You exceeded your current quota.")])
                ),
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

        Assert.Equal(ProcessStepRunStatus.Failed, status);
    }

    [Fact]
    public void TryResolveRecoverableProviderFailure_ignores_prompt_mentions_in_user_messages()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var tryResolveProviderFailure = serviceType.GetMethod("TryResolveRecoverableProviderFailure", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveRecoverableProviderFailure method was not found.");

        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Reviewed successfully.",
            summaryMarkdown: "Architecture review complete.");
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Architecture review run",
                "process-step",
                "step-2",
                "corr-2",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionStateWithMessages(
                    ("user", [CreateTextContent("Upstream evidence excerpt: The provider completed without returning text.")]),
                    ("assistant", [CreateTextContent(responseText)])
                ),
                []),
            null,
            [],
            []);

        object?[] arguments = [detail, responseText, null];

        var detected = tryResolveProviderFailure.Invoke(null, arguments);

        Assert.IsType<bool>(detected);
        Assert.False((bool)detected);
        Assert.Equal(string.Empty, arguments[2]);
    }

    [Fact]
    public void TryResolveRecoverableProviderFailure_ignores_stale_provider_errors_when_structured_outcome_exists()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var tryResolveProviderFailure = serviceType.GetMethod("TryResolveRecoverableProviderFailure", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveRecoverableProviderFailure method was not found.");

        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "QA blocked progression because the running app returned HTTP 500 during browser proof.",
            summaryMarkdown: "Browser evidence exists, but the target is returning HTTP 500 and needs repair.");
        const string staleProviderError = "The prompt was saved to the thread, but the run failed: Response status code does not indicate success: 500 (Internal Server Error).";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA run",
                "process-step",
                "step-4",
                "corr-4",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                staleProviderError,
                "Remote Ollama",
                "gptoss32k:latest",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateTextContent(staleProviderError)]),
                    ("assistant", [CreateTextContent(responseText)])
                ),
                []),
            null,
            [],
            []);

        object?[] arguments = [detail, responseText, null];

        var detected = tryResolveProviderFailure.Invoke(null, arguments);

        Assert.IsType<bool>(detected);
        Assert.False((bool)detected);
        Assert.Equal(string.Empty, arguments[2]);
    }

    [Fact]
    public void TryResolveRecoverableProviderFailure_detects_missing_provider_credentials()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var tryResolveProviderFailure = serviceType.GetMethod("TryResolveRecoverableProviderFailure", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveRecoverableProviderFailure method was not found.");

        var now = DateTimeOffset.UtcNow;
        const string responseText = "Environment variable 'OPENAI_API_KEY' is not set.";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA run",
                "process-step",
                "step-1",
                "corr-3",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateTextContent(responseText)])
                ),
                []),
            null,
            [],
            []);

        object?[] arguments = [detail, responseText, null];

        var detected = tryResolveProviderFailure.Invoke(null, arguments);

        Assert.IsType<bool>(detected);
        Assert.True((bool)detected);
        Assert.Equal(
            "The assigned provider did not have usable credentials in the current environment.",
            arguments[2]);
    }

    [Fact]
    public void TryResolveRecoverableProviderFailure_detects_provider_server_errors()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var tryResolveProviderFailure = serviceType.GetMethod("TryResolveRecoverableProviderFailure", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveRecoverableProviderFailure method was not found.");

        var now = DateTimeOffset.UtcNow;
        const string responseText = "The prompt was saved to the thread, but the run failed: Response status code does not indicate success: 500 (Internal Server Error).";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Architecture run",
                "process-step",
                "step-2",
                "corr-4",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "Remote Ollama",
                "gptoss32k:latest",
                ExecutionState.Failed,
                RunOutcome.Failed,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateTextContent(responseText)])
                ),
                []),
            null,
            [],
            []);

        object?[] arguments = [detail, responseText, null];

        var detected = tryResolveProviderFailure.Invoke(null, arguments);

        Assert.IsType<bool>(detected);
        Assert.True((bool)detected);
        Assert.Equal(
            "The assigned provider returned an upstream server error before the agent produced a usable response.",
            arguments[2]);
    }

    [Fact]
    public void TryResolveRecoverableProviderFailure_detects_response_ended_transport_errors()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var tryResolveProviderFailure = serviceType.GetMethod("TryResolveRecoverableProviderFailure", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveRecoverableProviderFailure method was not found.");

        var now = DateTimeOffset.UtcNow;
        const string responseText = "The response ended prematurely. (ResponseEnded)";
        var detail = new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Architecture run",
                "process-step",
                "step-2",
                "corr-5",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-4.1",
                ExecutionState.Failed,
                RunOutcome.Failed,
                now,
                now,
                now,
                now,
                responseText,
                BuildSerializedSessionStateWithMessages(
                    ("assistant", [CreateTextContent(responseText)])
                ),
                []),
            null,
            [],
            []);

        object?[] arguments = [detail, responseText, null];

        var detected = tryResolveProviderFailure.Invoke(null, arguments);

        Assert.IsType<bool>(detected);
        Assert.True((bool)detected);
        Assert.Equal(
            "The assigned provider response ended before the agent produced a usable response.",
            arguments[2]);
    }

    private static object CreateWorkflowImplementationDispatchCandidate()
    {
        return CreateDispatchCandidate(
            "Implement feature, tests, and migration notes for the workflow app.",
            ProcessStepKind.Work,
            (ProcessArtifactKind.Deliverable, "Implementation change set", true, "Must be linked to tests, migration notes, and touched-surface inventory."),
            (ProcessArtifactKind.Checklist, "Migration and rollout preparation checklist", true, "Must name data changes, operational preconditions, and rollback steps."));
    }

    private static object CreateDispatchCandidateWithStepTitle(
        string stepTitle,
        string workBriefText,
        ProcessStepKind stepKind = ProcessStepKind.Work,
        params (ProcessArtifactKind ArtifactKind, string Title, bool IsRequired, string ValidationRequirementSummary)[] expectedArtifactDefinitions)
    {
        return CreateDispatchCandidateCore(
            workBriefText,
            stepKind,
            [],
            false,
            expectedArtifactDefinitions,
            [],
            "Deliver the workflow showcase.",
            stepTitle);
    }

    private static object CreateDispatchCandidate(
        string workBriefText,
        ProcessStepKind stepKind = ProcessStepKind.Work,
        params (ProcessArtifactKind ArtifactKind, string Title, bool IsRequired, string ValidationRequirementSummary)[] expectedArtifactDefinitions)
    {
        return CreateDispatchCandidateCore(
            workBriefText,
            stepKind,
            [],
            false,
            expectedArtifactDefinitions,
            []);
    }

    private static object CreateProjectStructureDispatchCandidate(
        string workBriefText,
        ProcessProjectStructureContext projectStructureContext,
        ProcessStepKind stepKind = ProcessStepKind.Work)
    {
        return CreateDispatchCandidateCore(
            workBriefText,
            stepKind,
            [],
            false,
            [],
            [],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver the generated application showcase.",
                projectStructureContext));
    }

    private static object CreateProjectStructureGroundingNode(
        Type groundingNodeType,
        string id,
        string parentId,
        string objectType,
        string objectSubtype,
        string title,
        string subtitle,
        string status,
        string notes,
        string metadataJson)
    {
        return Activator.CreateInstance(
                   groundingNodeType,
                   id,
                   parentId,
                   objectType,
                   objectSubtype,
                   title,
                   subtitle,
                   status,
                   notes,
                   metadataJson)
               ?? throw new InvalidOperationException("ProjectStructureGroundingNodeData could not be constructed.");
    }

    private static object CreateDispatchCandidateWithBranchOutcomes(
        string workBriefText,
        bool requiresExplicitBranchOutcomeSelection,
        params (string Key, string Title, string Description)[] branchOutcomeDefinitions)
    {
        return CreateDispatchCandidateCore(
            workBriefText,
            ProcessStepKind.Review,
            branchOutcomeDefinitions,
            requiresExplicitBranchOutcomeSelection,
            [],
            []);
    }

    private static object CreateDispatchCandidateWithArtifactInputs(
        string workBriefText,
        params (string SourceStepTitle, string ExpectedArtifactTitle, (string Title, string ArtifactKind, string ManagedStoragePath, string ReviewSummary, string ProvenanceSummary)[] Artifacts)[] artifactInputDefinitions)
    {
        return CreateDispatchCandidateCore(
            workBriefText,
            ProcessStepKind.Work,
            [],
            false,
            [],
            artifactInputDefinitions);
    }

    private static object CreateDispatchCandidateCore(
        string workBriefText,
        ProcessStepKind stepKind,
        (string Key, string Title, string Description)[] branchOutcomeDefinitions,
        bool requiresExplicitBranchOutcomeSelection,
        (ProcessArtifactKind ArtifactKind, string Title, bool IsRequired, string ValidationRequirementSummary)[] expectedArtifactDefinitions,
        (string SourceStepTitle, string ExpectedArtifactTitle, (string Title, string ArtifactKind, string ManagedStoragePath, string ReviewSummary, string ProvenanceSummary)[] Artifacts)[] artifactInputDefinitions,
        string triggerReason = "Deliver the implementation showcase.",
        string stepTitle = "Implement feature")
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var candidateType = serviceType.GetNestedType("DispatchCandidate", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchCandidate type was not found.");
        var artifactExpectationType = serviceType.GetNestedType("DispatchArtifactExpectation", BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("DispatchArtifactExpectation type was not found.");
        var artifactInputType = serviceType.GetNestedType("DispatchArtifactInput", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchArtifactInput type was not found.");
        var artifactReferenceType = serviceType.GetNestedType("DispatchArtifactReference", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchArtifactReference type was not found.");
        var branchOutcomeType = serviceType.GetNestedType("DispatchBranchOutcome", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchBranchOutcome type was not found.");
        var expectedArtifacts = Array.CreateInstance(artifactExpectationType, expectedArtifactDefinitions.Length);
        var artifactInputs = Array.CreateInstance(artifactInputType, artifactInputDefinitions.Length);
        var branchOutcomes = Array.CreateInstance(branchOutcomeType, branchOutcomeDefinitions.Length);
        for (var index = 0; index < expectedArtifactDefinitions.Length; index++)
        {
            var definition = expectedArtifactDefinitions[index];
            var expectedArtifact = Activator.CreateInstance(
                artifactExpectationType,
                Guid.NewGuid(),
                definition.ArtifactKind,
                definition.Title,
                definition.IsRequired,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                definition.ValidationRequirementSummary,
                string.Empty)
                ?? throw new InvalidOperationException("DispatchArtifactExpectation could not be constructed.");
            expectedArtifacts.SetValue(expectedArtifact, index);
        }

        for (var index = 0; index < artifactInputDefinitions.Length; index++)
        {
            var definition = artifactInputDefinitions[index];
            var artifactReferences = Array.CreateInstance(artifactReferenceType, definition.Artifacts.Length);
            for (var artifactIndex = 0; artifactIndex < definition.Artifacts.Length; artifactIndex++)
            {
                var artifactDefinition = definition.Artifacts[artifactIndex];
                var artifactReference = Activator.CreateInstance(
                    artifactReferenceType,
                    artifactDefinition.Title,
                    artifactDefinition.ArtifactKind,
                    artifactDefinition.ManagedStoragePath,
                    artifactDefinition.ReviewSummary,
                    artifactDefinition.ProvenanceSummary)
                    ?? throw new InvalidOperationException("DispatchArtifactReference could not be constructed.");
                artifactReferences.SetValue(artifactReference, artifactIndex);
            }

            var artifactInput = Activator.CreateInstance(
                artifactInputType,
                definition.SourceStepTitle,
                definition.ExpectedArtifactTitle,
                artifactReferences)
                ?? throw new InvalidOperationException("DispatchArtifactInput could not be constructed.");
            artifactInputs.SetValue(artifactInput, index);
        }

        for (var index = 0; index < branchOutcomeDefinitions.Length; index++)
        {
            var definition = branchOutcomeDefinitions[index];
            var branchOutcome = Activator.CreateInstance(
                branchOutcomeType,
                Guid.NewGuid(),
                definition.Key,
                definition.Title,
                definition.Description)
                ?? throw new InvalidOperationException("DispatchBranchOutcome could not be constructed.");
            branchOutcomes.SetValue(branchOutcome, index);
        }

        var constructor = candidateType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Single(candidateConstructor => candidateConstructor.GetParameters().Length == 13);
        return constructor.Invoke(
                    [
                        new ProcessRun
                        {
                            Name = "Showcase run",
                            TriggerReason = triggerReason
                        },
                       new ProcessDefinition
                       {
                           Name = "Software delivery"
                       },
                       new ProcessStepRun
                       {
                           Title = stepTitle,
                           CurrentExecutorName = "Showcase Lead Engineer",
                           StepKind = stepKind
                       },
                       new ProcessWorkBrief
                       {
                           WorkBriefText = workBriefText,
                           HandoffSummary = "Architecture decision record.",
                           ExpectedOutcome = "Buildable implementation.",
                           EvidenceExpectationSummary = expectedArtifactDefinitions.Length == 0
                               ? "Implementation change set"
                               : string.Join(", ", expectedArtifactDefinitions.Select(item => item.Title))
                       },
                       Guid.NewGuid(),
                       expectedArtifacts,
                       artifactInputs,
                       new HashSet<string>(StringComparer.Ordinal),
                       null,
                       null,
                       string.Empty,
                       branchOutcomes,
                       requiresExplicitBranchOutcomeSelection
                   ])
               ?? throw new InvalidOperationException("DispatchCandidate could not be constructed.");
    }

    private static MethodInfo ResolveSelectedBranchOutcomeIdMethod(Type serviceType)
    {
        return serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method =>
                method.Name == "ResolveSelectedBranchOutcomeId" &&
                method.GetParameters().Length == 3);
    }

    private static Guid ResolveBranchOutcomeId(object candidate, string branchOutcomeKey)
    {
        var branchOutcomes = candidate.GetType().GetProperty("BranchOutcomes")
            ?.GetValue(candidate) as System.Collections.IEnumerable
            ?? throw new InvalidOperationException("DispatchCandidate.BranchOutcomes was not available.");
        foreach (var branchOutcome in branchOutcomes)
        {
            if (branchOutcome is null)
            {
                continue;
            }

            var key = branchOutcome.GetType().GetProperty("Key")?.GetValue(branchOutcome) as string;
            if (!string.Equals(key, branchOutcomeKey, StringComparison.Ordinal))
            {
                continue;
            }

            return (Guid)(branchOutcome.GetType().GetProperty("Id")?.GetValue(branchOutcome)
                ?? throw new InvalidOperationException("DispatchBranchOutcome.Id was not available."));
        }

        throw new InvalidOperationException($"Branch outcome '{branchOutcomeKey}' was not found.");
    }

    private static ExecutionRunDetail CreateProcessMockExecutionDetail(
        string responseText,
        string roleKey,
        string artifactRoot = "artifacts/process-mock/mockrun001",
        string? branchOutcomeKey = null,
        params (string RelativePath, string ContentSignalText)[] artifacts)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunDetail(
            new ExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Process mock step run",
                ProcessMockAgentCatalog.ProcessSourceKind,
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                ProcessMockAgentCatalog.ProviderName,
                ProcessMockAgentCatalog.Model,
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                JsonSerializer.Serialize(
                    new
                    {
                        processMockAgent = true,
                        roleKey,
                        runKey = "mockrun001",
                        artifactRoot,
                        branchOutcomeKey,
                        artifacts = artifacts.Select(artifact => new
                        {
                            artifact.RelativePath,
                            artifact.ContentSignalText
                        }).ToArray()
                    }),
                []),
            null,
            [],
            []);
    }

    private static ExecutionRunRecord CreateExecutionRun(string requestedBy, ExecutionState state, RunOutcome? outcome)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Implementation step run",
            "process-step",
            "step-1",
            "corr-1",
            "cause-1",
            requestedBy,
            "system",
            "{}",
            "Prompt",
            "Result",
            "OpenAI chat completions",
            "gpt-4.1-mini",
            state,
            outcome,
            now,
            now,
            now,
            state is ExecutionState.Completed or ExecutionState.Failed ? now : null,
            string.Empty,
            null,
            []);
    }

    private static ProcessStepRun CreateStepRun(ProcessStepRunStatus status, DateTimeOffset? startedAtUtc)
    {
        return new ProcessStepRun
        {
            Status = status,
            StartedAtUtc = startedAtUtc
        };
    }

    private static string BuildSerializedSessionState(params (string ToolName, object Result)[] toolCalls)
    {
        return BuildSerializedSessionState(toolCalls.Select(toolCall =>
            (toolCall.ToolName, (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), toolCall.Result)).ToArray());
    }

    private static string BuildSerializedSessionStateWithMessages(
        params (string Role, object[] Contents)[] messages)
    {
        return JsonSerializer.Serialize(
            new
            {
                stateBag = new
                {
                    InMemoryChatHistoryProvider = new
                    {
                        messages = messages.Select(message => new
                        {
                            role = message.Role,
                            contents = message.Contents
                        }).ToArray()
                    }
                }
            });
    }

    private static string BuildSerializedSessionState(params (string ToolName, IReadOnlyDictionary<string, object?> Arguments, object Result)[] toolCalls)
    {
        var callContents = toolCalls
            .Select((toolCall, index) => new
            {
                Content = new Dictionary<string, object?>
                {
                    ["$type"] = "functionCall",
                    ["callId"] = $"call-{index + 1}",
                    ["name"] = toolCall.ToolName,
                    ["arguments"] = toolCall.Arguments
                }
            }.Content)
            .ToArray();
        var resultContents = toolCalls
            .Select((toolCall, index) => new
            {
                Content = new Dictionary<string, object?>
                {
                    ["$type"] = "functionResult",
                    ["callId"] = $"call-{index + 1}",
                    ["result"] = toolCall.Result
                }
            }.Content)
            .ToArray();

        return JsonSerializer.Serialize(
            new
            {
                stateBag = new
                {
                    InMemoryChatHistoryProvider = new
                    {
                        messages = new object[]
                        {
                            new
                            {
                                role = "assistant",
                                contents = callContents
                            },
                            new
                            {
                                role = "tool",
                                contents = resultContents
                            }
                        }
                    }
                }
            });
    }

    private static object CreateProviderNativeTextResult(string text)
    {
        return new Dictionary<string, string>
        {
            ["$type"] = "text",
            ["text"] = text
        };
    }

    private static object CreateTextContent(string text)
    {
        return new Dictionary<string, string>
        {
            ["$type"] = "text",
            ["text"] = text
        };
    }

    private static string BuildAllowedExternalTargetMetadata(params string[] allowedExternalTargetAliases)
    {
        return JsonSerializer.Serialize(
            new Dictionary<string, string[]>
            {
                [ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey] = allowedExternalTargetAliases
            },
            AgentOutputJson.SerializerOptions);
    }

    private static ToolExecutionReceiptRecord CreateToolReceipt(
        string toolFamily,
        string toolName,
        string requestSummary,
        string workingDirectory,
        string exitSummary,
        DateTimeOffset timestamp)
    {
        return new ToolExecutionReceiptRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            toolFamily,
            toolName,
            "Validation",
            "Required",
            "Test",
            requestSummary,
            workingDirectory,
            exitSummary,
            timestamp,
            timestamp);
    }

    private static string ToExternalTargetAlias(string path)
    {
        var normalized = Path.GetFullPath(path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
        if (normalized.Length < 3 || normalized[1] != ':' || normalized[2] != '/')
        {
            throw new InvalidOperationException($"Path '{path}' cannot be mapped to an external-target alias.");
        }

        var suffix = normalized[3..].Trim('/');
        return string.IsNullOrWhiteSpace(suffix)
            ? $"external-target/{char.ToUpperInvariant(normalized[0])}"
            : $"external-target/{char.ToUpperInvariant(normalized[0])}/{suffix}";
    }

    private static object CreateAssistantErrorContent(string errorCode, string message)
    {
        return new Dictionary<string, string>
        {
            ["$type"] = "error",
            ["errorCode"] = errorCode,
            ["message"] = message
        };
    }

    private static string StructuredOutcome(
        ProcessStepOutcomeStatus status,
        string reason,
        string? branchOutcomeKey = null,
        string? summaryMarkdown = null,
        IReadOnlyList<string>? evidenceRefs = null)
    {
        var outcome = new ProcessStepOutcomeResult
        {
            Status = status,
            Reason = reason,
            BranchOutcomeKey = branchOutcomeKey ?? string.Empty,
            EvidenceRefs = evidenceRefs ?? ["execution://test-run"],
            NextActions = status == ProcessStepOutcomeStatus.Completed
                ? []
                : ["Resolve the reported issue."],
            HumanReadableSummaryMarkdown = summaryMarkdown ?? reason
        };
        return JsonSerializer.Serialize(outcome, AgentOutputJson.SerializerOptions);
    }

    private static MethodInfo ResolveTryResolveDeclaredStepOutcomeMethod()
    {
        return typeof(ProcessRunAutomationDispatchService)
                   .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                   .Single(method =>
                   {
                       if (!string.Equals(method.Name, "TryResolveDeclaredStepOutcome", StringComparison.Ordinal))
                       {
                           return false;
                       }

                       var parameters = method.GetParameters();
                       return parameters.Length == 2 &&
                              parameters[0].ParameterType == typeof(string) &&
                              parameters[1].ParameterType.IsByRef;
                   })
               ?? throw new InvalidOperationException("TryResolveDeclaredStepOutcome method was not found.");
    }
}
