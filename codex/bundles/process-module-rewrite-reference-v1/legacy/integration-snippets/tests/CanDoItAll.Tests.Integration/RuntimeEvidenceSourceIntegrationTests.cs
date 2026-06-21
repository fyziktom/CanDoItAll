using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class RuntimeEvidenceSourceIntegrationTests
{
    [Fact]
    public async Task Process_runtime_evidence_provider_exposes_stable_source_grounded_items()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var processes = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var provider = scope.ServiceProvider.GetRequiredService<IProcessRuntimeEvidenceSourceProvider>();

        var runId = await SeedProcessRuntimeEvidenceAsync(processes, dbContextFactory);

        var first = await provider.ReadSnapshotAsync(new ProcessRuntimeEvidenceSourceRequest(runId, Take: 4));
        var second = await provider.ReadSnapshotAsync(new ProcessRuntimeEvidenceSourceRequest(runId, Take: 4));

        Assert.Equal(first.Manifest.SnapshotId, second.Manifest.SnapshotId);
        Assert.Equal(
            first.Items.Select(item => item.Id.Value),
            second.Items.Select(item => item.Id.Value));
        Assert.True(first.Manifest.TotalItemCount > first.Items.Count);
        Assert.True(first.Manifest.HasMore);
        Assert.NotNull(first.Manifest.NextCursor);
        Assert.Equal(MemorySourceSnapshotHashScope.PageScoped, first.Manifest.SnapshotHashScope);
        Assert.Equal(MemorySourceSnapshotProviderVersions.ProcessRuntime, first.Manifest.ProviderVersion);

        var resumed = await provider.ReadSnapshotAsync(new ProcessRuntimeEvidenceSourceRequest(
            runId,
            first.Manifest.NextCursor,
            Take: 100));
        Assert.DoesNotContain(
            resumed.Items,
            item => first.Items.Any(firstItem => firstItem.Id == item.Id));

        var full = await provider.ReadSnapshotAsync(new ProcessRuntimeEvidenceSourceRequest(runId, Take: 100));
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessRun);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessStepRun);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessRunAssignment);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessWorkBrief);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessDecision);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessArtifact);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessJournal);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessConformanceObservation);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessImprovementCandidate);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessWorkflowRunLink);

        var artifact = Assert.Single(full.Items, item => item.Title == "Confidential delivery evidence");
        Assert.Equal(MemorySourceAccessMode.Redacted, artifact.Permission.AccessMode);
        Assert.Equal(MemorySourceSensitivity.Confidential, artifact.Permission.Sensitivity);
        Assert.True(artifact.Permission.ContainsSensitivePayload);
        Assert.Equal("processes/output/evidence.md", artifact.StorageReference?.Locator);

        var journal = Assert.Single(full.Items, item => item.Title == "Secret replay entry");
        Assert.Contains("[REDACTED]", journal.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-token", journal.Content, StringComparison.Ordinal);
        Assert.Equal(MemorySourceHashClassification.RestrictedIntegrity, journal.HashPolicy.Classification);
        Assert.Equal(MemorySourceHashPayloadBasis.RawSensitivePayload, journal.HashPolicy.PayloadBasis);
        Assert.False(journal.HashPolicy.Exportable);

        var processRun = Assert.Single(full.Items, item => item.EntityKind == MemorySourceEntityKind.ProcessRun);
        Assert.Contains("[REDACTED]", processRun.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-runtime-secret123", processRun.Content, StringComparison.Ordinal);
        Assert.Equal(MemorySourceHashClassification.RestrictedIntegrity, processRun.HashPolicy.Classification);

        var wrongScopeCursor = MemorySourceSnapshotCursor.Create(
            MemorySourceKind.ProcessRuntime,
            Guid.NewGuid(),
            MemorySourceSnapshotProviderVersions.ProcessRuntime,
            1,
            first.Items[0].Id);
        var wrongScopeException = await Assert.ThrowsAsync<MemorySourceSnapshotCursorException>(async () =>
            await provider.ReadSnapshotAsync(new ProcessRuntimeEvidenceSourceRequest(runId, wrongScopeCursor, Take: 2)));
        Assert.Equal(MemorySourceSnapshotCursorFailureReason.ScopeMismatch, wrongScopeException.Reason);

        var staleCursor = MemorySourceSnapshotCursor.Create(
            MemorySourceKind.ProcessRuntime,
            runId,
            MemorySourceSnapshotProviderVersions.ProcessRuntime,
            1,
            MemorySourceItemId.Create(
                MemorySourceKind.ProcessRuntime,
                runId,
                MemorySourceEntityKind.ProcessRun,
                Guid.NewGuid().ToString("D")));
        var staleException = await Assert.ThrowsAsync<MemorySourceSnapshotCursorException>(async () =>
            await provider.ReadSnapshotAsync(new ProcessRuntimeEvidenceSourceRequest(runId, staleCursor, Take: 2)));
        Assert.Equal(MemorySourceSnapshotCursorFailureReason.StaleAnchor, staleException.Reason);
    }

    [Fact]
    public async Task Workflow_runtime_evidence_provider_exposes_redacted_events_and_artifacts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var workflowRuns = scope.ServiceProvider.GetRequiredService<IWorkflowRunStore>();
        var provider = scope.ServiceProvider.GetRequiredService<IWorkflowRuntimeEvidenceSourceProvider>();
        var runId = await SeedWorkflowRuntimeEvidenceAsync(workflowRuns);

        var first = await provider.ReadSnapshotAsync(new WorkflowRuntimeEvidenceSourceRequest(runId, Take: 2));
        var second = await provider.ReadSnapshotAsync(new WorkflowRuntimeEvidenceSourceRequest(runId, Take: 2));

        Assert.Equal(first.Manifest.SnapshotId, second.Manifest.SnapshotId);
        Assert.Equal(
            first.Items.Select(item => item.Id.Value),
            second.Items.Select(item => item.Id.Value));
        Assert.True(first.Manifest.HasMore);
        Assert.NotNull(first.Manifest.NextCursor);
        Assert.Equal(MemorySourceSnapshotHashScope.PageScoped, first.Manifest.SnapshotHashScope);
        Assert.Equal(MemorySourceSnapshotProviderVersions.WorkflowRuntime, first.Manifest.ProviderVersion);

        var resumed = await provider.ReadSnapshotAsync(new WorkflowRuntimeEvidenceSourceRequest(
            runId,
            first.Manifest.NextCursor,
            Take: 100));
        Assert.DoesNotContain(
            resumed.Items,
            item => first.Items.Any(firstItem => firstItem.Id == item.Id));

        var full = await provider.ReadSnapshotAsync(new WorkflowRuntimeEvidenceSourceRequest(runId, Take: 100));
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.WorkflowRun);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.WorkflowEvent);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.WorkflowExternalRequest);
        Assert.Contains(full.Items, item => item.EntityKind == MemorySourceEntityKind.WorkflowArtifact);

        var workflowEvent = Assert.Single(full.Items, item => item.EntityKind == MemorySourceEntityKind.WorkflowEvent);
        Assert.Equal(MemorySourceAccessMode.Redacted, workflowEvent.Permission.AccessMode);
        Assert.Contains("[REDACTED]", workflowEvent.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-token", workflowEvent.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-runtime-secret123", workflowEvent.Content, StringComparison.Ordinal);
        Assert.Equal(MemorySourceHashClassification.RestrictedIntegrity, workflowEvent.HashPolicy.Classification);
        Assert.Equal(MemorySourceHashPayloadBasis.RawSensitivePayload, workflowEvent.HashPolicy.PayloadBasis);
        Assert.False(workflowEvent.HashPolicy.Exportable);

        var externalRequest = Assert.Single(full.Items, item => item.EntityKind == MemorySourceEntityKind.WorkflowExternalRequest);
        Assert.Equal(MemorySourceAccessMode.Redacted, externalRequest.Permission.AccessMode);
        Assert.Contains("[REDACTED]", externalRequest.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("workflow-password", externalRequest.Content, StringComparison.Ordinal);
        Assert.Equal(MemorySourceHashClassification.RestrictedIntegrity, externalRequest.HashPolicy.Classification);

        var artifact = Assert.Single(full.Items, item => item.EntityKind == MemorySourceEntityKind.WorkflowArtifact);
        Assert.Equal("workflow-runtime", artifact.StorageReference?.Provider);
        Assert.Equal("workflows/runtime/report.md", artifact.StorageReference?.Locator);

        var invalidCursorException = await Assert.ThrowsAsync<MemorySourceSnapshotCursorException>(async () =>
            await provider.ReadSnapshotAsync(new WorkflowRuntimeEvidenceSourceRequest(
                runId,
                new MemorySourceSnapshotCursor("not-a-supported-cursor"),
                Take: 2)));
        Assert.Equal(MemorySourceSnapshotCursorFailureReason.InvalidFormat, invalidCursorException.Reason);
    }

    private static async Task<Guid> SeedProcessRuntimeEvidenceAsync(
        ProcessesService processes,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        var definition = CreateProcessDefinition();
        var saveResult = await processes.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processes.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var runResult = await processes.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            RunName = "Runtime evidence source run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration source snapshot validation."
        });
        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var run = await dbContext.Set<ProcessRun>().SingleAsync(item => item.Id == runResult.Value);
        var step = await dbContext.Set<ProcessStepRun>().SingleAsync(item => item.ProcessRunId == runResult.Value);
        var assignment = await dbContext.Set<ProcessRunAssignment>().SingleAsync(item => item.ProcessRunId == runResult.Value);
        var createdAtUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);

        run.GovernanceSnapshot = """{"policy":"standard","token":"super-secret-token"}""";
        run.PolicySnapshot = """{"approval":"required","apiKey":"sk-runtime-secret123"}""";
        run.UpdatedAtUtc = createdAtUtc;
        step.Status = ProcessStepRunStatus.InProgress;
        step.StartedAtUtc = createdAtUtc.AddMinutes(1);

        dbContext.Set<ProcessWorkBrief>().Add(new ProcessWorkBrief
        {
            ProcessRunId = run.Id,
            StepRunId = step.Id,
            Title = "Runtime source brief",
            WorkBriefText = "Collect source-grounded process evidence.",
            HandoffSummary = "Evidence moves to validation.",
            AssignmentReason = "Assigned by runtime snapshot test.",
            ExpectedOutcome = "Provider exposes a work brief item.",
            EvidenceExpectationSummary = "Stable source item with links.",
            CreatedAtUtc = createdAtUtc.AddMinutes(2)
        });
        dbContext.Set<ProcessDecisionRecord>().Add(new ProcessDecisionRecord
        {
            ProcessRunId = run.Id,
            StepRunId = step.Id,
            DecisionKind = ProcessDecisionKind.Approval,
            Outcome = ProcessDecisionOutcome.Approved,
            Title = "Proceed with evidence capture",
            Reason = "The provider boundary is ready for validation.",
            PolicyEvaluation = "Policy evaluation summary.",
            DecidedBy = "integration-tests",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            CreatedAtUtc = createdAtUtc.AddMinutes(3)
        });
        dbContext.Set<ProcessArtifactRecord>().Add(new ProcessArtifactRecord
        {
            ProcessRunId = run.Id,
            StepRunId = step.Id,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Confidential delivery evidence",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Confidential,
            ProvenanceSummary = "Produced by integration evidence seed.",
            AllowedFutureUsageSummary = "Architecture validation only.",
            ReviewSummary = "Requires human review.",
            ManagedStoragePath = "processes/output/evidence.md",
            CreatedAtUtc = createdAtUtc.AddMinutes(4)
        });
        dbContext.Set<ProcessJournalEntry>().Add(new ProcessJournalEntry
        {
            ProcessRunId = run.Id,
            StepRunId = step.Id,
            EventType = "memory.snapshot.test",
            Title = "Secret replay entry",
            Description = "Replay context should be redacted.",
            CorrelationId = "runtime-evidence-test",
            PolicyVersion = "test-policy",
            EnvironmentMode = "test",
            ReplayContextJson = """{"token":"super-secret-token","safe":"visible"}""",
            OccurredAtUtc = createdAtUtc.AddMinutes(5)
        });
        dbContext.Set<ProcessConformanceObservation>().Add(new ProcessConformanceObservation
        {
            ProcessRunId = run.Id,
            StepRunId = step.Id,
            Severity = ProcessConformanceSeverity.Moderate,
            Category = "EvidenceBoundary",
            Observation = "Runtime evidence boundary was exercised.",
            DeviationReason = "No deviation.",
            ContainsSensitiveAssessment = true,
            CreatedAtUtc = createdAtUtc.AddMinutes(6)
        });
        dbContext.Set<ProcessImprovementCandidate>().Add(new ProcessImprovementCandidate
        {
            ProcessDefinitionId = saveResult.Value,
            ProcessRunId = run.Id,
            Title = "Improve runtime evidence summaries",
            Category = "Architecture",
            ProblemSummary = "Downstream memory features need source-grounded runtime context.",
            EvidenceSummary = "Provider snapshots expose source provenance and hashes.",
            Status = ProcessImprovementStatus.Open,
            RequiresGovernanceReview = true,
            CreatedAtUtc = createdAtUtc.AddMinutes(7)
        });
        dbContext.Set<ProcessWorkflowRunLink>().Add(new ProcessWorkflowRunLink
        {
            ProcessRunId = run.Id,
            StepRunId = step.Id,
            AssignmentId = assignment.Id,
            WorkflowDefinitionId = Guid.NewGuid(),
            WorkflowVersionId = Guid.NewGuid(),
            WorkflowRunId = Guid.NewGuid(),
            WorkflowBackend = WorkflowRuntimeBackendKind.InProcess,
            WorkflowBackendRunId = "workflow-runtime-evidence-test",
            State = WorkflowRunState.Running,
            Summary = "Workflow linked from process evidence test.",
            CreatedAtUtc = createdAtUtc.AddMinutes(8),
            UpdatedAtUtc = createdAtUtc.AddMinutes(9)
        });

        await dbContext.SaveChangesAsync();
        return run.Id;
    }

    private static ProcessDefinitionEditorModel CreateProcessDefinition()
    {
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            Name = $"Runtime evidence process {Guid.NewGuid():N}",
            Summary = "Definition used to validate process runtime source snapshots.",
            ValueStatement = "Expose durable process evidence without memory coupling.",
            CustomerName = "Integration tests",
            OwnerName = "Runtime evidence owner",
            GovernanceNotes = "Redact stored policy payloads.",
            ChangeSummary = "Initial source snapshot validation definition.",
            GovernancePolicySummary = "Source evidence must carry provenance and permissions.",
            ConstitutionRuleSummary = "Do not hide source errors behind fallback behavior.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for integration tests.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "owner",
                    DisplayName = "Runtime evidence owner",
                    Purpose = "Owns validation evidence.",
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Runtime evidence role snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = stepId,
                    Key = "capture-evidence",
                    Title = "Capture evidence",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Source rows exist.",
                    OutputContractSummary = "Snapshot evidence is generated.",
                    EvidenceContractSummary = "Provider emits source items.",
                    DecisionRightsSummary = "Owner decides evidence sufficiency.",
                    ExceptionPolicySummary = "Fail explicitly on missing source data.",
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static async Task<WorkflowRunId> SeedWorkflowRuntimeEvidenceAsync(IWorkflowRunStore workflowRuns)
    {
        var runId = WorkflowRunId.New();
        var workflowId = WorkflowId.New();
        var versionId = WorkflowVersionId.New();
        var createdAtUtc = new DateTimeOffset(2026, 5, 15, 14, 0, 0, TimeSpan.Zero);

        await workflowRuns.SaveRunAsync(new WorkflowRunSnapshot(
            runId,
            workflowId,
            versionId,
            WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess,
            "backend-runtime-evidence-test",
            "Workflow runtime evidence validation.",
            createdAtUtc,
            createdAtUtc.AddMinutes(1)));
        await workflowRuns.SaveEventAsync(new WorkflowEventRecord(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.ExecutorCompleted,
            new WorkflowNodeId("llm"),
            "Executor completed with token=super-secret-token.",
            """{"apiKey":"sk-runtime-secret123","safe":"visible"}""",
            createdAtUtc.AddMinutes(2)));
        await workflowRuns.SaveExternalRequestAsync(new WorkflowExternalRequestRecord(
            WorkflowExternalRequestId.New(),
            runId,
            WorkflowExternalRequestKind.ToolApproval,
            new WorkflowNodeId("approval"),
            "approve_tool",
            """{"token":"super-secret-token","scope":"test"}""",
            """{"approved":true,"password":"workflow-password"}""",
            createdAtUtc.AddMinutes(3),
            createdAtUtc.AddMinutes(4)));
        await workflowRuns.SaveArtifactAsync(new WorkflowArtifactRecord(
            WorkflowArtifactId.New(),
            runId,
            WorkflowArtifactKind.File,
            new WorkflowNodeId("artifact"),
            "Runtime report",
            "text/markdown",
            "workflows/runtime/report.md",
            "Report generated by workflow evidence test.",
            createdAtUtc.AddMinutes(5)));

        return runId;
    }
}
