using System.Reflection;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRuntimeOperatorReadModelTests {
    [Fact]
    public async Task Blocked_transition_creates_operator_escalation_and_rework_actions_are_journaled()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await CreateAgentRunFixtureAsync(scope.ServiceProvider, "Escalation lifecycle read model");
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var escalationService = scope.ServiceProvider.GetRequiredService<IProcessEscalationService>();
        var runDetailsLoader = scope.ServiceProvider.GetRequiredService<ProcessWorkspaceRunDetailsLoader>();
        var stepRun = await StartStepAsync(processesService, fixture);

        var blockResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.Blocked,
                Reason = "Required artifacts still missing: implementation-report.md.",
                DecidedBy = "integration-tests"
            });

        Assert.True(blockResult.IsSuccess, string.Join(" | ", blockResult.Errors.Select(error => error.Message)));

        var escalation = Assert.Single(await escalationService.ListAsync(fixture.RunId));

        Assert.Equal(ProcessEscalationKind.BlockedStep, escalation.Kind);
        Assert.Equal(ProcessEscalationStatus.Open, escalation.Status);
        Assert.Equal(ProcessEscalationSourceKind.Journal, escalation.SourceKind);
        Assert.Equal(stepRun.Id, escalation.StepRunId);

        var assignResult = await escalationService.AssignAsync(
            new ProcessEscalationAssignmentRequest
            {
                EscalationId = escalation.Id,
                Owner = "integration-operator",
                AssignedBy = "integration-tests"
            });

        Assert.True(assignResult.IsSuccess, string.Join(" | ", assignResult.Errors.Select(error => error.Message)));
        var assignedEscalation = Assert.Single(await escalationService.ListAsync(fixture.RunId));

        Assert.Equal(ProcessEscalationStatus.Assigned, assignedEscalation.Status);
        Assert.Equal("integration-operator", assignedEscalation.Owner);

        var resolveResult = await escalationService.ResolveAsync(
            new ProcessEscalationResolutionRequest
            {
                EscalationId = escalation.Id,
                Resolution = "Operator confirmed the rework path is required.",
                ResolvedBy = "integration-tests"
            });

        Assert.True(resolveResult.IsSuccess, string.Join(" | ", resolveResult.Errors.Select(error => error.Message)));
        var resolvedEscalation = Assert.Single(await escalationService.ListAsync(fixture.RunId));

        Assert.Equal(ProcessEscalationStatus.Resolved, resolvedEscalation.Status);
        Assert.Contains("rework path", resolvedEscalation.Resolution, StringComparison.OrdinalIgnoreCase);

        var reopenResult = await escalationService.ReopenAsync(
            new ProcessEscalationReopenRequest
            {
                EscalationId = escalation.Id,
                Reason = "Reopen for targeted agent rework.",
                ReopenedBy = "integration-tests"
            });

        Assert.True(reopenResult.IsSuccess, string.Join(" | ", reopenResult.Errors.Select(error => error.Message)));

        var blockedStep = Assert.Single(await processesService.ListStepRunsAsync(fixture.RunId));
        var reworkResult = await escalationService.RequestReworkAsync(
            new ProcessEscalationReworkRequest
            {
                EscalationId = escalation.Id,
                StepRunConcurrencyToken = blockedStep.StepRunConcurrencyToken,
                Directive = "Repair only the missing implementation-report.md evidence projection.",
                RequestedBy = "integration-tests"
            });

        Assert.True(reworkResult.IsSuccess, string.Join(" | ", reworkResult.Errors.Select(error => error.Message)));

        var details = await runDetailsLoader.LoadAsync(fixture.RunId);
        var reworkEscalation = Assert.Single(details.Escalations, item => item.Id == escalation.Id);
        var rerunStep = Assert.Single(details.StepRuns);

        Assert.Equal(ProcessEscalationStatus.ReworkRequested, reworkEscalation.Status);
        Assert.NotNull(reworkEscalation.ReworkPacketId);
        Assert.Equal(ProcessStepRunStatus.InProgress, rerunStep.Status);
        Assert.Equal(ProcessRecoveryClassification.ManualRerun, rerunStep.Health.RecoveryClassification);
        Assert.Contains(details.AttemptTimeline, item => item.Kind == ProcessAttemptTimelineKind.ReworkPacket);
        Assert.Contains(details.AttemptTimeline, item => item.Kind == ProcessAttemptTimelineKind.ManualRerun);
        Assert.Contains(details.AttemptTimeline, item =>
            item.Kind == ProcessAttemptTimelineKind.Escalation &&
            item.Status == ProcessRuntimeEventTypes.ProcessEscalationReworkRequested);
    }

    [Fact]
    public async Task Runtime_read_model_exposes_missing_artifact_obligations_for_blocked_agent_steps()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await CreateAgentRunFixtureAsync(scope.ServiceProvider, "Missing artifact read model");
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var stepRun = await StartStepAsync(processesService, fixture);

        var blockResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.Blocked,
                Reason = "Required artifacts still missing: implementation-report.md.",
                DecidedBy = "integration-tests"
            });

        Assert.True(blockResult.IsSuccess, string.Join(" | ", blockResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetRunDetailsAsync(fixture.RunId);
        var blockedStep = Assert.Single(details.StepRuns);
        var obligation = Assert.Single(blockedStep.ArtifactExpectations);

        Assert.Equal(ProcessStepRunStatus.Blocked, blockedStep.Status);
        Assert.Equal(ProcessArtifactExpectationSatisfactionStatus.Missing, obligation.Status);
        Assert.Equal(ProcessRecoveryClassification.MissingArtifact, blockedStep.Health.RecoveryClassification);
        Assert.Contains("implementation-report.md", blockedStep.Health.ActionableReason, StringComparison.OrdinalIgnoreCase);
        Assert.True(blockedStep.Health.CanManualRerun);
    }

    [Fact]
    public async Task Runtime_read_model_ignores_missing_artifact_obligations_for_skipped_steps()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await CreateAgentRunFixtureAsync(scope.ServiceProvider, "Skipped missing artifact read model");
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var runDetailsLoader = scope.ServiceProvider.GetRequiredService<ProcessWorkspaceRunDetailsLoader>();
        var stepRun = Assert.Single(await processesService.ListStepRunsAsync(fixture.RunId));

        var skipResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.Skipped,
                Reason = "Skipped because the upstream branch did not require this lane.",
                DecidedBy = "integration-tests"
            });

        Assert.True(skipResult.IsSuccess, string.Join(" | ", skipResult.Errors.Select(error => error.Message)));

        var serviceDetails = await processesService.GetRunDetailsAsync(fixture.RunId);
        var skippedServiceStep = Assert.Single(serviceDetails.StepRuns);
        var loaderDetails = await runDetailsLoader.LoadAsync(fixture.RunId);
        var skippedLoaderStep = Assert.Single(loaderDetails.StepRuns);

        Assert.Equal(ProcessStepRunStatus.Skipped, skippedServiceStep.Status);
        Assert.Equal(ProcessRecoveryClassification.None, skippedServiceStep.Health.RecoveryClassification);
        Assert.DoesNotContain("Missing required artifacts", skippedServiceStep.Health.ActionableReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, loaderDetails.Health.MissingArtifactCount);
        Assert.Equal(ProcessRecoveryClassification.None, skippedLoaderStep.Health.RecoveryClassification);
    }

    [Fact]
    public async Task Manual_agent_rerun_records_recovery_directive_and_dispatch_outbox_record()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await CreateAgentRunFixtureAsync(scope.ServiceProvider, "Manual rerun read model");
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var runDetailsLoader = scope.ServiceProvider.GetRequiredService<ProcessWorkspaceRunDetailsLoader>();
        var stepRun = await StartStepAsync(processesService, fixture);

        var blockResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.Blocked,
                Reason = "Required artifacts still missing: implementation-report.md.",
                DecidedBy = "integration-tests"
            });

        Assert.True(blockResult.IsSuccess, string.Join(" | ", blockResult.Errors.Select(error => error.Message)));
        var blockedStep = Assert.Single(await processesService.ListStepRunsAsync(fixture.RunId));

        var rerunResult = await processesService.RerunAgentStepAsync(
            new ProcessAgentStepRerunRequest
            {
                StepRunId = blockedStep.Id,
                StepRunConcurrencyToken = blockedStep.StepRunConcurrencyToken,
                OperatorReason = "The first agent attempt lost context before writing the required report."
            });

        Assert.True(rerunResult.IsSuccess, string.Join(" | ", rerunResult.Errors.Select(error => error.Message)));

        var details = await runDetailsLoader.LoadAsync(fixture.RunId);
        var rerunStep = Assert.Single(details.StepRuns);
        var dispatchRecord = Assert.Single(
            details.OutboxRecords,
            item => item.StepRunId == rerunStep.Id && item.Trigger == ProcessRuntimeEventTypes.ManualAgentStepRerun);

        Assert.Equal(ProcessStepRunStatus.InProgress, rerunStep.Status);
        Assert.Equal(ProcessRecoveryClassification.ManualRerun, rerunStep.Health.RecoveryClassification);
        Assert.Contains("implementation-report.md", rerunStep.Health.ActionableReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ProcessOutboxHealthStatus.Pending, dispatchRecord.HealthStatus);
        Assert.Equal(ProcessRuntimeEventTypes.ManualAgentStepRerun, dispatchRecord.Trigger);
    }

    [Fact]
    public async Task Manual_agent_rerun_reopens_completed_agent_step_when_operator_invalidates_proof()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await CreateAgentRunFixtureAsync(scope.ServiceProvider, "Manual rerun completed read model");
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var runDetailsLoader = scope.ServiceProvider.GetRequiredService<ProcessWorkspaceRunDetailsLoader>();
        var stepRun = await StartStepAsync(processesService, fixture);
        var expectation = Assert.Single(Assert.Single((await processesService.GetRunDetailsAsync(fixture.RunId)).StepRuns).ArtifactExpectations);
        const string artifactPath = "artifacts/test/manual-rerun-completed-proof.md";
        await WriteWorkspaceArtifactAsync(
            application,
            artifactPath,
            "Implementation report evidence is present before the operator invalidates proof.");

        var artifactResult = await processesService.RecordArtifactAsync(
            new ProcessArtifactRecordRequest
            {
                ProcessRunId = fixture.RunId,
                StepRunId = stepRun.Id,
                ArtifactExpectationId = expectation.ArtifactExpectationId,
                ArtifactKind = expectation.ArtifactKind,
                Title = expectation.Title,
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ProcessSensitivityLevel.Internal,
                ProvenanceSummary = "Recorded by integration test before the operator invalidates proof.",
                AllowedFutureUsageSummary = "Manual rerun validation.",
                ReviewSummary = "Implementation report evidence is present.",
                ManagedStoragePath = artifactPath
            });
        Assert.True(artifactResult.IsSuccess, string.Join(" | ", artifactResult.Errors.Select(error => error.Message)));

        var completionResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.Completed,
                Reason = "Complete with the originally recorded proof.",
                DecidedBy = "integration-tests"
            });
        Assert.True(completionResult.IsSuccess, string.Join(" | ", completionResult.Errors.Select(error => error.Message)));
        var completedStep = Assert.Single(await processesService.ListStepRunsAsync(fixture.RunId));

        var rerunResult = await processesService.RerunAgentStepAsync(
            new ProcessAgentStepRerunRequest
            {
                StepRunId = completedStep.Id,
                StepRunConcurrencyToken = completedStep.StepRunConcurrencyToken,
                OperatorReason = "Observer invalidated the prior proof after completion; rerun the agent step with fresh evidence."
            });

        Assert.True(rerunResult.IsSuccess, string.Join(" | ", rerunResult.Errors.Select(error => error.Message)));

        var details = await runDetailsLoader.LoadAsync(fixture.RunId);
        var rerunStep = Assert.Single(details.StepRuns);

        Assert.Equal(ProcessStepRunStatus.InProgress, rerunStep.Status);
        Assert.Equal(ProcessRecoveryClassification.ManualRerun, rerunStep.Health.RecoveryClassification);
        Assert.Contains(details.OutboxRecords, item =>
            item.StepRunId == rerunStep.Id &&
            item.Trigger == ProcessRuntimeEventTypes.ManualAgentStepRerun &&
            item.HealthStatus == ProcessOutboxHealthStatus.Pending);
    }

    [Fact]
    public async Task Manual_agent_rerun_reopens_blocked_agent_step_inside_failed_run()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await CreateAgentRunFixtureAsync(scope.ServiceProvider, "Manual rerun blocked failed run");
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var runDetailsLoader = scope.ServiceProvider.GetRequiredService<ProcessWorkspaceRunDetailsLoader>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var stepRun = await StartStepAsync(processesService, fixture);

        var blockResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.Blocked,
                Reason = "Automation exhausted repair attempts and needs operator rerun.",
                DecidedBy = "integration-tests"
            });
        Assert.True(blockResult.IsSuccess, string.Join(" | ", blockResult.Errors.Select(error => error.Message)));

        await MarkRunFailedAsync(dbContextFactory, fixture.RunId);
        var blockedStep = Assert.Single(await processesService.ListStepRunsAsync(fixture.RunId));

        var rerunResult = await processesService.RerunAgentStepAsync(
            new ProcessAgentStepRerunRequest
            {
                StepRunId = blockedStep.Id,
                StepRunConcurrencyToken = blockedStep.StepRunConcurrencyToken,
                OperatorReason = "Operator is reopening a blocked repair step after process-core proof classification was fixed."
            });

        Assert.True(rerunResult.IsSuccess, string.Join(" | ", rerunResult.Errors.Select(error => error.Message)));

        var details = await runDetailsLoader.LoadAsync(fixture.RunId);
        var rerunStep = Assert.Single(details.StepRuns);

        Assert.Equal(ProcessStepRunStatus.InProgress, rerunStep.Status);
        Assert.Equal(ProcessRecoveryClassification.ManualRerun, rerunStep.Health.RecoveryClassification);
        Assert.Contains(details.OutboxRecords, item =>
            item.StepRunId == rerunStep.Id &&
            item.Trigger == ProcessRuntimeEventTypes.ManualAgentStepRerun &&
            item.HealthStatus == ProcessOutboxHealthStatus.Pending);
    }

    [Fact]
    public async Task TransitionStepAsync_settles_reopened_inprogress_step_inside_failed_run()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await CreateAgentRunFixtureAsync(scope.ServiceProvider, "Failed run reopened step settlement");
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var stepRun = await StartStepAsync(processesService, fixture);

        await MarkRunFailedAsync(dbContextFactory, fixture.RunId);

        var blockResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.Blocked,
                Reason = "Automation exhausted governed repair attempts and needs operator review.",
                DecidedBy = "process-automation-dispatcher"
            });

        Assert.True(blockResult.IsSuccess, string.Join(" | ", blockResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetRunDetailsAsync(fixture.RunId);
        var settledStep = Assert.Single(details.StepRuns);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var runStatus = await dbContext.Set<ProcessRun>()
            .Where(item => item.Id == fixture.RunId)
            .Select(item => item.Status)
            .SingleAsync();

        Assert.Equal(ProcessStepRunStatus.Blocked, settledStep.Status);
        Assert.Equal(ProcessRunStatus.Blocked, runStatus);
    }

    [Fact]
    public void Manual_rerun_directive_filters_previous_recovery_directive_text()
    {
        var buildDirective = typeof(ProcessesService).GetMethod("BuildManualRerunDirective", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildManualRerunDirective method was not found.");
        var stepRun = new ProcessStepRun
        {
            Title = "Implement feature, tests, and migration notes",
            BlockedReason = "AgentFramework run 'New exploration thread' blocked step: validation incomplete. Recovery directive: Typed recovery decision: mode=ReworkContinuation; category=HumanRequestedRerun. Target artifacts/files: output/Program.cs.",
            ExceptionSummary = "Finalizer tool was not called. Recovery details: old packet text that must not be replayed."
        };
        var request = new ProcessAgentStepRerunRequest
        {
            OperatorReason = "Operator requested a governed agent rerun from Process Workspace. Recovery directive: Typed recovery decision: old nested packet."
        };
        var decisions = new[]
        {
            new ProcessDecisionRecord
            {
                DecisionKind = ProcessDecisionKind.Assignment,
                Reason = "Operator requested a governed agent rerun from Process Workspace. Recovery directive: Typed recovery decision: previous packet."
            },
            new ProcessDecisionRecord
            {
                DecisionKind = ProcessDecisionKind.Exception,
                Reason = "AgentFramework run 'New exploration thread' failed: required finalizer was missing."
            }
        };

        var directive = buildDirective.Invoke(
            null,
            [
                request,
                stepRun,
                Array.Empty<ProcessArtifactExpectation>(),
                Array.Empty<ProcessArtifactRecord>(),
                decisions
            ]) as string;

        Assert.NotNull(directive);
        Assert.Contains("Operator requested a governed agent rerun", directive, StringComparison.Ordinal);
        Assert.Contains("Prior blocked reason: AgentFramework run", directive, StringComparison.Ordinal);
        Assert.Contains("Exception: AgentFramework run 'New exploration thread' failed", directive, StringComparison.Ordinal);
        Assert.DoesNotContain("Typed recovery decision", directive, StringComparison.Ordinal);
        Assert.DoesNotContain("Recovery directive:", directive, StringComparison.Ordinal);
        Assert.DoesNotContain("Recovery details:", directive, StringComparison.Ordinal);
        Assert.DoesNotContain("Target artifacts/files:", directive, StringComparison.Ordinal);
        Assert.DoesNotContain("Assignment:", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_rerun_transition_reason_does_not_store_rendered_recovery_directive()
    {
        var buildTransitionReason = typeof(ProcessesService).GetMethod("BuildManualRerunTransitionReason", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("BuildManualRerunTransitionReason method was not found.");
        var request = new ProcessAgentStepRerunRequest
        {
            OperatorReason = "Operator requested a governed agent rerun from Process Workspace."
        };

        var reason = buildTransitionReason.Invoke(
            null,
            [
                request,
                "Typed recovery decision: mode=ReworkContinuation; category=HumanRequestedRerun. Rework packet id: e99862ce-4713-4c9b-b590-6c9f024daa54."
            ]) as string;

        Assert.NotNull(reason);
        Assert.Contains("Operator requested a governed agent rerun", reason, StringComparison.Ordinal);
        Assert.Contains("manual rerun journal", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Typed recovery decision", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Recovery directive:", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("e99862ce-4713-4c9b-b590-6c9f024daa54", reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Runtime_read_model_projects_dead_lettered_automation_as_run_health()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await CreateAgentRunFixtureAsync(scope.ServiceProvider, "Dead-letter read model");
        var runDetailsLoader = scope.ServiceProvider.GetRequiredService<ProcessWorkspaceRunDetailsLoader>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var now = DateTimeOffset.UtcNow;

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            await dbContext.Set<ProcessOutboxRecord>().AddAsync(
                new ProcessOutboxRecord
                {
                    ProjectId = fixture.ProjectId,
                    ProcessDefinitionId = fixture.DefinitionId,
                    ProcessRunId = fixture.RunId,
                    CommandKey = "dispatch-run-automation",
                    PayloadJson = JsonSerializer.Serialize(
                        new ProcessOutboxPayload(
                            null,
                            null,
                            null,
                            new ProcessOutboxAutomationDispatchRequest(fixture.RunId, fixture.StepRunId, "dead-letter-proof")),
                        new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    Status = ProcessOutboxRecordStatus.DeadLettered,
                    AttemptCount = 3,
                    LastAttemptAtUtc = now,
                    LastError = "Provider execution failed after retry exhaustion.",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            await dbContext.SaveChangesAsync();
        }

        var details = await runDetailsLoader.LoadAsync(fixture.RunId);
        var outboxRecord = Assert.Single(details.OutboxRecords, item => item.HealthStatus == ProcessOutboxHealthStatus.DeadLettered);

        Assert.Equal(ProcessOutboxHealthStatus.DeadLettered, outboxRecord.HealthStatus);
        Assert.Equal(ProcessRecoveryClassification.OutboxDeadLetter, details.Health.RecoveryClassification);
        Assert.Contains("dead-lettered", details.Health.ActionableReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(details.Escalations, item =>
            item.Id == outboxRecord.Id &&
            item.Kind == ProcessEscalationKind.OutboxDeadLetter &&
            item.SourceKind == ProcessEscalationSourceKind.OutboxRecord);
        Assert.Contains(details.AttemptTimeline, item =>
            item.Kind == ProcessAttemptTimelineKind.Outbox &&
            item.OutboxRecordId == outboxRecord.Id);
    }

    [Fact]
    public async Task Runtime_invariant_diagnostics_SB13_INV_001_exposes_generic_audit_issues()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await CreateGenericRunFixtureAsync(scope.ServiceProvider, "Supplier reconciliation invariant diagnostics");
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var stepRun = await dbContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == fixture.StepRunId);
            var expectation = await dbContext.Set<ProcessArtifactExpectation>().SingleAsync(item => item.StepDefinitionId == stepRun.StepDefinitionId);
            var now = DateTimeOffset.UtcNow;
            var lineageJson = ProcessArtifactProjectionLineageJson.Serialize(
                new ProcessArtifactProjectionLineage
                {
                    SourceKind = ProcessArtifactProjectionSourceKind.Manual,
                    SourceExternalReferenceKey = "supplier-ledger:may-close"
                });

            stepRun.Status = ProcessStepRunStatus.Blocked;
            stepRun.BlockedReason = "Legacy import left the supplier reconciliation blocked without a typed recovery action.";
            stepRun.BlockReasonCode = ProcessStepBlockReasonCode.None;
            stepRun.RecoveryOptionsJson = "[]";
            stepRun.NextRecoveryAction = ProcessStepRecoveryOption.None;

            await dbContext.Set<ProcessArtifactRecord>().AddRangeAsync(
                new ProcessArtifactRecord
                {
                    ProcessRunId = fixture.RunId,
                    StepRunId = stepRun.Id,
                    ArtifactExpectationId = expectation.Id,
                    ArtifactKind = expectation.ArtifactKind,
                    Title = expectation.Title,
                    TrustStatus = ProcessArtifactTrustStatus.Draft,
                    SensitivityLevel = expectation.SensitivityLevel,
                    ProvenanceSummary = "Legacy supplier note imported without approval.",
                    ProjectionLineageJson = lineageJson,
                    CreatedAtUtc = now
                },
                new ProcessArtifactRecord
                {
                    ProcessRunId = fixture.RunId,
                    StepRunId = stepRun.Id,
                    ArtifactExpectationId = expectation.Id,
                    ArtifactKind = expectation.ArtifactKind,
                    Title = $"{expectation.Title} duplicate",
                    TrustStatus = ProcessArtifactTrustStatus.Draft,
                    SensitivityLevel = expectation.SensitivityLevel,
                    ProvenanceSummary = "Duplicate legacy supplier note imported without identity hash.",
                    ProjectionLineageJson = lineageJson,
                    CreatedAtUtc = now.AddSeconds(1)
                });
            await dbContext.SaveChangesAsync();
        }

        var diagnostics = await processesService.ListRuntimeInvariantDiagnosticsAsync(fixture.RunId);

        Assert.Contains(diagnostics, item =>
            item.Kind == ProcessRuntimeInvariantDiagnosticKind.WeakArtifactRecord &&
            item.StepTitle.Contains("Reconcile supplier statement", StringComparison.Ordinal));
        Assert.Contains(diagnostics, item =>
            item.Kind == ProcessRuntimeInvariantDiagnosticKind.DuplicateLineageIdentity &&
            item.RecommendedAction.Contains("deduplicate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(diagnostics, item =>
            item.Kind == ProcessRuntimeInvariantDiagnosticKind.BlockedRecoveryState &&
            item.RecommendedAction.Contains("reclassify", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TransitionStepAsync_SB13_INV_001_records_manual_transition_validation_failure()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var fixture = await CreateGenericRunFixtureAsync(scope.ServiceProvider, "Manual transition invariant diagnostics");
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var stepRun = Assert.Single(await processesService.ListStepRunsAsync(fixture.RunId));

        var invalidTransition = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.Completed,
                Reason = "Operator attempted to settle the step before it was ready.",
                DecidedBy = "integration-tests"
            });

        Assert.False(invalidTransition.IsSuccess);

        var diagnostics = await processesService.ListRuntimeInvariantDiagnosticsAsync(fixture.RunId);
        var diagnostic = Assert.Single(diagnostics, item => item.Kind == ProcessRuntimeInvariantDiagnosticKind.ManualTransitionValidationFailure);

        Assert.Equal(stepRun.Id, diagnostic.StepRunId);
        Assert.Contains("Cannot move step", diagnostic.Detail, StringComparison.Ordinal);
        Assert.Contains("Refresh", diagnostic.RecommendedAction, StringComparison.OrdinalIgnoreCase);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await dbContext.Set<ProcessJournalEntry>().AnyAsync(item =>
            item.ProcessRunId == fixture.RunId &&
            item.StepRunId == stepRun.Id &&
            item.EventType == ProcessRuntimeEventTypes.RuntimeInvariantViolationRecorded &&
            item.CorrelationId == diagnostic.EvidenceKey));
    }

    private static async Task<AgentRunFixture> CreateAgentRunFixtureAsync(IServiceProvider services, string name)
    {
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = services.GetRequiredService<PartyDirectoryService>();
        var processesService = services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, name);
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var definition = BuildDefinition(projectId, roleId, stepId);
        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = saveResult.Value,
                ProjectId = projectId,
                RunName = $"{name} run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Operator read-model validation."
            });

        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

        var partyId = await CreatePartyAsync(partyDirectoryService, $"{name} agent party");
        var assignmentResult = await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runResult.Value,
                RoleRequirementId = roleId,
                PartyId = partyId,
                DisplayName = $"{name} agent",
                ExecutorKind = "AI agent",
                BindingReason = "Integration test binds an agent-owned process step.",
                AllowsDirectMessaging = true
            });

        Assert.True(assignmentResult.IsSuccess, string.Join(" | ", assignmentResult.Errors.Select(error => error.Message)));

        var stepRun = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value));
        return new AgentRunFixture(projectId, saveResult.Value, runResult.Value, stepRun.Id);
    }

    private static async Task<AgentRunFixture> CreateGenericRunFixtureAsync(IServiceProvider services, string name)
    {
        var projectsService = services.GetRequiredService<ProjectsService>();
        var partyDirectoryService = services.GetRequiredService<PartyDirectoryService>();
        var processesService = services.GetRequiredService<ProcessesService>();
        var projectId = await CreateProjectAsync(projectsService, name);
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var definition = BuildGenericDefinition(projectId, roleId, stepId);
        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = saveResult.Value,
                ProjectId = projectId,
                RunName = $"{name} run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Generic runtime invariant validation."
            });

        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

        var partyId = await CreatePartyAsync(partyDirectoryService, $"{name} analyst");
        var assignmentResult = await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runResult.Value,
                RoleRequirementId = roleId,
                PartyId = partyId,
                DisplayName = $"{name} analyst",
                ExecutorKind = "person",
                BindingReason = "Integration test binds a generic reconciliation role.",
                AllowsDirectMessaging = true
            });

        Assert.True(assignmentResult.IsSuccess, string.Join(" | ", assignmentResult.Errors.Select(error => error.Message)));

        var stepRun = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value));
        return new AgentRunFixture(projectId, saveResult.Value, runResult.Value, stepRun.Id);
    }

    private static async Task<ProcessStepRunViewModel> StartStepAsync(ProcessesService processesService, AgentRunFixture fixture)
    {
        var stepRun = Assert.Single(await processesService.ListStepRunsAsync(fixture.RunId));
        var startResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.InProgress,
                Reason = "Start agent-owned work for operator read-model validation.",
                DecidedBy = "integration-tests"
            });

        Assert.True(startResult.IsSuccess, string.Join(" | ", startResult.Errors.Select(error => error.Message)));
        return Assert.Single(await processesService.ListStepRunsAsync(fixture.RunId));
    }

    private static async Task MarkRunFailedAsync(IDbContextFactory<AppDbContext> dbContextFactory, Guid runId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;
        var run = await dbContext.Set<ProcessRun>().SingleAsync(item => item.Id == runId);

        run.Status = ProcessRunStatus.Failed;
        run.CompletedAtUtc = now;
        run.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync();
    }

    private static ProcessDefinitionEditorModel BuildDefinition(Guid projectId, Guid roleId, Guid stepId)
    {
        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Operator agent run proof process",
            Summary = "Validates process operator read models for agent-backed steps.",
            ValueStatement = "Keep agent process recovery visible in the UI.",
            CustomerName = "Integration customer",
            OwnerName = "Integration owner",
            GovernancePolicySummary = "Required artifacts must remain explicit.",
            ChangeSummary = "Operator read-model validation.",
            ConstitutionRuleSummary = "Do not complete without required artifacts.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for deterministic tests.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "implementation-agent",
                    DisplayName = "Implementation agent",
                    Purpose = "Produce the required implementation report.",
                    StaffingIntent = "AI-owned validation lane.",
                    PreferredExecutorKind = "AI agent",
                    DefaultAllocationPercent = 100
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = stepId,
                    Key = "produce-implementation-report",
                    Title = "Produce implementation report",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Operator request and process context.",
                    OutputContractSummary = "Implementation report exists.",
                    EvidenceContractSummary = "implementation-report.md must be recorded.",
                    DecisionRightsSummary = "Agent completes only with required evidence.",
                    ExceptionPolicySummary = "Block when evidence is missing.",
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Id = Guid.NewGuid(),
                            ArtifactKind = ProcessArtifactKind.Deliverable,
                            Title = "implementation-report.md",
                            IsRequired = true,
                            ValidationRequirementSummary = "Agent must write and project implementation-report.md."
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildGenericDefinition(Guid projectId, Guid roleId, Guid stepId)
    {
        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Supplier reconciliation process",
            Summary = "Validates generic process runtime invariant diagnostics.",
            ValueStatement = "Keep reconciliation blockers and weak evidence visible.",
            CustomerName = "Operations customer",
            OwnerName = "Operations owner",
            GovernancePolicySummary = "Supplier reconciliation evidence must be approved before closure.",
            ChangeSummary = "Runtime invariant validation.",
            ConstitutionRuleSummary = "Do not complete reconciliation with weak evidence.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for deterministic tests.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "reconciliation-analyst",
                    DisplayName = "Reconciliation analyst",
                    Purpose = "Reconcile supplier statements against ledger records.",
                    StaffingIntent = "Operations-owned reconciliation role.",
                    PreferredExecutorKind = "person",
                    DefaultAllocationPercent = 100
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = stepId,
                    Key = "reconcile-supplier-statement",
                    Title = "Reconcile supplier statement",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Supplier statement and ledger period.",
                    OutputContractSummary = "Reconciliation note is approved.",
                    EvidenceContractSummary = "Approved reconciliation note must be recorded.",
                    DecisionRightsSummary = "Analyst completes only with approved evidence.",
                    ExceptionPolicySummary = "Block when evidence is weak or duplicated.",
                    TargetLeadHours = 4,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Id = Guid.NewGuid(),
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "Approved reconciliation note",
                            IsRequired = true,
                            TrustRequirement = ProcessArtifactTrustRequirement.HumanApproved,
                            ValidationRequirementSummary = "Reconciliation note must be reviewed and approved."
                        }
                    ]
                }
            ]
        };
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(
            new ProjectEditorModel
            {
                Name = name,
                Description = $"{name} description",
                Objective = $"{name} objective",
                CurrentPhase = "Execution"
            });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService partyDirectoryService, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(
            new PartyEditorModel
            {
                PartyType = PartyType.AiAgent,
                LifecycleStatus = PartyLifecycleStatus.Active,
                DisplayName = displayName,
                Summary = $"{displayName} summary",
                LastChangedBy = "integration-tests"
            });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task WriteWorkspaceArtifactAsync(
        TestApplication application,
        string relativePath,
        string content)
    {
        var fullPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);
    }

    private sealed record AgentRunFixture(
        Guid ProjectId,
        Guid DefinitionId,
        Guid RunId,
        Guid StepRunId);
}
