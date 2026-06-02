using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Collaboration;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.ProcessWorkflowRepairDefinitionTestFixture;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessesServiceIntegrationTests
{
    private static readonly JsonSerializerOptions StringEnumJsonOptions = CreateStringEnumJsonOptions();

    [Fact]
    public async Task StartRunAsync_prefills_project_role_binding_and_persists_runtime_signals()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process integration project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Process Lead");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Process runtime integration",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess);

        var assignments = await processesService.ListAssignmentsAsync(runResult.Value);
        var assignment = Assert.Single(assignments);
        Assert.Equal("Morgan Process Lead", assignment.DisplayName);
        Assert.False(assignment.IsCapabilityGap);

        var partyOptions = await processesService.ListPartyOptionsAsync(projectId);
        Assert.Contains(partyOptions, item => item.PartyId == managerPartyId);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        Assert.Equal(2, stepRuns.Count);
        Assert.Equal(ProcessStepRunStatus.Ready, stepRuns[0].Status);
        Assert.Equal(ProcessStepRunStatus.Pending, stepRuns[1].Status);

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = stepRuns[0].Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Integration runtime started.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = stepRuns[0].Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Integration intake completed.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var secondStep = Assert.Single(stepRuns, item => item.Sequence == 1);
        Assert.Equal(ProcessStepRunStatus.Ready, secondStep.Status);

        Assert.True((await processesService.ResolveAssignmentAsync(new ProcessAssignmentResolutionRequest
        {
            ProcessRunId = runResult.Value,
            RoleRequirementId = assignment.RoleRequirementId,
            PartyId = managerPartyId,
            DisplayName = "Morgan Process Lead",
            ExecutorKind = "person",
            BindingReason = "Confirmed by integration test.",
            IsFallback = false
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = secondStep.Id,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = "Delivery readiness evidence is missing.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var artifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = secondStep.Id,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Delivery readiness note",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "Captured during integration coverage.",
            AllowedFutureUsageSummary = "Regression and architecture review only.",
            ReviewSummary = "Human confirmation still required."
        });

        Assert.True(artifactResult.IsSuccess);

        var decisions = await processesService.ListDecisionRecordsAsync(runResult.Value);
        var artifacts = await processesService.ListArtifactsAsync(runResult.Value);
        var conformance = await processesService.ListConformanceObservationsAsync(runResult.Value);
        var workBriefs = await processesService.ListWorkBriefsAsync(runResult.Value);
        var improvements = await processesService.ListImprovementsAsync(saveResult.Value);
        var analytics = await processesService.GetAnalyticsAsync(saveResult.Value, projectId);
        var runDetails = await processesService.GetRunDetailsAsync(runResult.Value);

        Assert.NotEmpty(decisions);
        Assert.Contains(artifacts, item => item.Title == "Delivery readiness note");
        Assert.Contains(conformance, item => item.Category == nameof(ProcessStepRunStatus.Blocked));
        Assert.Equal(2, workBriefs.Count);
        Assert.NotEmpty(improvements);
        Assert.True(analytics.BlockedRuns >= 1);
        Assert.True(analytics.ImprovementCandidateCount >= 1);
        Assert.Equal(stepRuns.Select(item => item.Id), runDetails.StepRuns.Select(item => item.Id));
        Assert.Equal(decisions.Select(item => item.Id), runDetails.Decisions.Select(item => item.Id));
        Assert.Equal(artifacts.Select(item => item.Id), runDetails.Artifacts.Select(item => item.Id));
        Assert.Equal(assignments.Select(item => item.Id), runDetails.Assignments.Select(item => item.Id));
        Assert.Equal(workBriefs.Select(item => item.Id), runDetails.WorkBriefs.Select(item => item.Id));
        Assert.Equal(conformance.Select(item => item.Id), runDetails.ConformanceObservations.Select(item => item.Id));
    }

    [Fact]
    public async Task SaveAsync_SB09_INV_001_persists_explicit_artifact_output_mappings()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Explicit output mapping project");
        var childExpectationId = Guid.NewGuid();
        var definition = BuildDefinitionEditor(projectId, Guid.NewGuid());
        var artifact = Assert.Single(definition.Steps.SelectMany(step => step.ArtifactExpectations));
        artifact.WorkflowOutputId = "board-decision-output";
        artifact.WorkflowOutputName = "Board decision memo";
        artifact.WorkflowOutputKind = WorkflowArtifactKind.Json;
        artifact.SubprocessChildArtifactExpectationId = childExpectationId;

        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));
        var saved = await processesService.GetEditorAsync(saveResult.Value);
        var savedArtifact = Assert.Single(saved.Steps.SelectMany(step => step.ArtifactExpectations));
        Assert.Equal("board-decision-output", savedArtifact.WorkflowOutputId);
        Assert.Equal("Board decision memo", savedArtifact.WorkflowOutputName);
        Assert.Equal(WorkflowArtifactKind.Json, savedArtifact.WorkflowOutputKind);
        Assert.Equal(childExpectationId, savedArtifact.SubprocessChildArtifactExpectationId);
    }

    [Fact]
    public async Task TransitionStepAsync_rejects_late_transition_after_run_becomes_terminal()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Terminal transition guard project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Terminal transition guard run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration terminal run transition guard"
        });

        Assert.True(runResult.IsSuccess);

        var stepRun = Assert.Single(
            await processesService.ListStepRunsAsync(runResult.Value),
            item => item.Sequence == 0);
        var startResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = stepRun.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started before cancellation.",
            DecidedBy = "integration-tests"
        });

        Assert.True(startResult.IsSuccess);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var run = await dbContext.Set<ProcessRun>().SingleAsync(item => item.Id == runResult.Value);
            run.Status = ProcessRunStatus.Cancelled;
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            run.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        var lateCompletionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = stepRun.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Late in-flight automation completion.",
            DecidedBy = "process-automation-dispatch"
        });

        Assert.True(lateCompletionResult.IsFailure);
        Assert.Contains(lateCompletionResult.Errors, error => error.Code == "processes.run-terminal");
    }

    [Fact]
    public async Task TransitionStepAsync_requires_recorded_required_artifacts_before_completion()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process artifact gate project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Artifact Gate");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var managerRoleId = Guid.NewGuid();
        var definition = BuildDefinitionEditor(projectId, managerRoleId);
        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Process artifact gate run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess);

        var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completed intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var deliveryStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 1);
        var requiredArtifactExpectationId = Assert.Single(deliveryStep.ArtifactOutputs).ArtifactExpectationId;
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started delivery review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var failedCompletionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Attempting to complete without evidence.",
            DecidedBy = "integration-tests"
        });

        Assert.True(failedCompletionResult.IsFailure);
        Assert.Contains(failedCompletionResult.Errors, error => error.Code == "processes.step-completion-missing-required-artifacts");

        const string deliveryEvidencePath = "artifacts/test/delivery-readiness-evidence.md";
        await WriteWorkspaceArtifactAsync(application, deliveryEvidencePath, "Evidence is now present.");

        var recordedArtifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = deliveryStep.Id,
            ArtifactExpectationId = requiredArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Delivery readiness evidence",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "Recorded after the failed completion attempt.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Evidence is now present.",
            ManagedStoragePath = deliveryEvidencePath
        });

        Assert.True(recordedArtifactResult.IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Evidence recorded.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        Assert.Equal(ProcessStepRunStatus.Completed, stepRuns.Single(item => item.Id == deliveryStep.Id).Status);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedArtifact = await dbContext.Set<ProcessArtifactRecord>()
            .SingleAsync(item => item.Id == recordedArtifactResult.Value);
        Assert.Equal(requiredArtifactExpectationId, persistedArtifact.ArtifactExpectationId);
    }

    [Fact]
    public async Task TransitionStepAsync_SB03_INV_001_rejects_placeholder_required_artifact_on_manual_completion()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process placeholder artifact gate project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Placeholder Gate");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Process placeholder artifact gate run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess);

        var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completed intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var deliveryStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 1);
        var requiredArtifactExpectationId = Assert.Single(deliveryStep.ArtifactOutputs).ArtifactExpectationId;
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started delivery review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var recordedArtifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = deliveryStep.Id,
            ArtifactExpectationId = requiredArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Delivery readiness evidence",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "Recorded as a placeholder for manual transition validation.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Placeholder evidence only.",
            ManagedStoragePath = "artifacts/test/placeholder-delivery-readiness-evidence.md"
        });

        Assert.True(recordedArtifactResult.IsSuccess, FormatErrors(recordedArtifactResult.Errors));

        var completionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Attempting to complete with placeholder evidence.",
            DecidedBy = "integration-tests"
        });

        Assert.True(completionResult.IsFailure);
        Assert.Contains(completionResult.Errors, error => error.Code == "processes.step-completion-invalid-required-artifacts");
        Assert.Contains(completionResult.Errors, error => error.Message.Contains("PlaceholderOnly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TransitionStepAsync_SB03_INV_002_rejects_malformed_json_required_artifact_on_manual_completion()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process malformed JSON artifact gate project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan JSON Gate");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var managerRoleId = Guid.NewGuid();
        var definition = BuildDefinitionEditor(projectId, managerRoleId);
        var deliveryExpectation = definition.Steps
            .Single(step => string.Equals(step.Key, "delivery-review", StringComparison.Ordinal))
            .ArtifactExpectations
            .Single();
        deliveryExpectation.ValidationRequirementSummary = "Must be a JSON evidence artifact with machine-readable readiness facts.";
        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Process malformed JSON artifact gate run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess);

        var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completed intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var deliveryStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 1);
        var requiredArtifactExpectationId = Assert.Single(deliveryStep.ArtifactOutputs).ArtifactExpectationId;
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started delivery review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        const string malformedEvidencePath = "artifacts/test/delivery-readiness-evidence.json";
        await WriteWorkspaceArtifactAsync(application, malformedEvidencePath, "{ not valid json");

        var recordedArtifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = deliveryStep.Id,
            ArtifactExpectationId = requiredArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Delivery readiness evidence",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "Recorded for manual transition malformed JSON validation.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "json content: { \"ready\": true,",
            ManagedStoragePath = malformedEvidencePath
        });

        Assert.True(recordedArtifactResult.IsSuccess, FormatErrors(recordedArtifactResult.Errors));

        var completionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Attempting to complete with malformed JSON evidence.",
            DecidedBy = "integration-tests"
        });

        Assert.True(completionResult.IsFailure);
        Assert.Contains(completionResult.Errors, error => error.Code == "processes.step-completion-invalid-required-artifacts");
        Assert.Contains(completionResult.Errors, error => error.Message.Contains("malformed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TransitionStepAsync_SB08_INV_001_rejects_malformed_storage_backed_json_required_artifact_on_manual_completion()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var storageCatalogService = scope.ServiceProvider.GetRequiredService<IStorageCatalogService>();
        var storageRoot = Path.Combine(Path.GetTempPath(), $"process-manual-storage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(storageRoot);
        try
        {
            var projectId = await CreateProjectAsync(projectsService, "Manual storage-backed artifact gate project");
            var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Storage Gate");
            var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                PartyId = managerPartyId,
                Role = ProjectPartyAssignmentRole.Manager,
                IsPrimary = true,
                Source = "integration-tests"
            });

            Assert.True(assignmentResult.IsSuccess);

            var managerRoleId = Guid.NewGuid();
            var definition = BuildDefinitionEditor(projectId, managerRoleId);
            var deliveryExpectation = definition.Steps
                .Single(step => string.Equals(step.Key, "delivery-review", StringComparison.Ordinal))
                .ArtifactExpectations
                .Single();
            deliveryExpectation.ValidationRequirementSummary = "Must be a JSON evidence artifact with machine-readable readiness facts.";
            var saveResult = await processesService.SaveAsync(definition);

            Assert.True(saveResult.IsSuccess);
            Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

            var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
            {
                ProcessDefinitionId = saveResult.Value,
                ProjectId = projectId,
                RunName = "Manual storage-backed artifact gate run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Integration verification"
            });

            Assert.True(runResult.IsSuccess);

            var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
            Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
            {
                StepRunId = intakeStep.Id,
                TargetStatus = ProcessStepRunStatus.InProgress,
                Reason = "Started intake.",
                DecidedBy = "integration-tests"
            })).IsSuccess);
            Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
            {
                StepRunId = intakeStep.Id,
                TargetStatus = ProcessStepRunStatus.Completed,
                Reason = "Completed intake.",
                DecidedBy = "integration-tests"
            })).IsSuccess);

            var storage = await storageCatalogService.SaveAsync(new StorageCatalogRecord
            {
                Name = "Manual process artifact storage",
                ProviderKind = StorageProviderKind.FileSystem,
                EndpointOrRoot = storageRoot,
                IsEnabled = true,
                CapabilityMask = StorageCapability.Read | StorageCapability.Write
            });
            var locator = "manual/delivery-readiness-evidence.json";
            var fullStoragePath = Path.Combine(storageRoot, locator.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullStoragePath)!);
            await File.WriteAllTextAsync(fullStoragePath, "{ not valid json");
            var reference = new StorageObjectReference(
                storage.Id,
                StorageProviderKind.FileSystem,
                StorageLocatorKind.RelativePath,
                locator,
                "delivery-readiness-evidence.json",
                "application/json",
                new FileInfo(fullStoragePath).Length);

            var deliveryStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 1);
            var requiredArtifactExpectationId = Assert.Single(deliveryStep.ArtifactOutputs).ArtifactExpectationId;
            Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
            {
                StepRunId = deliveryStep.Id,
                TargetStatus = ProcessStepRunStatus.InProgress,
                Reason = "Started delivery review.",
                DecidedBy = "integration-tests"
            })).IsSuccess);

            var recordedArtifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
            {
                ProcessRunId = runResult.Value,
                StepRunId = deliveryStep.Id,
                ArtifactExpectationId = requiredArtifactExpectationId,
                ArtifactKind = ProcessArtifactKind.Evidence,
                Title = "Delivery readiness evidence",
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ProcessSensitivityLevel.Internal,
                ProvenanceSummary = "Recorded for manual transition storage-backed JSON validation.",
                AllowedFutureUsageSummary = "Integration verification only.",
                ReviewSummary = "Storage-backed JSON evidence.",
                ManagedStoragePath = StorageJson.SerializeReference(reference)
            });

            Assert.True(recordedArtifactResult.IsSuccess, FormatErrors(recordedArtifactResult.Errors));

            var completionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
            {
                StepRunId = deliveryStep.Id,
                TargetStatus = ProcessStepRunStatus.Completed,
                Reason = "Attempting to complete with malformed storage-backed JSON evidence.",
                DecidedBy = "integration-tests"
            });

            Assert.True(completionResult.IsFailure);
            Assert.Contains(completionResult.Errors, error => error.Code == "processes.step-completion-invalid-required-artifacts");
            Assert.Contains(completionResult.Errors, error => error.Message.Contains("malformed JSON", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task TransitionStepAsync_SB10_INV_001_rejects_stale_execution_lineage_required_artifact_on_manual_completion()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Manual stale artifact lineage gate project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Stale Lineage Gate");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Manual stale artifact lineage gate run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess);

        var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completed intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var deliveryStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 1);
        var requiredArtifactExpectationId = Assert.Single(deliveryStep.ArtifactOutputs).ArtifactExpectationId;
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started delivery review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var staleExecutionRunId = Guid.NewGuid();
        const string staleEvidencePath = "artifacts/test/stale-lineage-delivery-readiness-evidence.md";
        await WriteWorkspaceArtifactAsync(application, staleEvidencePath, "Evidence content is present but belongs to a stale execution lineage.");

        var recordedArtifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = deliveryStep.Id,
            ArtifactExpectationId = requiredArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Delivery readiness evidence",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = $"Recorded from stale execution run {staleExecutionRunId:D}.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Evidence content is present but stale.",
            ManagedStoragePath = staleEvidencePath,
            ExternalReferenceKey = $"workspace-written-artifact|{staleExecutionRunId:D}|{requiredArtifactExpectationId:D}|{staleEvidencePath}",
            ProjectionLineage = new ProcessArtifactProjectionLineage
            {
                SourceKind = ProcessArtifactProjectionSourceKind.WorkspaceWrite,
                SourceExecutionRunId = staleExecutionRunId,
                SourceExternalReferenceKey = $"workspace-written-artifact|{staleExecutionRunId:D}|{requiredArtifactExpectationId:D}|{staleEvidencePath}"
            }
        });

        Assert.True(recordedArtifactResult.IsSuccess, FormatErrors(recordedArtifactResult.Errors));

        var completionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Attempting to complete with stale lineage evidence.",
            DecidedBy = "integration-tests"
        });

        Assert.True(completionResult.IsFailure);
        Assert.Contains(completionResult.Errors, error => error.Code == "processes.step-completion-invalid-required-artifacts");
        Assert.Contains(completionResult.Errors, error => error.Message.Contains("StaleOrWrongRun", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TransitionStepAsync_SB01_INV_001_allows_automation_completion_with_matching_execution_lineage_required_artifact()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Automation artifact lineage gate project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Automation Lineage Gate");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Automation artifact lineage gate run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess);

        var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completed intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var deliveryStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 1);
        var requiredArtifactExpectationId = Assert.Single(deliveryStep.ArtifactOutputs).ArtifactExpectationId;
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started delivery review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var executionRunId = Guid.NewGuid();
        const string evidencePath = "artifacts/test/current-lineage-delivery-readiness-evidence.md";
        await WriteWorkspaceArtifactAsync(application, evidencePath, "Evidence content is present and belongs to the current automation execution lineage.");

        var recordedArtifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = deliveryStep.Id,
            ArtifactExpectationId = requiredArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Delivery readiness evidence",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = $"Recorded from automation execution run {executionRunId:D}.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Evidence content is current.",
            ManagedStoragePath = evidencePath,
            ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{requiredArtifactExpectationId:D}|{evidencePath}",
            ProjectionLineage = new ProcessArtifactProjectionLineage
            {
                SourceKind = ProcessArtifactProjectionSourceKind.WorkspaceWrite,
                SourceExecutionRunId = executionRunId,
                SourceExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{requiredArtifactExpectationId:D}|{evidencePath}"
            }
        });

        Assert.True(recordedArtifactResult.IsSuccess, FormatErrors(recordedArtifactResult.Errors));

        var completionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completing with current automation lineage evidence.",
            DecidedBy = ProcessRunAutomationDispatchService.AutomationActor,
            ArtifactValidationExecutorKind = ProcessRunAutomationDispatchService.ProcessStepCompletionExecutorKind.DirectAgent,
            ArtifactValidationExecutionRunId = executionRunId
        });

        Assert.True(completionResult.IsSuccess, FormatErrors(completionResult.Errors));

        var completedStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Id == deliveryStep.Id);

        Assert.Equal(ProcessStepRunStatus.Completed, completedStep.Status);
    }

    [Fact]
    public async Task TransitionStepAsync_SB01_INV_002_allows_automation_completion_when_transition_context_is_inferred_from_step_artifacts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Automation inferred artifact lineage gate project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Automation Inferred Lineage Gate");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Automation inferred artifact lineage gate run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess);

        var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completed intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var deliveryStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 1);
        var requiredArtifactExpectationId = Assert.Single(deliveryStep.ArtifactOutputs).ArtifactExpectationId;
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started delivery review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var executionRunId = Guid.NewGuid();
        const string evidencePath = "artifacts/test/inferred-lineage-delivery-readiness-evidence.md";
        var externalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{requiredArtifactExpectationId:D}|{evidencePath}";

        await WriteWorkspaceArtifactAsync(
            application,
            evidencePath,
            "Evidence content is present and belongs to the automation execution lineage inferred from the recorded artifact.");

        var recordedArtifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = deliveryStep.Id,
            ArtifactExpectationId = requiredArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Delivery readiness evidence",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = $"Recorded from automation execution run {executionRunId:D}.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Evidence content is current.",
            ManagedStoragePath = evidencePath,
            ExternalReferenceKey = externalReferenceKey,
            ProjectionLineage = new ProcessArtifactProjectionLineage
            {
                SourceKind = ProcessArtifactProjectionSourceKind.WorkspaceWrite,
                SourceExecutionRunId = executionRunId,
                ProjectedExecutionRunId = executionRunId,
                SourceExternalReferenceKey = externalReferenceKey
            }
        });

        Assert.True(recordedArtifactResult.IsSuccess, FormatErrors(recordedArtifactResult.Errors));

        var completionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completing with current automation lineage evidence inferred from artifacts.",
            DecidedBy = ProcessRunAutomationDispatchService.AutomationActor
        });

        Assert.True(completionResult.IsSuccess, FormatErrors(completionResult.Errors));

        var completedStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Id == deliveryStep.Id);

        Assert.Equal(ProcessStepRunStatus.Completed, completedStep.Status);
    }

    [Fact]
    public async Task TransitionStepAsync_allows_repair_branch_without_positive_required_artifact()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process repair branch artifact gate project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Repair Branch");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var definition = BuildRepairBranchArtifactGateDefinitionEditor(projectId, Guid.NewGuid());
        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess, FormatErrors(saveResult.Errors));
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Repair branch artifact gate run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess);

        var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completed intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var qaStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Title == "Validate implementation");
        var acceptedOutcomeId = qaStep.AvailableBranchOutcomes.Single(item => item.Title == "Quality accepted").Id;
        var repairOutcomeId = qaStep.AvailableBranchOutcomes.Single(item => item.Title == "Repair required").Id;
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = qaStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started QA review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var positiveCompletionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = qaStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            SelectedBranchOutcomeId = acceptedOutcomeId,
            Reason = "Attempting to accept without validation evidence.",
            DecidedBy = "integration-tests"
        });

        Assert.True(positiveCompletionResult.IsFailure);
        Assert.Contains(positiveCompletionResult.Errors, error => error.Code == "processes.step-completion-missing-required-artifacts");

        var repairCompletionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = qaStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            SelectedBranchOutcomeId = repairOutcomeId,
            Reason = "QA found reproducible defects and routes the work to repair.",
            DecidedBy = "integration-tests"
        });

        Assert.True(repairCompletionResult.IsSuccess, FormatErrors(repairCompletionResult.Errors));

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        Assert.Equal(ProcessStepRunStatus.Ready, stepRuns.Single(item => item.Title == "Repair implementation").Status);
        Assert.Equal(ProcessStepRunStatus.Skipped, stepRuns.Single(item => item.Title == "Approve release").Status);
    }

    [Fact]
    public async Task TransitionStepAsync_replaces_pending_decision_record_summary_on_completion()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Decision summary project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Decision");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var definition = BuildDefinitionEditor(projectId, Guid.NewGuid());
        definition.Steps[1].RequiresDecisionRecord = true;
        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Decision summary run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess);

        var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completed intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var deliveryStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 1);
        Assert.Equal("Decision record required.", deliveryStep.DecisionSummary);

        var requiredArtifactExpectationId = Assert.Single(deliveryStep.ArtifactOutputs).ArtifactExpectationId;
        const string decisionEvidencePath = "artifacts/test/decision-summary-evidence.md";
        await WriteWorkspaceArtifactAsync(application, decisionEvidencePath, "Evidence is present.");

        var recordedArtifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = deliveryStep.Id,
            ArtifactExpectationId = requiredArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Delivery readiness evidence",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "Recorded for decision summary completion.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Evidence is present.",
            ManagedStoragePath = decisionEvidencePath
        });

        Assert.True(recordedArtifactResult.IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started decision step.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Evidence recorded and approved.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var completedDeliveryStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Id == deliveryStep.Id);
        Assert.Equal(ProcessStepRunStatus.Completed, completedDeliveryStep.Status);
        Assert.Equal("Evidence recorded and approved.", completedDeliveryStep.DecisionSummary);
    }

    [Fact]
    public async Task TransitionStepAsync_accepts_required_artifact_recorded_by_title_without_explicit_expectation_id()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process artifact title resolution project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Artifact Title Resolution");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var managerRoleId = Guid.NewGuid();
        var definition = BuildDefinitionEditor(projectId, managerRoleId);
        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Process artifact title resolution run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess);

        var intakeStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Completed intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var deliveryStep = (await processesService.ListStepRunsAsync(runResult.Value)).Single(item => item.Sequence == 1);
        var requiredArtifact = Assert.Single(deliveryStep.ArtifactOutputs);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started delivery review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        const string titleMatchedEvidencePath = "artifacts/test/title-matched-delivery-readiness-evidence.md";
        await WriteWorkspaceArtifactAsync(application, titleMatchedEvidencePath, "Expectation id should be inferred.");

        var recordedArtifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = deliveryStep.Id,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = requiredArtifact.Title,
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "Recorded with title-only matching.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Expectation id should be inferred.",
            ManagedStoragePath = titleMatchedEvidencePath
        });

        Assert.True(recordedArtifactResult.IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = deliveryStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Evidence recorded by title.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        Assert.Equal(ProcessStepRunStatus.Completed, stepRuns.Single(item => item.Id == deliveryStep.Id).Status);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedArtifact = await dbContext.Set<ProcessArtifactRecord>()
            .SingleAsync(item => item.Id == recordedArtifactResult.Value);
        Assert.Equal(requiredArtifact.ArtifactExpectationId, persistedArtifact.ArtifactExpectationId);
    }

    [Fact]
    public async Task RecordArtifactAsync_normalizes_null_optional_text_fields()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Nullable process artifact fields project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Nullable artifact field validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify optional artifact text fields are normalized."
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        var recordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = firstStep.Id,
            ArtifactKind = ProcessArtifactKind.Brief,
            Title = "Nullable optional proof",
            ProvenanceSummary = null!,
            AllowedFutureUsageSummary = null!,
            ReviewSummary = null!,
            ManagedStoragePath = null!,
            ExternalReferenceKey = null!
        });

        Assert.True(recordResult.IsSuccess);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var artifact = await dbContext.Set<ProcessArtifactRecord>()
            .SingleAsync(item => item.Id == recordResult.Value);

        Assert.Equal(string.Empty, artifact.ProvenanceSummary);
        Assert.Equal(string.Empty, artifact.AllowedFutureUsageSummary);
        Assert.Equal(string.Empty, artifact.ReviewSummary);
        Assert.Equal(string.Empty, artifact.ManagedStoragePath);
        Assert.Equal(string.Empty, artifact.ExternalReferenceKey);
    }

    [Fact]
    public async Task RecordArtifactAsync_returns_existing_record_for_duplicate_external_reference_key()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process artifact deduplication project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Artifact deduplication validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify artifact idempotency"
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        var externalReferenceKey = "agentframework-artifact:dedupe-proof";
        var firstRecordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = firstStep.Id,
            ArtifactKind = ProcessArtifactKind.Brief,
            Title = "Scope boundary packet",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "First projection pass.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "First record.",
            ManagedStoragePath = "artifacts/test/scope-boundary-packet.md",
            ExternalReferenceKey = externalReferenceKey
        });

        Assert.True(firstRecordResult.IsSuccess);

        var duplicateRecordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = firstStep.Id,
            ArtifactKind = ProcessArtifactKind.Brief,
            Title = "Scope boundary packet",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "Second projection pass.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Duplicate record should be collapsed.",
            ManagedStoragePath = "artifacts/test/scope-boundary-packet.md",
            ExternalReferenceKey = externalReferenceKey
        });

        Assert.True(duplicateRecordResult.IsSuccess);
        Assert.Equal(firstRecordResult.Value, duplicateRecordResult.Value);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var matchingArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item =>
                item.ProcessRunId == runResult.Value &&
                item.ExternalReferenceKey == externalReferenceKey)
            .ToListAsync();

        var persistedArtifact = Assert.Single(matchingArtifacts);
        Assert.Equal(firstRecordResult.Value, persistedArtifact.Id);
    }

    [Fact]
    public async Task RecordArtifactAsync_bounds_long_external_reference_key_and_keeps_deduplication()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Long artifact reference key project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Long artifact reference key validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify managed artifact projection keys stay durable."
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        var longRelativePath = string.Join("/", Enumerable.Repeat("nested-artifact-folder", 12)) + "/implementation-change-set.md";
        var externalReferenceKey =
            $"existing-managed-artifact|{Guid.NewGuid():D}|{Guid.NewGuid():D}|{longRelativePath}";

        Assert.True(externalReferenceKey.Length > 200);

        var firstRecordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = firstStep.Id,
            ArtifactKind = ProcessArtifactKind.Brief,
            Title = "Long managed artifact projection key",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "First long-key projection pass.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "First record should be stored with a bounded reference key.",
            ManagedStoragePath = "artifacts/test/long-managed-artifact-projection-key.md",
            ExternalReferenceKey = externalReferenceKey
        });

        Assert.True(firstRecordResult.IsSuccess, string.Join(" | ", firstRecordResult.Errors.Select(error => error.Message)));

        var duplicateRecordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = firstStep.Id,
            ArtifactKind = ProcessArtifactKind.Brief,
            Title = "Long managed artifact projection key",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "Second long-key projection pass.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Duplicate long key should resolve to the same stored record.",
            ManagedStoragePath = "artifacts/test/long-managed-artifact-projection-key.md",
            ExternalReferenceKey = externalReferenceKey
        });

        Assert.True(duplicateRecordResult.IsSuccess, string.Join(" | ", duplicateRecordResult.Errors.Select(error => error.Message)));
        Assert.Equal(firstRecordResult.Value, duplicateRecordResult.Value);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runResult.Value)
            .ToListAsync();

        var persistedArtifact = Assert.Single(persistedArtifacts);
        Assert.Equal(firstRecordResult.Value, persistedArtifact.Id);
        Assert.True(persistedArtifact.ExternalReferenceKey.Length <= 200);
        Assert.StartsWith("existing-managed-artifact|", persistedArtifact.ExternalReferenceKey, StringComparison.Ordinal);
        Assert.Contains('#', persistedArtifact.ExternalReferenceKey);
    }

    [Fact]
    public async Task RecordArtifactAsync_SB05_INV_001_dedupes_by_projection_identity_hash_before_display_key()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Projection identity artifact project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Projection identity validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify typed artifact lineage identity."
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        var workflowRunId = Guid.NewGuid();
        var workflowArtifactId = Guid.NewGuid();
        var lineage = new ProcessArtifactProjectionLineage
        {
            SourceKind = ProcessArtifactProjectionSourceKind.WorkflowArtifact,
            WorkflowRunId = workflowRunId,
            WorkflowArtifactId = workflowArtifactId,
            ContentHash = "sha256:abcdef"
        };

        var firstRecordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = firstStep.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Workflow mapped deliverable",
            ManagedStoragePath = "artifacts/workflow/mapped-deliverable.md",
            ExternalReferenceKey = $"display-key:{Guid.NewGuid():N}",
            ProjectionLineage = lineage
        });

        Assert.True(firstRecordResult.IsSuccess, string.Join(" | ", firstRecordResult.Errors.Select(error => error.Message)));

        var duplicateRecordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = firstStep.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Workflow mapped deliverable duplicate",
            ManagedStoragePath = "artifacts/workflow/mapped-deliverable-copy.md",
            ExternalReferenceKey = $"different-display-key:{Guid.NewGuid():N}",
            ProjectionLineage = new ProcessArtifactProjectionLineage
            {
                SourceKind = ProcessArtifactProjectionSourceKind.WorkflowArtifact,
                WorkflowRunId = workflowRunId,
                WorkflowArtifactId = workflowArtifactId,
                ContentHash = "sha256:abcdef"
            }
        });

        Assert.True(duplicateRecordResult.IsSuccess, string.Join(" | ", duplicateRecordResult.Errors.Select(error => error.Message)));
        Assert.Equal(firstRecordResult.Value, duplicateRecordResult.Value);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedArtifact = Assert.Single(await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runResult.Value)
            .ToListAsync());
        Assert.StartsWith("sha256:", persistedArtifact.ProjectionIdentityHash, StringComparison.Ordinal);
        Assert.Contains(persistedArtifact.ProjectionIdentityHash, persistedArtifact.ProjectionLineageJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordArtifactAsync_SB11_INV_001_rejects_projection_identity_for_wrong_step_expectation_scope()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Projection identity scoped artifact project");
        var managerRoleId = Guid.NewGuid();
        var definition = BuildDefinitionEditor(projectId, managerRoleId);
        var intakeArtifactExpectationId = Guid.NewGuid();
        definition.Steps[0].ArtifactExpectations =
        [
            new ProcessArtifactExpectationEditorModel
            {
                Id = intakeArtifactExpectationId,
                ArtifactKind = ProcessArtifactKind.Evidence,
                Title = "Intake evidence",
                ValidationRequirementSummary = "Must stay scoped to the intake step."
            }
        ];

        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Projection identity scope validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify projection identity cannot cross step and expectation scope."
        });

        Assert.True(runResult.IsSuccess);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var intakeStep = Assert.Single(stepRuns, item => item.Sequence == 0);
        var deliveryStep = Assert.Single(stepRuns, item => item.Sequence == 1);
        var intakeArtifact = Assert.Single(intakeStep.ArtifactOutputs);
        var deliveryArtifact = Assert.Single(deliveryStep.ArtifactOutputs);
        var executionRunId = Guid.NewGuid();
        var lineage = new ProcessArtifactProjectionLineage
        {
            SourceKind = ProcessArtifactProjectionSourceKind.WorkspaceWrite,
            SourceExecutionRunId = executionRunId,
            ProjectedExecutionRunId = executionRunId,
            SourceExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{intakeArtifact.ArtifactExpectationId:D}|artifacts/process-runs/{runResult.Value:D}/shared-evidence.md",
            ContentHash = "sha256:scoped-proof"
        };
        var firstRecordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = intakeStep.Id,
            ArtifactExpectationId = intakeArtifact.ArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = intakeArtifact.Title,
            ManagedStoragePath = "artifacts/process-runs/current/intake-evidence.md",
            ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{intakeArtifact.ArtifactExpectationId:D}|artifacts/process-runs/current/intake-evidence.md",
            ProjectionLineage = lineage
        });

        Assert.True(firstRecordResult.IsSuccess, string.Join(" | ", firstRecordResult.Errors.Select(error => error.Message)));

        var wrongScopeResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = deliveryStep.Id,
            ArtifactExpectationId = deliveryArtifact.ArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = deliveryArtifact.Title,
            ManagedStoragePath = "artifacts/process-runs/current/delivery-evidence.md",
            ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{deliveryArtifact.ArtifactExpectationId:D}|artifacts/process-runs/current/delivery-evidence.md",
            ProjectionLineage = new ProcessArtifactProjectionLineage
            {
                SourceKind = lineage.SourceKind,
                SourceExecutionRunId = lineage.SourceExecutionRunId,
                ProjectedExecutionRunId = lineage.ProjectedExecutionRunId,
                SourceExternalReferenceKey = lineage.SourceExternalReferenceKey,
                ContentHash = lineage.ContentHash
            }
        });

        Assert.False(wrongScopeResult.IsSuccess);
        Assert.Contains(
            wrongScopeResult.Errors,
            error => error.Code == "processes.artifact.projection-scope-conflict");

        var externalReferenceKey = $"scoped-external-reference:{Guid.NewGuid():N}";
        var scopedExternalReferenceResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = intakeStep.Id,
            ArtifactExpectationId = intakeArtifact.ArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Intake external reference evidence",
            ManagedStoragePath = "artifacts/process-runs/current/intake-external-reference.md",
            ExternalReferenceKey = externalReferenceKey
        });

        Assert.True(scopedExternalReferenceResult.IsSuccess, string.Join(" | ", scopedExternalReferenceResult.Errors.Select(error => error.Message)));

        var wrongExternalReferenceScopeResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = deliveryStep.Id,
            ArtifactExpectationId = deliveryArtifact.ArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Delivery external reference evidence",
            ManagedStoragePath = "artifacts/process-runs/current/delivery-external-reference.md",
            ExternalReferenceKey = externalReferenceKey
        });

        Assert.False(wrongExternalReferenceScopeResult.IsSuccess);
        Assert.Contains(
            wrongExternalReferenceScopeResult.Errors,
            error => error.Code == "processes.artifact.external-reference-scope-conflict");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runResult.Value)
            .ToListAsync();
        Assert.Equal(2, persistedArtifacts.Count);

        var persistedArtifact = Assert.Single(persistedArtifacts, item => item.Id == firstRecordResult.Value);
        Assert.Equal(firstRecordResult.Value, persistedArtifact.Id);
        Assert.Equal(intakeStep.Id, persistedArtifact.StepRunId);
        Assert.Equal(intakeArtifact.ArtifactExpectationId, persistedArtifact.ArtifactExpectationId);

        var persistedExternalReferenceArtifact = Assert.Single(persistedArtifacts, item => item.Id == scopedExternalReferenceResult.Value);
        Assert.Equal(intakeStep.Id, persistedExternalReferenceArtifact.StepRunId);
        Assert.Equal(intakeArtifact.ArtifactExpectationId, persistedExternalReferenceArtifact.ArtifactExpectationId);
    }

    [Fact]
    public async Task RecordArtifactAsync_SB02_INV_001_dedupes_long_display_keys_by_projection_identity_hash()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Projection identity long key project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Projection identity long key validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify normalized projection identity beats bounded display keys."
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        var workflowRunId = Guid.NewGuid();
        var workflowArtifactId = Guid.NewGuid();
        var commonPrefix = $"workflow-output|{workflowRunId:D}|{workflowArtifactId:D}|";
        var firstExternalReferenceKey = commonPrefix + new string('a', 300);
        var secondExternalReferenceKey = commonPrefix + new string('b', 300);

        var firstRecordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = firstStep.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Workflow mapped deliverable",
            ManagedStoragePath = "artifacts/workflow/long-key-deliverable.md",
            ExternalReferenceKey = firstExternalReferenceKey,
            ProjectionLineage = new ProcessArtifactProjectionLineage
            {
                SourceKind = ProcessArtifactProjectionSourceKind.WorkflowArtifact,
                WorkflowRunId = workflowRunId,
                WorkflowArtifactId = workflowArtifactId,
                ContentHash = "sha256:long-key-proof"
            }
        });

        Assert.True(firstRecordResult.IsSuccess, string.Join(" | ", firstRecordResult.Errors.Select(error => error.Message)));

        var duplicateRecordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = firstStep.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Workflow mapped deliverable duplicate",
            ManagedStoragePath = "artifacts/workflow/long-key-deliverable-copy.md",
            ExternalReferenceKey = secondExternalReferenceKey,
            ProjectionLineage = new ProcessArtifactProjectionLineage
            {
                SourceKind = ProcessArtifactProjectionSourceKind.WorkflowArtifact,
                WorkflowRunId = workflowRunId,
                WorkflowArtifactId = workflowArtifactId,
                ContentHash = "sha256:long-key-proof"
            }
        });

        Assert.True(duplicateRecordResult.IsSuccess, string.Join(" | ", duplicateRecordResult.Errors.Select(error => error.Message)));
        Assert.Equal(firstRecordResult.Value, duplicateRecordResult.Value);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedArtifact = Assert.Single(await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runResult.Value)
            .ToListAsync());
        Assert.StartsWith("sha256:", persistedArtifact.ProjectionIdentityHash, StringComparison.Ordinal);
        Assert.Contains(persistedArtifact.ProjectionIdentityHash, persistedArtifact.ProjectionLineageJson, StringComparison.Ordinal);
        Assert.Contains('#', persistedArtifact.ExternalReferenceKey);
    }

    [Fact]
    public async Task RecordArtifactAsync_SB10_INV_001_computes_missing_workspace_content_hash()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Workspace content hash project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Workspace content hash validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify missing projection content hash is computed from managed storage."
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        var executionRunId = Guid.NewGuid();
        var relativePath = $"artifacts/process-runs/{runResult.Value:D}/workspace-content-hash.md";
        var content = "# Current evidence\n\nHash this managed artifact.";
        var fullPath = Path.Combine(application.ActiveProfile.WorkspaceRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);

        var recordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = firstStep.Id,
            ArtifactKind = ProcessArtifactKind.Deliverable,
            Title = "Workspace content hash deliverable",
            ManagedStoragePath = relativePath,
            ExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{Guid.NewGuid():D}|{relativePath}",
            ProjectionLineage = new ProcessArtifactProjectionLineage
            {
                SourceKind = ProcessArtifactProjectionSourceKind.WorkspaceWrite,
                SourceExecutionRunId = executionRunId,
                ProjectedExecutionRunId = executionRunId,
                SourceExternalReferenceKey = $"workspace-written-artifact|{executionRunId:D}|{Guid.NewGuid():D}|{relativePath}",
                ContentHash = string.Empty
            }
        });

        Assert.True(recordResult.IsSuccess, string.Join(" | ", recordResult.Errors.Select(error => error.Message)));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedArtifact = Assert.Single(await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runResult.Value)
            .ToListAsync());
        var lineage = ProcessArtifactProjectionLineageJson.Deserialize(persistedArtifact.ProjectionLineageJson);

        Assert.NotNull(lineage);
        Assert.Equal(ProcessArtifactIdentityService.ComputeContentHash(Encoding.UTF8.GetBytes(content)), lineage.ContentHash);
        Assert.Equal(persistedArtifact.ProjectionIdentityHash, lineage.ProjectionIdentityHash);
        Assert.StartsWith("sha256:", persistedArtifact.ProjectionIdentityHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordArtifactAsync_SB01_INV_001_reactivates_blocked_downstream_with_tracked_materialized_artifact()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Materialization reactivation project");
        var saveResult = await processesService.SaveAsync(BuildArtifactMaterializationReactivationDefinitionEditor(projectId, Guid.NewGuid()));

        AssertSuccess(saveResult);
        AssertSuccess(await processesService.PublishAsync(saveResult.Value));

        var runId = await StartTestRunAsync(
            processesService,
            saveResult.Value,
            projectId,
            "Materialization reactivation run");
        var initialStepRuns = await processesService.ListStepRunsAsync(runId);
        var upstreamStep = Assert.Single(initialStepRuns, item => item.Sequence == 0);
        var downstreamStep = Assert.Single(initialStepRuns, item => item.Sequence == 1);
        var upstreamArtifact = Assert.Single(upstreamStep.ArtifactOutputs);
        var now = DateTimeOffset.UtcNow;

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var upstream = await dbContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == upstreamStep.Id);
            upstream.Status = ProcessStepRunStatus.Completed;
            upstream.CompletedAtUtc = now;
            upstream.DecisionSummary = "Upstream work completed before materialized artifact was recorded.";

            var downstream = await dbContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == downstreamStep.Id);
            downstream.Status = ProcessStepRunStatus.Blocked;
            downstream.BlockedReason = "Cannot dispatch 'Review materialized artifact' because required upstream artifacts are missing: upstream step 'Produce materialized artifact' must provide required artifact 'Materialized evidence'. Automation requested upstream artifact materialization from 'Produce materialized artifact' before retrying this step.";
            downstream.BlockReasonCode = ProcessStepBlockReasonCode.MissingUpstreamArtifact;
            downstream.RecoveryOptionsJson = "[\"WaitForArtifactMaterialization\"]";
            downstream.DecisionSummary = "Waiting for upstream materialized artifact.";

            var run = await dbContext.Set<ProcessRun>().SingleAsync(item => item.Id == runId);
            run.Status = ProcessRunStatus.Blocked;
            run.UpdatedAtUtc = now;

            await dbContext.SaveChangesAsync();
        }

        var recordResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runId,
            StepRunId = upstreamStep.Id,
            ArtifactExpectationId = upstreamArtifact.ArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = upstreamArtifact.Title,
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "Recorded by the same materialization call that must reopen the downstream step.",
            AllowedFutureUsageSummary = "SB01-INV-001 regression proof.",
            ReviewSummary = "Materialized evidence is present.",
            ManagedStoragePath = "artifacts/materialization/materialized-evidence.md",
            ExternalReferenceKey = $"sb01-inv-001:{Guid.NewGuid():N}"
        });

        AssertSuccess(recordResult);

        await using var assertionContext = await dbContextFactory.CreateDbContextAsync();
        var reactivatedStep = await assertionContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == downstreamStep.Id);
        Assert.Equal(ProcessStepRunStatus.Ready, reactivatedStep.Status);
        Assert.Equal(string.Empty, reactivatedStep.BlockedReason);
        Assert.Equal(ProcessStepBlockReasonCode.None, reactivatedStep.BlockReasonCode);
        Assert.Equal("[]", reactivatedStep.RecoveryOptionsJson);
        Assert.Equal("Reopened after upstream artifact materialization completed.", reactivatedStep.DecisionSummary);

        var recordedArtifact = await assertionContext.Set<ProcessArtifactRecord>().SingleAsync(item => item.Id == recordResult.Value);
        Assert.Equal(upstreamStep.Id, recordedArtifact.StepRunId);
        Assert.Equal(upstreamArtifact.ArtifactExpectationId, recordedArtifact.ArtifactExpectationId);

        var journalEntry = await assertionContext.Set<ProcessJournalEntry>()
            .SingleAsync(item =>
                item.ProcessRunId == runId &&
                item.StepRunId == downstreamStep.Id &&
                item.EventType == ProcessRuntimeEventTypes.MissingUpstreamArtifactMaterializationResolved);
        Assert.Contains("Materialized evidence", journalEntry.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransitionStepAsync_allows_restarting_failed_step_and_reactivates_run()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Process failed step retry project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Failed step retry validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify failed steps can be restarted."
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = firstStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started the failed-step retry validation.",
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        })).IsSuccess);

        var originalStartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        await using (var timingContext = await dbContextFactory.CreateDbContextAsync())
        {
            var timingStep = await timingContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == firstStep.Id);
            timingStep.StartedAtUtc = originalStartedAtUtc;
            await timingContext.SaveChangesAsync();
        }

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = firstStep.Id,
            TargetStatus = ProcessStepRunStatus.Failed,
            Reason = "Validation forced a recoverable failure.",
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        })).IsSuccess);

        var failedStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Id == firstStep.Id);
        Assert.Equal(ProcessStepRunStatus.Failed, failedStep.Status);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedFailedStep = await dbContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == firstStep.Id);
        Assert.Contains("recoverable failure", persistedFailedStep.ExceptionSummary, StringComparison.OrdinalIgnoreCase);

        var retryResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = firstStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Retry the failed governed step.",
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        });

        Assert.True(retryResult.IsSuccess, string.Join(" | ", retryResult.Errors.Select(error => error.Message)));

        var retriedStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Id == firstStep.Id);
        Assert.Equal(ProcessStepRunStatus.InProgress, retriedStep.Status);
        Assert.Equal(1, retriedStep.ReworkCount);
        Assert.True(retriedStep.TouchMinutes >= 5);
        Assert.Equal(failedStep.WaitMinutes, retriedStep.WaitMinutes);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var persistedStep = await verificationContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == firstStep.Id);
        var persistedRun = await verificationContext.Set<ProcessRun>().SingleAsync(item => item.Id == runResult.Value);

        Assert.Equal(ProcessStepRunStatus.InProgress, persistedStep.Status);
        Assert.Null(persistedStep.CompletedAtUtc);
        Assert.True(string.IsNullOrWhiteSpace(persistedStep.ExceptionSummary));
        Assert.True(persistedStep.StartedAtUtc > originalStartedAtUtc);
        Assert.True(persistedStep.TouchMinutes >= 5);
        Assert.Equal(ProcessRunStatus.Active, persistedRun.Status);
        Assert.Null(persistedRun.CompletedAtUtc);
    }

    [Fact]
    public async Task TransitionStepAsync_SB09_INV_001_persists_typed_policy_denial_block_state()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Typed policy denial block project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Typed policy denial validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify blocked state is typed."
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        var transitionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = firstStep.Id,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = "Tool policy denied external-target/C/legacy/source because the governed step is not authorized to mutate that external path.",
            BlockCause = ProcessStepBlockCause.PolicyDenied,
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        });

        Assert.True(transitionResult.IsSuccess, string.Join(" | ", transitionResult.Errors.Select(error => error.Message)));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedStep = await dbContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == firstStep.Id);
        Assert.Equal(ProcessStepBlockReasonCode.PolicyDeniedExternalPath, persistedStep.BlockReasonCode);
        var recoveryOptions = JsonSerializer.Deserialize<List<ProcessStepRecoveryOption>>(
            persistedStep.RecoveryOptionsJson,
            StringEnumJsonOptions) ?? [];
        Assert.Contains(ProcessStepRecoveryOption.HumanEscalation, recoveryOptions);
        Assert.Contains(ProcessStepRecoveryOption.ReworkContinuation, recoveryOptions);
    }

    [Fact]
    public async Task TransitionStepAsync_SB05_INV_001_persists_own_output_artifact_contract_block_cause()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Own output block cause project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Own output block cause validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify own missing artifact block cause."
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        var transitionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = firstStep.Id,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = "missing required artifact: Delivery readiness evidence",
            BlockCause = ProcessStepBlockCause.OwnOutput,
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        });

        Assert.True(transitionResult.IsSuccess, FormatErrors(transitionResult.Errors));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedStep = await dbContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == firstStep.Id);
        var recoveryOptions = JsonSerializer.Deserialize<List<ProcessStepRecoveryOption>>(
            persistedStep.RecoveryOptionsJson,
            StringEnumJsonOptions) ?? [];

        Assert.Equal(ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, persistedStep.BlockReasonCode);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, recoveryOptions);
        Assert.DoesNotContain(ProcessStepRecoveryOption.WaitForArtifactMaterialization, recoveryOptions);
    }

    [Fact]
    public async Task TransitionStepAsync_SB05_INV_002_persists_upstream_input_materialization_block_cause()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Upstream input block cause project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Upstream input block cause validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify upstream missing artifact block cause."
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        var transitionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = firstStep.Id,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = "Required upstream artifacts are missing and the source step must provide required artifact input.",
            BlockCause = ProcessStepBlockCause.UpstreamInput,
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        });

        Assert.True(transitionResult.IsSuccess, FormatErrors(transitionResult.Errors));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedStep = await dbContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == firstStep.Id);
        var recoveryOptions = JsonSerializer.Deserialize<List<ProcessStepRecoveryOption>>(
            persistedStep.RecoveryOptionsJson,
            StringEnumJsonOptions) ?? [];

        Assert.Equal(ProcessStepBlockReasonCode.MissingUpstreamArtifact, persistedStep.BlockReasonCode);
        Assert.Contains(ProcessStepRecoveryOption.WaitForArtifactMaterialization, recoveryOptions);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, recoveryOptions);
    }

    [Fact]
    public async Task TransitionStepAsync_SB12_INV_001_exposes_distinct_recovery_health_for_own_and_upstream_missing_artifacts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var ownOutput = await CreateBlockedRunAsync(
            "SB12 own missing artifact recovery",
            "missing required artifact: Delivery readiness evidence",
            ProcessStepBlockCause.OwnOutput);
        var ownOutputStep = Assert.Single(
            (await processesService.GetRunDetailsAsync(ownOutput.RunId)).StepRuns,
            item => item.Id == ownOutput.StepRunId);

        Assert.Equal(ProcessStepBlockReasonCode.ArtifactContractUnsatisfied, ownOutputStep.BlockReasonCode);
        Assert.Equal(ProcessStepRecoveryOption.RecoverArtifactsOnly, ownOutputStep.NextRecoveryAction);
        Assert.Equal(ProcessStepRecoveryOption.RecoverArtifactsOnly, ownOutputStep.Health.NextRecoveryAction);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, ownOutputStep.RecoveryOptions);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, ownOutputStep.Health.RecoveryOptions);
        Assert.DoesNotContain(ProcessStepRecoveryOption.WaitForArtifactMaterialization, ownOutputStep.RecoveryOptions);

        var upstream = await CreateBlockedRunAsync(
            "SB12 upstream missing artifact recovery",
            "Required upstream artifacts are missing and the source step must provide required artifact input.",
            ProcessStepBlockCause.UpstreamInput);
        var upstreamStep = Assert.Single(
            (await processesService.GetRunDetailsAsync(upstream.RunId)).StepRuns,
            item => item.Id == upstream.StepRunId);

        Assert.Equal(ProcessStepBlockReasonCode.MissingUpstreamArtifact, upstreamStep.BlockReasonCode);
        Assert.Equal(ProcessStepRecoveryOption.WaitForArtifactMaterialization, upstreamStep.NextRecoveryAction);
        Assert.Equal(ProcessStepRecoveryOption.WaitForArtifactMaterialization, upstreamStep.Health.NextRecoveryAction);
        Assert.Contains(ProcessStepRecoveryOption.WaitForArtifactMaterialization, upstreamStep.RecoveryOptions);
        Assert.Contains(ProcessStepRecoveryOption.WaitForArtifactMaterialization, upstreamStep.Health.RecoveryOptions);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, upstreamStep.RecoveryOptions);

        async Task<(Guid RunId, Guid StepRunId)> CreateBlockedRunAsync(
            string projectName,
            string reason,
            ProcessStepBlockCause blockCause)
        {
            var projectId = await CreateProjectAsync(projectsService, projectName);
            var managerRoleId = Guid.NewGuid();
            var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

            AssertSuccess(saveResult);
            AssertSuccess(await processesService.PublishAsync(saveResult.Value));

            var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
            {
                ProcessDefinitionId = saveResult.Value,
                ProjectId = projectId,
                RunName = projectName,
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Verify SB12 block recovery health."
            });

            AssertSuccess(runResult);

            var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
            var transitionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
            {
                StepRunId = firstStep.Id,
                TargetStatus = ProcessStepRunStatus.Blocked,
                Reason = reason,
                BlockCause = blockCause,
                DecidedBy = "integration-tests",
                SuppressAutomationDispatch = true
            });

            AssertSuccess(transitionResult);

            return (runResult.Value, firstStep.Id);
        }
    }

    [Fact]
    public async Task TransitionStepAsync_SB10_INV_001_persists_recovery_router_next_action_and_lifecycle_event()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Vendor invoice recovery router project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        AssertSuccess(saveResult);
        AssertSuccess(await processesService.PublishAsync(saveResult.Value));

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Vendor invoice recovery router validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify generic recovery routing state."
        });

        AssertSuccess(runResult);

        var firstStep = Assert.Single(await processesService.ListStepRunsAsync(runResult.Value), item => item.Sequence == 0);
        var transitionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = firstStep.Id,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = "Vendor invoice approval packet is missing the required compliance evidence artifact.",
            BlockCause = ProcessStepBlockCause.OwnOutput,
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        });

        AssertSuccess(transitionResult);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedStep = await dbContext.Set<ProcessStepRun>().SingleAsync(item => item.Id == firstStep.Id);
        var routingEvent = await dbContext.Set<ProcessJournalEntry>()
            .SingleAsync(item =>
                item.ProcessRunId == runResult.Value &&
                item.StepRunId == firstStep.Id &&
                item.EventType == ProcessRuntimeEventTypes.RecoveryRoutingDecisionRecorded);
        var runDetails = await processesService.GetRunDetailsAsync(runResult.Value);
        var routedStep = Assert.Single(runDetails.StepRuns, item => item.Id == firstStep.Id);

        Assert.Equal(ProcessStepRecoveryOption.RecoverArtifactsOnly, persistedStep.NextRecoveryAction);
        Assert.Equal(ProcessStepRecoveryOption.RecoverArtifactsOnly, routedStep.Health.NextRecoveryAction);
        Assert.Contains(ProcessStepRecoveryOption.RecoverArtifactsOnly, routedStep.Health.RecoveryOptions);
        Assert.Contains("RecoverArtifactsOnly", routingEvent.ReplayContextJson, StringComparison.Ordinal);
        Assert.Contains("Vendor invoice", routingEvent.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendDirectMessageAsync_persists_collaboration_transcript_when_policy_assignment_permissions_and_governance_allow_it()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var fixture = await CreateDirectMessagingRunFixtureAsync(scope.ServiceProvider, includeMessagingPolicy: true);

        await ResolveRunAssignmentAsync(processesService, fixture.RunId, fixture.SourceRoleRequirementId, "Delivery lead", allowsDirectMessaging: true);
        await ResolveRunAssignmentAsync(processesService, fixture.RunId, fixture.TargetRoleRequirementId, "Review lead", allowsDirectMessaging: true);

        var result = await processesService.SendDirectMessageAsync(new ProcessDirectMessageRequest
        {
            ProcessRunId = fixture.RunId,
            SourceRoleRequirementId = fixture.SourceRoleRequirementId,
            TargetRoleRequirementId = fixture.TargetRoleRequirementId,
            MessageBody = "Delivery handoff package is ready for review."
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));

        var runDetails = await processesService.GetRunDetailsAsync(fixture.RunId);
        var thread = Assert.Single(runDetails.DirectMessageThreads);
        Assert.Equal(result.Value, thread.ThreadId);
        Assert.Single(thread.Messages);
        Assert.Equal("Delivery lead", thread.Messages[0].AuthorName);
        Assert.Equal("Delivery handoff package is ready for review.", thread.Messages[0].Body);
        var decision = Assert.Single(runDetails.Decisions, item => item.DecisionKind == ProcessDecisionKind.DirectMessage);
        Assert.Equal(ProcessDecisionOutcome.Accepted, decision.Outcome);
        Assert.DoesNotContain(runDetails.ConformanceObservations, item => item.Category == "DirectMessagingPolicy");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedThread = await dbContext.Set<CollaborationThreadRecord>()
            .SingleAsync(item => item.Id == result.Value);
        Assert.Equal(CollaborationContextKind.ProcessRun, persistedThread.ContextKind);
        Assert.Equal(fixture.RunId, persistedThread.ContextId);
        var persistedParticipants = await dbContext.Set<CollaborationParticipantRecord>()
            .Where(item => item.ThreadId == result.Value)
            .ToListAsync();
        Assert.Equal(2, persistedParticipants.Count);
        var persistedMessage = await dbContext.Set<CollaborationMessageRecord>()
            .SingleAsync(item => item.ThreadId == result.Value);
        Assert.Equal("Delivery lead", persistedMessage.AuthorName);
        Assert.Equal("Delivery handoff package is ready for review.", persistedMessage.Body);
        var persistedInboxItem = await dbContext.Set<CollaborationInboxItemRecord>()
            .SingleAsync(item => item.ThreadId == result.Value);
        Assert.Equal("/collaboration?threadId=" + result.Value.ToString("D"), persistedInboxItem.Route);
        Assert.Equal(1, persistedInboxItem.UnreadCount);
    }

    [Fact]
    public async Task SendDirectMessageAsync_rejects_when_process_policy_has_no_messaging_link()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var fixture = await CreateDirectMessagingRunFixtureAsync(scope.ServiceProvider, includeMessagingPolicy: false);

        await ResolveRunAssignmentAsync(processesService, fixture.RunId, fixture.SourceRoleRequirementId, "Delivery lead", allowsDirectMessaging: true);
        await ResolveRunAssignmentAsync(processesService, fixture.RunId, fixture.TargetRoleRequirementId, "Review lead", allowsDirectMessaging: true);

        var result = await processesService.SendDirectMessageAsync(new ProcessDirectMessageRequest
        {
            ProcessRunId = fixture.RunId,
            SourceRoleRequirementId = fixture.SourceRoleRequirementId,
            TargetRoleRequirementId = fixture.TargetRoleRequirementId,
            MessageBody = "Attempt without a process-owned messaging link."
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "processes.direct-message-policy-missing");

        var runDetails = await processesService.GetRunDetailsAsync(fixture.RunId);
        Assert.Empty(runDetails.DirectMessageThreads);
        var decision = Assert.Single(runDetails.Decisions, item => item.DecisionKind == ProcessDecisionKind.DirectMessage);
        Assert.Equal(ProcessDecisionOutcome.Rejected, decision.Outcome);
        Assert.Contains(runDetails.ConformanceObservations, item =>
            item.Category == "DirectMessagingPolicy" &&
            item.Observation.Contains("no explicit Messaging link", StringComparison.Ordinal));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Empty(await dbContext.Set<CollaborationThreadRecord>()
            .Where(item => item.ContextKind == CollaborationContextKind.ProcessRun && item.ContextId == fixture.RunId)
            .ToListAsync());
    }

    [Fact]
    public async Task SendDirectMessageAsync_rejects_when_run_assignment_permissions_disable_direct_messaging()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var fixture = await CreateDirectMessagingRunFixtureAsync(scope.ServiceProvider, includeMessagingPolicy: true);

        await ResolveRunAssignmentAsync(processesService, fixture.RunId, fixture.SourceRoleRequirementId, "Delivery lead", allowsDirectMessaging: true);
        await ResolveRunAssignmentAsync(processesService, fixture.RunId, fixture.TargetRoleRequirementId, "Review lead", allowsDirectMessaging: false);

        var result = await processesService.SendDirectMessageAsync(new ProcessDirectMessageRequest
        {
            ProcessRunId = fixture.RunId,
            SourceRoleRequirementId = fixture.SourceRoleRequirementId,
            TargetRoleRequirementId = fixture.TargetRoleRequirementId,
            MessageBody = "Attempt blocked by assignment permission."
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "processes.direct-message-target-permission-denied");

        var runDetails = await processesService.GetRunDetailsAsync(fixture.RunId);
        Assert.Empty(runDetails.DirectMessageThreads);
        Assert.Contains(runDetails.ConformanceObservations, item =>
            item.Category == "DirectMessagingPolicy" &&
            item.Observation.Contains("cannot receive direct messages", StringComparison.Ordinal));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Empty(await dbContext.Set<CollaborationMessageRecord>().ToListAsync());
    }

    [Fact]
    public async Task SendDirectMessageAsync_rejects_when_governance_state_blocks_direct_messaging()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var fixture = await CreateDirectMessagingRunFixtureAsync(
            scope.ServiceProvider,
            includeMessagingPolicy: true,
            operatingMode: ProcessOperatingMode.Emergency);

        await ResolveRunAssignmentAsync(processesService, fixture.RunId, fixture.SourceRoleRequirementId, "Delivery lead", allowsDirectMessaging: true);
        await ResolveRunAssignmentAsync(processesService, fixture.RunId, fixture.TargetRoleRequirementId, "Review lead", allowsDirectMessaging: true);

        var result = await processesService.SendDirectMessageAsync(new ProcessDirectMessageRequest
        {
            ProcessRunId = fixture.RunId,
            SourceRoleRequirementId = fixture.SourceRoleRequirementId,
            TargetRoleRequirementId = fixture.TargetRoleRequirementId,
            MessageBody = "Attempt blocked by emergency governance."
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "processes.direct-message-governance-denied");

        var runDetails = await processesService.GetRunDetailsAsync(fixture.RunId);
        Assert.Empty(runDetails.DirectMessageThreads);
        Assert.Contains(runDetails.ConformanceObservations, item =>
            item.Category == "DirectMessagingPolicy" &&
            item.Observation.Contains("Direct messaging is not allowed while the run is Active in Emergency mode.", StringComparison.Ordinal));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Empty(await dbContext.Set<CollaborationThreadRecord>()
            .Where(item => item.ContextKind == CollaborationContextKind.ProcessRun && item.ContextId == fixture.RunId)
            .ToListAsync());
    }

    [Fact]
    public async Task SeedBaselineAsync_supports_global_then_project_scoped_baselines_without_slug_collisions()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var seedService = scope.ServiceProvider.GetRequiredService<ProcessDevelopmentSeedService>();

        var projectId = await CreateProjectAsync(projectsService, "Seeded process project");
        var globalSeedResult = await seedService.SeedBaselineAsync();

        Assert.True(globalSeedResult.IsSuccess, string.Join(" | ", globalSeedResult.Errors.Select(error => error.Message)));

        var seedResult = await seedService.SeedBaselineAsync(projectId);

        Assert.True(seedResult.IsSuccess, string.Join(" | ", seedResult.Errors.Select(error => error.Message)));
        Assert.NotNull(seedResult.Value);
        Assert.True(seedResult.Value!.SeededDefinitionIds.Count >= 5);
        Assert.True(seedResult.Value.SeededRunIds.Count >= 5);

        var repeatedSeedResult = await seedService.SeedBaselineAsync(projectId);

        Assert.True(repeatedSeedResult.IsSuccess, string.Join(" | ", repeatedSeedResult.Errors.Select(error => error.Message)));

        var definitions = await processesService.ListDefinitionsAsync(projectId);
        var projectDefinitions = definitions.Where(item => item.ProjectId == projectId).ToList();

        Assert.True(projectDefinitions.Count >= seedResult.Value.SeededDefinitionIds.Count);
        Assert.Equal(definitions.Count, definitions.Select(item => item.Id).Distinct().Count());
        Assert.Contains(projectDefinitions, item => item.Id == seedResult.Value.PrimaryDefinitionId);
        Assert.Contains(projectDefinitions, item => item.Id == seedResult.Value.SecondaryDefinitionId);

        var softwareDeliveryDefinition = Assert.Single(projectDefinitions, item => item.Name == "Multi-team software delivery and release governance");
        var branchingDefinition = Assert.Single(projectDefinitions, item => item.Name == "Branching code review and merge governance");
        var hotfixDefinition = Assert.Single(projectDefinitions, item => item.Name == "Emergency hotfix rollout with shard-risk governance");
        var customerOnboardingDefinition = Assert.Single(projectDefinitions, item => item.Name == "Customer onboarding orchestration");
        var incidentResponseDefinition = Assert.Single(projectDefinitions, item => item.Name == "Incident response and escalation");
        var releaseReadinessDefinition = Assert.Single(projectDefinitions, item => item.Name == "Release readiness and deployment control");
        var architectureDecisionDefinition = Assert.Single(projectDefinitions, item => item.Name == "Architecture decision governance and ADR stewardship");
        var blazorAppDeliveryDefinition = Assert.Single(projectDefinitions, item => item.Name == "Blazor app delivery");

        var softwareDeliveryRun = Assert.Single(
            await processesService.ListRunsAsync(softwareDeliveryDefinition.Id, projectId),
            item => item.Name == "Multi-team software delivery and release governance / reference delivery capability");
        var softwareDeliveryStepRuns = await processesService.ListStepRunsAsync(softwareDeliveryRun.Id);
        var softwareDeliveryArtifacts = await processesService.ListArtifactsAsync(softwareDeliveryRun.Id);
        var softwareDeliveryConformance = await processesService.ListConformanceObservationsAsync(softwareDeliveryRun.Id);
        var softwareDeliveryEditor = await processesService.GetEditorAsync(softwareDeliveryDefinition.Id, projectId);

        Assert.True(softwareDeliveryStepRuns.Count >= 9);
        Assert.Contains(
            softwareDeliveryStepRuns,
            item => item.Title == "Run QA validation and runtime or browser proof" &&
                    item.SelectedBranchOutcomeTitle == "Quality accepted");
        Assert.Contains(
            softwareDeliveryStepRuns,
            item => item.Title == "Perform security and data-handling review" &&
                    item.Status == ProcessStepRunStatus.Blocked);
        Assert.Contains(softwareDeliveryArtifacts, item => item.Title == "Reference delivery architecture decision record");
        Assert.Contains(softwareDeliveryArtifacts, item => item.Title == "Project structure context brief");
        Assert.Contains(softwareDeliveryArtifacts, item => item.Title == "Reference delivery regression evidence pack");
        Assert.Contains(softwareDeliveryArtifacts, item => item.Title == "Open security exception assessment for sensitive-data delivery capability");
        Assert.NotEmpty(softwareDeliveryConformance);
        var releaseApprovalStepRun = Assert.Single(softwareDeliveryStepRuns, item => item.Title == "Approve first-pass release readiness");
        Assert.True(releaseApprovalStepRun.Dependencies.Count >= 3);
        Assert.Equal(4, releaseApprovalStepRun.ArtifactInputCount);
        Assert.Contains(releaseApprovalStepRun.ResponsibilityPorts, item => item.ResponsibilityKind == ProcessResponsibilityKind.Approver);
        var releaseApprovalDefinitionStep = Assert.Single(softwareDeliveryEditor.Steps, item => item.Key == "release-approval");
        Assert.Equal(4, releaseApprovalDefinitionStep.ArtifactInputs.Count);

        var hotfixRun = Assert.Single(
            await processesService.ListRunsAsync(hotfixDefinition.Id, projectId),
            item => item.Name == "Emergency hotfix rollout with shard-risk governance / critical endpoint latency");
        var hotfixStepRuns = await processesService.ListStepRunsAsync(hotfixRun.Id);
        var hotfixArtifacts = await processesService.ListArtifactsAsync(hotfixRun.Id);
        var hotfixEditor = await processesService.GetEditorAsync(hotfixDefinition.Id, projectId);

        Assert.True(hotfixStepRuns.Count >= 7);
        Assert.Contains(hotfixStepRuns, item => item.Sequence == 5 && item.Status == ProcessStepRunStatus.Failed);
        Assert.Contains(hotfixArtifacts, item => item.Title == "Critical endpoint latency emergency rollout and telemetry log");
        var emergencyApprovalStepRun = Assert.Single(hotfixStepRuns, item => item.Title == "Approve emergency release window");
        Assert.True(emergencyApprovalStepRun.Dependencies.Count >= 2);
        Assert.Equal(2, emergencyApprovalStepRun.ArtifactInputCount);
        var emergencyApprovalDefinitionStep = Assert.Single(hotfixEditor.Steps, item => item.Key == "approve-emergency-release");
        Assert.Equal(2, emergencyApprovalDefinitionStep.ArtifactInputs.Count);

        var branchingRun = Assert.Single(
            await processesService.ListRunsAsync(branchingDefinition.Id, projectId),
            item => item.Name == "Branching code review and merge governance / reviewed UI change");
        var branchingStepRuns = await processesService.ListStepRunsAsync(branchingRun.Id);
        var branchingEditor = await processesService.GetEditorAsync(branchingDefinition.Id, projectId);

        Assert.True(branchingStepRuns.Count >= 12);
        Assert.Contains(branchingStepRuns, item => item.Title == "Route code review disposition");
        Assert.Contains(branchingStepRuns, item => item.Title == "Normalize default review lane");
        Assert.Contains(branchingStepRuns, item => item.Title == "Capture workflow failure and recovery path");
        Assert.Contains(branchingStepRuns, item => item.Title == "Approve direct merge route");
        Assert.Contains(branchingStepRuns, item => item.Title == "Approve merge after QA validation");
        Assert.Contains(branchingStepRuns, item => item.Title == "Approve merge after security review");
        Assert.Contains(branchingStepRuns, item => item.Title == "Approve merge after architecture escalation");
        Assert.Contains(branchingStepRuns, item => item.Title == "Approve merge after default normalization");
        var branchingDecisionStepRun = Assert.Single(branchingStepRuns, item => item.Title == "Route code review disposition");
        Assert.Equal("Review lead", branchingDecisionStepRun.DecisionRoleTitle);
        Assert.Equal(1, branchingDecisionStepRun.ArtifactInputCount);
        Assert.Single(branchingDecisionStepRun.ArtifactOutputs);
        var directMergeStepRun = Assert.Single(branchingStepRuns, item => item.Title == "Approve direct merge route");
        Assert.Equal(ProcessStepRunStatus.Skipped, directMergeStepRun.Status);
        var qaMergeStepRun = Assert.Single(branchingStepRuns, item => item.Title == "Approve merge after QA validation");
        Assert.Equal(ProcessStepRunStatus.Completed, qaMergeStepRun.Status);
        Assert.Equal(2, qaMergeStepRun.ArtifactInputCount);
        Assert.Equal(ProcessStepRunStatus.Skipped, Assert.Single(branchingStepRuns, item => item.Title == "Approve merge after security review").Status);
        Assert.Equal(ProcessStepRunStatus.Skipped, Assert.Single(branchingStepRuns, item => item.Title == "Approve merge after architecture escalation").Status);
        Assert.Equal(ProcessStepRunStatus.Skipped, Assert.Single(branchingStepRuns, item => item.Title == "Approve merge after default normalization").Status);
        var branchingQaDefinitionStep = Assert.Single(branchingEditor.Steps, item => item.Key == "validate-qa-lane");
        Assert.Single(branchingQaDefinitionStep.ArtifactInputs);
        var branchingQaMergeDefinitionStep = Assert.Single(branchingEditor.Steps, item => item.Key == "approve-merge-after-qa");
        Assert.Equal(2, branchingQaMergeDefinitionStep.Dependencies.Count);
        Assert.Equal(2, branchingQaMergeDefinitionStep.ArtifactInputs.Count);

        var customerOnboardingRun = Assert.Single(
            await processesService.ListRunsAsync(customerOnboardingDefinition.Id, projectId),
            item => item.Name == "Customer onboarding orchestration / enterprise rollout");
        var customerOnboardingStepRuns = await processesService.ListStepRunsAsync(customerOnboardingRun.Id);
        var customerOnboardingArtifacts = await processesService.ListArtifactsAsync(customerOnboardingRun.Id);

        Assert.Contains(
            customerOnboardingStepRuns,
            item => item.Title == "Review staffing intent" &&
                    item.SelectedBranchOutcomeTitle == "Staffing ready");
        Assert.Contains(customerOnboardingArtifacts, item => item.Title == "Enterprise customer kickoff approval record");

        var incidentResponseRun = Assert.Single(
            await processesService.ListRunsAsync(incidentResponseDefinition.Id, projectId),
            item => item.Name == "Incident response and escalation / user access disruption");
        var incidentResponseStepRuns = await processesService.ListStepRunsAsync(incidentResponseRun.Id);
        var incidentResponseArtifacts = await processesService.ListArtifactsAsync(incidentResponseRun.Id);

        Assert.Contains(
            incidentResponseStepRuns,
            item => item.Title == "Diagnose probable cause" &&
                    item.SelectedBranchOutcomeTitle == "Mitigation ready for approval");
        Assert.Contains(incidentResponseArtifacts, item => item.Title == "User access escalation approval record");

        var releaseReadinessRun = Assert.Single(
            await processesService.ListRunsAsync(releaseReadinessDefinition.Id, projectId),
            item => item.Name == "Release readiness and deployment control / customer-visible maintenance window");
        var releaseReadinessStepRuns = await processesService.ListStepRunsAsync(releaseReadinessRun.Id);
        var releaseReadinessArtifacts = await processesService.ListArtifactsAsync(releaseReadinessRun.Id);

        Assert.Contains(
            releaseReadinessStepRuns,
            item => item.Title == "Run final security review and go/no-go approval" &&
                    item.SelectedBranchOutcomeTitle == "Go");
        Assert.Contains(releaseReadinessArtifacts, item => item.Title == "Final go/no-go decision");
        Assert.Contains(releaseReadinessArtifacts, item => item.Title == "Release execution provenance note");

        var architectureDecisionRun = Assert.Single(
            await processesService.ListRunsAsync(architectureDecisionDefinition.Id, projectId),
            item => item.Name == "Architecture decision governance / integration boundary decision");
        var architectureDecisionStepRuns = await processesService.ListStepRunsAsync(architectureDecisionRun.Id);
        var architectureDecisionArtifacts = await processesService.ListArtifactsAsync(architectureDecisionRun.Id);

        Assert.Contains(
            architectureDecisionStepRuns,
            item => item.Title == "Make the board decision and record rationale" &&
                    item.SelectedBranchOutcomeTitle == "Approved");
        Assert.Contains(architectureDecisionArtifacts, item => item.Title == "Approved architecture decision record");

        var blazorAppDeliveryRun = Assert.Single(
            await processesService.ListRunsAsync(blazorAppDeliveryDefinition.Id, projectId),
            item => item.Name == "Blazor WASM PWA delivery / generic application");
        var blazorAppDeliveryStepRuns = await processesService.ListStepRunsAsync(blazorAppDeliveryRun.Id);
        var blazorAppDeliveryArtifacts = await processesService.ListArtifactsAsync(blazorAppDeliveryRun.Id);

        Assert.Contains(
            blazorAppDeliveryStepRuns,
            item => item.Title == "Validate Blazor runtime and browser evidence" &&
                    item.SelectedBranchOutcomeTitle == "Quality accepted");
        Assert.Contains(blazorAppDeliveryArtifacts, item => item.Title == "Blazor project-structure writeback receipt");

        var exportEnvelope = await processesService.ExportAsync(seedResult.Value.PrimaryDefinitionId);
        exportEnvelope.Definition.Id = null;
        exportEnvelope.Definition.WorkingVersionId = null;
        exportEnvelope.Definition.Name = "Imported process clone";
        exportEnvelope.Definition.ChangeSummary = "Imported from exported seed envelope.";

        var importResult = await processesService.ImportAsync(exportEnvelope);

        Assert.True(importResult.IsSuccess);

        var definitionsAfterImport = await processesService.ListDefinitionsAsync(projectId);
        Assert.Contains(definitionsAfterImport, item => item.Name == "Imported process clone");
    }

    [Fact]
    public async Task ListDefinitionsAsync_counts_roles_and_steps_from_the_current_summary_version_only()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process summary counts project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var definitions = await processesService.ListDefinitionsAsync(projectId);
        var definition = Assert.Single(definitions, item => item.Id == saveResult.Value);

        Assert.Equal(2, definition.LatestVersionNumber);
        Assert.Equal(1, definition.RoleCount);
        Assert.Equal(2, definition.StepCount);
    }

    [Fact]
    public async Task ListDefinitionsAsync_counts_active_runs_in_current_project_scope()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var firstProjectId = await CreateProjectAsync(projectsService, "Scoped process counts first project");
        var secondProjectId = await CreateProjectAsync(projectsService, "Scoped process counts second project");
        var definition = BuildDefinitionEditor(firstProjectId, Guid.NewGuid());
        definition.ProjectId = null;
        definition.Name = "Global scoped process counts";
        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        await StartTestRunAsync(processesService, saveResult.Value, firstProjectId, "First scoped active run");
        await StartTestRunAsync(processesService, saveResult.Value, secondProjectId, "Second scoped active run");

        var firstProjectDefinition = Assert.Single(
            await processesService.ListDefinitionsAsync(firstProjectId),
            item => item.Id == saveResult.Value);
        var secondProjectDefinition = Assert.Single(
            await processesService.ListDefinitionsAsync(secondProjectId),
            item => item.Id == saveResult.Value);
        var globalDefinition = Assert.Single(
            await processesService.ListDefinitionsAsync(null),
            item => item.Id == saveResult.Value);

        Assert.Equal(1, firstProjectDefinition.ActiveRunCount);
        Assert.Equal(1, secondProjectDefinition.ActiveRunCount);
        Assert.Equal(2, globalDefinition.ActiveRunCount);
    }

    [Fact]
    public async Task ListRunsAsync_returns_projected_step_progress_and_capability_gap_counts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process run projection project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Projected run summary validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify projected run counters"
        });

        Assert.True(runResult.IsSuccess);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var firstStep = Assert.Single(stepRuns, item => item.Sequence == 0);

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = firstStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start projected summary flow.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = firstStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "First step completed for projection verification.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var secondStep = Assert.Single(
            await processesService.ListStepRunsAsync(runResult.Value),
            item => item.Sequence == 1);

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = secondStep.Id,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = "Block the second step to test projected counts.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var run = Assert.Single(await processesService.ListRunsAsync(saveResult.Value, projectId), item => item.Id == runResult.Value);

        Assert.Equal(1, run.CompletedStepCount);
        Assert.Equal(2, run.TotalStepCount);
        Assert.Equal(1, run.BlockedStepCount);
        Assert.Equal(2, run.CapabilityGapCount);
        Assert.Equal(ProcessRunStatus.Blocked, run.Status);
    }

    [Fact]
    public async Task RuntimeStateOverview_separates_active_blocked_and_failed_runs()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var runtimeStateOverviewService = scope.ServiceProvider.GetRequiredService<ProcessRuntimeStateOverviewService>();

        var projectId = await CreateProjectAsync(projectsService, "Process runtime state overview project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var activeRunId = await StartTestRunAsync(processesService, saveResult.Value, projectId, "Active overview run");
        var blockedRunId = await StartTestRunAsync(processesService, saveResult.Value, projectId, "Blocked overview run");
        var failedRunId = await StartTestRunAsync(processesService, saveResult.Value, projectId, "Failed overview run");

        var blockedStep = Assert.Single(
            await processesService.ListStepRunsAsync(blockedRunId),
            item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = blockedStep.Id,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = "Blocked for runtime state overview verification.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var failedStep = Assert.Single(
            await processesService.ListStepRunsAsync(failedRunId),
            item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = failedStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started for runtime state overview verification.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = failedStep.Id,
            TargetStatus = ProcessStepRunStatus.Failed,
            Reason = "Failed for runtime state overview verification.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var definitions = await processesService.ListDefinitionsAsync(projectId);
        var definition = Assert.Single(definitions, item => item.Id == saveResult.Value);
        var overview = await runtimeStateOverviewService.GetOverviewAsync([saveResult.Value], projectId, forceRefresh: true);
        var definitionRunCounts = overview.GetDefinition(saveResult.Value).RunCounts;

        Assert.NotEqual(Guid.Empty, activeRunId);
        Assert.Equal(1, definition.ActiveRunCount);
        Assert.Equal(1, overview.Totals.Active);
        Assert.Equal(1, overview.Totals.Blocked);
        Assert.Equal(1, overview.Totals.Failed);
        Assert.Equal(1, definitionRunCounts.Active);
        Assert.Equal(1, definitionRunCounts.Blocked);
        Assert.Equal(1, definitionRunCounts.Failed);
    }

    [Fact]
    public async Task StopBlockedRunAsync_cancels_blocked_run_and_rejects_late_transitions()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Stop blocked process run project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var activeRunId = await StartTestRunAsync(processesService, saveResult.Value, projectId, "Active stop guard run");
        var activeStopResult = await processesService.StopBlockedRunAsync(new ProcessRunStopRequest
        {
            ProcessRunId = activeRunId,
            Reason = "Active runs must not be stopped by the blocked-run action.",
            StoppedBy = "integration-tests"
        });

        Assert.True(activeStopResult.IsFailure);
        Assert.Contains(activeStopResult.Errors, error => error.Code == "processes.stop-blocked-run-invalid-status");

        var blockedRunId = await StartTestRunAsync(processesService, saveResult.Value, projectId, "Blocked stop run");
        var blockedStep = Assert.Single(
            await processesService.ListStepRunsAsync(blockedRunId),
            item => item.Sequence == 0);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = blockedStep.Id,
            TargetStatus = ProcessStepRunStatus.Blocked,
            Reason = "Blocked before stop verification.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var stopResult = await processesService.StopBlockedRunAsync(new ProcessRunStopRequest
        {
            ProcessRunId = blockedRunId,
            Reason = "Operator stopped a blocked run during integration verification.",
            StoppedBy = "integration-tests"
        });

        Assert.True(stopResult.IsSuccess);

        var stoppedRun = Assert.Single(await processesService.ListRunsAsync(saveResult.Value, projectId), item => item.Id == blockedRunId);
        var lateTransitionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = blockedStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Late blocked run resume after stop.",
            DecidedBy = "integration-tests"
        });

        Assert.Equal(ProcessRunStatus.Cancelled, stoppedRun.Status);
        Assert.True(lateTransitionResult.IsFailure);
        Assert.Contains(lateTransitionResult.Errors, error => error.Code == "processes.run-terminal");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var persistedRun = await dbContext.Set<ProcessRun>().SingleAsync(item => item.Id == blockedRunId);
        var stopDecision = await dbContext.Set<ProcessDecisionRecord>()
            .SingleAsync(item => item.ProcessRunId == blockedRunId && item.Title == "Stopped blocked process run");
        var stopJournalEntry = await dbContext.Set<ProcessJournalEntry>()
            .SingleAsync(item => item.ProcessRunId == blockedRunId && item.EventType == "blocked-run-stopped");

        Assert.NotNull(persistedRun.CompletedAtUtc);
        Assert.Equal(ProcessDecisionKind.Exception, stopDecision.DecisionKind);
        Assert.Equal(ProcessDecisionOutcome.Rejected, stopDecision.Outcome);
        Assert.Equal("integration-tests", stopDecision.DecidedBy);
        Assert.Equal("Operator stopped a blocked run during integration verification.", stopJournalEntry.Description);
    }

    [Fact]
    public async Task PublishAsync_rejects_unused_branch_outcomes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process branch validation project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildInvalidBranchDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);

        var publishResult = await processesService.PublishAsync(saveResult.Value);

        Assert.True(publishResult.IsFailure);
        Assert.Contains(publishResult.Errors, error => error.Code == "processes.publish-branch-outcome-unused");
    }

    [Fact]
    public async Task PublishAsync_SB10_INV_001_applies_strict_lint_for_high_criticality_definitions()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "SB10 high criticality lint project");
        var definition = BuildProductMutationLintGateDefinitionEditor(projectId, Guid.NewGuid());
        definition.Criticality = ProcessCriticality.High;
        var saveResult = await processesService.SaveAsync(definition);

        AssertSuccess(saveResult);

        var publishResult = await processesService.PublishAsync(saveResult.Value);

        Assert.True(publishResult.IsFailure);
        Assert.Contains(publishResult.Errors, error =>
            error.Code == "processes.publish.lint-blocked" &&
            error.Message.Contains("processes.lint.step-operation-contract-missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishAsync_SB12_INV_001_rejects_strict_version_missing_risky_operation_contract()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "SB12 strict contract mode project");
        var definition = BuildProductMutationLintGateDefinitionEditor(projectId, Guid.NewGuid());
        definition.ContractMode = ProcessDefinitionContractMode.Strict;
        var saveResult = await processesService.SaveAsync(definition);

        AssertSuccess(saveResult);

        var publishResult = await processesService.PublishAsync(saveResult.Value);

        Assert.True(publishResult.IsFailure);
        Assert.Contains(publishResult.Errors, error =>
            error.Code == "processes.publish.lint-blocked" &&
            error.Message.Contains("processes.lint.step-operation-contract-missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublishAsync_SB12_INV_002_allows_compatibility_version_with_visible_contract_warning()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "SB12 compatibility contract mode project");
        var definition = BuildProductMutationLintGateDefinitionEditor(projectId, Guid.NewGuid());
        definition.ContractMode = ProcessDefinitionContractMode.Compatibility;
        var saveResult = await processesService.SaveAsync(definition);

        AssertSuccess(saveResult);

        var editor = await processesService.GetEditorAsync(saveResult.Value);
        var publishResult = await processesService.PublishAsync(saveResult.Value);

        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));
        Assert.Equal(ProcessDefinitionContractMode.Compatibility, editor.ContractMode);
        Assert.Contains(editor.LintResult.Issues, issue =>
            issue.Code == "processes.lint.step-operation-contract-missing" &&
            issue.Severity == ProcessDefinitionLintSeverity.Warning);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var publishedVersion = await dbContext.Set<ProcessDefinitionVersion>()
            .SingleAsync(item =>
                item.ProcessDefinitionId == saveResult.Value &&
                item.Status == ProcessVersionStatus.Published);

        Assert.Equal(ProcessDefinitionContractMode.Compatibility, publishedVersion.ContractMode);
    }

    [Fact]
    public async Task StartRunAsync_SB10_INV_001_applies_strict_lint_for_delegated_published_definitions()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "SB10 delegated lint project");
        var definition = BuildProductMutationLintGateDefinitionEditor(projectId, Guid.NewGuid());
        definition.ContractMode = ProcessDefinitionContractMode.Compatibility;
        var saveResult = await processesService.SaveAsync(definition);

        AssertSuccess(saveResult);
        AssertSuccess(await processesService.PublishAsync(saveResult.Value));

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var persistedDefinition = await dbContext.Set<ProcessDefinition>()
                .SingleAsync(item => item.Id == saveResult.Value);
            persistedDefinition.AutonomyLevel = ProcessAutonomyLevel.Delegated;
            await dbContext.SaveChangesAsync();
        }

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "SB10 lint gate run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "SB10 strict lint gate verification."
        });

        Assert.True(runResult.IsFailure);
        Assert.Contains(runResult.Errors, error =>
            error.Code == "processes.run-start.lint-blocked" &&
            error.Message.Contains("processes.lint.step-operation-contract-missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveAsync_rejects_self_referencing_step_dependency()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Self dependency validation project");
        var model = BuildArtifactInputDefinitionEditor(projectId, Guid.NewGuid());
        var reviewStep = Assert.Single(model.Steps, item => item.Key == "qa-review");

        reviewStep.Dependencies = CreateDependencies((reviewStep.Id!.Value, null));

        var saveResult = await processesService.SaveAsync(model);

        Assert.True(saveResult.IsFailure);
        Assert.Contains(saveResult.Errors, error => error.Code == "processes.branch-dependency-self-reference");
    }

    [Fact]
    public async Task SaveAsync_rejects_multi_step_dependency_cycle()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Cycle validation project");
        var model = BuildArtifactInputDefinitionEditor(projectId, Guid.NewGuid());
        var captureStep = Assert.Single(model.Steps, item => item.Key == "capture-package");
        var reviewStep = Assert.Single(model.Steps, item => item.Key == "qa-review");

        captureStep.Dependencies = CreateDependencies((reviewStep.Id!.Value, null));

        var saveResult = await processesService.SaveAsync(model);

        Assert.True(saveResult.IsFailure);
        Assert.Contains(saveResult.Errors, error => error.Code == "processes.branch-dependency-cycle-invalid");
    }

    [Fact]
    public async Task SaveAsync_rejects_step_role_assignments_without_resolved_role_ids()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Missing role assignment validation project");
        var managerRoleId = Guid.NewGuid();
        var model = BuildDefinitionEditor(projectId, managerRoleId);
        var firstStep = Assert.Single(model.Steps, item => item.Key == "intake");
        firstStep.RoleAssignments =
        [
            new ProcessStepRoleRequirementEditorModel
            {
                RoleRequirementId = null,
                ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                RebindPolicySummary = "This assignment should be rejected before save."
            }
        ];

        var saveResult = await processesService.SaveAsync(model);

        Assert.True(saveResult.IsFailure);
        Assert.Contains(saveResult.Errors, error => error.Code == "processes.step-role-assignment-role-required");
    }

    [Fact]
    public async Task PublishAsync_rejects_self_referencing_dependency_in_persisted_draft()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Persisted self dependency validation project");
        var saveResult = await processesService.SaveAsync(BuildArtifactInputDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        await AddPersistedDependencyAsync(
            dbContextFactory,
            saveResult.Value,
            ProcessVersionStatus.Draft,
            "qa-review",
            "qa-review");

        var publishResult = await processesService.PublishAsync(saveResult.Value);

        Assert.True(publishResult.IsFailure);
        Assert.Contains(publishResult.Errors, error => error.Code == "processes.publish-branch-dependency-self-reference");
    }

    [Fact]
    public async Task StartRunAsync_rejects_published_dependency_cycle_when_storage_is_invalid()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Persisted cycle runtime validation project");
        var saveResult = await processesService.SaveAsync(BuildArtifactInputDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        await AddPersistedDependencyAsync(
            dbContextFactory,
            saveResult.Value,
            ProcessVersionStatus.Published,
            "capture-package",
            "qa-review");

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Invalid persisted graph run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Runtime graph guard validation"
        });

        Assert.True(runResult.IsFailure);
        Assert.Contains(runResult.Errors, error => error.Code == "processes.run-invalid-graph");
    }

    [Fact]
    public async Task ResolveAssignmentAsync_concurrent_step_scoped_resolution_keeps_a_single_assignment_row()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var reviewStepId = Guid.NewGuid();
        var projectId = await CreateProjectAsync(projectsService, "Concurrent assignment resolution project");
        var saveResult = await processesService.SaveAsync(BuildLinearAssignmentDefinitionEditor(projectId, roleId, intakeStepId, reviewStepId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Concurrent step assignment resolution",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify assignment upsert singularity"
        });

        Assert.True(runResult.IsSuccess);

        var resolutionTasks = Enumerable.Range(0, 8)
            .Select(async index =>
            {
                await using var resolutionScope = application.Services.CreateAsyncScope();
                var resolutionService = resolutionScope.ServiceProvider.GetRequiredService<ProcessesService>();
                return await resolutionService.ResolveAssignmentAsync(new ProcessAssignmentResolutionRequest
                {
                    ProcessRunId = runResult.Value,
                    RoleRequirementId = roleId,
                    StepDefinitionId = reviewStepId,
                    DisplayName = $"Concurrent assignee {index}",
                    ExecutorKind = "person",
                    BindingReason = $"Concurrent resolution attempt {index}",
                    IsFallback = false
                });
            })
            .ToList();

        var resolutionResults = await Task.WhenAll(resolutionTasks);

        Assert.All(
            resolutionResults,
            result => Assert.True(
                result.IsSuccess,
                string.Join(" | ", result.Errors.Select(error => $"{error.Code}:{error.Message}"))));

        var assignments = await processesService.ListAssignmentsAsync(runResult.Value);
        var scopedAssignments = assignments
            .Where(item => item.RoleRequirementId == roleId && item.StepDefinitionId == reviewStepId)
            .ToList();
        Assert.Single(scopedAssignments);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Equal(
            1,
            await verificationContext.Set<ProcessRunAssignment>()
                .CountAsync(
                    item => item.ProcessRunId == runResult.Value &&
                        item.RoleRequirementId == roleId &&
                        item.StepDefinitionId == reviewStepId));
    }

    [Fact]
    public async Task TransitionStepAsync_routes_selected_branch_and_skips_the_non_selected_path()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Branch runtime project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Taylor Branch Owner");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildBranchingDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(
            publishResult.IsSuccess,
            string.Join(", ", publishResult.Errors.Select(error => $"{error.Code}:{error.Message}")));

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Branch runtime validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Branch routing verification"
        });

        Assert.True(runResult.IsSuccess);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var intakeStep = Assert.Single(stepRuns, item => item.Title == "Capture change proposal");

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Intake captured.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var decisionStep = Assert.Single(stepRuns, item => item.Title == "Route requested revision");
        var uiReviewOutcome = Assert.Single(decisionStep.AvailableBranchOutcomes, item => item.Title == "UI architect revision");

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = decisionStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start decision routing.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = decisionStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            SelectedBranchOutcomeId = uiReviewOutcome.Id,
            Reason = "Send this change to UI review first.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var uiReviewStep = Assert.Single(stepRuns, item => item.Title == "Review UI architecture");
        var dbReviewStep = Assert.Single(stepRuns, item => item.Title == "Review data architecture");

        Assert.Equal(ProcessStepRunStatus.Ready, uiReviewStep.Status);
        Assert.Equal(ProcessStepRunStatus.Skipped, dbReviewStep.Status);
        Assert.Equal("UI architect revision", decisionStep.AvailableBranchOutcomes.Single(item => item.Id == uiReviewOutcome.Id).Title);

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = uiReviewStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start UI review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = uiReviewStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "UI review completed.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var run = Assert.Single(await processesService.ListRunsAsync(saveResult.Value, projectId), item => item.Id == runResult.Value);
        Assert.Equal(ProcessRunStatus.Completed, run.Status);

        var decisions = await processesService.ListDecisionRecordsAsync(runResult.Value);
        Assert.Contains(decisions, item => item.BranchOutcomeTitle == "UI architect revision");
    }

    [Fact]
    public async Task Workflow_repair_process_routes_qa_rejection_repair_recheck_and_release()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Workflow repair process graph project");
        var fixture = ProcessWorkflowRepairDefinitionTestFixture.Create(projectId);
        AssertWorkflowRepairGraph(fixture);

        var saveResult = await processesService.SaveAsync(fixture.Editor);

        AssertSuccess(saveResult);
        AssertSuccess(await processesService.PublishAsync(saveResult.Value));

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Workflow QA repair loop validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify deterministic workflow QA repair progression"
        });

        AssertSuccess(runResult);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        Assert.Equal(8, stepRuns.Count);
        Assert.Equal(ProcessStepRunStatus.Ready, GetStepRunByKey(stepRuns, fixture, StepKeys.Scope).Status);
        Assert.Equal(ProcessStepRunStatus.Pending, GetStepRunByKey(stepRuns, fixture, StepKeys.Architecture).Status);
        Assert.All(stepRuns, stepRun => AssertExpectedArtifactOutput(fixture, stepRun));

        await CompleteRequiredArtifactStepAsync(
            application,
            processesService,
            runResult.Value,
            GetStepRunByKey(stepRuns, fixture, StepKeys.Scope),
            ProcessArtifactKind.Brief);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        Assert.Equal(ProcessStepRunStatus.Ready, GetStepRunByKey(stepRuns, fixture, StepKeys.Architecture).Status);

        await CompleteRequiredArtifactStepAsync(
            application,
            processesService,
            runResult.Value,
            GetStepRunByKey(stepRuns, fixture, StepKeys.Architecture),
            ProcessArtifactKind.Decision);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var firstImplementationStep = GetStepRunByKey(stepRuns, fixture, StepKeys.FirstImplementation);
        Assert.Equal(ProcessStepRunStatus.Ready, firstImplementationStep.Status);

        await CompleteRequiredArtifactStepAsync(
            application,
            processesService,
            runResult.Value,
            firstImplementationStep,
            ProcessArtifactKind.Deliverable,
            assertMissingArtifactGate: true);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var qaFirstReviewStep = GetStepRunByKey(stepRuns, fixture, StepKeys.QaFirstReview);
        var repairsRequiredOutcome = Assert.Single(qaFirstReviewStep.AvailableBranchOutcomes, item => item.Title == "Repairs required");
        var firstPassApprovedOutcome = Assert.Single(qaFirstReviewStep.AvailableBranchOutcomes, item => item.Title == "Approved");
        var repairsRequiredOutcomeId = repairsRequiredOutcome.Id;

        Assert.Equal(ProcessStepRunStatus.Ready, qaFirstReviewStep.Status);
        Assert.NotEqual(Guid.Empty, repairsRequiredOutcome.Id);
        Assert.NotEqual(Guid.Empty, firstPassApprovedOutcome.Id);

        await CompleteRequiredArtifactStepAsync(
            application,
            processesService,
            runResult.Value,
            qaFirstReviewStep,
            ProcessArtifactKind.Evidence,
            selectedBranchOutcomeId: repairsRequiredOutcomeId);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        qaFirstReviewStep = GetStepRunByKey(stepRuns, fixture, StepKeys.QaFirstReview);
        var directReleaseNotesStep = GetStepRunByKey(stepRuns, fixture, StepKeys.DirectReleaseNotes);
        var repairImplementationStep = GetStepRunByKey(stepRuns, fixture, StepKeys.RepairImplementation);

        Assert.Equal(repairsRequiredOutcomeId, qaFirstReviewStep.SelectedBranchOutcomeId);
        Assert.Equal("Repairs required", qaFirstReviewStep.SelectedBranchOutcomeTitle);
        Assert.Equal(ProcessStepRunStatus.Skipped, directReleaseNotesStep.Status);
        Assert.Equal(ProcessStepRunStatus.Ready, repairImplementationStep.Status);
        Assert.Equal(ProcessStepRunStatus.Pending, GetStepRunByKey(stepRuns, fixture, StepKeys.QaRecheck).Status);

        await CompleteRequiredArtifactStepAsync(
            application,
            processesService,
            runResult.Value,
            repairImplementationStep,
            ProcessArtifactKind.Deliverable);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var qaRecheckStep = GetStepRunByKey(stepRuns, fixture, StepKeys.QaRecheck);
        var qaRecheckApprovedOutcomeId = Assert.Single(qaRecheckStep.AvailableBranchOutcomes, item => item.Title == "Approved").Id;

        Assert.Equal(ProcessStepRunStatus.Ready, qaRecheckStep.Status);
        Assert.NotEqual(Guid.Empty, qaRecheckApprovedOutcomeId);

        await CompleteRequiredArtifactStepAsync(
            application,
            processesService,
            runResult.Value,
            qaRecheckStep,
            ProcessArtifactKind.Evidence,
            selectedBranchOutcomeId: qaRecheckApprovedOutcomeId);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var releaseNotesStep = GetStepRunByKey(stepRuns, fixture, StepKeys.ReleaseNotes);

        Assert.Equal(ProcessStepRunStatus.Ready, releaseNotesStep.Status);

        await CompleteRequiredArtifactStepAsync(
            application,
            processesService,
            runResult.Value,
            releaseNotesStep,
            ProcessArtifactKind.Deliverable);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        Assert.Equal(ProcessStepRunStatus.Skipped, GetStepRunByKey(stepRuns, fixture, StepKeys.DirectReleaseNotes).Status);
        Assert.Equal(ProcessStepRunStatus.Completed, GetStepRunByKey(stepRuns, fixture, StepKeys.ReleaseNotes).Status);

        var run = Assert.Single(await processesService.ListRunsAsync(saveResult.Value, projectId), item => item.Id == runResult.Value);
        Assert.Equal(ProcessRunStatus.Completed, run.Status);

        var artifacts = await processesService.ListArtifactsAsync(runResult.Value);
        var artifactTitles = artifacts.Select(item => item.Title).ToHashSet(StringComparer.Ordinal);

        Assert.Contains(ArtifactTitles.Scope, artifactTitles);
        Assert.Contains(ArtifactTitles.Architecture, artifactTitles);
        Assert.Contains(ArtifactTitles.FirstImplementation, artifactTitles);
        Assert.Contains(ArtifactTitles.QaFirstReview, artifactTitles);
        Assert.Contains(ArtifactTitles.RepairImplementation, artifactTitles);
        Assert.Contains(ArtifactTitles.QaRecheck, artifactTitles);
        Assert.Contains(ArtifactTitles.ReleaseNotes, artifactTitles);
        Assert.DoesNotContain(ArtifactTitles.DirectReleaseNotes, artifactTitles);

        var decisions = await processesService.ListDecisionRecordsAsync(runResult.Value);
        Assert.Contains(decisions, item => item.BranchOutcomeTitle == "Repairs required");
        Assert.Contains(decisions, item => item.BranchOutcomeTitle == "Approved");
    }

    [Fact]
    public async Task TransitionStepAsync_rejects_branch_outcome_selection_for_non_completed_transition()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Branch transition guard project");
        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Invalid branch selection validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify branch outcome guard"
        });

        Assert.True(runResult.IsSuccess);

        var firstStep = Assert.Single(
            await processesService.ListStepRunsAsync(runResult.Value),
            item => item.Sequence == 0);

        var transitionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = firstStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            SelectedBranchOutcomeId = Guid.NewGuid(),
            Reason = "Branch outcomes should not be accepted here.",
            DecidedBy = "integration-tests"
        });

        Assert.True(transitionResult.IsFailure);
        Assert.Contains(transitionResult.Errors, error => error.Code == "processes.branch-outcome-invalid-transition");
    }

    [Fact]
    public async Task TransitionStepAsync_requires_branch_outcome_when_conditional_dependents_exist()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Branch outcome required project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Morgan Branch Owner");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildBranchingDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Missing branch outcome validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify branch outcome requirement"
        });

        Assert.True(runResult.IsSuccess);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var intakeStep = Assert.Single(stepRuns, item => item.Title == "Capture change proposal");

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Intake captured.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var decisionStep = Assert.Single(
            await processesService.ListStepRunsAsync(runResult.Value),
            item => item.Title == "Route requested revision");

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = decisionStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start decision routing.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var transitionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = decisionStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Attempt completion without choosing a path.",
            DecidedBy = "integration-tests"
        });

        Assert.True(transitionResult.IsFailure);
        Assert.Contains(transitionResult.Errors, error => error.Code == "processes.branch-outcome-required");
    }

    [Fact]
    public async Task PublishAsync_preserves_role_and_branch_canvas_positions_in_the_next_draft()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process canvas persistence project");
        var managerRoleId = Guid.NewGuid();
        var model = BuildBranchingDefinitionEditor(projectId, managerRoleId);
        var routingRole = Assert.Single(model.Roles);
        routingRole.CanvasX = 180;
        routingRole.CanvasY = 260;

        var decisionStep = Assert.Single(model.Steps, item => item.Title == "Route requested revision");
        decisionStep.BranchCanvasX = 960;
        decisionStep.BranchCanvasY = 220;

        var saveResult = await processesService.SaveAsync(model);

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var editor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var persistedRole = Assert.Single(editor.Roles, item => item.DisplayName == "Routing owner");
        var persistedDecisionStep = Assert.Single(editor.Steps, item => item.Title == "Route requested revision");

        Assert.Equal(180, persistedRole.CanvasX);
        Assert.Equal(260, persistedRole.CanvasY);
        Assert.Equal(960, persistedDecisionStep.BranchCanvasX);
        Assert.Equal(220, persistedDecisionStep.BranchCanvasY);
    }

    [Fact]
    public async Task GetEditorAsync_and_publish_clone_preserve_artifact_input_links()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process artifact input persistence project");
        var managerRoleId = Guid.NewGuid();
        var model = BuildArtifactInputDefinitionEditor(projectId, managerRoleId);

        var saveResult = await processesService.SaveAsync(model);

        Assert.True(saveResult.IsSuccess);

        var savedEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var savedSourceStep = Assert.Single(savedEditor.Steps, item => item.Key == "capture-package");
        var savedSourceArtifact = Assert.Single(savedSourceStep.ArtifactExpectations, item => item.Title == "Implementation package");
        var savedConsumerStep = Assert.Single(savedEditor.Steps, item => item.Key == "qa-review");
        var savedArtifactInput = Assert.Single(savedConsumerStep.ArtifactInputs);

        Assert.Equal(savedSourceArtifact.Id, savedArtifactInput.ArtifactExpectationId);

        var publishResult = await processesService.PublishAsync(saveResult.Value);

        Assert.True(publishResult.IsSuccess);

        var nextDraftEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var nextDraftSourceStep = Assert.Single(nextDraftEditor.Steps, item => item.Key == "capture-package");
        var nextDraftSourceArtifact = Assert.Single(nextDraftSourceStep.ArtifactExpectations, item => item.Title == "Implementation package");
        var nextDraftConsumerStep = Assert.Single(nextDraftEditor.Steps, item => item.Key == "qa-review");
        var nextDraftArtifactInput = Assert.Single(nextDraftConsumerStep.ArtifactInputs);

        Assert.Equal(nextDraftSourceArtifact.Id, nextDraftArtifactInput.ArtifactExpectationId);
    }

    [Fact]
    public async Task Save_export_import_and_publish_SB08_INV_001_preserve_step_operation_contract()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Step operation contract project");
        var managerRoleId = Guid.NewGuid();
        var model = BuildDefinitionEditor(projectId, managerRoleId);
        var deliveryStep = Assert.Single(model.Steps, step => step.Key == "delivery-review");
        deliveryStep.AllowedOperations =
        [
            ProcessStepOperation.WriteExternalArtifactDestination,
            ProcessStepOperation.WriteManagedProcessArtifacts
        ];
        deliveryStep.OperationTargetScope = ProcessStepTargetScope.ExternalArtifactDestination;

        var saveResult = await processesService.SaveAsync(model);

        AssertSuccess(saveResult);

        var savedEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var savedDeliveryStep = Assert.Single(savedEditor.Steps, step => step.Key == "delivery-review");

        Assert.Equal(
            [
                ProcessStepOperation.ReadProcessContext,
                ProcessStepOperation.WriteManagedProcessArtifacts,
                ProcessStepOperation.WriteExternalArtifactDestination
            ],
            savedDeliveryStep.AllowedOperations);
        Assert.Equal(ProcessStepTargetScope.ExternalArtifactDestination, savedDeliveryStep.OperationTargetScope);

        var exportEnvelope = await processesService.ExportAsync(saveResult.Value);
        var exportedDeliveryStep = Assert.Single(exportEnvelope.Definition.Steps, step => step.Key == "delivery-review");

        Assert.Equal(savedDeliveryStep.AllowedOperations, exportedDeliveryStep.AllowedOperations);
        Assert.Equal(savedDeliveryStep.OperationTargetScope, exportedDeliveryStep.OperationTargetScope);

        exportEnvelope.Definition.Id = null;
        exportEnvelope.Definition.WorkingVersionId = null;
        exportEnvelope.Definition.DefinitionConcurrencyToken = null;
        exportEnvelope.Definition.WorkingVersionConcurrencyToken = null;
        exportEnvelope.Definition.Name = "Imported step operation contract process";
        exportEnvelope.Definition.ChangeSummary = "Imported for SB08 operation contract persistence validation.";
        var importResult = await processesService.ImportAsync(exportEnvelope);

        AssertSuccess(importResult);

        var importedEditor = await processesService.GetEditorAsync(importResult.Value, projectId);
        var importedDeliveryStep = Assert.Single(importedEditor.Steps, step => step.Key == "delivery-review");

        Assert.Equal(savedDeliveryStep.AllowedOperations, importedDeliveryStep.AllowedOperations);
        Assert.Equal(ProcessStepTargetScope.ExternalArtifactDestination, importedDeliveryStep.OperationTargetScope);

        AssertSuccess(await processesService.PublishAsync(importResult.Value));

        var nextDraftEditor = await processesService.GetEditorAsync(importResult.Value, projectId);
        var nextDraftDeliveryStep = Assert.Single(nextDraftEditor.Steps, step => step.Key == "delivery-review");

        Assert.Equal(savedDeliveryStep.AllowedOperations, nextDraftDeliveryStep.AllowedOperations);
        Assert.Equal(ProcessStepTargetScope.ExternalArtifactDestination, nextDraftDeliveryStep.OperationTargetScope);
    }

    [Fact]
    public async Task PublishAsync_allocates_next_draft_version_after_the_highest_existing_version()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process publish version allocation project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var firstDraftEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);

        Assert.Equal(2, firstDraftEditor.WorkingVersionNumber);
        Assert.NotNull(firstDraftEditor.WorkingVersionId);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var workingVersion = await dbContext.Set<ProcessDefinitionVersion>()
                .SingleAsync(item => item.Id == firstDraftEditor.WorkingVersionId!.Value);

            await dbContext.Set<ProcessDefinitionVersion>().AddAsync(
                new ProcessDefinitionVersion
                {
                    ProcessDefinitionId = saveResult.Value,
                    VersionNumber = 3,
                    Status = ProcessVersionStatus.Archived,
                    ChangeSummary = "Inserted archived version to validate next draft allocation.",
                    GovernancePolicySummary = workingVersion.GovernancePolicySummary,
                    ConstitutionRuleSummary = workingVersion.ConstitutionRuleSummary,
                    OperatingModeSummary = workingVersion.OperatingModeSummary,
                    SimulationReadinessSummary = workingVersion.SimulationReadinessSummary,
                    ImportedFrom = workingVersion.ImportedFrom,
                    ImportWarnings = workingVersion.ImportWarnings,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            await dbContext.SaveChangesAsync();
        }

        var publishResult = await processesService.PublishAsync(saveResult.Value);

        Assert.True(
            publishResult.IsSuccess,
            string.Join(" | ", publishResult.Errors.Select(error => $"{error.Code}:{error.Message}")));

        var nextDraftEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);

        Assert.Equal(4, nextDraftEditor.WorkingVersionNumber);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var versions = await verificationContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == saveResult.Value)
            .OrderBy(item => item.VersionNumber)
            .Select(item => new
            {
                item.VersionNumber,
                item.Status
            })
            .ToListAsync();

        Assert.Collection(
            versions,
            item =>
            {
                Assert.Equal(1, item.VersionNumber);
                Assert.Equal(ProcessVersionStatus.Superseded, item.Status);
            },
            item =>
            {
                Assert.Equal(2, item.VersionNumber);
                Assert.Equal(ProcessVersionStatus.Published, item.Status);
            },
            item =>
            {
                Assert.Equal(3, item.VersionNumber);
                Assert.Equal(ProcessVersionStatus.Archived, item.Status);
            },
            item =>
            {
                Assert.Equal(4, item.VersionNumber);
                Assert.Equal(ProcessVersionStatus.Draft, item.Status);
            });
    }

    [Fact]
    public async Task SaveAsync_rejects_artifact_inputs_without_matching_structural_dependencies()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process artifact input validation project");
        var managerRoleId = Guid.NewGuid();
        var model = BuildArtifactInputDefinitionEditor(projectId, managerRoleId, includeDependency: false);

        var saveResult = await processesService.SaveAsync(model);

        Assert.True(saveResult.IsFailure);
        Assert.Contains(saveResult.Errors, error => error.Code == "processes.artifact-input-dependency-required");
    }

    [Fact]
    public async Task TransitionStepAsync_waits_for_all_dependencies_before_join_step_becomes_ready()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Many-to-many dependency project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Jordan Delivery Lead");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var managerRoleId = Guid.NewGuid();
        var saveResult = await processesService.SaveAsync(BuildParallelJoinDefinitionEditor(projectId, managerRoleId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Parallel join validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify wait-for-all dependencies"
        });

        Assert.True(runResult.IsSuccess);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var intakeStep = Assert.Single(stepRuns, item => item.Title == "Capture implementation package");

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Intake completed.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var qaReviewStep = Assert.Single(stepRuns, item => item.Title == "Validate QA evidence");
        var securityReviewStep = Assert.Single(stepRuns, item => item.Title == "Validate security posture");
        var releaseJoinStep = Assert.Single(stepRuns, item => item.Title == "Approve merge readiness");

        Assert.Equal(ProcessStepRunStatus.Ready, qaReviewStep.Status);
        Assert.Equal(ProcessStepRunStatus.Ready, securityReviewStep.Status);
        Assert.Equal(ProcessStepRunStatus.Pending, releaseJoinStep.Status);

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = qaReviewStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start QA review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = qaReviewStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "QA review completed.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        releaseJoinStep = Assert.Single(stepRuns, item => item.Title == "Approve merge readiness");
        Assert.Equal(ProcessStepRunStatus.Pending, releaseJoinStep.Status);

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = securityReviewStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start security review.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = securityReviewStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Security review completed.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        releaseJoinStep = Assert.Single(stepRuns, item => item.Title == "Approve merge readiness");
        Assert.Equal(ProcessStepRunStatus.WaitingApproval, releaseJoinStep.Status);
    }

    [Fact]
    public async Task Canonical_dependency_collection_survives_import_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Canonical dependency compatibility project");
        var managerPartyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, "Jordan Delivery Lead");
        var assignmentResult = await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = managerPartyId,
            Role = ProjectPartyAssignmentRole.Manager,
            IsPrimary = true,
            Source = "integration-tests"
        });

        Assert.True(assignmentResult.IsSuccess);

        var managerRoleId = Guid.NewGuid();
        var baselineSaveResult = await processesService.SaveAsync(BuildParallelJoinDefinitionEditor(projectId, managerRoleId));

        Assert.True(baselineSaveResult.IsSuccess);

        var importEnvelope = await processesService.ExportAsync(baselineSaveResult.Value);
        importEnvelope.Definition.Id = null;
        importEnvelope.Definition.WorkingVersionId = null;
        importEnvelope.Definition.DefinitionConcurrencyToken = null;
        importEnvelope.Definition.WorkingVersionConcurrencyToken = null;
        importEnvelope.Definition.Name = "Imported canonical dependency compatibility process";
        importEnvelope.Definition.ChangeSummary = "Imported for canonical dependency boundary validation.";
        var importedIntakeStep = Assert.Single(importEnvelope.Definition.Steps, item => item.Key == "capture-package");
        var importedJoinStep = Assert.Single(importEnvelope.Definition.Steps, item => item.Key == "merge-readiness");
        importedJoinStep.DependsOnStepId = importedIntakeStep.Id;
        importedJoinStep.DependsOnBranchOutcomeId = null;

        var importResult = await processesService.ImportAsync(importEnvelope);

        Assert.True(importResult.IsSuccess);

        var savedEditor = await processesService.GetEditorAsync(importResult.Value, projectId);
        var savedJoinStep = Assert.Single(savedEditor.Steps, item => item.Key == "merge-readiness");

        Assert.Equal(["qa-review", "security-review"], ResolveDependencyKeys(savedEditor, savedJoinStep));
        Assert.DoesNotContain("capture-package", ResolveDependencyKeys(savedEditor, savedJoinStep));

        var exportedEnvelope = await processesService.ExportAsync(importResult.Value);
        var exportedJoinStep = Assert.Single(exportedEnvelope.Definition.Steps, item => item.Key == "merge-readiness");

        Assert.Null(exportedJoinStep.DependsOnStepId);
        Assert.Null(exportedJoinStep.DependsOnBranchOutcomeId);
        Assert.Equal(2, exportedJoinStep.Dependencies.Count);

        Assert.True((await processesService.PublishAsync(importResult.Value)).IsSuccess);

        var nextDraftEditor = await processesService.GetEditorAsync(importResult.Value, projectId);
        var nextDraftJoinStep = Assert.Single(nextDraftEditor.Steps, item => item.Key == "merge-readiness");

        Assert.Equal(["qa-review", "security-review"], ResolveDependencyKeys(nextDraftEditor, nextDraftJoinStep));

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = importResult.Value,
            ProjectId = projectId,
            RunName = "Canonical dependency compatibility validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify canonical dependencies override stale scalar fallback"
        });

        Assert.True(runResult.IsSuccess);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var intakeStep = Assert.Single(stepRuns, item => item.Title == "Capture implementation package");

        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Intake completed.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var qaReviewStep = Assert.Single(stepRuns, item => item.Title == "Validate QA evidence");
        var securityReviewStep = Assert.Single(stepRuns, item => item.Title == "Validate security posture");
        var releaseJoinStep = Assert.Single(stepRuns, item => item.Title == "Approve merge readiness");

        Assert.Equal(ProcessStepRunStatus.Ready, qaReviewStep.Status);
        Assert.Equal(ProcessStepRunStatus.Ready, securityReviewStep.Status);
        Assert.Equal(ProcessStepRunStatus.Pending, releaseJoinStep.Status);
    }

    [Fact]
    public async Task SaveAsync_rejects_stale_editor_concurrency_tokens_after_concurrent_update()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Definition concurrency save project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var staleEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        Assert.NotNull(staleEditor.WorkingVersionId);
        Assert.NotNull(staleEditor.DefinitionConcurrencyToken);
        Assert.NotNull(staleEditor.WorkingVersionConcurrencyToken);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var definition = await dbContext.Set<ProcessDefinition>()
                .SingleAsync(item => item.Id == saveResult.Value);
            var workingVersion = await dbContext.Set<ProcessDefinitionVersion>()
                .SingleAsync(item => item.Id == staleEditor.WorkingVersionId!.Value);

            definition.Summary = "Concurrent summary update before stale save.";
            definition.UpdatedAtUtc = DateTimeOffset.UtcNow;
            workingVersion.ChangeSummary = "Concurrent draft update before stale save.";
            workingVersion.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync();
        }

        staleEditor.Summary = "Stale editor summary write.";
        staleEditor.ChangeSummary = "Stale editor draft write.";

        var staleSaveResult = await processesService.SaveAsync(staleEditor);

        Assert.True(staleSaveResult.IsFailure);
        Assert.Contains(
            staleSaveResult.Errors,
            error => error.Code == "processes.definition-concurrency-conflict");
    }

    [Fact]
    public async Task SaveAsync_rejects_stale_editor_concurrency_tokens_when_no_working_draft_exists()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Definition concurrency no-draft project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var baselineDraftEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var draftVersion = await dbContext.Set<ProcessDefinitionVersion>()
                .SingleAsync(item => item.ProcessDefinitionId == saveResult.Value && item.Status == ProcessVersionStatus.Draft);

            draftVersion.Status = ProcessVersionStatus.Archived;
            draftVersion.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync();
        }

        var staleEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);

        Assert.Null(staleEditor.WorkingVersionId);
        Assert.NotNull(staleEditor.DefinitionConcurrencyToken);

        staleEditor.ChangeSummary = baselineDraftEditor.ChangeSummary;
        staleEditor.GovernancePolicySummary = baselineDraftEditor.GovernancePolicySummary;
        staleEditor.ConstitutionRuleSummary = baselineDraftEditor.ConstitutionRuleSummary;
        staleEditor.OperatingModeSummary = baselineDraftEditor.OperatingModeSummary;
        staleEditor.SimulationReadinessSummary = baselineDraftEditor.SimulationReadinessSummary;
        staleEditor.Roles = baselineDraftEditor.Roles;
        staleEditor.Steps = baselineDraftEditor.Steps;

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var definition = await dbContext.Set<ProcessDefinition>()
                .SingleAsync(item => item.Id == saveResult.Value);

            definition.Summary = "Concurrent update before stale no-draft save.";
            definition.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync();
        }

        staleEditor.Summary = "Stale no-draft summary write.";

        var staleSaveResult = await processesService.SaveAsync(staleEditor);

        Assert.True(staleSaveResult.IsFailure);
        Assert.Contains(
            staleSaveResult.Errors,
            error => error.Code == "processes.definition-concurrency-conflict");
    }

    [Fact]
    public async Task PublishAsync_rejects_stale_publish_request_after_concurrent_update()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Definition concurrency publish project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var editor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        Assert.NotNull(editor.WorkingVersionId);
        Assert.NotNull(editor.DefinitionConcurrencyToken);
        Assert.NotNull(editor.WorkingVersionConcurrencyToken);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var definition = await dbContext.Set<ProcessDefinition>()
                .SingleAsync(item => item.Id == saveResult.Value);
            var workingVersion = await dbContext.Set<ProcessDefinitionVersion>()
                .SingleAsync(item => item.Id == editor.WorkingVersionId!.Value);

            definition.Summary = "Concurrent publish conflict setup.";
            definition.UpdatedAtUtc = DateTimeOffset.UtcNow;
            workingVersion.GovernancePolicySummary = "Concurrent governance update before publish.";
            workingVersion.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync();
        }

        var publishResult = await processesService.PublishAsync(
            new ProcessDefinitionPublishRequest
            {
                DefinitionId = saveResult.Value,
                DefinitionConcurrencyToken = editor.DefinitionConcurrencyToken,
                DraftVersionConcurrencyToken = editor.WorkingVersionConcurrencyToken
            });

        Assert.True(publishResult.IsFailure);
        Assert.Contains(
            publishResult.Errors,
            error => error.Code == "processes.publish-concurrency-conflict");
    }

    [Fact]
    public async Task Template_services_keep_role_and_artifact_editor_mapping_rules_aligned()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var packLoader = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessTemplateCatalogService>();
        var libraryService = scope.ServiceProvider.GetRequiredService<ProcessTemplateLibraryService>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        var pack = packLoader.Load();
        var roleSeed = pack.RoleTemplates.First(seed =>
            !string.IsNullOrWhiteSpace(seed.TemplateRoleKey) &&
            pack.SharedRoles.ContainsKey(seed.TemplateRoleKey));
        var sharedRoleItem = Assert.Single(
            libraryService.ListItems(ProcessTemplateLibraryCategory.Roles),
            item =>
                string.Equals(item.ScopeLabel, "Shared role library", StringComparison.Ordinal) &&
                string.Equals(item.Key, roleSeed.TemplateRoleKey, StringComparison.OrdinalIgnoreCase));

        Assert.True(catalogService.TryCreateRoleDraft(roleSeed.ActionId, 1, out var catalogRole));

        var libraryRole = libraryService.CreateRoleDraft(sharedRoleItem.ItemId, 1);
        var roleResource = pack.SharedRoles[roleSeed.TemplateRoleKey];
        var projectionRoleCase = pack.Processes.Values.First(process =>
            process.RoleUsages.Any(usage =>
                string.Equals(usage.RoleResourceKey, roleSeed.TemplateRoleKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(usage.Key, roleSeed.TemplateRoleKey, StringComparison.OrdinalIgnoreCase)));
        var projectionRoleUsage = Assert.Single(
            projectionRoleCase.RoleUsages,
            usage =>
                string.Equals(usage.RoleResourceKey, roleSeed.TemplateRoleKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(usage.Key, roleSeed.TemplateRoleKey, StringComparison.OrdinalIgnoreCase));
        var projectedRoleEnvelope = projectionService.GetProjectedEnvelope(projectionRoleCase.Key);
        var projectedRole = Assert.Single(
            projectedRoleEnvelope.Definition.Roles,
            role => string.Equals(role.Key, roleSeed.TemplateRoleKey, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(roleResource.Purpose, catalogRole.Purpose);
        Assert.Equal(roleResource.Purpose, libraryRole.Purpose);
        Assert.Equal(FirstNonEmpty(projectionRoleUsage.Purpose, roleResource.Purpose), projectedRole.Purpose);
        Assert.Equal(roleResource.StaffingIntent, catalogRole.StaffingIntent);
        Assert.Equal(roleResource.StaffingIntent, libraryRole.StaffingIntent);
        Assert.Equal(FirstNonEmpty(projectionRoleUsage.StaffingIntent, roleResource.StaffingIntent), projectedRole.StaffingIntent);
        Assert.Equal(catalogRole.PreferredExecutorKind, libraryRole.PreferredExecutorKind);
        Assert.Equal(FirstNonEmpty(projectionRoleUsage.PreferredExecutorKind, roleResource.PreferredExecutorKind), projectedRole.PreferredExecutorKind);
        Assert.Equal(catalogRole.PreferredProjectAssignmentRole, libraryRole.PreferredProjectAssignmentRole);
        Assert.Equal(
            EnumValueParser.ParseNullable<ProjectPartyAssignmentRole>(
                FirstNonEmpty(projectionRoleUsage.PreferredProjectAssignmentRole, roleResource.PreferredProjectAssignmentRole)),
            projectedRole.PreferredProjectAssignmentRole);
        Assert.Equal(catalogRole.IsRequired, libraryRole.IsRequired);
        Assert.Equal(projectionRoleUsage.IsRequired, projectedRole.IsRequired);
        Assert.Equal(catalogRole.AllowsFallback, libraryRole.AllowsFallback);
        Assert.Equal(projectionRoleUsage.AllowsFallback, projectedRole.AllowsFallback);
        Assert.Equal(catalogRole.RequiresExplicitApproval, libraryRole.RequiresExplicitApproval);
        Assert.Equal(projectionRoleUsage.RequiresExplicitApproval, projectedRole.RequiresExplicitApproval);
        Assert.Equal(catalogRole.DefaultAllocationPercent, libraryRole.DefaultAllocationPercent);
        Assert.Equal(
            projectionRoleUsage.DefaultAllocationPercent > 0
                ? projectionRoleUsage.DefaultAllocationPercent
                : roleResource.DefaultAllocationPercent,
            projectedRole.DefaultAllocationPercent);
        Assert.Equal(catalogRole.RoleTemplateSourceKey, libraryRole.RoleTemplateSourceKey);
        Assert.Equal(catalogRole.RoleTemplateSourceKey, projectedRole.RoleTemplateSourceKey);
        Assert.Equal(catalogRole.RoleTemplateSnapshotName, libraryRole.RoleTemplateSnapshotName);
        Assert.Equal(catalogRole.RoleTemplateSnapshotName, projectedRole.RoleTemplateSnapshotName);
        Assert.Equal(catalogRole.SnapshotSummary, libraryRole.SnapshotSummary);
        Assert.Equal(catalogRole.SnapshotSummary, projectedRole.SnapshotSummary);

        var artifactSeed = pack.StepTemplates.First(seed =>
            seed.Template.ArtifactExpectations.Count == 1 &&
            !string.IsNullOrWhiteSpace(seed.Template.ArtifactExpectations[0].TemplateKey) &&
            pack.SharedArtifacts.ContainsKey(seed.Template.ArtifactExpectations[0].TemplateKey) &&
            HasNoArtifactOverrides(seed.Template.ArtifactExpectations[0]));
        var artifactTemplate = Assert.Single(artifactSeed.Template.ArtifactExpectations);
        var sharedArtifactItem = Assert.Single(
            libraryService.ListItems(ProcessTemplateLibraryCategory.Artifacts),
            item =>
                string.Equals(item.ScopeLabel, "Shared artifact library", StringComparison.Ordinal) &&
                string.Equals(item.Key, artifactTemplate.TemplateKey, StringComparison.OrdinalIgnoreCase));

        Assert.True(catalogService.TryCreateStepDraft(artifactSeed.ActionId, 1, null, 0, 0, out var catalogStep));

        var catalogArtifact = Assert.Single(catalogStep.ArtifactExpectations);
        var libraryArtifact = libraryService.CreateArtifactExpectation(sharedArtifactItem.ItemId);
        var projectionArtifactCase = pack.Processes.Values
            .SelectMany(process => process.Steps.Select(step => new { process, step }))
            .SelectMany(item => item.step.ArtifactExpectations.Select(artifact => new { item.process, item.step, artifact }))
            .First(item =>
                item.step.ArtifactExpectations.Count == 1 &&
                string.Equals(item.artifact.TemplateKey, artifactTemplate.TemplateKey, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(item.artifact.TrustRequirement) &&
                string.IsNullOrWhiteSpace(item.artifact.SensitivityLevel) &&
                item.artifact.RetentionDays <= 0 &&
                string.IsNullOrWhiteSpace(item.artifact.AllowedFutureUsageSummary) &&
                string.IsNullOrWhiteSpace(item.artifact.ValidationRequirementSummary));
        var projectedEnvelope = projectionService.GetProjectedEnvelope(projectionArtifactCase.process.Key);
        var projectedStep = Assert.Single(
            projectedEnvelope.Definition.Steps,
            step => string.Equals(step.Key, projectionArtifactCase.step.Key, StringComparison.OrdinalIgnoreCase));
        var projectedArtifact = Assert.Single(projectedStep.ArtifactExpectations);

        Assert.Equal(catalogArtifact.ArtifactKind, libraryArtifact.ArtifactKind);
        Assert.Equal(catalogArtifact.Title, libraryArtifact.Title);
        Assert.Equal(catalogArtifact.IsRequired, libraryArtifact.IsRequired);
        Assert.Equal(catalogArtifact.TrustRequirement, libraryArtifact.TrustRequirement);
        Assert.Equal(catalogArtifact.TrustRequirement, projectedArtifact.TrustRequirement);
        Assert.Equal(catalogArtifact.SensitivityLevel, libraryArtifact.SensitivityLevel);
        Assert.Equal(catalogArtifact.SensitivityLevel, projectedArtifact.SensitivityLevel);
        Assert.Equal(catalogArtifact.RetentionDays, libraryArtifact.RetentionDays);
        Assert.Equal(catalogArtifact.RetentionDays, projectedArtifact.RetentionDays);
        Assert.Equal(catalogArtifact.AllowedFutureUsageSummary, libraryArtifact.AllowedFutureUsageSummary);
        Assert.Equal(catalogArtifact.AllowedFutureUsageSummary, projectedArtifact.AllowedFutureUsageSummary);
        Assert.Equal(catalogArtifact.ValidationRequirementSummary, libraryArtifact.ValidationRequirementSummary);
        Assert.Equal(catalogArtifact.ValidationRequirementSummary, projectedArtifact.ValidationRequirementSummary);
        Assert.Equal(
            string.IsNullOrWhiteSpace(projectionArtifactCase.artifact.ArtifactKind)
                ? libraryArtifact.ArtifactKind
                : EnumValueParser.ParseOrDefault(projectionArtifactCase.artifact.ArtifactKind, ProcessArtifactKind.Evidence),
            projectedArtifact.ArtifactKind);
        Assert.Equal(
            string.IsNullOrWhiteSpace(projectionArtifactCase.artifact.Title)
                ? libraryArtifact.Title
                : projectionArtifactCase.artifact.Title,
            projectedArtifact.Title);
        Assert.Equal(projectionArtifactCase.artifact.IsRequired, projectedArtifact.IsRequired);
    }

    [Fact]
    public async Task Software_delivery_template_requires_process_visible_current_run_browser_evidence_only_when_browser_workflow_is_in_scope()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var packLoader = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var pack = packLoader.Load();
        var projectedEnvelope = projectionService.GetProjectedEnvelope("software-delivery");
        var qaStep = Assert.Single(
            projectedEnvelope.Definition.Steps,
            step => string.Equals(step.Key, "qa-validation", StringComparison.OrdinalIgnoreCase));
        var qaRecheckStep = Assert.Single(
            projectedEnvelope.Definition.Steps,
            step => string.Equals(step.Key, "qa-recheck", StringComparison.OrdinalIgnoreCase));
        var regressionEvidencePack = pack.SharedArtifacts["regression-evidence-pack"];
        var qaEvidenceChecklist = pack.SharedChecklists["qa-evidence-checklist"];
        var qaRiskReviewPrompt = pack.SharedPrompts["prompt-qa-risk-review"];

        var qaContract = string.Join(
            Environment.NewLine,
            qaStep.OutputContractSummary,
            qaStep.EvidenceContractSummary,
            string.Join(Environment.NewLine, qaStep.ArtifactExpectations.Select(item => item.ValidationRequirementSummary)));
        var qaRecheckContract = string.Join(
            Environment.NewLine,
            qaRecheckStep.OutputContractSummary,
            qaRecheckStep.EvidenceContractSummary,
            string.Join(Environment.NewLine, qaRecheckStep.ArtifactExpectations.Select(item => item.ValidationRequirementSummary)));
        var sharedContract = string.Join(
            Environment.NewLine,
            regressionEvidencePack.ValidationRequirementSummary,
            string.Join(Environment.NewLine, qaEvidenceChecklist.Checks),
            string.Join(Environment.NewLine, qaEvidenceChecklist.EvidenceExpectations),
            string.Join(Environment.NewLine, qaRiskReviewPrompt.RequiredInputs),
            string.Join(Environment.NewLine, qaRiskReviewPrompt.OutputSchema),
            string.Join(Environment.NewLine, qaRiskReviewPrompt.RefusalConditions));
        var fullContract = string.Join(Environment.NewLine, qaContract, qaRecheckContract, sharedContract);

        Assert.Contains("visible browser workflow is in scope", fullContract, StringComparison.Ordinal);
        Assert.Contains("current-run process-visible browser artifacts", fullContract, StringComparison.Ordinal);
        Assert.Contains("artifacts/process-runs/<run-id>/browser/", fullContract, StringComparison.Ordinal);
        Assert.Contains("browser_snapshot or browser_evaluate", fullContract, StringComparison.Ordinal);
        Assert.Contains("browser_console_messages", fullContract, StringComparison.Ordinal);
        Assert.Contains("acceptance-state assertion", fullContract, StringComparison.Ordinal);
        Assert.Contains("missing, stale, detached, or chat-only", fullContract, StringComparison.Ordinal);

        Assert.DoesNotContain("always capture browser", fullContract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("browser proof is required for every", fullContract, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("browser artifacts are required for every", fullContract, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Blazor_process_templates_project_with_required_runtime_browser_and_writeback_contracts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var packLoader = scope.ServiceProvider.GetRequiredService<ProcessTemplatePackLoader>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var pack = packLoader.Load();
        string[] templateKeys =
        [
            "blazor-app-delivery",
            "blazor-app-repair-fix",
            "blazor-backend-feature",
            "blazor-frontend-feature",
            "blazor-fullstack-feature"
        ];

        foreach (var templateKey in templateKeys)
        {
            Assert.True(pack.Processes.ContainsKey(templateKey), $"Missing process template '{templateKey}'.");

            var projectedEnvelope = projectionService.GetProjectedEnvelope(templateKey);
            var qaStep = Assert.Single(
                projectedEnvelope.Definition.Steps,
                step => string.Equals(step.Key, "validate-blazor-runtime", StringComparison.OrdinalIgnoreCase));
            var recordStep = Assert.Single(
                projectedEnvelope.Definition.Steps,
                step => string.Equals(step.Key, "record-blazor-results", StringComparison.OrdinalIgnoreCase));
            _ = Assert.Single(
                projectedEnvelope.Definition.Steps,
                step => string.Equals(step.Key, "escalate-blazor-unresolved-repair", StringComparison.OrdinalIgnoreCase));

            Assert.NotEmpty(projectedEnvelope.Definition.Roles);
            Assert.Contains(
                qaStep.ArtifactExpectations,
                artifact => string.Equals(artifact.Title, "Blazor runtime evidence pack", StringComparison.Ordinal));
            Assert.Contains(
                recordStep.ArtifactExpectations,
                artifact => string.Equals(artifact.Title, "Run evidence index", StringComparison.Ordinal));

            var contract = BuildBlazorTemplateContract(projectedEnvelope);
            Assert.Contains("Blazor SSR, WASM, or WASM PWA", contract, StringComparison.Ordinal);
            Assert.Contains("dotnet restore", contract, StringComparison.Ordinal);
            Assert.Contains("dotnet build", contract, StringComparison.Ordinal);
            Assert.Contains("dotnet test", contract, StringComparison.Ordinal);
            Assert.Contains("Playwright", contract, StringComparison.Ordinal);
            Assert.Contains("screenshot", contract, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("browser_snapshot or browser_evaluate", contract, StringComparison.Ordinal);
            Assert.Contains("browser_console_messages", contract, StringComparison.Ordinal);
            Assert.Contains("no active JavaScript/runtime errors", contract, StringComparison.Ordinal);
            Assert.Contains("cleanup receipt", contract, StringComparison.Ordinal);
            Assert.Contains("project-structure evidence writeback", contract, StringComparison.Ordinal);
            Assert.Contains("project_structure_asset_create", contract, StringComparison.Ordinal);
            Assert.Contains("project_structure_node_create", contract, StringComparison.Ordinal);
            Assert.Contains("do not select the Error branch as a completed outcome", contract, StringComparison.Ordinal);
            Assert.Contains("run evidence index", contract, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Missing, blank, detached, stale, or chat-only screenshots are not acceptable", contract, StringComparison.Ordinal);

            var importResult = await processesService.ImportAsync(projectedEnvelope);
            Assert.True(importResult.IsSuccess, string.Join(" | ", importResult.Errors.Select(error => error.Message)));
            var publishResult = await processesService.PublishAsync(importResult.Value);
            Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));
        }

        static string BuildBlazorTemplateContract(ProcessImportExportEnvelope projectedEnvelope)
        {
            return string.Join(
                Environment.NewLine,
                projectedEnvelope.Definition.Summary,
                projectedEnvelope.Definition.ValueStatement,
                projectedEnvelope.Definition.InterfaceContractSummary,
                projectedEnvelope.Definition.GovernanceNotes,
                projectedEnvelope.Definition.GovernancePolicySummary,
                string.Join(Environment.NewLine, projectedEnvelope.Definition.Roles.SelectMany(role => new[]
                {
                    role.DisplayName,
                    role.Purpose,
                    role.StaffingIntent
                })),
                string.Join(Environment.NewLine, projectedEnvelope.Definition.Steps.SelectMany(step => new[]
                {
                    step.Title,
                    step.Subtitle,
                    step.Notes,
                    step.InputContractSummary,
                    step.OutputContractSummary,
                    step.EvidenceContractSummary,
                    step.DecisionRightsSummary,
                    step.ExceptionPolicySummary
                })),
                string.Join(
                    Environment.NewLine,
                    projectedEnvelope.Definition.Steps
                        .SelectMany(step => step.ArtifactExpectations)
                        .SelectMany(artifact => new[]
                        {
                            artifact.Title,
                            artifact.AllowedFutureUsageSummary,
                            artifact.ValidationRequirementSummary
                        })));
        }
    }

    [Fact]
    public async Task TransitionStepAsync_rejects_stale_step_run_concurrency_token_after_prior_transition()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Step transition concurrency project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Transition concurrency validation",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify stale step transition tokens are rejected"
        });

        Assert.True(runResult.IsSuccess);

        var stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var intakeStep = Assert.Single(stepRuns, item => item.Sequence == 0);
        var staleToken = intakeStep.StepRunConcurrencyToken;

        var startResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = intakeStep.Id,
                StepRunConcurrencyToken = staleToken,
                TargetStatus = ProcessStepRunStatus.InProgress,
                Reason = "Start the intake step.",
                DecidedBy = "integration-tests"
            });

        Assert.True(startResult.IsSuccess);

        stepRuns = await processesService.ListStepRunsAsync(runResult.Value);
        var updatedIntakeStep = Assert.Single(stepRuns, item => item.Id == intakeStep.Id);
        Assert.NotEqual(staleToken, updatedIntakeStep.StepRunConcurrencyToken);

        var staleTransitionResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = intakeStep.Id,
                StepRunConcurrencyToken = staleToken,
                TargetStatus = ProcessStepRunStatus.Completed,
                Reason = "Attempt completion with a stale token.",
                DecidedBy = "integration-tests"
            });

        Assert.True(staleTransitionResult.IsFailure);
        Assert.Contains(
            staleTransitionResult.Errors,
            error => error.Code == "processes.step-transition-conflict");
    }

    [Fact]
    public void Core_process_models_do_not_expose_legacy_dependency_mirror_properties()
    {
        Assert.Null(typeof(ProcessStepDefinition).GetProperty("DependsOnStepId"));
        Assert.Null(typeof(ProcessStepDefinition).GetProperty("DependsOnBranchOutcomeId"));
        Assert.Null(typeof(ProcessStepEditorModel).GetProperty("DependsOnStepId"));
        Assert.Null(typeof(ProcessStepEditorModel).GetProperty("DependsOnBranchOutcomeId"));
        Assert.Null(typeof(ProcessStepRunViewModel).GetProperty("DependsOnStepDefinitionId"));
        Assert.Null(typeof(ProcessStepRunViewModel).GetProperty("DependsOnBranchOutcomeId"));
    }

    [Fact]
    public async Task ImportAsync_maps_legacy_single_dependency_payload_into_canonical_dependency_collection()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Legacy dependency import compatibility project");
        var baselineSaveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(baselineSaveResult.IsSuccess);

        var importEnvelope = await processesService.ExportAsync(baselineSaveResult.Value);
        importEnvelope.Definition.Id = null;
        importEnvelope.Definition.WorkingVersionId = null;
        importEnvelope.Definition.DefinitionConcurrencyToken = null;
        importEnvelope.Definition.WorkingVersionConcurrencyToken = null;
        importEnvelope.Definition.Name = "Imported legacy dependency fallback process";
        importEnvelope.Definition.ChangeSummary = "Imported for legacy single-dependency compatibility validation.";
        var intakeStep = Assert.Single(importEnvelope.Definition.Steps, item => item.Key == "intake");
        var reviewStep = Assert.Single(importEnvelope.Definition.Steps, item => item.Key == "delivery-review");
        reviewStep.Dependencies.Clear();
        reviewStep.DependsOnStepId = intakeStep.Id;
        reviewStep.DependsOnBranchOutcomeId = null;

        var importResult = await processesService.ImportAsync(importEnvelope);

        Assert.True(importResult.IsSuccess);

        var importedEditor = await processesService.GetEditorAsync(importResult.Value, projectId);
        var importedReviewStep = Assert.Single(importedEditor.Steps, item => item.Key == "delivery-review");

        Assert.Equal(["intake"], ResolveDependencyKeys(importedEditor, importedReviewStep));
        Assert.Single(importedReviewStep.Dependencies);
    }

    [Fact]
    public void NormalizeDefinitionEditor_is_idempotent_for_branching_and_dependency_shapes()
    {
        var model = BuildBranchingDefinitionEditor(Guid.NewGuid(), Guid.NewGuid());

        ProcessCanvasBranching.NormalizeDefinitionEditor(model);
        var firstPass = JsonSerializer.Serialize(model);

        ProcessCanvasBranching.NormalizeDefinitionEditor(model);
        var secondPass = JsonSerializer.Serialize(model);

        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public async Task SaveAsync_preserves_child_ids_across_no_op_editor_round_trip()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Differential save no-op stability project");
        var saveResult = await processesService.SaveAsync(BuildBranchingDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var editor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var baselineIdentity = CreateEditorIdentitySnapshot(editor);

        var secondSaveResult = await processesService.SaveAsync(editor);

        Assert.True(secondSaveResult.IsSuccess);

        var reloadedEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);

        Assert.Equal(baselineIdentity, CreateEditorIdentitySnapshot(reloadedEditor));
    }

    [Fact]
    public async Task SaveAsync_targeted_step_update_preserves_unrelated_child_ids_and_artifact_links()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Differential save targeted update project");
        var saveResult = await processesService.SaveAsync(BuildArtifactInputDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var editor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var baselineIdentity = CreateEditorIdentitySnapshot(editor);
        var qaReviewStep = Assert.Single(editor.Steps, item => item.Key == "qa-review");
        qaReviewStep.Title = "Validate QA readiness evidence";

        var updatedSaveResult = await processesService.SaveAsync(editor);

        Assert.True(updatedSaveResult.IsSuccess);

        var reloadedEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var reloadedQaReviewStep = Assert.Single(reloadedEditor.Steps, item => item.Key == "qa-review");
        var capturePackageStep = Assert.Single(reloadedEditor.Steps, item => item.Key == "capture-package");
        var implementationPackage = Assert.Single(capturePackageStep.ArtifactExpectations, item => item.Title == "Implementation package");
        var artifactInput = Assert.Single(reloadedQaReviewStep.ArtifactInputs);

        Assert.Equal("Validate QA readiness evidence", reloadedQaReviewStep.Title);
        Assert.Equal(baselineIdentity, CreateEditorIdentitySnapshot(reloadedEditor));
        Assert.Equal(implementationPackage.Id, artifactInput.ArtifactExpectationId);
    }

    [Fact]
    public async Task SaveAsync_targeted_delete_removes_only_selected_branch_path_and_preserves_survivors()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Differential save targeted delete project");
        var saveResult = await processesService.SaveAsync(BuildBranchingDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(saveResult.IsSuccess);

        var editor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var routingRole = Assert.Single(editor.Roles);
        var decisionStep = Assert.Single(editor.Steps, item => item.Key == "route-change");
        var uiReviewStep = Assert.Single(editor.Steps, item => item.Key == "ui-review");
        var uiOutcome = Assert.Single(decisionStep.BranchOutcomes, item => item.Key == "ui-review");

        editor.Steps.RemoveAll(item => item.Key == "db-review");
        decisionStep.BranchOutcomes.RemoveAll(item => item.Key == "db-review");

        var deleteSaveResult = await processesService.SaveAsync(editor);

        Assert.True(deleteSaveResult.IsSuccess);

        var reloadedEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var reloadedRole = Assert.Single(reloadedEditor.Roles);
        var reloadedDecisionStep = Assert.Single(reloadedEditor.Steps, item => item.Key == "route-change");
        var reloadedUiReviewStep = Assert.Single(reloadedEditor.Steps, item => item.Key == "ui-review");
        var reloadedUiOutcome = Assert.Single(reloadedDecisionStep.BranchOutcomes, item => item.Key == "ui-review");

        Assert.DoesNotContain(reloadedEditor.Steps, item => item.Key == "db-review");
        Assert.DoesNotContain(reloadedDecisionStep.BranchOutcomes, item => item.Key == "db-review");
        Assert.Equal(routingRole.Id, reloadedRole.Id);
        Assert.Equal(decisionStep.Id, reloadedDecisionStep.Id);
        Assert.Equal(uiReviewStep.Id, reloadedUiReviewStep.Id);
        Assert.Equal(uiOutcome.Id, reloadedUiOutcome.Id);
    }

    [Fact]
    public async Task SaveAsync_rolls_back_graph_changes_when_child_identity_conflicts()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Differential save rollback project");
        var primarySaveResult = await processesService.SaveAsync(BuildBranchingDefinitionEditor(projectId, Guid.NewGuid()));
        var secondarySaveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(primarySaveResult.IsSuccess);
        Assert.True(secondarySaveResult.IsSuccess);

        var baselineEditor = await processesService.GetEditorAsync(primarySaveResult.Value, projectId);
        var baselineIdentity = CreateEditorIdentitySnapshot(baselineEditor);
        var conflictingEditor = await processesService.GetEditorAsync(primarySaveResult.Value, projectId);
        var foreignStepId = Assert.Single(
            (await processesService.GetEditorAsync(secondarySaveResult.Value, projectId)).Steps,
            item => item.Key == "intake").Id;

        Assert.NotNull(foreignStepId);

        conflictingEditor.Roles[0].DisplayName = "Routing owner should roll back";
        conflictingEditor.Steps.RemoveAll(item => item.Key == "db-review");
        var conflictingDecisionStep = Assert.Single(conflictingEditor.Steps, item => item.Key == "route-change");
        conflictingDecisionStep.BranchOutcomes.RemoveAll(item => item.Key == "db-review");
        var conflictingUiReviewStep = Assert.Single(conflictingEditor.Steps, item => item.Key == "ui-review");
        conflictingUiReviewStep.Id = foreignStepId;

        var failedSaveResult = await processesService.SaveAsync(conflictingEditor);

        Assert.True(failedSaveResult.IsFailure);
        Assert.Contains(failedSaveResult.Errors, error => error.Code == "processes.definition-unique-conflict");

        var reloadedEditor = await processesService.GetEditorAsync(primarySaveResult.Value, projectId);

        Assert.Equal(baselineIdentity, CreateEditorIdentitySnapshot(reloadedEditor));
        Assert.Equal("Routing owner", Assert.Single(reloadedEditor.Roles).DisplayName);
        Assert.Contains(reloadedEditor.Steps, item => item.Key == "db-review");
        Assert.Contains(
            Assert.Single(reloadedEditor.Steps, item => item.Key == "route-change").BranchOutcomes,
            item => item.Key == "db-review");
    }

    private static string CreateEditorIdentitySnapshot(ProcessDefinitionEditorModel editor)
    {
        return JsonSerializer.Serialize(new
        {
            Roles = editor.Roles
                .Select(role => new
                {
                    role.Id,
                    role.Key,
                    RequiredSkillIds = role.RequiredSkillIds
                        .OrderBy(item => item)
                        .ToList()
                })
                .ToList(),
            Steps = editor.Steps
                .Select(step => new
                {
                    step.Id,
                    step.Key,
                    step.DecisionRoleRequirementId,
                    BranchOutcomes = step.BranchOutcomes
                        .Select(outcome => new
                        {
                            outcome.Id,
                            outcome.Key
                        })
                        .ToList(),
                    Dependencies = ProcessCanvasBranching.GetOrderedDependencies(step)
                        .Select(dependency => new
                        {
                            dependency.Id,
                            dependency.DependsOnStepId,
                            dependency.DependsOnBranchOutcomeId
                        })
                        .ToList(),
                    RoleAssignments = step.RoleAssignments
                        .Select(assignment => new
                        {
                            assignment.Id,
                            assignment.RoleRequirementId,
                            assignment.ResponsibilityKind
                        })
                        .ToList(),
                    ArtifactExpectations = step.ArtifactExpectations
                        .OrderBy(artifact => artifact.Id)
                        .Select(artifact => new
                        {
                            artifact.Id,
                            artifact.ArtifactKind
                        })
                        .ToList(),
                    ArtifactInputs = step.ArtifactInputs
                        .Select(input => new
                        {
                            input.Id,
                            input.ArtifactExpectationId
                        })
                        .ToList()
                })
                .ToList()
        });
    }

    private static void AssertWorkflowRepairGraph(WorkflowRepairProcessDefinitionFixture fixture)
    {
        Assert.Equal(8, fixture.Editor.Steps.Count);
        Assert.All(
            fixture.Editor.Steps,
            step =>
            {
                var artifactExpectation = Assert.Single(step.ArtifactExpectations);
                Assert.Equal(fixture.ArtifactExpectationId(step.Key), artifactExpectation.Id);
            });

        var qaFirstReviewStep = Assert.Single(fixture.Editor.Steps, item => item.Key == StepKeys.QaFirstReview);
        Assert.Equal(
            [BranchOutcomeKeys.RepairsRequired, BranchOutcomeKeys.Approved],
            qaFirstReviewStep.BranchOutcomes.Select(item => item.Key).ToArray());

        var directReleaseDependency = Assert.Single(
            fixture.Editor.Steps.Single(item => item.Key == StepKeys.DirectReleaseNotes).Dependencies);
        Assert.Equal(fixture.StepId(StepKeys.QaFirstReview), directReleaseDependency.DependsOnStepId);
        Assert.Equal(fixture.BranchOutcomeId(StepKeys.QaFirstReview, BranchOutcomeKeys.Approved), directReleaseDependency.DependsOnBranchOutcomeId);

        var repairDependency = Assert.Single(
            fixture.Editor.Steps.Single(item => item.Key == StepKeys.RepairImplementation).Dependencies);
        Assert.Equal(fixture.StepId(StepKeys.QaFirstReview), repairDependency.DependsOnStepId);
        Assert.Equal(fixture.BranchOutcomeId(StepKeys.QaFirstReview, BranchOutcomeKeys.RepairsRequired), repairDependency.DependsOnBranchOutcomeId);

        var qaRecheckDependency = Assert.Single(
            fixture.Editor.Steps.Single(item => item.Key == StepKeys.QaRecheck).Dependencies);
        Assert.Equal(fixture.StepId(StepKeys.RepairImplementation), qaRecheckDependency.DependsOnStepId);
        Assert.Null(qaRecheckDependency.DependsOnBranchOutcomeId);

        var releaseNotesDependency = Assert.Single(
            fixture.Editor.Steps.Single(item => item.Key == StepKeys.ReleaseNotes).Dependencies);
        Assert.Equal(fixture.StepId(StepKeys.QaRecheck), releaseNotesDependency.DependsOnStepId);
        Assert.Equal(fixture.BranchOutcomeId(StepKeys.QaRecheck, BranchOutcomeKeys.Approved), releaseNotesDependency.DependsOnBranchOutcomeId);
    }

    private static async Task CompleteRequiredArtifactStepAsync(
        TestApplication application,
        ProcessesService processesService,
        Guid processRunId,
        ProcessStepRunViewModel stepRun,
        ProcessArtifactKind artifactKind,
        bool assertMissingArtifactGate = false,
        Guid? selectedBranchOutcomeId = null)
    {
        AssertSuccess(await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = stepRun.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = $"Start {stepRun.Title}.",
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        }));

        if (assertMissingArtifactGate)
        {
            var failedCompletionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                TargetStatus = ProcessStepRunStatus.Completed,
                SelectedBranchOutcomeId = selectedBranchOutcomeId,
                Reason = "Attempt completion before the required artifact is recorded.",
                DecidedBy = "integration-tests",
                SuppressAutomationDispatch = true
            });

            Assert.True(failedCompletionResult.IsFailure);
            Assert.Contains(failedCompletionResult.Errors, error => error.Code == "processes.step-completion-missing-required-artifacts");
        }

        var artifactOutput = Assert.Single(stepRun.ArtifactOutputs);
        var managedStoragePath = $"artifacts/workflow-repair/{stepRun.Id:N}.md";
        await WriteWorkspaceArtifactAsync(
            application,
            managedStoragePath,
            BuildWorkflowRepairManagedArtifactContent(stepRun, artifactOutput.Title));

        AssertSuccess(await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = processRunId,
            StepRunId = stepRun.Id,
            ArtifactExpectationId = artifactOutput.ArtifactExpectationId,
            ArtifactKind = artifactKind,
            Title = artifactOutput.Title,
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = $"Recorded by integration test for {stepRun.Title}.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Required artifact is present.",
            ManagedStoragePath = managedStoragePath
        }));
        AssertSuccess(await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = stepRun.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            SelectedBranchOutcomeId = selectedBranchOutcomeId,
            Reason = "Required artifact recorded.",
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        }));
    }

    private static ProcessStepRunViewModel GetStepRunByKey(
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        WorkflowRepairProcessDefinitionFixture fixture,
        string stepKey)
    {
        var stepDefinitionId = fixture.StepId(stepKey);
        return Assert.Single(stepRuns, item => item.StepDefinitionId == stepDefinitionId);
    }

    private static void AssertExpectedArtifactOutput(
        WorkflowRepairProcessDefinitionFixture fixture,
        ProcessStepRunViewModel stepRun)
    {
        var stepKey = fixture.StepIdsByKey.Single(item => item.Value == stepRun.StepDefinitionId).Key;
        var artifactOutput = Assert.Single(stepRun.ArtifactOutputs);

        Assert.Equal(GetExpectedWorkflowArtifactTitle(stepKey), artifactOutput.Title);
        Assert.True(artifactOutput.IsRequired);
    }

    private static string GetExpectedWorkflowArtifactTitle(string stepKey)
    {
        return stepKey switch
        {
            StepKeys.Scope => ArtifactTitles.Scope,
            StepKeys.Architecture => ArtifactTitles.Architecture,
            StepKeys.FirstImplementation => ArtifactTitles.FirstImplementation,
            StepKeys.QaFirstReview => ArtifactTitles.QaFirstReview,
            StepKeys.DirectReleaseNotes => ArtifactTitles.DirectReleaseNotes,
            StepKeys.RepairImplementation => ArtifactTitles.RepairImplementation,
            StepKeys.QaRecheck => ArtifactTitles.QaRecheck,
            StepKeys.ReleaseNotes => ArtifactTitles.ReleaseNotes,
            _ => throw new InvalidOperationException($"Unknown workflow process step key '{stepKey}'.")
        };
    }

    private static string BuildWorkflowRepairManagedArtifactContent(
        ProcessStepRunViewModel stepRun,
        string artifactTitle)
    {
        return $"""
            # {artifactTitle}

            Step title: {stepRun.Title}
            Sequence: {stepRun.Sequence}
            Validation source: workflow repair integration test.
            Evidence summary: This current-run artifact records the concrete scope, implementation, QA disposition, repair evidence, or release handoff required by the step contract.
            Outcome: The required artifact has durable managed content for manual transition validation.
            """;
    }

    private static void AssertSuccess(Result result)
    {
        Assert.True(result.IsSuccess, FormatErrors(result.Errors));
    }

    private static void AssertSuccess<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, FormatErrors(result.Errors));
    }

    private static string FormatErrors(IEnumerable<Error> errors)
    {
        return string.Join(" | ", errors.Select(error => $"{error.Code}:{error.Message}"));
    }

    private static ProcessDefinitionEditorModel BuildDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();
        var deliveryReadinessArtifactId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Integration delivery process",
            Summary = "Validates role-first process runtime behavior.",
            ValueStatement = "Keep definition, runtime, and governance evidence on one durable model.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Delivery work requires recorded runtime evidence.",
            ChangeSummary = "Initial integration definition.",
            ConstitutionRuleSummary = "Role contracts outlive executor changes.",
            OperatingModeSummary = "Assisted execution with explicit review.",
            SimulationReadinessSummary = "Safe for local integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "delivery-owner",
                    DisplayName = "Delivery owner",
                    Purpose = "Own the delivery readiness path.",
                    StaffingIntent = "Primary delivery-side owner for the project.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Delivery owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "intake",
                    Title = "Capture intake",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Project scope and delivery notes.",
                    OutputContractSummary = "Typed intake package.",
                    EvidenceContractSummary = "Intake evidence retained for review.",
                    DecisionRightsSummary = "Delivery owner can move intake forward.",
                    ExceptionPolicySummary = "Escalate missing scope details.",
                    TargetLeadHours = 2,
                    CanvasX = 140,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Rebind to the current delivery owner."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "delivery-review",
                    Title = "Review delivery readiness",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Typed intake package.",
                    OutputContractSummary = "Delivery readiness conclusion.",
                    EvidenceContractSummary = "Blocked reasons or readiness proof.",
                    DecisionRightsSummary = "Delivery owner decides whether to proceed or block.",
                    ExceptionPolicySummary = "Block when evidence is missing.",
                    TargetLeadHours = 4,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    CanvasX = 420,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Delivery owner remains explicitly assigned."
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Id = deliveryReadinessArtifactId,
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "Delivery readiness evidence",
                            ValidationRequirementSummary = "Human review required before final approval."
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildProductMutationLintGateDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var model = BuildDefinitionEditor(projectId, managerRoleId);
        var deliveryStep = model.Steps.Single(step => string.Equals(step.Key, "delivery-review", StringComparison.Ordinal));
        deliveryStep.Title = "Implement Blazor product component";
        deliveryStep.OutputContractSummary = "Implement the Blazor component in the product root.";
        deliveryStep.EvidenceContractSummary = "Implementation evidence retained for review.";
        deliveryStep.ArtifactExpectations =
        [
            new ProcessArtifactExpectationEditorModel
            {
                Id = Guid.NewGuid(),
                ArtifactKind = ProcessArtifactKind.Deliverable,
                Title = "Implementation change set",
                ValidationRequirementSummary = "Must list product files changed."
            }
        ];

        return model;
    }

    private static ProcessDefinitionEditorModel BuildArtifactMaterializationReactivationDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var upstreamStepId = Guid.NewGuid();
        var downstreamStepId = Guid.NewGuid();
        var materializedArtifactId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Materialization reactivation process",
            Summary = "Validates that materialized upstream artifacts reactivate blocked downstream steps.",
            ValueStatement = "Keep artifact materialization and downstream dispatch in one consistent lifecycle.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Downstream work must wait for typed upstream artifacts.",
            ChangeSummary = "SB01-INV-001 integration definition.",
            ConstitutionRuleSummary = "Artifact materialization is runtime state, not prompt text.",
            OperatingModeSummary = "Assisted execution with explicit materialization.",
            SimulationReadinessSummary = "Safe for local integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "delivery-owner",
                    DisplayName = "Delivery owner",
                    Purpose = "Own materialization validation.",
                    StaffingIntent = "Primary process owner for the project.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Delivery owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = upstreamStepId,
                    Key = "produce-materialized-artifact",
                    Title = "Produce materialized artifact",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Current process scope.",
                    OutputContractSummary = "Materialized evidence for downstream review.",
                    EvidenceContractSummary = "A concrete evidence artifact must be recorded.",
                    DecisionRightsSummary = "Delivery owner confirms materialization.",
                    ExceptionPolicySummary = "Block downstream work when the artifact is absent.",
                    TargetLeadHours = 1,
                    CanvasX = 120,
                    CanvasY = 120,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Id = materializedArtifactId,
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "Materialized evidence",
                            ValidationRequirementSummary = "Must be present before downstream review dispatches."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = downstreamStepId,
                    Key = "review-materialized-artifact",
                    Title = "Review materialized artifact",
                    StepKind = ProcessStepKind.Review,
                    InputContractSummary = "Materialized evidence from the upstream step.",
                    OutputContractSummary = "Review readiness decision.",
                    EvidenceContractSummary = "Review uses the upstream materialized evidence.",
                    DecisionRightsSummary = "Delivery owner decides whether to continue.",
                    ExceptionPolicySummary = "Remain blocked when upstream materialized evidence is absent.",
                    TargetLeadHours = 1,
                    Dependencies = CreateDependencies((upstreamStepId, null)),
                    CanvasX = 420,
                    CanvasY = 120,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ],
                    ArtifactInputs =
                    [
                        new ProcessStepArtifactInputEditorModel
                        {
                            ArtifactExpectationId = materializedArtifactId
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildRepairBranchArtifactGateDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();
        var qaStepId = Guid.NewGuid();
        var acceptedOutcomeId = Guid.NewGuid();
        var repairOutcomeId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Repair branch artifact gate process",
            Summary = "Validates artifact gates for positive versus repair branch outcomes.",
            ValueStatement = "Let negative review outcomes route to repair without pretending positive evidence exists.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Acceptance requires proof; repair routing requires an explicit branch decision.",
            ChangeSummary = "Initial integration definition.",
            ConstitutionRuleSummary = "Branch outcomes control downstream execution.",
            OperatingModeSummary = "Assisted execution with explicit review.",
            SimulationReadinessSummary = "Safe for local integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "quality-owner",
                    DisplayName = "Quality owner",
                    Purpose = "Own validation and repair routing decisions.",
                    StaffingIntent = "Primary review-side owner for the project.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Quality owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "intake",
                    Title = "Capture intake",
                    StepKind = ProcessStepKind.Start,
                    TargetLeadHours = 1,
                    CanvasX = 140,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = qaStepId,
                    Key = "qa-validation",
                    Title = "Validate implementation",
                    StepKind = ProcessStepKind.Review,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    DecisionRoleRequirementId = managerRoleId,
                    ExceptionPolicySummary = "Artifact recovery: selecting repair-required is allowed when validation evidence is missing or invalid; quality-accepted requires recorded validation evidence.",
                    TargetLeadHours = 2,
                    CanvasX = 420,
                    CanvasY = 160,
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = acceptedOutcomeId,
                            Key = "quality-accepted",
                            Title = "Quality accepted",
                            Description = "Validation proof is sufficient for release approval."
                        },
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = repairOutcomeId,
                            Key = "repair-required",
                            Title = "Repair required",
                            Description = "Validation found defects or proof gaps that must be repaired."
                        }
                    ],
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "Validation evidence pack",
                            ValidationRequirementSummary = "Required before selecting the positive quality-accepted outcome."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "approve-release",
                    Title = "Approve release",
                    StepKind = ProcessStepKind.Approval,
                    Dependencies = CreateDependencies((qaStepId, acceptedOutcomeId)),
                    TargetLeadHours = 1,
                    CanvasX = 720,
                    CanvasY = 100,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "repair-implementation",
                    Title = "Repair implementation",
                    StepKind = ProcessStepKind.Work,
                    Dependencies = CreateDependencies((qaStepId, repairOutcomeId)),
                    TargetLeadHours = 3,
                    CanvasX = 720,
                    CanvasY = 240,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildInvalidBranchDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();
        var decisionStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Invalid branch definition",
            Summary = "Ensures publish rejects orphaned branch outcomes.",
            ValueStatement = "Keep invalid branch models from being published.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Branches must route to an explicit downstream path.",
            ChangeSummary = "Initial invalid branching definition.",
            ConstitutionRuleSummary = "Branching outcomes must not be orphaned.",
            OperatingModeSummary = "Assisted execution with explicit routing.",
            SimulationReadinessSummary = "Safe for integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "routing-owner",
                    DisplayName = "Routing owner",
                    Purpose = "Own routing decisions.",
                    StaffingIntent = "Primary routing authority.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Routing owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "intake",
                    Title = "Capture intake",
                    StepKind = ProcessStepKind.Start,
                    TargetLeadHours = 1,
                    CanvasX = 140,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = decisionStepId,
                    Key = "route-change",
                    Title = "Route requested revision",
                    StepKind = ProcessStepKind.Decision,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    DecisionRoleRequirementId = managerRoleId,
                    TargetLeadHours = 2,
                    CanvasX = 420,
                    CanvasY = 160,
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = Guid.NewGuid(),
                            Key = "ui-review",
                            Title = "UI architect revision",
                            Description = "Route the change into UI review."
                        }
                    ],
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildBranchingDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();
        var decisionStepId = Guid.NewGuid();
        var uiOutcomeId = Guid.NewGuid();
        var dbOutcomeId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Branching delivery process",
            Summary = "Validates switch-style runtime routing.",
            ValueStatement = "Keep routing choices explicit and durable.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Branch routing requires an explicit decision owner and chosen outcome.",
            ChangeSummary = "Initial branching definition.",
            ConstitutionRuleSummary = "Selected branch outcomes must control the next path.",
            OperatingModeSummary = "Assisted execution with explicit routing decisions.",
            SimulationReadinessSummary = "Safe for integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "routing-owner",
                    DisplayName = "Routing owner",
                    Purpose = "Own branching decisions.",
                    StaffingIntent = "Primary routing authority for the process.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Routing owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "capture-change",
                    Title = "Capture change proposal",
                    StepKind = ProcessStepKind.Start,
                    TargetLeadHours = 1,
                    CanvasX = 140,
                    CanvasY = 160,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = decisionStepId,
                    Key = "route-change",
                    Title = "Route requested revision",
                    StepKind = ProcessStepKind.Decision,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    DecisionRoleRequirementId = managerRoleId,
                    TargetLeadHours = 2,
                    CanvasX = 420,
                    CanvasY = 160,
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = uiOutcomeId,
                            Key = "ui-review",
                            Title = "UI architect revision",
                            Description = "Route the change through UI review."
                        },
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = dbOutcomeId,
                            Key = "db-review",
                            Title = "DB architect revision",
                            Description = "Route the change through DB review."
                        }
                    ],
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "ui-review",
                    Title = "Review UI architecture",
                    StepKind = ProcessStepKind.Review,
                    Dependencies = CreateDependencies((decisionStepId, uiOutcomeId)),
                    TargetLeadHours = 2,
                    CanvasX = 720,
                    CanvasY = 100,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "db-review",
                    Title = "Review data architecture",
                    StepKind = ProcessStepKind.Review,
                    Dependencies = CreateDependencies((decisionStepId, dbOutcomeId)),
                    TargetLeadHours = 2,
                    CanvasX = 720,
                    CanvasY = 240,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildArtifactInputDefinitionEditor(Guid projectId, Guid managerRoleId, bool includeDependency = true)
    {
        var captureStepId = Guid.NewGuid();
        var reviewStepId = Guid.NewGuid();
        var packageArtifactId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Artifact input delivery process",
            Summary = "Validates persisted artifact input relations.",
            ValueStatement = "Keep artifact-consuming steps explicit and durable.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Artifact-consuming steps must keep explicit upstream structure.",
            ChangeSummary = "Initial artifact input definition.",
            ConstitutionRuleSummary = "Artifact inputs must reference explicit upstream evidence.",
            OperatingModeSummary = "Assisted execution with durable artifact contracts.",
            SimulationReadinessSummary = "Safe for integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "delivery-owner",
                    DisplayName = "Delivery owner",
                    Purpose = "Own the implementation package and QA evidence flow.",
                    StaffingIntent = "Primary delivery authority for evidence routing.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Delivery owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = captureStepId,
                    Key = "capture-package",
                    Title = "Capture implementation package",
                    StepKind = ProcessStepKind.Start,
                    TargetLeadHours = 1,
                    CanvasX = 140,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Id = packageArtifactId,
                            ArtifactKind = ProcessArtifactKind.Deliverable,
                            Title = "Implementation package",
                            ValidationRequirementSummary = "Implementation package must exist before QA review."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = reviewStepId,
                    Key = "qa-review",
                    Title = "Validate QA evidence",
                    StepKind = ProcessStepKind.Review,
                    TargetLeadHours = 2,
                    Dependencies = includeDependency
                        ? CreateDependencies((captureStepId, null))
                        : [],
                    CanvasX = 460,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ],
                    ArtifactInputs =
                    [
                        new ProcessStepArtifactInputEditorModel
                        {
                            ArtifactExpectationId = packageArtifactId
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildParallelJoinDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();
        var qaStepId = Guid.NewGuid();
        var securityStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Parallel dependency delivery process",
            Summary = "Validates wait-for-all join behavior.",
            ValueStatement = "Keep multi-input process joins explicit and durable.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Merge readiness must wait for all required upstream evidence.",
            ChangeSummary = "Initial parallel dependency definition.",
            ConstitutionRuleSummary = "Join steps must not activate until every required input has arrived.",
            OperatingModeSummary = "Assisted execution with explicit evidence gates.",
            SimulationReadinessSummary = "Safe for integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "delivery-owner",
                    DisplayName = "Delivery owner",
                    Purpose = "Own the evidence gates for merge readiness.",
                    StaffingIntent = "Primary delivery authority for the process.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Delivery owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "capture-package",
                    Title = "Capture implementation package",
                    StepKind = ProcessStepKind.Start,
                    TargetLeadHours = 1,
                    CanvasX = 140,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = qaStepId,
                    Key = "qa-review",
                    Title = "Validate QA evidence",
                    StepKind = ProcessStepKind.Review,
                    TargetLeadHours = 2,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    CanvasX = 460,
                    CanvasY = 120,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = securityStepId,
                    Key = "security-review",
                    Title = "Validate security posture",
                    StepKind = ProcessStepKind.Review,
                    TargetLeadHours = 2,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    CanvasX = 460,
                    CanvasY = 260,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "merge-readiness",
                    Title = "Approve merge readiness",
                    StepKind = ProcessStepKind.Approval,
                    RequiresApproval = true,
                    TargetLeadHours = 1,
                    Dependencies =
                    [
                        new ProcessStepDependencyEditorModel
                        {
                            Id = Guid.NewGuid(),
                            DependsOnStepId = qaStepId
                        },
                        new ProcessStepDependencyEditorModel
                        {
                            Id = Guid.NewGuid(),
                            DependsOnStepId = securityStepId
                        }
                    ],
                    CanvasX = 820,
                    CanvasY = 190,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Approver
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessDefinitionEditorModel BuildLinearAssignmentDefinitionEditor(
        Guid projectId,
        Guid roleId,
        Guid intakeStepId,
        Guid reviewStepId)
    {
        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Linear assignment race proof process",
            Summary = "Validates step-scoped assignment singularity.",
            ValueStatement = "Runtime assignment rows must stay singular.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Assignment resolution must not fork duplicate logical rows.",
            ChangeSummary = "Initial assignment race proof definition.",
            ConstitutionRuleSummary = "One step-scoped assignment row per run, role, and step.",
            OperatingModeSummary = "Assisted execution for concurrency verification.",
            SimulationReadinessSummary = "Safe for integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "delivery-owner",
                    DisplayName = "Delivery owner",
                    Purpose = "Own the assignment race proof flow.",
                    StaffingIntent = "Primary delivery owner.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Assignment race proof owner."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "capture-intake",
                    Title = "Capture intake",
                    StepKind = ProcessStepKind.Start,
                    TargetLeadHours = 1,
                    CanvasX = 140,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = reviewStepId,
                    Key = "review-intake",
                    Title = "Review intake",
                    StepKind = ProcessStepKind.Review,
                    TargetLeadHours = 2,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    CanvasX = 460,
                    CanvasY = 180,
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

    private static ProcessDefinitionEditorModel BuildDirectMessagingDefinitionEditor(Guid projectId, bool includeMessagingPolicy)
    {
        var sourceRoleId = Guid.NewGuid();
        var targetRoleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = includeMessagingPolicy
                ? "Process direct messaging policy"
                : "Process direct messaging without policy",
            Summary = "Validates process-owned direct role messaging policy and runtime enforcement.",
            ValueStatement = "Direct role messaging must stay process-owned, auditable, and deterministic.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Direct messaging is allowed only for explicit role links with explicit runtime permission.",
            ChangeSummary = "Integration coverage for direct role messaging.",
            ConstitutionRuleSummary = "No role may bypass process-owned messaging policy or governance state.",
            OperatingModeSummary = "Assisted execution or emergency governance validation.",
            SimulationReadinessSummary = "Safe for integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = sourceRoleId,
                    Key = "delivery-lead",
                    DisplayName = "Delivery lead",
                    Purpose = "Initiate direct delivery handoffs.",
                    StaffingIntent = "Primary delivery authority.",
                    PreferredExecutorKind = "person",
                    DefaultAllocationPercent = 60
                },
                new ProcessRoleEditorModel
                {
                    Id = targetRoleId,
                    Key = "review-lead",
                    DisplayName = "Review lead",
                    Purpose = "Receive delivery-side direct review handoffs.",
                    StaffingIntent = "Primary review authority.",
                    PreferredExecutorKind = "person",
                    DefaultAllocationPercent = 40
                }
            ],
            MessagingPolicies = includeMessagingPolicy
                ? [
                    new ProcessRoleMessagingPolicyEditorModel
                    {
                        SourceRoleRequirementId = sourceRoleId,
                        TargetRoleRequirementId = targetRoleId
                    }
                ]
                : [],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "capture-delivery-handoff",
                    Title = "Capture delivery handoff",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Release candidate ready for delivery handoff.",
                    OutputContractSummary = "Structured delivery handoff package.",
                    EvidenceContractSummary = "Visible published process run with run-scoped role assignments.",
                    DecisionRightsSummary = "Delivery lead confirms the package is ready for review.",
                    ExceptionPolicySummary = "Escalate when the handoff package is incomplete.",
                    TargetLeadHours = 1,
                    CanvasX = 180,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = sourceRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "review-delivery-handoff",
                    Title = "Review delivery handoff",
                    StepKind = ProcessStepKind.Review,
                    InputContractSummary = "Structured delivery handoff package.",
                    OutputContractSummary = "Reviewed handoff package ready for next execution stage.",
                    EvidenceContractSummary = "Review note or direct-message evidence attached to the run.",
                    DecisionRightsSummary = "Review lead confirms the package is reviewable.",
                    ExceptionPolicySummary = "Block the run when delivery handoff evidence is missing.",
                    TargetLeadHours = 1,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    CanvasX = 520,
                    CanvasY = 180,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = targetRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible
                        }
                    ]
                }
            ]
        };
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> StartTestRunAsync(
        ProcessesService processesService,
        Guid definitionId,
        Guid projectId,
        string runName)
    {
        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = definitionId,
            ProjectId = projectId,
            RunName = runName,
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration verification"
        });

        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));
        return runResult.Value;
    }

    private static async Task<DirectMessagingRunFixture> CreateDirectMessagingRunFixtureAsync(
        IServiceProvider serviceProvider,
        bool includeMessagingPolicy,
        ProcessOperatingMode operatingMode = ProcessOperatingMode.AssistedExecution)
    {
        var projectsService = serviceProvider.GetRequiredService<ProjectsService>();
        var processesService = serviceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(
            projectsService,
            includeMessagingPolicy
                ? "Direct messaging policy project"
                : "Direct messaging policy missing project");
        var saveResult = await processesService.SaveAsync(BuildDirectMessagingDefinitionEditor(projectId, includeMessagingPolicy));

        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));
        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var roleIds = await ResolvePublishedDirectMessagingRoleIdsAsync(dbContextFactory, saveResult.Value);
        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = includeMessagingPolicy
                ? "Direct messaging run"
                : "Direct messaging run without policy",
            OperatingMode = operatingMode,
            TriggerReason = "Integration verification for direct role messaging."
        });

        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));
        return new DirectMessagingRunFixture(projectId, saveResult.Value, runResult.Value, roleIds.SourceRoleRequirementId, roleIds.TargetRoleRequirementId);
    }

    private static async Task ResolveRunAssignmentAsync(
        ProcessesService processesService,
        Guid runId,
        Guid roleRequirementId,
        string displayName,
        bool allowsDirectMessaging)
    {
        var result = await processesService.ResolveAssignmentAsync(new ProcessAssignmentResolutionRequest
        {
            ProcessRunId = runId,
            RoleRequirementId = roleRequirementId,
            DisplayName = displayName,
            ExecutorKind = "person",
            BindingReason = $"Resolved for {displayName} during integration coverage.",
            AllowsDirectMessaging = allowsDirectMessaging
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
    }

    private static async Task<(Guid SourceRoleRequirementId, Guid TargetRoleRequirementId)> ResolvePublishedDirectMessagingRoleIdsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid definitionId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var definition = await dbContext.Set<ProcessDefinition>()
            .SingleAsync(item => item.Id == definitionId);
        Assert.True(definition.ActivePublishedVersionId.HasValue);

        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .Where(item => item.ProcessDefinitionVersionId == definition.ActivePublishedVersionId.Value)
            .ToListAsync();
        var sourceRole = Assert.Single(roles, item => item.Key == "delivery-lead");
        var targetRole = Assert.Single(roles, item => item.Key == "review-lead");
        return (sourceRole.Id, targetRole.Id);
    }

    private static async Task AddPersistedDependencyAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid definitionId,
        ProcessVersionStatus versionStatus,
        string stepKey,
        string dependsOnStepKey)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var version = await dbContext.Set<ProcessDefinitionVersion>()
            .SingleAsync(item => item.ProcessDefinitionId == definitionId && item.Status == versionStatus);
        var steps = await dbContext.Set<ProcessStepDefinition>()
            .Where(item => item.ProcessDefinitionVersionId == version.Id)
            .ToListAsync();
        var step = Assert.Single(steps, item => item.Key == stepKey);
        var dependsOnStep = Assert.Single(steps, item => item.Key == dependsOnStepKey);
        var nextDisplayOrder = (await dbContext.Set<ProcessStepDependencyDefinition>()
            .Where(item => item.StepDefinitionId == step.Id)
            .Select(item => (int?)item.DisplayOrder)
            .MaxAsync()) ?? -1;

        await dbContext.Set<ProcessStepDependencyDefinition>().AddAsync(
            new ProcessStepDependencyDefinition
            {
                Id = Guid.NewGuid(),
                StepDefinitionId = step.Id,
                DependsOnStepId = dependsOnStep.Id,
                DisplayOrder = nextDisplayOrder + 1
            });

        await dbContext.SaveChangesAsync();
    }

    private static List<string> ResolveDependencyKeys(ProcessDefinitionEditorModel editor, ProcessStepEditorModel step)
    {
        return ProcessCanvasBranching.GetOrderedDependencies(step)
            .Select(dependency => Assert.Single(editor.Steps, candidate => candidate.Id == dependency.DependsOnStepId).Key)
            .OrderBy(key => key)
            .ToList();
    }

    private static bool HasNoArtifactOverrides(ProcessTemplateArtifactExpectation artifact)
    {
        return string.IsNullOrWhiteSpace(artifact.Title) &&
               string.IsNullOrWhiteSpace(artifact.ArtifactKind) &&
               string.IsNullOrWhiteSpace(artifact.TrustRequirement) &&
               string.IsNullOrWhiteSpace(artifact.SensitivityLevel) &&
               artifact.RetentionDays <= 0 &&
               string.IsNullOrWhiteSpace(artifact.AllowedFutureUsageSummary) &&
               string.IsNullOrWhiteSpace(artifact.ValidationRequirementSummary);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static List<ProcessStepDependencyEditorModel> CreateDependencies(params (Guid StepId, Guid? BranchOutcomeId)[] items)
    {
        return items
            .Select(item => new ProcessStepDependencyEditorModel
            {
                Id = Guid.NewGuid(),
                DependsOnStepId = item.StepId,
                DependsOnBranchOutcomeId = item.BranchOutcomeId
            })
            .ToList();
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService partyDirectoryService, PartyType partyType, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests"
        });

        Assert.True(result.IsSuccess);
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

    private static JsonSerializerOptions CreateStringEnumJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record DirectMessagingRunFixture(
        Guid ProjectId,
        Guid DefinitionId,
        Guid RunId,
        Guid SourceRoleRequirementId,
        Guid TargetRoleRequirementId);
}
