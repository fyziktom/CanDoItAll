using System.Reflection;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessesServiceIntegrationTests
{
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

        Assert.NotEmpty(decisions);
        Assert.Contains(artifacts, item => item.Title == "Delivery readiness note");
        Assert.Contains(conformance, item => item.Category == nameof(ProcessStepRunStatus.Blocked));
        Assert.Equal(2, workBriefs.Count);
        Assert.NotEmpty(improvements);
        Assert.True(analytics.BlockedRuns >= 1);
        Assert.True(analytics.ImprovementCandidateCount >= 1);
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
        Assert.Equal(5, definitions.Count);
        Assert.Contains(definitions, item => item.Id == seedResult.Value.PrimaryDefinitionId);
        Assert.Contains(definitions, item => item.Id == seedResult.Value.SecondaryDefinitionId);

        var softwareDeliveryDefinition = Assert.Single(definitions, item => item.Name == "Multi-team software delivery and release governance");
        var branchingDefinition = Assert.Single(definitions, item => item.Name == "Branching code review and merge governance");
        var hotfixDefinition = Assert.Single(definitions, item => item.Name == "Emergency hotfix rollout with shard-risk governance");
        Assert.Single(definitions, item => item.Name == "Customer onboarding orchestration");
        Assert.Single(definitions, item => item.Name == "Incident response and escalation");

        var softwareDeliveryRun = Assert.Single(
            await processesService.ListRunsAsync(softwareDeliveryDefinition.Id, projectId),
            item => item.Name == "Multi-team software delivery and release governance / billing export capability");
        var softwareDeliveryStepRuns = await processesService.ListStepRunsAsync(softwareDeliveryRun.Id);
        var softwareDeliveryArtifacts = await processesService.ListArtifactsAsync(softwareDeliveryRun.Id);
        var softwareDeliveryConformance = await processesService.ListConformanceObservationsAsync(softwareDeliveryRun.Id);
        var softwareDeliveryEditor = await processesService.GetEditorAsync(softwareDeliveryDefinition.Id, projectId);

        Assert.True(softwareDeliveryStepRuns.Count >= 9);
        Assert.Contains(softwareDeliveryStepRuns, item => item.Sequence == 5 && item.Status == ProcessStepRunStatus.Blocked);
        Assert.Contains(softwareDeliveryArtifacts, item => item.Title == "Open security exception assessment for tenant export capability");
        Assert.NotEmpty(softwareDeliveryConformance);
        var releaseApprovalStepRun = Assert.Single(softwareDeliveryStepRuns, item => item.Title == "Approve release readiness");
        Assert.True(releaseApprovalStepRun.Dependencies.Count >= 3);
        Assert.Equal(3, releaseApprovalStepRun.ArtifactInputCount);
        Assert.Contains(releaseApprovalStepRun.ResponsibilityPorts, item => item.ResponsibilityKind == ProcessResponsibilityKind.Approver);
        var releaseApprovalDefinitionStep = Assert.Single(softwareDeliveryEditor.Steps, item => item.Key == "release-approval");
        Assert.Equal(3, releaseApprovalDefinitionStep.ArtifactInputs.Count);

        var hotfixRun = Assert.Single(
            await processesService.ListRunsAsync(hotfixDefinition.Id, projectId),
            item => item.Name == "Emergency hotfix rollout with shard-risk governance / checkout latency");
        var hotfixStepRuns = await processesService.ListStepRunsAsync(hotfixRun.Id);
        var hotfixArtifacts = await processesService.ListArtifactsAsync(hotfixRun.Id);
        var hotfixEditor = await processesService.GetEditorAsync(hotfixDefinition.Id, projectId);

        Assert.True(hotfixStepRuns.Count >= 7);
        Assert.Contains(hotfixStepRuns, item => item.Sequence == 5 && item.Status == ProcessStepRunStatus.Failed);
        Assert.Contains(hotfixArtifacts, item => item.Title == "Failed rollout telemetry capture and rollback trigger notes");
        var emergencyApprovalStepRun = Assert.Single(hotfixStepRuns, item => item.Title == "Approve emergency release window");
        Assert.True(emergencyApprovalStepRun.Dependencies.Count >= 2);
        Assert.Equal(2, emergencyApprovalStepRun.ArtifactInputCount);
        var emergencyApprovalDefinitionStep = Assert.Single(hotfixEditor.Steps, item => item.Key == "approve-emergency-release");
        Assert.Equal(2, emergencyApprovalDefinitionStep.ArtifactInputs.Count);

        var branchingRun = Assert.Single(
            await processesService.ListRunsAsync(branchingDefinition.Id, projectId),
            item => item.Name == "Branching code review and merge governance / account-settings UI");
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
    public async Task Canonical_dependency_collection_survives_save_publish_clone_and_runtime_when_legacy_primary_dependency_is_stale()
    {
        static List<string> ResolveDependencyKeys(ProcessDefinitionEditorModel editor, ProcessStepEditorModel step)
        {
            return ProcessCanvasBranching.GetOrderedDependencies(step)
                .Select(dependency => Assert.Single(editor.Steps, candidate => candidate.Id == dependency.DependsOnStepId).Key)
                .OrderBy(key => key)
                .ToList();
        }

        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
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
        var model = BuildParallelJoinDefinitionEditor(projectId, managerRoleId);
        var intakeDraftStep = Assert.Single(model.Steps, item => item.Key == "capture-package");
        var joinDraftStep = Assert.Single(model.Steps, item => item.Key == "merge-readiness");
        joinDraftStep.DependsOnStepId = intakeDraftStep.Id;
        joinDraftStep.DependsOnBranchOutcomeId = null;

        var saveResult = await processesService.SaveAsync(model);

        Assert.True(saveResult.IsSuccess);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var workingVersion = await dbContext.Set<ProcessDefinitionVersion>()
                .Where(item => item.ProcessDefinitionId == saveResult.Value)
                .OrderBy(item => item.Status == ProcessVersionStatus.Draft ? 0 : 1)
                .ThenByDescending(item => item.VersionNumber)
                .FirstAsync();
            var persistedIntakeStep = await dbContext.Set<ProcessStepDefinition>()
                .SingleAsync(item => item.ProcessDefinitionVersionId == workingVersion.Id && item.Key == "capture-package");
            var persistedJoinStep = await dbContext.Set<ProcessStepDefinition>()
                .SingleAsync(item => item.ProcessDefinitionVersionId == workingVersion.Id && item.Key == "merge-readiness");
            persistedJoinStep.DependsOnStepId = persistedIntakeStep.Id;
            persistedJoinStep.DependsOnBranchOutcomeId = null;
            await dbContext.SaveChangesAsync();
        }

        var savedEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var savedIntakeStep = Assert.Single(savedEditor.Steps, item => item.Key == "capture-package");
        var savedJoinStep = Assert.Single(savedEditor.Steps, item => item.Key == "merge-readiness");

        Assert.Equal(["qa-review", "security-review"], ResolveDependencyKeys(savedEditor, savedJoinStep));
        Assert.NotEqual(savedIntakeStep.Id, savedJoinStep.DependsOnStepId);

        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        var nextDraftEditor = await processesService.GetEditorAsync(saveResult.Value, projectId);
        var nextDraftJoinStep = Assert.Single(nextDraftEditor.Steps, item => item.Key == "merge-readiness");

        Assert.Equal(["qa-review", "security-review"], ResolveDependencyKeys(nextDraftEditor, nextDraftJoinStep));

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
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
    public void ValidateDefinitionEditor_is_side_effect_free_for_stale_legacy_dependency_mirrors()
    {
        var model = BuildParallelJoinDefinitionEditor(Guid.NewGuid(), Guid.NewGuid());
        var intakeStep = Assert.Single(model.Steps, item => item.Key == "capture-package");
        var joinStep = Assert.Single(model.Steps, item => item.Key == "merge-readiness");
        model.Name = string.Empty;
        joinStep.DependsOnStepId = intakeStep.Id;
        joinStep.DependsOnBranchOutcomeId = null;

        var snapshot = JsonSerializer.Serialize(model);
        var method = typeof(ProcessesService).GetMethod("ValidateDefinitionEditor", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var error = method!.Invoke(null, [model]);

        Assert.NotNull(error);
        Assert.Equal("processes.name-required", Assert.IsType<Error>(error).Code);
        Assert.Equal(snapshot, JsonSerializer.Serialize(model));
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

    private static ProcessDefinitionEditorModel BuildDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();

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
                    DependsOnStepId = intakeStepId,
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
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "Delivery readiness evidence",
                            ValidationRequirementSummary = "Human review required before final approval."
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
                    DependsOnStepId = intakeStepId,
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
                    DependsOnStepId = intakeStepId,
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
                    DependsOnStepId = decisionStepId,
                    DependsOnBranchOutcomeId = uiOutcomeId,
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
                    DependsOnStepId = decisionStepId,
                    DependsOnBranchOutcomeId = dbOutcomeId,
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
                    DependsOnStepId = includeDependency ? captureStepId : null,
                    Dependencies = includeDependency
                        ?
                        [
                            new ProcessStepDependencyEditorModel
                            {
                                Id = Guid.NewGuid(),
                                DependsOnStepId = captureStepId
                            }
                        ]
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
                    DependsOnStepId = intakeStepId,
                    Dependencies =
                    [
                        new ProcessStepDependencyEditorModel
                        {
                            Id = Guid.NewGuid(),
                            DependsOnStepId = intakeStepId
                        }
                    ],
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
                    DependsOnStepId = intakeStepId,
                    Dependencies =
                    [
                        new ProcessStepDependencyEditorModel
                        {
                            Id = Guid.NewGuid(),
                            DependsOnStepId = intakeStepId
                        }
                    ],
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
}
