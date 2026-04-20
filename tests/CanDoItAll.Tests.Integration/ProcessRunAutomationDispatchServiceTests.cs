using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
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
            staleCreatedAtUtc.AddMinutes(3));

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
            createdAtUtc.AddMinutes(3));

        Assert.False(hasBlockingRun);
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
    public void ResolveReusableAutomationChatSessionId_returns_latest_chat_backed_automation_run()
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

        Assert.Equal(latestSessionId, chatSessionId);
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
    [InlineData(ProcessStepRunStatus.InProgress, null, "2026-04-19T02:00:00+00:00", "2026-04-19T02:01:00+00:00", "step-transition:Completed", true)]
    [InlineData(ProcessStepRunStatus.InProgress, null, "2026-04-19T02:00:00+00:00", "2026-04-19T02:01:00+00:00", "step-transition:InProgress", true)]
    [InlineData(ProcessStepRunStatus.InProgress, null, "2026-04-19T02:00:00+00:00", "2026-04-19T02:01:00+00:00", "runtime-recovery-scan", true)]
    [InlineData(ProcessStepRunStatus.InProgress, null, "2026-04-19T02:00:00+00:00", "2026-04-19T02:03:00+00:00", "runtime-recovery-scan", false)]
    [InlineData(ProcessStepRunStatus.Ready, null, null, "2026-04-19T02:01:00+00:00", "step-transition:Completed", false)]
    public void ShouldSkipFreshAutomationDispatch_skips_only_stale_non_recovery_dispatches(
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

        var workBrief = buildWorkBrief.Invoke(null, [definition, step, "Showcase Lead Engineer"]) as string;

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
                "Must capture the clarified release boundary.")
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
                "Must capture the clarified release boundary.")
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
                "Imported browser screenshot is required.")
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
                "Imported browser screenshot is required.")
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
                "Create this artifact at artifacts/showcases/blazor-ssr-calculator/evidence/process/implementation/implementation-change-set.md using workspace create/write file tools.")
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
                "Create this artifact at showcases/blazor-ssr-calculator/app/SimpleCalculatorApp/SimpleCalculatorApp.csproj using workspace create/write file tools.")
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
                "The durable screenshot must exist at artifacts/showcases/blazor-ssr-calculator/evidence/ui/qa-validation/calculator-proof.png.")
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
            "PROCESS_STEP_OUTCOME",
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
    public void ResolveCompletionStatus_fails_when_required_step_tools_were_not_executed()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the calculator.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude.");
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
                "Implementation complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Implementation and required validation completed.\"} -->",
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

        var candidate = CreateDispatchCandidate(
            "Implement the calculator.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude.");
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
                "Retry complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Required tools succeeded across recovery attempts.\"} -->",
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

        Assert.Equal(ProcessStepRunStatus.Completed, status);
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
                "Implementation complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Required implementation tools succeeded.\"} -->",
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

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.NotNull(reason);
        Assert.Contains("completed step", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("workspace_pwsh_run_script", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_completed_run_that_only_missed_required_tools()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");

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

        var shouldRetryResult = shouldRetry.Invoke(null, [detail, new[] { "workspace_write_file", "workspace_dotnet_build" }, 1]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_completed_run_with_unresolved_critical_tool_failure()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");

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

        var shouldRetryResult = shouldRetry.Invoke(null, [detail, Array.Empty<string>(), 1]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_false_after_final_attempt()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");

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

        var shouldRetryResult = shouldRetry.Invoke(null, [detail, new[] { "workspace_write_file", "workspace_dotnet_build" }, 3]);

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
            "Implement the calculator.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude. Do not use workspace_append_file for canonical deliverables.");

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
    public void ResolveCompletionStatus_ignores_negated_tool_references_when_required_step_tools_succeed()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the calculator.\nInstructions: Call workspace_pwsh_run_script first and then call workspace_dotnet_build before you conclude. Do not use workspace_append_file for canonical deliverables.");
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
                "Review complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Review completed without blocking findings.\"} -->",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ExecutionState.Completed,
                RunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                string.Empty,
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

        Assert.Equal(ProcessStepRunStatus.Completed, status);
    }

    [Fact]
    public void ResolveCompletionStatus_ignores_failed_workspace_git_diff_in_non_git_delivery_workspaces()
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
                "Code review run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "QA proof complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Required browser evidence was captured.\"} -->",
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
                "QA proof complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Required browser evidence was captured.\"} -->",
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

        var candidate = CreateDispatchCandidate("Review the delivered application and block progression if the required feature is missing.");
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
                "Critical defect: the stock scaffold still renders and the units conversion flow does not exist. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Blocked\",\"reason\":\"Critical defect: the stock scaffold still renders and the units conversion flow does not exist.\"} -->",
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

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Run QA validation and browser proof"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("blocked step", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("units conversion flow does not exist", reason, StringComparison.OrdinalIgnoreCase);
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
            "Validate the UI.\nInstructions: Call browser_take_screenshot, browser_snapshot, and browser_console_messages before you conclude.");
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
                "QA proof complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Required browser evidence was captured.\"} -->",
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
            "Validate the UI.\nInstructions: Use browser_resize, browser_navigate, browser_fill_form, browser_select_option, browser_take_screenshot, browser_snapshot, and browser_console_messages before you conclude.");
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
                "QA proof complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Required browser evidence was captured.\"} -->",
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
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Complete governed code review"]) as string;

        Assert.Equal(ProcessStepRunStatus.Failed, status);
        Assert.NotNull(reason);
        Assert.Contains("PROCESS_STEP_OUTCOME", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_add_governed_inspection_tools_for_work_steps()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate("Implement the units converter and prove the build passes.");

        var requiredToolNames = resolveRequiredToolNames.Invoke(null, [candidate]) as IReadOnlyList<string>;

        Assert.NotNull(requiredToolNames);
        Assert.DoesNotContain("workspace_stat_path", requiredToolNames, StringComparer.Ordinal);
        Assert.DoesNotContain("workspace_read_file", requiredToolNames, StringComparer.Ordinal);
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
                null,
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Implement the units converter"]) as string;

        Assert.Equal(ProcessStepRunStatus.Failed, status);
        Assert.NotNull(reason);
        Assert.Contains("PROCESS_STEP_OUTCOME", reason, StringComparison.Ordinal);
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
        Assert.Contains("\"branchOutcomeKey\":\"approved\"", prompt, StringComparison.Ordinal);
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
                "Review complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Code review passed.\"} -->",
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
                "Review complete. <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"Code review passed.\",\"branchOutcomeKey\":\"approved\"} -->",
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
            expectedArtifactDefinitions);
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
            []);
    }

    private static object CreateDispatchCandidateCore(
        string workBriefText,
        ProcessStepKind stepKind,
        (string Key, string Title, string Description)[] branchOutcomeDefinitions,
        bool requiresExplicitBranchOutcomeSelection,
        params (ProcessArtifactKind ArtifactKind, string Title, bool IsRequired, string ValidationRequirementSummary)[] expectedArtifactDefinitions)
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var candidateType = serviceType.GetNestedType("DispatchCandidate", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchCandidate type was not found.");
        var artifactExpectationType = serviceType.GetNestedType("DispatchArtifactExpectation", BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("DispatchArtifactExpectation type was not found.");
        var artifactInputType = serviceType.GetNestedType("DispatchArtifactInput", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchArtifactInput type was not found.");
        var branchOutcomeType = serviceType.GetNestedType("DispatchBranchOutcome", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchBranchOutcome type was not found.");
        var expectedArtifacts = Array.CreateInstance(artifactExpectationType, expectedArtifactDefinitions.Length);
        var artifactInputs = Array.CreateInstance(artifactInputType, 0);
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
                definition.ValidationRequirementSummary)
                ?? throw new InvalidOperationException("DispatchArtifactExpectation could not be constructed.");
            expectedArtifacts.SetValue(expectedArtifact, index);
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
            .Single();
        return constructor.Invoke(
                   [
                       new ProcessRun
                       {
                           Name = "Showcase run",
                           TriggerReason = "Deliver the calculator showcase."
                       },
                       new ProcessDefinition
                       {
                           Name = "Software delivery"
                       },
                       new ProcessStepRun
                       {
                           Title = "Implement feature, tests, and migration notes",
                           CurrentExecutorName = "Showcase Lead Engineer",
                           StepKind = stepKind
                       },
                       new ProcessWorkBrief
                       {
                           WorkBriefText = workBriefText,
                           HandoffSummary = "Architecture decision record.",
                           ExpectedOutcome = "Buildable calculator implementation.",
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
                       branchOutcomes,
                       requiresExplicitBranchOutcomeSelection
                   ])
               ?? throw new InvalidOperationException("DispatchCandidate could not be constructed.");
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
}
