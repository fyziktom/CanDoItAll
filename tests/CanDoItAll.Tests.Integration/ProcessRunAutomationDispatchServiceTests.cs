using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
    public void ResolveRecoverableAutomationExecutionRunId_ignores_cancelled_restart_recovery_runs()
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

        Assert.Equal(previousCompletedRun.Id, recoverableRunId);
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
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-calculator/evidence/process/feature-intake/scope-boundary-packet.md",
            "text/markdown",
            "workspace",
            "Durable scope packet",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

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
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-calculator/logs/stdout.log",
            "text/plain",
            "workspace",
            "Command output",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

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
                "Calculator proof",
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
            "showcases/blazor-ssr-calculator/.playwright-mcp/qa-validation/calculator-proof.png",
            "image/png",
            "workspace",
            "calculator-proof.png",
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
                "Calculator proof",
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
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-proof.png",
            "image/png",
            "workspace",
            "calculator-proof.png",
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
                "Create this artifact at artifacts/showcases/blazor-ssr-calculator/evidence/process/implementation/implementation-change-set.md using workspace create/write file tools.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/showcases/blazor-ssr-calculator/evidence/process/implementation/implementation-change-set/implementation-change-set.md",
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
                "Calculator app project",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Create this artifact at showcases/blazor-ssr-calculator/app/SimpleCalculatorApp/SimpleCalculatorApp.csproj using workspace create/write file tools.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "showcases/blazor-ssr-calculator/app/SimpleCalculatorApp/SimpleCalculatorApp.csproj",
            "text/xml",
            "workspace",
            "SimpleCalculatorApp.csproj",
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
                "Calculator proof",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "The durable screenshot must exist at artifacts/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-proof.png.",
                string.Empty)
        };
        var artifact = new ExecutionArtifactRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            string.Empty,
            "artifacts/scopes/organization/demo/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-proof.png",
            "image/png",
            "workspace",
            "calculator-proof.png",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
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
            "Use status Blocked when unresolved defects, missing proof, rejected approval, or required remediation mean the next step must not proceed yet.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not stop after inspection, reconnaissance, bootstrap confirmation, or a next-steps summary if required tools, concrete deliverables, or required artifacts are still missing.",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_explicitly_treats_missing_scaffold_targets_as_bootstrap_work()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate(
            "Implement the units converter.\nInstructions: Use workspace_pwsh_run_script to run Bootstrap-UnitsConverterSolution.ps1 before substantial edits.");

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
            [candidate, null, "Ancestor path to the target work node:\n- Calculator request (root-request)", null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Live project structure grounding:", prompt, StringComparison.Ordinal);
        Assert.Contains("Calculator request (root-request)", prompt, StringComparison.Ordinal);
        Assert.Contains(
            "The dispatcher already fetched a live project-structure snapshot for this selected branch",
            prompt,
            StringComparison.Ordinal);
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
                            "Captured the calculator scope and boundary.",
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
    public void BuildProjectStructureGroundingSummary_includes_descendant_requirement_nodes_from_sibling_planning_blocks()
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
            ProjectName = "Calculator",
            Nodes = new object[]
            {
                new
                {
                    Id = $"project:{projectId:D}",
                    ParentId = string.Empty,
                    ObjectType = "ProjectRoot",
                    ObjectSubtype = string.Empty,
                    Title = "Calculator",
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
                    Title = @"output must be placed in C:\programovani\csharp\calculator",
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
                    Title = "buttons for +,-,/,*,=",
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
                    Title = "calculations history list",
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
                }
            }
        };

        var summary = buildProjectStructureGroundingSummary.Invoke(null, [surface, context]) as string;

        Assert.NotNull(summary);
        Assert.Contains("Descendant requirement context from sibling planning nodes:", summary, StringComparison.Ordinal);
        Assert.Contains(@"output must be placed in C:\programovani\csharp\calculator", summary, StringComparison.Ordinal);
        Assert.Contains("Blazor SSR", summary, StringComparison.Ordinal);
        Assert.Contains("buttons for +,-,/,*,=", summary, StringComparison.Ordinal);
        Assert.Contains("calculations history list", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("Previous run", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_requires_blocked_outcome_when_browser_proof_cannot_be_captured()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the calculator app.",
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
        Assert.Contains("return `Blocked` instead of `Completed`", prompt, StringComparison.Ordinal);
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
            "Implement the calculator and prove the build passes.",
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
        var candidate = CreateCalculatorImplementationDispatchCandidate();

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
            "Implement the calculator as a Blazor app and prove the build passes.",
            (
                "Write calculator architecture",
                "Calculator architecture artifact",
                []));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Upstream artifact gate:", prompt, StringComparison.Ordinal);
        Assert.Contains("Write calculator architecture", prompt, StringComparison.Ordinal);
        Assert.Contains("Calculator architecture artifact", prompt, StringComparison.Ordinal);
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
            "Implement the units converter.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude.");
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
                    "showcases/blazor-ssr-calculator/app/SimpleCalculatorApp/Program.cs",
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
            "Implement the units converter.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude.");
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "Apply-CalculatorShowcaseApp.ps1",
                    "showcases/blazor-ssr-calculator",
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
                    "SimpleCalculatorApp.csproj",
                    "showcases/blazor-ssr-calculator/app/SimpleCalculatorApp",
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
            "Implement the units converter.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude.");
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "deliveries/units-converter/src",
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
Selected option: build the calculator as a single Blazor app under the grounded project output.
Rejected options: a console-only tool and a split service/UI package were rejected because they add unnecessary seams.
Source-of-truth choice: calculator state remains in the UI host with no extra persistence layer.
Migration ownership: the programming workspace analyst owns the bootstrap and follow-up implementation.

## Project structure context brief
Originating project-structure node: Create main application.
Resolved working directory: external-target/C/programovani/csharp/calculator.
Touched modules or routes: calculator shell, keypad interactions, result display, and history surface.
Dependency boundaries: keep the calculator self-contained and avoid billing/process module coupling.
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
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_completed_run_that_only_missed_required_tools()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate("Implement the units converter and prove the build passes.");

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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "deliveries/units-converter/src",
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
        var candidate = CreateDispatchCandidate("Implement the units converter and prove the build passes.");

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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "build deliveries/units-converter/Units.slnx -c Debug",
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_completed_run_that_reported_missing_browser_proof()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the calculator app.",
            ProcessStepKind.Review);

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
            [candidate, detail, responseText, Array.Empty<string>(), 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
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
        var candidate = CreateDispatchCandidate("Implement the units converter and prove the build passes.");

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
            "Implement the units converter.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude. Do not use workspace_append_file for canonical deliverables.");

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
            "Run QA validation and browser proof for the calculator app.",
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
            "Clarify the scope and release boundary for the calculator delivery.",
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
                            "Captured the calculator scope and boundary.",
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
            "Implement the units converter.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude. Do not use workspace_append_file for canonical deliverables.");
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "Apply-CalculatorShowcaseApp.ps1",
                    "showcases/blazor-ssr-calculator",
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
                    "SimpleCalculatorApp.csproj",
                    "showcases/blazor-ssr-calculator/app/SimpleCalculatorApp",
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
                    "BasicUnitsConverter.slnx",
                    "deliveries/blazor-ssr-basic-units-converter",
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
                    "BasicUnitsConverter.slnx",
                    "deliveries/blazor-ssr-basic-units-converter",
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
            "Run QA validation and browser proof for the calculator app.",
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

        var candidate = CreateDispatchCandidate("Implement the calculator and prove the build passes.");
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
            "artifacts/scopes/organization/demo/deliveries/blazor-ssr-basic-units-converter/process/implementation/implementation-change-set.md",
            "text/markdown",
            "workspace",
            "Durable implementation evidence",
            DateTimeOffset.UtcNow);

        var relativePath = buildStorageRelativePath.Invoke(null, [candidate, artifact]) as string;

        Assert.Equal(
            "artifacts/scopes/organization/demo/deliveries/blazor-ssr-basic-units-converter/process/implementation/implementation-change-set.md",
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
                    "showcases/blazor-ssr-calculator",
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
                    "deliveries/units-converter",
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
            ("browser_take_screenshot", new Dictionary<string, object?> { ["filename"] = "execute-release-rollout/calculator-proof.png" }, CreateProviderNativeTextResult("Screenshot saved.")),
            ("browser_snapshot", new Dictionary<string, object?> { ["filename"] = "execute-release-rollout/calculator-page.yml" }, CreateProviderNativeTextResult("Snapshot saved.")),
            ("browser_console_messages", new Dictionary<string, object?> { ["filename"] = "execute-release-rollout/calculator-console.log" }, CreateProviderNativeTextResult("Console log saved.")));

        var outputFilesByToolName = resolveSuccessfulSessionToolOutputFiles.Invoke(null, [serializedSessionState]) as IReadOnlyDictionary<string, IReadOnlyList<string>>;

        Assert.NotNull(outputFilesByToolName);
        Assert.Contains(outputFilesByToolName.Keys, item => string.Equals(item, "browser_take_screenshot", StringComparison.Ordinal));
        Assert.Contains(outputFilesByToolName.Keys, item => string.Equals(item, "browser_snapshot", StringComparison.Ordinal));
        Assert.Contains(outputFilesByToolName.Keys, item => string.Equals(item, "browser_console_messages", StringComparison.Ordinal));
        Assert.Contains("execute-release-rollout/calculator-proof.png", outputFilesByToolName["browser_take_screenshot"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains("execute-release-rollout/calculator-page.yml", outputFilesByToolName["browser_snapshot"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains("execute-release-rollout/calculator-console.log", outputFilesByToolName["browser_console_messages"], StringComparer.OrdinalIgnoreCase);
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
            [executionRunId, "artifacts/scopes/organization/demo/deliveries/blazor-ssr-basic-units-converter/process/peer-review/peer-review-note.md"]) as string;
        var unscopedKey = buildResponseTextArtifactExternalReferenceKey.Invoke(
            null,
            [executionRunId, "artifacts/deliveries/blazor-ssr-basic-units-converter/process/peer-review/peer-review-note.md"]) as string;

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
            "Clarify the calculator scope, acceptance checks, and release boundary.",
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
            "Clarify the calculator scope, acceptance checks, and release boundary.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Scope boundary packet", true, "Must capture in-scope behavior, out-of-scope behavior, acceptance checks, tenant impact, and release boundary."));
        var now = DateTimeOffset.UtcNow;
        var responseText = """
            ## Project layout

            C:\programovani\csharp\calculator

            Create a CalculatorApp folder with a Blazor WebAssembly project, add a Calculator.razor component,
            wire up +, -, *, /, and = buttons, and then run dotnet build. The executable or published output can
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
            "Clarify the calculator scope, acceptance checks, and release boundary.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Scope boundary packet", true, "Must capture in-scope behavior, out-of-scope behavior, acceptance checks, tenant impact, and release boundary."));
        var now = DateTimeOffset.UtcNow;
        var responseText = """
            ## Scope boundary packet

            In-scope behavior: deliver a Blazor SSR calculator in C:\programovani\csharp\calculator with numeric keypad buttons, +, -, *, /, = operations, and a calculation history list.
            Out-of-scope behavior: authentication, persistence beyond the in-memory history list, multi-tenant administration, and deployment automation are excluded from this run.
            Acceptance checks: the app builds, the calculator buttons update the display, division by zero is handled, and completed calculations append to history.
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
            "Review the delivered calculator app and confirm integration readiness.",
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
        var candidate = CreateDispatchCandidate("Implement the units converter and prove the build passes.");

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
            "Implement the calculator as a Blazor app and prove the build passes.",
            (
                "Document the calculator architecture",
                "Calculator architecture",
                [
                    (
                        "Calculator architecture",
                        "evidence",
                        "artifacts/scopes/organization/demo/architecture/Calculator-Architecture.md",
                        "Blazor Server app with calculator UI.",
                        "Approved architecture note.")
                ]));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("inspect the upstream durable artifacts directly", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_stat_path", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_read_file", prompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/scopes/organization/demo/architecture/Calculator-Architecture.md", prompt, StringComparison.Ordinal);
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
            "Implement the calculator as a Blazor app and prove the build passes.",
            new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-main-application",
                ParentNodeTitle = "Create main application"
            });
        const string projectStructureGrounding = """
Dispatcher fetched the live project structure for `Calculator` and focused this prompt on the selected work branch.
Descendant requirement context from sibling planning nodes:
- Blazor SSR (feature:blazor); type: ProjectBlock/feature
- buttons for +,-,/,*,= (feature:buttons); type: ProjectBlock/feature
- calculations history list (feature:history); type: ProjectBlock/feature
- output must be placed in C:\programovani\csharp\calculator (note:output-path); type: ProjectBlock/note
""";

        var prompt = buildExecutionPromptCore.Invoke(null, [candidate, null, projectStructureGrounding, null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Concrete feature and constraint nodes from the live project structure are required scope for this implementation step.", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not defer grounded features, UI behavior, acceptance notes, or output constraints", prompt, StringComparison.Ordinal);
        Assert.Contains("replace placeholder output with the requested product, document, analysis, workflow, or other concrete deliverable", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not write implementation artifacts that say the requested behavior, analysis, artifacts, tests", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Hello, world!", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("CalculatorEngine", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_surfaces_grounded_external_target_and_forbids_artifact_scaffolding_for_implementation_steps()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var projectId = Guid.NewGuid();
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the calculator as a Blazor app and prove the build passes.",
            new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-main-application",
                ParentNodeTitle = "Create main application"
            });
        const string projectStructureGrounding = """
Dispatcher fetched the live project structure for `Calculator` and focused this prompt on the selected work branch.
Descendant requirement context from sibling planning nodes:
- Blazor SSR (feature:blazor); type: ProjectBlock/feature
- output must be placed in C:\programovani\csharp\calculator (note:output-path); type: ProjectBlock/note
""";

        var prompt = buildExecutionPromptCore.Invoke(null, [candidate, null, projectStructureGrounding, null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains(
            "The grounded project structure already identifies the external output root `C:\\programovani\\csharp\\calculator` mapped to `external-target/C/programovani/csharp/calculator`.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "For this implementation, create and edit the deliverable under `external-target/C/programovani/csharp/calculator`.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not build a shadow product in `artifacts/`, `output/`, `data/`, or other managed evidence folders",
            prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain("workspace_dotnet_new", prompt, StringComparison.Ordinal);
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
            "Calculator",
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
            @"output must be placed in C:\programovani\csharp\calculator",
            string.Empty,
            "Draft",
            @"output must be placed in C:\programovani\csharp\calculator",
            "{}"), 3);

        var summary = buildGroundingSummary.Invoke(
            null,
            [
                "Calculator",
                surfaceNodes,
                canonicalNodes,
                context
            ]) as string;

        Assert.NotNull(summary);
        Assert.Contains("Sibling planning context under the same parent:", summary, StringComparison.Ordinal);
        Assert.Contains("Main architecture", summary, StringComparison.Ordinal);
        Assert.Contains("Main features", summary, StringComparison.Ordinal);
        Assert.Contains("Descendant requirement context from sibling planning nodes:", summary, StringComparison.Ordinal);
        Assert.Contains("Blazor SSR", summary, StringComparison.Ordinal);
        Assert.Contains(@"output must be placed in C:\programovani\csharp\calculator", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPrompt_requires_tests_now_when_implementation_step_contract_mentions_tests()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate("Implement feature, tests, and migration notes for the calculator app.");

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
    public void ResolveRequiredToolNames_adds_implementation_proof_tools_for_work_steps()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate("Implement the units converter and prove the build passes.");

        var requiredToolNames = resolveRequiredToolNames.Invoke(null, [candidate]) as IReadOnlyList<string>;

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_stat_path", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_read_file", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_dotnet_build", requiredToolNames, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_fails_work_step_without_declared_outcome()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidate("Implement the units converter and prove the build passes.");
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "deliveries/units-converter/src",
                    "Succeeded",
                    now,
                    now)
            ]
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement the units converter"]) as string;

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
        var candidate = CreateCalculatorImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Blazor SSR app scaffolded, build succeeded, and required artifacts written per scope and architecture requirements.",
            summaryMarkdown: """
            - Read and validated upstream architecture and scope artifacts.
            - Confirmed the required output directory and Blazor SSR stack.
            - Successfully scaffolded a Blazor SSR app in `external-target/C/programovani/csharp/calculator/CalculatorApp` targeting .NET 10.0.
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "CalculatorApp.csproj",
                    "external-target/C/programovani/csharp/calculator/CalculatorApp",
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
                    "external-target/C/programovani/csharp/calculator/CalculatorApp.Tests/CalculatorApp.Tests.csproj",
                    "external-target/C/programovani/csharp/calculator/CalculatorApp.Tests",
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
        var candidate = CreateCalculatorImplementationDispatchCandidate();
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "CalculatorApp.csproj",
                    "external-target/C/programovani/csharp/calculator/CalculatorApp",
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
                    "external-target/C/programovani/csharp/calculator/CalculatorApp.Tests/CalculatorApp.Tests.csproj",
                    "external-target/C/programovani/csharp/calculator/CalculatorApp.Tests",
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
    public void ResolveCompletionStatus_blocks_work_step_when_response_lists_next_required_actions()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateCalculatorImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        const string responseText = """
            **Summary of current state and actions taken:**
            - The Calculator app is scaffolded and builds successfully.
            - The main route is still untouched template output ("Hello, world!").
            - No required calculator UI or logic is present yet.
            - A test project (`CalculatorApp.Tests`) was created using xUnit.

            **Next required actions:**
            - Replace the template `Home.razor` with a calculator UI.
            - Implement minimal business logic.
            - Add at least one meaningful automated test.
            - Prepare and write the required migration/rollout checklist artifact.
            - Prepare and write the required implementation change set artifact.

            **Proceeding to implement the calculator UI and logic, update tests, and write required artifacts.**
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "CalculatorApp.csproj",
                    "external-target/C/programovani/csharp/calculator/CalculatorApp",
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
                    "external-target/C/programovani/csharp/calculator/CalculatorApp.Tests/CalculatorApp.Tests.csproj",
                    "external-target/C/programovani/csharp/calculator/CalculatorApp.Tests",
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
        var candidate = CreateCalculatorImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        const string responseText = """
            Summary of current state:
            - The app is scaffolded and builds successfully.
            - No required calculator UI or logic is present yet.

            Next required actions:
            - Replace the template page with a calculator UI.
            - Implement the required logic and tests.

            Proceeding to implement the calculator UI and logic.
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
                    "external-target/C/units-converter/src/Units/Program.cs",
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
                    "CalculatorApp.csproj",
                    "external-target/C/programovani/csharp/calculator/CalculatorApp",
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
            "Implement the calculator as a Blazor app and prove the build passes.",
            (
                "Document the calculator architecture",
                "Calculator architecture",
                [
                    (
                        "Calculator architecture",
                        "evidence",
                        "artifacts/scopes/organization/demo/architecture/Calculator-Architecture.md",
                        "Blazor Server app with calculator UI.",
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
        Assert.Contains("artifacts/scopes/organization/demo/architecture/Calculator-Architecture.md", directive, StringComparison.Ordinal);
        Assert.Contains("runnable web host", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRecoveryDirective_requires_process_step_outcome_for_governed_review_retry()
    {
        var buildRecoveryDirective = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildRecoveryDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRecoveryDirective method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the calculator app.",
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
            "Run QA validation and browser proof for the calculator app.",
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
        Assert.Contains("convert it to the native Windows path inside the helper", directive, StringComparison.Ordinal);
        Assert.Contains("a relative `external-target/...` string can resolve under the managed workspace path alias", directive, StringComparison.Ordinal);
        Assert.Contains("click a representative sequence", directive, StringComparison.Ordinal);
        Assert.Contains("Blazor render-mode or static-SSR implementation defect", directive, StringComparison.Ordinal);
        Assert.Contains("browser_take_screenshot", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRecoveryDirective_requires_reusing_existing_scaffold_after_dotnet_new_overwrite_conflicts()
    {
        var buildRecoveryDirective = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildRecoveryDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRecoveryDirective method was not found.");
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the calculator and prove the build passes.",
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
                        RequestSummary: "new blazor -n CalculatorApp",
                        WorkingDirectory: ".",
                        ExitSummary: "Failed (exit 73)",
                        StartedAtUtc: now,
                        CompletedAtUtc: now)
                },
                1
            ]) as string;

        Assert.NotNull(directive);
        Assert.Contains("files already existed", directive, StringComparison.Ordinal);
        Assert.Contains("continue by repairing, reading, and building that existing project in place", directive, StringComparison.Ordinal);
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
            "Write calculator scope.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Calculator scope artifact", true, "Scope artifact must describe arithmetic operations and divide-by-zero acceptance criteria."));
        var detail = CreateProcessMockExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Calculator scope and acceptance criteria were written."),
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
            "Write calculator scope.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief, "Unrelated compliance packet", true, "Compliance packet must include unrelated governance metadata."));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Calculator scope and acceptance criteria were written.");
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
        var candidate = CreateCalculatorImplementationDispatchCandidate();
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Implementation change set was written.",
            summaryMarkdown: """
            ## Implementation change set
            Touched surface inventory: CalculatorEngine owns the calculator arithmetic behavior.
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
        var candidate = CreateCalculatorImplementationDispatchCandidate();
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Implementation and DB-free rollout checklist were written.",
            summaryMarkdown: """
            ## Implementation change set
            Touched surface inventory: CalculatorEngine owns Add, Subtract, Multiply, and Divide behavior.
            Tests and validation: deterministic process mock validation covers the implementation lane and links the change set to test proof.
            Migration notes: no schema, persistent data, or backfill changes are part of this implementation.

            ## Migration and rollout preparation checklist
            Data changes: no data migration required; no schema migration, seed update, backfill, or data rollback is needed.
            Operational preconditions: implementation validation must pass and QA must verify calculator arithmetic plus divide-by-zero behavior.
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
            "Implement the calculator as a Blazor app and prove the build passes.",
            (
                "Write calculator architecture",
                "Calculator architecture artifact",
                []));
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Write calculator architecture must provide Calculator architecture artifact before implementation can proceed.",
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
            "Review the first calculator implementation and route the next step.",
            ProcessStepKind.Review,
            [
                (ProcessMockAgentCatalog.BranchRepairsRequired, "Repairs required", "Route the calculator implementation through defect repair."),
                (ProcessMockAgentCatalog.BranchApproved, "Approved", "Route directly to release notes when no repair is required.")
            ],
            true,
            [(ProcessArtifactKind.Evidence, "Calculator QA rejection artifact", true, "QA first review artifact must record the branch reason.")],
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
            "Recheck the repaired calculator implementation and approve the release path.",
            ProcessStepKind.Review,
            [(ProcessMockAgentCatalog.BranchApproved, "Approved", "Route repaired calculator implementation to release notes.")],
            true,
            [(ProcessArtifactKind.Evidence, "Calculator QA approval artifact", true, "QA recheck artifact must record approval for release.")],
            []);
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Repaired calculator implementation passed QA.",
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
            [processRunId, stepRunId, "Review first calculator implementation", partyId, AiResourceBindingStatus.Unbound, null]) as string;

        Assert.NotNull(diagnostic);
        Assert.Contains("Review first calculator implementation", diagnostic, StringComparison.Ordinal);
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
        var candidate = CreateDispatchCandidate("Implement the calculator and prove the build passes.");

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
    public void ResolveCompletionStatus_returns_failed_for_non_governed_step_when_provider_failure_is_detected()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var candidate = CreateDispatchCandidate("Implement the calculator and prove the build passes.");

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

    private static object CreateCalculatorImplementationDispatchCandidate()
    {
        return CreateDispatchCandidate(
            "Implement feature, tests, and migration notes for the calculator app.",
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
            "Deliver the calculator showcase.",
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
                "Deliver the calculator showcase.",
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
            "Units converter step run",
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
