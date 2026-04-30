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

    private sealed record AgentRunFixture(
        Guid ProjectId,
        Guid DefinitionId,
        Guid RunId,
        Guid StepRunId);
}
