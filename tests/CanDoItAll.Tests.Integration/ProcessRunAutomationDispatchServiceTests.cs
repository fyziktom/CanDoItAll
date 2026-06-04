using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRunAutomationDispatchServiceTests
{
    [Fact]
    public void DispatchDecisionServices_expose_typed_resolver_boundaries()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var nestedTypeFlags = BindingFlags.NonPublic | BindingFlags.Public;
        var staticFieldFlags = BindingFlags.NonPublic | BindingFlags.Static;

        foreach (var nestedTypeName in new[]
        {
            "IRequiredToolResolver",
            "IBrowserProofRequirementResolver",
            "IArtifactRequirementMatcher",
            "IStepCompletionPolicy",
            "IDispatchDecisionEngine",
            "RequiredToolResolution",
            "BrowserProofRequirement",
            "ArtifactRequirementMatch",
            "StepCompletionPolicyInput",
            "StepCompletionPolicyDecision",
            "DispatchDecisionInput",
            "DispatchDecision"
        })
        {
            Assert.NotNull(serviceType.GetNestedType(nestedTypeName, nestedTypeFlags));
        }

        foreach (var fieldName in new[]
        {
            "RequiredToolResolver",
            "BrowserProofRequirementResolver",
            "ArtifactRequirementMatcher",
            "StepCompletionPolicy",
            "DispatchDecisionEngine"
        })
        {
            var field = serviceType.GetField(fieldName, staticFieldFlags);

            Assert.NotNull(field);
            Assert.NotNull(field.GetValue(null));
        }
    }

    [Fact]
    public void HasBlockingAutomationExecutionRun_ignores_failed_manual_debug_runs()
    {
        var hasBlockingRun = ProcessRunAutomationDispatchService.HasBlockingAutomationExecutionRun(
        [
            CreateExecutionRun("agent-run-debug", ProcessAutomationExecutionState.Failed, ProcessAutomationRunOutcome.Failed)
        ]);

        Assert.False(hasBlockingRun);
    }

    [Fact]
    public void HasBlockingAutomationExecutionRun_ignores_completed_automation_runs()
    {
        var hasBlockingRun = ProcessRunAutomationDispatchService.HasBlockingAutomationExecutionRun(
        [
            CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded)
        ]);

        Assert.False(hasBlockingRun);
    }

    [Fact]
    public void HasBlockingAutomationExecutionRun_blocks_active_automation_runs()
    {
        var hasBlockingRun = ProcessRunAutomationDispatchService.HasBlockingAutomationExecutionRun(
        [
            CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Running, null)
        ]);

        Assert.True(hasBlockingRun);
    }

    [Theory]
    [InlineData(ProcessRunStatus.Failed, ProcessStepRunStatus.InProgress, false)]
    [InlineData(ProcessRunStatus.Failed, ProcessStepRunStatus.Completed, true)]
    [InlineData(ProcessRunStatus.Failed, ProcessStepRunStatus.Failed, true)]
    [InlineData(ProcessRunStatus.Completed, ProcessStepRunStatus.InProgress, true)]
    [InlineData(ProcessRunStatus.Cancelled, ProcessStepRunStatus.InProgress, true)]
    [InlineData(ProcessRunStatus.Active, ProcessStepRunStatus.InProgress, false)]
    public void IsRunClosedToAutomation_keeps_reopened_step_dispatchable_inside_failed_run(
        ProcessRunStatus runStatus,
        ProcessStepRunStatus stepStatus,
        bool expected)
    {
        var closed = ProcessRunAutomationDispatchService.IsRunClosedToAutomation(runStatus, stepStatus);

        Assert.Equal(expected, closed);
    }

    [Theory]
    [InlineData(ProcessRunStatus.Failed, ProcessStepRunStatus.InProgress, true)]
    [InlineData(ProcessRunStatus.Failed, ProcessStepRunStatus.Ready, false)]
    [InlineData(ProcessRunStatus.Failed, ProcessStepRunStatus.WaitingApproval, false)]
    [InlineData(ProcessRunStatus.Active, ProcessStepRunStatus.Ready, true)]
    [InlineData(ProcessRunStatus.Active, ProcessStepRunStatus.WaitingApproval, true)]
    [InlineData(ProcessRunStatus.Active, ProcessStepRunStatus.InProgress, true)]
    public void IsStepStatusDispatchableForRun_restricts_failed_runs_to_reopened_inprogress_steps(
        ProcessRunStatus runStatus,
        ProcessStepRunStatus stepStatus,
        bool expected)
    {
        var dispatchable = ProcessRunAutomationDispatchService.IsStepStatusDispatchableForRun(runStatus, stepStatus);

        Assert.Equal(expected, dispatchable);
    }

    [Fact]
    public void TryReadDotnetRunStartupReceipt_preserves_static_web_assets_alias_cleanup_targets()
    {
        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.DotnetRunStartupReceipt.{Guid.NewGuid():N}");
        var stdoutPath = Path.Combine(tempRoot, "stdout.txt");
        var workspaceRoot = Path.Combine(tempRoot, "workspace");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var receiptJson = JsonSerializer.Serialize(new
            {
                keepAlive = true,
                lifetimeScope = "ExecutionRun",
                cleanupAttempted = false,
                appProcessTreeIds = new[] { 321, 654 },
                staticWebAssetsAliasMappings = new[]
                {
                    new { drive = "Y:", workspaceRoot, mounted = true },
                    new { drive = "Z:", workspaceRoot = Path.Combine(tempRoot, "other-workspace"), mounted = false }
                }
            });
            File.WriteAllText(stdoutPath, $"startup log noise{Environment.NewLine}{receiptJson}", Encoding.UTF8);

            var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
                "TryReadDotnetRunStartupReceipt",
                BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new InvalidOperationException("TryReadDotnetRunStartupReceipt method was not found.");
            object?[] arguments = [stdoutPath, null];

            var parsed = (bool)method.Invoke(null, arguments)!;

            Assert.True(parsed);
            var receipt = arguments[1] ?? throw new InvalidOperationException("Startup receipt was not returned.");
            var mappings = Assert.IsAssignableFrom<IReadOnlyList<ProcessRunAutomationDispatchService.StaticWebAssetsAliasMapping>>(
                ReadRecordProperty(receipt, "StaticWebAssetsAliasMappings"));
            Assert.Equal(2, mappings.Count);
            Assert.Equal("Y:", mappings[0].Drive);
            Assert.Equal(workspaceRoot, mappings[0].WorkspaceRoot);
            Assert.True(mappings[0].Mounted);
            Assert.Equal("Z:", mappings[1].Drive);
            Assert.False(mappings[1].Mounted);
            Assert.True(Assert.IsType<bool>(ReadRecordProperty(receipt, "HasCleanupTargets")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("Y:\\: => C:\\repo\\workspace\r\nZ:\\: => C:\\other", "Y:", "C:\\repo\\workspace")]
    [InlineData("y:\\: => C:\\repo\\workspace", "Y:", "C:\\repo\\workspace")]
    [InlineData("Y:\\: => C:\\repo\\workspace", "Q:", null)]
    public void ResolveSubstDriveTargetFromOutput_parses_subst_alias_lines(
        string substOutput,
        string drive,
        string? expectedTarget)
    {
        var target = ProcessRunAutomationDispatchService.ResolveSubstDriveTargetFromOutput(substOutput, drive);

        Assert.Equal(expectedTarget, target);
    }

    [Fact]
    public void ClassifyStaticWebAssetsAliasCleanup_requires_matching_subst_target_before_dismount()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "CanDoItAll.StaticWebAssetsAlias", "workspace");
        var mapping = new ProcessRunAutomationDispatchService.StaticWebAssetsAliasMapping("y:", workspaceRoot, true);

        Assert.Equal(
            ProcessRunAutomationDispatchService.StaticWebAssetsAliasCleanupStatus.ReadyToDismount,
            ProcessRunAutomationDispatchService.ClassifyStaticWebAssetsAliasCleanup(mapping, workspaceRoot + Path.DirectorySeparatorChar));
        Assert.Equal(
            ProcessRunAutomationDispatchService.StaticWebAssetsAliasCleanupStatus.SkippedMappingMismatch,
            ProcessRunAutomationDispatchService.ClassifyStaticWebAssetsAliasCleanup(mapping, Path.Combine(Path.GetTempPath(), "other-workspace")));
        Assert.Equal(
            ProcessRunAutomationDispatchService.StaticWebAssetsAliasCleanupStatus.SkippedNoCurrentMapping,
            ProcessRunAutomationDispatchService.ClassifyStaticWebAssetsAliasCleanup(mapping, null));
        Assert.Equal(
            ProcessRunAutomationDispatchService.StaticWebAssetsAliasCleanupStatus.SkippedInvalidDrive,
            ProcessRunAutomationDispatchService.ClassifyStaticWebAssetsAliasCleanup(
                new ProcessRunAutomationDispatchService.StaticWebAssetsAliasMapping("Y:\\", workspaceRoot, true),
                workspaceRoot));
        Assert.Equal(
            ProcessRunAutomationDispatchService.StaticWebAssetsAliasCleanupStatus.SkippedNotMounted,
            ProcessRunAutomationDispatchService.ClassifyStaticWebAssetsAliasCleanup(
                new ProcessRunAutomationDispatchService.StaticWebAssetsAliasMapping("Y:", workspaceRoot, false),
                workspaceRoot));
    }

    [Theory]
    [InlineData(ProcessStepBlockReasonCode.MissingUpstreamArtifact, ProcessStepBlockCause.UpstreamInput, ProcessStepRecoveryOption.WaitForArtifactMaterialization)]
    [InlineData(ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, ProcessStepBlockCause.OwnOutput, ProcessStepRecoveryOption.RecoverArtifactsOnly)]
    [InlineData(ProcessStepBlockReasonCode.NoProgress, ProcessStepBlockCause.RuntimeEvidence, ProcessStepRecoveryOption.FreshAgentSession)]
    [InlineData(ProcessStepBlockReasonCode.PolicyDeniedExternalPath, ProcessStepBlockCause.PolicyDenied, ProcessStepRecoveryOption.HumanEscalation)]
    [InlineData(ProcessStepBlockReasonCode.ValidationFailed, ProcessStepBlockCause.RuntimeEvidence, ProcessStepRecoveryOption.RepairImplementation)]
    public void ProcessRecoveryRouter_SB10_INV_001_selects_deterministic_next_action(
        ProcessStepBlockReasonCode blockReasonCode,
        ProcessStepBlockCause blockCause,
        ProcessStepRecoveryOption expectedAction)
    {
        var decision = ProcessRecoveryRouter.Route(new ProcessRecoveryRoutingRequest(
            blockReasonCode,
            blockCause,
            "Deterministic routing diagnostic.",
            ProcessStepRunBlockState.ResolveRecoveryOptions(blockReasonCode),
            [],
            EvidenceFingerprint: "sb10-router-positive",
            HasNewEvidence: true));

        Assert.Equal(expectedAction, decision.NextAction);
        Assert.Contains(expectedAction, decision.AvailableActions);
        Assert.False(decision.IsNoProgressGuarded);
    }

    [Fact]
    public void ProcessRecoveryRouter_SB10_INV_002_escalates_repeated_no_progress_without_new_evidence()
    {
        var recentAttempt = new ProcessRecoveryRoutingAttempt(
            ProcessStepRecoveryOption.FreshAgentSession,
            "sb10-no-progress",
            DateTimeOffset.UtcNow.AddMinutes(-5));

        var decision = ProcessRecoveryRouter.Route(new ProcessRecoveryRoutingRequest(
            ProcessStepBlockReasonCode.NoProgress,
            ProcessStepBlockCause.RuntimeEvidence,
            "The same recovery attempt made no progress.",
            ProcessStepRunBlockState.ResolveRecoveryOptions(ProcessStepBlockReasonCode.NoProgress),
            [recentAttempt],
            EvidenceFingerprint: "sb10-no-progress",
            HasNewEvidence: false));

        Assert.Equal(ProcessStepRecoveryOption.HumanEscalation, decision.NextAction);
        Assert.True(decision.IsNoProgressGuarded);
        Assert.Contains("without new evidence", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessBlockStateClassifier_SB11_INV_001_classifies_typed_causes_without_step_mutation()
    {
        var classification = ProcessBlockStateClassifier.Classify(
            "The upstream vendor intake packet has not materialized.",
            ProcessStepBlockCause.UpstreamInput);

        Assert.Equal(ProcessStepBlockReasonCode.MissingUpstreamArtifact, classification.ReasonCode);
        Assert.Equal(ProcessStepBlockCause.UpstreamInput, classification.BlockCause);
        Assert.Contains(ProcessStepRecoveryOption.WaitForArtifactMaterialization, classification.RecoveryOptions);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, classification.RecoveryOptions);
    }

    [Fact]
    public void ProcessHealthInvariantAuditor_SB11_INV_001_builds_generic_actionable_recovery_health()
    {
        var stepRun = new ProcessStepRun
        {
            Id = Guid.NewGuid(),
            Status = ProcessStepRunStatus.Blocked,
            CurrentExecutorPartyId = Guid.NewGuid(),
            BlockedReason = "Vendor compliance evidence is missing."
        };
        ProcessStepRunBlockState.Apply(
            stepRun,
            "Vendor compliance evidence artifact is missing from the approval packet.",
            ProcessStepBlockCause.OwnOutput);

        var health = ProcessHealthInvariantAuditor.BuildStepHealth(
            stepRun,
            [
                new ProcessArtifactExpectationSatisfactionViewModel(
                    stepRun.Id,
                    Guid.NewGuid(),
                    ProcessArtifactKind.Evidence,
                    "Vendor compliance evidence",
                    IsRequired: true,
                    ProcessArtifactExpectationSatisfactionStatus.Missing,
                    ProcessArtifactExpectationSourceKind.None,
                    null,
                    string.Empty,
                    string.Empty,
                    "Required vendor compliance evidence was not recorded.")
            ],
            manualRecoveryDirective: string.Empty);

        Assert.Equal(ProcessRecoveryClassification.MissingArtifact, health.RecoveryClassification);
        Assert.Equal(ProcessStepRecoveryOption.RecoverArtifactsOnly, health.NextRecoveryAction);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, health.RecoveryOptions);
        Assert.True(health.CanManualRerun);
        Assert.Contains("Vendor compliance evidence", health.ActionableReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowSubprocessArtifactMapper_SB11_INV_001_resolves_explicit_mappings_without_dispatch_partials()
    {
        var workflowExpectation = new ProcessArtifactExpectation
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = Guid.NewGuid(),
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Vendor remittance packet",
            WorkflowOutputId = "remittance-output",
            WorkflowOutputKind = WorkflowArtifactKind.Json
        };
        var workflowArtifact = new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            WorkflowRunId.New(),
            WorkflowArtifactKind.Json,
            new WorkflowNodeId("remittance-output"),
            "Vendor remittance packet",
            "application/json",
            "workflow/remittance.json",
            "Vendor remittance packet emitted from workflow node.",
            DateTimeOffset.UtcNow);

        var workflowResult = WorkflowSubprocessArtifactMapper.ResolveWorkflowArtifactExpectation(
            [workflowExpectation],
            [workflowArtifact],
            ProcessArtifactKind.Deliverable,
            workflowArtifact,
            out var workflowDiagnostic);

        var childExpectationId = Guid.NewGuid();
        var subprocessExpectation = new ProcessArtifactExpectation
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = Guid.NewGuid(),
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Signed vendor attestation",
            SubprocessChildArtifactExpectationId = childExpectationId
        };
        var subprocessArtifact = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = childExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Signed vendor attestation",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var subprocessResult = WorkflowSubprocessArtifactMapper.ResolveSubprocessSourceArtifact(
            [subprocessArtifact],
            [subprocessExpectation],
            subprocessExpectation,
            out var subprocessDiagnostic);

        Assert.Equal(workflowExpectation.Id, workflowResult?.Id);
        Assert.Equal(string.Empty, workflowDiagnostic);
        Assert.Equal(subprocessArtifact.Id, subprocessResult?.Id);
        Assert.Equal(string.Empty, subprocessDiagnostic);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(ProcessRunStatus.Active, true)]
    [InlineData(ProcessRunStatus.Blocked, true)]
    [InlineData(ProcessRunStatus.Failed, true)]
    [InlineData(ProcessRunStatus.Completed, false)]
    [InlineData(ProcessRunStatus.Cancelled, false)]
    public void IsRunEligibleForDispatchCandidate_allows_failed_run_recovery_without_reopening_closed_runs(
        ProcessRunStatus? runStatus,
        bool expected)
    {
        var eligible = ProcessRunAutomationDispatchService.IsRunEligibleForDispatchCandidate(runStatus);

        Assert.Equal(expected, eligible);
    }

    [Fact]
    public void ApplyProjectStructureReadAccess_adds_project_scoped_read_access()
    {
        var projectId = Guid.NewGuid();
        var agentEditor = new AgentEditorModel();

        var changed = ProcessRunAutomationDispatchService.ApplyProjectStructureReadAccess(agentEditor, projectId);

        Assert.True(changed);
        Assert.True(agentEditor.ProjectStructureAccess.CanRead);
        Assert.False(agentEditor.ProjectStructureAccess.CanWrite);
        Assert.False(agentEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.Equal([projectId], agentEditor.ProjectStructureAccess.AllowedProjectIds);
    }

    [Fact]
    public void ApplyProjectStructureReadAccess_preserves_existing_allow_all_scope()
    {
        var agentEditor = new AgentEditorModel
        {
            ProjectStructureAccess = new AgentProjectStructureAccessSettings
            {
                CanRead = true,
                AllowAllProjects = true
            }
        };

        var changed = ProcessRunAutomationDispatchService.ApplyProjectStructureReadAccess(agentEditor, Guid.NewGuid());

        Assert.False(changed);
        Assert.True(agentEditor.ProjectStructureAccess.CanRead);
        Assert.True(agentEditor.ProjectStructureAccess.AllowAllProjects);
        Assert.Empty(agentEditor.ProjectStructureAccess.AllowedProjectIds);
    }

    [Fact]
    public void ResolveBlockingAutomationExecutionRunId_returns_latest_fresh_active_automation_run()
    {
        var olderRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Preparing, null) with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2)
        };
        var latestRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Running, null) with
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
            CreateExecutionRun("agent-run-debug", ProcessAutomationExecutionState.Running, null) with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Preparing, null) with
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
        var staleRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Preparing, null) with
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
        var quietRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Running, null) with
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
    public void ResolveBlockingAutomationExecutionRunId_SB09_INV_001_ignores_active_runs_from_previous_attempt_window()
    {
        var attemptStartedAtUtc = DateTimeOffset.Parse("2026-04-19T09:18:25+00:00");
        var previousAttemptRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Running, null) with
        {
            CreatedAtUtc = attemptStartedAtUtc.AddMinutes(-20),
            StartedAtUtc = attemptStartedAtUtc.AddMinutes(-20),
            UpdatedAtUtc = attemptStartedAtUtc.AddMinutes(2)
        };

        var blockingRunId = ProcessRunAutomationDispatchService.ResolveBlockingAutomationExecutionRunId(
            CreateStepRun(ProcessStepRunStatus.InProgress, attemptStartedAtUtc),
            [previousAttemptRun],
            attemptStartedAtUtc.AddMinutes(3));

        Assert.Null(blockingRunId);
    }

    [Fact]
    public void HasPriorNoProgressRetrySignal_SB09_INV_001_detects_repeated_fingerprint_after_restart()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var priorExecutionRunId = Guid.NewGuid();
        var currentExecutionRunId = Guid.NewGuid();
        var signal = new ProcessRunAutomationDispatchService.NoProgressRetrySignal(
            "sb09-no-progress-fingerprint",
            currentExecutionRunId,
            "tool-signature",
            "artifact-validation-fingerprint",
            "mutation-delta:none",
            "proof-delta:none");
        var priorEntry = new ProcessJournalEntry
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            EventType = ProcessRuntimeEventTypes.NoProgressRetryObserved,
            Title = "No-progress retry observed",
            CorrelationId = signal.Fingerprint,
            ReplayContextJson = JsonSerializer.Serialize(
                new
                {
                    ExecutionRunId = priorExecutionRunId,
                    signal.Fingerprint,
                    signal.ToolSignature,
                    signal.ArtifactValidationFingerprint,
                    signal.MutationDelta,
                    signal.ProofDelta
                })
        };
        var serializedJournal = JsonSerializer.Serialize(new[] { priorEntry });
        var reloadedJournal = JsonSerializer.Deserialize<List<ProcessJournalEntry>>(serializedJournal)
            ?? throw new InvalidOperationException("No-progress journal reload failed.");

        Assert.True(ProcessRunAutomationDispatchService.HasPriorNoProgressRetrySignal(reloadedJournal, signal));

        var duplicateProcessingSignal = signal with { ExecutionRunId = priorExecutionRunId };
        Assert.False(ProcessRunAutomationDispatchService.HasPriorNoProgressRetrySignal(reloadedJournal, duplicateProcessingSignal));
    }

    [Fact]
    public async Task ProcessDispatchLeaseHeartbeat_renews_outer_and_step_claims_during_long_work()
    {
        var simulatedLeaseDuration = TimeSpan.FromMilliseconds(60);
        var stepRenewals = 0;
        var outerRenewals = 0;

        await using var heartbeat = ProcessDispatchLeaseHeartbeat.Start(
            Guid.NewGuid(),
            TimeSpan.FromMilliseconds(10),
            token =>
            {
                token.ThrowIfCancellationRequested();
                Interlocked.Increment(ref outerRenewals);
                Interlocked.Increment(ref stepRenewals);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        await Task.Delay(
            TimeSpan.FromMilliseconds(simulatedLeaseDuration.TotalMilliseconds * 3),
            heartbeat.DispatchCancellationToken);

        heartbeat.ThrowIfClaimLost();
        Assert.True(stepRenewals >= 3, $"Expected at least 3 renewals, got {stepRenewals}.");
        Assert.Equal(stepRenewals, outerRenewals);
    }

    [Fact]
    public async Task ProcessDispatchLeaseHeartbeat_cancels_dispatch_when_renewal_fails()
    {
        var stepRunId = Guid.NewGuid();
        var attempts = 0;

        await using var heartbeat = ProcessDispatchLeaseHeartbeat.Start(
            stepRunId,
            TimeSpan.FromMilliseconds(10),
            _ =>
            {
                Interlocked.Increment(ref attempts);
                throw new InvalidOperationException("lease renewal rejected");
            },
            CancellationToken.None);

        await WaitForHeartbeatLossAsync(heartbeat);

        Assert.True(heartbeat.DispatchCancellationToken.IsCancellationRequested);
        Assert.True(attempts >= 1);
        var exception = Assert.Throws<ProcessDispatchClaimLostException>(heartbeat.ThrowIfClaimLost);
        Assert.Equal(stepRunId, exception.StepRunId);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task LoadLatestManualRecoveryDirective_filters_started_at_with_postgresql()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(TestApplicationBootstrap.ModuleAssemblies);
        await using var database = PostgresTestDatabaseLease.Create("processrunautomationdispatchservicetests");

        var options = database.CreateAppDbContextOptions();
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var runId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var definitionVersionId = Guid.NewGuid();
        var stepDefinitionId = Guid.NewGuid();
        var stepStartedAtUtc = DateTimeOffset.Parse("2026-04-27T12:00:00+00:00");

        await dbContext.Set<ProcessDefinition>().AddAsync(new ProcessDefinition
        {
            Id = definitionId,
            Name = "Recovery directive test",
            Slug = $"recovery-directive-{Guid.NewGuid():N}",
            CreatedAtUtc = stepStartedAtUtc,
            UpdatedAtUtc = stepStartedAtUtc
        });
        await dbContext.Set<ProcessDefinitionVersion>().AddAsync(new ProcessDefinitionVersion
        {
            Id = definitionVersionId,
            ProcessDefinitionId = definitionId,
            Status = ProcessVersionStatus.Published,
            CreatedAtUtc = stepStartedAtUtc,
            UpdatedAtUtc = stepStartedAtUtc,
            PublishedAtUtc = stepStartedAtUtc,
            PublishedBy = "integration-tests"
        });
        await dbContext.Set<ProcessStepDefinition>().AddAsync(new ProcessStepDefinition
        {
            Id = stepDefinitionId,
            ProcessDefinitionVersionId = definitionVersionId,
            Key = "recovery-directive-step",
            Title = "Recovery directive step"
        });
        await dbContext.Set<ProcessRun>().AddAsync(new ProcessRun
        {
            Id = runId,
            ProcessDefinitionId = definitionId,
            ProcessDefinitionVersionId = definitionVersionId,
            Name = "Recovery directive run",
            Status = ProcessRunStatus.Active,
            TriggerReason = "Integration test",
            CreatedAtUtc = stepStartedAtUtc,
            UpdatedAtUtc = stepStartedAtUtc,
            StartedAtUtc = stepStartedAtUtc
        });
        await dbContext.Set<ProcessStepRun>().AddAsync(new ProcessStepRun
        {
            Id = stepRunId,
            ProcessRunId = runId,
            StepDefinitionId = stepDefinitionId,
            Title = "Recovery directive step",
            Status = ProcessStepRunStatus.InProgress,
            StartedAtUtc = stepStartedAtUtc
        });
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

    private static async Task WaitForHeartbeatLossAsync(ProcessDispatchLeaseHeartbeat heartbeat)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (!heartbeat.ClaimLost && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(heartbeat.ClaimLost);
    }

    [Fact]
    public void BuildCanonicalProjectStructureGroundingSql_uses_postgresql_safe_identifiers_and_values()
    {
        var sql = InvokeBuildCanonicalProjectStructureGroundingSql();

        Assert.Contains("FROM \"Workbench_ProjectObjects\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"ProjectId\" = @projectId", sql, StringComparison.Ordinal);
        Assert.Contains("\"IsSystemManaged\" = FALSE", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("$projectId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("= 0", sql, StringComparison.Ordinal);
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
    public void TryResolveExternalTargetHintFromProjectStructureGrounding_trims_project_note_metadata_tail()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "TryResolveExternalTargetHintFromProjectStructureGrounding",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveExternalTargetHintFromProjectStructureGrounding method was not found.");
        object?[] arguments =
        [
            "Requirements from project-level planning context:\n- output must be placed in C:\\programovani\\csharp\\workflow (note:output-path); type: ProjectBlock/note",
            string.Empty,
            string.Empty
        ];

        var resolved = (bool)method.Invoke(null, arguments)!;

        Assert.True(resolved);
        Assert.Equal("C:\\programovani\\csharp\\workflow", arguments[1]);
        Assert.Equal("external-target/C/programovani/csharp/workflow", arguments[2]);
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

    [Theory]
    [InlineData("external-source-root")]
    [InlineData("runtime")]
    public void IsProjectLevelPlanningContextNode_includes_source_and_runtime_context(string objectSubtype)
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var nodeType = serviceType.GetNestedType(
            "ProjectStructureGroundingNodeData",
            BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProjectStructureGroundingNodeData type was not found.");
        var constructor = nodeType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(item => item.GetParameters().Length == 9);
        var node = constructor.Invoke(
        [
            "custom:source-root",
            "project:demo",
            "External",
            objectSubtype,
            "Scenario source root",
            @"C:\programovani\candoitall-dev-55-output\scenario-01-dotnet-trailhead-snack-box",
            "",
            @"External generated app source root: C:\programovani\candoitall-dev-55-output\scenario-01-dotnet-trailhead-snack-box",
            "{}"
        ]);
        var method = serviceType.GetMethod(
            "IsProjectLevelPlanningContextNode",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("IsProjectLevelPlanningContextNode method was not found.");

        var included = (bool)method.Invoke(null, [node])!;

        Assert.True(included);
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
    public void PruneAllowedExternalTargetAliasesForCurrentRun_keeps_product_root_when_descendant_product_path_exists()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-192839/product",
            "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-192839/product/tests/product"
        ]);

        Assert.Contains(
            "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-192839/product",
            aliases);
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
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_collapsed_project_structure_heading()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/candoitall-processes2-dotnet-cli-a Architecture: - .NET console application. - Solution name: TodoSummary."
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/candoitall-processes2-dotnet-cli-a", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_project_structure_node_id_annotation()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/candoitall-processes1-blazor-counter-a (custom:7e4daf18f7cd439abc568402150a1889"
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/candoitall-processes1-blazor-counter-a", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_inline_generated_source_sentence()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/dotnet/HarborShiftScheduler. All generated app source belongs under this directory."
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/dotnet/HarborShiftScheduler", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_inline_hosting_sentence()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/dotnet-demo/output. - Hosting target: ordinary static web hosting."
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/dotnet-demo/output", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_approved_product_root_annotation()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653 Approved product root for this run"
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_escaped_newline_generated_source_sentence()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/dotnet/HarborShiftScheduler./nAll app source belongs under this directory."
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/dotnet/HarborShiftScheduler", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_escaped_newline_mapped_label()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/candoitall-dev-output/raincheck-cards-js/nMapped alias for C:\\programovani\\candoitall-dev-output\\raincheck-cards-js.",
            "external-target/C/programovani/candoitall-dev-output/raincheck-cards-js Workspace alias: external-target/C/programovani/candoitall-dev-output/raincheck-cards-js All generated app source belongs under"
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/candoitall-dev-output/raincheck-cards-js", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_escaped_newline_bulleted_source_annotation()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/candoitall-dev-55-output/scenario-01-dotnet-trailhead-snack-box\\n- Source root (mapped alias): external-target/C/programovani/candoitall-dev-55-output/scenario-01-dotnet-trailhead-snack-box - Expected project source path for web project (relative to source root): src/TrailheadSnackBox.Web - Run command (assumption): dotnet run --project src/TrailheadSnackBox.Web"
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/candoitall-dev-55-output/scenario-01-dotnet-trailhead-snack-box", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_escaped_newline_exact_archetype()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/candoitall-dev-output/parcel-gate-v3-csharp./nExact archetype: ASP.NET Core minimal API in Program.cs."
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/candoitall-dev-output/parcel-gate-v3-csharp", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_removes_project_level_parent_after_prose_strip()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/candoitall-dev-output and one business-case analysis scenario without stack-specific process-core assumptions",
            "external-target/C/programovani/candoitall-dev-output/route-ration-v3-csharp"
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/candoitall-dev-output/route-ration-v3-csharp", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_adjacent_business_analysis_label()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/candoitall-dev-55-output/scenario-01-dotnet-trailhead-snack-box Business-analysis ro"
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/candoitall-dev-55-output/scenario-01-dotnet-trailhead-snack-box", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_strips_inline_app_project_path_annotation()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/candoitall-dev-55-output/scenario-01-dotnet-trailhead-snack-box - App project path (relative to source root): src/TrailheadSnackBox.Web - Run command (expected): dotnet run --project src/TrailheadSnackBox.Web - Expected base URL (primary assumption): https://localhost:5001"
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/candoitall-dev-55-output/scenario-01-dotnet-trailhead-snack-box", alias);
    }

    [Fact]
    public void PruneAllowedExternalTargetAliasesForCurrentRun_keeps_folder_when_keep_file_is_referenced()
    {
        var aliases = ProcessRunAutomationDispatchService.PruneAllowedExternalTargetAliasesForCurrentRun(
        [
            "external-target/C/programovani/candoitall-dev-55-output/business-analysis",
            "external-target/C/programovani/candoitall-dev-55-output/business-analysis/.keep and"
        ]);

        var alias = Assert.Single(aliases);
        Assert.Equal("external-target/C/programovani/candoitall-dev-55-output/business-analysis", alias);
    }

    [Fact]
    public void TryMapAbsoluteExternalPathToAlias_strips_inline_generated_source_sentence()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "TryMapAbsoluteExternalPathToAlias",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryMapAbsoluteExternalPathToAlias method was not found.");
        object?[] arguments =
        [
            @"C:\programovani\dotnet\HarborShiftScheduler. All generated app source belongs under this directory.",
            string.Empty
        ];

        var mapped = (bool)method.Invoke(null, arguments)!;

        Assert.True(mapped);
        Assert.Equal("external-target/C/programovani/dotnet/HarborShiftScheduler", arguments[1]);
    }

    [Fact]
    public void TryMapAbsoluteExternalPathToAlias_strips_inline_hosting_sentence()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "TryMapAbsoluteExternalPathToAlias",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryMapAbsoluteExternalPathToAlias method was not found.");
        object?[] arguments =
        [
            @"C:\programovani\dotnet-demo\output. - Hosting target: ordinary static web hosting.",
            string.Empty
        ];

        var mapped = (bool)method.Invoke(null, arguments)!;

        Assert.True(mapped);
        Assert.Equal("external-target/C/programovani/dotnet-demo/output", arguments[1]);
    }

    [Fact]
    public void TryMapAbsoluteExternalPathToAlias_strips_project_level_and_scenario_tail()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "TryMapAbsoluteExternalPathToAlias",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryMapAbsoluteExternalPathToAlias method was not found.");
        object?[] arguments =
        [
            @"C:\programovani\candoitall-dev-output and one business-case analysis scenario without stack-specific process-core assumptions",
            string.Empty
        ];

        var mapped = (bool)method.Invoke(null, arguments)!;

        Assert.True(mapped);
        Assert.Equal("external-target/C/programovani/candoitall-dev-output", arguments[1]);
    }

    [Fact]
    public void TryMapAbsoluteExternalPathToAlias_strips_escaped_newline_mapped_label()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "TryMapAbsoluteExternalPathToAlias",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryMapAbsoluteExternalPathToAlias method was not found.");
        object?[] arguments =
        [
            @"C:\programovani\candoitall-dev-output\raincheck-cards-js\nMapped alias: external-target/C/programovani/candoitall-dev-output/raincheck-cards-js.",
            string.Empty
        ];

        var mapped = (bool)method.Invoke(null, arguments)!;

        Assert.True(mapped);
        Assert.Equal("external-target/C/programovani/candoitall-dev-output/raincheck-cards-js", arguments[1]);
    }

    [Fact]
    public void TryMapAbsoluteExternalPathToAlias_strips_escaped_newline_generated_source_sentence()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "TryMapAbsoluteExternalPathToAlias",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryMapAbsoluteExternalPathToAlias method was not found.");
        object?[] arguments =
        [
            @"C:\programovani\dotnet\HarborShiftScheduler.\nAll app source belongs under this directory.",
            string.Empty
        ];

        var mapped = (bool)method.Invoke(null, arguments)!;

        Assert.True(mapped);
        Assert.Equal("external-target/C/programovani/dotnet/HarborShiftScheduler", arguments[1]);
    }

    [Fact]
    public void TryMapAbsoluteExternalPathToAlias_strips_inline_app_project_path_annotation()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "TryMapAbsoluteExternalPathToAlias",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryMapAbsoluteExternalPathToAlias method was not found.");
        object?[] arguments =
        [
            @"C:\programovani\candoitall-dev-55-output\scenario-01-dotnet-trailhead-snack-box - App project path (relative to source root): src/TrailheadSnackBox.Web - Run command (expected): dotnet run --project src/TrailheadSnackBox.Web",
            string.Empty
        ];

        var mapped = (bool)method.Invoke(null, arguments)!;

        Assert.True(mapped);
        Assert.Equal("external-target/C/programovani/candoitall-dev-55-output/scenario-01-dotnet-trailhead-snack-box", arguments[1]);
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
    public void ResolveOutOfScopeExternalTargetReferenceSummary_allows_documented_current_run_output_root()
    {
        var summary = ProcessRunAutomationDispatchService.ResolveOutOfScopeExternalTargetReferenceSummary(
            """
            Run boundary:
            - Output root: C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-192839
            - Product root: C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-192839\product
            - Fresh screenshot file: artifacts/process-runs/run-001/blazor-pwa-revalidated-current.png
            """,
            ["external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-192839/product"]);

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveOutOfScopeExternalTargetReferenceSummary_allows_prohibited_sibling_parent_reference()
    {
        var summary = ProcessRunAutomationDispatchService.ResolveOutOfScopeExternalTargetReferenceSummary(
            """
            Absolute output root: C:\programovani\candoitall-dev-output\receipt-radar-csharp
            Workspace tool alias: external-target/C/programovani/candoitall-dev-output/receipt-radar-csharp
            Do not inspect or copy sibling apps in C:\programovani\candoitall-dev-output.
            """,
            ["external-target/C/programovani/candoitall-dev-output/receipt-radar-csharp"]);

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveOutOfScopeExternalTargetReferenceSummary_allows_unrelated_absolute_managed_workspace_paths()
    {
        var summary = ProcessRunAutomationDispatchService.ResolveOutOfScopeExternalTargetReferenceSummary(
            """
            Architecture context:
            - C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\postgresql\workspace\project-structure-context-brief\architecture-decision-record.md
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

    [Fact]
    public void ResolveWorkspaceToolProfile_uses_software_development_for_live_implementation_brief_with_architecture_inputs()
    {
        var profile = InvokeResolveWorkspaceToolProfile(
            new ProcessStepRun
            {
                Title = "Implement feature, tests, and migration notes",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = "Blazor Application Developer",
                RoleSnapshotSummary = "Implementation owner for code, tests, migration notes, and executable delivery proof."
            },
            new ProcessWorkBrief
            {
                Title = "Implement feature, tests, and migration notes brief",
                WorkBriefText = """
                Multi-team software delivery and release governance: Implement feature, tests, and migration notes
                Executor: Blazor Application Developer
                Inputs: Approved architecture path, scope packet, and unresolved technical questions.
                Outputs: Review-ready implementation with tests, migration notes, and rollout checklist inputs.
                Evidence: Change set, test outputs, migration steps, and touched-surface inventory.
                """,
                ExpectedOutcome = "Review-ready implementation with tests, migration notes, and rollout checklist inputs.",
                EvidenceExpectationSummary = "Implementation change set; Migration and rollout preparation checklist"
            },
            new ProcessRoleRequirement
            {
                Key = "lead-engineer",
                DisplayName = "Lead engineer",
                Purpose = "Own the change set and keep implementation evidence aligned with the approved architecture and release boundary.",
                StaffingIntent = "Build-capable engineering owner for the working change set and adjacent test evidence.",
                PreferredExecutorKind = "person-or-agent",
                SnapshotSummary = "Implementation owner for code, tests, migration notes, and executable delivery proof."
            },
            [
                (ProcessArtifactKind.Deliverable, "Implementation change set", "Must be linked to tests, migration notes, and touched-surface inventory."),
                (ProcessArtifactKind.Checklist, "Migration and rollout preparation checklist", "Must name data changes, operational preconditions, and rollback steps.")
            ]);

        Assert.Equal(AgentWorkspaceToolProfileKind.SoftwareDevelopment, profile);
    }

    [Fact]
    public void ResolveWorkspaceToolProfile_uses_software_development_for_repair_work_even_with_qa_inputs()
    {
        var profile = InvokeResolveWorkspaceToolProfile(
            new ProcessStepRun
            {
                Title = "Repair validation findings",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = "Blazor Application Developer",
                RoleSnapshotSummary = "Implementation owner for code, tests, migration notes, and executable delivery proof."
            },
            new ProcessWorkBrief
            {
                Title = "Repair validation findings brief",
                WorkBriefText = """
                Inputs: QA repair-required disposition, reviewed implementation package, and failing proof details.
                Outputs: Repaired change set and validation notes ready for QA recheck.
                Instructions: Repair concrete defects, missing workflows, failed validation, or proof gaps identified by QA.
                """,
                ExpectedOutcome = "Repaired change set and validation notes ready for QA recheck.",
                EvidenceExpectationSummary = "Quality repair change set"
            },
            new ProcessRoleRequirement
            {
                Key = "lead-engineer",
                DisplayName = "Lead engineer",
                Purpose = "Own implementation repairs.",
                StaffingIntent = "Build-capable engineering owner for concrete repair work.",
                PreferredExecutorKind = "person-or-agent",
                SnapshotSummary = "Implementation owner for code, tests, migration notes, and executable delivery proof."
            },
            [
                (ProcessArtifactKind.Deliverable, "Quality repair change set", "Must identify changed files, rerun validation, and remaining risks.")
            ]);

        Assert.Equal(AgentWorkspaceToolProfileKind.SoftwareDevelopment, profile);
    }

    [Fact]
    public void ResolveWorkspaceToolProfile_keeps_architecture_review_for_architect_step()
    {
        var profile = InvokeResolveWorkspaceToolProfile(
            new ProcessStepRun
            {
                Title = "Review architecture and canonical-model impact",
                StepKind = ProcessStepKind.Review,
                CurrentExecutorName = ".NET Solution Architect",
                RoleSnapshotSummary = "Architecture authority for system boundaries, irreversible decisions, and cross-domain technical coherence."
            },
            new ProcessWorkBrief
            {
                Title = "Review architecture and canonical-model impact brief",
                WorkBriefText = "Inputs: Scope packet, touched modules, data-flow map, and integration concerns.",
                ExpectedOutcome = "Approved architecture path with explicit trade-offs and rejected alternatives.",
                EvidenceExpectationSummary = "Architecture decision record; Project structure context brief"
            },
            new ProcessRoleRequirement
            {
                Key = "solution-architect",
                DisplayName = "Solution architect",
                Purpose = "Protect maintainability and operability by reviewing design options.",
                StaffingIntent = "A senior technical authority who can reason across components, environments, data boundaries, and operational consequences.",
                PreferredExecutorKind = "person-or-agent",
                SnapshotSummary = "Architecture authority for system boundaries, irreversible decisions, and cross-domain technical coherence."
            },
            [
                (ProcessArtifactKind.Brief, "Project structure context brief", "Must capture resolved working directory and touched modules."),
                (ProcessArtifactKind.Decision, "Architecture decision record", "Must capture selected option, rejected options, and source-of-truth choice.")
            ]);

        Assert.Equal(AgentWorkspaceToolProfileKind.ArchitectureReview, profile);
    }

    [Fact]
    public void ResolveWorkspaceToolProfile_keeps_quality_validation_for_qa_step()
    {
        var profile = InvokeResolveWorkspaceToolProfile(
            new ProcessStepRun
            {
                Title = "Run QA validation and browser proof",
                StepKind = ProcessStepKind.Review,
                CurrentExecutorName = ".NET QA Review Lead",
                RoleSnapshotSummary = "Quality owner for coverage depth, evidence quality, and release confidence."
            },
            new ProcessWorkBrief
            {
                Title = "Run QA validation and browser proof brief",
                WorkBriefText = "Inputs: Peer-reviewed change set, changed-surface inventory, and release-scope assumptions.",
                ExpectedOutcome = "Targeted QA result with screenshots, regressions, residual quality risk, and an explicit accepted or repair-required branch.",
                EvidenceExpectationSummary = "Regression evidence pack"
            },
            new ProcessRoleRequirement
            {
                Key = "qa-lead",
                DisplayName = "QA lead",
                Purpose = "Challenge whether the delivered change is proven enough for its risk profile.",
                StaffingIntent = "A senior test leader able to shape strategy, select evidence depth, and identify where automation is not enough.",
                PreferredExecutorKind = "person-or-agent",
                SnapshotSummary = "Quality owner for coverage depth, evidence quality, and release confidence."
            },
            [
                (ProcessArtifactKind.Evidence, "Regression evidence pack", "Must include browser proof and regression logs.")
            ]);

        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, profile);
    }

    [Fact]
    public void ResolveWorkspaceToolProfile_keeps_quality_validation_for_runtime_cleanup_handoff()
    {
        var profile = InvokeResolveWorkspaceToolProfile(
            new ProcessStepRun
            {
                Title = "Cleanup and handoff",
                StepKind = ProcessStepKind.End,
                CurrentExecutorName = "Runtime App Screenshot Capture Agent"
            },
            new ProcessWorkBrief
            {
                Title = "Cleanup and handoff",
                WorkBriefText = """
                Read upstream screenshot review findings and project image asset storage receipt.
                Stop the managed app process if one is still live and write the handoff.
                The upstream capture step already produced browser_snapshot, browser_take_screenshot, and browser_console_messages.
                """,
                ExpectedOutcome = "Single page screenshot handoff with cleanup status.",
                EvidenceExpectationSummary = "Single page screenshot handoff"
            },
            new ProcessRoleRequirement
            {
                Key = "app-screenshot-capture-agent",
                DisplayName = "App screenshot capture agent",
                Purpose = "Capture screenshots and clean up runnable app processes."
            },
            [
                (ProcessArtifactKind.Evidence, "Single page screenshot handoff", "Must include final route, screenshot artifact, asset node id, cleanup status, and blockers.")
            ]);

        Assert.Equal(AgentWorkspaceToolProfileKind.QualityValidation, profile);
    }

    [Fact]
    public void ResolveRequiredToolNames_for_dotnet_solution_setup_scaffold_step_skips_runtime_validation()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = ".NET solution setup subprocess",
                Slug = "dotnet-solution-setup"
            },
            new ProcessStepDefinition
            {
                Key = "create-dotnet-project",
                Title = "Create solution and .NET app project",
                StepKind = ProcessStepKind.Work
            },
            new ProcessStepRun
            {
                Title = "Create solution and .NET app project",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = ".NET Application Developer"
            },
            new ProcessWorkBrief
            {
                Title = "Create solution and .NET app project",
                WorkBriefText = "Create a Blazor Web App solution skeleton. Build, test, runtime smoke, and browser proof are later validation steps.",
                ExpectedOutcome = "Solution and app project are present.",
                EvidenceExpectationSummary = "Solution skeleton change set"
            },
            [
                (ProcessArtifactKind.Deliverable, "Solution skeleton change set", "Must include the .slnx or .sln solution file, requested .NET app project, selected template proof, and solution membership proof.")
            ]);

        Assert.Contains("workspace_dotnet_new", tools);
        Assert.DoesNotContain("workspace_write_file", tools);
        Assert.DoesNotContain("workspace_dotnet_build", tools);
        Assert.DoesNotContain("workspace_dotnet_test", tools);
        Assert.DoesNotContain("workspace_dotnet_run", tools);
        Assert.DoesNotContain("browser_snapshot", tools);
        Assert.DoesNotContain("browser_take_screenshot", tools);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_require_project_structure_writeback_for_applicable_intake_wording()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = "Blazor app delivery",
                Slug = "blazor-app-delivery"
            },
            new ProcessStepDefinition
            {
                Key = "resolve-blazor-delivery-contract",
                Title = "Resolve Blazor delivery contract",
                StepKind = ProcessStepKind.Review,
                EvidenceContractSummary = "Capture process artifact paths and project-structure writeback references as applicable."
            },
            new ProcessStepRun
            {
                Title = "Resolve Blazor delivery contract",
                StepKind = ProcessStepKind.Review,
                CurrentExecutorName = "Blazor delivery manager"
            },
            new ProcessWorkBrief
            {
                Title = "Resolve Blazor delivery contract",
                WorkBriefText = "Read the project structure context and produce the delivery contract. Include project-structure writeback references as applicable for later result-recording steps.",
                ExpectedOutcome = "Delivery contract is ready for implementation.",
                EvidenceExpectationSummary = "Delivery contract"
            },
            [
                (ProcessArtifactKind.Brief, "Blazor delivery contract", "Must include target work item, selected app mode, product root, acceptance criteria, and evidence plan.")
            ],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver the generated application showcase.",
                new ProcessProjectStructureContext
                {
                    ProjectId = Guid.NewGuid(),
                    NodeId = "custom:basic-app",
                    NodeTitle = "Basic App"
                }));

        Assert.Contains("project_structure_read", tools);
        Assert.DoesNotContain("project_structure_node_create", tools);
        Assert.DoesNotContain("project_structure_asset_create", tools);
    }

    [Fact]
    public void ResolveRequiredToolNames_for_blazor_revalidation_does_not_require_project_structure_writeback_without_external_action()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = "Blazor app delivery",
                Slug = "blazor-app-delivery"
            },
            new ProcessStepDefinition
            {
                Key = "revalidate-blazor-repair",
                Title = "Revalidate Blazor repair",
                StepKind = ProcessStepKind.Review,
                EvidenceContractSummary = "Record commands, files, URLs, browser proof outputs, console messages, errors, and assumptions. Result-recording steps own project-structure updates; validation remains read-only.",
                AllowedOperations =
                [
                    ProcessStepOperation.ReadProcessContext,
                    ProcessStepOperation.ReadProjectStructure,
                    ProcessStepOperation.ReadUpstreamArtifacts,
                    ProcessStepOperation.RunValidation,
                    ProcessStepOperation.LaunchRuntime,
                    ProcessStepOperation.CaptureRuntimeProof,
                    ProcessStepOperation.WriteManagedProcessArtifacts
                ],
                OperationTargetScope = ProcessStepTargetScope.ExternalProductTargetReadOnly
            },
            new ProcessStepRun
            {
                Title = "Revalidate Blazor repair",
                StepKind = ProcessStepKind.Review,
                CurrentExecutorName = ".NET QA Review Lead"
            },
            new ProcessWorkBrief
            {
                Title = "Revalidate Blazor repair",
                WorkBriefText = "Run validation against the delivered Blazor WebAssembly PWA app. Do not call project_structure_node_create or project_structure_asset_create from this validation step; result writeback belongs to result-recording steps.",
                ExpectedOutcome = "Fresh Blazor runtime and browser proof is recorded.",
                EvidenceExpectationSummary = "Runtime evidence pack"
            },
            [
                (ProcessArtifactKind.Evidence, "Blazor runtime evidence pack", "Must include fresh dotnet build/test results as applicable, fresh browser proof with app startup receipt, fresh browser_snapshot output, fresh browser_console_messages output showing no active JavaScript/runtime errors, visible behavior assertions, and cleanup receipt."),
                (ProcessArtifactKind.Brief, "Validation self-review summary", "Must state validated routes, browser proof captured, console status, failed assertions if any, and whether acceptance criteria are satisfied.")
            ],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver a Blazor WebAssembly PWA from project structure.",
                new ProcessProjectStructureContext
                {
                    ProjectId = Guid.NewGuid(),
                    NodeId = "custom:main-app",
                    NodeTitle = "Main app"
                }));

        Assert.Contains("project_structure_read", tools);
        Assert.Contains("browser_snapshot", tools);
        Assert.Contains("browser_take_screenshot", tools);
        Assert.Contains("browser_console_messages", tools);
        Assert.DoesNotContain("project_structure_node_create", tools);
        Assert.DoesNotContain("project_structure_asset_create", tools);
    }

    [Fact]
    public void ResolveRequiredToolNames_for_result_recording_requires_project_structure_writeback_when_external_action_allowed()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = "Blazor app delivery",
                Slug = "blazor-app-delivery"
            },
            new ProcessStepDefinition
            {
                Key = "record-repaired-blazor-results",
                Title = "Record repaired Blazor results and evidence index",
                StepKind = ProcessStepKind.Delivery,
                EvidenceContractSummary = "Record project-structure writeback references.",
                AllowedOperations =
                [
                    ProcessStepOperation.ReadProcessContext,
                    ProcessStepOperation.ReadProjectStructure,
                    ProcessStepOperation.ReadUpstreamArtifacts,
                    ProcessStepOperation.WriteManagedProcessArtifacts,
                    ProcessStepOperation.ExecuteExternalAction
                ],
                OperationTargetScope = ProcessStepTargetScope.ExternalActionControlled
            },
            new ProcessStepRun
            {
                Title = "Record repaired Blazor results and evidence index",
                StepKind = ProcessStepKind.Delivery,
                CurrentExecutorName = "Blazor delivery manager"
            },
            new ProcessWorkBrief
            {
                Title = "Record repaired Blazor results and evidence index",
                WorkBriefText = "Write the final verdict back into project structure. Must call project_structure_node_create before completing.",
                ExpectedOutcome = "Run evidence index and project-structure writeback summary were prepared.",
                EvidenceExpectationSummary = "Repaired run evidence index"
            },
            [
                (ProcessArtifactKind.Evidence, "Repaired run evidence index", "Must confirm result/evidence was written back to project structure through actual project_structure_* tool calls, including project_structure_node_create receipt and screenshot/evidence project_structure_asset_create ids where applicable.")
            ],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver a Blazor WebAssembly PWA from project structure.",
                new ProcessProjectStructureContext
                {
                    ProjectId = Guid.NewGuid(),
                    NodeId = "custom:main-app",
                    NodeTitle = "Main app"
                }));

        Assert.Contains("project_structure_read", tools);
        Assert.Contains("project_structure_node_create", tools);
        Assert.Contains("project_structure_asset_create", tools);
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_uses_business_analysis_profile_for_external_result_writeback_contract()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidate(
            new ProcessDefinition
            {
                Name = "Blazor app delivery",
                Slug = "blazor-app-delivery"
            },
            new ProcessStepDefinition
            {
                Key = "record-repaired-blazor-results",
                Title = "Record repaired Blazor results and evidence index",
                StepKind = ProcessStepKind.End,
                AllowedOperations =
                [
                    ProcessStepOperation.ReadProcessContext,
                    ProcessStepOperation.ReadProjectStructure,
                    ProcessStepOperation.ReadUpstreamArtifacts,
                    ProcessStepOperation.WriteManagedProcessArtifacts,
                    ProcessStepOperation.ExecuteExternalAction
                ],
                OperationTargetScope = ProcessStepTargetScope.ExternalActionControlled
            },
            new ProcessStepRun
            {
                Title = "Record repaired Blazor results and evidence index",
                StepKind = ProcessStepKind.End,
                CurrentExecutorName = "Blazor delivery manager"
            },
            new ProcessWorkBrief
            {
                Title = "Record repaired Blazor results and evidence index",
                WorkBriefText = "Write the final verdict back into project structure through project_structure_node_create and project_structure_asset_create.",
                ExpectedOutcome = "Run evidence index and project-structure writeback summary were prepared.",
                EvidenceExpectationSummary = "Repaired run evidence index"
            },
            [
                (ProcessArtifactKind.Evidence, "Repaired run evidence index", "Must confirm project-structure writeback ids and managed evidence paths.")
            ],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver a Blazor WebAssembly PWA from project structure.",
                new ProcessProjectStructureContext
                {
                    ProjectId = Guid.NewGuid(),
                    NodeId = "custom:main-app",
                    NodeTitle = "Main app"
                }));

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                null,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.Equal(
            "ExternalAction",
            document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepExecutionBoundaryMetadataKey).GetString());
        Assert.Equal(
            AgentWorkspaceToolAccessProfiles.BusinessAnalysisProfileKey,
            document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessWorkspaceToolProfileMetadataKey).GetString());
        Assert.Contains(
            "managed artifact writeback",
            document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessCooperationSummaryMetadataKey).GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildExecutionPrompt_uses_boundary_aware_profile_for_external_result_writeback_contract()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPrompt = serviceType.GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate(
            new ProcessDefinition
            {
                Name = "Blazor app delivery",
                Slug = "blazor-app-delivery"
            },
            new ProcessStepDefinition
            {
                Key = "record-repaired-blazor-results",
                Title = "Record repaired Blazor results and evidence index",
                StepKind = ProcessStepKind.End,
                AllowedOperations =
                [
                    ProcessStepOperation.ReadProcessContext,
                    ProcessStepOperation.ReadProjectStructure,
                    ProcessStepOperation.ReadUpstreamArtifacts,
                    ProcessStepOperation.WriteManagedProcessArtifacts,
                    ProcessStepOperation.ExecuteExternalAction
                ],
                OperationTargetScope = ProcessStepTargetScope.ExternalActionControlled
            },
            new ProcessStepRun
            {
                Title = "Record repaired Blazor results and evidence index",
                StepKind = ProcessStepKind.End,
                CurrentExecutorName = "Blazor delivery manager"
            },
            new ProcessWorkBrief
            {
                Title = "Record repaired Blazor results and evidence index",
                WorkBriefText = "Write the final verdict back into project structure through project_structure_node_create and project_structure_asset_create.",
                ExpectedOutcome = "Run evidence index and project-structure writeback summary were prepared.",
                EvidenceExpectationSummary = "Repaired run evidence index"
            },
            [
                (ProcessArtifactKind.Evidence, "Repaired run evidence index", "Must confirm project-structure writeback ids and managed evidence paths.")
            ],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver a Blazor WebAssembly PWA from project structure.",
                new ProcessProjectStructureContext
                {
                    ProjectId = Guid.NewGuid(),
                    NodeId = "custom:main-app",
                    NodeTitle = "Main app"
                }));

        var prompt = (string)buildExecutionPrompt.Invoke(null, [candidate])!;

        Assert.Contains("- Workspace tool profile: business-analysis", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("- Workspace tool profile: software-development", prompt, StringComparison.Ordinal);
        Assert.Contains("project_structure_node_create", prompt, StringComparison.Ordinal);
        Assert.Contains("project_structure_asset_create", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiresConcreteImplementationProof_exempts_dotnet_solution_setup_scaffold_step()
    {
        var requiresProof = InvokeRequiresConcreteImplementationProof(
            new ProcessDefinition
            {
                Name = ".NET solution setup subprocess",
                Slug = "dotnet-solution-setup"
            },
            new ProcessStepDefinition
            {
                Key = "create-dotnet-project",
                Title = "Create solution and .NET app project",
                StepKind = ProcessStepKind.Work
            },
            new ProcessStepRun
            {
                Title = "Create solution and .NET app project",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = ".NET Application Developer"
            },
            new ProcessWorkBrief
            {
                Title = "Create solution and .NET app project",
                WorkBriefText = "Create the requested solution skeleton. Build, test, runtime smoke, and browser proof belong to later validation steps.",
                ExpectedOutcome = "Solution and app project are present.",
                EvidenceExpectationSummary = "Solution skeleton change set"
            },
            [
                (ProcessArtifactKind.Deliverable, "Solution skeleton change set", "Must include the .slnx or .sln solution file, requested .NET app project, selected template proof, and solution membership proof.")
            ]);

        Assert.False(requiresProof);
    }

    [Fact]
    public void RequiresConcreteImplementationProof_keeps_generic_implementation_change_set_required()
    {
        var requiresProof = InvokeRequiresConcreteImplementationProof(
            new ProcessDefinition
            {
                Name = "Software delivery",
                Slug = "software-delivery"
            },
            new ProcessStepDefinition
            {
                Key = "implement-feature",
                Title = "Implement feature",
                StepKind = ProcessStepKind.Work
            },
            new ProcessStepRun
            {
                Title = "Implement feature",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = "Application Developer"
            },
            new ProcessWorkBrief
            {
                Title = "Implement feature",
                WorkBriefText = "Implement the requested product behavior and prove the changed files.",
                ExpectedOutcome = "Implementation is complete.",
                EvidenceExpectationSummary = "Implementation change set"
            },
            [
                (ProcessArtifactKind.Deliverable, "Implementation change set", "Must include concrete changed product files and validation evidence.")
            ]);

        Assert.True(requiresProof);
    }

    [Fact]
    public void ResolveRequiredToolNames_for_dotnet_solution_setup_scaffold_step_ignores_downstream_tool_names()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = ".NET solution setup subprocess",
                Slug = "dotnet-solution-setup"
            },
            new ProcessStepDefinition
            {
                Key = "create-dotnet-project",
                Title = "Create solution and .NET app project",
                StepKind = ProcessStepKind.Work
            },
            new ProcessStepRun
            {
                Title = "Create solution and .NET app project",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = ".NET Application Developer"
            },
            new ProcessWorkBrief
            {
                Title = "Create solution and .NET app project",
                WorkBriefText = "Create the solution and app project. Later validation steps mention workspace_dotnet_build, workspace_dotnet_test, workspace_dotnet_run, browser_snapshot, browser_take_screenshot, and browser_console_messages, but this scaffold step must not run them.",
                ExpectedOutcome = "Solution and app project are present.",
                EvidenceExpectationSummary = "Solution skeleton change set"
            },
            [
                (ProcessArtifactKind.Deliverable, "Solution skeleton change set", "Must include the .slnx or .sln solution file, requested .NET app project, selected template proof, and solution membership proof.")
            ]);

        Assert.Contains("workspace_dotnet_new", tools);
        Assert.DoesNotContain("workspace_write_file", tools);
        Assert.DoesNotContain("workspace_dotnet_build", tools);
        Assert.DoesNotContain("workspace_dotnet_test", tools);
        Assert.DoesNotContain("workspace_dotnet_run", tools);
        Assert.DoesNotContain("browser_snapshot", tools);
        Assert.DoesNotContain("browser_take_screenshot", tools);
        Assert.DoesNotContain("browser_console_messages", tools);
    }

    [Fact]
    public void ResolveRequiredToolNames_for_console_setup_validation_does_not_require_browser_tools()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = ".NET solution setup subprocess",
                Slug = "dotnet-solution-setup"
            },
            new ProcessStepDefinition
            {
                Key = "validate-first-build",
                Title = "Validate first build and test discovery",
                StepKind = ProcessStepKind.Review
            },
            new ProcessStepRun
            {
                Title = "Validate first build and test discovery",
                StepKind = ProcessStepKind.Review,
                CurrentExecutorName = ".NET QA Review Lead"
            },
            new ProcessWorkBrief
            {
                Title = "Validate first build and test discovery",
                WorkBriefText = "Validate the .NET console application with restore, build, and test. Do not start a web app, run browser proof, or take screenshots because this is a CLI app with no web UI.",
                ExpectedOutcome = "Console app scaffold is buildable and tests are discoverable.",
                EvidenceExpectationSummary = "First build and test discovery evidence"
            },
            [
                (ProcessArtifactKind.Evidence, "First build and test discovery evidence", "Must include restore/build/test command output. Browser proof is not applicable for this console application.")
            ]);

        Assert.DoesNotContain("browser_console_messages", tools);
        Assert.DoesNotContain("browser_snapshot", tools);
        Assert.DoesNotContain("browser_take_screenshot", tools);
    }

    [Fact]
    public void ResolveRequiredToolNames_for_app_startup_step_honors_negated_browser_capture()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = "App page screenshot capture",
                Slug = "app-page-screenshot"
            },
            new ProcessStepDefinition
            {
                Key = "start-app-once",
                Title = "Start app once",
                StepKind = ProcessStepKind.Work
            },
            new ProcessStepRun
            {
                Title = "Start app once",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = "Runtime App Screenshot Capture Agent"
            },
            new ProcessWorkBrief
            {
                Title = "Start app once brief",
                WorkBriefText = """
                App page screenshot capture: Start app once
                Inputs: Single-page target packet.
                Outputs: Reachable local app instance.
                Evidence: Startup command, working directory, PID, port, and readiness proof.
                Instructions: Start the app from the approved source root using the requested .NET or JavaScript command. For .NET apps, call workspace_dotnet_run with keepAlive true and lifetimeScope ProcessRun because the later capture and cleanup steps own browser proof and shutdown. Wait for the expected local URL to respond. Record process id, command, working directory, port, stdout/stderr summary, readiness checks, and stop command. Do not use Playwright or capture screenshots in this step.
                """,
                ExpectedOutcome = "Reachable local app instance.",
                EvidenceExpectationSummary = "App startup receipt"
            },
            [
                (ProcessArtifactKind.Evidence, "App startup receipt", "Must include command, working directory, process id or managed run handle, URL, and readiness status.")
            ]);

        Assert.Contains("workspace_dotnet_run", tools);
        Assert.DoesNotContain("workspace_pwsh_run_script", tools);
        Assert.DoesNotContain("browser_console_messages", tools);
        Assert.DoesNotContain("browser_snapshot", tools);
        Assert.DoesNotContain("browser_take_screenshot", tools);
    }

    [Fact]
    public void ResolveRequiredToolNames_for_javascript_app_startup_step_does_not_require_dotnet_runner()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = "App page screenshot capture",
                Slug = "app-page-screenshot"
            },
            new ProcessStepDefinition
            {
                Key = "start-app-once",
                Title = "Start app once",
                StepKind = ProcessStepKind.Work
            },
            new ProcessStepRun
            {
                Title = "Start app once",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = "JavaScript QA Review Lead",
                RoleSnapshotSummary = "QA role for JavaScript, Vite, Node, and npm browser application validation."
            },
            new ProcessWorkBrief
            {
                Title = "Start app once brief",
                WorkBriefText = """
                App page screenshot capture: Start app once
                Inputs: Single-page target packet.
                Outputs: Reachable local app instance.
                Evidence: Startup command, working directory, PID, port, and readiness proof.
                Instructions: Start the app from the approved source root using the requested .NET or JavaScript command. For .NET apps, call workspace_dotnet_run with keepAlive true and lifetimeScope ProcessRun because the later capture and cleanup steps own browser proof and shutdown. Wait for the expected local URL to respond. Record process id, command, working directory, port, stdout/stderr summary, readiness checks, and stop command. Do not use Playwright or capture screenshots in this step.
                """,
                ExpectedOutcome = "Reachable local app instance.",
                EvidenceExpectationSummary = "App startup receipt"
            },
            [
                (ProcessArtifactKind.Evidence, "App startup receipt", "Must include command, working directory, process id or managed run handle, URL, and readiness status.")
            ],
            "Capture screenshots for a JavaScript Vite app from package.json using npm scripts.");

        Assert.DoesNotContain("workspace_dotnet_run", tools);
        Assert.DoesNotContain("workspace_dotnet_build", tools);
        Assert.DoesNotContain("browser_take_screenshot", tools);
    }

    [Fact]
    public void ResolveRequiredToolNames_for_blazor_browser_validation_does_not_require_powershell_runner_for_js_interop()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = "Blazor app delivery",
                Slug = "blazor-app-delivery"
            },
            new ProcessStepDefinition
            {
                Key = "validate-blazor-runtime",
                Title = "Validate Blazor runtime and browser evidence",
                StepKind = ProcessStepKind.Review,
                AllowedOperations =
                [
                    ProcessStepOperation.ReadProcessContext,
                    ProcessStepOperation.ReadProjectStructure,
                    ProcessStepOperation.ReadUpstreamArtifacts,
                    ProcessStepOperation.WriteManagedProcessArtifacts,
                    ProcessStepOperation.RunValidation,
                    ProcessStepOperation.LaunchRuntime,
                    ProcessStepOperation.CaptureRuntimeProof
                ]
            },
            new ProcessStepRun
            {
                Title = "Validate Blazor runtime and browser evidence",
                StepKind = ProcessStepKind.Review,
                CurrentExecutorName = "JavaScript QA and browser proof lead",
                RoleSnapshotSummary = "QA role for Blazor WebAssembly PWA browser validation with JavaScript interop evidence."
            },
            new ProcessWorkBrief
            {
                Title = "Validate Blazor runtime and browser evidence",
                WorkBriefText = """
                Validate the Blazor WebAssembly PWA from external-target/C/programovani/dotnet-demo/output/output.csproj.
                The app includes wwwroot/tetris-storage.js for localStorage interop, but the runnable host is Blazor/.NET.
                Start the app with workspace_dotnet_run, capture browser_snapshot, browser_take_screenshot, and browser_console_messages, then write the runtime evidence pack.
                Do not run a cleanup script; the kept-alive dotnet process is managed by the dispatcher.
                """,
                ExpectedOutcome = "Blazor runtime evidence pack with browser proof.",
                EvidenceExpectationSummary = "Browser screenshot, bounded snapshot, console messages, and runtime validation notes."
            },
            [
                (ProcessArtifactKind.Evidence, "Blazor runtime evidence pack", "Must include browser screenshot, browser snapshot, console status, startup receipt, and validation disposition.")
            ],
            "Build a generic Blazor WASM PWA with JavaScript interop from project structure.");

        Assert.Contains("workspace_dotnet_run", tools);
        Assert.Contains("browser_console_messages", tools);
        Assert.Contains("browser_snapshot", tools);
        Assert.Contains("browser_take_screenshot", tools);
        Assert.DoesNotContain("workspace_pwsh_run_script", tools);
    }

    [Fact]
    public void ResolveRequiredToolNames_for_screenshot_review_storage_step_does_not_require_browser_capture_tools()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = "App page screenshot capture",
                Slug = "app-page-screenshot"
            },
            new ProcessStepDefinition
            {
                Key = "review-and-store-screenshot",
                Title = "Review and store screenshot",
                StepKind = ProcessStepKind.Review
            },
            new ProcessStepRun
            {
                Title = "Review and store screenshot",
                StepKind = ProcessStepKind.Review,
                CurrentExecutorName = "Runtime Screenshot Review Storage Agent"
            },
            new ProcessWorkBrief
            {
                Title = "Review and store screenshot",
                WorkBriefText = """
                Review the captured screenshot and browser evidence. Store accepted screenshots through project_structure_asset_create with sourceWorkspacePath.
                The upstream capture step already owns browser_snapshot, browser_take_screenshot, and browser_console_messages.
                """,
                ExpectedOutcome = "Review findings and project image asset storage receipt.",
                EvidenceExpectationSummary = "Screenshot review findings and Project image asset storage receipt"
            },
            [
                (ProcessArtifactKind.Evidence, "Screenshot review findings", "Must state accepted or rejected with the visual reason."),
                (ProcessArtifactKind.Evidence, "Project image asset storage receipt", "Must include project id, image asset node id, content type, original file name, and storage locator.")
            ]);

        Assert.Contains("project_structure_asset_create", tools);
        Assert.DoesNotContain("browser_console_messages", tools);
        Assert.DoesNotContain("browser_snapshot", tools);
        Assert.DoesNotContain("browser_take_screenshot", tools);
    }

    [Fact]
    public void ResolveRequiredToolNames_keeps_image_generation_tool_references()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = "App layout image generation",
                Slug = "app-layout-image-generation"
            },
            new ProcessStepDefinition
            {
                Key = "generate-layout-recommendation",
                Title = "Generate layout recommendation",
                StepKind = ProcessStepKind.Work
            },
            new ProcessStepRun
            {
                Title = "Generate layout recommendation",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = "Runtime Layout Image Generation Agent"
            },
            new ProcessWorkBrief
            {
                Title = "Generate layout recommendation",
                WorkBriefText = """
                Read the stored screenshot asset and call image_generation_create with sourceProjectAssets.
                Store the generated image through project_structure_asset_create with sourceWorkspacePath from the generation result.
                """,
                ExpectedOutcome = "Generated layout image asset storage receipt.",
                EvidenceExpectationSummary = "Generated layout image asset storage receipt"
            },
            [
                (ProcessArtifactKind.Evidence, "Generated layout image asset storage receipt", "Must include generated image node id, provider, model, source screenshot asset id, and storage locator.")
            ]);

        Assert.Contains("image_generation_create", tools);
        Assert.Contains("project_structure_asset_create", tools);
    }

    [Fact]
    public void ResolveRequiredToolNames_preserves_boundary_tool_families_for_execution_lineage()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = "Visual app delivery",
                Slug = "visual-app-delivery"
            },
            new ProcessStepDefinition
            {
                Key = "validate-generate-and-store-visual-proof",
                Title = "Validate, generate, and store visual proof",
                StepKind = ProcessStepKind.Work,
                AllowedOperations =
                [
                    ProcessStepOperation.ReadProjectStructure,
                    ProcessStepOperation.ReadUpstreamArtifacts,
                    ProcessStepOperation.RunValidation,
                    ProcessStepOperation.CaptureRuntimeProof,
                    ProcessStepOperation.WriteManagedProcessArtifacts,
                    ProcessStepOperation.ExecuteExternalAction
                ],
                OperationTargetScope = ProcessStepTargetScope.ExternalActionControlled
            },
            new ProcessStepRun
            {
                Title = "Validate, generate, and store visual proof",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = "Runtime visual evidence agent"
            },
            new ProcessWorkBrief
            {
                Title = "Validate, generate, and store visual proof",
                WorkBriefText = """
                Read the implementation notes with workspace_read_file.
                Use project_structure_read to resolve the source app node.
                Capture browser proof with browser_snapshot, browser_take_screenshot, and browser_console_messages.
                Call image_generation_create for the visual recommendation.
                Store the generated image and lineage receipt with project_structure_asset_create and workspace_write_file.
                """,
                ExpectedOutcome = "Visual proof and generated image lineage are recorded.",
                EvidenceExpectationSummary = "Visual proof and generated image lineage"
            },
            [
                (ProcessArtifactKind.Evidence, "Visual proof and generated image lineage", "Must include browser proof, generated image metadata, project asset node id, and storage receipt.")
            ],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver a visual app proof from project structure.",
                new ProcessProjectStructureContext
                {
                    ProjectId = Guid.NewGuid(),
                    NodeId = "custom:visual-app",
                    NodeTitle = "Visual App"
                }));

        Assert.Contains("workspace_read_file", tools);
        Assert.Contains("workspace_write_file", tools);
        Assert.Contains("project_structure_read", tools);
        Assert.Contains("project_structure_asset_create", tools);
        Assert.Contains("image_generation_create", tools);
        Assert.Contains("browser_snapshot", tools);
        Assert.Contains("browser_take_screenshot", tools);
        Assert.Contains("browser_console_messages", tools);
    }

    [Fact]
    public void ResolveMissingRequiredToolExecutions_accepts_completed_internal_maf_tool_invocations_from_execution_log()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingTools = serviceType.GetMethod(
            "ResolveMissingRequiredToolExecutions",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredToolExecutions method was not found.");
        var candidate = CreateDispatchCandidate(
            new ProcessDefinition
            {
                Name = "App layout image generation",
                Slug = "app-layout-image-generation"
            },
            new ProcessStepDefinition
            {
                Key = "generate-layout-recommendation",
                Title = "Generate layout recommendation",
                StepKind = ProcessStepKind.Work
            },
            new ProcessStepRun
            {
                Title = "Generate layout recommendation",
                StepKind = ProcessStepKind.Work,
                CurrentExecutorName = "Runtime Layout Image Generation Agent"
            },
            new ProcessWorkBrief
            {
                Title = "Generate layout recommendation",
                WorkBriefText = """
                Read the stored screenshot asset and call image_generation_create with sourceProjectAssets.
                Store the generated image through project_structure_asset_create with sourceWorkspacePath from the generation result.
                """,
                ExpectedOutcome = "Generated layout image asset storage receipt.",
                EvidenceExpectationSummary = "Generated layout image asset storage receipt"
            },
            [
                (ProcessArtifactKind.Evidence, "Generated layout image asset storage receipt", "Must include generated image node id, provider, model, source screenshot asset id, and storage locator.")
            ]);
        var detail = CreateSuccessfulExecutionDetail(
            """
            {"status":"Completed","reason":"Generated layout recommendation image created and stored as project asset.","branchOutcomeKey":"completed","branchOutcomeTitle":"Completed","evidenceRefs":["custom:1fc595afea754f6fb41137d670c72c99"],"nextActions":[],"humanReadableSummaryMarkdown":"Generated layout image asset storage receipt."}
            """,
            serializedSessionStateJson: null);
        var now = DateTimeOffset.UtcNow;
        detail = detail with
        {
            ExecutionLog =
            [
                CreateExecutionLogToolInvocation(detail.Run.Id, detail.Run.AgentId, now, "image_generation_create"),
                CreateExecutionLogToolInvocation(detail.Run.Id, detail.Run.AgentId, now.AddSeconds(1), "project_structure_asset_create")
            ]
        };

        var missingRequiredTools = resolveMissingTools.Invoke(null, [candidate, detail]) as IReadOnlyList<string>;

        Assert.NotNull(missingRequiredTools);
        Assert.DoesNotContain("image_generation_create", missingRequiredTools, StringComparer.Ordinal);
        Assert.DoesNotContain("project_structure_asset_create", missingRequiredTools, StringComparer.Ordinal);
    }

    [Fact]
    public void ResolveRequiredToolNames_for_screenshot_cleanup_handoff_step_does_not_require_browser_capture_tools()
    {
        var tools = InvokeResolveRequiredToolNames(
            new ProcessDefinition
            {
                Name = "App page screenshot capture",
                Slug = "app-page-screenshot"
            },
            new ProcessStepDefinition
            {
                Key = "cleanup-and-handoff",
                Title = "Cleanup and handoff",
                StepKind = ProcessStepKind.End
            },
            new ProcessStepRun
            {
                Title = "Cleanup and handoff",
                StepKind = ProcessStepKind.End,
                CurrentExecutorName = "Runtime App Screenshot Capture Agent"
            },
            new ProcessWorkBrief
            {
                Title = "Cleanup and handoff",
                WorkBriefText = """
                Read upstream screenshot review findings and project image asset storage receipt.
                Stop the managed app process if one is still live and write the handoff.
                The upstream capture step already produced browser_snapshot, browser_take_screenshot, and browser_console_messages.
                """,
                ExpectedOutcome = "Single page screenshot handoff with cleanup status.",
                EvidenceExpectationSummary = "Single page screenshot handoff"
            },
            [
                (ProcessArtifactKind.Evidence, "Single page screenshot handoff", "Must include final route, screenshot artifact, asset node id, cleanup status, and blockers.")
            ]);

        Assert.Contains("workspace_pwsh_run_script", tools);
        Assert.DoesNotContain("browser_console_messages", tools);
        Assert.DoesNotContain("browser_snapshot", tools);
        Assert.DoesNotContain("browser_take_screenshot", tools);
    }

    [Fact]
    public void ResolveMissingRunnableApplicationProofSummary_skips_dotnet_solution_setup_scaffold_step()
    {
        var resolveMissingRunnableProof = typeof(ProcessRunAutomationDispatchService)
            .GetMethod("ResolveMissingRunnableApplicationProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRunnableApplicationProofSummary method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Create the .NET solution and app project. Build, test, run, and browser proof are downstream validation steps.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Solution skeleton change set", true, "Must include the solution file and requested .NET project under the agreed app directory.")],
            [],
            stepTitle: "Create solution and .NET app project",
            processName: ".NET solution setup subprocess",
            outputContractSummary: "Solution file and requested .NET application project are present and added to the solution.",
            processSlug: "dotnet-solution-setup",
            stepKey: "create-dotnet-project");
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Solution scaffold created and downstream validation is intentionally deferred.",
            summaryMarkdown: "## Solution skeleton change set\nSolution and app project were created.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionStateWithMessages(("assistant", [CreateTextContent(responseText)])),
            [
                CreateToolReceipt("workspace-file", "workspace_write_file", "external-target/C/app/src/App/Program.cs", ".", "Succeeded", now),
                CreateToolReceipt("workspace-file", "workspace_read_file", "external-target/C/app/src/App/App.csproj", ".", "Succeeded", now.AddSeconds(1)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_build", "build external-target/C/app/App.sln", ".", "Succeeded", now.AddSeconds(2)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_test", "test external-target/C/app/App.sln", ".", "Succeeded", now.AddSeconds(3))
            ]);

        var summary = resolveMissingRunnableProof.Invoke(null, [candidate, detail]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Theory]
    [InlineData("artifacts/scopes/organization/demo/project-structure-context-brief.md", true)]
    [InlineData("artifacts/project-structure-context-brief.md", true)]
    [InlineData("artifacts/scopes/.../implementation-slice-scope-packet.md", false)]
    [InlineData("artifacts/scopes/<scope>/<id>/implementation-slice-scope-packet.md", false)]
    [InlineData("artifacts/scopes/organization/demo/process-runs", false)]
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
    public void ResolveShallowSharedManagedArtifactReferenceSummary_ignores_deliverable_paths_under_grounded_output_leaf()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "ResolveShallowSharedManagedArtifactReferenceSummary",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveShallowSharedManagedArtifactReferenceSummary method was not found.");
        const string rootAlias = "external-target/C/programovani/dotnet-demo/output";
        const string responseText = "Updated output/MIGRATION.md and output/README-validation.md in the grounded product root.";
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            "{}",
            serializedInvocationMetadataJson: BuildAllowedExternalTargetMetadata(rootAlias));

        var summary = method.Invoke(null, [detail, responseText]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveRecoverableAutomationExecutionRunId_returns_latest_terminal_automation_run_for_in_progress_step()
    {
        var attemptStartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
        var olderRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded) with
        {
            CreatedAtUtc = attemptStartedAtUtc.AddMinutes(1),
            StartedAtUtc = attemptStartedAtUtc.AddMinutes(1),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var latestRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Failed, ProcessAutomationRunOutcome.Failed) with
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

    private static string InvokeBuildCanonicalProjectStructureGroundingSql()
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "BuildCanonicalProjectStructureGroundingSql",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCanonicalProjectStructureGroundingSql method was not found.");

        return (string)method.Invoke(null, [])!;
    }

    private static AgentWorkspaceToolProfileKind InvokeResolveWorkspaceToolProfile(
        ProcessStepRun stepRun,
        ProcessWorkBrief workBrief,
        ProcessRoleRequirement role,
        (ProcessArtifactKind ArtifactKind, string Title, string ValidationRequirementSummary)[] expectedArtifactDefinitions)
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod(
            "ResolveWorkspaceToolProfile",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveWorkspaceToolProfile method was not found.");
        var expectedArtifacts = CreateDispatchArtifactExpectations(expectedArtifactDefinitions);

        return (AgentWorkspaceToolProfileKind)method.Invoke(
            null,
            [stepRun, workBrief, role, null, expectedArtifacts])!;
    }

    private static IReadOnlyList<string> InvokeResolveRequiredToolNames(
        ProcessDefinition definition,
        ProcessStepDefinition stepDefinition,
        ProcessStepRun stepRun,
        ProcessWorkBrief workBrief,
        (ProcessArtifactKind ArtifactKind, string Title, string ValidationRequirementSummary)[] expectedArtifactDefinitions,
        string triggerReason = "Create a grounded .NET app from project structure.")
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod(
            "ResolveRequiredToolNames",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate(definition, stepDefinition, stepRun, workBrief, expectedArtifactDefinitions, triggerReason);
        var result = (IEnumerable)method.Invoke(null, [candidate])!;

        return result.Cast<string>().ToList();
    }

    private static bool InvokeRequiresConcreteImplementationProof(
        ProcessDefinition definition,
        ProcessStepDefinition stepDefinition,
        ProcessStepRun stepRun,
        ProcessWorkBrief workBrief,
        (ProcessArtifactKind ArtifactKind, string Title, string ValidationRequirementSummary)[] expectedArtifactDefinitions,
        string triggerReason = "Create a grounded .NET app from project structure.")
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod(
            "RequiresConcreteImplementationProof",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("RequiresConcreteImplementationProof method was not found.");
        var candidate = CreateDispatchCandidate(definition, stepDefinition, stepRun, workBrief, expectedArtifactDefinitions, triggerReason);

        return (bool)method.Invoke(null, [candidate])!;
    }

    private static object CreateDispatchCandidate(
        ProcessDefinition definition,
        ProcessStepDefinition stepDefinition,
        ProcessStepRun stepRun,
        ProcessWorkBrief workBrief,
        (ProcessArtifactKind ArtifactKind, string Title, string ValidationRequirementSummary)[] expectedArtifactDefinitions,
        string triggerReason = "Create a grounded .NET app from project structure.")
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var candidateType = serviceType.GetNestedType("DispatchCandidate", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchCandidate type was not found.");
        var artifactInputType = serviceType.GetNestedType("DispatchArtifactInput", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchArtifactInput type was not found.");
        var branchOutcomeType = serviceType.GetNestedType("DispatchBranchOutcome", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchBranchOutcome type was not found.");
        var expectedArtifacts = CreateDispatchArtifactExpectations(expectedArtifactDefinitions);
        var artifactInputs = Array.CreateInstance(artifactInputType, 0);
        var branchOutcomes = Array.CreateInstance(branchOutcomeType, 0);
        var run = new ProcessRun
        {
            Name = "Test process run",
            TriggerReason = triggerReason
        };

        return Activator.CreateInstance(
            candidateType,
            run,
            definition,
            stepRun,
            stepDefinition,
            workBrief,
            Guid.NewGuid(),
            expectedArtifacts,
            new HashSet<Guid>(),
            artifactInputs,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            null,
            null,
            string.Empty,
            branchOutcomes,
            false,
            new AgentProcessCooperationMetadata(
                AgentProcessCooperationMode.ProcessArtifactHandoff,
                AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                "test")) ?? throw new InvalidOperationException("DispatchCandidate could not be constructed.");
    }

    private static Array CreateDispatchArtifactExpectations(
        (ProcessArtifactKind ArtifactKind, string Title, string ValidationRequirementSummary)[] expectedArtifactDefinitions)
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var artifactExpectationType = serviceType.GetNestedType("DispatchArtifactExpectation", BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("DispatchArtifactExpectation type was not found.");
        var expectedArtifacts = Array.CreateInstance(artifactExpectationType, expectedArtifactDefinitions.Length);
        for (var index = 0; index < expectedArtifactDefinitions.Length; index++)
        {
            var definition = expectedArtifactDefinitions[index];
            var expectedArtifact = Activator.CreateInstance(
                artifactExpectationType,
                Guid.NewGuid(),
                definition.ArtifactKind,
                definition.Title,
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                definition.ValidationRequirementSummary,
                string.Empty)
                ?? throw new InvalidOperationException("DispatchArtifactExpectation could not be constructed.");
            expectedArtifacts.SetValue(expectedArtifact, index);
        }

        return expectedArtifacts;
    }

    [Fact]
    public void ResolveRecoverableAutomationExecutionRunId_ignores_terminal_runs_when_step_is_not_in_progress()
    {
        var recoverableRunId = ProcessRunAutomationDispatchService.ResolveRecoverableAutomationExecutionRunId(
            CreateStepRun(ProcessStepRunStatus.Ready, null),
            [
                CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded)
            ]);

        Assert.Null(recoverableRunId);
    }

    [Fact]
    public void ResolveReusableAutomationChatSessionId_does_not_reuse_terminal_chat_backed_automation_run()
    {
        var olderSessionId = Guid.NewGuid();
        var latestSessionId = Guid.NewGuid();
        var olderRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded) with
        {
            ChatSessionId = olderSessionId,
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var latestRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Running, null) with
        {
            ChatSessionId = latestSessionId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var chatSessionId = ProcessRunAutomationDispatchService.ResolveReusableAutomationChatSessionId(
            [olderRun, latestRun]);

        Assert.Null(chatSessionId);
    }

    [Fact]
    public void ResolveReusableAutomationChatSessionId_ignores_manual_or_sessionless_runs()
    {
        var chatSessionId = ProcessRunAutomationDispatchService.ResolveReusableAutomationChatSessionId(
        [
            CreateExecutionRun("agent-run-debug", ProcessAutomationExecutionState.Running, null) with
            {
                ChatSessionId = Guid.NewGuid(),
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Running, null) with
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
        var completedRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded) with
        {
            ChatSessionId = completedSessionId,
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var activeRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Running, null) with
        {
            ChatSessionId = activeSessionId,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var chatSessionId = ProcessRunAutomationDispatchService.ResolveReusableAutomationChatSessionId(
            [completedRun, activeRun]);

        Assert.Null(chatSessionId);
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
        var interruptedRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Failed, ProcessAutomationRunOutcome.Cancelled) with
        {
            CreatedAtUtc = attemptStartedAtUtc.AddMinutes(20),
            StartedAtUtc = attemptStartedAtUtc.AddMinutes(20),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            CompletedAtUtc = DateTimeOffset.UtcNow
        };
        var previousCompletedRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded) with
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
        var previousAttemptRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded) with
        {
            CreatedAtUtc = attemptStartedAtUtc.AddMinutes(-20),
            StartedAtUtc = attemptStartedAtUtc.AddMinutes(-20),
            UpdatedAtUtc = attemptStartedAtUtc.AddMinutes(-10),
            CompletedAtUtc = attemptStartedAtUtc.AddMinutes(-10)
        };
        var currentAttemptRun = CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded) with
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
        var artifact = new ProcessAutomationExecutionArtifact(
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
        var artifact = new ProcessAutomationExecutionArtifact(
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
        var artifact = new ProcessAutomationExecutionArtifact(
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
    public void MatchExpectedArtifactId_does_not_bind_external_project_file_to_narrative_change_set()
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
                "Must be linked to tests, migration notes, and touched-surface inventory.",
                string.Empty)
        };
        var artifact = new ProcessAutomationExecutionArtifact(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            "output.csproj",
            "external-target/C/programovani/dotnet/output/output.csproj",
            "application/octet-stream",
            "workspace_dotnet_build",
            "Workspace path touched or targeted by the recipe.",
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
        var artifact = new ProcessAutomationExecutionArtifact(
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
        var artifact = new ProcessAutomationExecutionArtifact(
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
        var artifact = new ProcessAutomationExecutionArtifact(
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
    public void MatchExpectedArtifactId_does_not_bind_provider_native_browser_snapshot_to_regression_evidence_pack()
    {
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                Guid.NewGuid(),
                ProcessArtifactKind.Evidence,
                "Repaired regression evidence pack",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Must name repaired flows, assertion depth, runtime/API/browser evidence as applicable, screenshots for UI surfaces, and unresolved risks after the repair pass.",
                string.Empty)
        };
        var artifact = new ProcessAutomationExecutionArtifact(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            "recheck-browser-snapshot.md",
            "artifacts/scopes/organization/demo/process-runs/run-001/recheck-browser-snapshot.md",
            "text/markdown",
            "browser_snapshot",
            "Projected provider-native browser output.",
            DateTimeOffset.UtcNow);
        const string artifactContent = """
            ## Repaired regression evidence pack

            Browser proof was attempted, but this file is a provider-native browser snapshot, not the durable QA evidence pack.
            """;

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(
            expectedArtifacts,
            artifact,
            artifactContent);

        Assert.Null(matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_matches_provider_native_browser_screenshot_to_pathless_visual_expectation()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                expectedArtifactId,
                ProcessArtifactKind.Evidence,
                "Page screenshot file",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Capture a PNG screenshot of the requested app page using browser_take_screenshot.",
                string.Empty),
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                Guid.NewGuid(),
                ProcessArtifactKind.Evidence,
                "Browser navigation and console evidence",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Capture browser navigation, URL, and console observations.",
                string.Empty)
        };
        var artifact = new ProcessAutomationExecutionArtifact(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            "inventory-127.0.0.1-53227.png",
            "artifacts/scopes/organization/demo/process-runs/run-001/inventory-127.0.0.1-53227.png",
            "image/png",
            "browser_take_screenshot",
            "Projected provider-native browser output.",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(expectedArtifactId, matchedExpectationId);
    }

    [Fact]
    public void MatchExpectedArtifactId_prefers_route_specific_provider_native_browser_screenshot_expectation()
    {
        var inventoryScreenshotId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                Guid.NewGuid(),
                ProcessArtifactKind.Evidence,
                "Home page screenshot",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Capture a PNG screenshot of the home page using browser_take_screenshot.",
                string.Empty),
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                inventoryScreenshotId,
                ProcessArtifactKind.Evidence,
                "Inventory page screenshot",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Capture a PNG screenshot of the inventory page using browser_take_screenshot.",
                string.Empty)
        };
        var artifact = new ProcessAutomationExecutionArtifact(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "generated-output",
            "inventory-127.0.0.1-53227.png",
            "artifacts/scopes/organization/demo/process-runs/run-001/inventory-127.0.0.1-53227.png",
            "image/png",
            "browser_take_screenshot",
            "Projected provider-native browser output.",
            DateTimeOffset.UtcNow);

        var matchedExpectationId = ProcessRunAutomationDispatchService.MatchExpectedArtifactId(expectedArtifacts, artifact);

        Assert.Equal(inventoryScreenshotId, matchedExpectationId);
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
        var artifact = new ProcessAutomationExecutionArtifact(
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
        var artifact = new ProcessAutomationExecutionArtifact(
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
        var artifact = new ProcessAutomationExecutionArtifact(
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
    public void WorkspaceWrittenFileMatchesExpectedArtifact_matches_single_managed_recheck_evidence_alias()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifact = new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
            expectedArtifactId,
            ProcessArtifactKind.Evidence,
            "Repaired regression evidence pack",
            true,
            ProcessArtifactTrustRequirement.ReviewRequired,
            ProcessSensitivityLevel.Internal,
            "Must name repaired flows, assertion depth, runtime/API/browser evidence as applicable, screenshots for UI surfaces, and unresolved risks after the repair pass.",
            string.Empty);
        var expectedArtifacts = new[] { expectedArtifact };

        var matches = ProcessRunAutomationDispatchService.WorkspaceWrittenFileMatchesExpectedArtifact(
            expectedArtifacts,
            expectedArtifact,
            "artifacts/scopes/organization/demo/process-runs/11111111-1111-1111-1111-111111111111/qa-recheck-evidence-pack.md",
            """
            ## QA recheck evidence pack

            The repaired JavaScript package was inspected after the repair pass.
            Runtime validation, browser proof, and unresolved risks are documented here.
            """);

        Assert.True(matches);
    }

    [Fact]
    public void WorkspaceWrittenFileMatchesExpectedArtifact_does_not_treat_product_source_as_narrative_evidence()
    {
        var expectedArtifactId = Guid.NewGuid();
        var expectedArtifact = new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
            expectedArtifactId,
            ProcessArtifactKind.Deliverable,
            "Implementation change set",
            true,
            ProcessArtifactTrustRequirement.ReviewRequired,
            ProcessSensitivityLevel.Internal,
            "Must be linked to tests, migration notes, and touched-surface inventory.",
            string.Empty);
        var expectedArtifacts = new[] { expectedArtifact };

        var matches = ProcessRunAutomationDispatchService.WorkspaceWrittenFileMatchesExpectedArtifact(
            expectedArtifacts,
            expectedArtifact,
            "external-target/C/programovani/dotnet/output/output.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        Assert.False(matches);
    }

    [Fact]
    public void ResolveProjectStructureRequiredArtifactPaths_extracts_governed_markdown_paths()
    {
        var paths = ProcessRunAutomationDispatchService.ResolveProjectStructureRequiredArtifactPaths(
            """
            Project-structure required artifact contract:
            - Required file `02-business-plan.md` must be written at `external-target/C/programovani/candoitall-dev-55-output/business-analysis/02-business-plan.md`.
            - Required file `04-go-to-market-experiment-plan.md` must be written at `external-target/C/programovani/candoitall-dev-55-output/business-analysis/04-go-to-market-experiment-plan.md`.
            """);

        Assert.Collection(
            paths,
            first =>
            {
                Assert.Equal("02-business-plan.md", first.FileName);
                Assert.Equal("external-target/C/programovani/candoitall-dev-55-output/business-analysis/02-business-plan.md", first.AliasPath);
            },
            second =>
            {
                Assert.Equal("04-go-to-market-experiment-plan.md", second.FileName);
                Assert.Equal("external-target/C/programovani/candoitall-dev-55-output/business-analysis/04-go-to-market-experiment-plan.md", second.AliasPath);
            });
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_rejects_project_structure_artifact_written_to_wrong_root()
    {
        var resolveMissingRequiredArtifactSummary = typeof(ProcessRunAutomationDispatchService)
            .GetMethod("ResolveMissingRequiredArtifactSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Draft the business plan for the reviewed product.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Business plan", true, "Must include customer segment, offer, risks, and validation plan.")],
            [],
            triggerReason: "Analyze the reviewed app as a business case and write the plan outputs.",
            stepTitle: "Draft business plan",
            processName: "Business plan development",
            outputContractSummary: "Decision-ready business plan.");
        var prompt = """
            Project-structure required artifact contract:
            - Required file `02-business-plan.md` must be written at `external-target/C/programovani/candoitall-dev-55-output/business-analysis/02-business-plan.md`.
            """;
        var detail = CreateSuccessfulExecutionDetail(
            responseText: string.Empty,
            serializedSessionStateJson: BuildSerializedSessionState((
                "workspace_write_file",
                new Dictionary<string, object?>
                {
                    ["path"] = "external-target/C/programovani/candoitall-dev-55-output/scenario-03-js-rain-barrel-chore-splitter/02-business-plan.md",
                    ["content"] = "Business plan content with customer segment, offer, risks, and validation plan."
                },
                CreateProviderNativeTextResult("File written."))),
            prompt: prompt);

        var summary = resolveMissingRequiredArtifactSummary.Invoke(null, [candidate, detail, string.Empty]) as string;

        Assert.Equal("Business plan", summary);
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_accepts_project_structure_artifact_at_governed_path()
    {
        var resolveMissingRequiredArtifactSummary = typeof(ProcessRunAutomationDispatchService)
            .GetMethod("ResolveMissingRequiredArtifactSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Draft the business plan for the reviewed product.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Business plan", true, "Must include customer segment, offer, risks, and validation plan.")],
            [],
            triggerReason: "Analyze the reviewed app as a business case and write the plan outputs.",
            stepTitle: "Draft business plan",
            processName: "Business plan development",
            outputContractSummary: "Decision-ready business plan.");
        var prompt = """
            Project-structure required artifact contract:
            - Required file `02-business-plan.md` must be written at `external-target/C/programovani/candoitall-dev-55-output/business-analysis/02-business-plan.md`.
            """;
        var detail = CreateSuccessfulExecutionDetail(
            responseText: string.Empty,
            serializedSessionStateJson: BuildSerializedSessionState((
                "workspace_write_file",
                new Dictionary<string, object?>
                {
                    ["path"] = "external-target/C/programovani/candoitall-dev-55-output/business-analysis/02-business-plan.md",
                    ["content"] = "Business plan content with customer segment, offer, risks, and validation plan."
                },
                CreateProviderNativeTextResult("File written."))),
            prompt: prompt);

        var summary = resolveMissingRequiredArtifactSummary.Invoke(null, [candidate, detail, string.Empty]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveSuccessfulWorkspaceFileMutationReceiptPaths_extracts_receipt_only_artifact_writes()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveReceiptPaths = serviceType.GetMethod(
            "ResolveSuccessfulWorkspaceFileMutationReceiptPaths",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveSuccessfulWorkspaceFileMutationReceiptPaths method was not found.");
        var now = DateTimeOffset.UtcNow;
        const string artifactPath = "artifacts/process-runs/11111111-1111-1111-1111-111111111111/01-architecture-decision-record.md";
        var detail = new ProcessAutomationExecutionRunDetail(
            CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    artifactPath,
                    ".",
                    $"Succeeded: Overwrote '{artifactPath}' with 2979 characters.",
                    now)
            ]
        };

        var paths = ((IEnumerable?)resolveReceiptPaths.Invoke(null, [detail]))
            ?.Cast<string>()
            .ToList()
            ?? [];

        var path = Assert.Single(paths);
        Assert.Equal(artifactPath, path);
    }

    [Fact]
    public void HasProjectedArtifactExpectationExternalReference_detects_workspace_written_artifact_record()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var hasProjectedReference = serviceType.GetMethod(
            "HasProjectedArtifactExpectationExternalReference",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("HasProjectedArtifactExpectationExternalReference method was not found.");
        var expectedArtifactId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var keys = new[]
        {
            $"workspace-written-artifact|{executionRunId:D}|{expectedArtifactId:D}|artifacts/process-runs/run/01-architecture-decision-record.md"
        };

        var result = hasProjectedReference.Invoke(null, [keys, expectedArtifactId]) as bool?;

        Assert.True(result);
    }

    [Fact]
    public void ResetRecordedArtifactExpectationsForExecutionProjection_clears_stale_attempt_satisfaction_without_losing_external_refs()
    {
        var expectedArtifactId = Guid.NewGuid();
        var candidate = (ProcessRunAutomationDispatchService.DispatchCandidate)CreateDispatchCandidateCore(
            "Record final evidence.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Final evidence", true, "Must include current execution artifact.")],
            [],
            stepTitle: "Record evidence");
        candidate.RecordedArtifactExpectationIds.Add(expectedArtifactId);
        candidate.ExternalReferenceKeys.Add($"workspace-written-artifact|{Guid.NewGuid():D}|{expectedArtifactId:D}|artifacts/process-runs/current/final-evidence.md");

        ProcessRunAutomationDispatchService.ResetRecordedArtifactExpectationsForExecutionProjection(candidate);

        Assert.Empty(candidate.RecordedArtifactExpectationIds);
        Assert.Single(candidate.ExternalReferenceKeys);
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
            "Treat the run objective, work brief, required artifacts, grounded project-structure nodes, upstream artifacts, and current-run tool outputs as the scope boundary.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not add optional features, extra documents, new workflows, new agent roles, visual flourishes, or technology changes only because they seem useful.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Escalate with `Blocked` or `Failed` when the requested result cannot be built inside that boundary",
            prompt,
            StringComparison.Ordinal);
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
            "Do not stop after inspection, reconnaissance, bootstrap confirmation, or a next-steps summary if required tools, concrete deliverables required by this step, or required artifacts are still missing.",
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
            "Missing preferences are different from stated requirements.",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not use status Blocked for ambiguity that can be handled by explicit assumptions",
            prompt,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDowngradedProjectStructureRequirementSummary_flags_weakened_grounded_requirement()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveSummary = serviceType.GetMethod(
                "ResolveDowngradedProjectStructureRequirementSummary",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveDowngradedProjectStructureRequirementSummary method was not found.");
        var candidate = CreateDispatchCandidate(
            "Clarify scope and release boundary from project-structure source of truth.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief,
                "Scope boundary packet",
                true,
                "Must preserve explicit project-structure source-of-truth requirements without downgrading them to optional, excluded, non-acceptance, or follow-up work."));
        const string prompt = """
You are executing a CanDoItAll process step.

Live project structure grounding:
Requirements from project-level planning context:
- Persistence: save the best score locally (note:persistence); type: ProjectBlock/note
- Mobile app can be handled later if needed (note:mobile); type: ProjectBlock/note
""";
        const string response = "Persistence: local score storage is optional and not required for acceptance.";
        var detail = CreateSuccessfulExecutionDetail(response, "{}", prompt: prompt);

        var summary = Assert.IsType<string>(resolveSummary.Invoke(null, [candidate, detail, response]));

        Assert.Contains("downgrades a grounded project-structure requirement", summary, StringComparison.Ordinal);
        Assert.Contains("Persistence", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveDowngradedProjectStructureRequirementSummary_ignores_source_marked_as_deferred()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveSummary = serviceType.GetMethod(
                "ResolveDowngradedProjectStructureRequirementSummary",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveDowngradedProjectStructureRequirementSummary method was not found.");
        var candidate = CreateDispatchCandidate(
            "Clarify scope and release boundary from project-structure source of truth.",
            ProcessStepKind.Start,
            (ProcessArtifactKind.Brief,
                "Scope boundary packet",
                true,
                "Must preserve explicit project-structure source-of-truth requirements without downgrading them to optional, excluded, non-acceptance, or follow-up work."));
        const string prompt = """
You are executing a CanDoItAll process step.

Live project structure grounding:
Requirements from project-level planning context:
- Mobile app can be handled later if needed (note:mobile); type: ProjectBlock/note
""";
        const string response = "Mobile app work is out of scope for this run.";
        var detail = CreateSuccessfulExecutionDetail(response, "{}", prompt: prompt);

        var summary = Assert.IsType<string>(resolveSummary.Invoke(null, [candidate, detail, response]));

        Assert.Equal(string.Empty, summary);
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
        Assert.Contains(
            "repair the real entry point",
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
    public void BuildExecutionPromptCore_routes_non_code_artifacts_to_grounded_external_artifact_destination()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var candidate = CreateDispatchCandidateCore(
            "Draft the business plan, marketing plan, and financial model for the reviewed product.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Business plan", true, "Must include customer segment, offer, risks, and validation plan.")],
            [],
            triggerReason: "Analyze the reviewed app as a business case and write the plan outputs.",
            stepTitle: "Draft business plan",
            processName: "Business plan development",
            outputContractSummary: "Decision-ready business plan, marketing plan, and financial model.");

        var prompt = buildExecutionPromptCore.Invoke(
            null,
            [
                candidate,
                null,
                """
                Dispatcher fetched the live project structure for `Business validation project` and focused this prompt on the selected work branch.
                Grounded external target paths from the selected project structure:
                - `C:\programovani\candoitall-dev-output\orchard-shift-board-business-v9` mapped to `external-target/C/programovani/candoitall-dev-output/orchard-shift-board-business-v9` from Artifact destination folder (node-target)
                Project-structure required artifact contract:
                - Required file `02-business-plan.md` must be written at `external-target/C/programovani/candoitall-dev-output/orchard-shift-board-business-v9/02-business-plan.md`.
                """,
                null
            ]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("described as an artifact, report, plan, document, or handoff destination", prompt, StringComparison.Ordinal);
        Assert.Contains("Write required generated deliverable artifacts under `external-target/C/programovani/candoitall-dev-output/orchard-shift-board-business-v9`", prompt, StringComparison.Ordinal);
        Assert.Contains("Governed path: external-target/C/programovani/candoitall-dev-output/orchard-shift-board-business-v9/02-business-plan.md", prompt, StringComparison.Ordinal);
        Assert.Contains("wrong-root file does not satisfy it", prompt, StringComparison.Ordinal);
        Assert.Contains("write it under `external-target/C/programovani/candoitall-dev-output/orchard-shift-board-business-v9`", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_routes_scope_artifacts_to_managed_root_when_external_product_root_is_grounded()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var candidate = CreateDispatchCandidateCore(
            "Capture implementation slice boundary for a .NET CLI app. This step only records scope and downstream validation hooks.",
            ProcessStepKind.Start,
            [],
            false,
            [(ProcessArtifactKind.Brief, "Implementation slice scope packet", true, "Must capture product root, feature boundary, assumptions, and validation hooks.")],
            [],
            triggerReason: "Create a .NET CLI app from the project mindmap.",
            stepTitle: "Capture implementation slice boundary",
            processName: ".NET implementation slice with atomic validation",
            outputContractSummary: "Implementation slice scope packet.");

        var prompt = buildExecutionPromptCore.Invoke(
            null,
            [
                candidate,
                null,
                """
                Dispatcher fetched the live project structure for `TodoSummary` and focused this prompt on the selected work branch.
                Grounded external target paths from the selected project structure:
                - `C:\programovani\candoitall-processes1-dotnet-cli-h` mapped to `external-target/C/programovani/candoitall-processes1-dotnet-cli-h` from Product root (custom:target)
                Requirements from project-level planning context:
                - Output folder: C:\programovani\candoitall-processes1-dotnet-cli-h
                - Solution name TodoSummary.
                - Console app project src/TodoSummary.Console.
                - xUnit test project tests/TodoSummary.Tests.
                """,
                null
            ]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("This step is non-mutating. Do not create directories or write files under `external-target/C/programovani/candoitall-processes1-dotnet-cli-h`.", prompt, StringComparison.Ordinal);
        Assert.Contains("Managed path: artifacts/process-runs/", prompt, StringComparison.Ordinal);
        Assert.Contains("Use `artifacts/process-runs/", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Write required generated deliverable artifacts under `external-target/C/programovani/candoitall-processes1-dotnet-cli-h`", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("write it under `external-target/C/programovani/candoitall-processes1-dotnet-cli-h`", prompt, StringComparison.Ordinal);
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
    public void BuildProjectStructureGroundingSummary_prioritizes_selected_run_instruction_product_root()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var groundingNodeType = serviceType.GetNestedType("ProjectStructureGroundingNodeData", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProjectStructureGroundingNodeData type was not found.");
        var nodes = Array.CreateInstance(groundingNodeType, 4);
        var projectId = Guid.NewGuid();
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                $"project:{projectId:D}",
                string.Empty,
                "ProjectRoot",
                string.Empty,
                "SamplePwaApp",
                string.Empty,
                string.Empty,
                string.Empty,
                "{}"),
            0);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:target",
                $"project:{projectId:D}",
                "WorkItem",
                "implementation",
                "Implement Blazor app",
                string.Empty,
                string.Empty,
                "Previous run output: C:\\programovani\\dotnet-demo\\output\\codex-live-blazor-20260522-181000\\product",
                "{}"),
            1);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:current-run",
                "custom:target",
                "Note",
                "run-instructions",
                "Codex live Blazor delivery run instructions 20260522-190813",
                string.Empty,
                string.Empty,
                """
                Approved output root: C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-190813
                Approved product root: C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-190813\product
                Approved agent evidence root: C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-190813\agent-evidence
                Approved backup root: C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-190813\project-structure-backup
                """,
                "{}"),
            2);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:old-run",
                "custom:target",
                "Note",
                "run-instructions",
                "Old run instructions 20260522-181000",
                string.Empty,
                string.Empty,
                "Approved product root: C:\\programovani\\dotnet-demo\\output\\codex-live-blazor-20260522-181000\\product",
                "{}"),
            3);
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
                "SamplePwaApp",
                nodes,
                new ProcessProjectStructureContext
                {
                    ProjectId = projectId,
                    NodeId = "custom:current-run",
                    NodeTitle = "Codex live Blazor delivery run instructions 20260522-190813",
                    ParentNodeId = "custom:target",
                    ParentNodeTitle = "Implement Blazor app"
                }
            ]) as string;

        Assert.NotNull(summary);
        var currentProductIndex = summary.IndexOf(
            "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813/product",
            StringComparison.Ordinal);
        var oldProductIndex = summary.IndexOf(
            "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-181000/product",
            StringComparison.Ordinal);

        Assert.True(currentProductIndex >= 0);
        Assert.True(oldProductIndex < 0 || currentProductIndex < oldProductIndex);
    }

    [Fact]
    public void BuildProjectStructureGroundingSummary_includes_required_artifact_contract_from_focus_node()
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
                "ProjectBlock",
                "business-analysis",
                "Scenario 05 - Business Analysis",
                string.Empty,
                string.Empty,
                """
                Business-analysis output root: C:\programovani\candoitall-dev-55-output\business-analysis
                Required durable files: 00-strategy-intake-brief.md, 01-product-evidence-assessment.md, 02-business-plan.md, 03-financial-model-and-sensitivity.md, 04-go-to-market-experiment-plan.md, 05-integrated-business-plan-review.md, 06-run-summary.md
                Planning constraints: separate observed facts from assumptions.
                """,
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
                "Business validation project",
                nodes,
                new ProcessProjectStructureContext
                {
                    ProjectId = Guid.NewGuid(),
                    NodeId = "node-target",
                    NodeTitle = "Scenario 05 - Business Analysis"
                }
            ]) as string;

        Assert.NotNull(summary);
        Assert.Contains("Project-structure required artifact contract:", summary, StringComparison.Ordinal);
        Assert.Contains(
            "Required file `04-go-to-market-experiment-plan.md` must be written at `external-target/C/programovani/candoitall-dev-55-output/business-analysis/04-go-to-market-experiment-plan.md`",
            summary,
            StringComparison.Ordinal);
        Assert.Contains("wrong-root or sibling-root files do not satisfy", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProjectStructureGroundingSummary_uses_selected_project_node_when_parent_is_process_definition()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var groundingNodeType = serviceType.GetNestedType("ProjectStructureGroundingNodeData", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProjectStructureGroundingNodeData type was not found.");
        var nodes = Array.CreateInstance(groundingNodeType, 5);
        var projectId = Guid.NewGuid();
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                $"project:{projectId:D}",
                string.Empty,
                "ProjectRoot",
                string.Empty,
                "UnitConverterApp",
                string.Empty,
                string.Empty,
                string.Empty,
                "{}"),
            0);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:main",
                $"project:{projectId:D}",
                "ProjectBlock",
                "architecture",
                "Main",
                string.Empty,
                string.Empty,
                string.Empty,
                "{}"),
            1);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:output-note",
                "custom:main",
                "Note",
                string.Empty,
                @"output must be placed in ""C:\programovani\dotnet\output""",
                string.Empty,
                string.Empty,
                @"output must be placed in ""C:\programovani\dotnet\output""",
                "{}"),
            2);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:basic-app",
                $"project:{projectId:D}",
                "ProjectBlock",
                "delivery",
                "Basic App",
                string.Empty,
                string.Empty,
                string.Empty,
                "{}"),
            3);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:unrelated",
                $"project:{projectId:D}",
                "WorkItem",
                "task",
                "Unrelated sample",
                string.Empty,
                string.Empty,
                @"Target directory: C:\programovani\dotnet\UnrelatedSample.",
                "{}"),
            4);
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
                "UnitConverterApp",
                nodes,
                new ProcessProjectStructureContext
                {
                    ProjectId = projectId,
                    NodeId = "custom:basic-app",
                    NodeTitle = "Basic App",
                    ParentNodeId = "process-definition:4fdc77a9-6d8c-4b10-9efb-4be15732b1b0",
                    ParentNodeTitle = "Multi-team software delivery and release governance"
                }
            ]) as string;

        Assert.NotNull(summary);
        Assert.Contains("Ancestor path to the target work node:", summary, StringComparison.Ordinal);
        Assert.Contains("Basic App (custom:basic-app)", summary, StringComparison.Ordinal);
        Assert.Contains("Grounded external target paths from the selected project structure:", summary, StringComparison.Ordinal);
        Assert.Contains(@"C:\programovani\dotnet\output", summary, StringComparison.Ordinal);
        Assert.Contains("external-target/C/programovani/dotnet/output", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("UnrelatedSample", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProjectStructureGroundingSummary_includes_output_folder_from_top_level_architecture_branch_for_nested_delivery_target()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var groundingNodeType = serviceType.GetNestedType("ProjectStructureGroundingNodeData", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ProjectStructureGroundingNodeData type was not found.");
        var nodes = Array.CreateInstance(groundingNodeType, 6);
        var projectId = Guid.NewGuid();
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                $"project:{projectId:D}",
                string.Empty,
                "ProjectRoot",
                string.Empty,
                "TetrisGame",
                string.Empty,
                string.Empty,
                string.Empty,
                "{}"),
            0);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:delivery",
                $"project:{projectId:D}",
                "ProjectBlock",
                "delivery",
                "Main app",
                string.Empty,
                string.Empty,
                string.Empty,
                "{}"),
            1);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:nested-target",
                "custom:delivery",
                "WorkItem",
                "task",
                "Main app",
                string.Empty,
                string.Empty,
                string.Empty,
                "{}"),
            2);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:architecture",
                $"project:{projectId:D}",
                "ProjectBlock",
                "architecture",
                "Main architecture",
                string.Empty,
                string.Empty,
                string.Empty,
                "{}"),
            3);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:output-folder",
                "custom:architecture",
                "ProjectBlock",
                "delivery",
                "Output folder",
                string.Empty,
                string.Empty,
                string.Empty,
                "{}"),
            4);
        nodes.SetValue(
            CreateProjectStructureGroundingNode(
                groundingNodeType,
                "custom:output-path",
                "custom:output-folder",
                "File",
                string.Empty,
                @"C:\programovani\dotnet-demo\output",
                string.Empty,
                string.Empty,
                @"C:\programovani\dotnet-demo\output",
                "{}"),
            5);
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
                "TetrisGame",
                nodes,
                new ProcessProjectStructureContext
                {
                    ProjectId = projectId,
                    NodeId = "process-definition:672935c3-f687-4255-b8bf-90528248c642",
                    NodeTitle = "Blazor app delivery",
                    ParentNodeId = "custom:nested-target",
                    ParentNodeTitle = "Main app"
                }
            ]) as string;

        Assert.NotNull(summary);
        Assert.Contains("Grounded external target paths from the selected project structure:", summary, StringComparison.Ordinal);
        Assert.Contains(@"C:\programovani\dotnet-demo\output", summary, StringComparison.Ordinal);
        Assert.Contains("external-target/C/programovani/dotnet-demo/output", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_requires_external_target_final_delivery_proof_when_grounded()
    {
        var buildExecutionPromptCore = typeof(ProcessRunAutomationDispatchService)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var candidate = CreateDispatchCandidateCore(
            "Build the Blazor app and prove it runs.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Implementation change set", true, "Must name changed files and validation proof.")],
            [],
            triggerReason: "Started from project structure API.",
            stepTitle: "Build Blazor application",
            processName: "Blazor app delivery");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `TetrisGame` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet-demo\output` mapped to `external-target/C/programovani/dotnet-demo/output` from Output folder (custom:output-path)
            """;

        var prompt = buildExecutionPromptCore.Invoke(
            null,
            [
                candidate,
                null,
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("the final runnable product must be delivered into the grounded external target", prompt, StringComparison.Ordinal);
        Assert.Contains("Workspace-only proof is not sufficient when an external target is grounded", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_allows_external_target_from_project_structure_grounding()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the requested app and prove build, tests, and startup smoke.",
            new ProcessProjectStructureContext
            {
                ProjectId = Guid.NewGuid(),
                NodeId = "custom:basic-app",
                NodeTitle = "Basic App",
                ParentNodeId = "process-definition:4fdc77a9-6d8c-4b10-9efb-4be15732b1b0",
                ParentNodeTitle = "Multi-team software delivery and release governance"
            });
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `UnitConverterApp` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet\output` mapped to `external-target/C/programovani/dotnet/output` from output note (custom:output-note)
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        var aliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains("external-target/C/programovani/dotnet/output", aliases);
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_keeps_backup_and_evidence_roots_read_only_for_mutating_step()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the requested app and prove build, tests, and startup smoke.",
            new ProcessProjectStructureContext
            {
                ProjectId = Guid.NewGuid(),
                NodeId = "custom:basic-app",
                NodeTitle = "Basic App",
                ParentNodeId = "process-definition:4fdc77a9-6d8c-4b10-9efb-4be15732b1b0",
                ParentNodeTitle = "Blazor app delivery"
            });
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `SamplePwaApp` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-170653 Approved product root for this run` mapped to `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653 Approved product root for this run` from run note (custom:run-note)
            - `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-170653\project-structure-backup` mapped to `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653/project-structure-backup` from backup note (custom:backup-note)
            - `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-170653\agent-evidence` mapped to `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653/agent-evidence` from evidence note (custom:evidence-note)
            - `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-170653\product` mapped to `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653/product` from product root note (custom:product-note)
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        var allowedAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        var allowedAlias = Assert.Single(allowedAliases);
        Assert.Equal("external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653/product", allowedAlias);
        Assert.Contains("external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653/project-structure-backup", readOnlyAliases);
        Assert.Contains("external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-170653/agent-evidence", readOnlyAliases);
        Assert.DoesNotContain(readOnlyAliases, alias => string.Equals(alias, allowedAlias, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_keeps_current_upstream_product_root_read_only_when_project_structure_product_is_stale()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Implement the requested app and prove build, tests, and startup smoke.",
            ProcessStepKind.Work,
            [],
            false,
            [],
            [],
            triggerReason: "Deliver the generated application showcase.",
            runName: "Codex live Blazor delivery 20260522-190813",
            processName: "Blazor app delivery",
            outputContractSummary: "Buildable Blazor implementation");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `SamplePwaApp` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-181000\product` mapped to `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-181000/product` from old run note (custom:old-run)
            - `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-190813\project-structure-backup` mapped to `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813/project-structure-backup` from current run instructions (custom:current-run)
            """;
        const string artifactInspectionGroundingSummary = """
            Upstream artifact excerpt:
            - Product root: `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813/product`
            - Output root: `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-190813`
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                artifactInspectionGroundingSummary
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        var allowedAliases = document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out var allowedAliasesElement)
            ? allowedAliasesElement
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray()
            : [];
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Empty(allowedAliases);
        Assert.Contains("external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813/product", readOnlyAliases);
        Assert.Contains("external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813/project-structure-backup", readOnlyAliases);
        Assert.DoesNotContain(
            readOnlyAliases,
            alias => string.Equals(
                alias,
                "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-181000/product",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_grants_read_only_external_target_alias_for_explicit_repair_revalidation_contract()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Revalidate the repaired app by building it, launching it, and capturing browser proof.",
            ProcessStepKind.Review,
            [],
            false,
            [],
            [],
            triggerReason: "Deliver the generated application showcase.",
            stepTitle: "Revalidate Blazor repair",
            processName: "Blazor app delivery",
            outputContractSummary: "Fresh runtime and browser validation evidence");
        var stepDefinition = (ProcessStepDefinition)(candidate.GetType().GetProperty("StepDefinition")?.GetValue(candidate)
            ?? throw new InvalidOperationException("DispatchCandidate.StepDefinition was not available."));
        stepDefinition.AllowedOperations =
        [
            ProcessStepOperation.ReadProcessContext,
            ProcessStepOperation.ReadProjectStructure,
            ProcessStepOperation.ReadUpstreamArtifacts,
            ProcessStepOperation.WriteManagedProcessArtifacts,
            ProcessStepOperation.RunValidation,
            ProcessStepOperation.LaunchRuntime,
            ProcessStepOperation.CaptureRuntimeProof
        ];
        stepDefinition.OperationTargetScope = ProcessStepTargetScope.ExternalProductTargetReadOnly;
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `TetrisGame` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet-demo\output` mapped to `external-target/C/programovani/dotnet-demo/output` from output note (custom:output-note)
            """;
        const string artifactInspectionGroundingSummary = """
            Upstream repair evidence references:
            - Host project: `external-target/C/programovani/dotnet-demo/output/output.csproj`
            - Test project: `external-target/C/programovani/dotnet-demo/output/tests/output.Tests/output.Tests.csproj`
            - Diagnostic artifact: `external-target/C/Users/lucys/AppData/Local/CanDoItAll/workspace/artifacts/scopes/organization/1170ea92148839066da5cdc49c98874e/pr`
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                artifactInspectionGroundingSummary
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out _));
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("external-target/C/programovani/dotnet-demo/output", readOnlyAliases);
        Assert.DoesNotContain(
            readOnlyAliases,
            alias => string.Equals(
                alias,
                "external-target/C/Users/lucys/AppData/Local/CanDoItAll/workspace/artifacts/scopes/organization/1170ea92148839066da5cdc49c98874e/pr",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_grants_read_only_upstream_external_artifact_paths_for_managed_review_contract()
    {
        var candidate = (ProcessRunAutomationDispatchService.DispatchCandidate)CreateReviewDispatchCandidateWithArtifactInputs(
            "Escalate unresolved Blazor repair findings after reading the inherited product files.",
            (
                "Revalidate Blazor repair",
                "Blazor repair evidence",
                [
                    (
                        "Game.razor",
                        "Deliverable",
                        "external-target/C/programovani/dotnet-demo/output/Components/Pages/Game.razor",
                        "Component file inspected during the upstream repair step.",
                        "external-target"),
                    (
                        "output.csproj",
                        "Deliverable",
                        "external-target/C/programovani/dotnet-demo/output/output.csproj",
                        "Project file inspected during the upstream repair step.",
                        "external-target")
                ]));
        candidate.StepDefinition.AllowedOperations =
        [
            ProcessStepOperation.ReadProcessContext,
            ProcessStepOperation.ReadUpstreamArtifacts,
            ProcessStepOperation.WriteManagedProcessArtifacts,
            ProcessStepOperation.EscalateOrDecide
        ];
        candidate.StepDefinition.OperationTargetScope = ProcessStepTargetScope.ManagedProcessArtifactsOnly;
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `TetrisGame` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet-demo\output` mapped to `external-target/C/programovani/dotnet-demo/output` from output note (custom:output-note)
            """;

        var metadataJson = ProcessRunAutomationDispatchService.ProcessInvocationMetadataBuilder.Build(
            candidate,
            new ExecutionInvocationPolicy(),
            projectStructureGroundingSummary,
            null);

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out _));
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("external-target/C/programovani/dotnet-demo/output/Components/Pages/Game.razor", readOnlyAliases);
        Assert.Contains("external-target/C/programovani/dotnet-demo/output/output.csproj", readOnlyAliases);
        Assert.DoesNotContain(
            readOnlyAliases,
            alias => string.Equals(
                alias,
                "external-target/C/programovani/dotnet-demo/output",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_SB04_INV_001_keeps_stale_upstream_product_alias_read_only_for_mutating_step()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Implement the requested app and prove build, tests, and startup smoke.",
            ProcessStepKind.Work,
            [],
            false,
            [],
            [],
            triggerReason: "Deliver the generated application showcase.",
            runName: "Codex live Blazor delivery 20260522-190813",
            processName: "Blazor app delivery",
            outputContractSummary: "Buildable Blazor implementation");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `SamplePwaApp` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-190813\product` mapped to `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813/product` from current product root note (custom:current-run)
            """;
        const string artifactInspectionGroundingSummary = """
            Upstream artifact stale excerpt:
            - Prior failed attempt wrote into sibling product root `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813-sibling/product`.
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                artifactInspectionGroundingSummary
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        var allowedAliases = document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out var allowedAliasesElement)
            ? allowedAliasesElement
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray()
            : [];
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813/product", allowedAliases);
        Assert.DoesNotContain(
            allowedAliases,
            alias => string.Equals(
                alias,
                "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813-sibling/product",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813-sibling/product",
            readOnlyAliases);
    }

    [Fact]
    public void ProcessInvocationMetadataBuilder_SB04_INV_001_builds_external_artifact_destination_metadata_without_reflection()
    {
        var candidate = (ProcessRunAutomationDispatchService.DispatchCandidate)CreateDispatchCandidateCore(
            "Create the market expansion business plan and write it to the governed report destination.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Business plan report", true, "Must include market assumptions, budget, owners, and approval criteria.")],
            [],
            triggerReason: "Prepare a business plan report for executive review.",
            stepTitle: "Create business plan report",
            processName: "Business planning workflow",
            outputContractSummary: "Operation contract: allowed operations WriteExternalArtifactDestination and WriteManagedProcessArtifacts; target scope ExternalArtifactDestination.");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `MarketPlan` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\business\market-plan\reports` mapped to `external-target/C/business/market-plan/reports` from report destination note (custom:report-destination)
            """;

        var metadataJson = ProcessRunAutomationDispatchService.ProcessInvocationMetadataBuilder.Build(
            candidate,
            new ExecutionInvocationPolicy(),
            projectStructureGroundingSummary,
            null);

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.Equal(
            "ExternalArtifactDestination",
            document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey).GetString());
        Assert.False(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey).GetBoolean());
        var allowedAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("external-target/C/business/market-plan/reports", allowedAliases);
    }

    [Fact]
    public void ProcessStepOperationContractResolver_SB04_INV_001_resolves_persisted_contract_without_reflection()
    {
        var stepDefinition = new ProcessStepDefinition
        {
            AllowedOperations =
            [
                ProcessStepOperation.WriteManagedProcessArtifacts,
                ProcessStepOperation.WriteExternalArtifactDestination
            ],
            OperationTargetScope = ProcessStepTargetScope.ExternalArtifactDestination
        };

        var resolved = ProcessRunAutomationDispatchService.ProcessStepOperationContractResolver.TryResolvePersistedOperationContract(
            stepDefinition,
            out var contract);

        Assert.True(resolved);
        Assert.True(contract.IsExplicit);
        Assert.Equal(ProcessStepTargetScope.ExternalArtifactDestination, contract.TargetScope);
        Assert.Equal(
            ProcessStepOperationContractState.NormalizeDeclaredContract(
                stepDefinition.StepKind,
                stepDefinition.AllowedOperations,
                stepDefinition.OperationTargetScope,
                inferMissingTargetScope: true).AllowedOperations,
            contract.AllowedOperations);
        Assert.Contains(ProcessStepOperation.ReadProcessContext, contract.AllowedOperations);
        Assert.Contains(ProcessStepOperation.WriteExternalArtifactDestination, contract.AllowedOperations);
        Assert.DoesNotContain(ProcessStepOperation.MutateProductTarget, contract.AllowedOperations);
    }

    [Fact]
    public void ProcessTargetGroundingLedgerBuilder_SB04_INV_001_resolves_current_run_grounding_without_reflection()
    {
        var candidate = (ProcessRunAutomationDispatchService.DispatchCandidate)CreateDispatchCandidateCore(
            "Implement the requested app and prove build, tests, and startup smoke.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Delivery package", true, "Must identify concrete product files.")],
            [],
            triggerReason: "Deliver the generated application showcase.",
            runName: "Codex live Blazor delivery 20260522-190813",
            processName: "Blazor app delivery",
            outputContractSummary: "Buildable Blazor implementation");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `SamplePwaApp` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-190813\product` mapped to `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813/product` from current product root note (custom:current-run)
            """;

        var groundings = ProcessRunAutomationDispatchService.ProcessTargetGroundingLedgerBuilder.ResolveExternalTargetGroundings(
            candidate,
            projectStructureGroundingSummary,
            null);
        var writableAliases = ProcessRunAutomationDispatchService.ProcessTargetGroundingLedgerBuilder.ResolveMutableExternalTargetAliases(
            candidate,
            groundings);

        Assert.Contains(
            groundings,
            grounding =>
                grounding.Authority == ProcessRunAutomationDispatchService.ProcessTargetGroundingAuthority.Writable &&
                string.Equals(
                    grounding.Alias,
                    "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813/product",
                    StringComparison.OrdinalIgnoreCase));
        Assert.Contains("external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-190813/product", writableAliases);
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_keeps_current_product_root_read_only_for_validation_when_descendant_product_path_exists()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Validate Blazor runtime and browser evidence. Inspect inherited source artifacts before accepting the implementation.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must include browser proof, console messages, screenshots, and inspected source artifact evidence.")],
            [],
            triggerReason: "Validate current Blazor run outputs under C:\\programovani\\dotnet-demo\\output\\codex-live-blazor-20260522-192839.",
            stepTitle: "Validate Blazor runtime and browser evidence",
            processName: "Blazor app delivery",
            runName: "Codex live Blazor delivery 20260522-192839");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `SamplePwaApp` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-181000\product` mapped to `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-181000/product` from old run note (custom:old-run)
            - `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-192839\project-structure-backup` mapped to `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-192839/project-structure-backup` from current backup note (custom:backup-note)
            """;
        const string artifactInspectionGroundingSummary = """
            Upstream artifact excerpt:
            - Product root: `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-192839/product`
            - Test project root: `external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-192839/product/tests/product`
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                artifactInspectionGroundingSummary
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.Equal(
            "AnalysisDesign",
            document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepExecutionBoundaryMetadataKey).GetString());
        Assert.Equal(
            AgentWorkspaceToolAccessProfiles.ArchitectureReviewProfileKey,
            document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessWorkspaceToolProfileMetadataKey).GetString());
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out _));
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains(
            "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-192839/product",
            readOnlyAliases);
        Assert.Contains(
            "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-192839/product/tests/product",
            readOnlyAliases);
        Assert.DoesNotContain(
            readOnlyAliases,
            alias => string.Equals(
                alias,
                "external-target/C/programovani/dotnet-demo/output/codex-live-blazor-20260522-181000/product",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_sets_context_workspace_scope_from_project_structure_context()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var projectId = Guid.Parse("ea4d3293-ad91-4939-a645-c8f2402a6400");
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the requested app using the selected project-structure branch.",
            new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "custom:basic-app",
                NodeTitle = "Basic App",
                ParentNodeId = "process-definition:4fdc77a9-6d8c-4b10-9efb-4be15732b1b0",
                ParentNodeTitle = "Multi-team software delivery and release governance"
            });

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                null,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        var scope = document.RootElement.GetProperty(ExecutionInvocationMetadata.ContextWorkspaceScopeMetadataKey);
        Assert.Equal(nameof(WorkspaceScopeKind.Project), scope.GetProperty("kind").GetString());
        Assert.Equal(projectId.ToString("D"), scope.GetProperty("key").GetString());
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_keeps_delegated_change_execution_writable_even_with_safety_review_text()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Execute delegated work; AI safety reviewer may halt execution when boundary breaches appear. Output must include concrete product files for a plain static JavaScript app.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Delegated change set", true, "Must identify concrete product files created or changed under the grounded product root.")],
            [],
            triggerReason: "Deliver the generated application showcase.",
            stepTitle: "Run delegated execution and capture full trace",
            processName: "AI-assisted change delivery with guarded delegation",
            outputContractSummary: "Draft change output plus full execution trace.");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `StaticTimerApp` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\js-timer` mapped to `external-target/C/programovani/js-timer` from product root note (custom:root-note)
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        var aliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains("external-target/C/programovani/js-timer", aliases);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey, out _));
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_marks_architecture_external_target_read_only()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Review architecture and source-of-truth impact. Produce the ADR as a managed process artifact.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Decision, "Slice architecture decision record", true, "Architecture decision must not mutate the product root.")],
            [],
            triggerReason: "Validate architecture for a greenfield CLI app.",
            stepTitle: "Check architecture and source-of-truth impact",
            processName: ".NET implementation slice with atomic validation",
            outputContractSummary: "Architecture decision record or no-ADR rationale.");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `TodoSummary` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\todo-summary` mapped to `external-target/C/programovani/todo-summary` from product root note (custom:root-note)
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out _));
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains("external-target/C/programovani/todo-summary", readOnlyAliases);
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_marks_scope_external_product_root_read_only()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Capture implementation slice boundary for a .NET CLI app. This step records scope only.",
            ProcessStepKind.Start,
            [],
            false,
            [(ProcessArtifactKind.Brief, "Implementation slice scope packet", true, "Must capture product root, feature boundary, assumptions, and validation hooks.")],
            [],
            triggerReason: "Create a .NET CLI app from the project mindmap.",
            stepTitle: "Capture implementation slice boundary",
            processName: ".NET implementation slice with atomic validation",
            outputContractSummary: "Implementation slice scope packet.");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `TodoSummary` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\candoitall-processes1-dotnet-cli-h` mapped to `external-target/C/programovani/candoitall-processes1-dotnet-cli-h` from Product root (custom:target)
            Requirements from project-level planning context:
            - Output folder: C:\programovani\candoitall-processes1-dotnet-cli-h
            - Solution name TodoSummary.
            - Console app project src/TodoSummary.Console.
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out _));
        var readOnlyAliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains("external-target/C/programovani/candoitall-processes1-dotnet-cli-h", readOnlyAliases);
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_allows_external_artifact_destination_without_product_mutation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Create the market expansion business plan and write it to the governed report destination.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Business plan report", true, "Must include market assumptions, budget, owners, and approval criteria.")],
            [],
            triggerReason: "Prepare a business plan report for executive review.",
            stepTitle: "Create business plan report",
            processName: "Business planning workflow",
            outputContractSummary: "Operation contract: allowed operations WriteExternalArtifactDestination and WriteManagedProcessArtifacts; target scope ExternalArtifactDestination.");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `MarketPlan` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\business\market-plan\reports` mapped to `external-target/C/business/market-plan/reports` from report destination note (custom:report-destination)
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.Equal(
            "ExternalArtifactDestination",
            document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey).GetString());
        Assert.False(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey).GetBoolean());
        var operations = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains("WriteExternalArtifactDestination", operations);
        Assert.DoesNotContain("MutateProductTarget", operations);
        var allowedAliases = document.RootElement.TryGetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey, out var allowedAliasesElement)
            ? allowedAliasesElement
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray()
            : [];
        Assert.Contains("external-target/C/business/market-plan/reports", allowedAliases);
        var ledger = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ProcessGroundedTargetAliasLedgerMetadataKey)
            .EnumerateArray()
            .ToArray();
        var reportLedgerEntry = Assert.Single(ledger, item =>
            string.Equals(
                item.GetProperty("alias").GetString(),
                "external-target/C/business/market-plan/reports",
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                item.GetProperty("intendedUse").GetString(),
                "current-run-target",
                StringComparison.Ordinal)
            && string.Equals(
                item.GetProperty("trustLevel").GetString(),
                "trusted-current-run",
                StringComparison.Ordinal));
        Assert.Equal("Writable", reportLedgerEntry.GetProperty("effectiveAccess").GetString());
        Assert.Equal("current-run-target", reportLedgerEntry.GetProperty("intendedUse").GetString());
        Assert.Equal("trusted-current-run", reportLedgerEntry.GetProperty("trustLevel").GetString());
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_SB08_INV_001_uses_persisted_operation_contract_without_text_markers()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Create the governed executive report and place it in the selected report destination.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Executive report", true, "Must include owner, assumptions, findings, and approval criteria.")],
            [],
            triggerReason: "Prepare an executive report for the selected destination.",
            stepTitle: "Create executive report",
            processName: "Executive reporting workflow",
            outputContractSummary: "Executive report.");
        var stepDefinition = (ProcessStepDefinition)(candidate.GetType().GetProperty("StepDefinition")?.GetValue(candidate)
            ?? throw new InvalidOperationException("DispatchCandidate.StepDefinition was not available."));
        stepDefinition.AllowedOperations =
        [
            ProcessStepOperation.WriteManagedProcessArtifacts,
            ProcessStepOperation.WriteExternalArtifactDestination
        ];
        stepDefinition.OperationTargetScope = ProcessStepTargetScope.ExternalArtifactDestination;
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `ExecutiveReports` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\business\executive-reports` mapped to `external-target/C/business/executive-reports` from report destination note (custom:report-destination)
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.Equal(
            ProcessStepTargetScope.ExternalArtifactDestination.ToString(),
            document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey).GetString());
        Assert.False(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey).GetBoolean());
        var operations = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains(ProcessStepOperation.WriteExternalArtifactDestination.ToString(), operations);
        Assert.Contains(ProcessStepOperation.WriteManagedProcessArtifacts.ToString(), operations);
        Assert.DoesNotContain(ProcessStepOperation.MutateProductTarget.ToString(), operations);
    }

    [Fact]
    public async Task ToolPolicy_rejects_product_mutation_against_read_only_process_boundary()
    {
        var policy = new DefaultAgentToolInvocationPolicy();
        var decision = await policy.EvaluateAsync(
            new ToolInvocationPolicyContext(
                Guid.NewGuid(),
                "Architecture reviewer",
                "workspace_write_file",
                new Dictionary<string, string>
                {
                    ["path"] = "external-target/C/programovani/todo-summary/src/Program.cs",
                    ["content"] = "Console.WriteLine(\"changed\");"
                },
                ToolInvocationClassification.Mutation,
                IsKnownTool: true,
                AutoApprovalAllowed: true,
                ApprovalWrapperAvailable: false,
                ExecutionRunId: Guid.NewGuid().ToString("D"),
                SourceKind: "process-step",
                ProcessRunId: Guid.NewGuid().ToString("D"),
                ProcessStepId: Guid.NewGuid().ToString("D"),
                AllowedExternalTargetAliases: [],
                ReadOnlyExternalTargetAliases: ["external-target/C/programovani/todo-summary"],
                ProcessAllowsProductMutation: false,
                ProcessStepAllowedOperations: [ProcessStepOperation.MutateProductTarget.ToString()],
                ProcessStepTargetScope: ProcessStepTargetScope.ExternalProductTargetReadOnly.ToString()),
            CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, decision.Kind);
        Assert.Contains("not authorized to mutate product targets", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("read-only", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_disables_browser_tools_for_dotnet_solution_setup_scaffold_step()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Create the .NET solution and app project. Build, test, run, and browser proof are downstream validation steps.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Solution scaffold", true, "Must create the requested solution and project files.")],
            [],
            triggerReason: "Create a Blazor counter app.",
            stepTitle: "Create solution and .NET app project",
            processName: ".NET solution setup subprocess",
            processSlug: "dotnet-solution-setup",
            stepKey: "create-dotnet-project");

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                null,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.False(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey).GetBoolean());
        Assert.True(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessScaffoldToolOnlyMetadataKey).GetBoolean());
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_disables_browser_tools_for_dotnet_solution_setup_scaffold_step_when_slug_is_missing()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Create the .NET solution and app project. Build, test, run, and browser proof are downstream validation steps.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Solution scaffold", true, "Must create the requested solution and project files.")],
            [],
            triggerReason: "Create a console app.",
            stepTitle: "Create solution and .NET app project",
            processName: ".NET solution setup subprocess",
            processSlug: "",
            stepKey: "create-dotnet-project");

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                null,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.False(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey).GetBoolean());
        Assert.True(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessScaffoldToolOnlyMetadataKey).GetBoolean());
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_allows_browser_tools_for_browser_proof_step()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for the Blazor browser app. Capture screenshot-backed evidence.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must include browser proof, screenshot, console messages, and unresolved risks.")],
            [],
            triggerReason: "Validate the requested Blazor browser app.",
            stepTitle: "Run QA validation and browser proof",
            processName: ".NET implementation slice with atomic validation");

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                null,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.True(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey).GetBoolean());
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_allows_browser_tools_for_static_web_qa_recheck()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Verify the repaired package from the source document and project structure. The deliverable is a JavaScript static web page with no backend.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Repaired regression evidence pack", true, "Must name repaired flows, assertion depth, warning counts, executed-test counts when tests are expected, runtime/API/browser evidence as applicable, screenshots for UI surfaces, and unresolved risks after the repair pass.")],
            [],
            triggerReason: "Run launched from selected project-structure node.",
            stepTitle: "Re-run QA validation and runtime or browser proof after repair",
            processName: "Multi-team software delivery");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet-demo\output` mapped to `external-target/C/programovani/dotnet-demo/output` from Artifact destination folder (node-target)
            Project-structure required artifact contract:
            - Delivery shape: JavaScript static web page.
            - Runtime: no backend; client-local state only.
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.True(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey).GetBoolean());
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_disables_browser_tools_for_peer_review_with_prior_browser_failure_recovery()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Inputs: implementation package, architecture decision record, and changed-surface inventory. Outputs: peer-reviewed change set with explicit residual risk and follow-up items.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Peer review note", true, "Must capture accepted issues, rejected concerns, and explicit residual risk.")],
            [
                (
                    "Implement bounded delivery change",
                    "Implementation change set",
                    [
                        (
                            "Implementation change set",
                            "Deliverable",
                            "artifacts/process-runs/run-1/03-implementation-change-set.md",
                            "Static browser app delivered; downstream QA will capture browser proof.",
                            "Created by implementation step."
                        )
                    ])
            ],
            triggerReason: "Project structure says the product is a static web page hosted from an external output directory.",
            stepTitle: "Complete peer review and integration readiness",
            outputContractSummary: "Peer review note",
            manualRecoveryDirective: """
                Manual rerun requested for step 'Complete peer review and integration readiness'.
                Generic dispatcher repair: static-web project grounding identifies the deliverable surface, but peer review does not require browser-proof tools unless this step contract explicitly asks for browser/runtime proof.
                Prior blocked reason: Mandatory browser proof and PowerShell run-script gating tools were not executed: unable to capture browser_snapshot, browser_take_screenshot, and browser_console_messages because no reachable hosted URL or environment run-script was provided in this run.
                """);
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\dotnet-demo\output` mapped to `external-target/C/programovani/dotnet-demo/output` from Artifact destination folder (node-target)
            Project-structure required artifact contract:
            - Delivery shape: JavaScript static web page.
            - Runtime: no backend; client-local state only.
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.False(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey).GetBoolean());
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_disables_browser_tools_for_security_review_after_qa_browser_proof()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Review sensitive-data handling, secrets, boundary changes, and policy exceptions for the QA-accepted package.",
            ProcessStepKind.Approval,
            [],
            false,
            [(ProcessArtifactKind.Decision, "Security exception assessment", true, "Must capture controls, residual risk owner, and approval or block rationale.")],
            [
                (
                    "Run QA validation and runtime or browser proof",
                    "Regression evidence pack",
                    [
                        (
                            "Regression evidence pack",
                            "Evidence",
                            "artifacts/process-runs/run-1/05-regression-evidence-pack.md",
                            "Quality accepted with browser proof, screenshot, console messages, and residual risks recorded.",
                            "Created by QA validation step."
                        )
                    ])
            ],
            triggerReason: "Project structure says the product is a static web page hosted from an external output directory.",
            stepTitle: "Perform security and data-handling review",
            outputContractSummary: "Security outcome with explicit approval, block, or exception rationale.");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure and focused this prompt on the selected work branch.
            Project-structure required artifact contract:
            - Delivery shape: JavaScript static web page.
            - Runtime: no backend; client-local state only.
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        Assert.False(document.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey).GetBoolean());
    }

    [Fact]
    public void BuildExecutionPromptCore_guides_security_review_to_inspect_inherited_browser_evidence_without_fresh_browser_gate()
    {
        var buildExecutionPromptCore = typeof(ProcessRunAutomationDispatchService)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var candidate = CreateDispatchCandidateCore(
            "Review sensitive-data handling, secrets, boundary changes, and policy exceptions for the QA-accepted package.",
            ProcessStepKind.Approval,
            [],
            false,
            [(ProcessArtifactKind.Decision, "Security exception assessment", true, "Must capture controls, residual risk owner, and approval or block rationale.")],
            [
                (
                    "Run QA validation and runtime or browser proof",
                    "Regression evidence pack",
                    [
                        (
                            "Regression evidence pack",
                            "Evidence",
                            "artifacts/process-runs/run-1/05-regression-evidence-pack.md",
                            "Quality accepted with browser proof, screenshot, console messages, and residual risks recorded.",
                            "Created by QA validation step."
                        )
                    ])
            ],
            triggerReason: "Project structure says the product is a static web page hosted from an external output directory.",
            stepTitle: "Perform security and data-handling review",
            outputContractSummary: "Security outcome with explicit approval, block, or exception rationale.");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure and focused this prompt on the selected work branch.
            Project-structure required artifact contract:
            - Delivery shape: JavaScript static web page.
            - Runtime: no backend; client-local state only.
            """;

        var prompt = buildExecutionPromptCore.Invoke(
            null,
            [
                candidate,
                null,
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Browser proof boundary:", prompt, StringComparison.Ordinal);
        Assert.Contains("this step is not browser-proof gated", prompt, StringComparison.Ordinal);
        Assert.Contains("inspect those inherited artifact paths directly with workspace tools", prompt, StringComparison.Ordinal);
        Assert.Contains("artifacts/process-runs/run-1/05-regression-evidence-pack.md", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Mandatory browser proof execution plan:", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("`browser_snapshot`", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("`browser_take_screenshot`", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildProcessInvocationMetadataJson_allows_external_artifact_destination_writes()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("BuildProcessInvocationMetadataJson", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildProcessInvocationMetadataJson method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Draft the business plan, marketing plan, and financial model for the reviewed product.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Business plan", true, "Must include customer segment, offer, risks, and validation plan.")],
            [],
            triggerReason: "Analyze the reviewed app as a business case and write the plan outputs.",
            stepTitle: "Draft business plan",
            processName: "Business plan development",
            outputContractSummary: "Decision-ready business plan, marketing plan, and financial model.");
        const string projectStructureGroundingSummary = """
            Dispatcher fetched the live project structure for `Business validation project` and focused this prompt on the selected work branch.
            Grounded external target paths from the selected project structure:
            - `C:\programovani\candoitall-dev-output\orchard-shift-board-business-v9` mapped to `external-target/C/programovani/candoitall-dev-output/orchard-shift-board-business-v9` from Artifact destination folder (node-target)
            Project-structure required artifact contract:
            - Required file `02-business-plan.md` must be written at `external-target/C/programovani/candoitall-dev-output/orchard-shift-board-business-v9/02-business-plan.md`.
            """;

        var metadataJson = method.Invoke(
            null,
            [
                candidate,
                new ExecutionInvocationPolicy(),
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        using var document = JsonDocument.Parse(metadataJson);
        var aliases = document.RootElement
            .GetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains("external-target/C/programovani/candoitall-dev-output/orchard-shift-board-business-v9", aliases);
        Assert.False(document.RootElement.TryGetProperty(ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey, out _));
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
        Assert.Contains("Context preservation rules:", prompt, StringComparison.Ordinal);
        Assert.Contains("prefetched governed artifact grounding", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_read_file with a larger maxCharacters value", prompt, StringComparison.Ordinal);
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
        Assert.Contains("After browser inspection, review the bounded snapshot, screenshot, or tool-returned content", prompt, StringComparison.Ordinal);
        Assert.Contains("perform a representative user sequence", prompt, StringComparison.Ordinal);
        Assert.Contains("do not approve the proof", prompt, StringComparison.Ordinal);
        Assert.Contains("no available branch outcome represents the needed repair", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not reframe missing browser proof", prompt, StringComparison.Ordinal);
        Assert.Contains("For browser proof with an unspecified stack", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("For .NET browser proof, call `workspace_dotnet_run`", prompt, StringComparison.Ordinal);
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
        Assert.Contains("Required tool execution checklist", prompt, StringComparison.Ordinal);
        Assert.Contains("`workspace_write_file`", prompt, StringComparison.Ordinal);
        Assert.Contains("It does not replace creating, scaffolding, editing, reading, and validating real product files", prompt, StringComparison.Ordinal);
        Assert.Contains("create the real deliverable now", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not return Completed only because existing implementation-summary", prompt, StringComparison.Ordinal);
        Assert.Contains("Follow the current step contract, assigned agent instructions, available skills", prompt, StringComparison.Ordinal);
        Assert.Contains("Inspect existing files before creating or replacing scaffolds", prompt, StringComparison.Ordinal);
        Assert.Contains("Repair an existing deliverable in place", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not delete the grounded product root", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("For .NET scaffolding", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("use `workspace_dotnet_new` with `parentDirectory`", prompt, StringComparison.Ordinal);
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
    public void BuildResolvedArtifactInputs_rejects_stale_process_run_artifact()
    {
        var fixture = CreateResolvedArtifactInputFixture();
        var staleRunId = Guid.Parse("49fd0000-0000-0000-0000-000000000001");
        var staleArtifact = CreateProcessArtifactRecord(
            fixture.Expectation,
            fixture.SourceStepRun,
            staleRunId,
            $"artifacts/process-runs/{staleRunId:D}/02-implementation-change-set.md");

        var resolvedInputs = InvokeBuildResolvedArtifactInputs(fixture, [staleArtifact]);
        var resolvedInput = Assert.Single(resolvedInputs);

        Assert.Empty(resolvedInput.Artifacts);
    }

    [Fact]
    public void BuildResolvedArtifactInputs_rejects_current_run_artifact_outside_managed_root()
    {
        var fixture = CreateResolvedArtifactInputFixture();
        var wrongRootArtifact = CreateProcessArtifactRecord(
            fixture.Expectation,
            fixture.SourceStepRun,
            fixture.ProcessRunId,
            "src/GeneratedApp/App.razor");

        var resolvedInputs = InvokeBuildResolvedArtifactInputs(fixture, [wrongRootArtifact]);
        var resolvedInput = Assert.Single(resolvedInputs);

        Assert.Empty(resolvedInput.Artifacts);
    }

    [Fact]
    public void BuildResolvedArtifactInputs_accepts_current_run_managed_artifact()
    {
        var fixture = CreateResolvedArtifactInputFixture();
        var currentArtifact = CreateProcessArtifactRecord(
            fixture.Expectation,
            fixture.SourceStepRun,
            fixture.ProcessRunId,
            $"artifacts/process-runs/{fixture.ProcessRunId:D}/02-implementation-change-set.md");

        var resolvedInputs = InvokeBuildResolvedArtifactInputs(fixture, [currentArtifact]);
        var resolvedInput = Assert.Single(resolvedInputs);
        var artifact = Assert.Single(resolvedInput.Artifacts);

        Assert.Equal(currentArtifact.ManagedStoragePath, artifact.ManagedStoragePath);
    }

    [Fact]
    public void BuildExecutionPrompt_treats_escalation_no_go_steps_as_completable_decisions()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Escalate unresolved repair findings and make an explicit no-go, scope reset, or replan decision.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Decision, "Repair escalation record", true, "Must include unresolved defect list, no-go decision, next repair scope, and accountable owner.")],
            [],
            stepTitle: "Escalate unresolved repair findings");

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("unresolved product defects are the decision payload", prompt, StringComparison.Ordinal);
        Assert.Contains("return `Completed`; do not return `Blocked` merely because the product is not release-ready", prompt, StringComparison.Ordinal);
        Assert.Contains("`Completed` means the escalation decision was recorded", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_review_completion_without_upstream_artifact_inspection()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        const string upstreamArtifactPath = "artifacts/process-runs/run-001/implementation/implementation-change-set.md";
        var candidate = CreateReviewDispatchCandidateWithArtifactInputs(
            "Review the implementation change set and decide whether QA can proceed.",
            (
                "Implementation",
                "Implementation change set",
                [("Implementation change set", "Deliverable", upstreamArtifactPath, "Implementation artifact was produced.", "workspace")]));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "QA accepted the implementation handoff.",
            evidenceRefs: [upstreamArtifactPath]);
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                (
                    "workspace_stat_path",
                    new Dictionary<string, object?> { ["path"] = "artifacts/process-runs/run-001/implementation/unrelated.md" },
                    CreateProviderNativeTextResult("Path exists.")),
                (
                    "workspace_read_file",
                    new Dictionary<string, object?> { ["path"] = "artifacts/process-runs/run-001/implementation/unrelated.md" },
                    CreateProviderNativeTextResult("Read complete."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "QA validation"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("not directly inspected", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("workspace_stat_path", reason, StringComparison.Ordinal);
        Assert.Contains("workspace_read_file", reason, StringComparison.Ordinal);
        Assert.Contains(upstreamArtifactPath, reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_review_completion_when_upstream_artifacts_are_inspected()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        const string upstreamArtifactPath = "artifacts/process-runs/run-001/implementation/implementation-change-set.md";
        var candidate = CreateReviewDispatchCandidateWithArtifactInputs(
            "Review the implementation change set and decide whether QA can proceed.",
            (
                "Implementation",
                "Implementation change set",
                [("Implementation change set", "Deliverable", upstreamArtifactPath, "Implementation artifact was produced.", "workspace")]));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "QA accepted the implementation handoff.",
            evidenceRefs: [upstreamArtifactPath]);
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                (
                    "workspace_stat_path",
                    new Dictionary<string, object?> { ["path"] = upstreamArtifactPath },
                    CreateProviderNativeTextResult("Path exists.")),
                (
                    "workspace_read_file",
                    new Dictionary<string, object?> { ["path"] = upstreamArtifactPath },
                    CreateProviderNativeTextResult("Read complete."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "QA validation"]) as string;

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.NotNull(reason);
        Assert.Contains("QA accepted the implementation handoff", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRecoveryDirective_names_missing_inherited_artifact_inspections()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildRecoveryDirective = serviceType.GetMethod("BuildRecoveryDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRecoveryDirective method was not found.");
        const string changeSetPath = "artifacts/process-runs/run-001/02-blazor-implementation-change-set.md";
        const string sourcePath = "external-target/C/programovani/dotnet-demo/output/run-001/product/Features/SamplePwaApp/AppFeature.cs";
        var candidate = CreateReviewDispatchCandidateWithArtifactInputs(
            "Validate runtime and browser evidence for the inherited Blazor implementation.",
            (
                "Build Blazor application",
                "Blazor implementation change set",
                [
                    ("Blazor implementation change set", "Deliverable", changeSetPath, "Implementation artifact was produced.", "workspace"),
                    ("AppFeature.cs", "Deliverable", sourcePath, "Source artifact was produced.", "external-target")
                ]));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "QA accepted the implementation handoff.",
            evidenceRefs: [changeSetPath, sourcePath]);
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                (
                    "workspace_stat_path",
                    new Dictionary<string, object?> { ["path"] = changeSetPath },
                    CreateProviderNativeTextResult("Path exists.")),
                (
                    "workspace_read_file",
                    new Dictionary<string, object?> { ["path"] = changeSetPath },
                    CreateProviderNativeTextResult("Read complete."))));

        var directive = buildRecoveryDirective.Invoke(
            null,
            [
                candidate,
                detail,
                responseText,
                Array.Empty<string>(),
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
                1
            ]) as string;

        Assert.NotNull(directive);
        Assert.Contains("Inherited upstream artifact inspection is incomplete", directive, StringComparison.Ordinal);
        Assert.Contains("workspace_stat_path", directive, StringComparison.Ordinal);
        Assert.Contains("workspace_read_file", directive, StringComparison.Ordinal);
        Assert.Contains(sourcePath, directive, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_review_completion_when_visual_attachment_has_readable_source_evidence()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        const string evidencePackPath = "artifacts/scopes/organization/demo/process-runs/run-001/regression-evidence-pack.md";
        const string screenshotPath = "artifacts/scopes/organization/demo/process-runs/run-001/market-mosaic-home.png";
        var candidate = CreateReviewDispatchCandidateWithArtifactInputs(
            "Review the QA evidence pack and its visual browser attachment before approving security.",
            (
                "QA validation",
                "Regression evidence pack",
                [("Regression evidence pack", "Evidence", evidencePackPath, "QA evidence pack includes browser route, console, and screenshot references.", "workspace")]),
            (
                "QA validation",
                "Browser screenshot",
                [("Browser screenshot", "Evidence", screenshotPath, "Browser screenshot captured by the QA step.", "provider-native browser")]));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Security review accepted the QA evidence pack and screenshot attachment.",
            evidenceRefs: [evidencePackPath, screenshotPath]);
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                (
                    "workspace_stat_path",
                    new Dictionary<string, object?> { ["path"] = evidencePackPath },
                    CreateProviderNativeTextResult("Path exists.")),
                (
                    "workspace_read_file",
                    new Dictionary<string, object?> { ["path"] = evidencePackPath },
                    CreateProviderNativeTextResult("Browser route, console, and screenshot evidence reviewed."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Security review"]) as string;

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.NotNull(reason);
        Assert.DoesNotContain("not directly inspected", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_review_completion_when_visual_attachment_has_no_readable_source_evidence()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        const string screenshotPath = "artifacts/scopes/organization/demo/process-runs/run-001/market-mosaic-home.png";
        var candidate = CreateReviewDispatchCandidateWithArtifactInputs(
            "Review the visual browser attachment before approving security.",
            (
                "QA validation",
                "Browser screenshot",
                [("Browser screenshot", "Evidence", screenshotPath, "Browser screenshot captured by the QA step.", "provider-native browser")]));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Security review accepted the screenshot attachment.",
            evidenceRefs: [screenshotPath]);
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                (
                    "workspace_stat_path",
                    new Dictionary<string, object?> { ["path"] = "artifacts/process-runs/run-001/unrelated.png" },
                    CreateProviderNativeTextResult("Path exists."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Security review"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("workspace_stat_path", reason, StringComparison.Ordinal);
        Assert.Contains(screenshotPath, reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_review_completion_when_external_target_input_is_inspected_from_receipts()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        const string upstreamArtifactPath = "artifacts/scopes/organization/demo/process-runs/run-001/03-implementation-change-set.md";
        const string externalProjectPath = "external-target/C/programovani/dotnet/output/output.csproj";
        var candidate = CreateReviewDispatchCandidateWithArtifactInputs(
            "Review the implementation change set and decide whether QA can proceed.",
            (
                "Implementation",
                "Implementation change set",
                [
                    ("Implementation change set", "Deliverable", upstreamArtifactPath, "Implementation artifact was produced.", "workspace"),
                    ("output.csproj", "Deliverable", externalProjectPath, "Project file targeted by implementation validation.", "workspace")
                ]));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "QA accepted the implementation handoff.",
            evidenceRefs: [upstreamArtifactPath, externalProjectPath]);
        var now = DateTimeOffset.UtcNow;
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            null,
            [
                CreateToolReceipt("workspace-file", "workspace_stat_path", upstreamArtifactPath, ".", "Succeeded: Path exists.", now),
                CreateToolReceipt("workspace-file", "workspace_read_file", upstreamArtifactPath, ".", "Succeeded: Read file.", now.AddSeconds(1)),
                CreateToolReceipt("workspace-file", "workspace_stat_path", externalProjectPath, ".", "Succeeded: Path exists.", now.AddSeconds(2)),
                CreateToolReceipt("workspace-file", "workspace_read_file", externalProjectPath, ".", "Succeeded: Read file.", now.AddSeconds(3))
            ]);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "QA validation"]) as string;

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.NotNull(reason);
        Assert.Contains("QA accepted the implementation handoff", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_review_completion_when_upstream_artifact_input_is_missing()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateReviewDispatchCandidateWithArtifactInputs(
            "Review the implementation change set and decide whether QA can proceed.",
            (
                "Implementation",
                "Implementation change set",
                []));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "QA accepted the implementation handoff.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                (
                    "workspace_stat_path",
                    new Dictionary<string, object?> { ["path"] = "artifacts/process-runs/run-001/implementation/unrelated.md" },
                    CreateProviderNativeTextResult("Path exists.")),
                (
                    "workspace_read_file",
                    new Dictionary<string, object?> { ["path"] = "artifacts/process-runs/run-001/implementation/unrelated.md" },
                    CreateProviderNativeTextResult("Read complete."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "QA validation"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("required upstream artifacts are missing", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Implementation", reason, StringComparison.Ordinal);
        Assert.Contains("Implementation change set", reason, StringComparison.Ordinal);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_run",
                    "SimpleWorkflowApp.csproj",
                    "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp",
                    "Succeeded",
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_run",
                    "Units.slnx",
                    "deliveries/workflow-suite/src",
                    "Succeeded",
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
    public void ResolveCompletionStatusWithCarryForward_allows_finalizer_only_retry_after_verified_recipe_app_attempt()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveCompletionStatusWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 5)
            ?? throw new InvalidOperationException("ResolveCompletionStatusWithCarryForward method was not found.");
        var buildCompletionReason = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "BuildCompletionReasonWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 6)
            ?? throw new InvalidOperationException("BuildCompletionReasonWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidateWithStepTitle(
            "Implement feature, tests, and migration notes",
            "Implement a recipe cost explorer web application, prove build, tests, and startup smoke, then record delivery notes.");
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Finalizer retry recorded the already verified recipe cost explorer implementation.",
            summaryMarkdown: "The prior implementation attempt already produced concrete source inspection and runnable host proof.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionStateWithMessages(
                ("assistant", [CreateTextContent(responseText)])));
        var carriedProof = CreateCarriedImplementationProof(
            hasConcreteImplementationProof: true,
            hasRunnableApplicationProof: true);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(
            null,
            [candidate, detail, new[] { "workspace_write_file" }, responseText, carriedProof]);
        var reason = buildCompletionReason.Invoke(
            null,
            [
                candidate,
                detail,
                "Implement feature, tests, and migration notes",
                new[] { "workspace_write_file" },
                responseText,
                carriedProof
            ]) as string;

        Assert.True(status == ProcessStepRunStatus.Completed, $"{status}: {reason}");
        Assert.DoesNotContain("current-attempt implementation proof", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatusWithCarryForward_blocks_repair_step_that_only_reuses_prior_implementation_proof()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveCompletionStatusWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 5)
            ?? throw new InvalidOperationException("ResolveCompletionStatusWithCarryForward method was not found.");
        var buildCompletionReason = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "BuildCompletionReasonWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 6)
            ?? throw new InvalidOperationException("BuildCompletionReasonWithCarryForward method was not found.");
        var resolveMissingProof = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveMissingConcreteImplementationProofSummaryWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 3)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummaryWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidateCore(
            "Repair validation findings for a JavaScript browser app after QA rejected placeholder validation.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Quality repair change set", true, "Must name repaired defects, changed files or deliverables, rerun validation, and unresolved risk.")],
            [],
            stepTitle: "Repair validation findings");
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Repair handoff recorded from existing evidence.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionStateWithMessages(
                ("assistant", [CreateTextContent(responseText)])),
            [
                new ProcessAutomationToolExecutionReceipt(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/programovani/candoitall-dev-output/orchard-shift-board-v9-js/public/app.js",
                    ".",
                    "Succeeded",
                    now,
                    now)
            ]);
        var carriedProof = CreateCarriedImplementationProof(
            hasConcreteImplementationProof: true,
            hasRunnableApplicationProof: true);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(
            null,
            [candidate, detail, Array.Empty<string>(), responseText, carriedProof]);
        var missingProof = resolveMissingProof.Invoke(
            null,
            [candidate, detail, carriedProof]) as string;
        var reason = buildCompletionReason.Invoke(
            null,
            [
                candidate,
                detail,
                "Repair validation findings",
                Array.Empty<string>(),
                responseText,
                carriedProof
            ]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(missingProof);
        Assert.Contains("current repair attempt did not mutate", missingProof, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(reason);
    }

    [Fact]
    public void ResolveMissingConcreteImplementationProofSummary_accepts_current_mjs_repair_mutation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingProof = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveMissingConcreteImplementationProofSummaryWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 3)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummaryWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidateCore(
            "Repair validation findings for a JavaScript browser app after QA rejected placeholder validation.",
            ProcessStepKind.Work,
            [],
            false,
            [],
            [],
            stepTitle: "Repair validation findings");
        var now = DateTimeOffset.UtcNow;
        const string changedPath = "external-target/C/programovani/candoitall-dev-output/orchard-shift-board-v9-js/tools/lint.mjs";
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Repair changed the JavaScript lint module and re-read the changed product file.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionStateWithMessages(
                ("assistant", [CreateTextContent(responseText)])),
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    changedPath,
                    ".",
                    $"Succeeded: Overwrote '{changedPath}' with 738 characters.",
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_read_file",
                    changedPath,
                    ".",
                    "Succeeded: Read changed lint module.",
                    now.AddSeconds(1))
            ]);
        var carriedProof = CreateCarriedImplementationProof(
            hasConcreteImplementationProof: true,
            hasRunnableApplicationProof: true);

        var missingProof = resolveMissingProof.Invoke(
            null,
            [candidate, detail, carriedProof]) as string;

        Assert.Equal(string.Empty, missingProof);
    }

    [Fact]
    public void ResolveMissingConcreteImplementationProofSummary_accepts_prior_recovery_mutation_with_fresh_read()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCarriedProof = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveCarriedImplementationProof", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 3)
            ?? throw new InvalidOperationException("ResolveCarriedImplementationProof method was not found.");
        var resolveMissingProof = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveMissingConcreteImplementationProofSummaryWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 3)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummaryWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidateCore(
            "Repair validation findings for a JavaScript browser app after QA rejected placeholder validation.",
            ProcessStepKind.Work,
            [],
            false,
            [],
            [],
            stepTitle: "Repair validation findings");
        var now = DateTimeOffset.UtcNow;
        const string changedPath = "external-target/C/programovani/candoitall-dev-output/orchard-shift-board-v9-js/public/app.js";
        var mutationResponseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Repair changed the browser application script but did not re-read it after the write.");
        var mutationDetail = CreateSuccessfulExecutionDetail(
            mutationResponseText,
            BuildSerializedSessionStateWithMessages(
                ("assistant", [CreateTextContent(mutationResponseText)])),
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_read_file",
                    changedPath,
                    ".",
                    "Succeeded: Read app script before editing.",
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    changedPath,
                    ".",
                    $"Succeeded: Overwrote '{changedPath}' with 702 characters.",
                    now.AddSeconds(1))
            ]);
        var readResponseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Recovery re-read the repaired browser application script after the prior mutation.");
        var readDetail = CreateSuccessfulExecutionDetail(
            readResponseText,
            BuildSerializedSessionStateWithMessages(
                ("assistant", [CreateTextContent(readResponseText)])),
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_read_file",
                    changedPath,
                    ".",
                    "Succeeded: Read repaired app script after the prior recovery mutation.",
                    now.AddSeconds(2))
            ]);
        var carriedAfterMutation = resolveCarriedProof.Invoke(
            null,
            [candidate, mutationDetail, CreateCarriedImplementationProof(false, false)]);
        Assert.NotNull(carriedAfterMutation);
        var carriedAfterRead = resolveCarriedProof.Invoke(
            null,
            [candidate, readDetail, carriedAfterMutation]);
        Assert.NotNull(carriedAfterRead);

        var missingProof = resolveMissingProof.Invoke(
            null,
            [candidate, readDetail, carriedAfterRead]) as string;

        Assert.Equal(string.Empty, missingProof);
    }

    [Fact]
    public void ResolveHistoricalCarriedImplementationProof_accepts_same_step_repair_mutation_with_fresh_rerun_read()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveHistoricalProof = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveHistoricalCarriedImplementationProof", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 2)
            ?? throw new InvalidOperationException("ResolveHistoricalCarriedImplementationProof method was not found.");
        var resolveCarriedProof = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveCarriedImplementationProof", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 3)
            ?? throw new InvalidOperationException("ResolveCarriedImplementationProof method was not found.");
        var resolveMissingProof = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveMissingConcreteImplementationProofSummaryWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 3)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummaryWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidateCore(
            "Repair validation findings for a JavaScript browser app after QA rejected placeholder validation.",
            ProcessStepKind.Work,
            [],
            false,
            [],
            [],
            stepTitle: "Repair validation findings");
        var now = DateTimeOffset.UtcNow;
        const string changedPath = "external-target/C/programovani/candoitall-dev-output/orchard-shift-board-v9-js/public/app.js";
        var mutationResponseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Earlier repair rerun changed the browser application script.");
        var mutationDetail = CreateSuccessfulExecutionDetail(
            mutationResponseText,
            BuildSerializedSessionStateWithMessages(
                ("assistant", [CreateTextContent(mutationResponseText)])),
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    changedPath,
                    ".",
                    $"Succeeded: Overwrote '{changedPath}' with 702 characters.",
                    now)
            ]);
        var readResponseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Manual rerun re-read the repaired browser application script.");
        var readDetail = CreateSuccessfulExecutionDetail(
            readResponseText,
            BuildSerializedSessionStateWithMessages(
                ("assistant", [CreateTextContent(readResponseText)])),
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_read_file",
                    changedPath,
                    ".",
                    "Succeeded: Read repaired app script after historical same-step repair mutation.",
                    now.AddSeconds(1))
            ]);

        var historicalProof = resolveHistoricalProof.Invoke(
            null,
            [candidate, new[] { mutationDetail }]);
        Assert.NotNull(historicalProof);
        var carriedAfterRead = resolveCarriedProof.Invoke(
            null,
            [candidate, readDetail, historicalProof]);
        Assert.NotNull(carriedAfterRead);

        var missingProof = resolveMissingProof.Invoke(
            null,
            [candidate, readDetail, carriedAfterRead]) as string;

        Assert.Equal(string.Empty, missingProof);
    }

    [Fact]
    public void ResolveUnresolvedCriticalToolFailures_ignores_inapplicable_dotnet_denial_for_javascript_step()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCriticalFailures = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveUnresolvedCriticalToolFailures", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 2)
            ?? throw new InvalidOperationException("ResolveUnresolvedCriticalToolFailures overload was not found.");

        var candidate = CreateDispatchCandidateCore(
            "Repair validation findings for a JavaScript browser app after QA rejected placeholder validation. This is not a .NET app.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Quality repair change set", true, "Must name repaired defects, changed JavaScript files, rerun validation, and unresolved risk.")],
            [],
            stepTitle: "Repair validation findings");
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "JavaScript repair evidence was recorded after source inspection.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionStateWithMessages(
                ("assistant", [CreateTextContent(responseText)])),
            [
                new ProcessAutomationToolExecutionReceipt(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_dotnet_run",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "dotnet_run_http_smoke",
                    ".",
                    "Denied: workspace_dotnet_run is not available for this JavaScript workspace profile.",
                    now,
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_read_file",
                    "external-target/C/programovani/candoitall-dev-output/orchard-shift-board-v9-js/public/app.js",
                    ".",
                    "Succeeded: Read repaired JavaScript file.",
                    now.AddSeconds(1))
            ]);

        var failures = Assert.IsAssignableFrom<IReadOnlyList<ProcessAutomationToolExecutionReceipt>>(
            resolveCriticalFailures.Invoke(null, [candidate, detail]));

        Assert.Empty(failures);
    }

    [Fact]
    public void ResolveCompletionStatusWithCarryForward_blocks_when_campus_booking_retry_mutates_without_fresh_proof()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveCompletionStatusWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 5)
            ?? throw new InvalidOperationException("ResolveCompletionStatusWithCarryForward method was not found.");
        var buildCompletionReason = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "BuildCompletionReasonWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 6)
            ?? throw new InvalidOperationException("BuildCompletionReasonWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidateWithStepTitle(
            "Implement feature, tests, and migration notes",
            "Implement a campus room booking web application, prove build, tests, and startup smoke, then record delivery notes.");
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Campus room booking implementation was updated.");
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Campus booking retry",
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
                "gpt-4o-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
            [])
        {
            ToolReceipts =
            [
                new ProcessAutomationToolExecutionReceipt(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_stat_path",
                    "WorkspaceStat",
                    "Required",
                    "Workspace-root-only file service.",
                    "artifacts/process-runs/campus-room-booking/current-step-notes.md",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ProcessAutomationToolExecutionReceipt(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_read_file",
                    "WorkspaceRead",
                    "Required",
                    "Workspace-root-only file service.",
                    "artifacts/process-runs/campus-room-booking/current-step-notes.md",
                    ".",
                    "Succeeded",
                    now,
                    now),
                new ProcessAutomationToolExecutionReceipt(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-file",
                    "workspace_write_file",
                    "WorkspaceWrite",
                    "Required",
                    "Workspace-root-only file service.",
                    "external-target/C/campus-room-booking/Program.cs",
                    ".",
                    "Succeeded",
                    now,
                    now)
            ]
        };
        var carriedProof = CreateCarriedImplementationProof(
            hasConcreteImplementationProof: true,
            hasRunnableApplicationProof: true);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(
            null,
            [candidate, detail, Array.Empty<string>(), responseText, carriedProof]);
        var reason = buildCompletionReason.Invoke(
            null,
            [
                candidate,
                detail,
                "Implement feature, tests, and migration notes",
                Array.Empty<string>(),
                responseText,
                carriedProof
            ]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.Contains("current-attempt implementation proof", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_uses_recorded_route_capacity_step_artifacts()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredArtifactSummary = serviceType.GetMethod(
            "ResolveMissingRequiredArtifactSummary",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Implement a route capacity planner web application and record the current step artifacts.",
            ProcessStepKind.Work,
            [],
            false,
            [
                (ProcessArtifactKind.Deliverable, "Implementation change set", true, "Must be linked to tests, migration notes, and touched-surface inventory."),
                (ProcessArtifactKind.Checklist, "Migration and rollout preparation checklist", true, "Must name data changes, operational preconditions, and rollback steps.")
            ],
            [],
            recordedArtifactTitles:
            [
                "Implementation change set",
                "Migration and rollout preparation checklist"
            ]);
        var detail = CreateSuccessfulExecutionDetail(string.Empty, null);

        var summary = resolveMissingRequiredArtifactSummary.Invoke(
            null,
            [candidate, detail, string.Empty]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_rejects_implementation_artifacts_written_before_latest_validation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredArtifactSummary = serviceType.GetMethod(
            "ResolveMissingRequiredArtifactSummary",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Implementation completed."),
            null,
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "artifacts/process-runs/run-001/03-implementation-change-set.md",
                    ".",
                    "Succeeded: Created implementation change set.",
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "artifacts/process-runs/run-001/03-migration-and-rollout-preparation-checklist.md",
                    ".",
                    "Succeeded: Created migration checklist.",
                    now.AddSeconds(1)),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "external-target/C/programovani/dotnet/output/Components/Pages/Home.razor",
                    ".",
                    "Succeeded: Overwrote Home.razor.",
                    now.AddSeconds(2)),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_build",
                    "external-target/C/programovani/dotnet/output/output.csproj",
                    "external-target/C/programovani/dotnet/output",
                    "Succeeded (exit 0)",
                    now.AddSeconds(3)),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_test",
                    "external-target/C/programovani/dotnet/output/output.csproj",
                    "external-target/C/programovani/dotnet/output",
                    "Succeeded (exit 0)",
                    now.AddSeconds(4))
            ]);

        var summary = resolveMissingRequiredArtifactSummary.Invoke(
            null,
            [candidate, detail, detail.Run.ResultSummary]) as string;

        Assert.Contains("Implementation change set", summary, StringComparison.Ordinal);
        Assert.Contains("Migration and rollout preparation checklist", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_accepts_implementation_artifacts_written_after_latest_validation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredArtifactSummary = serviceType.GetMethod(
            "ResolveMissingRequiredArtifactSummary",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Implementation completed."),
            null,
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "external-target/C/programovani/dotnet/output/Components/Pages/Home.razor",
                    ".",
                    "Succeeded: Overwrote Home.razor.",
                    now),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_build",
                    "external-target/C/programovani/dotnet/output/output.csproj",
                    "external-target/C/programovani/dotnet/output",
                    "Succeeded (exit 0)",
                    now.AddSeconds(1)),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_test",
                    "external-target/C/programovani/dotnet/output/output.csproj",
                    "external-target/C/programovani/dotnet/output",
                    "Succeeded (exit 0)",
                    now.AddSeconds(2)),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "artifacts/process-runs/run-001/03-implementation-change-set.md",
                    ".",
                    "Succeeded: Created implementation change set.",
                    now.AddSeconds(3)),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "artifacts/process-runs/run-001/03-migration-and-rollout-preparation-checklist.md",
                    ".",
                    "Succeeded: Created migration checklist.",
                    now.AddSeconds(4))
            ]);

        var summary = resolveMissingRequiredArtifactSummary.Invoke(
            null,
            [candidate, detail, detail.Run.ResultSummary]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_accepts_repair_change_set_written_after_external_deliverable_mutation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredArtifactSummary = serviceType.GetMethod(
            "ResolveMissingRequiredArtifactSummary",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "Repair validation findings",
            "Repair validation findings for a JavaScript browser app after QA rejected placeholder validation.",
            ProcessStepKind.Work,
            [(ProcessArtifactKind.Deliverable, "Quality repair change set", true, "Must name repaired defects, changed files or deliverables, rerun validation, and unresolved risk.")]);
        var rootAlias = "external-target/C/programovani/dotnet-demo/output";
        var now = DateTimeOffset.UtcNow;
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Repair completed."),
            null,
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    $"{rootAlias}/README-validation.md",
                    ".",
                    "Succeeded: Created validation README.",
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    $"{rootAlias}/MIGRATION.md",
                    ".",
                    "Succeeded: Created migration note.",
                    now.AddSeconds(1)),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_read_file",
                    $"{rootAlias}/index.html",
                    ".",
                    "Succeeded: Inspected entrypoint.",
                    now.AddSeconds(2)),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "artifacts/process-runs/run-001/06-quality-repair-change-set.md",
                    ".",
                    "Succeeded: Created quality repair change set.",
                    now.AddSeconds(3))
            ],
            serializedInvocationMetadataJson: BuildAllowedExternalTargetMetadata(rootAlias));

        var summary = resolveMissingRequiredArtifactSummary.Invoke(
            null,
            [candidate, detail, detail.Run.ResultSummary]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveWorkspaceWrittenArtifactSourceRelativePaths_keeps_unscoped_managed_write_as_read_source()
    {
        var workspaceScope = WorkspaceScopeDescriptor.Organization("dc8abe5458cd4a8798ab5a14de6f846b");
        var paths = ProcessRunAutomationDispatchService.ResolveWorkspaceWrittenArtifactSourceRelativePaths(
            workspaceScope,
            "artifacts/process-runs/run-001/03-implementation-change-set.md",
            "artifacts/scopes/organization/dc8abe5458cd4a8798ab5a14de6f846b/process-runs/run-001/03-implementation-change-set.md");

        Assert.Equal(
            [
                "artifacts/process-runs/run-001/03-implementation-change-set.md",
                "artifacts/scopes/organization/dc8abe5458cd4a8798ab5a14de6f846b/process-runs/run-001/03-implementation-change-set.md"
            ],
            paths);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
    public void ResolveCompletionStatusWithCarryForward_fails_project_asset_storage_receipt_when_internal_tool_receipt_is_not_projected()
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
            "Review and store screenshot",
            """
            Review the captured screenshot and store accepted screenshots through project_structure_asset_create with sourceWorkspacePath.
            Do not recapture browser proof in this review step.
            """,
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Screenshot review findings", true, "Must state accepted or rejected with the visual reason."),
            (ProcessArtifactKind.Evidence, "Project image asset storage receipt", true, "Must include project id, image asset node id, content type, original file name, and storage locator."));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Screenshot reviewed and stored as project image asset.",
            summaryMarkdown: """
## Project image asset storage receipt
Project id: 3569901c-dcc2-4f88-a08a-01801bfae9b9
Image asset node id: custom:c9ed5f770fbb4d57bee1f504f651e8a4
Content type: image/png
Original file name: 01-inventory-page.png
Storage locator: managed-files/project-media/images/3569901cdcc24f88a08a01801bfae9b9/01-inventory-page.png
Source workspace path (ingested): artifacts/process-runs/run-001/01-inventory-page.png

## Screenshot review findings
Result: Accepted
Reason: Screenshot shows the requested inventory route.
""");
        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Screenshot review",
                "process-step",
                "review-and-store-screenshot",
                "corr-screenshot-review",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-5-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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

        var priorSuccessfulTools = new[]
        {
            "workspace_read_file",
            "workspace_stat_path",
            "workspace_write_file"
        };
        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail, priorSuccessfulTools, responseText]);
        var reason = buildCompletionReason.Invoke(
            null,
            [candidate, detail, "Review and store screenshot", priorSuccessfulTools, responseText]) as string;

        Assert.True(status == ProcessStepRunStatus.Failed, reason);
        Assert.NotNull(reason);
        Assert.Contains("project_structure_asset_create", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatusWithCarryForward_fails_project_asset_storage_receipt_with_png_evidence_ref_without_asset_tool()
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
            "Review and store screenshot",
            """
            Review the captured screenshot and store accepted screenshots through project_structure_asset_create with sourceWorkspacePath.
            """,
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Screenshot review findings", true, "Must state accepted or rejected with the visual reason."),
            (ProcessArtifactKind.Evidence, "Project image asset storage receipt", true, "Must include project id, image asset node id, content type, original file name, and storage locator."));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Screenshot reviewed and stored as project image asset.",
            evidenceRefs:
            [
                "artifacts/scopes/organization/demo/process-runs/run-001/03-inventory-page.png",
                "artifacts/scopes/organization/demo/process-runs/run-001/04-project-image-asset-storage-receipt.md"
            ],
            summaryMarkdown: """
## Project image asset storage receipt
Project id: 3569901c-dcc2-4f88-a08a-01801bfae9b9
Image asset node id: custom:f26734b04646415cb8c1e32b130a08b1
Content type: image/png
Original file name: 03-inventory-page.png
Storage locator: managed-files/project-media/images/3569901cdcc24f88a08a01801bfae9b9/03-inventory-page.png

## Screenshot review findings
Decision: Accepted
Reason: Screenshot shows the requested inventory route and is readable enough to store as the project image asset.
File: artifacts/process-runs/run-001/03-inventory-page.png
""");
        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Screenshot review",
                "process-step",
                "review-and-store-screenshot",
                "corr-screenshot-review",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-5-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var priorSuccessfulTools = new[]
        {
            "workspace_read_file",
            "workspace_stat_path",
            "workspace_write_file"
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail, priorSuccessfulTools, responseText]);
        var reason = buildCompletionReason.Invoke(
            null,
            [candidate, detail, "Review and store screenshot", priorSuccessfulTools, responseText]) as string;

        Assert.True(status == ProcessStepRunStatus.Failed, reason);
        Assert.NotNull(reason);
        Assert.Contains("project_structure_asset_create", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatusWithCarryForward_fails_project_structure_writeback_summary_without_node_tool()
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
            "Record delivery results and evidence index",
            """
            Write a compact run evidence index and final verdict back into project structure through APIs/tools.
            Must call project_structure_node_create before completing.
            """,
            ProcessStepKind.Review);
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Run evidence index and project-structure writeback summary were prepared.",
            summaryMarkdown: """
## Project-structure result writeback summary
Project id: 3569901c-dcc2-4f88-a08a-01801bfae9b9
Target node id: custom:feature-node
Created project-structure node id: custom:claimed-writeback-node

## Run evidence index
Build/test: passed
Process artifacts: artifacts/process-runs/run-001/07-run-evidence-index.md
""");
        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Record Blazor results",
                "process-step",
                "record-blazor-results",
                "corr-record-results",
                "step-transition:Completed",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-5.4-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var priorSuccessfulTools = new[]
        {
            "project_structure_read",
            "workspace_read_file",
            "workspace_write_file"
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail, priorSuccessfulTools, responseText]);
        var reason = buildCompletionReason.Invoke(
            null,
            [candidate, detail, "Record delivery results and evidence index", priorSuccessfulTools, responseText]) as string;

        Assert.True(status == ProcessStepRunStatus.Failed, reason);
        Assert.NotNull(reason);
        Assert.Contains("project_structure_node_create", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatusWithCarryForward_fails_blocked_required_tool_claim_without_failed_receipt()
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
            "Record delivery results and evidence index",
            """
            Write the final verdict back into project structure through tools.
            Must call project_structure_node_create before completing.
            """,
            ProcessStepKind.Review);
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "project_structure_node_create failed or was unavailable.",
            summaryMarkdown: """
## Project-structure result writeback summary
No node was created because project_structure_node_create failed or was unavailable.
""");
        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Record Blazor results",
                "process-step",
                "record-blazor-results",
                "corr-record-results",
                "step-transition:Blocked",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-5.4-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var priorSuccessfulTools = new[]
        {
            "project_structure_read",
            "workspace_read_file",
            "workspace_write_file"
        };

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail, priorSuccessfulTools, responseText]);
        var reason = buildCompletionReason.Invoke(
            null,
            [candidate, detail, "Record delivery results and evidence index", priorSuccessfulTools, responseText]) as string;

        Assert.True(status == ProcessStepRunStatus.Failed, reason);
        Assert.NotNull(reason);
        Assert.Contains("no failed receipt", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project_structure_node_create", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveMissingUpstreamArtifactInspectionSummary_treats_successful_read_as_text_artifact_inspection()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingInspection = serviceType.GetMethod(
            "ResolveMissingUpstreamArtifactInspectionSummary",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingUpstreamArtifactInspectionSummary method was not found.");
        const string browserEvidencePath = "artifacts/scopes/organization/demo/process-runs/run-001/03-browser-navigation-and-console-evidence.md";
        var candidate = CreateDispatchCandidateCore(
            """
            Review the captured screenshot and browser evidence, then store the accepted screenshot as a project image asset.
            """,
            ProcessStepKind.Review,
            [],
            false,
            [
                (ProcessArtifactKind.Evidence, "Screenshot review findings", true, "Must state accepted or rejected with the visual reason."),
                (ProcessArtifactKind.Evidence, "Project image asset storage receipt", true, "Must include project id, image asset node id, content type, original file name, and storage locator.")
            ],
            [
                (
                    "Capture page screenshot",
                    "Browser navigation and console evidence",
                    [
                        (
                            "Browser navigation and console evidence",
                            "Evidence",
                            browserEvidencePath,
                            "Browser proof for /inventory.",
                            "Projected from capture step."
                        )
                    ]
                )
            ],
            stepTitle: "Review and store screenshot");
        var now = DateTimeOffset.UtcNow;
        var detail = CreateSuccessfulExecutionDetail(
            responseText: "Reviewed browser evidence.",
            serializedSessionStateJson: BuildSerializedSessionState(
                ("workspace_read_file", CreateProviderNativeTextResult("Read browser evidence."))),
            toolReceipts:
            [
                CreateToolReceipt("workspace-file", "workspace_read_file", browserEvidencePath, ".", "Succeeded", now)
            ]);

        var summary = resolveMissingInspection.Invoke(null, [candidate, detail]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveCompletionStatusWithCarryForward_allows_pathless_governance_snapshot_artifact_sections()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(method => string.Equals(method.Name, "ResolveCompletionStatusWithCarryForward", StringComparison.Ordinal) &&
                                       method.GetParameters().Length == 4)
            ?? throw new InvalidOperationException("ResolveCompletionStatusWithCarryForward method was not found.");

        var candidate = CreateDispatchCandidateWithStepTitle(
            "Define delegation boundary",
            "Create the delegation configuration and prompt package.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Delegation configuration snapshot", true, string.Empty),
            (ProcessArtifactKind.Prompt, "Delegation contract and prompt package", true, string.Empty));
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Delegation configuration snapshot and prompt package completed.",
            summaryMarkdown: """
## Delegation configuration snapshot

Allowed touches: index.html, styles.css, app.js.
Forbidden actions: package installs, backend services, persistence, and files outside the product root.
Escalation conditions: missing writable product root, unexpected package manager requirements, or requested work outside the explicit file list.

## Delegation contract and prompt package

Use only the project-structure mindmap requirements as scope. Create the requested static files only, and return Blocked if a required architecture or tool boundary is missing.
""");
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Delegation boundary",
                "process-step",
                "step-delegation",
                "corr-delegation",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-5-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                "{}",
                []),
            null,
            [],
            []);

        var priorSuccessfulTools = new[] { "project_structure_read", "workspace_stat_path", "workspace_read_file" };
        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail, priorSuccessfulTools, responseText]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
    public void ResolveProjectedArtifactTrustStatus_approves_completed_human_decision_artifact()
    {
        var expectedArtifact = new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
            Guid.NewGuid(),
            ProcessArtifactKind.Decision,
            "Repair escalation record",
            true,
            ProcessArtifactTrustRequirement.HumanApproved,
            ProcessSensitivityLevel.Internal,
            "Must name unresolved findings, no-go rationale, accountable owner, and required next repair scope.",
            string.Empty);

        var trustStatus = ProcessRunAutomationDispatchService.ResolveProjectedArtifactTrustStatus(
            expectedArtifact,
            ProcessStepRunStatus.Completed);

        Assert.Equal(ProcessArtifactTrustStatus.Approved, trustStatus);
    }

    [Fact]
    public void ResolveProjectedArtifactTrustStatus_keeps_non_completed_or_non_decision_artifacts_review_required()
    {
        var decisionArtifact = new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
            Guid.NewGuid(),
            ProcessArtifactKind.Decision,
            "Repair escalation record",
            true,
            ProcessArtifactTrustRequirement.HumanApproved,
            ProcessSensitivityLevel.Internal,
            "Must name unresolved findings, no-go rationale, accountable owner, and required next repair scope.",
            string.Empty);
        var evidenceArtifact = decisionArtifact with
        {
            Id = Guid.NewGuid(),
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Regression evidence pack"
        };

        Assert.Equal(
            ProcessArtifactTrustStatus.ReviewRequired,
            ProcessRunAutomationDispatchService.ResolveProjectedArtifactTrustStatus(
                decisionArtifact,
                ProcessStepRunStatus.Blocked));
        Assert.Equal(
            ProcessArtifactTrustStatus.ReviewRequired,
            ProcessRunAutomationDispatchService.ResolveProjectedArtifactTrustStatus(
                evidenceArtifact,
                ProcessStepRunStatus.Completed));
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_completed_run_that_only_missed_required_tools()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate("Implement the requested application and prove the build passes.");

        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_run",
                    "Units.slnx",
                    "deliveries/workflow-suite/src",
                    "Succeeded",
                    now)
            ]
        };

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, detail.Run.ResultSummary, new[] { "workspace_write_file", "workspace_dotnet_build" }, CreateCarriedImplementationProof(false, false), 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_compresses_repeated_no_progress_missing_tool_attempt()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate("Implement the requested application and prove the build passes.");

        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                "I could not write files or run the build.",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
            [candidate, detail, detail.Run.ResultSummary, new[] { "workspace_write_file", "workspace_dotnet_build" }, CreateCarriedImplementationProof(false, false), 2, 5]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.False((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_compresses_repeated_wrong_root_write_without_satisfied_artifact()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate(
            "Implement the requested application and prove the build passes.",
            ProcessStepKind.Work,
            (ProcessArtifactKind.Deliverable, "Implementation change set", true, "Must identify concrete product source files changed under the current product root."));

        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                BuildAllowedExternalTargetMetadata("external-target/C/programovani/dotnet/CurrentApp/product"),
                "Prompt",
                "I wrote the implementation note to a sibling product root but did not satisfy the current artifact contract.",
                "OpenAI chat completions",
                "gpt-4o-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_write_file", CreateProviderNativeTextResult("Wrote external-target/C/programovani/dotnet/OldApp/product/src/Program.cs"))),
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
                    "external-target/C/programovani/dotnet/OldApp/product/src/Program.cs",
                    ".",
                    "Succeeded",
                    now)
            ]
        };

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, detail.Run.ResultSummary, Array.Empty<string>(), CreateCarriedImplementationProof(false, false), 2, 5]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.False((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_true_for_completed_run_with_unresolved_critical_tool_failure()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate("Implement the requested application and prove the build passes.");

        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
            [candidate, detail, detail.Run.ResultSummary, Array.Empty<string>(), CreateCarriedImplementationProof(false, false), 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_false_for_explicit_repair_disposition_with_failed_diagnostic_tool()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for the generated Blazor app, then select quality-accepted or repair-required.",
            ProcessStepKind.Review,
            [
                ("quality-accepted", "Quality accepted", "Continue to result writeback."),
                ("repair-required", "Repair required", "Route back to implementation repair.")
            ],
            true,
            [
                (ProcessArtifactKind.Evidence, "Blazor runtime evidence pack", true, "Must include screenshots, browser console, and visible behavior assertions."),
                (ProcessArtifactKind.Transcript, "Validation self-review summary", true, "Must state accepted or repair-required disposition.")
            ],
            [],
            stepTitle: "Validate Blazor runtime and browser evidence",
            outputContractSummary: "Browser validation disposition");
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Runtime validation found a repairable form binding failure.",
            branchOutcomeKey: "repair-required",
            summaryMarkdown: """
            ## Blazor runtime evidence pack
            Browser URL: http://127.0.0.1:5305/
            Screenshot: artifacts/process-runs/run-1/browser/mobile.png
            Console result: EditForm requires either a Model parameter, or an EditContext parameter.
            Visible behavior assertion: the page renders the error UI instead of the pantry planner.

            ## Validation self-review summary
            Acceptance decision:
            - Status: repair-required
            - Reason: Browser runtime proof found a repairable EditForm binding defect.
            """);
        var now = DateTimeOffset.UtcNow;
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                ("browser_take_screenshot", new Dictionary<string, object?> { ["filename"] = ".playwright-mcp/page-repair-proof.png" }, CreateProviderNativeTextResult("Screenshot captured.")),
                ("browser_snapshot", new Dictionary<string, object?> { ["filename"] = ".playwright-mcp/page-repair-proof.yml" }, CreateProviderNativeTextResult("Snapshot captured.")),
                ("browser_console_messages", new Dictionary<string, object?> { ["filename"] = ".playwright-mcp/console-repair-proof.log" }, CreateProviderNativeTextResult("Error: EditForm requires either a Model parameter, or an EditContext parameter.")),
                ("workspace_stat_path", (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), CreateProviderNativeTextResult("Evidence artifact path exists.")),
                ("workspace_read_file", (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), CreateProviderNativeTextResult("Evidence artifact reviewed.")),
                ("workspace_write_file", (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), CreateProviderNativeTextResult("Evidence artifacts written."))),
            [
                CreateToolReceipt("browser", "browser_take_screenshot", "http://127.0.0.1:5305/", ".", "Succeeded", now),
                CreateToolReceipt("browser", "browser_snapshot", "http://127.0.0.1:5305/", ".", "Succeeded", now.AddSeconds(1)),
                CreateToolReceipt("browser", "browser_console_messages", "http://127.0.0.1:5305/", ".", "Succeeded", now.AddSeconds(2)),
                CreateToolReceipt("workspace-file", "workspace_write_file", "artifacts/process-runs/run-1/03-blazor-runtime-evidence-pack.md", ".", "Succeeded", now.AddSeconds(3)),
                CreateToolReceipt("workspace-file", "workspace_write_file", "artifacts/process-runs/run-1/03-validation-self-review-summary.md", ".", "Succeeded", now.AddSeconds(4)),
                CreateToolReceipt("workspace-file", "workspace_stat_path", "artifacts/process-runs/run-1/03-blazor-runtime-evidence-pack.md", ".", "Succeeded", now.AddSeconds(5)),
                CreateToolReceipt("workspace-file", "workspace_read_file", "artifacts/process-runs/run-1/03-validation-self-review-summary.md", ".", "Succeeded", now.AddSeconds(6)),
                CreateToolReceipt("workspace-process", "workspace_pwsh_run_script", "read locked browser host stderr log", ".", "Failed: The process cannot access the file because it is being used by another process.", now.AddSeconds(7))
            ]);

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, Array.Empty<string>(), CreateCarriedImplementationProof(false, false), 1, 3]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.False((bool)shouldRetryResult);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
            [candidate, detail, responseText, Array.Empty<string>(), CreateCarriedImplementationProof(false, false), 1, 3]);

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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
            [candidate, detail, responseText, Array.Empty<string>(), CreateCarriedImplementationProof(false, false), 1, 3]);

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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                CreateCarriedImplementationProof(false, false),
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
        var chatSession = new ProcessAutomationChatSession(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Architecture review",
            now,
            now,
            [
                new ProcessAutomationChatMessage(
                    Guid.NewGuid(),
                    ProcessAutomationChatMessageRole.Assistant,
                    recoveredAssistantMessage,
                    now,
                    recoveredAssistantMessage.Length)
            ],
            LatestExecutionRunId: null);
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
    public void ResolvePreferredExecutionResponseText_prefers_structured_result_summary_over_unstructured_primary_text()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolvePreferredResponseText = serviceType.GetMethod("ResolvePreferredExecutionResponseText", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolvePreferredExecutionResponseText method was not found.");
        var candidate = CreateDispatchCandidate(
            "Review the screenshot and store accepted image assets.",
            ProcessStepKind.Review);

        var now = DateTimeOffset.UtcNow;
        var structuredResultSummary = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Image asset storage failed.",
            summaryMarkdown: "## Project image asset storage receipt\r\nNo image assets stored.");
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Screenshot review run",
                "process-step",
                "step-4",
                "corr-4",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                structuredResultSummary,
                "OpenAI chat completions",
                "gpt-5-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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

        var unstructuredPrimaryText = "## Project image asset storage receipt\r\nNo image assets stored because the screenshot was not readable.";
        var resolvedResponseText = resolvePreferredResponseText.Invoke(
            null,
            [candidate, unstructuredPrimaryText, detail]) as string;

        Assert.NotNull(resolvedResponseText);
        Assert.Equal(structuredResultSummary, resolvedResponseText);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_returns_false_after_final_attempt()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate("Implement the requested application and prove the build passes.");

        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
            [candidate, detail, detail.Run.ResultSummary, new[] { "workspace_write_file", "workspace_dotnet_build" }, CreateCarriedImplementationProof(false, false), 3, 3]);

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
    public void ResolveRequiredToolNames_adds_run_for_runnable_dotnet_implementation_steps()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveRequiredToolNames = serviceType.GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");

        var candidate = CreateDispatchCandidate(
            "Implement the requested Blazor app with tests and migration notes.");

        var requiredToolNames = resolveRequiredToolNames.Invoke(null, [candidate]) as IReadOnlyList<string>;

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_dotnet_build", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_dotnet_test", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_dotnet_run", requiredToolNames, StringComparer.Ordinal);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded),
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
        var detail = new ProcessAutomationExecutionRunDetail(
            CreateExecutionRun("process-automation-dispatch", ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded),
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "external-target/C/workflow-suite/src/Workflow/Program.cs",
                    ".",
                    "Succeeded",
                    now),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_run",
                    "SimpleWorkflowApp.csproj",
                    "showcases/blazor-ssr-workflow/app/SimpleWorkflowApp",
                    "Succeeded",
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
    public void ResolveCompletionStatus_blocks_completed_qa_when_test_command_reports_zero_tests()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "QA validation",
            "Run QA validation with package test results and release readiness evidence.",
            ProcessStepKind.Review);
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "QA validation accepted the automated test command.",
                summaryMarkdown: "The package test command exited successfully."),
            BuildSerializedSessionState((
                "workspace_pwsh_run_script",
                new
                {
                    succeeded = true,
                    stdout = "# tests 0\n# pass 0\n# fail 0",
                    stderr = string.Empty,
                    exitCode = 0
                })));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "QA validation"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("zero executed tests", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_completed_qa_when_dotnet_test_reports_czech_no_tests()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "QA validation",
            "Run QA validation with dotnet test results and release readiness evidence.",
            ProcessStepKind.Review);
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "QA validation accepted the test command.",
                summaryMarkdown: "The dotnet test command exited successfully."),
            BuildSerializedSessionState((
                "workspace_dotnet_build",
                new
                {
                    succeeded = true,
                    stdout = "Build succeeded.\n    0 Warning(s)\n    0 Error(s)",
                    stderr = string.Empty,
                    exitCode = 0
                }),
                (
                "workspace_dotnet_test",
                new
                {
                    succeeded = true,
                    stdout = "V C:\\work\\SignalTally.Tests\\bin\\Debug\\net10.0\\SignalTally.Tests.dll nejsou dostupn\u00e9 \u017e\u00e1dn\u00e9 testy.",
                    stderr = string.Empty,
                    exitCode = 0
                })));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "QA validation"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("zero executed tests", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_completed_qa_when_build_receipt_contains_warning()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "QA validation",
            "Run QA validation with dotnet build and release readiness evidence.",
            ProcessStepKind.Review);
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "QA validation accepted the build.",
                summaryMarkdown: "Build command exited successfully."),
            BuildSerializedSessionState((
                "workspace_dotnet_build",
                new
                {
                    succeeded = true,
                    stdout = "Build succeeded.\nProgram.cs(5,25): warning CS7022: The entry point of the program is global code; Program.Main() is ignored.\n    1 Warning(s)\n    0 Error(s)",
                    stderr = string.Empty,
                    exitCode = 0
                })));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "QA validation"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("warning-free", reason, StringComparison.OrdinalIgnoreCase);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
    public void ResolveCompletionStatus_blocks_completed_browser_proof_step_when_response_reports_runtime_error()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");

        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the requested app.",
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                    "QA smoke executed; host started and Playwright browser proof captured. Root route shows an application error.",
                    summaryMarkdown: "Browser snapshot and screenshot captured the primary route, but the route shows an application error and needs implementation repair."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        Assert.Contains("application runtime error", reason, StringComparison.OrdinalIgnoreCase);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Failed,
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
        var artifact = new ProcessAutomationExecutionArtifact(
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
    public void ResolveCompletionStatus_allows_completion_when_required_browser_tools_are_confirmed_by_execution_log()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");

        var candidate = CreateDispatchCandidate(
            "Validate the UI.\nInstructions: Call browser_take_screenshot, browser_snapshot, and browser_console_messages before you conclude.",
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                runId,
                agentId,
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
            [
                CreateExecutionLogToolInvocation(runId, agentId, now, "browser_take_screenshot"),
                CreateExecutionLogToolInvocation(runId, agentId, now, "browser_snapshot"),
                CreateExecutionLogToolInvocation(runId, agentId, now, "browser_console_messages")
            ],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
    }

    [Fact]
    public void ResolveSuccessfulBrowserToolOutputFiles_reads_provider_native_filenames_from_execution_log()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveBrowserOutputs = serviceType.GetMethod("ResolveSuccessfulBrowserToolOutputFiles", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveSuccessfulBrowserToolOutputFiles method was not found.");
        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var snapshotPath = $"artifacts/process-runs/{runId:D}/browser-snapshot.yml";
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                runId,
                agentId,
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(Array.Empty<(string ToolName, object Result)>()),
                []),
            null,
            [
                CreateExecutionLogToolInvocationWithFilename(runId, agentId, now, "browser_snapshot", snapshotPath)
            ],
            []);

        var outputs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            resolveBrowserOutputs.Invoke(null, [detail]));

        Assert.True(outputs.TryGetValue("browser_snapshot", out var snapshotFiles));
        Assert.Contains(snapshotPath, snapshotFiles);
    }

    [Fact]
    public void ResolveSuccessfulBrowserToolOutputFiles_reads_playwright_mcp_outputs_from_structured_evidence_refs()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveBrowserOutputs = serviceType.GetMethod("ResolveSuccessfulBrowserToolOutputFiles", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveSuccessfulBrowserToolOutputFiles method was not found.");
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Required browser evidence was captured.",
            evidenceRefs:
            [
                ".playwright-mcp\\page-2026-05-22T14-59-45-865Z.png",
                ".playwright-mcp\\page-2026-05-22T14-58-45-608Z.yml",
                ".playwright-mcp\\console-2026-05-22T14-58-45-447Z.log",
                ".playwright-mcp\\page-dom.json"
            ]);
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(Array.Empty<(string ToolName, object Result)>()));

        var outputs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            resolveBrowserOutputs.Invoke(null, [detail]));

        Assert.Contains(".playwright-mcp/page-2026-05-22T14-59-45-865Z.png", outputs["browser_take_screenshot"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains(".playwright-mcp/page-2026-05-22T14-58-45-608Z.yml", outputs["browser_snapshot"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains(".playwright-mcp/console-2026-05-22T14-58-45-447Z.log", outputs["browser_console_messages"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains(".playwright-mcp/page-dom.json", outputs["browser_evaluate"], StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(".playwright-mcp/page-2026-05-22T14-59-45-865Z.png", true)]
    [InlineData(".playwright-mcp/page-2026-05-22T14-58-45-608Z.yml", true)]
    [InlineData(".playwright-mcp/console-2026-05-22T14-58-45-447Z.log", true)]
    [InlineData(".playwright-mcp/page-dom.json", true)]
    [InlineData("artifacts/process-runs/run-001/browser-proof.png", true)]
    [InlineData("output/process-runs/run-001/browser-proof.png", false)]
    public void IsProviderNativeBrowserArtifactPath_accepts_managed_and_playwright_browser_outputs(
        string relativePath,
        bool expected)
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var isProviderNativeBrowserArtifactPath = serviceType.GetMethod("IsProviderNativeBrowserArtifactPath", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("IsProviderNativeBrowserArtifactPath method was not found.");

        var result = (bool)(isProviderNativeBrowserArtifactPath.Invoke(null, [relativePath]) ?? !expected);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsWrongRootArtifact_allows_only_current_run_managed_output_paths() {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var isWrongRootArtifact = serviceType.GetMethod("IsWrongRootArtifact", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("IsWrongRootArtifact method was not found.");
        var processRunId = Guid.NewGuid();
        var processMockRunKey = processRunId.ToString("N")[..16];

        Assert.False(Invoke($"output/process-runs/{processRunId:D}/SampleApp/ValidationEngine.cs"));
        Assert.False(Invoke($"output/scopes/organization/demo/process-runs/{processRunId:N}/SampleApp/ValidationEngine.cs"));
        Assert.False(Invoke($"output/scopes/organization/demo/process-mock/{processMockRunKey}/MockApp/ValidationEngine.cs"));
        Assert.True(Invoke($"output/process-runs/{Guid.NewGuid():D}/SampleApp/ValidationEngine.cs"));
        Assert.True(Invoke("output/shared/ValidationEngine.cs"));
        Assert.True(Invoke("src/SampleApp/ValidationEngine.cs"));

        bool Invoke(string managedStoragePath) {
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = processRunId,
                ManagedStoragePath = managedStoragePath
            };

            return (bool)(isWrongRootArtifact.Invoke(null, [artifact]) ?? false);
        }
    }

    [Fact]
    public void ResolveProviderNativeBrowserProjectedRelativePath_places_playwright_mcp_outputs_under_current_run_browser_artifacts()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveProjectedPath = serviceType.GetMethod("ResolveProviderNativeBrowserProjectedRelativePath", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveProviderNativeBrowserProjectedRelativePath method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must include browser proof and screenshots for UI surfaces."));
        var run = candidate.GetType().GetProperty("Run")?.GetValue(candidate) as ProcessRun
            ?? throw new InvalidOperationException("DispatchCandidate.Run was not available.");
        var workspaceScope = WorkspaceScopeDescriptor.Organization("demo");

        var projectedPath = resolveProjectedPath.Invoke(
            null,
            [candidate, workspaceScope, ".playwright-mcp/page-2026-05-22T14-59-45-865Z.png"]) as string;

        Assert.Equal(
            $"artifacts/scopes/organization/demo/process-runs/{run.Id:D}/browser/page-2026-05-22T14-59-45-865Z.png",
            projectedPath);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_completed_qa_when_required_browser_screenshot_artifact_is_missing()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for the delivered browser app.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must include browser proof, screenshots for UI surfaces, console messages, and unresolved risks.")],
            [],
            recordedArtifactTitles: ["Regression evidence pack"]);
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Quality accepted with browser evidence.",
                evidenceRefs:
                [
                    ".playwright-mcp/page-2026-05-22T15-08-00-000Z.yml",
                    ".playwright-mcp/console-2026-05-22T15-08-01-000Z.log"
                ]),
            BuildSerializedSessionState(Array.Empty<(string ToolName, object Result)>()));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Run QA validation and browser proof"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("screenshot evidence", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("durable browser artifact", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_completed_qa_when_browser_console_contains_active_javascript_error()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for the delivered browser app.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must include browser proof, screenshots for UI surfaces, console messages, and unresolved risks.")],
            [],
            recordedArtifactTitles: ["Regression evidence pack"]);
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Quality accepted with browser evidence.",
                evidenceRefs:
                [
                    ".playwright-mcp/page-2026-05-22T15-09-00-000Z.png",
                    ".playwright-mcp/page-2026-05-22T15-09-01-000Z.yml",
                    ".playwright-mcp/console-2026-05-22T15-09-02-000Z.log"
                ]),
            BuildSerializedSessionState(
                ("browser_console_messages", CreateProviderNativeTextResult("TypeError: Cannot read properties of undefined (reading 'spawnPiece')"))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Run QA validation and browser proof"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("browser console", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runtime errors", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveInvalidBrowserProofSummary_allows_classified_post_stop_disconnect_after_browser_evidence()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveInvalidBrowserProofSummary = serviceType.GetMethod("ResolveInvalidBrowserProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveInvalidBrowserProofSummary method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for the delivered browser app.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must include browser proof, screenshots for UI surfaces, console messages, and unresolved risks.")],
            [],
            recordedArtifactTitles: ["Regression evidence pack"]);
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Quality accepted with browser evidence.",
                evidenceRefs:
                [
                    ".playwright-mcp/page-2026-05-22T15-10-00-000Z.png",
                    ".playwright-mcp/page-2026-05-22T15-10-01-000Z.yml",
                    ".playwright-mcp/console-2026-05-22T15-10-02-000Z.log"
                ]),
            BuildSerializedSessionState(
                ("browser_console_messages", CreateProviderNativeTextResult("Post-stop cleanup: host stopped. WebSocket connection to ws://127.0.0.1 failed with ERR_CONNECTION_REFUSED after host stopped."))));

        var summary = resolveInvalidBrowserProofSummary.Invoke(null, [candidate, detail]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveCompletionStatus_blocks_interactive_browser_proof_without_representative_interaction_tool()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for an interactive canvas game. The step contract requires representative interaction before acceptance.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must include browser proof, screenshots for UI surfaces, console messages, and unresolved risks.")],
            [],
            recordedArtifactTitles: ["Regression evidence pack"]);
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Quality accepted after browser proof.",
                evidenceRefs:
                [
                    ".playwright-mcp/page-2026-05-22T15-11-00-000Z.png",
                    ".playwright-mcp/page-2026-05-22T15-11-01-000Z.yml",
                    ".playwright-mcp/console-2026-05-22T15-11-02-000Z.log"
                ]),
            BuildSerializedSessionState(
                ("browser_console_messages", CreateProviderNativeTextResult("No active browser console errors."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Run QA validation and browser proof"]) as string;

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("representative interaction", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_allows_interactive_browser_proof_with_evaluate_state_artifact()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for an interactive canvas game. The step contract requires representative interaction before acceptance.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must include browser proof, screenshots for UI surfaces, console messages, and unresolved risks.")],
            [],
            recordedArtifactTitles: ["Regression evidence pack"]);
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Quality accepted after browser proof and representative state interaction.",
                evidenceRefs:
                [
                    ".playwright-mcp/page-2026-05-22T15-12-00-000Z.png",
                    ".playwright-mcp/page-2026-05-22T15-12-01-000Z.yml",
                    ".playwright-mcp/console-2026-05-22T15-12-02-000Z.log",
                    ".playwright-mcp/state-2026-05-22T15-12-03-000Z.json"
                ]),
            BuildSerializedSessionState(
                ("workspace_stat_path", (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), CreateProviderNativeTextResult("Artifact path verified.")),
                ("workspace_read_file", (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), CreateProviderNativeTextResult("Artifact contents reviewed.")),
                ("browser_take_screenshot", new Dictionary<string, object?> { ["filename"] = ".playwright-mcp/page-2026-05-22T15-12-00-000Z.png" }, CreateProviderNativeTextResult("Screenshot saved.")),
                ("browser_snapshot", new Dictionary<string, object?> { ["filename"] = ".playwright-mcp/page-2026-05-22T15-12-01-000Z.yml" }, CreateProviderNativeTextResult("Snapshot saved.")),
                ("browser_console_messages", new Dictionary<string, object?> { ["filename"] = ".playwright-mcp/console-2026-05-22T15-12-02-000Z.log" }, CreateProviderNativeTextResult("No active browser console errors.")),
                ("browser_evaluate", new Dictionary<string, object?> { ["filename"] = ".playwright-mcp/state-2026-05-22T15-12-03-000Z.json" }, CreateProviderNativeTextResult("Representative key event dispatched and visible state changed."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Run QA validation and browser proof"]) as string;

        Assert.True(status == ProcessStepRunStatus.Completed, reason);
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_rejects_declared_browser_artifact_without_matching_output()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredArtifactSummary = serviceType.GetMethod("ResolveMissingRequiredArtifactSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the delivered browser app.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Browser screenshot artifact", true, "Create this artifact at artifacts/process-runs/run-001/browser/browser-proof.png using browser_take_screenshot."));
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Quality accepted without output evidence."),
            BuildSerializedSessionState(Array.Empty<(string ToolName, object Result)>()));

        var summary = resolveMissingRequiredArtifactSummary.Invoke(null, [candidate, detail, detail.Run.ResultSummary]) as string;

        Assert.Contains("Browser screenshot artifact", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_accepts_declared_browser_artifact_with_matching_output()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredArtifactSummary = serviceType.GetMethod("ResolveMissingRequiredArtifactSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the delivered browser app.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Browser screenshot artifact", true, "Create this artifact at artifacts/process-runs/run-001/browser/browser-proof.png using browser_take_screenshot."));
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Quality accepted with screenshot output evidence.",
                evidenceRefs: ["artifacts/process-runs/run-001/browser/browser-proof.png"]),
            BuildSerializedSessionState(Array.Empty<(string ToolName, object Result)>()));

        var summary = resolveMissingRequiredArtifactSummary.Invoke(null, [candidate, detail, detail.Run.ResultSummary]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_rejects_dotnet_stdout_evidence_ref_as_browser_console_output()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredArtifactSummary = serviceType.GetMethod("ResolveMissingRequiredArtifactSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the delivered browser app.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Browser console artifact", true, "Create this artifact at artifacts/process-runs/run-001/browser/browser-console.log using browser_console_messages."));
        var dotnetStdoutPath = "artifacts/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/20260522/20260522-193340659-dotnet-build/stdout.txt";
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Build stdout was captured, but no browser console output was produced.",
                evidenceRefs: [dotnetStdoutPath]),
            BuildSerializedSessionState(Array.Empty<(string ToolName, object Result)>()));

        var summary = resolveMissingRequiredArtifactSummary.Invoke(null, [candidate, detail, detail.Run.ResultSummary]) as string;

        Assert.Contains("Browser console artifact", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveCompletionStatus_ignores_provider_native_browser_file_read_scope_miss_when_file_exists()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var resolveFailures = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => string.Equals(method.Name, "ResolveUnresolvedCriticalToolFailures", StringComparison.Ordinal) &&
                              method.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("ResolveUnresolvedCriticalToolFailures method was not found.");

        var tempWorkspace = Path.Combine(Path.GetTempPath(), $"candoitall-browser-proof-{Guid.NewGuid():N}");
        try
        {
            var candidate = CreateDispatchCandidate(
                "Validate the UI.\nInstructions: Call browser_take_screenshot, browser_snapshot, and browser_console_messages before you conclude.",
                ProcessStepKind.Review);
            var now = DateTimeOffset.UtcNow;
            var runId = Guid.NewGuid();
            var agentId = Guid.NewGuid();
            var snapshotPath = $"artifacts/process-runs/{runId:D}/browser-snapshot.yml";
            var screenshotPath = $"artifacts/process-runs/{runId:D}/browser-desktop.png";
            var consolePath = $"artifacts/process-runs/{runId:D}/browser-console-error.log";
            var snapshotFullPath = Path.Combine(tempWorkspace, snapshotPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotFullPath)!);
            File.WriteAllText(snapshotFullPath, "- text: Unit converter\n- text: Result 12.5 cm\n");
            var screenshotFullPath = Path.Combine(tempWorkspace, screenshotPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(screenshotFullPath)!);
            File.WriteAllBytes(screenshotFullPath, [137, 80, 78, 71]);
            var consoleFullPath = Path.Combine(tempWorkspace, consolePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(consoleFullPath)!);
            File.WriteAllText(consoleFullPath, "No active browser console errors.");
            var detail = new ProcessAutomationExecutionRunDetail(
                new ProcessAutomationExecutionRunRecord(
                    runId,
                    agentId,
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
                    ProcessAutomationExecutionState.Completed,
                    ProcessAutomationRunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(Array.Empty<(string ToolName, object Result)>()),
                    []),
                null,
                [
                    CreateExecutionLogToolInvocationWithFilename(runId, agentId, now, "browser_take_screenshot", screenshotPath),
                    CreateExecutionLogToolInvocationWithFilename(runId, agentId, now.AddSeconds(1), "browser_snapshot", snapshotPath),
                    CreateExecutionLogToolInvocationWithFilename(runId, agentId, now.AddSeconds(2), "browser_console_messages", consolePath)
                ],
                [])
            {
                ToolReceipts =
                [
                    CreateToolReceipt("workspace-file", "workspace_stat_path", "artifacts/process-runs/upstream/implementation/change-set.md", ".", "Succeeded: Path exists.", now),
                    CreateToolReceipt("workspace-file", "workspace_read_file", "artifacts/process-runs/upstream/implementation/change-set.md", ".", "Succeeded: Read file.", now.AddSeconds(1)),
                    CreateToolReceipt("workspace-process", "local_mcp_launch", "@playwright/mcp@latest --headless", tempWorkspace, "Prepared", now),
                    CreateToolReceipt("workspace-process", "workspace_read_file", $"artifacts/scopes/organization/demo/process-runs/{runId:D}/browser-snapshot.yml", ".", $"Failed: File '{snapshotPath}' does not exist in the workspace.", now.AddSeconds(3))
                ]
            };

            var failures = Assert.IsAssignableFrom<IReadOnlyList<ProcessAutomationToolExecutionReceipt>>(
                resolveFailures.Invoke(null, [detail]));
            var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);

            Assert.Empty(failures);
            Assert.Equal(ProcessStepRunStatus.Completed, status);
        }
        finally
        {
            if (Directory.Exists(tempWorkspace))
            {
                Directory.Delete(tempWorkspace, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveUnresolvedCriticalToolFailures_ignores_redundant_denied_bootstrap_after_tool_backed_validation_succeeds()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveFailures = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => string.Equals(method.Name, "ResolveUnresolvedCriticalToolFailures", StringComparison.Ordinal) &&
                              method.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("ResolveUnresolvedCriticalToolFailures method was not found.");

        var now = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var chatSession = new ProcessAutomationChatSession(
            Guid.NewGuid(),
            agentId,
            "Recovered scaffold run",
            now,
            now,
            [
                new ProcessAutomationChatMessage(
                    Guid.NewGuid(),
                    ProcessAutomationChatMessageRole.Assistant,
                    "The existing solution skeleton was inspected and validated.",
                    now.AddSeconds(5),
                    56)
            ],
            LatestExecutionRunId: null);
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                runId,
                agentId,
                chatSession.Id,
                "Recovered scaffold run",
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
                    "The existing solution skeleton was inspected, built, and smoke-tested."),
                "OpenAI chat completions",
                "gpt-4o-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                []),
            chatSession,
            [],
            [])
        {
            ToolReceipts =
            [
                CreateToolReceipt("workspace-process", "workspace_dotnet_new", "dotnet_new sln", ".", "Succeeded (exit 0)", now),
                CreateToolReceipt("workspace-process", "workspace_dotnet_new", "dotnet_new blazor", ".", "Succeeded (exit 0)", now.AddMilliseconds(500)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_build", "build external-target/C/app/App.csproj", ".", "Succeeded (exit 0)", now.AddSeconds(1)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_run", "run external-target/C/app/App.csproj", ".", "Succeeded (exit 0)", now.AddSeconds(2)),
                CreateToolReceipt("workspace-file", "workspace_read_file", "external-target/C/app/App.csproj", ".", "Succeeded: Read file.", now.AddSeconds(3)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_new", "dotnet_new blazor", ".", "Denied", now.AddSeconds(4))
            ]
        };

        var failures = Assert.IsAssignableFrom<IReadOnlyList<ProcessAutomationToolExecutionReceipt>>(
            resolveFailures.Invoke(null, [detail]));

        Assert.Empty(failures);
    }

    [Fact]
    public void ResolveMissingRequiredToolExecutions_accepts_validated_existing_dotnet_scaffold_without_dotnet_new()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingTools = serviceType.GetMethod("ResolveMissingRequiredToolExecutions", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredToolExecutions method was not found.");

        var candidate = CreateDispatchCandidateWithStepTitle(
            "Create solution and Blazor SSR app",
            "Use workspace_dotnet_new when the scaffold is absent. If an existing scaffold is present, inspect it and prove it with workspace_dotnet_build, workspace_dotnet_test, and workspace_dotnet_run.",
            ProcessStepKind.Work);
        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Existing scaffold validation run",
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
                    "Existing .NET scaffold was inspected, built, tested, and smoke-tested."),
                "OpenAI chat completions",
                "gpt-4.1",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                CreateToolReceipt("workspace-file", "workspace_stat_path", "external-target/C/app/PocketPantry.MenuPlanner.slnx", ".", "Succeeded: Path exists.", now),
                CreateToolReceipt("workspace-file", "workspace_read_file", "external-target/C/app/src/PocketPantry.MenuPlanner/PocketPantry.MenuPlanner.csproj", ".", "Succeeded: Read file.", now.AddSeconds(1)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_build", "build external-target/C/app/src/PocketPantry.MenuPlanner/PocketPantry.MenuPlanner.csproj", ".", "Succeeded (exit 0)", now.AddSeconds(2)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_test", "test external-target/C/app/tests/PocketPantry.MenuPlanner.Tests/PocketPantry.MenuPlanner.Tests.csproj", ".", "Succeeded (exit 0)", now.AddSeconds(3)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_run", "run external-target/C/app/src/PocketPantry.MenuPlanner/PocketPantry.MenuPlanner.csproj", ".", "Succeeded (exit 0)", now.AddSeconds(4))
            ]
        };

        var missingTools = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            resolveMissingTools.Invoke(null, [candidate, detail]));

        Assert.DoesNotContain("workspace_dotnet_new", missingTools);
        Assert.Empty(missingTools);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "workspace-process",
                    "workspace_pwsh_run_script",
                    "LocalExecution",
                    "Required",
                    "PolicyOnlyLocal",
                    "Launch-WorkflowProof.ps1",
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
    public void ResolveProviderNativeBrowserOutputDirectoryPaths_prepares_current_run_and_browser_expectation_dirs()
    {
        var runId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                Guid.NewGuid(),
                ProcessArtifactKind.Evidence,
                "Workflow screenshot",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                $"Create this artifact at artifacts/process-runs/{runId:D}/browser/workflow-proof.png using browser_take_screenshot.",
                string.Empty),
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                Guid.NewGuid(),
                ProcessArtifactKind.Checklist,
                "QA note",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                $"Create this artifact at artifacts/process-runs/{runId:D}/notes/qa-note.md using workspace_write_file.",
                string.Empty)
        };

        var directories = ProcessRunAutomationDispatchService.ResolveProviderNativeBrowserOutputDirectoryPaths(
            $"artifacts/process-runs/{runId:D}",
            expectedArtifacts);

        Assert.Contains($"artifacts/process-runs/{runId:D}", directories, StringComparer.OrdinalIgnoreCase);
        Assert.Contains($"artifacts/process-runs/{runId:D}/browser", directories, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain($"artifacts/process-runs/{runId:D}/notes", directories, StringComparer.OrdinalIgnoreCase);
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
    [InlineData(ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded, true)]
    [InlineData(ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Failed, false)]
    [InlineData(ProcessAutomationExecutionState.Failed, ProcessAutomationRunOutcome.Failed, false)]
    [InlineData(ProcessAutomationExecutionState.WaitingOnTool, null, false)]
    public void ShouldProjectFinalAssistantResponse_only_allows_completed_successful_runs(
        ProcessAutomationExecutionState state,
        ProcessAutomationRunOutcome? outcome,
        bool expected)
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldProjectFinalAssistantResponse = serviceType.GetMethod("ShouldProjectFinalAssistantResponse", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldProjectFinalAssistantResponse method was not found.");

        var result = (bool)(shouldProjectFinalAssistantResponse.Invoke(null, [CreateExecutionRun("process-automation-dispatch", state, outcome)]) ?? false);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded, ProcessStepRunStatus.Completed, true)]
    [InlineData(ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded, ProcessStepRunStatus.Failed, false)]
    [InlineData(ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Succeeded, ProcessStepRunStatus.WaitingApproval, false)]
    [InlineData(ProcessAutomationExecutionState.Completed, ProcessAutomationRunOutcome.Failed, ProcessStepRunStatus.Completed, false)]
    public void ShouldProjectResponseTextArtifacts_requires_a_completed_process_step(
        ProcessAutomationExecutionState state,
        ProcessAutomationRunOutcome? outcome,
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
    public void ResolveProjectableResponseArtifactText_uses_human_summary_for_structured_process_outcome()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveProjectableResponseArtifactText = serviceType.GetMethod(
            "ResolveProjectableResponseArtifactText",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveProjectableResponseArtifactText method was not found.");
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Scope boundary was recorded.",
            summaryMarkdown:
            """
            ## Scope boundary packet

            No-go constraints, operational impact, acceptance boundary, assumptions, exclusions, and validation hooks are recorded for this run.
            """);

        var projectableText = resolveProjectableResponseArtifactText.Invoke(null, [responseText]) as string;

        Assert.NotNull(projectableText);
        Assert.StartsWith("## Scope boundary packet", projectableText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"humanReadableSummaryMarkdown\"", projectableText, StringComparison.Ordinal);
        Assert.DoesNotContain("\"status\"", projectableText, StringComparison.Ordinal);
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

    [Theory]
    [InlineData("App startup receipt", "Startup command, working directory, PID, port, and readiness proof.")]
    [InlineData("Single page screenshot handoff", "Cleanup receipt and final project asset reference.")]
    [InlineData("Browser navigation and console evidence", "Capture URL, route, browser console observations, and page readiness proof.")]
    [InlineData("Repaired run evidence index", "Must include output root, run folder, app path, build/test outputs, final app URL/screenshot, console log, project-structure node/asset ids, approvals/blockers, and raw record pointers.")]
    public void CanProjectResponseTextArtifactWithoutDeclaredPath_allows_pathless_receipt_and_handoff_evidence(
        string title,
        string validationRequirementSummary)
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var canProjectResponseTextArtifactWithoutDeclaredPath = serviceType.GetMethod(
            "CanProjectResponseTextArtifactWithoutDeclaredPath",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CanProjectResponseTextArtifactWithoutDeclaredPath method was not found.");
        var expectedArtifact = new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
            Guid.NewGuid(),
            ProcessArtifactKind.Evidence,
            title,
            true,
            ProcessArtifactTrustRequirement.ReviewRequired,
            ProcessSensitivityLevel.Internal,
            validationRequirementSummary,
            string.Empty);

        var result = (bool)(canProjectResponseTextArtifactWithoutDeclaredPath.Invoke(null, [expectedArtifact]) ?? false);

        Assert.True(result);
    }

    [Theory]
    [InlineData("artifacts/process-runs/run-001/inventory-desktop.png")]
    [InlineData("artifacts/scopes/organization/demo/process-runs/run-001/inventory-desktop.png")]
    public void IsProviderNativeBrowserArtifactPath_accepts_current_run_and_scoped_process_run_outputs(string path)
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "IsProviderNativeBrowserArtifactPath",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("IsProviderNativeBrowserArtifactPath method was not found.");

        var result = (bool)(method.Invoke(null, [path]) ?? false);

        Assert.True(result);
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_satisfies_pathless_browser_evidence_from_response_and_scoped_browser_output()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredArtifactSummary = serviceType.GetMethod(
            "ResolveMissingRequiredArtifactSummary",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "Capture page screenshot",
            "Capture the app page screenshot with browser tools and report browser navigation evidence.",
            ProcessStepKind.Work,
            (ProcessArtifactKind.Evidence, "Browser navigation and console evidence", true, "Capture URL, route, browser console observations, and page readiness proof."),
            (ProcessArtifactKind.Evidence, "Page screenshot file", true, "Capture a PNG screenshot of the requested app page using browser_take_screenshot."));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "The browser reached the inventory route and screenshots were saved for the requested page.",
            summaryMarkdown:
            """
            ## Browser navigation and console evidence

            Browser navigation opened http://127.0.0.1:56039/inventory, confirmed the inventory route was ready, and checked console observations with no blocking errors. Page readiness was confirmed before the screenshots were captured, and the route under test matched the process target.

            ## Page screenshot file

            Desktop screenshot: artifacts/scopes/organization/demo/process-runs/run-001/inventory-desktop.png
            Mobile screenshot: artifacts/scopes/organization/demo/process-runs/run-001/inventory-mobile.png
            """);
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                (
                    "browser_take_screenshot",
                    new Dictionary<string, object?> { ["filename"] = "artifacts/scopes/organization/demo/process-runs/run-001/inventory-desktop.png" },
                    CreateProviderNativeTextResult("Saved screenshot.")),
                (
                    "browser_console_messages",
                    new Dictionary<string, object?> { ["filename"] = "artifacts/scopes/organization/demo/process-runs/run-001/inventory-console.log" },
                    CreateProviderNativeTextResult("Saved console messages."))));

        var missingSummary = resolveMissingRequiredArtifactSummary.Invoke(null, [candidate, detail, responseText]) as string;

        Assert.Equal(string.Empty, missingSummary);
    }

    [Fact]
    public void ResolveMissingRequiredArtifactSummary_satisfies_pathless_startup_receipt_from_structured_summary()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRequiredArtifactSummary = serviceType.GetMethod(
            "ResolveMissingRequiredArtifactSummary",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "Start app once",
            "Start the app, record process id, command, working directory, URL, readiness status, and stop command. Do not use Playwright or capture screenshots in this step.",
            ProcessStepKind.Work,
            (ProcessArtifactKind.Evidence, "App startup receipt", true, "Must include command, working directory, process id or managed run handle, URL, and readiness status."));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "The app started and the startup receipt was captured.",
            summaryMarkdown:
            """
            ## App startup receipt

            - Command: dotnet run --project src/TrailheadSnackBox.Web
            - Working directory: external-target/C/programovani/candoitall-dev-55-output/scenario-01-dotnet-trailhead-snack-box/src/TrailheadSnackBox.Web
            - Process id / managed run handle: 63760
            - URL: http://127.0.0.1:52123/inventory
            - Readiness status: HTTP 200 returned for /inventory.
            - Stop command: Stop-Process -Id 63760 -Force
            """);
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                ("workspace_dotnet_run", CreateProviderNativeTextResult("App started at http://127.0.0.1:52123."))));

        var missingSummary = resolveMissingRequiredArtifactSummary.Invoke(null, [candidate, detail, responseText]) as string;

        Assert.Equal(string.Empty, missingSummary);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        Assert.Contains("an absent greenfield deliverable is not a blocker by itself", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("If the concrete deliverable required by this step does not exist", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_does_not_require_browser_proof_for_scope_intake_from_project_structure()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var projectStructureContext = new ProcessProjectStructureContext
        {
            ProjectId = Guid.NewGuid(),
            NodeId = "process-definition:dotnet-development-slice",
            NodeTitle = ".NET implementation slice with atomic validation",
            ParentNodeId = "custom:blazor-counter",
            ParentNodeTitle = "Blazor counter requirements mindmap"
        };
        var candidate = CreateDispatchCandidateCore(
            "Capture scope for a .NET Blazor SSR counter app. Downstream validation must include browser proof for counter increment interaction.",
            ProcessStepKind.Start,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Implementation slice scope packet", true, "Must define acceptance criteria, exclusions, intended product root, setup needs, and validation hooks.")],
            [],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver the requested .NET Blazor SSR app.",
                projectStructureContext),
            "Capture implementation slice boundary");
        const string projectStructureGrounding = """
Dispatcher fetched the live project structure for `Blazor counter requirements mindmap` and focused this prompt on the selected work branch.
Grounded external target paths from the selected project structure:
- `C:\programovani\candoitall-processes1-blazor-counter-a` mapped to `external-target/C/programovani/candoitall-processes1-blazor-counter-a` from Product root (custom:target)
Requirements from project-level planning context:
- .NET Blazor SSR app.
- Browser proof for counter interaction.
""";

        var prompt = buildExecutionPromptCore.Invoke(null, [candidate, null, projectStructureGrounding, null]) as string;

        Assert.NotNull(prompt);
        Assert.DoesNotContain("Mandatory browser proof execution plan", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("This step requires runnable browser proof", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("For .NET browser proof, call `workspace_dotnet_run`", prompt, StringComparison.Ordinal);
        Assert.Contains("an absent greenfield deliverable is not a blocker by itself", prompt, StringComparison.Ordinal);
        Assert.Contains("validation hooks", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildExecutionPromptCore_does_not_require_greenfield_deliverable_for_architecture_steps()
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
            "Validate application, workspace, data, integration, and operational boundaries before implementation starts.",
            ProcessStepKind.Review,
            [],
            false,
            [
                (ProcessArtifactKind.Brief, "Project structure context brief", true, "Must capture the originating project-structure node, resolved working directory, touched modules or routes, dependency boundaries, and downstream artifact expectations."),
                (ProcessArtifactKind.Decision, "Architecture decision record", true, "Must capture selected option, rejected options, source-of-truth choice, and migration ownership.")
            ],
            [],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver the generated application showcase.",
                projectStructureContext),
            "Review architecture and canonical-model impact");
        const string projectStructureGrounding = """
Dispatcher fetched the live project structure for `Workflow` and focused this prompt on the selected work branch.
Grounded external target paths from the selected project structure:
- `C:\programovani\dotnet\WorkflowBoard` mapped to `external-target/C/programovani/dotnet/WorkflowBoard` from Workflow
""";

        var prompt = buildExecutionPromptCore.Invoke(null, [candidate, null, projectStructureGrounding, null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("A greenfield external product root can be absent during scope, architecture, research, or planning steps", prompt, StringComparison.Ordinal);
        Assert.Contains("an absent greenfield deliverable is not a blocker by itself", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("For this implementation, create and edit the deliverable", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("If the concrete deliverable required by this step does not exist", prompt, StringComparison.Ordinal);
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
            "contains only markdown, notes, summaries, checklists, logs, or empty folders",
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
        Assert.Contains(
            "Choose the .NET template and project shape named by the current-run requirements",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Do not default to Blazor, Razor, or Web App templates unless the selected work branch explicitly asks",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "the next product action must be a concrete mutation under that root",
            prompt,
            StringComparison.Ordinal);
        Assert.Contains(
            "Existing markdown, checklist, log, or README files in that directory are not a scaffold and are not a reason to skip project creation",
            prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain("runnable app", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_uses_scaffold_contract_instead_of_product_root_leaf_for_dotnet_setup_steps()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var projectId = Guid.NewGuid();
        var projectStructureContext = new ProcessProjectStructureContext
        {
            ProjectId = projectId,
            NodeId = "process-definition:dotnet-solution-setup",
            NodeTitle = ".NET solution setup subprocess",
            ParentNodeId = "custom:blazor-counter",
            ParentNodeTitle = "Blazor counter requirements mindmap"
        };
        var candidate = CreateDispatchCandidateCore(
            "Create the .NET solution and app project from the upstream scaffold contract.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Solution skeleton change set", true, "Must include the solution file and requested .NET project under the agreed app directory.")],
            [
                (
                    "Capture solution scaffold contract",
                    "Scaffold contract",
                    [
                        (
                            "Scaffold contract",
                            "plan",
                            "artifacts/scopes/organization/demo/process-runs/setup/scaffold-contract.md",
                            "Solution ProcessCounter, app ProcessCounter.Web under productRoot/src, tests ProcessCounter.Tests under productRoot/tests.",
                            "Created by setup contract step.")
                    ])
            ],
            ProcessProjectStructureContextFormatter.AppendToTriggerReason(
                "Deliver the requested .NET Blazor SSR app.",
                projectStructureContext),
            "Create solution and .NET app project",
            processName: ".NET solution setup subprocess",
            outputContractSummary: "Solution file and requested .NET application project are present and added to the solution.");
        const string projectStructureGrounding = """
Dispatcher fetched the live project structure for `Blazor counter requirements mindmap` and focused this prompt on the selected work branch.
Grounded external target paths from the selected project structure:
- `C:\programovani\candoitall-processes1-blazor-counter-c` mapped to `external-target/C/programovani/candoitall-processes1-blazor-counter-c` from Product root (custom:target)
Requirements from project-level planning context:
- .NET Blazor SSR app.
- Solution name ProcessCounter.
- App project ProcessCounter.Web under productRoot/src.
- Test project ProcessCounter.Tests under productRoot/tests.
""";

        var prompt = buildExecutionPromptCore.Invoke(null, [candidate, null, projectStructureGrounding, null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("The upstream scaffold contract overrides the generic product-root leaf scaffold shortcut", prompt, StringComparison.Ordinal);
        Assert.Contains("set `workspace_dotnet_new` `name` to the contract's app project name", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "use `workspace_dotnet_new` with `parentDirectory` set to `external-target/C/programovani` and `name` set to `candoitall-processes1-blazor-counter-c`",
            prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain("name` `candoitall-processes1-blazor-counter-c`", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_uses_stack_neutral_scaffold_guidance_for_javascript_external_targets()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var projectId = Guid.NewGuid();
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the requested JavaScript browser app with package scripts and prove validation passes.",
            new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-main-application",
                ParentNodeTitle = "Create main application"
            });
        const string projectStructureGrounding = """
Dispatcher fetched the live project structure for `FocusTile` and focused this prompt on the selected work branch.
Requirements from project-level planning context:
- JavaScript browser app (feature:javascript); type: ProjectBlock/feature
- package.json scripts, index.html, app.js, app.css, and validation script (feature:static-ui); type: ProjectBlock/feature
- output must be placed in C:\programovani\candoitall-dev-output\focus-tile-final-js-v2 (note:output-path); type: ProjectBlock/note
""";

        var prompt = buildExecutionPromptCore.Invoke(null, [candidate, null, projectStructureGrounding, null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("For JavaScript or TypeScript scaffolding into the grounded external product root", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not use `workspace_dotnet_new` for JavaScript", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("For .NET scaffolding into the grounded external product root", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("use `workspace_dotnet_new` with `parentDirectory`", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("For .NET browser proof, call `workspace_dotnet_run`", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_uses_run_scoped_output_root_when_no_external_target_is_grounded()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildExecutionPromptCore = serviceType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var projectId = Guid.NewGuid();
        var candidate = CreateProjectStructureDispatchCandidate(
            "Implement the requested Blazor app and prove the build passes.",
            new ProcessProjectStructureContext
            {
                ProjectId = projectId,
                NodeId = "project-root",
                NodeTitle = "Tool lending checkout"
            });
        const string projectStructureGrounding = """
Dispatcher fetched the live project structure for `Tool lending checkout` and focused this prompt on the selected work branch.
Ancestor path to the target work node:
- Tool lending checkout (project-root); type: ProjectRoot/default; status: Active; notes: Build a small checkout app for shared tools.
""";

        var prompt = buildExecutionPromptCore.Invoke(null, [candidate, null, projectStructureGrounding, null]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Current-run managed output root:", prompt, StringComparison.Ordinal);
        Assert.Contains("output/process-runs/", prompt, StringComparison.Ordinal);
        Assert.Contains("The dispatcher did not ground an external product root for this run", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not invent, create, retry, or cite any `external-target/...` path", prompt, StringComparison.Ordinal);
        Assert.Contains("use `workspace_dotnet_new` under `output/process-runs/", prompt, StringComparison.Ordinal);
        Assert.Contains("shared `src/`, shared `tests/`, or guessed host folders", prompt, StringComparison.Ordinal);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationExecutionArtifact(
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
        var candidate = CreateDispatchCandidate("Implement the generated .NET application and inspect the concrete deliverable files.");

        var requiredToolNames = resolveRequiredToolNames.Invoke(null, [candidate]) as IReadOnlyList<string>;

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_stat_path", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_read_file", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_write_file", requiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_dotnet_build", requiredToolNames, StringComparer.Ordinal);
        Assert.DoesNotContain("workspace_dotnet_test", requiredToolNames, StringComparer.Ordinal);

        var testCandidate = CreateDispatchCandidate("Implement the generated .NET application and prove build and tests pass.");
        var testRequiredToolNames = resolveRequiredToolNames.Invoke(null, [testCandidate]) as IReadOnlyList<string>;

        Assert.NotNull(testRequiredToolNames);
        Assert.Contains("workspace_dotnet_build", testRequiredToolNames, StringComparer.Ordinal);
        Assert.Contains("workspace_dotnet_test", testRequiredToolNames, StringComparer.Ordinal);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp/Program.cs",
                    ".",
                    "Succeeded",
                    now),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_run",
                    "WorkflowApp.csproj",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp",
                    "Succeeded",
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                    now),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp/Program.cs",
                    ".",
                    "Succeeded",
                    now),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_run",
                    "WorkflowApp.csproj",
                    "external-target/C/programovani/csharp/workflow/WorkflowApp",
                    "Succeeded",
                    now)
            ]
        };

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, Array.Empty<string>(), CreateCarriedImplementationProof(false, false), 1, 3]);

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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
            [candidate, detail, responseText, new[] { "browser_snapshot", "browser_take_screenshot" }, CreateCarriedImplementationProof(false, false), 1, 3]);

        Assert.Equal(ProcessStepRunStatus.Blocked, status);
        Assert.NotNull(reason);
        Assert.Contains("Runtime startup smoke failed", reason, StringComparison.Ordinal);
        Assert.IsType<bool>(shouldRetryResult);
        Assert.False((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_retries_blocked_implementation_after_post_failure_mutation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Build failed, Home.razor was repaired afterward, but post-repair build proof was not captured.",
            summaryMarkdown: "Implementation changed product source after a failed build and still needs a post-repair build receipt.");
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-implementation",
                "corr-implementation",
                "run-start",
                "process-automation-dispatch",
                "system",
                BuildAllowedExternalTargetMetadata("external-target/C/programovani/dotnet/output"),
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_dotnet_build", CreateProviderNativeTextResult("Build failed.")),
                    ("workspace_write_file", CreateProviderNativeTextResult("Home.razor repaired.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Read repaired Home.razor."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_build",
                    "external-target/C/programovani/dotnet/output/output.csproj",
                    "external-target/C/programovani/dotnet/output",
                    "Failed (exit 1): CS8852 init-only property cannot be assigned by generated bind code.",
                    now.AddSeconds(1)),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_write_file",
                    "external-target/C/programovani/dotnet/output/Components/Pages/Home.razor",
                    ".",
                    "Succeeded: Overwrote Home.razor.",
                    now.AddSeconds(2)),
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_read_file",
                    "external-target/C/programovani/dotnet/output/Components/Pages/Home.razor",
                    ".",
                    "Succeeded: Read Home.razor.",
                    now.AddSeconds(3))
            ]
        };

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, Array.Empty<string>(), CreateCarriedImplementationProof(false, false), 2, 5]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_retries_blocked_implementation_after_failed_startup_smoke()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Build and tests passed, but workspace_dotnet_run failed because Home.razor requests UnitConversionService and Program.cs does not register it.",
            summaryMarkdown: "Implementation inspected the concrete product and found a startup smoke defect that needs repair.");
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation run",
                "process-step",
                "step-implementation",
                "corr-implementation",
                "run-start",
                "process-automation-dispatch",
                "system",
                BuildAllowedExternalTargetMetadata("external-target/C/programovani/dotnet/output"),
                "Prompt",
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_read_file", CreateProviderNativeTextResult("Read Home.razor.")),
                    ("workspace_dotnet_build", CreateProviderNativeTextResult("Build succeeded.")),
                    ("workspace_dotnet_test", CreateProviderNativeTextResult("Tests passed.")),
                    ("workspace_dotnet_run", CreateProviderNativeTextResult("Failed startup smoke."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                CreateToolReceipt(
                    "workspace-file",
                    "workspace_read_file",
                    "external-target/C/programovani/dotnet/output/Components/Pages/Home.razor",
                    ".",
                    "Succeeded: Read Home.razor.",
                    now.AddSeconds(1)),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_build",
                    "external-target/C/programovani/dotnet/output/output.csproj",
                    "external-target/C/programovani/dotnet/output",
                    "Succeeded: Build succeeded.",
                    now.AddSeconds(2)),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_test",
                    "external-target/C/programovani/dotnet/output/output.csproj",
                    "external-target/C/programovani/dotnet/output",
                    "Succeeded: Tests passed.",
                    now.AddSeconds(3)),
                CreateToolReceipt(
                    "workspace-process",
                    "workspace_dotnet_run",
                    "external-target/C/programovani/dotnet/output/output.csproj",
                    "external-target/C/programovani/dotnet/output",
                    "Failed (exit 1): InvalidOperationException: Cannot provide a value for property 'ConversionService' on type 'Home'. There is no registered service of type 'output.Domain.UnitConversionService'.",
                    now.AddSeconds(4))
            ]
        };

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [candidate, detail, responseText, Array.Empty<string>(), CreateCarriedImplementationProof(false, false), 1, 5]);

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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
            [candidate, detail, responseText, Array.Empty<string>(), CreateCarriedImplementationProof(false, false), 1, 3]);

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
    public void ResolveRequiredToolNames_does_not_add_dotnet_validation_for_javascript_runnable_app()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate("Implement the JavaScript browser app with package scripts, tests, and startup proof.");

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_stat_path", requiredToolNames);
        Assert.Contains("workspace_read_file", requiredToolNames);
        Assert.Contains("workspace_write_file", requiredToolNames);
        Assert.DoesNotContain("workspace_dotnet_build", requiredToolNames);
        Assert.DoesNotContain("workspace_dotnet_test", requiredToolNames);
        Assert.DoesNotContain("workspace_dotnet_run", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_add_dotnet_validation_for_javascript_work_with_negated_dotnet_stack()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate(
            "Build a simple JavaScript browser application named Garden Plot Planner. This is JavaScript browser work, not .NET, not C#, and not Blazor. Use package scripts for lint, test, build, and preview.");

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_stat_path", requiredToolNames);
        Assert.Contains("workspace_read_file", requiredToolNames);
        Assert.Contains("workspace_write_file", requiredToolNames);
        Assert.DoesNotContain("workspace_dotnet_build", requiredToolNames);
        Assert.DoesNotContain("workspace_dotnet_test", requiredToolNames);
        Assert.DoesNotContain("workspace_dotnet_run", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_add_javascript_helper_for_dotnet_browser_work_with_negated_javascript_stack()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the .NET Blazor app. This is .NET and C# work, not JavaScript.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must name changed flows, browser proof, screenshots, and unresolved risks."));

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.Contains("browser_console_messages", requiredToolNames);
        Assert.Contains("browser_snapshot", requiredToolNames);
        Assert.Contains("browser_take_screenshot", requiredToolNames);
        Assert.DoesNotContain("workspace_pwsh_run_script", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_require_browser_tools_for_peer_review_with_static_web_grounding()
    {
        var resolveRequiredToolNamesCore = typeof(ProcessRunAutomationDispatchService)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "ResolveRequiredToolNamesCore");
        var candidate = CreateDispatchCandidateCore(
            "Inputs: architecture decision record and implementation change set. Outputs: peer review note with accepted issues, rejected concerns, residual risk, and integration readiness.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Peer review note", true, "Must identify accepted issues, rejected concerns, residual risk, and integration readiness.")],
            [
                (
                    "Implement bounded delivery change",
                    "Implementation change set",
                    [
                        (
                            "Implementation change set",
                            "Deliverable",
                            "artifacts/process-runs/run-1/03-implementation-change-set.md",
                            "Static browser app delivered; downstream QA will capture browser proof.",
                            "Created by implementation step."
                        )
                    ])
            ],
            triggerReason: "Project structure says the product is a static web page hosted from an external output directory.",
            stepTitle: "Complete peer review and integration readiness",
            outputContractSummary: "Peer review note",
            manualRecoveryDirective: """
                Manual rerun requested for step 'Complete peer review and integration readiness'.
                Generic dispatcher repair: static-web project grounding identifies the deliverable surface, but peer review does not require browser-proof tools unless this step contract explicitly asks for browser/runtime proof.
                Prior blocked reason: Mandatory browser proof and PowerShell run-script gating tools were not executed: unable to capture browser_snapshot, browser_take_screenshot, and browser_console_messages because no reachable hosted URL or environment run-script was provided in this run.
                Minimal next actions:
                - Repair only the delta described by this packet.
                - Rerun invalidated proof tools.
                """);

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNamesCore.Invoke(
            null,
            [
                candidate,
                "Project-structure grounding: static web app, browser-playable surface, keyboard controls, local storage, and downstream runtime or browser proof."
            ]);

        Assert.NotNull(requiredToolNames);
        Assert.DoesNotContain("browser_console_messages", requiredToolNames);
        Assert.DoesNotContain("browser_snapshot", requiredToolNames);
        Assert.DoesNotContain("browser_take_screenshot", requiredToolNames);
        Assert.DoesNotContain("workspace_pwsh_run_script", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_require_browser_tools_for_security_review_of_qa_accepted_static_web_package()
    {
        var resolveRequiredToolNamesCore = typeof(ProcessRunAutomationDispatchService)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "ResolveRequiredToolNamesCore");
        var candidate = CreateDispatchCandidateCore(
            "Review sensitive-data handling, secrets, boundary changes, and policy exceptions for the QA-accepted package.",
            ProcessStepKind.Approval,
            [],
            false,
            [(ProcessArtifactKind.Decision, "Security exception assessment", true, "Must capture controls, residual risk owner, and approval or block rationale.")],
            [
                (
                    "Run QA validation and runtime or browser proof",
                    "Regression evidence pack",
                    [
                        (
                            "Regression evidence pack",
                            "Evidence",
                            "artifacts/process-runs/run-1/05-regression-evidence-pack.md",
                            "Quality accepted with browser proof, screenshot, console messages, and residual risks recorded.",
                            "Created by QA validation step."
                        )
                    ])
            ],
            triggerReason: "Project structure says the product is a static web page hosted from an external output directory.",
            stepTitle: "Perform security and data-handling review",
            outputContractSummary: "Security outcome with explicit approval, block, or exception rationale.");

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNamesCore.Invoke(
            null,
            [
                candidate,
                "Project-structure grounding: static web app, browser-playable surface, keyboard controls, local storage, and completed upstream QA browser proof."
            ]);

        Assert.NotNull(requiredToolNames);
        Assert.DoesNotContain("browser_console_messages", requiredToolNames);
        Assert.DoesNotContain("browser_snapshot", requiredToolNames);
        Assert.DoesNotContain("browser_take_screenshot", requiredToolNames);
        Assert.DoesNotContain("workspace_pwsh_run_script", requiredToolNames);
    }

    [Fact]
    public void BuildExecutionPrompt_guides_javascript_browser_proof_to_stack_appropriate_helper()
    {
        var buildExecutionPrompt = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildExecutionPrompt", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildExecutionPrompt method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the JavaScript browser app with package scripts.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must name changed flows, browser proof, screenshots, and unresolved risks."));

        var prompt = buildExecutionPrompt.Invoke(null, [candidate]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("For JavaScript or TypeScript browser proof", prompt, StringComparison.Ordinal);
        Assert.Contains("Mandatory browser proof execution plan", prompt, StringComparison.Ordinal);
        Assert.Contains("browser_navigate", prompt, StringComparison.Ordinal);
        Assert.Contains("then call `browser_snapshot` with depth 2 and boxes false, `browser_take_screenshot` with fullPage false or no fullPage argument, and `browser_console_messages`", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not use `workspace_dotnet_build`, `workspace_dotnet_test`, or `workspace_dotnet_run` for JavaScript or TypeScript deliverables", prompt, StringComparison.Ordinal);
        Assert.Contains("first create a helper script", prompt, StringComparison.Ordinal);
        Assert.Contains("Never write helper code like `Resolve-Path 'external-target/C/...'`", prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", prompt, StringComparison.Ordinal);
        Assert.Contains("npm.cmd", prompt, StringComparison.Ordinal);
        Assert.Contains("single-quoted here-string", prompt, StringComparison.Ordinal);
        Assert.Contains("escape every literal `$`", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not run the long-running server loop inside the `workspace_pwsh_run_script` process until the tool times out.", prompt, StringComparison.Ordinal);
        Assert.Contains("The helper must not be the foreground web server.", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not pass a server implementation script itself to `workspace_pwsh_run_script`", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not call blocking stream reads such as `.ReadToEnd()`", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not use `[System.Threading.Tasks.Task]::Run({ ... })` with scriptblocks", prompt, StringComparison.Ordinal);
        Assert.Contains("Use native absolute paths for stdout/stderr redirection", prompt, StringComparison.Ordinal);
        Assert.Contains("Do not build child PowerShell server code as a double-quoted `-Command` string", prompt, StringComparison.Ordinal);
        Assert.Contains("Probe the recorded URL with a bounded `Invoke-WebRequest` loop", prompt, StringComparison.Ordinal);
        Assert.Contains("do not make missing `package.json` or missing automated tests release-blocking", prompt, StringComparison.Ordinal);
        Assert.Contains("use `browser_evaluate`", prompt, StringComparison.Ordinal);
        Assert.Contains("replace it with `browser_evaluate` DOM or state proof", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildExecutionPromptCore_marks_peer_review_with_static_web_grounding_as_not_browser_gated()
    {
        var buildExecutionPromptCore = typeof(ProcessRunAutomationDispatchService)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Single(method => method.Name == "BuildExecutionPromptCore" && method.GetParameters().Length == 4);
        var candidate = CreateDispatchCandidateCore(
            "Inputs: implementation package, architecture decision record, and changed-surface inventory. Outputs: peer-reviewed change set with explicit residual risk and follow-up items.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Peer review note", true, "Must capture accepted issues, rejected concerns, and explicit residual risk.")],
            [],
            triggerReason: "Project structure says the product is a static web page hosted from an external output directory.",
            stepTitle: "Complete peer review and integration readiness",
            outputContractSummary: "Peer review note",
            manualRecoveryDirective: """
                Manual rerun requested for step 'Complete peer review and integration readiness'.
                Prior blocked reason: Mandatory browser proof and PowerShell run-script gating tools were not executed: unable to capture browser_snapshot, browser_take_screenshot, and browser_console_messages.
                Browser proof is not required for this peer-review rerun unless the current step contract explicitly asks for runtime or browser proof.
                """);
        const string projectStructureGroundingSummary = """
            Project-structure required artifact contract:
            - Delivery shape: JavaScript static web page.
            - Output root: C:\programovani\dotnet-demo\output mapped to external-target/C/programovani/dotnet-demo/output.
            """;

        var prompt = buildExecutionPromptCore.Invoke(
            null,
            [
                candidate,
                null,
                projectStructureGroundingSummary,
                null
            ]) as string;

        Assert.NotNull(prompt);
        Assert.Contains("Browser proof boundary:", prompt, StringComparison.Ordinal);
        Assert.Contains("this step is not browser-proof gated", prompt, StringComparison.Ordinal);
        Assert.Contains("inspect those inherited artifact paths directly with workspace tools", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Mandatory browser proof execution plan:", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("`browser_snapshot`", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("`browser_take_screenshot`", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_require_browser_tools_for_console_qa_template_wording()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the C# console app.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must name changed flows, runtime/API/browser evidence as applicable, screenshots for UI surfaces, and unresolved risks. When a visible browser workflow is in scope, include current-run process-visible browser artifacts under `artifacts/process-runs/<run-id>/browser/`: screenshot image, browser_snapshot or browser_evaluate state output, browser_console_messages output, actual URL or entrypoint, launch and cleanup receipts, and acceptance-state assertion."));

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.DoesNotContain("browser_console_messages", requiredToolNames);
        Assert.DoesNotContain("browser_snapshot", requiredToolNames);
        Assert.DoesNotContain("browser_take_screenshot", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_require_browser_tools_for_api_as_applicable_template_wording()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate(
            "Re-run QA validation and runtime or browser proof after repair for the .NET minimal API service.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Repaired regression evidence pack", true, "Must name repaired flows, assertion depth, runtime/API/browser evidence as applicable, screenshots for UI surfaces, and unresolved risks after the repair pass. When a visible browser workflow is in scope, include current-run process-visible browser artifacts under `artifacts/process-runs/<run-id>/browser/`: screenshot image, browser_snapshot or browser_evaluate state output, browser_console_messages output, actual URL or entrypoint, launch and cleanup receipts, and acceptance-state assertion."));

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.DoesNotContain("browser_console_messages", requiredToolNames);
        Assert.DoesNotContain("browser_snapshot", requiredToolNames);
        Assert.DoesNotContain("browser_take_screenshot", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_requires_browser_tools_for_static_web_recheck_with_document_context()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Verify the repaired package from the source document and project structure. The deliverable is a JavaScript static web page with no backend.",
            ProcessStepKind.Review,
            [],
            false,
            [(ProcessArtifactKind.Evidence, "Repaired regression evidence pack", true, "Must name repaired flows, assertion depth, warning counts, executed-test counts when tests are expected, runtime/API/browser evidence as applicable, screenshots for UI surfaces, and unresolved risks after the repair pass. When a visible browser workflow is in scope, include current-run process-visible browser artifacts under `artifacts/process-runs/<run-id>/browser/`: screenshot image, browser_snapshot or browser_evaluate state output, browser_console_messages output, actual URL or entrypoint, launch and cleanup receipts, and acceptance-state assertion.")],
            [],
            triggerReason: "Project structure requires a static web page in C:\\programovani\\dotnet-demo\\output with no backend and client-local state.",
            stepTitle: "Re-run QA validation and runtime or browser proof after repair");

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.Contains("browser_console_messages", requiredToolNames);
        Assert.Contains("browser_snapshot", requiredToolNames);
        Assert.Contains("browser_take_screenshot", requiredToolNames);
        Assert.Contains("workspace_pwsh_run_script", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_requires_browser_tools_for_javascript_browser_app()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the JavaScript browser app.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must name changed flows, browser proof, screenshots, and unresolved risks."));

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.Contains("browser_console_messages", requiredToolNames);
        Assert.Contains("browser_snapshot", requiredToolNames);
        Assert.Contains("browser_take_screenshot", requiredToolNames);
        Assert.Contains("workspace_pwsh_run_script", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_require_browser_tools_for_javascript_architecture_review()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate(
            "Review architecture for the JavaScript browser app and document the source-of-truth decision.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Decision, "Architecture decision record", true, "Must capture selected option and rejected alternatives."),
            (ProcessArtifactKind.Brief, "Project structure context brief", true, "Must capture product root and touched files."));

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.DoesNotContain("browser_console_messages", requiredToolNames);
        Assert.DoesNotContain("browser_snapshot", requiredToolNames);
        Assert.DoesNotContain("browser_take_screenshot", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_require_browser_tools_for_architecture_with_downstream_browser_validation_hooks()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate(
            "Check architecture and source-of-truth impact for the Blazor browser app. Record downstream validation hooks: build, test, run, and browser proof after implementation. Instructions: browser_take_screenshot belongs to the later validation step, not this architecture step.",
            ProcessStepKind.Work,
            (ProcessArtifactKind.Decision, "Architecture decision record", true, "Must capture selected option, rejected alternatives, source-of-truth impact, and downstream browser validation hooks."));

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.DoesNotContain("browser_console_messages", requiredToolNames);
        Assert.DoesNotContain("browser_snapshot", requiredToolNames);
        Assert.DoesNotContain("browser_take_screenshot", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_adds_dotnet_validation_for_dotnet_runnable_app()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate("Implement the .NET minimal API app with tests and startup proof.");

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_dotnet_build", requiredToolNames);
        Assert.Contains("workspace_dotnet_test", requiredToolNames);
        Assert.Contains("workspace_dotnet_run", requiredToolNames);
    }

    [Fact]
    public void ResolveRequiredToolNames_does_not_add_dotnet_test_from_generic_change_set_artifact()
    {
        var resolveRequiredToolNames = typeof(ProcessRunAutomationDispatchService).GetMethod("ResolveRequiredToolNames", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRequiredToolNames method was not found.");
        var candidate = CreateDispatchCandidate(
            "Implement the .NET minimal API app with startup endpoint proof.",
            ProcessStepKind.Work,
            (ProcessArtifactKind.Deliverable, "Implementation change set", true, "Must be linked to tests, migration notes, and touched-surface inventory."));

        var requiredToolNames = (IReadOnlyList<string>?)resolveRequiredToolNames.Invoke(null, [candidate]);

        Assert.NotNull(requiredToolNames);
        Assert.Contains("workspace_dotnet_build", requiredToolNames);
        Assert.Contains("workspace_dotnet_run", requiredToolNames);
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
    public void ResolveMissingConcreteImplementationProofSummary_rejects_markdown_only_app_artifacts_under_external_output_root()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingProof = serviceType.GetMethod("ResolveMissingConcreteImplementationProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummary method was not found.");
        var candidate = CreateDispatchCandidate("Implement the Basic App as a Blazor application and prove startup.");
        var rootAlias = "external-target/C/programovani/dotnet/output";
        var markdownArtifactPath = $"{rootAlias}/03-implementation-change-set.md";
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Implementation markdown artifacts were reviewed.");
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(("workspace_read_file", CreateProviderNativeTextResult("Read markdown artifact."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                CreateToolReceipt("workspace-file", "workspace_read_file", markdownArtifactPath, ".", "Succeeded", now)
            ]
        };

        var summary = resolveMissingProof.Invoke(null, [candidate, detail]) as string;

        Assert.NotNull(summary);
        Assert.Contains("source or project", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveMissingConcreteImplementationProofSummary_allows_repair_deliverable_mutation_after_source_read()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingProof = serviceType.GetMethod("ResolveMissingConcreteImplementationProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummary method was not found.");
        var candidate = CreateDispatchCandidate("Repair validation findings for the static web app and prepare it for browser proof.");
        var rootAlias = "external-target/C/programovani/dotnet-demo/output";
        var now = DateTimeOffset.UtcNow;
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Repair artifacts updated and source inspected."),
            BuildSerializedSessionState(
                ("workspace_write_file", CreateProviderNativeTextResult("Validation README updated.")),
                ("workspace_write_file", CreateProviderNativeTextResult("Migration note updated.")),
                ("workspace_read_file", CreateProviderNativeTextResult("Entrypoint source inspected."))),
            [
                CreateToolReceipt("workspace-file", "workspace_write_file", $"{rootAlias}/README-validation.md", ".", "Succeeded", now),
                CreateToolReceipt("workspace-file", "workspace_write_file", $"{rootAlias}/MIGRATION.md", ".", "Succeeded", now.AddSeconds(1)),
                CreateToolReceipt("workspace-file", "workspace_read_file", $"{rootAlias}/index.html", ".", "Succeeded", now.AddSeconds(2))
            ],
            serializedInvocationMetadataJson: BuildAllowedExternalTargetMetadata(rootAlias));

        var summary = resolveMissingProof.Invoke(null, [candidate, detail]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveMissingConcreteImplementationProofSummary_accepts_source_read_under_external_output_root()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingProof = serviceType.GetMethod("ResolveMissingConcreteImplementationProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummary method was not found.");
        var candidate = CreateDispatchCandidate("Implement the Basic App as a Blazor application.");
        var rootAlias = "external-target/C/programovani/dotnet/output";
        var sourcePath = $"{rootAlias}/Program.cs";
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Implementation source was created and inspected.");
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_write_file", CreateProviderNativeTextResult("Source written.")),
                    ("workspace_read_file", CreateProviderNativeTextResult("Source reviewed."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                CreateToolReceipt("workspace-file", "workspace_write_file", sourcePath, ".", "Succeeded", now),
                CreateToolReceipt("workspace-file", "workspace_read_file", sourcePath, ".", "Succeeded", now.AddSeconds(1))
            ]
        };

        var summary = resolveMissingProof.Invoke(null, [candidate, detail]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveMissingConcreteImplementationProofSummary_accepts_source_read_under_scoped_current_run_output_root()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingProof = serviceType.GetMethod("ResolveMissingConcreteImplementationProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummary method was not found.");
        var candidate = CreateDispatchCandidate("Implement the requested Blazor application and prove startup.");
        var runId = Guid.NewGuid();
        var sourcePath = $"output/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/{runId:D}/SamplePwaApp/Program.cs";
        var projectPath = $"output/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/{runId:D}/SamplePwaApp/SamplePwaApp.csproj";
        var now = DateTimeOffset.UtcNow;
        var detail = CreateSuccessfulExecutionDetail(
            StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Implementation source was written, read back, built, and smoke-tested."),
            BuildSerializedSessionState(
                ("workspace_write_file", CreateProviderNativeTextResult("Source written.")),
                ("workspace_read_file", CreateProviderNativeTextResult("Source reviewed.")),
                ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                ("workspace_dotnet_run", CreateProviderNativeTextResult("Startup smoke passed."))),
            [
                CreateToolReceipt("workspace-file", "workspace_write_file", sourcePath, ".", "Succeeded", now),
                CreateToolReceipt("workspace-file", "workspace_read_file", sourcePath, ".", "Succeeded", now.AddSeconds(1)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_build", $"build {projectPath}", ".", "Succeeded (exit 0)", now.AddSeconds(2)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_run", projectPath, ".", "Succeeded (exit 0)", now.AddSeconds(3))
            ]);

        var summary = resolveMissingProof.Invoke(null, [candidate, detail]) as string;

        Assert.Equal(string.Empty, summary);
    }

    [Fact]
    public void ResolveRunnableDotNetHostProjectPaths_ignores_managed_workspace_working_directory_for_external_runs()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveHostPaths = serviceType.GetMethod("ResolveRunnableDotNetHostProjectPaths", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveRunnableDotNetHostProjectPaths method was not found.");
        var scratchRoot = Path.Combine(Path.GetTempPath(), $"candoitall-host-proof-{Guid.NewGuid():N}");
        var productRoot = Path.Combine(scratchRoot, "product", "output");
        var managedWorkspaceRoot = Path.Combine(
            scratchRoot,
            "CanDoItAll",
            "control-plane",
            "database-profiles",
            "postgresql",
            "profile",
            "workspace");
        var staleProjectRoot = Path.Combine(
            managedWorkspaceRoot,
            "artifacts",
            "scopes",
            "organization",
            "profile",
            "CalculatorApp");
        try
        {
            Directory.CreateDirectory(productRoot);
            Directory.CreateDirectory(staleProjectRoot);
            File.WriteAllText(
                Path.Combine(staleProjectRoot, "CalculatorApp.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            var rootAlias = ToExternalTargetAlias(productRoot);
            var now = DateTimeOffset.UtcNow;
            var detail = new ProcessAutomationExecutionRunDetail(
                new ProcessAutomationExecutionRunRecord(
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
                    "No host exists in the external target yet.",
                    "OpenAI chat completions",
                    "gpt-4.1",
                    ProcessAutomationExecutionState.Completed,
                    ProcessAutomationRunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(("workspace_stat_path", CreateProviderNativeTextResult("Path exists."))),
                    []),
                null,
                [],
                [])
            {
                ToolReceipts =
                [
                    CreateToolReceipt("workspace-file", "workspace_stat_path", rootAlias, managedWorkspaceRoot, "Succeeded", now)
                ]
            };

            var paths = ((System.Collections.IEnumerable?)resolveHostPaths.Invoke(null, [detail, detail.ToolReceipts]))
                ?.Cast<object>()
                .Select(item => item.ToString() ?? string.Empty)
                .ToList()
                ?? new List<string>();

            Assert.Empty(paths);
        }
        finally
        {
            if (Directory.Exists(scratchRoot))
            {
                Directory.Delete(scratchRoot, recursive: true);
            }
        }
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
            var detail = new ProcessAutomationExecutionRunDetail(
                new ProcessAutomationExecutionRunRecord(
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
                    ProcessAutomationExecutionState.Completed,
                    ProcessAutomationRunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Read complete.")),
                        ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                        ("workspace_dotnet_test", CreateProviderNativeTextResult("Tests passed."))),
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
                    CreateToolReceipt("workspace-process", "workspace_dotnet_test", projectAlias, rootAlias, "Succeeded", now.AddSeconds(3)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(4))
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
    public void ResolveMissingRunnableApplicationProofSummary_ignores_accidental_csproj_for_javascript_contract()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingRunnableProof = serviceType.GetMethod("ResolveMissingRunnableApplicationProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRunnableApplicationProofSummary method was not found.");
        var productRoot = Path.Combine(Path.GetTempPath(), $"candoitall-js-runtime-proof-{Guid.NewGuid():N}");
        try
        {
            var projectPath = Path.Combine(productRoot, "GardenPlotPlanner.csproj");
            var sourcePath = Path.Combine(productRoot, "app.js");
            Directory.CreateDirectory(productRoot);
            File.WriteAllText(
                projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(sourcePath, "export function planGarden() { return []; }");

            var rootAlias = ToExternalTargetAlias(productRoot);
            var sourceAlias = ToExternalTargetAlias(sourcePath);
            var candidate = CreateDispatchCandidate(
                "Build a simple JavaScript browser application named Garden Plot Planner. This is JavaScript browser work, not .NET, not C#, and not Blazor. Use package scripts for lint, test, build, and preview.");
            var now = DateTimeOffset.UtcNow;
            var responseText = StructuredOutcome(ProcessStepOutcomeStatus.Completed, "JavaScript implementation completed and npm validation passed.");
            var detail = new ProcessAutomationExecutionRunDetail(
                new ProcessAutomationExecutionRunRecord(
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
                    ProcessAutomationExecutionState.Completed,
                    ProcessAutomationRunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_write_file", CreateProviderNativeTextResult("Source written.")),
                        ("workspace_pwsh_run_script", CreateProviderNativeTextResult("npm lint, test, build, and preview passed.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Source reviewed."))),
                    []),
                null,
                [],
                [])
            {
                ToolReceipts =
                [
                    CreateToolReceipt("workspace-file", "workspace_stat_path", rootAlias, ".", "Succeeded", now),
                    CreateToolReceipt("workspace-file", "workspace_write_file", sourceAlias, ".", "Succeeded", now.AddSeconds(1)),
                    CreateToolReceipt("workspace-process", "workspace_pwsh_run_script", rootAlias, rootAlias, "Succeeded", now.AddSeconds(2)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(3))
                ]
            };

            var summary = resolveMissingRunnableProof.Invoke(null, [candidate, detail]) as string;

            Assert.Equal(string.Empty, summary);
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
            var detail = new ProcessAutomationExecutionRunDetail(
                new ProcessAutomationExecutionRunRecord(
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
                    ProcessAutomationExecutionState.Completed,
                    ProcessAutomationRunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Read complete.")),
                        ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                        ("workspace_dotnet_test", CreateProviderNativeTextResult("Tests passed.")),
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
                    CreateToolReceipt("workspace-process", "workspace_dotnet_test", projectAlias, rootAlias, "Succeeded", now.AddSeconds(3)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_run", projectAlias, rootAlias, "Succeeded", now.AddSeconds(4)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(5))
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
    public void ResolveCompletionStatus_allows_product_relative_paths_when_external_target_leaf_is_output()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var productParent = Path.Combine(Path.GetTempPath(), $"candoitall-output-leaf-{Guid.NewGuid():N}");
        var productRoot = Path.Combine(productParent, "output");
        try
        {
            var projectPath = Path.Combine(productRoot, "output.csproj");
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
            var responseText = StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Updated product files output/output.csproj and output/Program.cs, then build/test/startup validation passed.");
            var detail = new ProcessAutomationExecutionRunDetail(
                new ProcessAutomationExecutionRunRecord(
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
                    ProcessAutomationExecutionState.Completed,
                    ProcessAutomationRunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Read output/Program.cs.")),
                        ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                        ("workspace_dotnet_test", CreateProviderNativeTextResult("Tests passed.")),
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
                    CreateToolReceipt("workspace-process", "workspace_dotnet_test", projectAlias, rootAlias, "Succeeded", now.AddSeconds(3)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_run", projectAlias, rootAlias, "Succeeded", now.AddSeconds(4)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(5))
                ]
            };

            var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
            var reason = (string?)buildCompletionReason.Invoke(null, [candidate, detail, "Implementation"]);

            Assert.True(status == ProcessStepRunStatus.Completed, reason);
        }
        finally
        {
            if (Directory.Exists(productParent))
            {
                Directory.Delete(productParent, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveCompletionStatus_accepts_retry_artifacts_after_validation_without_new_product_mutation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var productRoot = Path.Combine(Path.GetTempPath(), $"candoitall-implementation-retry-{Guid.NewGuid():N}");
        try
        {
            var projectPath = Path.Combine(productRoot, "output.csproj");
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
            var runId = Guid.NewGuid();
            var implementationArtifactPath = $"artifacts/process-runs/{runId:D}/03-implementation-change-set.md";
            var rolloutArtifactPath = $"artifacts/process-runs/{runId:D}/03-migration-and-rollout-preparation-checklist.md";
            var candidate = CreateWorkflowImplementationDispatchCandidate();
            var now = DateTimeOffset.UtcNow;
            var responseText = StructuredOutcome(
                ProcessStepOutcomeStatus.Completed,
                "Existing implementation was re-read and validated with build, tests, and startup smoke. Fresh implementation and rollout artifacts were written after validation.");
            var detail = new ProcessAutomationExecutionRunDetail(
                new ProcessAutomationExecutionRunRecord(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    null,
                    "Implementation retry run",
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
                    ProcessAutomationExecutionState.Completed,
                    ProcessAutomationRunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Read product source.")),
                        ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                        ("workspace_dotnet_test", CreateProviderNativeTextResult("Tests passed.")),
                        ("workspace_dotnet_run", CreateProviderNativeTextResult("Startup smoke passed.")),
                        ("workspace_write_file", CreateProviderNativeTextResult("Artifacts written."))),
                    []),
                null,
                [],
                [])
            {
                ToolReceipts =
                [
                    CreateToolReceipt("workspace-file", "workspace_stat_path", rootAlias, ".", "Succeeded", now.AddSeconds(1)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(2)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_build", projectAlias, rootAlias, "Succeeded", now.AddSeconds(3)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_test", projectAlias, rootAlias, "Succeeded", now.AddSeconds(4)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_run", projectAlias, rootAlias, "Succeeded", now.AddSeconds(5)),
                    CreateToolReceipt("workspace-file", "workspace_write_file", implementationArtifactPath, ".", "Succeeded", now.AddSeconds(6)),
                    CreateToolReceipt("workspace-file", "workspace_write_file", rolloutArtifactPath, ".", "Succeeded", now.AddSeconds(7))
                ]
            };

            var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
            var reason = (string?)buildCompletionReason.Invoke(null, [candidate, detail, "Implementation"]);

            Assert.True(status == ProcessStepRunStatus.Completed, reason);
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
            var detail = new ProcessAutomationExecutionRunDetail(
                new ProcessAutomationExecutionRunRecord(
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
                    ProcessAutomationExecutionState.Completed,
                    ProcessAutomationRunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Read complete.")),
                        ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                        ("workspace_dotnet_test", CreateProviderNativeTextResult("Tests passed.")),
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
                    CreateToolReceipt("workspace-process", "workspace_dotnet_test", projectAlias, rootAlias, "Succeeded", now.AddSeconds(3)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_run", projectAlias, rootAlias, "Succeeded", now.AddSeconds(4)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(5))
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
            var detail = new ProcessAutomationExecutionRunDetail(
                new ProcessAutomationExecutionRunRecord(
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
                    ProcessAutomationExecutionState.Completed,
                    ProcessAutomationRunOutcome.Succeeded,
                    now,
                    now,
                    now,
                    now,
                    string.Empty,
                    BuildSerializedSessionState(
                        ("workspace_stat_path", CreateProviderNativeTextResult("Path exists.")),
                        ("workspace_read_file", CreateProviderNativeTextResult("Read complete.")),
                        ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                        ("workspace_dotnet_test", CreateProviderNativeTextResult("Tests passed.")),
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
                    CreateToolReceipt("workspace-process", "workspace_dotnet_test", projectAlias, rootAlias, "Succeeded", now.AddSeconds(3)),
                    CreateToolReceipt("workspace-process", "workspace_dotnet_run", "-NoLogo -NoProfile -EncodedCommand AAAA", rootAlias, "Succeeded", now.AddSeconds(4)),
                    CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(5))
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
                new ProcessAutomationToolExecutionReceipt(
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
    public void ResolveMissingConcreteImplementationProofSummary_allows_dotnet_validation_after_source_mutation()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveMissingProof = serviceType.GetMethod("ResolveMissingConcreteImplementationProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingConcreteImplementationProofSummary method was not found.");
        var candidate = CreateDispatchCandidate("Implement the Blazor app with tests and migration notes.");
        var now = DateTimeOffset.UtcNow;
        var rootAlias = "external-target/C/programovani/dotnet/output";
        var sourceAlias = $"{rootAlias}/Domain/UnitConversionTests.cs";
        var projectAlias = $"{rootAlias}/output.csproj";
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Blazor implementation run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Source was inspected, repaired, built, and tested after the final mutation.",
                "OpenAI chat completions",
                "gpt-5.4-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_read_file", CreateProviderNativeTextResult("Read current source.")),
                    ("workspace_write_file", CreateProviderNativeTextResult("Updated source.")),
                    ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                    ("workspace_dotnet_test", CreateProviderNativeTextResult("Tests passed."))),
                []),
            null,
            [],
            [])
        {
            ToolReceipts =
            [
                CreateToolReceipt("workspace-file", "workspace_read_file", sourceAlias, ".", "Succeeded", now.AddSeconds(1)),
                CreateToolReceipt("workspace-file", "workspace_write_file", sourceAlias, ".", "Succeeded", now.AddSeconds(2)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_build", projectAlias, rootAlias, "Succeeded", now.AddSeconds(3)),
                CreateToolReceipt("workspace-process", "workspace_dotnet_test", projectAlias, rootAlias, "Succeeded", now.AddSeconds(4))
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
                1
            ]) as string;

        Assert.NotNull(directive);
        Assert.Contains("Inspect the inherited durable artifacts directly on this retry", directive, StringComparison.Ordinal);
        Assert.Contains("artifacts/scopes/organization/demo/architecture/Workflow-Architecture.md", directive, StringComparison.Ordinal);
        Assert.Contains("runnable host/project", directive, StringComparison.Ordinal);
        Assert.Contains("Reading only markdown evidence", directive, StringComparison.Ordinal);
        Assert.Contains("Do not recover by submitting Completed for pre-existing markdown artifacts", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCompletionArtifactRecoveryDirective_focuses_on_missing_artifacts_without_full_step_rerun()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var buildRecoveryDirective = serviceType.GetMethod("BuildCompletionArtifactRecoveryDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionArtifactRecoveryDirective method was not found.");
        var outcomeType = serviceType.GetNestedType("DispatchExecutionOutcome", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DispatchExecutionOutcome type was not found.");
        var candidate = CreateDispatchCandidate(
            "Build the Blazor application and write implementation handoff artifacts.",
            ProcessStepKind.Work,
            (
                ProcessArtifactKind.Evidence,
                "Blazor implementation change set",
                true,
                "Create this artifact at artifacts/process-runs/run-1/02-blazor-implementation-change-set.md with changed files and validation proof."),
            (
                ProcessArtifactKind.Evidence,
                "Implementation self-review summary",
                true,
                "Create this artifact at artifacts/process-runs/run-1/02-implementation-self-review-summary.md with risks and follow-up notes."));
        var expectedArtifacts = candidate.GetType().GetProperty("ExpectedArtifacts")?.GetValue(candidate)
            ?? throw new InvalidOperationException("DispatchCandidate.ExpectedArtifacts property was not found.");
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(ProcessStepOutcomeStatus.Completed, "Implementation completed and validation passed.");
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Implementation completed",
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
                "gpt-5.4-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_dotnet_build", CreateProviderNativeTextResult("Build passed.")),
                    ("workspace_dotnet_test", CreateProviderNativeTextResult("Tests passed."))),
                []),
            null,
            [],
            []);
        var outcome = Activator.CreateInstance(
                outcomeType,
                detail,
                responseText,
                ProcessStepRunStatus.Completed,
                "Implementation completed.",
                Array.Empty<string>(),
                1,
                null)
            ?? throw new InvalidOperationException("DispatchExecutionOutcome could not be constructed.");

        var directive = buildRecoveryDirective.Invoke(null, [candidate, outcome, expectedArtifacts]) as string;

        Assert.NotNull(directive);
        Assert.Contains("Targeted completion-artifact recovery is required.", directive, StringComparison.Ordinal);
        Assert.Contains("You are the process manager", directive, StringComparison.Ordinal);
        Assert.Contains("previous step history", directive, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("left the process without required artifact record", directive, StringComparison.Ordinal);
        Assert.Contains("Do not repeat broad implementation work", directive, StringComparison.Ordinal);
        Assert.Contains("Do not delegate this back to the implementation executor", directive, StringComparison.Ordinal);
        Assert.Contains("workspace_write_file", directive, StringComparison.Ordinal);
        Assert.Contains("02-blazor-implementation-change-set.md", directive, StringComparison.Ordinal);
        Assert.Contains("02-implementation-self-review-summary.md", directive, StringComparison.Ordinal);
        Assert.Contains("ProcessStepOutcomeResult", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldAttemptManagerArtifactRecoveryForStrandedStep_requires_recoverable_inprogress_attempt_with_missing_artifacts()
    {
        var missingArtifact = CreateDispatchArtifactExpectation(
            "Blazor implementation change set",
            isRequired: true);
        var recordedArtifact = CreateDispatchArtifactExpectation(
            "Implementation self-review summary",
            isRequired: true);
        var recordedArtifactIds = new HashSet<Guid> { recordedArtifact.Id };
        var recoveryExecutionRunId = Guid.NewGuid();

        Assert.True(ProcessRunAutomationDispatchService.ShouldAttemptManagerArtifactRecoveryForStrandedStep(
            ProcessStepRunStatus.InProgress,
            recoveryExecutionRunId,
            [missingArtifact, recordedArtifact],
            recordedArtifactIds));
        Assert.False(ProcessRunAutomationDispatchService.ShouldAttemptManagerArtifactRecoveryForStrandedStep(
            ProcessStepRunStatus.Ready,
            recoveryExecutionRunId,
            [missingArtifact],
            new HashSet<Guid>()));
        Assert.False(ProcessRunAutomationDispatchService.ShouldAttemptManagerArtifactRecoveryForStrandedStep(
            ProcessStepRunStatus.InProgress,
            null,
            [missingArtifact],
            new HashSet<Guid>()));
        Assert.False(ProcessRunAutomationDispatchService.ShouldAttemptManagerArtifactRecoveryForStrandedStep(
            ProcessStepRunStatus.InProgress,
            recoveryExecutionRunId,
            [missingArtifact],
            new HashSet<Guid> { missingArtifact.Id }));
    }

    [Fact]
    public void ResolveArtifactRecoveryExecutionRunId_allows_prior_terminal_attempt_for_reopened_missing_artifact_step()
    {
        var missingArtifact = CreateDispatchArtifactExpectation(
            "Blazor implementation change set",
            isRequired: true);
        var reopenedAtUtc = DateTimeOffset.UtcNow;
        var priorTerminalRun = CreateExecutionRun(
            "process-automation-dispatch",
            ProcessAutomationExecutionState.Failed,
            ProcessAutomationRunOutcome.Cancelled) with
        {
            StartedAtUtc = reopenedAtUtc.AddMinutes(-20),
            CompletedAtUtc = reopenedAtUtc.AddMinutes(-15),
            UpdatedAtUtc = reopenedAtUtc.AddMinutes(-15)
        };
        var manualDebugRun = CreateExecutionRun(
            "agent-run-debug",
            ProcessAutomationExecutionState.Completed,
            ProcessAutomationRunOutcome.Succeeded) with
        {
            CompletedAtUtc = reopenedAtUtc
        };

        var result = ProcessRunAutomationDispatchService.ResolveArtifactRecoveryExecutionRunId(
            new ProcessStepRun
            {
                Status = ProcessStepRunStatus.InProgress,
                StartedAtUtc = reopenedAtUtc
            },
            [manualDebugRun, priorTerminalRun],
            [missingArtifact],
            new HashSet<Guid>());

        Assert.Equal(priorTerminalRun.Id, result);
    }

    [Fact]
    public void ResolveArtifactRecoveryExecutionRunId_returns_stale_waiting_tool_attempt_for_missing_artifact_step()
    {
        var missingArtifact = CreateDispatchArtifactExpectation(
            "Blazor runtime evidence pack",
            isRequired: true);
        var startedAtUtc = DateTimeOffset.Parse("2026-05-27T18:52:44+00:00");
        var staleWaitingRun = CreateExecutionRun(
            "process-automation-dispatch",
            ProcessAutomationExecutionState.WaitingOnTool,
            null) with
        {
            CreatedAtUtc = startedAtUtc,
            StartedAtUtc = startedAtUtc,
            UpdatedAtUtc = startedAtUtc
        };
        var freshWaitingRun = CreateExecutionRun(
            "process-automation-dispatch",
            ProcessAutomationExecutionState.WaitingOnTool,
            null) with
        {
            CreatedAtUtc = startedAtUtc.AddMinutes(20),
            StartedAtUtc = startedAtUtc.AddMinutes(20),
            UpdatedAtUtc = startedAtUtc.AddMinutes(20)
        };

        var staleResult = ProcessRunAutomationDispatchService.ResolveArtifactRecoveryExecutionRunId(
            new ProcessStepRun
            {
                Status = ProcessStepRunStatus.InProgress,
                StartedAtUtc = startedAtUtc
            },
            [staleWaitingRun],
            [missingArtifact],
            new HashSet<Guid>(),
            startedAtUtc.AddMinutes(11));
        var freshResult = ProcessRunAutomationDispatchService.ResolveArtifactRecoveryExecutionRunId(
            new ProcessStepRun
            {
                Status = ProcessStepRunStatus.InProgress,
                StartedAtUtc = startedAtUtc
            },
            [freshWaitingRun],
            [missingArtifact],
            new HashSet<Guid>(),
            startedAtUtc.AddMinutes(21));

        Assert.Equal(staleWaitingRun.Id, staleResult);
        Assert.Null(freshResult);
    }

    [Theory]
    [InlineData(ProcessRuntimeEventTypes.ManualAgentStepRerun, false)]
    [InlineData(" runtime-recovery-scan ", true)]
    [InlineData("step-transition:Completed", true)]
    [InlineData("", true)]
    public void ShouldReusePriorArtifactRecoveryExecutionRun_starts_fresh_attempt_for_manual_rerun(
        string trigger,
        bool expected)
    {
        var shouldReuse = ProcessRunAutomationDispatchService.ShouldReusePriorArtifactRecoveryExecutionRun(trigger);

        Assert.Equal(expected, shouldReuse);
    }

    [Fact]
    public void ShouldAttemptStrandedDispositionArtifactFinalization_requires_stale_or_terminal_disposition_run()
    {
        var candidate = (ProcessRunAutomationDispatchService.DispatchCandidate)CreateDispatchCandidateCore(
            "Validate the Blazor runtime and route to repair when validation fails.",
            ProcessStepKind.Review,
            [
                ("repair-required", "Repair required", "Validation found repairable runtime findings."),
                ("passed", "Passed", "Validation passed.")
            ],
            requiresExplicitBranchOutcomeSelection: true,
            [(ProcessArtifactKind.Evidence, "Blazor runtime evidence pack", true, "Create artifacts/process-runs/{processRunId}/03-blazor-runtime-evidence-pack.md.")],
            [],
            stepTitle: "Validate Blazor runtime and browser evidence");
        var startedAtUtc = DateTimeOffset.Parse("2026-05-27T18:52:44+00:00");
        var staleWaitingRun = CreateExecutionRun(
            "process-automation-dispatch",
            ProcessAutomationExecutionState.WaitingOnTool,
            null) with
        {
            CreatedAtUtc = startedAtUtc,
            StartedAtUtc = startedAtUtc,
            UpdatedAtUtc = startedAtUtc
        };
        var completedRun = CreateExecutionRun(
            "process-automation-dispatch",
            ProcessAutomationExecutionState.Failed,
            ProcessAutomationRunOutcome.Failed);

        Assert.True(ProcessRunAutomationDispatchService.ShouldAttemptStrandedDispositionArtifactFinalization(
            candidate,
            staleWaitingRun,
            "Acceptance decision status: repair-required.",
            startedAtUtc.AddMinutes(11)));
        Assert.True(ProcessRunAutomationDispatchService.ShouldAttemptStrandedDispositionArtifactFinalization(
            candidate,
            completedRun,
            "Status: repair-required.",
            startedAtUtc.AddMinutes(1)));
        Assert.False(ProcessRunAutomationDispatchService.ShouldAttemptStrandedDispositionArtifactFinalization(
            candidate,
            staleWaitingRun,
            "Validation failed but no branch outcome was selected.",
            startedAtUtc.AddMinutes(11)));
        Assert.False(ProcessRunAutomationDispatchService.ShouldAttemptStrandedDispositionArtifactFinalization(
            candidate,
            staleWaitingRun,
            "Status: repair-required.",
            startedAtUtc.AddMinutes(2)));
    }

    [Fact]
    public void ApplyArtifactProjectionLineage_SB02_INV_001_uses_compact_key_for_long_recovery_lineage()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var lineageType = serviceType.GetNestedType("ArtifactProjectionLineage", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ArtifactProjectionLineage type was not found.");
        var applyLineage = serviceType.GetMethod("ApplyArtifactProjectionLineage", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ApplyArtifactProjectionLineage method was not found.");
        var recoveryExecutionRunId = Guid.NewGuid();
        var recoveredForExecutionRunId = Guid.NewGuid();
        var projectedExecutionRunId = Guid.NewGuid();
        var reworkPacketId = Guid.NewGuid();
        var longSourceKey = $"workspace-written-artifact|{projectedExecutionRunId:D}|{Guid.NewGuid():D}|{string.Join("/", Enumerable.Repeat("deep-artifact-segment", 12))}/implementation-change-set.md";
        var lineage = Activator.CreateInstance(
            lineageType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [recoveryExecutionRunId, recoveredForExecutionRunId, reworkPacketId],
            culture: null);

        var compactKey = (string?)applyLineage.Invoke(null, [longSourceKey, projectedExecutionRunId, lineage])
            ?? throw new InvalidOperationException("Lineage key was not returned.");

        Assert.StartsWith("manager-recovery-artifact|sha256:", compactKey, StringComparison.Ordinal);
        Assert.True(compactKey.Length <= 200);
        Assert.DoesNotContain(recoveryExecutionRunId.ToString("D"), compactKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(recoveredForExecutionRunId.ToString("D"), compactKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(longSourceKey, compactKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessArtifactProjectionLineageBuilder_SB05_INV_001_hashes_recovery_key_and_records_lineage()
    {
        var recoveryExecutionRunId = Guid.NewGuid();
        var recoveredForExecutionRunId = Guid.NewGuid();
        var projectedExecutionRunId = Guid.NewGuid();
        var sourceArtifactId = Guid.NewGuid();
        var context = new ProcessArtifactRecoveryProjectionContext(
            recoveryExecutionRunId,
            recoveredForExecutionRunId,
            Guid.NewGuid());
        var sourceExternalReferenceKey = $"workspace-written-artifact|{projectedExecutionRunId:D}|{Guid.NewGuid():D}|artifacts/process-runs/current/deep/implementation-change-set.md";

        var compactKey = ProcessArtifactProjectionLineageBuilder.ApplyRecoveryLineage(
            sourceExternalReferenceKey,
            projectedExecutionRunId,
            context);
        var lineage = ProcessArtifactProjectionLineageBuilder.BuildLineage(
            ProcessArtifactProjectionSourceKind.AgentExecutionArtifact,
            projectedExecutionRunId,
            context,
            sourceArtifactId,
            sourceExternalReferenceKey);
        var provenance = ProcessArtifactProjectionLineageBuilder.BuildProvenance(
            "Projected artifact.",
            projectedExecutionRunId,
            context);

        Assert.StartsWith("manager-recovery-artifact|sha256:", compactKey, StringComparison.Ordinal);
        Assert.True(compactKey.Length <= 200);
        Assert.Equal(ProcessArtifactProjectionSourceKind.AgentExecutionArtifact, lineage.SourceKind);
        Assert.Equal(projectedExecutionRunId, lineage.SourceExecutionRunId);
        Assert.Equal(projectedExecutionRunId, lineage.ProjectedExecutionRunId);
        Assert.Equal(recoveryExecutionRunId, lineage.RecoveryExecutionRunId);
        Assert.Equal(recoveredForExecutionRunId, lineage.RecoveredForExecutionRunId);
        Assert.Equal(sourceArtifactId, lineage.SourceArtifactId);
        Assert.Equal(sourceExternalReferenceKey, lineage.SourceExternalReferenceKey);
        Assert.Contains(recoveryExecutionRunId.ToString("D"), provenance, StringComparison.Ordinal);
        Assert.Contains(recoveredForExecutionRunId.ToString("D"), provenance, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessArtifactExpectationMatcher_SB05_INV_002_disambiguates_strong_match_by_kind()
    {
        var deliverableId = Guid.NewGuid();
        var evidenceId = Guid.NewGuid();
        var expectedArtifacts = new[]
        {
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                deliverableId,
                ProcessArtifactKind.Deliverable,
                "Release packet",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Create release packet.",
                string.Empty),
            new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
                evidenceId,
                ProcessArtifactKind.Evidence,
                "Release packet",
                true,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                "Create release packet evidence.",
                string.Empty)
        };

        var matchedId = ProcessArtifactExpectationMatcher.MatchStrongExpectedArtifactId(
            expectedArtifacts,
            ProcessArtifactKind.Evidence,
            item => string.Equals(item.Title, "Release packet", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(evidenceId, matchedId);
    }

    [Fact]
    public void ProcessArtifactProjectionPlanner_SB07_INV_001_plans_execution_artifact_without_storage_side_effects()
    {
        var executionRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "Implementation change set",
            true,
            "Create a durable implementation change set.");
        var artifact = new ProcessAutomationExecutionArtifact(
            Guid.NewGuid(),
            executionRunId,
            "generated-output",
            "implementation-change-set.md",
            "artifacts/process-runs/current/implementation-change-set.md",
            "text/markdown",
            "workspace_write_file",
            "Implementation diff summary.",
            DateTimeOffset.UtcNow);

        var plan = ProcessArtifactProjectionPlanner.PlanExecutionArtifact(
            executionRunId,
            artifact,
            expectation,
            ProcessArtifactKind.Evidence,
            ProcessStepRunStatus.Completed,
            "Run completed.",
            ProcessArtifactRecoveryProjectionContext.None);

        Assert.Equal(ProcessArtifactProjectionSourceKind.AgentExecutionArtifact, plan.SourceKind);
        Assert.Equal($"agentframework-artifact:{artifact.Id:D}", plan.SourceExternalReferenceKey);
        Assert.Equal(plan.SourceExternalReferenceKey, plan.ExternalReferenceKey);
        Assert.Equal(expectation.Id, plan.ArtifactExpectationId);
        Assert.Equal(ProcessArtifactKind.Deliverable, plan.ArtifactKind);
        Assert.Equal("Implementation change set", plan.Title);
        Assert.Equal(ProcessArtifactTrustStatus.ReviewRequired, plan.TrustStatus);
        Assert.Equal("Implementation diff summary.", plan.ReviewSummary);
        Assert.Equal(plan.SourceExternalReferenceKey, plan.ProjectionLineage.SourceExternalReferenceKey);
        Assert.Equal(executionRunId, plan.ProjectionLineage.SourceExecutionRunId);
    }

    [Fact]
    public void ProcessArtifactProjectionPlanner_SB09_INV_001_normalizes_projection_adapter_keys()
    {
        var executionRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var expectationId = Guid.NewGuid();

        Assert.Equal(
            $"workspace-written-artifact|{executionRunId:D}|{expectationId:D}|artifacts/process-runs/current/report.md",
            ProcessArtifactProjectionPlanner.BuildWorkspaceWrittenArtifactExternalReferenceKey(
                executionRunId,
                expectationId,
                "\\artifacts\\process-runs\\current\\report.md"));
        Assert.Equal(
            $"existing-managed-artifact|{executionRunId:D}|{expectationId:D}|output/process-runs/current/result.json",
            ProcessArtifactProjectionPlanner.BuildExistingManagedArtifactExternalReferenceKey(
                executionRunId,
                expectationId,
                "output/process-runs/current/result.json"));
        Assert.Equal(
            $"assistant-response|{executionRunId:D}|artifacts/process-runs/current/summary.md",
            ProcessArtifactProjectionPlanner.BuildResponseTextArtifactExternalReferenceKey(
                executionRunId,
                "artifacts/process-runs/current/summary.md"));
        Assert.Equal(
            $"process-mock-artifact:{stepRunId:D}:{expectationId:D}:data/process-runs/current/mock.json",
            ProcessArtifactProjectionPlanner.BuildProcessMockArtifactExternalReferenceKey(
                stepRunId,
                expectationId,
                "data/process-runs/current/mock.json"));
    }

    [Fact]
    public void ProcessArtifactEvidenceValidationRules_SB10_INV_001_rejects_stranded_evidence_and_requires_durable_paths()
    {
        Assert.False(ProcessArtifactEvidenceValidationRules.IsProducerAllowedForMode(
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Evidence,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AssistantResponse));
        Assert.True(ProcessArtifactEvidenceValidationRules.RequiresManagedEvidencePath(
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RuntimeProof,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ProviderNativeBrowser));
        Assert.True(ProcessArtifactEvidenceValidationRules.RequiresStoredArtifactContent(
            expectationIsRequired: true,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Narrative,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Manual,
            "artifacts/process-runs/current/review.md"));
        Assert.False(ProcessArtifactEvidenceValidationRules.RequiresStoredArtifactContent(
            expectationIsRequired: true,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Decision,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact,
            "workflow/output/decision.json"));
    }

    [Fact]
    public void WorkflowArtifactProjectionMapping_SB09_INV_001_uses_explicit_output_id_when_same_kind_names_conflict()
    {
        var stepDefinitionId = Guid.NewGuid();
        var workflowRunId = WorkflowRunId.New();
        var financeExpectation = new ProcessArtifactExpectation
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = stepDefinitionId,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Finance approval packet",
            WorkflowOutputId = "finance-output",
            WorkflowOutputKind = WorkflowArtifactKind.Json
        };
        var complianceExpectation = new ProcessArtifactExpectation
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = stepDefinitionId,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Compliance approval packet",
            WorkflowOutputId = "compliance-output",
            WorkflowOutputKind = WorkflowArtifactKind.Json
        };
        var financeArtifact = new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            workflowRunId,
            WorkflowArtifactKind.Json,
            new WorkflowNodeId("finance-output"),
            "Compliance approval packet",
            "application/json",
            "workflow/finance.json",
            "Finance packet emitted from workflow node.",
            DateTimeOffset.UtcNow);
        var complianceArtifact = new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            workflowRunId,
            WorkflowArtifactKind.Json,
            new WorkflowNodeId("compliance-output"),
            "Finance approval packet",
            "application/json",
            "workflow/compliance.json",
            "Compliance packet emitted from workflow node.",
            DateTimeOffset.UtcNow);

        var result = ProcessWorkflowRunCoordinator.ResolveWorkflowArtifactExpectation(
            [financeExpectation, complianceExpectation],
            [financeArtifact, complianceArtifact],
            ProcessArtifactKind.Deliverable,
            financeArtifact,
            out var diagnostic);

        Assert.Equal(financeExpectation.Id, result?.Id);
        Assert.Equal(string.Empty, diagnostic);
    }

    [Fact]
    public void WorkflowArtifactProjectionMapping_SB09_INV_001_blocks_same_kind_heuristic_without_explicit_output_id()
    {
        var stepDefinitionId = Guid.NewGuid();
        var workflowRunId = WorkflowRunId.New();
        var financeExpectation = new ProcessArtifactExpectation
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = stepDefinitionId,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Finance approval packet"
        };
        var complianceExpectation = new ProcessArtifactExpectation
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = stepDefinitionId,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Compliance approval packet"
        };
        var financeArtifact = new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            workflowRunId,
            WorkflowArtifactKind.Json,
            new WorkflowNodeId("finance-output"),
            "Finance approval packet",
            "application/json",
            "workflow/finance.json",
            "Finance packet emitted from workflow node.",
            DateTimeOffset.UtcNow);
        var complianceArtifact = new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            workflowRunId,
            WorkflowArtifactKind.Json,
            new WorkflowNodeId("compliance-output"),
            "Compliance approval packet",
            "application/json",
            "workflow/compliance.json",
            "Compliance packet emitted from workflow node.",
            DateTimeOffset.UtcNow);

        var result = ProcessWorkflowRunCoordinator.ResolveWorkflowArtifactExpectation(
            [financeExpectation, complianceExpectation],
            [financeArtifact, complianceArtifact],
            ProcessArtifactKind.Deliverable,
            financeArtifact,
            out var diagnostic);

        Assert.Null(result);
        Assert.Contains("ambiguous", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicit workflow output mapping", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowArtifactProjectionMapping_SB09_INV_001_warns_when_legacy_same_kind_fallback_maps()
    {
        var stepDefinitionId = Guid.NewGuid();
        var workflowRunId = WorkflowRunId.New();
        var expectation = new ProcessArtifactExpectation
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = stepDefinitionId,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Board decision memo"
        };
        var artifact = new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            workflowRunId,
            WorkflowArtifactKind.Json,
            new WorkflowNodeId("decision-output"),
            "Board decision memo",
            "application/json",
            "workflow/decision.json",
            "Board decision memo emitted from workflow node.",
            DateTimeOffset.UtcNow);

        var result = ProcessWorkflowRunCoordinator.ResolveWorkflowArtifactExpectation(
            [expectation],
            [artifact],
            ProcessArtifactKind.Deliverable,
            artifact,
            out var diagnostic);

        Assert.Equal(expectation.Id, result?.Id);
        Assert.Contains("legacy same-kind workflow artifact fallback", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubprocessArtifactProjectionMapping_SB09_INV_001_uses_child_expectation_id_when_same_kind_titles_conflict()
    {
        var childFinanceExpectationId = Guid.NewGuid();
        var childComplianceExpectationId = Guid.NewGuid();
        var parentFinanceExpectation = new ProcessArtifactExpectation
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = Guid.NewGuid(),
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Finance approval packet",
            SubprocessChildArtifactExpectationId = childFinanceExpectationId
        };
        var wrongTitleArtifact = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = childComplianceExpectationId,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Finance approval packet",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var mappedArtifact = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = childFinanceExpectationId,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Compliance approval packet",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = ProcessRunAutomationDispatchService.ResolveSubprocessSourceArtifact(
            [wrongTitleArtifact, mappedArtifact],
            [parentFinanceExpectation],
            parentFinanceExpectation,
            out var diagnostic);

        Assert.Equal(mappedArtifact.Id, result?.Id);
        Assert.Equal(string.Empty, diagnostic);
    }

    [Fact]
    public void SubprocessArtifactProjectionMapping_SB09_INV_001_blocks_same_kind_heuristic_without_child_mapping()
    {
        var parentFinanceExpectation = new ProcessArtifactExpectation
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = Guid.NewGuid(),
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Finance approval packet"
        };
        var wrongTitleArtifact = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = Guid.NewGuid(),
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Finance approval packet",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var secondArtifact = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = Guid.NewGuid(),
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Compliance approval packet",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = ProcessRunAutomationDispatchService.ResolveSubprocessSourceArtifact(
            [wrongTitleArtifact, secondArtifact],
            [parentFinanceExpectation],
            parentFinanceExpectation,
            out var diagnostic);

        Assert.Null(result);
        Assert.Contains("ambiguous", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("explicit subprocess child expectation mapping", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SubprocessArtifactProjectionMapping_SB09_INV_001_warns_when_legacy_same_kind_fallback_maps()
    {
        var parentExpectation = new ProcessArtifactExpectation
        {
            Id = Guid.NewGuid(),
            StepDefinitionId = Guid.NewGuid(),
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Board decision memo"
        };
        var childArtifact = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = Guid.NewGuid(),
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Board decision memo",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = ProcessRunAutomationDispatchService.ResolveSubprocessSourceArtifact(
            [childArtifact],
            [parentExpectation],
            parentExpectation,
            out var diagnostic);

        Assert.Equal(childArtifact.Id, result?.Id);
        Assert.Contains("legacy same-kind subprocess artifact fallback", diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveManagerArtifactRecoveryAgent_prefers_configured_run_manager_option()
    {
        var managerPartyId = Guid.NewGuid();
        var managerTechnicalAgentId = Guid.NewGuid();
        var run = new ProcessRun
        {
            ManagerAgentId = managerPartyId,
            ManagerAgentName = "Default process manager"
        };

        var result = ProcessRunAutomationDispatchService.ResolveManagerArtifactRecoveryAgent(
            run,
            [
                new ProcessManagerAgentOption(
                    managerPartyId,
                    managerTechnicalAgentId,
                    "Default process manager",
                    "OpenAI",
                    "gpt-5.4-mini",
                    "Bound process manager")
            ],
            []);

        Assert.NotNull(result);
        Assert.Equal(managerTechnicalAgentId, result.TechnicalAgentId);
        Assert.Equal("run-manager-id", result.ResolutionSource);
    }

    [Fact]
    public void ResolveManagerArtifactRecoveryAgent_uses_assigned_manager_before_ambiguous_fallback()
    {
        var assignedManagerPartyId = Guid.NewGuid();
        var assignedManagerTechnicalAgentId = Guid.NewGuid();
        var otherManagerPartyId = Guid.NewGuid();
        var otherManagerTechnicalAgentId = Guid.NewGuid();
        var run = new ProcessRun
        {
            ManagerAgentName = "Default process manager"
        };

        var result = ProcessRunAutomationDispatchService.ResolveManagerArtifactRecoveryAgent(
            run,
            [
                CreateAssignment(assignedManagerPartyId, "Blazor delivery manager AI agent", "Blazor delivery manager"),
                CreateAssignment(Guid.NewGuid(), "Blazor Application Developer", "Blazor implementation engineer")
            ],
            [
                new ProcessManagerAgentOption(
                    assignedManagerPartyId,
                    assignedManagerTechnicalAgentId,
                    "Blazor delivery manager AI agent",
                    "OpenAI",
                    "gpt-5-mini",
                    "Projected from AgentFramework organization catalog."),
                new ProcessManagerAgentOption(
                    otherManagerPartyId,
                    otherManagerTechnicalAgentId,
                    "Delivery manager AI agent",
                    "OpenAI",
                    "gpt-5.4-mini",
                    "Projected from AgentFramework organization catalog.")
            ],
            []);

        Assert.NotNull(result);
        Assert.Equal(assignedManagerTechnicalAgentId, result.TechnicalAgentId);
        Assert.Equal("run-manager-assignment", result.ResolutionSource);
    }

    [Fact]
    public void ResolveManagerArtifactRecoveryAgent_rejects_ambiguous_manager_like_fallback_agents()
    {
        var run = new ProcessRun();
        var now = DateTimeOffset.UtcNow;

        var result = ProcessRunAutomationDispatchService.ResolveManagerArtifactRecoveryAgent(
            run,
            [],
            [
                CreateAgentDefinition("Delivery Manager", "Process manager", now),
                CreateAgentDefinition("Release Manager", "Process manager", now)
            ]);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveManagerArtifactRecoveryAgent_rejects_single_generic_lead_fallback_agent()
    {
        var run = new ProcessRun();
        var now = DateTimeOffset.UtcNow;

        var result = ProcessRunAutomationDispatchService.ResolveManagerArtifactRecoveryAgent(
            run,
            [],
            [CreateAgentDefinition("Delivery Lead", "Lead engineer", now)]);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveManagerArtifactRecoveryAgent_allows_single_explicit_artifact_recovery_manager()
    {
        var technicalAgentId = Guid.NewGuid();

        var result = ProcessRunAutomationDispatchService.ResolveManagerArtifactRecoveryAgent(
            new ProcessRun(),
            [
                new ProcessManagerAgentOption(
                    Guid.NewGuid(),
                    technicalAgentId,
                    "Recovery specialist",
                    "OpenAI",
                    "gpt-5-mini",
                    "Capability: process-artifact-recovery-manager")
            ],
            []);

        Assert.NotNull(result);
        Assert.Equal(technicalAgentId, result.TechnicalAgentId);
        Assert.Equal("single-explicit-recovery-option", result.ResolutionSource);
    }

    [Fact]
    public void ProcessManagerAgentResolver_uses_assigned_manager_before_ambiguous_manager_options()
    {
        var assignedManagerPartyId = Guid.NewGuid();
        var assignedManagerTechnicalAgentId = Guid.NewGuid();
        var otherManagerPartyId = Guid.NewGuid();
        var otherManagerTechnicalAgentId = Guid.NewGuid();

        var result = ProcessManagerAgentResolver.ResolveAssignedTechnicalAgentId(
            [
                CreateAssignment(assignedManagerPartyId, "Blazor delivery manager AI agent", "Blazor delivery manager"),
                CreateAssignment(Guid.NewGuid(), "Blazor Application Developer", "Blazor implementation engineer")
            ],
            [
                new ProcessManagerAgentOption(
                    assignedManagerPartyId,
                    assignedManagerTechnicalAgentId,
                    "Blazor delivery manager AI agent",
                    "OpenAI",
                    "gpt-5-mini",
                    "Projected from AgentFramework organization catalog."),
                new ProcessManagerAgentOption(
                    otherManagerPartyId,
                    otherManagerTechnicalAgentId,
                    "Delivery manager AI agent",
                    "OpenAI",
                    "gpt-5.4-mini",
                    "Projected from AgentFramework organization catalog.")
            ],
            []);

        Assert.Equal(assignedManagerTechnicalAgentId, result);
    }

    [Fact]
    public void ProcessManagerAgentResolver_rejects_ambiguous_assigned_managers()
    {
        var firstManagerPartyId = Guid.NewGuid();
        var secondManagerPartyId = Guid.NewGuid();

        var result = ProcessManagerAgentResolver.ResolveAssignedTechnicalAgentId(
            [
                CreateAssignment(firstManagerPartyId, "Run Process Manager", "Process manager"),
                CreateAssignment(secondManagerPartyId, "Process Manager", "Process manager")
            ],
            [
                new ProcessManagerAgentOption(
                    firstManagerPartyId,
                    Guid.NewGuid(),
                    "Run Process Manager",
                    "OpenAI",
                    "gpt-5-mini",
                    "Projected from AgentFramework organization catalog."),
                new ProcessManagerAgentOption(
                    secondManagerPartyId,
                    Guid.NewGuid(),
                    "Process Manager",
                    "OpenAI",
                    "gpt-5.4-mini",
                    "Projected from AgentFramework organization catalog.")
            ],
            []);

        Assert.Null(result);
    }

    [Fact]
    public void ArtifactContractValidation_rejects_response_text_as_runtime_evidence()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Evidence,
            "Regression evidence pack",
            isRequired: true,
            "Must include browser proof, screenshot, and test log output.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = expectation.Title,
            ManagedStoragePath = "artifacts/process-runs/current/regression-evidence.md",
            ExternalReferenceKey = $"assistant-response|{executionRunId:D}|artifacts/process-runs/current/regression-evidence.md",
            ReviewSummary = "Final assistant response text.",
            ProvenanceSummary = $"Projected from final assistant response for AgentFramework execution run {executionRunId:D}."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            executionRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.WrongProducerMode, result.Status);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AssistantResponse, result.ProducerKind);
    }

    [Fact]
    public void ArtifactContractValidation_rejects_placeholder_record_for_required_artifact()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "Implementation change set",
            isRequired: true,
            "Must identify concrete product files and validation evidence.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = expectation.Title,
            ManagedStoragePath = "artifacts/process-runs/current/implementation-change-set.md",
            ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|artifacts/process-runs/current/implementation-change-set.md",
            ReviewSummary = "Placeholder only; implementation artifact is not available.",
            ProvenanceSummary = "Missing artifact gap marker."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            executionRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.PlaceholderOnly, result.Status);
    }

    [Fact]
    public void ArtifactContractValidation_reports_missing_required_artifact_for_current_step()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Evidence,
            "Runtime validation evidence",
            isRequired: true,
            "Must include current run command output.");

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            executionRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Missing, result.Status);
        Assert.False(result.IsSatisfied);
    }

    [Fact]
    public void ProcessArtifactIdentityService_SB07_INV_001_normalizes_projection_identity_without_runtime()
    {
        var workflowRunId = Guid.NewGuid();
        var workflowArtifactId = Guid.NewGuid();
        var lineage = new ProcessArtifactProjectionLineage
        {
            SourceKind = ProcessArtifactProjectionSourceKind.WorkflowArtifact,
            WorkflowRunId = workflowRunId,
            WorkflowArtifactId = workflowArtifactId,
            SourceExternalReferenceKey = $"workflow-run:{workflowRunId:D}:artifact:{workflowArtifactId:D}",
            ContentHash = "sha256:content"
        };

        var normalized = ProcessArtifactIdentityService.NormalizeProjectionLineage(lineage);
        var hash = ProcessArtifactIdentityService.ComputeProjectionIdentityHash(lineage);
        var serialized = ProcessArtifactIdentityService.SerializeNormalizedProjectionLineage(normalized);

        Assert.NotNull(normalized);
        Assert.Equal(hash, normalized.ProjectionIdentityHash);
        Assert.Contains(hash, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessCompletionArtifactValidator_SB07_INV_001_accepts_generic_nonsoftware_deliverable_without_dispatch_runtime()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "Board meeting decision memo",
            isRequired: true,
            "Must be a Markdown memo with the approved budget decision.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = expectation.Title,
            ManagedStoragePath = "artifacts/process-runs/current/board-meeting-decision-memo.md",
            ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|artifacts/process-runs/current/board-meeting-decision-memo.md",
            ReviewSummary = "Approved operating budget decision memo.",
            ProvenanceSummary = $"Projected from governed process execution run {executionRunId:D}."
        };

        var result = ProcessRunAutomationDispatchService.ProcessCompletionArtifactValidator.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            executionRunId);

        Assert.True(result.IsSatisfied);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, result.Status);
    }

    [Fact]
    public void ArtifactContractValidation_SB07_INV_001_classifies_missing_required_artifact_as_own_output()
    {
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Evidence,
            "QA evidence pack",
            isRequired: true,
            "Must include runtime proof and defect findings.");

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            Guid.NewGuid(),
            Guid.NewGuid(),
            expectation,
            [],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Missing, result.Status);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactFailureOwnership.OwnOutput, result.FailureOwnership);
    }

    [Fact]
    public void ArtifactContractValidation_accepts_matching_workflow_artifact_for_process_expectation()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var workflowRunId = Guid.NewGuid();
        var workflowArtifactId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "Business plan",
            isRequired: true,
            "Write the business plan as Markdown.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Business plan",
            ManagedStoragePath = "workflow-output/business-plan.md",
            ExternalReferenceKey = $"workflow-run:{workflowRunId:D}:artifact:{workflowArtifactId:D}",
            ReviewSummary = "Workflow produced the business plan.",
            ProvenanceSummary = $"Produced by workflow run {workflowRunId:D}."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.WorkflowBackedRole,
            workflowRunId: workflowRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, result.Status);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact, result.ProducerKind);
    }

    [Fact]
    public void ArtifactContractValidation_does_not_treat_decision_log_as_runtime_proof()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Decision,
            "Legal decision log",
            isRequired: true,
            "Record the legal approval decision log and unavailable findings.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Decision,
            Title = expectation.Title,
            ManagedStoragePath = string.Empty,
            ExternalReferenceKey = $"process-step-decision:{stepRunId:D}:{expectation.Id:D}",
            ReviewSummary = "Decision log: records that one requested legal finding is not available from supplied records.",
            ProvenanceSummary = "Completed decision artifact."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, result.Status);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Decision, result.Mode);
    }

    [Fact]
    public void ArtifactContractValidation_accepts_todo_register_as_legitimate_deliverable()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "Operations TODO register",
            isRequired: true,
            "Create a TODO register with owners, dates, and follow-up actions.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = expectation.Title,
            ManagedStoragePath = "artifacts/process-runs/current/operations-todo-register.md",
            ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|artifacts/process-runs/current/operations-todo-register.md",
            ReviewSummary = "TODO register with concrete owners and dates.",
            ProvenanceSummary = $"Written by execution run {executionRunId:D}."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            executionRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, result.Status);
    }

    [Fact]
    public void ArtifactContractValidation_SB02_INV_008_accepts_markdown_evidence_pack_that_references_screenshot_paths()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Evidence,
            "Blazor runtime evidence pack",
            isRequired: true,
            "Must include dotnet build results, Playwright screenshot paths, browser_console_messages output, and visible behavior assertions.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = expectation.Title,
            ManagedStoragePath = "artifacts/process-runs/current/03-blazor-runtime-evidence-pack.md",
            ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|artifacts/process-runs/current/03-blazor-runtime-evidence-pack.md",
            ReviewSummary = "Markdown evidence pack with screenshot paths and browser console status.",
            ProvenanceSummary = $"Written by execution run {executionRunId:D}."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            executionRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, result.Status);
    }

    [Fact]
    public void ArtifactContractValidation_accepts_markdown_evidence_index_that_lists_screenshot_files()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Evidence,
            "Repaired run evidence index",
            isRequired: true,
            "Must include fresh screenshot files, console output, runtime proof, and validation evidence as a Markdown evidence index.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = expectation.Title,
            ManagedStoragePath = "artifacts/process-runs/current/07-repaired-run-evidence-index.md",
            ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|artifacts/process-runs/current/07-repaired-run-evidence-index.md",
            ReviewSummary = "Markdown evidence index that cites screenshot file paths and browser console status.",
            ProvenanceSummary = $"Written by execution run {executionRunId:D}."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            executionRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, result.Status);
    }

    [Fact]
    public void ArtifactContractValidation_SB02_INV_009_rejects_markdown_when_expectation_is_screenshot_artifact()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Evidence,
            "Browser screenshot",
            isRequired: true,
            "Must be captured as a screenshot artifact.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = expectation.Title,
            ManagedStoragePath = "artifacts/process-runs/current/browser-screenshot.md",
            ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|artifacts/process-runs/current/browser-screenshot.md",
            ReviewSummary = "Markdown file where an image screenshot artifact was required.",
            ProvenanceSummary = $"Written by execution run {executionRunId:D}."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            executionRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InvalidFormat, result.Status);
        Assert.Contains("not an image file", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArtifactContractValidation_SB04_INV_001_reads_catalog_backed_storage_reference()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"process-artifact-workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        try
        {
            var processRunId = Guid.NewGuid();
            var stepRunId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var storageId = Guid.NewGuid();
            var contentBytes = System.Text.Encoding.UTF8.GetBytes("{\"approved\":true}");
            var reference = new StorageObjectReference(
                storageId,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                "process-runs/current/finance-approval-packet.json",
                "finance-approval-packet.json",
                "application/json",
                contentBytes.Length);
            var expectation = CreateDispatchArtifactExpectation(
                ProcessArtifactKind.Deliverable,
                "Finance approval packet",
                isRequired: true,
                "Create the approval packet as JSON.");
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = processRunId,
                StepRunId = stepRunId,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = ProcessArtifactKind.Deliverable,
                Title = expectation.Title,
                ManagedStoragePath = StorageJson.SerializeReference(reference),
                ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|{reference.Locator}",
                ReviewSummary = "Finance approval packet.",
                ProvenanceSummary = $"Written by execution run {executionRunId:D}."
            };
            var storageCatalog = new TestStorageCatalogService(new StorageCatalogRecord
            {
                Id = storageId,
                Name = "Process artifact storage",
                ProviderKind = StorageProviderKind.FileSystem,
                IsEnabled = true,
                CapabilityMask = StorageCapability.Read
            });
            var storageDriver = new TestStorageDriver(StorageProviderKind.FileSystem, contentBytes);
            var reader = new ProcessRunAutomationDispatchService.StorageBackedProcessArtifactContentReader(
                new TestWorkspacePathResolver(workspaceRoot),
                storageCatalog,
                new TestStorageDriverRegistry(storageDriver));

            var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
                processRunId,
                stepRunId,
                expectation,
                [artifact],
                ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
                executionRunId,
                managedArtifactContentReader: reader);

            Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, result.Status);
            Assert.Equal(2, storageDriver.OpenReadCount);
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
    public void ArtifactContractValidation_rejects_malformed_json_file_when_json_is_required()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"process-artifact-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempFile, "{ not valid json");
        try
        {
            var processRunId = Guid.NewGuid();
            var stepRunId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var expectation = CreateDispatchArtifactExpectation(
                ProcessArtifactKind.Deliverable,
                "Finance approval packet",
                isRequired: true,
                "Create the approval packet as JSON.");
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = processRunId,
                StepRunId = stepRunId,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = ProcessArtifactKind.Deliverable,
                Title = expectation.Title,
                ManagedStoragePath = tempFile,
                ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|{tempFile}",
                ReviewSummary = "Finance approval packet.",
                ProvenanceSummary = $"Written by execution run {executionRunId:D}."
            };

            var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
                processRunId,
                stepRunId,
                expectation,
                [artifact],
                ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
                executionRunId);

            Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InvalidFormat, result.Status);
            Assert.Contains("malformed JSON", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ArtifactContractValidation_SB05_INV_001_rejects_malformed_json_from_relative_managed_storage_path()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"process-artifact-workspace-{Guid.NewGuid():N}");
        var relativePath = "artifacts/process-runs/current/finance-approval-packet.json";
        var fullPath = Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "{ not valid json");
        try
        {
            var processRunId = Guid.NewGuid();
            var stepRunId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var expectation = CreateDispatchArtifactExpectation(
                ProcessArtifactKind.Deliverable,
                "Finance approval packet",
                isRequired: true,
                "Create the approval packet as JSON.");
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = processRunId,
                StepRunId = stepRunId,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = ProcessArtifactKind.Deliverable,
                Title = expectation.Title,
                ManagedStoragePath = relativePath,
                ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|{relativePath}",
                ReviewSummary = "Finance approval packet.",
                ProvenanceSummary = $"Written by execution run {executionRunId:D}."
            };
            var reader = new ProcessRunAutomationDispatchService.WorkspaceProcessArtifactContentReader(
                new TestWorkspacePathResolver(workspaceRoot));

            var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
                processRunId,
                stepRunId,
                expectation,
                [artifact],
                ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
                executionRunId,
                managedArtifactContentReader: reader);

            Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InvalidFormat, result.Status);
            Assert.Contains("malformed JSON", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
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
    public void ArtifactContractValidation_SB05_INV_001_reports_missing_relative_managed_storage_content()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"process-artifact-workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        try
        {
            var processRunId = Guid.NewGuid();
            var stepRunId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var relativePath = "artifacts/process-runs/current/missing-runtime-proof.md";
            var expectation = CreateDispatchArtifactExpectation(
                ProcessArtifactKind.Evidence,
                "Runtime validation evidence",
                isRequired: true,
                "Must include runtime proof as Markdown.");
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = processRunId,
                StepRunId = stepRunId,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = ProcessArtifactKind.Evidence,
                Title = expectation.Title,
                ManagedStoragePath = relativePath,
                ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|{relativePath}",
                ReviewSummary = "Runtime proof.",
                ProvenanceSummary = $"Written by execution run {executionRunId:D}."
            };
            var reader = new ProcessRunAutomationDispatchService.WorkspaceProcessArtifactContentReader(
                new TestWorkspacePathResolver(workspaceRoot));

            var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
                processRunId,
                stepRunId,
                expectation,
                [artifact],
                ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
                executionRunId,
                managedArtifactContentReader: reader);

            Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentUnavailable, result.Status);
            Assert.Contains("could not be loaded", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not found", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
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
    public void ArtifactContractValidation_SB11_INV_001_reports_missing_required_brief_content()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"process-artifact-workspace-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        try
        {
            var processRunId = Guid.NewGuid();
            var stepRunId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var relativePath = "artifacts/process-runs/current/blazor-delivery-contract.md";
            var expectation = CreateDispatchArtifactExpectation(
                ProcessArtifactKind.Brief,
                "Blazor delivery contract",
                isRequired: true,
                "Artifact mode: Narrative. Required strict delivery contract must be content-backed as Markdown.");
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = processRunId,
                StepRunId = stepRunId,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = ProcessArtifactKind.Brief,
                Title = expectation.Title,
                ManagedStoragePath = relativePath,
                ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|{relativePath}",
                ReviewSummary = "Delivery contract should be stored in the managed artifact path.",
                ProvenanceSummary = $"Written by execution run {executionRunId:D}."
            };
            var reader = new ProcessRunAutomationDispatchService.WorkspaceProcessArtifactContentReader(
                new TestWorkspacePathResolver(workspaceRoot));

            var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
                processRunId,
                stepRunId,
                expectation,
                [artifact],
                ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
                executionRunId,
                managedArtifactContentReader: reader);

            Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Narrative, result.Mode);
            Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentUnavailable, result.Status);
            Assert.False(result.IsSatisfied);
            Assert.Contains("could not be loaded", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
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
    public void ArtifactContractValidation_SB05_INV_001_rejects_relative_managed_storage_content_over_validation_limit()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"process-artifact-workspace-{Guid.NewGuid():N}");
        var relativePath = "artifacts/process-runs/current/oversized-approval-packet.json";
        var fullPath = Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, new string('x', 1024 * 1024 + 1));
        try
        {
            var processRunId = Guid.NewGuid();
            var stepRunId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var expectation = CreateDispatchArtifactExpectation(
                ProcessArtifactKind.Deliverable,
                "Finance approval packet",
                isRequired: true,
                "Create the approval packet as JSON.");
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = processRunId,
                StepRunId = stepRunId,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = ProcessArtifactKind.Deliverable,
                Title = expectation.Title,
                ManagedStoragePath = relativePath,
                ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|{relativePath}",
                ReviewSummary = "Finance approval packet.",
                ProvenanceSummary = $"Written by execution run {executionRunId:D}."
            };
            var reader = new ProcessRunAutomationDispatchService.WorkspaceProcessArtifactContentReader(
                new TestWorkspacePathResolver(workspaceRoot));

            var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
                processRunId,
                stepRunId,
                expectation,
                [artifact],
                ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
                executionRunId,
                managedArtifactContentReader: reader);

            Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentUnavailable, result.Status);
            Assert.Contains("exceeding the validation limit", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
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
    public void ArtifactContractValidation_SB09_INV_001_accepts_current_run_org_scoped_path_with_matching_typed_lineage()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"process-artifact-workspace-{Guid.NewGuid():N}");
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var executionRunId = Guid.NewGuid();
        var scopeId = Guid.NewGuid();
        var relativePath = $"artifacts/scopes/organizations/{scopeId:D}/process-runs/{processRunId:D}/01-blazor-delivery-contract.md";
        var fullPath = Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "# Delivery contract\n\nCurrent run evidence.");
        try
        {
            var expectation = CreateDispatchArtifactExpectation(
                ProcessArtifactKind.Deliverable,
                "Blazor delivery contract",
                isRequired: true,
                "Must create the delivery contract as Markdown.");
            var externalReferencePath = $"artifacts/process-runs/{processRunId:D}/01-blazor-delivery-contract.md";
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = processRunId,
                StepRunId = stepRunId,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = ProcessArtifactKind.Deliverable,
                Title = expectation.Title,
                ManagedStoragePath = relativePath,
                ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|{externalReferencePath}",
                ProjectionLineageJson = ProcessArtifactProjectionLineageJson.Serialize(
                    new ProcessArtifactProjectionLineage
                    {
                        SourceKind = ProcessArtifactProjectionSourceKind.WorkspaceWrite,
                        SourceExecutionRunId = executionRunId,
                        ProjectedExecutionRunId = executionRunId,
                        SourceExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|{externalReferencePath}",
                        ContentHash = string.Empty
                    }),
                ReviewSummary = "Current run delivery contract.",
                ProvenanceSummary = $"Projected from current execution run {executionRunId:D}."
            };
            var reader = new ProcessRunAutomationDispatchService.WorkspaceProcessArtifactContentReader(
                new TestWorkspacePathResolver(workspaceRoot));

            var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
                processRunId,
                stepRunId,
                expectation,
                [artifact],
                ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
                executionRunId,
                managedArtifactContentReader: reader);

            Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, result.Status);
            Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkspaceWrite, result.ProducerKind);
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
    public void ArtifactContractValidation_SB10_INV_001_reports_content_hash_mismatch_without_stale_run_classification()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), $"process-artifact-workspace-{Guid.NewGuid():N}");
        var relativePath = "artifacts/process-runs/current/content-hash-mismatch.md";
        var fullPath = Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "# Current content");
        try
        {
            var processRunId = Guid.NewGuid();
            var stepRunId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var expectation = CreateDispatchArtifactExpectation(
                ProcessArtifactKind.Evidence,
                "Runtime validation evidence",
                isRequired: true,
                "Must include current run proof as Markdown.");
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = processRunId,
                StepRunId = stepRunId,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = ProcessArtifactKind.Evidence,
                Title = expectation.Title,
                ManagedStoragePath = relativePath,
                ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{expectation.Id:D}|{relativePath}",
                ProjectionLineageJson = ProcessArtifactProjectionLineageJson.Serialize(
                    new ProcessArtifactProjectionLineage
                    {
                        SourceKind = ProcessArtifactProjectionSourceKind.WorkspaceWrite,
                        SourceExecutionRunId = executionRunId,
                        ContentHash = ProcessArtifactIdentityService.ComputeContentHash(Encoding.UTF8.GetBytes("stale content"))
                    }),
                ReviewSummary = "Runtime proof.",
                ProvenanceSummary = $"Written by execution run {executionRunId:D}."
            };
            var reader = new ProcessRunAutomationDispatchService.WorkspaceProcessArtifactContentReader(
                new TestWorkspacePathResolver(workspaceRoot));

            var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
                processRunId,
                stepRunId,
                expectation,
                [artifact],
                ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
                executionRunId,
                managedArtifactContentReader: reader);

            Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.ContentHashMismatch, result.Status);
            Assert.Contains("content hash", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
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
    public void ArtifactContractValidation_rejects_workspace_artifact_from_wrong_execution_run()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var currentExecutionRunId = Guid.NewGuid();
        var staleExecutionRunId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "HR screening packet",
            isRequired: true,
            "Create a current-run HR screening deliverable.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = expectation.Title,
            ManagedStoragePath = "artifacts/process-runs/current/hr-screening.md",
            ExternalReferenceKey = $"workspace-written-artifact|{staleExecutionRunId:D}|{expectation.Id:D}|artifacts/process-runs/current/hr-screening.md",
            ReviewSummary = "Screening packet.",
            ProvenanceSummary = $"Written by stale execution run {staleExecutionRunId:D}."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            currentExecutionRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.StaleOrWrongRun, result.Status);
    }

    [Fact]
    public void ArtifactContractValidation_SB02_INV_001_accepts_manager_recovery_with_compact_key_and_typed_lineage()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var recoveryExecutionRunId = Guid.NewGuid();
        var recoveredForExecutionRunId = Guid.NewGuid();
        var projectedExecutionRunId = Guid.NewGuid();
        var sourceExternalReferenceKey = $"workspace-written-artifact|{projectedExecutionRunId:D}|{Guid.NewGuid():D}|{string.Join("/", Enumerable.Repeat("nested-artifact-segment", 12))}/implementation-change-set.md";
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "Recovered implementation change set",
            isRequired: true,
            "Must identify concrete product files and validation evidence.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = expectation.Title,
            ManagedStoragePath = "artifacts/process-runs/current/recovered-implementation-change-set.md",
            ExternalReferenceKey = "manager-recovery-artifact|sha256:0123456789abcdef0123456789abcdef",
            ProjectionLineageJson = ProcessArtifactProjectionLineageJson.Serialize(
                new ProcessArtifactProjectionLineage
                {
                    SourceKind = ProcessArtifactProjectionSourceKind.WorkspaceWrite,
                    SourceExecutionRunId = projectedExecutionRunId,
                    RecoveryExecutionRunId = recoveryExecutionRunId,
                    RecoveredForExecutionRunId = recoveredForExecutionRunId,
                    ProjectedExecutionRunId = projectedExecutionRunId,
                    SourceExternalReferenceKey = sourceExternalReferenceKey
                }),
            ReviewSummary = "Recovered implementation change set.",
            ProvenanceSummary = "Manager recovery artifact with structured lineage."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            executionRunId: projectedExecutionRunId,
            recoveryExecutionRunId: recoveryExecutionRunId,
            recoveredForExecutionRunId: recoveredForExecutionRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, result.Status);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ManagerRecovery, result.ProducerKind);
        Assert.True(result.IsSatisfied);
    }

    [Fact]
    public void ArtifactContractValidation_accepts_subprocess_artifact_with_current_child_lineage()
    {
        var processRunId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var subprocessRunId = Guid.NewGuid();
        var sourceArtifactId = Guid.NewGuid();
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "Operations incident review",
            isRequired: true,
            "Create the incident review deliverable.");
        var artifact = new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = expectation.Title,
            ManagedStoragePath = "artifacts/subprocess/incident-review.md",
            ExternalReferenceKey = $"subprocess-run:{subprocessRunId:D}:artifact:{sourceArtifactId:D}",
            ReviewSummary = "Projected from child incident review artifact.",
            ProvenanceSummary = $"Auto-projected from completed subprocess run {subprocessRunId:D}."
        };

        var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            processRunId,
            stepRunId,
            expectation,
            [artifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.SubprocessParent,
            subprocessRunId: subprocessRunId);

        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, result.Status);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.SubprocessArtifact, result.ProducerKind);
    }

    [Fact]
    public void DispatchSource_routes_direct_and_workflow_completion_through_process_owned_finalizer()
    {
        var dispatchSource = File.ReadAllText(Path.Combine(
            IntegrationTestPaths.RepositoryRoot,
            "src",
            "CanDoItAll.Modules.Processes",
            "Automation",
            "Dispatch",
            "ProcessRunAutomationDispatchService.Dispatch.cs"));

        Assert.Contains("FinalizeStepCompletionAsync", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("ProcessStepCompletionExecutorKind.DirectAgent", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("ProcessStepCompletionExecutorKind.WorkflowBackedRole", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("ProcessStepCompletionExecutorKind.SubprocessParent", dispatchSource, StringComparison.Ordinal);
        Assert.Contains("ProcessStepCompletionExecutorKind.ManagerArtifactRecovery", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetStatus = workflowOutcome.CompletionStatus", dispatchSource, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceArtifact?.ManagedStoragePath ?? string.Empty", dispatchSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessRedTeamScenarioHarness_SB14_INV_001_blocks_architecture_mutation_and_allows_external_destination()
    {
        var architectureCandidate = (ProcessRunAutomationDispatchService.DispatchCandidate)CreateDispatchCandidateCore(
            "Review the target architecture and produce an ADR. Do not modify product files.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Architecture decision record", true, "Must describe architecture decisions and risks as Markdown.")],
            [],
            triggerReason: "Architecture-only software planning review.",
            stepTitle: "Review architecture only",
            processName: "Software architecture review",
            outputContractSummary: "Architecture-only planning artifact.");
        architectureCandidate.StepDefinition.AllowedOperations =
        [
            ProcessStepOperation.ReadProcessContext,
            ProcessStepOperation.WriteManagedProcessArtifacts
        ];
        architectureCandidate.StepDefinition.OperationTargetScope = ProcessStepTargetScope.ManagedProcessArtifactsOnly;

        var architectureMetadataJson = ProcessRunAutomationDispatchService.ProcessInvocationMetadataBuilder.Build(
            architectureCandidate,
            new ExecutionInvocationPolicy(),
            "Read-only product target: external-target/C/programovani/payments-service",
            null);

        using (var architectureDocument = JsonDocument.Parse(architectureMetadataJson))
        {
            Assert.False(architectureDocument.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey).GetBoolean());
            var operations = architectureDocument.RootElement
                .GetProperty(ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey)
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray();
            Assert.DoesNotContain(ProcessStepOperation.MutateProductTarget.ToString(), operations);
            Assert.Contains(ProcessStepOperation.WriteManagedProcessArtifacts.ToString(), operations);
        }

        var mutationDecision = await new DefaultAgentToolInvocationPolicy().EvaluateAsync(
            new ToolInvocationPolicyContext(
                Guid.NewGuid(),
                "Architecture reviewer",
                "workspace_write_file",
                new Dictionary<string, string>
                {
                    ["path"] = "external-target/C/programovani/payments-service/src/Program.cs",
                    ["content"] = "Console.WriteLine(\"mutated from planning step\");"
                },
                ToolInvocationClassification.Mutation,
                IsKnownTool: true,
                AutoApprovalAllowed: true,
                ApprovalWrapperAvailable: false,
                ExecutionRunId: Guid.NewGuid().ToString("D"),
                SourceKind: "process-step",
                ProcessRunId: Guid.NewGuid().ToString("D"),
                ProcessStepId: Guid.NewGuid().ToString("D"),
                AllowedExternalTargetAliases: [],
                ReadOnlyExternalTargetAliases: ["external-target/C/programovani/payments-service"]),
            CancellationToken.None);

        Assert.Equal(ToolInvocationDecisionKind.Deny, mutationDecision.Kind);

        var businessPlanCandidate = (ProcessRunAutomationDispatchService.DispatchCandidate)CreateDispatchCandidateCore(
            "Create the market expansion business plan and write it to the governed report destination.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Deliverable, "Business plan report", true, "Must include market assumptions, budget, owners, and approval criteria.")],
            [],
            triggerReason: "Prepare a business plan report for executive review.",
            stepTitle: "Create business plan report",
            processName: "Business planning workflow",
            outputContractSummary: "Business report artifact.");
        businessPlanCandidate.StepDefinition.AllowedOperations =
        [
            ProcessStepOperation.WriteManagedProcessArtifacts,
            ProcessStepOperation.WriteExternalArtifactDestination
        ];
        businessPlanCandidate.StepDefinition.OperationTargetScope = ProcessStepTargetScope.ExternalArtifactDestination;
        const string externalDestinationGrounding = """
            Dispatcher fetched the live project structure for `MarketPlan`.
            Grounded external target paths from the selected project structure:
            - `C:\business\market-plan\reports` mapped to `external-target/C/business/market-plan/reports` from report destination note (custom:report-destination)
            """;

        var businessPlanMetadataJson = ProcessRunAutomationDispatchService.ProcessInvocationMetadataBuilder.Build(
            businessPlanCandidate,
            new ExecutionInvocationPolicy(),
            externalDestinationGrounding,
            null);

        using var businessPlanDocument = JsonDocument.Parse(businessPlanMetadataJson);
        Assert.Equal(
            ProcessStepTargetScope.ExternalArtifactDestination.ToString(),
            businessPlanDocument.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey).GetString());
        Assert.False(businessPlanDocument.RootElement.GetProperty(ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey).GetBoolean());
        var allowedAliases = businessPlanDocument.RootElement
            .GetProperty(ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey)
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();
        Assert.Contains("external-target/C/business/market-plan/reports", allowedAliases);
    }

    [Fact]
    public void ProcessRedTeamScenarioHarness_SB14_INV_001_validates_generic_artifact_producers_and_recovery_actions()
    {
        var legalResult = ValidateDirectArtifact(
            ProcessArtifactKind.Decision,
            "Legal approval decision log",
            "Record the legal approval decision, approver, and unavailable findings.",
            string.Empty,
            "process-step-decision",
            "Legal approved with one unavailable finding recorded.",
            artifactExpectationMode: ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Decision);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, legalResult.Status);

        var manufacturingResult = ValidateDirectArtifact(
            ProcessArtifactKind.Evidence,
            "Manufacturing QA inspection record",
            "Must include manufacturing QA inspection results as Markdown.",
            "artifacts/process-runs/current/manufacturing-qa-inspection.md",
            "workspace-written-artifact",
            "QA inspection passed after torque and visual checks.");
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied, manufacturingResult.Status);

        var workflowRunId = Guid.NewGuid();
        var workflowArtifactId = Guid.NewGuid();
        var workflowExpectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "Workflow-backed business plan",
            isRequired: true,
            "Write the business plan as Markdown.");
        var workflowArtifact = new ProcessArtifactRecord
        {
            ProcessRunId = Guid.NewGuid(),
            StepRunId = Guid.NewGuid(),
            ArtifactExpectationId = workflowExpectation.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = workflowExpectation.Title,
            ManagedStoragePath = "workflow-output/business-plan.md",
            ExternalReferenceKey = $"workflow-run:{workflowRunId:D}:artifact:{workflowArtifactId:D}",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ReviewSummary = "Workflow produced the business plan.",
            ProvenanceSummary = $"Produced by workflow run {workflowRunId:D}."
        };
        var workflowResult = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            workflowArtifact.ProcessRunId,
            workflowArtifact.StepRunId!.Value,
            workflowExpectation,
            [workflowArtifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.WorkflowBackedRole,
            workflowRunId: workflowRunId);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.WorkflowArtifact, workflowResult.ProducerKind);
        Assert.True(workflowResult.IsSatisfied);

        var subprocessRunId = Guid.NewGuid();
        var subprocessArtifactId = Guid.NewGuid();
        var subprocessExpectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "Incident response parent review",
            isRequired: true,
            "Create the parent incident response review from the subprocess artifact.");
        var subprocessArtifact = new ProcessArtifactRecord
        {
            ProcessRunId = Guid.NewGuid(),
            StepRunId = Guid.NewGuid(),
            ArtifactExpectationId = subprocessExpectation.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = subprocessExpectation.Title,
            ManagedStoragePath = "artifacts/subprocess/incident-response-review.md",
            ExternalReferenceKey = $"subprocess-run:{subprocessRunId:D}:artifact:{subprocessArtifactId:D}",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ReviewSummary = "Projected from child incident response artifact.",
            ProvenanceSummary = $"Auto-projected from completed subprocess run {subprocessRunId:D}."
        };
        var subprocessResult = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            subprocessArtifact.ProcessRunId,
            subprocessArtifact.StepRunId!.Value,
            subprocessExpectation,
            [subprocessArtifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.SubprocessParent,
            subprocessRunId: subprocessRunId);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.SubprocessArtifact, subprocessResult.ProducerKind);
        Assert.True(subprocessResult.IsSatisfied);

        var recoveryExecutionRunId = Guid.NewGuid();
        var recoveredForExecutionRunId = Guid.NewGuid();
        var projectedExecutionRunId = Guid.NewGuid();
        var managerExpectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Deliverable,
            "Manager recovered QA packet",
            isRequired: true,
            "Recover the failed QA packet with structured lineage.");
        var managerArtifact = new ProcessArtifactRecord
        {
            ProcessRunId = Guid.NewGuid(),
            StepRunId = Guid.NewGuid(),
            ArtifactExpectationId = managerExpectation.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = managerExpectation.Title,
            ManagedStoragePath = "artifacts/process-runs/current/manager-recovered-qa-packet.md",
            ExternalReferenceKey = "manager-recovery-artifact|sha256:sb14",
            ProjectionLineageJson = ProcessArtifactProjectionLineageJson.Serialize(
                new ProcessArtifactProjectionLineage
                {
                    SourceKind = ProcessArtifactProjectionSourceKind.WorkspaceWrite,
                    SourceExecutionRunId = projectedExecutionRunId,
                    RecoveryExecutionRunId = recoveryExecutionRunId,
                    RecoveredForExecutionRunId = recoveredForExecutionRunId,
                    ProjectedExecutionRunId = projectedExecutionRunId,
                    SourceExternalReferenceKey = $"workspace-written-artifact|{projectedExecutionRunId:D}|{managerExpectation.Id:D}|manager-recovered-qa-packet.md"
                }),
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ReviewSummary = "Manager recovery artifact with structured lineage.",
            ProvenanceSummary = "Manager recovery artifact with structured lineage."
        };
        var managerResult = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            managerArtifact.ProcessRunId,
            managerArtifact.StepRunId!.Value,
            managerExpectation,
            [managerArtifact],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            executionRunId: projectedExecutionRunId,
            recoveryExecutionRunId: recoveryExecutionRunId,
            recoveredForExecutionRunId: recoveredForExecutionRunId);
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.ManagerRecovery, managerResult.ProducerKind);
        Assert.True(managerResult.IsSatisfied);

        var missingRequired = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateDispatchArtifactExpectation(ProcessArtifactKind.Evidence, "Runtime proof", true, "Must include current run proof as Markdown."),
            [],
            ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            Guid.NewGuid());
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Missing, missingRequired.Status);
        Assert.False(missingRequired.IsSatisfied);

        var placeholderResult = ValidateDirectArtifact(
            ProcessArtifactKind.Deliverable,
            "Implementation change set",
            "Must identify concrete product files and validation evidence.",
            "artifacts/process-runs/current/implementation-change-set.md",
            "workspace-written-artifact",
            "Placeholder only; implementation artifact is not available.",
            provenanceSummary: "Missing artifact gap marker.");
        Assert.Equal(ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.PlaceholderOnly, placeholderResult.Status);

        var ownOutputRoute = ProcessRecoveryRouter.Route(
            new ProcessRecoveryRoutingRequest(
                ProcessStepBlockReasonCode.ArtifactContractUnsatisfied,
                ProcessStepBlockCause.OwnOutput,
                "Required artifact contract validation failed: Manufacturing QA inspection record: Missing.",
                [ProcessStepRecoveryOption.RecoverArtifactsOnly, ProcessStepRecoveryOption.HumanEscalation],
                [],
                string.Empty,
                HasNewEvidence: false));
        Assert.Equal(ProcessStepRecoveryOption.RecoverArtifactsOnly, ownOutputRoute.NextAction);

        var upstreamRoute = ProcessRecoveryRouter.Route(
            new ProcessRecoveryRoutingRequest(
                ProcessStepBlockReasonCode.MissingUpstreamArtifact,
                ProcessStepBlockCause.UpstreamInput,
                "Required upstream incident packet was not materialized.",
                [ProcessStepRecoveryOption.WaitForArtifactMaterialization, ProcessStepRecoveryOption.RecoverArtifactsOnly],
                [],
                string.Empty,
                HasNewEvidence: false));
        Assert.Equal(ProcessStepRecoveryOption.WaitForArtifactMaterialization, upstreamRoute.NextAction);

        static ProcessRunAutomationDispatchService.ProcessArtifactExpectationValidationResult ValidateDirectArtifact(
            ProcessArtifactKind artifactKind,
            string title,
            string validationRequirement,
            string managedStoragePath,
            string externalReferencePrefix,
            string reviewSummary,
            string provenanceSummary = "Produced by the current process execution.",
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode? artifactExpectationMode = null)
        {
            var processRunId = Guid.NewGuid();
            var stepRunId = Guid.NewGuid();
            var executionRunId = Guid.NewGuid();
            var expectation = CreateDispatchArtifactExpectation(
                artifactKind,
                title,
                isRequired: true,
                validationRequirement);
            var externalReferenceKey = externalReferencePrefix == "process-step-decision"
                ? $"process-step-decision:{stepRunId:D}:{expectation.Id:D}"
                : $"{externalReferencePrefix}|{executionRunId:D}|{expectation.Id:D}|{managedStoragePath}";
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = processRunId,
                StepRunId = stepRunId,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = artifactKind,
                Title = title,
                ManagedStoragePath = managedStoragePath,
                ExternalReferenceKey = externalReferenceKey,
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ProcessSensitivityLevel.Internal,
                ReviewSummary = reviewSummary,
                ProvenanceSummary = provenanceSummary
            };

            var result = ProcessRunAutomationDispatchService.ValidateArtifactExpectationForRecordedArtifacts(
                processRunId,
                stepRunId,
                expectation,
                [artifact],
                ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
                executionRunId);
            if (artifactExpectationMode is not null)
            {
                Assert.Equal(artifactExpectationMode, result.Mode);
            }

            return result;
        }
    }

    [Fact]
    public void ProcessStepRunBlockState_SB05_INV_001_maps_own_missing_required_artifact_to_artifact_contract_recovery()
    {
        var stepRun = new ProcessStepRun();

        ProcessStepRunBlockState.Apply(
            stepRun,
            "Required artifact contract validation failed: Delivery readiness evidence: Missing.",
            ProcessStepBlockCause.OwnOutput);
        var recoveryOptions = ProcessStepRunBlockState.ResolveRecoveryOptions(stepRun);

        Assert.Equal(ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, stepRun.BlockReasonCode);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, recoveryOptions);
        Assert.DoesNotContain(ProcessStepRecoveryOption.WaitForArtifactMaterialization, recoveryOptions);
    }

    [Fact]
    public void ProcessStepRunBlockState_SB05_INV_002_maps_upstream_missing_artifact_to_materialization_recovery()
    {
        var stepRun = new ProcessStepRun();

        ProcessStepRunBlockState.Apply(
            stepRun,
            "Required upstream artifacts are missing and the source step must provide required artifact input.",
            ProcessStepBlockCause.UpstreamInput);
        var recoveryOptions = ProcessStepRunBlockState.ResolveRecoveryOptions(stepRun);

        Assert.Equal(ProcessStepBlockReasonCode.MissingUpstreamArtifact, stepRun.BlockReasonCode);
        Assert.Contains(ProcessStepRecoveryOption.WaitForArtifactMaterialization, recoveryOptions);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, recoveryOptions);
    }

    [Fact]
    public void ProcessStepRunBlockState_SB05_INV_003_does_not_infer_own_required_artifact_as_upstream()
    {
        var code = ProcessStepRunBlockState.InferBlockReasonCode("missing required artifact: Delivery readiness evidence");

        Assert.Equal(ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, code);
    }

    [Fact]
    public void ProcessBlockStateClassifier_SB12_INV_001_prefers_typed_block_cause_over_prose_inference()
    {
        var classification = ProcessBlockStateClassifier.Classify(
            "Required upstream artifacts are missing in the diagnostic text, but the missing output belongs to this step.",
            ProcessStepBlockCause.OwnOutput);

        Assert.Equal(ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, classification.ReasonCode);
        Assert.Equal(ProcessStepBlockCause.OwnOutput, classification.BlockCause);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, classification.RecoveryOptions);
        Assert.DoesNotContain(ProcessStepRecoveryOption.WaitForArtifactMaterialization, classification.RecoveryOptions);
    }

    [Fact]
    public void ProcessStepRunBlockState_SB12_INV_001_uses_legacy_text_inference_only_when_block_cause_is_absent()
    {
        var ownOutputStep = new ProcessStepRun();
        var ownOutputDecision = ProcessStepRunBlockState.Apply(
            ownOutputStep,
            "missing required artifact: Delivery readiness evidence");
        var ownOutputRecoveryOptions = ProcessStepRunBlockState.ResolveRecoveryOptions(ownOutputStep);

        Assert.Equal(ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, ownOutputStep.BlockReasonCode);
        Assert.Equal(ProcessStepBlockCause.OwnOutput, ownOutputDecision.FailureOwnership);
        Assert.Equal(ProcessStepRecoveryOption.RecoverArtifactsOnly, ownOutputDecision.NextAction);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, ownOutputRecoveryOptions);
        Assert.DoesNotContain(ProcessStepRecoveryOption.WaitForArtifactMaterialization, ownOutputRecoveryOptions);

        var upstreamStep = new ProcessStepRun();
        var upstreamDecision = ProcessStepRunBlockState.Apply(
            upstreamStep,
            "Required upstream artifacts are missing and the source step must provide required artifact input.");
        var upstreamRecoveryOptions = ProcessStepRunBlockState.ResolveRecoveryOptions(upstreamStep);

        Assert.Equal(ProcessStepBlockReasonCode.MissingUpstreamArtifact, upstreamStep.BlockReasonCode);
        Assert.Equal(ProcessStepBlockCause.UpstreamInput, upstreamDecision.FailureOwnership);
        Assert.Equal(ProcessStepRecoveryOption.WaitForArtifactMaterialization, upstreamDecision.NextAction);
        Assert.Contains(ProcessStepRecoveryOption.WaitForArtifactMaterialization, upstreamRecoveryOptions);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, upstreamRecoveryOptions);
    }

    [Fact]
    public void ResolveArtifactContractBlockCause_SB05_INV_001_prefers_upstream_ownership_when_present()
    {
        var results = new[]
        {
            new ProcessRunAutomationDispatchService.ProcessArtifactExpectationValidationResult(
                Guid.NewGuid(),
                "Upstream evidence",
                ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Evidence,
                ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Missing,
                ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Unknown,
                null,
                string.Empty,
                "The upstream evidence was not materialized.",
                "Materialize upstream artifact.",
                "upstream-proof",
                ProcessRunAutomationDispatchService.ProcessArtifactFailureOwnership.UpstreamInput),
            new ProcessRunAutomationDispatchService.ProcessArtifactExpectationValidationResult(
                Guid.NewGuid(),
                "Own evidence",
                ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Evidence,
                ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Missing,
                ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Unknown,
                null,
                string.Empty,
                "The own output evidence was not recorded.",
                "Recover own output.",
                "own-proof",
                ProcessRunAutomationDispatchService.ProcessArtifactFailureOwnership.OwnOutput)
        };

        var cause = ProcessRunAutomationDispatchService.ResolveArtifactContractBlockCause(results);

        Assert.Equal(ProcessStepBlockCause.UpstreamInput, cause);
    }

    [Fact]
    public void ArtifactDispositionRouter_SB07_INV_001_blocks_missing_own_required_artifact_even_with_negative_branch()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("ResolveArtifactContractDispositionBranchOutcome", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveArtifactContractDispositionBranchOutcome method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "QA review records a disposition decision and routes discovered product defects to implementation repair.",
            ProcessStepKind.Review,
            [("no_go", "No-go", "Route blocking product defects to no-go.")],
            true,
            [
                (ProcessArtifactKind.Decision, "QA disposition decision", true, "Required decision artifact for the governed review disposition."),
                (ProcessArtifactKind.Evidence, "QA evidence pack", true, "Must include browser proof and defect findings.")
            ],
            [],
            stepTitle: "QA validation",
            recordedArtifactTitles: ["QA disposition decision"]);
        var evidenceExpectation = ResolveDispatchArtifactExpectation(candidate, "QA evidence pack");
        var failure = new ProcessRunAutomationDispatchService.ProcessArtifactExpectationValidationResult(
            evidenceExpectation.Id,
            evidenceExpectation.Title,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RuntimeProof,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Missing,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.Unknown,
            ArtifactRecordId: null,
            AttemptedPath: string.Empty,
            Diagnostic: "No current step artifact record matches the required expectation.",
            SuggestedAction: "Recover or block with the exact missing artifact.",
            Fingerprint: "fingerprint");

        var routedOutcome = method.Invoke(null, [candidate, new[] { failure }]);

        Assert.Null(routedOutcome);
    }

    [Fact]
    public void ArtifactDispositionRouter_routes_review_disposition_failure_to_repair_branch_when_decision_artifact_exists()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("ResolveArtifactContractDispositionBranchOutcome", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveArtifactContractDispositionBranchOutcome method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "QA review records defect findings and routes implementation repair.",
            ProcessStepKind.Review,
            [("repair_required", "Repair required", "Route product defects to repair.")],
            true,
            [
                (ProcessArtifactKind.Decision, "QA disposition decision", true, "Required decision artifact for the governed review disposition."),
                (ProcessArtifactKind.Evidence, "QA evidence", true, "Must include browser proof and defect findings.")
            ],
            [],
            stepTitle: "QA validation",
            recordedArtifactTitles: ["QA disposition decision"]);
        var expectation = ResolveDispatchArtifactExpectation(candidate, "QA evidence");
        var failure = new ProcessRunAutomationDispatchService.ProcessArtifactExpectationValidationResult(
            expectation.Id,
            expectation.Title,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RuntimeProof,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InsufficientEvidence,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AssistantResponse,
            ArtifactRecordId: null,
            AttemptedPath: string.Empty,
            Diagnostic: "Browser proof evidence is insufficient.",
            SuggestedAction: "Route repair or recover evidence.",
            Fingerprint: "fingerprint",
            FailureOwnership: ProcessRunAutomationDispatchService.ProcessArtifactFailureOwnership.ReviewDisposition);

        var routedOutcome = method.Invoke(null, [candidate, new[] { failure }])
            ?? throw new InvalidOperationException("Disposition route was not resolved.");
        var routedOutcomeId = (Guid)(routedOutcome.GetType().GetProperty("Id")?.GetValue(routedOutcome)
            ?? throw new InvalidOperationException("DispatchBranchOutcome.Id was not available."));

        Assert.Equal(ResolveBranchOutcomeId(candidate, "repair_required"), routedOutcomeId);
    }

    [Fact]
    public void ArtifactDispositionRouter_keeps_missing_upstream_input_blocked()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("ResolveArtifactContractDispositionBranchOutcome", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveArtifactContractDispositionBranchOutcome method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "QA review records defect findings and routes implementation repair.",
            ProcessStepKind.Review,
            [("repair_required", "Repair required", "Route product defects to repair.")],
            true,
            [(ProcessArtifactKind.Evidence, "QA evidence", true, "Must include browser proof and defect findings.")],
            [("Implement feature", "Implementation change set", [])]);
        var expectation = CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Evidence,
            "QA evidence",
            isRequired: true,
            "Must include browser proof and defect findings.");
        var failure = new ProcessRunAutomationDispatchService.ProcessArtifactExpectationValidationResult(
            expectation.Id,
            expectation.Title,
            ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RuntimeProof,
            ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.InsufficientEvidence,
            ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AssistantResponse,
            ArtifactRecordId: null,
            AttemptedPath: string.Empty,
            Diagnostic: "Browser proof evidence is insufficient.",
            SuggestedAction: "Route repair or recover evidence.",
            Fingerprint: "fingerprint");

        var routedOutcome = method.Invoke(null, [candidate, new[] { failure }]);

        Assert.Null(routedOutcome);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
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
            "Run QA validation and browser proof for the .NET workflow app.",
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
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
    public void BuildRecoveryDirective_guides_javascript_browser_proof_retry_to_non_dotnet_helper()
    {
        var buildRecoveryDirective = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildRecoveryDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildRecoveryDirective method was not found.");
        var candidate = CreateProjectStructureDispatchCandidate(
            "Run QA validation and browser proof for the JavaScript browser app.",
            new ProcessProjectStructureContext
            {
                ProjectId = Guid.NewGuid(),
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery and release governance",
                ParentNodeId = "task:create-javascript-application",
                ParentNodeTitle = "Create JavaScript application"
            },
            ProcessStepKind.Review);
        var now = DateTimeOffset.UtcNow;
        const string responseText = "QA validation and browser proof cannot proceed because the JavaScript application is not running and no screenshots can be captured.";
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
                1
            ]) as string;

        Assert.NotNull(directive);
        Assert.Contains("Do not call `workspace_dotnet_build`, `workspace_dotnet_test`, or `workspace_dotnet_run` for JavaScript or TypeScript deliverables", directive, StringComparison.Ordinal);
        Assert.Contains("first write a helper script", directive, StringComparison.Ordinal);
        Assert.Contains("workspace_pwsh_run_script", directive, StringComparison.Ordinal);
        Assert.Contains("Convert that alias to a native path inside the controlled helper script", directive, StringComparison.Ordinal);
        Assert.Contains("Never call native PowerShell or process APIs with `external-target/...` directly", directive, StringComparison.Ordinal);
        Assert.Contains("npm.cmd", directive, StringComparison.Ordinal);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
                    new ProcessAutomationToolExecutionReceipt(
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
        Assert.Contains("only inspected markdown", directive, StringComparison.Ordinal);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
    public void ResolveCompletionStatus_infers_explicit_repair_branch_from_disposition_summary()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var resolveSelectedBranchOutcomeId = ResolveSelectedBranchOutcomeIdMethod(serviceType);
        var candidate = CreateDispatchCandidateWithBranchOutcomes(
            "Review the implementation and route the next step honestly.",
            true,
            ("quality-accepted", "Quality accepted", "Continue to result writeback."),
            ("repair-required", "Repair required", "Route back to implementation repair."));
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Validation artifacts were written and the review disposition is in the evidence summary.",
            summaryMarkdown: """
            ## Validation self-review summary

            Acceptance decision:
            - Status: repair-required
            - Reason: Browser interaction proof found a broken hard-drop keyboard binding.
            """);
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                responseText,
                "OpenAI chat completions",
                "gpt-4.1",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                BuildSerializedSessionState(
                    ("workspace_write_file", CreateProviderNativeTextResult("Validation artifacts written.")),
                    ("browser_take_screenshot", CreateProviderNativeTextResult("Screenshot saved.")),
                    ("browser_console_messages", CreateProviderNativeTextResult("Console inspected."))),
                []),
            null,
            [],
            []);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var selectedBranchOutcomeId = (Guid?)resolveSelectedBranchOutcomeId.Invoke(null, [candidate, ProcessStepRunStatus.Completed, responseText]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.Equal(ResolveBranchOutcomeId(candidate, "repair-required"), selectedBranchOutcomeId);
    }

    [Fact]
    public void ResolveCompletionStatusWithCarryForward_allows_explicit_repair_disposition_despite_failed_diagnostic_tool()
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
        var resolveMissingTools = serviceType.GetMethod("ResolveMissingRequiredToolExecutionsWithCarryForward", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredToolExecutionsWithCarryForward method was not found.");
        var resolveMissingArtifact = serviceType.GetMethod("ResolveMissingRequiredArtifactSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveMissingRequiredArtifactSummary method was not found.");
        var resolveInvalidBrowserProof = serviceType.GetMethod("ResolveInvalidBrowserProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveInvalidBrowserProofSummary method was not found.");
        var resolveInvalidQualityProof = serviceType.GetMethod("ResolveInvalidQualityValidationProofSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveInvalidQualityValidationProofSummary method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for the generated Blazor app, then select quality-accepted or repair-required.",
            ProcessStepKind.Review,
            [
                ("quality-accepted", "Quality accepted", "Continue to result writeback."),
                ("repair-required", "Repair required", "Route back to implementation repair.")
            ],
            true,
            [
                (ProcessArtifactKind.Evidence, "Blazor runtime evidence pack", true, "Must include screenshots, browser console, and visible behavior assertions."),
                (ProcessArtifactKind.Transcript, "Validation self-review summary", true, "Must state accepted or repair-required disposition.")
            ],
            [],
            stepTitle: "Validate Blazor runtime and browser evidence",
            outputContractSummary: "Browser validation disposition");
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Runtime validation found a repairable form binding failure.",
            branchOutcomeKey: "repair-required",
            summaryMarkdown: """
            ## Blazor runtime evidence pack
            Browser URL: http://127.0.0.1:5305/
            Screenshot: artifacts/process-runs/run-1/browser/mobile.png
            Console result: EditForm requires either a Model parameter, or an EditContext parameter.
            Visible behavior assertion: the page renders the error UI instead of the pantry planner.

            ## Validation self-review summary
            Acceptance decision:
            - Status: repair-required
            - Reason: Browser runtime proof found a repairable EditForm binding defect.
            """);
        var now = DateTimeOffset.UtcNow;
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                ("browser_take_screenshot", new Dictionary<string, object?> { ["filename"] = ".playwright-mcp/page-repair-proof.png" }, CreateProviderNativeTextResult("Screenshot captured.")),
                ("browser_snapshot", new Dictionary<string, object?> { ["filename"] = ".playwright-mcp/page-repair-proof.yml" }, CreateProviderNativeTextResult("Snapshot captured.")),
                ("browser_console_messages", new Dictionary<string, object?> { ["filename"] = ".playwright-mcp/console-repair-proof.log" }, CreateProviderNativeTextResult("Error: EditForm requires either a Model parameter, or an EditContext parameter.")),
                ("workspace_stat_path", (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), CreateProviderNativeTextResult("Evidence artifact path exists.")),
                ("workspace_read_file", (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), CreateProviderNativeTextResult("Evidence artifact reviewed.")),
                ("workspace_write_file", (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>(), CreateProviderNativeTextResult("Evidence artifacts written."))),
            [
                CreateToolReceipt("browser", "browser_take_screenshot", "http://127.0.0.1:5305/", ".", "Succeeded", now),
                CreateToolReceipt("browser", "browser_snapshot", "http://127.0.0.1:5305/", ".", "Succeeded", now.AddSeconds(1)),
                CreateToolReceipt("browser", "browser_console_messages", "http://127.0.0.1:5305/", ".", "Succeeded", now.AddSeconds(2)),
                CreateToolReceipt("workspace-file", "workspace_write_file", "artifacts/process-runs/run-1/03-blazor-runtime-evidence-pack.md", ".", "Succeeded", now.AddSeconds(3)),
                CreateToolReceipt("workspace-file", "workspace_write_file", "artifacts/process-runs/run-1/03-validation-self-review-summary.md", ".", "Succeeded", now.AddSeconds(4)),
                CreateToolReceipt("workspace-file", "workspace_stat_path", "artifacts/process-runs/run-1/03-blazor-runtime-evidence-pack.md", ".", "Succeeded", now.AddSeconds(5)),
                CreateToolReceipt("workspace-file", "workspace_read_file", "artifacts/process-runs/run-1/03-validation-self-review-summary.md", ".", "Succeeded", now.AddSeconds(6)),
                CreateToolReceipt("workspace-process", "workspace_pwsh_run_script", "read locked browser host stderr log", ".", "Failed: The process cannot access the file because it is being used by another process.", now.AddSeconds(7))
            ]);

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail, Array.Empty<string>(), responseText]);
        var reason = buildCompletionReason.Invoke(
            null,
            [candidate, detail, "Validate Blazor runtime and browser evidence", Array.Empty<string>(), responseText]) as string;
        var missingTools = resolveMissingTools.Invoke(null, [candidate, detail, Array.Empty<string>()]) as IReadOnlyList<string>;
        var missingArtifact = resolveMissingArtifact.Invoke(null, [candidate, detail, responseText]) as string;
        var invalidBrowserProof = resolveInvalidBrowserProof.Invoke(null, [candidate, detail]) as string;
        var invalidQualityProof = resolveInvalidQualityProof.Invoke(null, [candidate, detail, responseText]) as string;

        Assert.True(
            status == ProcessStepRunStatus.Completed,
            $"{reason} Missing tools: {string.Join(", ", missingTools ?? Array.Empty<string>())}. Missing artifact: {missingArtifact}. Invalid browser proof: {invalidBrowserProof}. Invalid quality proof: {invalidQualityProof}.");
    }

    [Fact]
    public void SatisfiedArtifactDispositionCompletion_recovers_failed_writeback_with_explicit_repair_branch()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("TryResolveSatisfiedArtifactDispositionCompletion", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveSatisfiedArtifactDispositionCompletion method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for the implemented Blazor app.",
            ProcessStepKind.Review,
            [
                ("quality-accepted", "Quality accepted", "Continue to result writeback."),
                ("repair-required", "Repair required", "Route back to implementation repair.")
            ],
            true,
            [
                (ProcessArtifactKind.Evidence, "Blazor runtime evidence pack", true, "Must include screenshots, browser console, and visible behavior assertions."),
                (ProcessArtifactKind.Transcript, "Validation self-review summary", true, "Must state accepted or repair-required disposition.")
            ],
            [],
            stepTitle: "Validate Blazor runtime and browser evidence",
            recordedArtifactTitles: ["Blazor runtime evidence pack", "Validation self-review summary"]);
        var evidenceExpectation = ResolveDispatchArtifactExpectation(candidate, "Blazor runtime evidence pack");
        var summaryExpectation = ResolveDispatchArtifactExpectation(candidate, "Validation self-review summary");
        var validationResults = new[]
        {
            new ProcessRunAutomationDispatchService.ProcessArtifactExpectationValidationResult(
                evidenceExpectation.Id,
                evidenceExpectation.Title,
                ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.RuntimeProof,
                ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied,
                ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AgentExecutionArtifact,
                Guid.NewGuid(),
                "artifacts/process-runs/run-1/03-blazor-runtime-evidence-pack.md",
                "Satisfied by a process artifact record.",
                string.Empty,
                "runtime-proof"),
            new ProcessRunAutomationDispatchService.ProcessArtifactExpectationValidationResult(
                summaryExpectation.Id,
                summaryExpectation.Title,
                ProcessRunAutomationDispatchService.ProcessArtifactExpectationMode.Narrative,
                ProcessRunAutomationDispatchService.ProcessArtifactValidationStatus.Satisfied,
                ProcessRunAutomationDispatchService.ProcessArtifactProducerKind.AgentExecutionArtifact,
                Guid.NewGuid(),
                "artifacts/process-runs/run-1/03-validation-self-review-summary.md",
                "Satisfied by a process artifact record.",
                string.Empty,
                "self-review")
        };
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Failed,
            "project_structure_node_create failed after the managed evidence files were written.",
            summaryMarkdown: """
            ## Validation self-review summary

            Acceptance decision:
            - Status: repair-required
            - Reason: Space-key browser proof failed and requires implementation repair.
            """);
        var arguments = new object?[]
        {
            candidate,
            ProcessStepRunStatus.Failed,
            validationResults,
            responseText,
            null,
            null
        };

        var recovered = (bool)(method.Invoke(null, arguments)
            ?? throw new InvalidOperationException("Disposition recovery result was not returned."));
        var branchOutcome = arguments[4]
            ?? throw new InvalidOperationException("Recovered branch outcome was not returned.");
        var branchOutcomeId = (Guid)(branchOutcome.GetType().GetProperty("Id")?.GetValue(branchOutcome)
            ?? throw new InvalidOperationException("DispatchBranchOutcome.Id was not available."));
        var reason = arguments[5] as string;

        Assert.True(recovered);
        Assert.Equal(ResolveBranchOutcomeId(candidate, "repair-required"), branchOutcomeId);
        Assert.Contains("required current-run artifacts are satisfied", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_uses_synthetic_default_branch_when_it_is_only_success_path()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var resolveSelectedBranchOutcomeId = ResolveSelectedBranchOutcomeIdMethod(serviceType);
        var candidate = CreateDispatchCandidateWithBranchOutcomes(
            "Review architecture impact and continue on the default path when no issue is found.",
            true,
            ("__default__", "Default", "Continue when no explicit branch outcome is selected."),
            ("__error__", "Error", "Handle exceptions, failed validations, or explicit error escalation."));
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Architecture review found no source-of-truth conflict.");
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Architecture review run",
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var selectedBranchOutcomeId = (Guid?)resolveSelectedBranchOutcomeId.Invoke(null, [candidate, ProcessStepRunStatus.Completed, responseText]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.Equal(ResolveBranchOutcomeId(candidate, "__default__"), selectedBranchOutcomeId);
    }

    [Fact]
    public void ResolveCompletionStatus_rejects_synthetic_default_branch_when_domain_disposition_is_required()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var resolveSelectedBranchOutcomeId = ResolveSelectedBranchOutcomeIdMethod(serviceType);
        var candidate = CreateDispatchCandidateWithBranchOutcomes(
            "Run QA validation and choose the real validation disposition.",
            true,
            ("quality-accepted", "Quality accepted", "Continue to result writeback."),
            ("repair-required", "Repair required", "Route back to implementation repair."),
            ("__default__", "Default", "Continue when no explicit branch outcome is selected."),
            ("__error__", "Error", "Handle exceptions, failed validations, or explicit error escalation."));
        var now = DateTimeOffset.UtcNow;
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Completed,
            "Validation did not select a real governed disposition.",
            branchOutcomeKey: "__default__");
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "QA review run",
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var selectedBranchOutcomeId = (Guid?)resolveSelectedBranchOutcomeId.Invoke(null, [candidate, ProcessStepRunStatus.Completed, responseText]);

        Assert.Equal(ProcessStepRunStatus.Failed, status);
        Assert.Null(selectedBranchOutcomeId);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
    public void ResolveCompletionStatus_routes_branched_qa_blocked_product_defect_to_repair_branch()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var resolveSelectedBranchOutcomeId = ResolveSelectedBranchOutcomeIdMethod(serviceType);
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for the implemented Blazor SSR app.",
            ProcessStepKind.Review,
            [
                ("quality_accepted", "Quality accepted", "Continue to release governance."),
                ("repair_required", "Repair required", "Route back to the repair implementation step."),
                ("error", "Error", "Escalate process execution failure.")
            ],
            true,
            [(ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must capture browser evidence and unresolved defects.")],
            [],
            stepTitle: "Run QA validation and browser proof",
            recordedArtifactTitles: ["Regression evidence pack"]);
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Browser proof reached the live /shifts route and screenshots were captured, but the current-run browser console reports 2 errors and unresolved UI/runtime proof risk needs repair.",
            summaryMarkdown: "Regression evidence pack captured browser proof and console defects that require repair.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                ("workspace_stat_path", CreateProviderNativeTextResult("Artifact path verified.")),
                ("workspace_read_file", CreateProviderNativeTextResult("Artifact contents reviewed.")),
                ("workspace_write_file", CreateProviderNativeTextResult("Regression evidence pack written.")),
                ("browser_take_screenshot", CreateProviderNativeTextResult("Screenshot saved.")),
                ("browser_snapshot", CreateProviderNativeTextResult("Snapshot saved.")),
                ("browser_console_messages", CreateProviderNativeTextResult("Console log saved."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Run QA validation and browser proof"]) as string;
        var selectedBranchOutcomeId = (Guid?)resolveSelectedBranchOutcomeId.Invoke(null, [candidate, ProcessStepRunStatus.Completed, responseText]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.Equal(ResolveBranchOutcomeId(candidate, "repair_required"), selectedBranchOutcomeId);
        Assert.NotNull(reason);
        Assert.Contains("repair disposition 'Repair required'", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_routes_repairable_branch_proof_gap_even_when_validation_evidence_is_missing()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var resolveSelectedBranchOutcomeId = ResolveSelectedBranchOutcomeIdMethod(serviceType);
        var candidate = CreateDispatchCandidateCore(
            "Run QA validation and browser proof for the implemented JavaScript app.",
            ProcessStepKind.Review,
            [
                ("quality_accepted", "Quality accepted", "Continue to release governance."),
                ("repair_required", "Repair required", "Route back to the repair implementation step."),
                ("error", "Error", "Escalate process execution failure.")
            ],
            true,
            [(ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must capture validation evidence and unresolved defects.")],
            [],
            stepTitle: "Run QA validation and browser proof");
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "QA evidence is insufficient for release: the lint script is a placeholder and no current-run nonzero test, build, or browser receipts were executed. Validation proof is missing and the implementation requires repair.",
            summaryMarkdown: "Repair required because validation proof is missing.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                ("workspace_read_file", CreateProviderNativeTextResult("Inspected package.json and found placeholder validation scripts."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Run QA validation and browser proof"]) as string;
        var selectedBranchOutcomeId = (Guid?)resolveSelectedBranchOutcomeId.Invoke(null, [candidate, ProcessStepRunStatus.Completed, responseText]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.Equal(ResolveBranchOutcomeId(candidate, "repair_required"), selectedBranchOutcomeId);
        Assert.NotNull(reason);
        Assert.Contains("repair disposition 'Repair required'", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_routes_repaired_qa_blocked_product_defect_to_escalation_branch()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var resolveSelectedBranchOutcomeId = ResolveSelectedBranchOutcomeIdMethod(serviceType);
        var candidate = CreateDispatchCandidateCore(
            "Re-run QA validation and browser proof after repair.",
            ProcessStepKind.Review,
            [
                ("quality-accepted", "Quality accepted", "Repaired evidence is sufficient for downstream security and release governance."),
                ("repair-escalation", "Repair escalation", "The same or new release-blocking quality issue remains after repair and needs explicit delivery escalation."),
                ("__default__", "Default", "Continue when no explicit branch outcome is selected."),
                ("__error__", "Error", "Handle exceptions, failed validations, or explicit error escalation.")
            ],
            true,
            [(ProcessArtifactKind.Evidence, "Repaired regression evidence pack", true, "Must name repaired flows, assertion depth, screenshots, and unresolved risks after the repair pass.")],
            [],
            stepTitle: "Re-run QA validation and browser proof after repair",
            recordedArtifactTitles: ["Repaired regression evidence pack"]);
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Browser proof captured product routes and desktop screenshots for `/` and `/shifts`, but validation is not clean: browser tools reported 2 console errors on both routes and the snapshot shows an unhandled runtime error. The repaired pass still needs escalation.",
            summaryMarkdown: "Repaired regression evidence pack captured browser proof, console defects, and unresolved runtime risk after repair.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                ("workspace_stat_path", CreateProviderNativeTextResult("Artifact path verified.")),
                ("workspace_read_file", CreateProviderNativeTextResult("Artifact contents reviewed.")),
                ("workspace_write_file", CreateProviderNativeTextResult("Repaired regression evidence pack written.")),
                ("workspace_dotnet_run", CreateProviderNativeTextResult("Host started.")),
                ("browser_take_screenshot", CreateProviderNativeTextResult("Screenshot saved.")),
                ("browser_snapshot", CreateProviderNativeTextResult("Snapshot saved.")),
                ("browser_console_messages", CreateProviderNativeTextResult("Console log saved."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Re-run QA validation and browser proof after repair"]) as string;
        var selectedBranchOutcomeId = (Guid?)resolveSelectedBranchOutcomeId.Invoke(null, [candidate, ProcessStepRunStatus.Completed, responseText]);

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.Equal(ResolveBranchOutcomeId(candidate, "repair-escalation"), selectedBranchOutcomeId);
        Assert.NotNull(reason);
        Assert.Contains("repair disposition 'Repair escalation'", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveCompletionStatus_completes_escalation_step_when_blocked_no_go_record_is_written()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var resolveCompletionStatus = serviceType.GetMethod("ResolveCompletionStatus", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveCompletionStatus method was not found.");
        var buildCompletionReason = serviceType.GetMethod("BuildCompletionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildCompletionReason method was not found.");
        var candidate = CreateDispatchCandidateCore(
            "Escalate unresolved repair findings and make an explicit no-go, scope reset, or replan decision.",
            ProcessStepKind.Work,
            [],
            false,
            [(ProcessArtifactKind.Decision, "Repair escalation record", true, "Must include unresolved defect list, no-go decision, next repair scope, and accountable owner.")],
            [
                (
                    "Re-run QA validation and browser proof after repair",
                    "Repaired regression evidence pack",
                    [("Repaired regression evidence pack", "Evidence", "artifacts/process-runs/run-001/repaired-regression-evidence-pack.md", "Repaired QA evidence recorded unresolved defects.", "workspace")])
            ],
            stepTitle: "Escalate unresolved repair findings",
            recordedArtifactTitles: ["Repair escalation record"]);
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Unresolved post-repair browser/runtime findings remain, including console errors and a release-blocking UI defect. The repair escalation record captures a no-go decision and required replan owner.",
            summaryMarkdown: "## Repair escalation record\n\nNo-go decision recorded with unresolved defect list and accountable next repair owner.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                (
                    "project_structure_read",
                    new Dictionary<string, object?>(),
                    CreateProviderNativeTextResult("Process node inspected.")),
                (
                    "workspace_stat_path",
                    new Dictionary<string, object?> { ["path"] = "artifacts/process-runs/run-001/repaired-regression-evidence-pack.md" },
                    CreateProviderNativeTextResult("Upstream evidence exists.")),
                (
                    "workspace_read_file",
                    new Dictionary<string, object?> { ["path"] = "artifacts/process-runs/run-001/repaired-regression-evidence-pack.md" },
                    CreateProviderNativeTextResult("Upstream evidence reviewed.")),
                (
                    "workspace_write_file",
                    new Dictionary<string, object?>(),
                    CreateProviderNativeTextResult("Repair escalation record written."))));

        var status = (ProcessStepRunStatus?)resolveCompletionStatus.Invoke(null, [candidate, detail]);
        var reason = buildCompletionReason.Invoke(null, [candidate, detail, "Escalate unresolved repair findings"]) as string;

        Assert.Equal(ProcessStepRunStatus.Completed, status);
        Assert.NotNull(reason);
        Assert.Contains("escalation disposition 'Repair escalation record'", reason, StringComparison.Ordinal);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
            [candidate, detail, responseText, new[] { "workspace_dotnet_build", "workspace_dotnet_test" }, CreateCarriedImplementationProof(false, false), 1, 5]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.False((bool)shouldRetryResult);
    }

    [Fact]
    public void ApplyTransitionConsequences_keeps_blocked_dependent_until_upstream_artifact_materializes()
    {
        var upstreamStepDefinitionId = Guid.NewGuid();
        var downstreamStepDefinitionId = Guid.NewGuid();
        var upstreamStepDefinition = new ProcessStepDefinition
        {
            Id = upstreamStepDefinitionId,
            Title = "Implement bounded delivery change",
            OrderIndex = 0
        };
        var downstreamStepDefinition = new ProcessStepDefinition
        {
            Id = downstreamStepDefinitionId,
            Title = "Complete peer review and integration readiness",
            OrderIndex = 1
        };
        var upstreamStepRun = new ProcessStepRun
        {
            StepDefinitionId = upstreamStepDefinitionId,
            Title = upstreamStepDefinition.Title,
            Status = ProcessStepRunStatus.Completed
        };
        var downstreamStepRun = new ProcessStepRun
        {
            StepDefinitionId = downstreamStepDefinitionId,
            Title = downstreamStepDefinition.Title,
            Status = ProcessStepRunStatus.Blocked,
            BlockedReason = "Cannot dispatch 'Complete peer review and integration readiness' because required upstream artifacts are missing: upstream step 'Implement bounded delivery change' must provide required artifact 'Implementation change set'. Automation requested upstream artifact materialization from 'Implement bounded delivery change' before retrying this step."
        };
        var now = DateTimeOffset.UtcNow;
        var stepDefinitionsById = new Dictionary<Guid, ProcessStepDefinition>
        {
            [upstreamStepDefinitionId] = upstreamStepDefinition,
            [downstreamStepDefinitionId] = downstreamStepDefinition
        };
        var stepRunsByDefinitionId = new Dictionary<Guid, ProcessStepRun>
        {
            [upstreamStepDefinitionId] = upstreamStepRun,
            [downstreamStepDefinitionId] = downstreamStepRun
        };
        var dependenciesByStepId = new Dictionary<Guid, List<ProcessStepDependencyDefinition>>
        {
            [downstreamStepDefinitionId] =
            [
                new ProcessStepDependencyDefinition
                {
                    StepDefinitionId = downstreamStepDefinitionId,
                    DependsOnStepId = upstreamStepDefinitionId
                }
            ]
        };

        ProcessRuntimeProgressionPlanner.ApplyTransitionConsequences(
            ProcessStepRunStatus.Completed,
            upstreamStepDefinition,
            stepDefinitionsById,
            stepRunsByDefinitionId,
            dependenciesByStepId,
            now);

        Assert.Equal(ProcessStepRunStatus.Blocked, downstreamStepRun.Status);
        Assert.NotEqual(now, downstreamStepRun.ReadyAtUtc);
        Assert.Contains("required upstream artifacts are missing", downstreamStepRun.BlockedReason, StringComparison.OrdinalIgnoreCase);

        ProcessRuntimeProgressionPlanner.ReactivateBlockedStepRunAfterUpstreamArtifactMaterialization(
            downstreamStepRun,
            downstreamStepDefinition,
            now);

        Assert.Equal(ProcessStepRunStatus.Ready, downstreamStepRun.Status);
        Assert.Equal(now, downstreamStepRun.ReadyAtUtc);
        Assert.Equal(string.Empty, downstreamStepRun.BlockedReason);
        Assert.Equal("Reopened after upstream artifact materialization completed.", downstreamStepRun.DecisionSummary);
    }

    [Fact]
    public void CreateObservedActiveAutomationExecutionOutcome_keeps_step_in_progress_without_finalization()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var method = serviceType.GetMethod("CreateObservedActiveAutomationExecutionOutcome", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateObservedActiveAutomationExecutionOutcome method was not found.");
        var now = DateTimeOffset.UtcNow;
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Long running process step",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Partial output",
                "OpenAI chat completions",
                "gpt-4.1",
                ProcessAutomationExecutionState.Running,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                null,
                now,
                string.Empty,
                null,
                []),
            null,
            [],
            []);

        var outcome = method.Invoke(null, [detail, "Partial output", 2])
            ?? throw new InvalidOperationException("Active execution outcome was not returned.");
        var status = (ProcessStepRunStatus)(outcome.GetType().GetProperty("CompletionStatus")?.GetValue(outcome)
            ?? throw new InvalidOperationException("CompletionStatus was not available."));
        var reason = outcome.GetType().GetProperty("CompletionReason")?.GetValue(outcome) as string;
        var selectedBranchOutcomeId = outcome.GetType().GetProperty("SelectedBranchOutcomeId")?.GetValue(outcome);

        Assert.Equal(ProcessStepRunStatus.InProgress, status);
        Assert.Contains("still Running", reason, StringComparison.Ordinal);
        Assert.Null(selectedBranchOutcomeId);
    }

    [Fact]
    public void ShouldRetryIncompleteSuccessfulRun_retries_javascript_browser_proof_blocked_before_launch_attempt()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryIncompleteSuccessfulRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryIncompleteSuccessfulRun method was not found.");
        var candidate = CreateDispatchCandidate(
            "Run QA validation and browser proof for the JavaScript browser app with package scripts.",
            ProcessStepKind.Review,
            (ProcessArtifactKind.Evidence, "Regression evidence pack", true, "Must name changed flows, browser proof, screenshots, and unresolved risks."));
        var responseText = StructuredOutcome(
            ProcessStepOutcomeStatus.Blocked,
            "Required browser proof could not be completed. The product root and handoff artifacts were inspected, but no reachable localhost URL or fresh browser receipts were captured.");
        var detail = CreateSuccessfulExecutionDetail(
            responseText,
            BuildSerializedSessionState(
                ("workspace_stat_path", CreateProviderNativeTextResult("Product root exists.")),
                ("workspace_read_file", CreateProviderNativeTextResult("package.json reviewed.")),
                ("workspace_write_file", CreateProviderNativeTextResult("Regression evidence pack written."))));

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [
                candidate,
                detail,
                responseText,
                new[]
                {
                    "workspace_pwsh_run_script",
                    "browser_navigate",
                    "browser_snapshot",
                    "browser_take_screenshot",
                    "browser_console_messages"
                },
                CreateCarriedImplementationProof(false, false),
                1,
                3
            ]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
            [candidate, detail, responseText, Array.Empty<string>(), CreateCarriedImplementationProof(false, false), 1, 3]);

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
        const string finalizerFailureSummary = "Finalizer tool 'submit_process_step_outcome' in Required mode failed validation. Errors: agent.finalizer.missing: Required finalizer tool 'submit_process_step_outcome' was not called.";
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                finalizerFailureSummary,
                "OpenAI default",
                "gpt-4.1",
                ProcessAutomationExecutionState.Failed,
                ProcessAutomationRunOutcome.Failed,
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

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [
                candidate,
                detail,
                responseText,
                Array.Empty<string>(),
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
                1,
                5
            ]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryRecoverableFailedRun_returns_true_for_missing_finalizer_on_decision_step()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryRecoverableFailedRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryRecoverableFailedRun method was not found.");
        var candidate = CreateDispatchCandidateWithStepTitle(
            "Decide technical approach",
            "Choose the architecture path and record the decision.",
            ProcessStepKind.Decision,
            (ProcessArtifactKind.Decision, "Implementation approach decision", true, "Must summarize selected approach and risks."));

        var now = DateTimeOffset.UtcNow;
        const string responseText = "The approach decision was drafted, but the governed finalizer was not emitted.";
        const string finalizerFailureSummary = "Finalizer tool 'submit_process_step_outcome' in Required mode failed validation. Errors: agent.finalizer.missing: Required finalizer tool 'submit_process_step_outcome' was not called.";
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Approach decision run",
                "process-step",
                "step-2",
                "corr-2",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                finalizerFailureSummary,
                "OpenAI default",
                "gpt-4.1",
                ProcessAutomationExecutionState.Failed,
                ProcessAutomationRunOutcome.Failed,
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

        var shouldRetryResult = shouldRetry.Invoke(
            null,
            [
                candidate,
                detail,
                responseText,
                Array.Empty<string>(),
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
                1,
                3
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Failed,
                ProcessAutomationRunOutcome.Failed,
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
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Failed,
                ProcessAutomationRunOutcome.Cancelled,
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
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
                1,
                3
            ]);

        Assert.IsType<bool>(shouldRetryResult);
        Assert.True((bool)shouldRetryResult);
    }

    [Fact]
    public void ShouldRetryRecoverableFailedRun_returns_true_for_failed_host_restart_interruption()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var shouldRetry = serviceType.GetMethod("ShouldRetryRecoverableFailedRun", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ShouldRetryRecoverableFailedRun method was not found.");
        var candidate = CreateWorkflowImplementationDispatchCandidate();

        var now = DateTimeOffset.UtcNow;
        const string responseText = "Execution interrupted because the CanDoItAll host restarted before the run completed.";
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "New exploration thread",
                "process-step",
                "step-implementation",
                "corr-4",
                "runtime-recovery-scan",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "OpenAI default",
                "gpt-4.1",
                ProcessAutomationExecutionState.Failed,
                ProcessAutomationRunOutcome.Failed,
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
                Array.Empty<ProcessAutomationToolExecutionReceipt>(),
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Failed,
                ProcessAutomationRunOutcome.Failed,
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
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Failed,
                ProcessAutomationRunOutcome.Failed,
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

    [Fact]
    public void TryResolveRecoverableProviderFailure_detects_structured_output_incompatible_provider()
    {
        var serviceType = typeof(ProcessRunAutomationDispatchService);
        var tryResolveProviderFailure = serviceType.GetMethod("TryResolveRecoverableProviderFailure", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("TryResolveRecoverableProviderFailure method was not found.");

        var now = DateTimeOffset.UtcNow;
        const string responseText = "Provider 'Local Ollama' using transport 'ChatCompletions' cannot enforce structured output contract 'process_step_outcome_result'. Choose a structured-output capable OpenAI/Azure OpenAI provider or disable the machine-critical structured-output request.";
        var detail = new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Scope run",
                "process-step",
                "step-scope",
                "corr-scope",
                "run-start",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                responseText,
                "Local Ollama",
                "llama3.1",
                ProcessAutomationExecutionState.Failed,
                ProcessAutomationRunOutcome.Failed,
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
            "The assigned provider cannot enforce the required structured output contract.",
            arguments[2]);
    }

    [Fact]
    public void OrderFallbackProviders_excludes_ollama_from_process_recovery_fallbacks()
    {
        var orderFallbackProviders = typeof(ProcessRunAutomationDispatchService).GetMethod(
            "OrderFallbackProviders",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("OrderFallbackProviders method was not found.");
        var failedProviderId = Guid.NewGuid();
        var openAiProvider = CreateProviderProfile(
            "OpenAI default",
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            ManagedSeedProviderFallbacks.OpenAiDefaultModel);
        var remoteOllamaProvider = CreateProviderProfile(
            "Remote Ollama",
            ProviderKind.Ollama,
            ProviderTransportKind.ChatCompletions,
            "gptoss32k:latest");

        var ordered = orderFallbackProviders.Invoke(
            null,
            [new[] { remoteOllamaProvider, openAiProvider }, failedProviderId]) as IReadOnlyList<ProviderProfile>;

        Assert.NotNull(ordered);
        Assert.Equal(openAiProvider.Id, ordered![0].Id);
        Assert.DoesNotContain(ordered, provider => provider.Kind == ProviderKind.Ollama);
    }

    [Fact]
    public void ResolveProcessRunActualCost_prefers_usage_ledger_over_legacy_metrics()
    {
        var provider = CreateProviderProfile(
            "Provider A",
            ProviderKind.OpenAi,
            ProviderTransportKind.Responses,
            "model-a") with
        {
            ModelPrices = [new ProviderModelTokenPrice("model-a", 1.00m, 0.10m, 4.00m)]
        };
        var agentId = Guid.NewGuid();
        var usageRunId = Guid.NewGuid();
        var legacyRunId = Guid.NewGuid();
        var observedUsage = CreateCostUsageObservation(
            usageRunId,
            ProcessAutomationProviderUsageStatus.Observed,
            ProviderUsageSourcePhases.AgentRuntime,
            responseId: "resp-usage-001",
            inputTokens: 1_000_000,
            cachedInputTokens: 250_000,
            outputTokens: 500_000);
        var duplicateObservedUsage = observedUsage with
        {
            Id = Guid.NewGuid()
        };
        var unknownUsage = CreateCostUsageObservation(
            usageRunId,
            ProcessAutomationProviderUsageStatus.MissingAfterProviderActivity,
            ProviderUsageSourcePhases.FinalizerShortCircuit,
            responseId: "resp-unknown-001",
            inputTokens: 0,
            cachedInputTokens: 0,
            outputTokens: 0);
        var usageDetail = CreateCostedExecutionRunDetail(
            usageRunId,
            agentId,
            [CreateCostMetric(usageRunId, agentId, costUsd: 50.00m)],
            [observedUsage, duplicateObservedUsage, unknownUsage]);
        var legacyDetail = CreateCostedExecutionRunDetail(
            legacyRunId,
            agentId,
            [CreateCostMetric(legacyRunId, agentId, costUsd: 1.23m)],
            []);

        var actualCost = ProcessRunAutomationDispatchService.ResolveProcessRunActualCost(
            [usageDetail, legacyDetail],
            [provider]);

        Assert.Equal(4.005m, actualCost);
    }

    private static ProviderProfile CreateProviderProfile(
        string name,
        ProviderKind kind,
        ProviderTransportKind transport,
        string defaultModel)
    {
        return new ProviderProfile(
            Id: Guid.NewGuid(),
            Name: name,
            Kind: kind,
            BaseUrl: kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi
                ? "https://api.openai.com/v1"
                : "http://127.0.0.1:11434",
            ApiKeyEnvironmentVariable: kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi
                ? "OPENAI_API_KEY"
                : string.Empty,
            DefaultModel: defaultModel,
            Transport: transport,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: transport != ProviderTransportKind.Responses,
            SupportsBackgroundResponses: transport == ProviderTransportKind.Responses,
            ConfigurationJson: "{}",
            Notes: "Integration test provider.",
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [defaultModel]);
    }

    private static ProcessAutomationExecutionRunDetail CreateCostedExecutionRunDetail(
        Guid executionRunId,
        Guid agentId,
        IReadOnlyList<ProcessAutomationRunMetric> metrics,
        IReadOnlyList<ProcessAutomationProviderUsageObservation> usageObservations)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                executionRunId,
                agentId,
                null,
                "Process cost run",
                "process-step",
                "step-001",
                "corr-cost",
                "cause-cost",
                "process-automation-dispatch",
                "system",
                "{}",
                "Prompt",
                "Completed.",
                "Provider A",
                "model-a",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                null,
                [],
                ProcessRunId: "process-run-001",
                ProcessStepId: "step-001"),
            null,
            [],
            metrics)
        {
            UsageObservations = usageObservations
        };
    }

    private static ProcessAutomationRunMetric CreateCostMetric(
        Guid executionRunId,
        Guid agentId,
        decimal costUsd)
    {
        return new ProcessAutomationRunMetric(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            ChatSessionId: null,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Outcome: ProcessAutomationRunOutcome.Succeeded,
            ProviderName: "Provider A",
            Model: "model-a",
            DurationMs: 100,
            InputTokens: 1_000,
            OutputTokens: 500,
            ToolCalls: 0)
        {
            ExecutionRunId = executionRunId,
            CostUsd = costUsd
        };
    }

    private static ProcessAutomationProviderUsageObservation CreateCostUsageObservation(
        Guid executionRunId,
        ProcessAutomationProviderUsageStatus status,
        string sourcePhase,
        string responseId,
        int inputTokens,
        int cachedInputTokens,
        int outputTokens)
    {
        return new ProcessAutomationProviderUsageObservation(
            Id: Guid.NewGuid(),
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ProviderName: "Provider A",
            ProviderKind: ProviderKind.OpenAi.ToString(),
            Model: "model-a",
            TransportKind: ProviderTransportKind.Responses.ToString(),
            SourcePhase: sourcePhase,
            UsageStatus: status,
            InputTokens: inputTokens,
            CachedInputTokens: cachedInputTokens,
            OutputTokens: outputTokens,
            ReasoningTokens: 0,
            TotalTokens: inputTokens + outputTokens,
            ToolCallCount: 0)
        {
            ExecutionRunId = executionRunId,
            ProviderResponseId = responseId
        };
    }

    private static object CreateWorkflowImplementationDispatchCandidate()
    {
        return CreateDispatchCandidate(
            "Implement feature, tests, and migration notes for the workflow app.",
            ProcessStepKind.Work,
            (ProcessArtifactKind.Deliverable, "Implementation change set", true, "Must be linked to tests, migration notes, and touched-surface inventory."),
            (ProcessArtifactKind.Checklist, "Migration and rollout preparation checklist", true, "Must name data changes, operational preconditions, and rollback steps."));
    }

    private sealed record ResolvedArtifactInputFixture(
        Guid ProcessRunId,
        ProcessStepArtifactInputDefinition ConfiguredInput,
        ProcessArtifactExpectation Expectation,
        ProcessStepDefinition SourceStepDefinition,
        ProcessStepRun SourceStepRun);

    private static ResolvedArtifactInputFixture CreateResolvedArtifactInputFixture()
    {
        var processRunId = Guid.NewGuid();
        var sourceStepDefinition = new ProcessStepDefinition
        {
            Title = "Implement bounded delivery change"
        };
        var sourceStepRun = new ProcessStepRun
        {
            ProcessRunId = processRunId,
            StepDefinitionId = sourceStepDefinition.Id,
            Sequence = 1,
            Title = sourceStepDefinition.Title,
            Status = ProcessStepRunStatus.Completed
        };
        var expectation = new ProcessArtifactExpectation
        {
            StepDefinitionId = sourceStepDefinition.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Implementation change set",
            IsRequired = true,
            ValidationRequirementSummary = "Must describe code changes and validation proof."
        };
        var configuredInput = new ProcessStepArtifactInputDefinition
        {
            ArtifactExpectationId = expectation.Id
        };

        return new(
            processRunId,
            configuredInput,
            expectation,
            sourceStepDefinition,
            sourceStepRun);
    }

    private static ProcessArtifactRecord CreateProcessArtifactRecord(
        ProcessArtifactExpectation expectation,
        ProcessStepRun sourceStepRun,
        Guid processRunId,
        string managedStoragePath)
    {
        return new ProcessArtifactRecord
        {
            ProcessRunId = processRunId,
            StepRunId = sourceStepRun.Id,
            ArtifactExpectationId = expectation.Id,
            ArtifactKind = expectation.ArtifactKind,
            Title = expectation.Title,
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ReviewSummary = "Implementation artifact was produced.",
            ProvenanceSummary = "workspace",
            ManagedStoragePath = managedStoragePath,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static IReadOnlyList<ProcessRunAutomationDispatchService.DispatchArtifactInput> InvokeBuildResolvedArtifactInputs(
        ResolvedArtifactInputFixture fixture,
        IReadOnlyList<ProcessArtifactRecord> existingArtifacts)
    {
        var method = typeof(ProcessRunAutomationDispatchService).GetMethod("BuildResolvedArtifactInputs", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildResolvedArtifactInputs method was not found.");
        var result = method.Invoke(
            null,
            [
                new[] { fixture.ConfiguredInput },
                new Dictionary<Guid, ProcessArtifactExpectation> { [fixture.Expectation.Id] = fixture.Expectation },
                new Dictionary<Guid, ProcessStepDefinition> { [fixture.SourceStepDefinition.Id] = fixture.SourceStepDefinition },
                new Dictionary<Guid, IReadOnlyList<ProcessStepRun>> { [fixture.SourceStepDefinition.Id] = [fixture.SourceStepRun] },
                existingArtifacts
            ]);

        return (IReadOnlyList<ProcessRunAutomationDispatchService.DispatchArtifactInput>)(result
            ?? throw new InvalidOperationException("BuildResolvedArtifactInputs did not return a result."));
    }

    private static object CreateCarriedImplementationProof(
        bool hasConcreteImplementationProof,
        bool hasRunnableApplicationProof,
        bool hasConcreteProductMutation = false)
    {
        var proofType = typeof(ProcessRunAutomationDispatchService).GetNestedType(
            "CarriedImplementationProof",
            BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CarriedImplementationProof type was not found.");

        return Activator.CreateInstance(
                   proofType,
                   BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                   binder: null,
                   args: [hasConcreteImplementationProof, hasRunnableApplicationProof, hasConcreteProductMutation],
                   culture: null)
               ?? throw new InvalidOperationException("CarriedImplementationProof could not be constructed.");
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

    private static object CreateReviewDispatchCandidateWithArtifactInputs(
        string workBriefText,
        params (string SourceStepTitle, string ExpectedArtifactTitle, (string Title, string ArtifactKind, string ManagedStoragePath, string ReviewSummary, string ProvenanceSummary)[] Artifacts)[] artifactInputDefinitions)
    {
        return CreateDispatchCandidateCore(
            workBriefText,
            ProcessStepKind.Review,
            [],
            false,
            [],
            artifactInputDefinitions,
            stepTitle: "QA validation");
    }

    private static AgentDefinition CreateAgentDefinition(
        string name,
        string roleTitle,
        DateTimeOffset updatedAtUtc)
    {
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            roleTitle,
            "Test manager agent.",
            "Manage the process.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            "gpt-5.4-mini",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            0.2d,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: updatedAtUtc,
            UpdatedAtUtc: updatedAtUtc);
    }

    private static ProcessRunAutomationDispatchService.DispatchArtifactExpectation CreateDispatchArtifactExpectation(
        string title,
        bool isRequired)
        => CreateDispatchArtifactExpectation(
            ProcessArtifactKind.Evidence,
            title,
            isRequired,
            $"Create {title}.");

    private static ProcessRunAutomationDispatchService.DispatchArtifactExpectation CreateDispatchArtifactExpectation(
        ProcessArtifactKind artifactKind,
        string title,
        bool isRequired,
        string validationRequirementSummary)
    {
        return new ProcessRunAutomationDispatchService.DispatchArtifactExpectation(
            Guid.NewGuid(),
            artifactKind,
            title,
            isRequired,
            ProcessArtifactTrustRequirement.ReviewRequired,
            ProcessSensitivityLevel.Internal,
                validationRequirementSummary,
                string.Empty);
    }

    private static ProcessRunAutomationDispatchService.DispatchArtifactExpectation ResolveDispatchArtifactExpectation(
        object candidate,
        string title)
    {
        var expectedArtifacts = candidate.GetType().GetProperty("ExpectedArtifacts")?.GetValue(candidate)
            ?? throw new InvalidOperationException("DispatchCandidate.ExpectedArtifacts was not available.");
        return ((IEnumerable)expectedArtifacts)
            .Cast<ProcessRunAutomationDispatchService.DispatchArtifactExpectation>()
            .Single(item => string.Equals(item.Title, title, StringComparison.Ordinal));
    }

    private static ProcessRunAssignmentViewModel CreateAssignment(
        Guid partyId,
        string displayName,
        string roleDisplayName)
    {
        return new ProcessRunAssignmentViewModel(
            Guid.NewGuid(),
            Guid.NewGuid(),
            StepDefinitionId: null,
            PartyId: partyId,
            WorkflowDefinitionId: null,
            WorkflowVersionId: null,
            DisplayName: displayName,
            ExecutorKind: "AI agent",
            BindingReason: "Projected from AgentFramework organization catalog.",
            SourceRegistryKey: string.Empty,
            SnapshotSummary: string.Empty,
            IsFallback: false,
            IsCapabilityGap: false,
            AllowsDirectMessaging: true)
        {
            RoleDisplayName = roleDisplayName
        };
    }

    private static object CreateDispatchCandidateCore(
        string workBriefText,
        ProcessStepKind stepKind,
        (string Key, string Title, string Description)[] branchOutcomeDefinitions,
        bool requiresExplicitBranchOutcomeSelection,
        (ProcessArtifactKind ArtifactKind, string Title, bool IsRequired, string ValidationRequirementSummary)[] expectedArtifactDefinitions,
        (string SourceStepTitle, string ExpectedArtifactTitle, (string Title, string ArtifactKind, string ManagedStoragePath, string ReviewSummary, string ProvenanceSummary)[] Artifacts)[] artifactInputDefinitions,
        string triggerReason = "Deliver the implementation showcase.",
        string stepTitle = "Implement feature",
        IReadOnlyCollection<string>? recordedArtifactTitles = null,
        string processName = "Software delivery",
        string outputContractSummary = "Buildable implementation",
        string processSlug = "",
        string stepKey = "",
        string manualRecoveryDirective = "",
        string runName = "Showcase run")
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
        var recordedArtifactTitleSet = recordedArtifactTitles is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(recordedArtifactTitles, StringComparer.OrdinalIgnoreCase);
        var recordedArtifactExpectationIds = new HashSet<Guid>();
        for (var index = 0; index < expectedArtifactDefinitions.Length; index++)
        {
            var definition = expectedArtifactDefinitions[index];
            var expectationId = Guid.NewGuid();
            var expectedArtifact = Activator.CreateInstance(
                artifactExpectationType,
                expectationId,
                definition.ArtifactKind,
                definition.Title,
                definition.IsRequired,
                ProcessArtifactTrustRequirement.ReviewRequired,
                ProcessSensitivityLevel.Internal,
                definition.ValidationRequirementSummary,
                string.Empty)
                ?? throw new InvalidOperationException("DispatchArtifactExpectation could not be constructed.");
            expectedArtifacts.SetValue(expectedArtifact, index);
            if (recordedArtifactTitleSet.Contains(definition.Title))
            {
                recordedArtifactExpectationIds.Add(expectationId);
            }
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
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                null,
                null,
                false,
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
            .Single(candidateConstructor => candidateConstructor.GetParameters().Length == 16);
        return constructor.Invoke(
                    [
                        new ProcessRun
                        {
                            Name = runName,
                            TriggerReason = triggerReason
                        },
                       new ProcessDefinition
                       {
                           Name = processName,
                           Slug = processSlug
                       },
                       new ProcessStepRun
                       {
                           Title = stepTitle,
                           CurrentExecutorName = "Showcase Lead Engineer",
                           StepKind = stepKind
                       },
                       new ProcessStepDefinition
                       {
                           Key = stepKey,
                           Title = stepTitle,
                           StepKind = stepKind,
                           InputContractSummary = "Use the available process context and artifacts.",
                           OutputContractSummary = outputContractSummary,
                           EvidenceContractSummary = expectedArtifactDefinitions.Length == 0
                               ? "Implementation change set"
                               : string.Join(", ", expectedArtifactDefinitions.Select(item => item.Title))
                       },
                       new ProcessWorkBrief
                       {
                           WorkBriefText = workBriefText,
                           HandoffSummary = "Architecture decision record.",
                           ExpectedOutcome = outputContractSummary,
                           EvidenceExpectationSummary = expectedArtifactDefinitions.Length == 0
                               ? "Implementation change set"
                               : string.Join(", ", expectedArtifactDefinitions.Select(item => item.Title))
                       },
                       Guid.NewGuid(),
                       expectedArtifacts,
                       recordedArtifactExpectationIds,
                       artifactInputs,
                       new HashSet<string>(StringComparer.Ordinal),
                       null,
                       null,
                       manualRecoveryDirective,
                       branchOutcomes,
                       requiresExplicitBranchOutcomeSelection,
                       new AgentProcessCooperationMetadata(
                           AgentProcessCooperationMode.ProcessArtifactHandoff,
                           AgentWorkspaceToolProfileKind.SoftwareDevelopment,
                           "Test dispatch candidate uses the default process artifact handoff cooperation profile.")
                   ])
               ?? throw new InvalidOperationException("DispatchCandidate could not be constructed.");
    }

    private static ProcessAutomationExecutionRunDetail CreateSuccessfulExecutionDetail(
        string responseText,
        string? serializedSessionStateJson,
        IReadOnlyList<ProcessAutomationToolExecutionReceipt>? toolReceipts = null,
        string prompt = "Prompt",
        string serializedInvocationMetadataJson = "{}")
    {
        var now = DateTimeOffset.UtcNow;
        return new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                "Process step run",
                "process-step",
                "step-1",
                "corr-1",
                "run-start",
                "process-automation-dispatch",
                "system",
                serializedInvocationMetadataJson,
                prompt,
                responseText,
                "OpenAI chat completions",
                "gpt-5.4-mini",
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
                now,
                now,
                now,
                now,
                string.Empty,
                serializedSessionStateJson,
                []),
            null,
            [],
            [])
        {
            ToolReceipts = toolReceipts ?? []
        };
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

    private static ProcessAutomationExecutionRunDetail CreateProcessMockExecutionDetail(
        string responseText,
        string roleKey,
        string artifactRoot = "artifacts/process-mock/mockrun001",
        string? branchOutcomeKey = null,
        params (string RelativePath, string ContentSignalText)[] artifacts)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProcessAutomationExecutionRunDetail(
            new ProcessAutomationExecutionRunRecord(
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
                ProcessAutomationExecutionState.Completed,
                ProcessAutomationRunOutcome.Succeeded,
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

    private static ProcessAutomationExecutionRunRecord CreateExecutionRun(string requestedBy, ProcessAutomationExecutionState state, ProcessAutomationRunOutcome? outcome)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProcessAutomationExecutionRunRecord(
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
            state is ProcessAutomationExecutionState.Completed or ProcessAutomationExecutionState.Failed ? now : null,
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

    private static ProcessAutomationToolExecutionReceipt CreateToolReceipt(
        string toolFamily,
        string toolName,
        string requestSummary,
        string workingDirectory,
        string exitSummary,
        DateTimeOffset timestamp)
    {
        return new ProcessAutomationToolExecutionReceipt(
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

    private static ProcessAutomationExecutionLogEntry CreateExecutionLogToolInvocation(
        Guid executionRunId,
        Guid agentId,
        DateTimeOffset timestamp,
        string toolName)
    {
        return new ProcessAutomationExecutionLogEntry(
            Guid.NewGuid(),
            agentId,
            null,
            timestamp,
            ProcessAutomationExecutionState.Running,
            "Tool",
            $"Invoking tool '{toolName}' with test arguments.") { ExecutionRunId = executionRunId };
    }

    private static ProcessAutomationExecutionLogEntry CreateExecutionLogToolInvocationWithFilename(
        Guid executionRunId,
        Guid agentId,
        DateTimeOffset timestamp,
        string toolName,
        string fileName)
    {
        return new ProcessAutomationExecutionLogEntry(
            Guid.NewGuid(),
            agentId,
            null,
            timestamp,
            ProcessAutomationExecutionState.Running,
            "Tool",
            $"Invoking tool '{toolName}' with filename=\"{fileName}\".") { ExecutionRunId = executionRunId };
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

    private static object ReadRecordProperty(object instance, string propertyName)
    {
        return instance.GetType()
                   .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                   ?.GetValue(instance)
               ?? throw new InvalidOperationException($"Property '{propertyName}' was not found.");
    }

    private sealed class TestStorageCatalogService(StorageCatalogRecord storage) : IStorageCatalogService
    {
        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([storage]);
        }

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var result = id == storage.Id
                ? storage
                : null;
            return Task.FromResult(result);
        }

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(storage);
        }

        public Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);
        }

        public Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestStorageDriverRegistry(IStorageDriver driver) : IStorageDriverRegistry
    {
        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => [driver.ProviderKind];

        public bool TryResolve(StorageProviderKind providerKind, out IStorageDriver resolvedDriver)
        {
            if (providerKind == driver.ProviderKind)
            {
                resolvedDriver = driver;
                return true;
            }

            resolvedDriver = null!;
            return false;
        }

        public IStorageDriver Resolve(StorageProviderKind providerKind)
        {
            return TryResolve(providerKind, out var resolvedDriver)
                ? resolvedDriver
                : throw new InvalidOperationException($"No storage driver is registered for provider '{providerKind}'.");
        }
    }

    private sealed class TestStorageDriver(StorageProviderKind providerKind, byte[] contentBytes) : IStorageDriver
    {
        public StorageProviderKind ProviderKind => providerKind;

        public StorageCapability SupportedCapabilities => StorageCapability.Read;

        public int OpenReadCount { get; private set; }

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new StorageConnectionTestResult(
                true,
                "ok",
                StorageHealthStatus.Healthy,
                SupportedCapabilities,
                DateTimeOffset.UtcNow));
        }

        public Task<StorageWriteResult> SaveAsync(
            StorageCatalogRecord storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
        {
            OpenReadCount++;
            return Task.FromResult<Stream>(new MemoryStream(contentBytes, writable: false));
        }

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, "manager-artifacts");
    }
}
