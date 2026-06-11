using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessTemplateExecutionE2ETests {
    [Fact]
    public async Task Blazor_app_delivery_template_SB03_INV_001_completes_through_automation_dispatch_finalizer_and_readback() {
        const string validationLabel = "Blazor process automation validation";

        await using var application = await ProcessTemplateAutomationTestSupport.CreateProcessMockEnabledApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await ProcessTemplateAutomationTestSupport.CreateProjectAsync(
            projectsService,
            "Blazor process automation validation project",
            "Execution");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build governed Blazor app through automation",
                "Implementation",
                "Build a Blazor WASM PWA with runtime/browser proof and project-structure writeback through automation dispatch.",
                null,
                ObjectSubtype: "task"));

        var result = await ProcessTemplateAutomationTestSupport.ExecuteTemplateWithProcessMockAgentsAsync(
            scope.ServiceProvider,
            "blazor-app-delivery",
            projectId,
            validationLabel,
            $"{validationLabel} launch",
            "Validate a Blazor template through automation dispatch, finalizer completion, and managed artifact readback.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["blazor-delivery-manager"] = ProcessMockAgentRoleKeys.ProductOwner,
                ["blazor-solution-architect"] = ProcessMockAgentRoleKeys.Architect,
                ["blazor-engineer"] = ProcessMockAgentRoleKeys.Developer,
                ["blazor-qa-lead"] = ProcessMockAgentRoleKeys.Qa
            },
            new ProcessProjectStructureContext {
                ProjectId = projectId,
                NodeId = "process-definition:blazor-app-delivery",
                NodeTitle = "Blazor app delivery",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            },
            TimeSpan.FromSeconds(120));

        AssertStep(result.StepRuns, "Resolve Blazor delivery contract", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Build Blazor application", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Validate Blazor runtime and browser evidence", ProcessStepRunStatus.Completed, "Quality accepted");
        AssertStep(result.StepRuns, "Record Blazor results and evidence index", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Repair Blazor validation findings", ProcessStepRunStatus.Skipped);
        AssertStep(result.StepRuns, "Revalidate Blazor repair", ProcessStepRunStatus.Skipped);
        AssertStep(result.StepRuns, "Record repaired Blazor results and evidence index", ProcessStepRunStatus.Skipped);
        AssertStep(result.StepRuns, "Escalate unresolved Blazor repair findings", ProcessStepRunStatus.Skipped);

        AssertArtifact(result.ArtifactRecords, "Blazor implementation change set", ProcessArtifactKind.Deliverable);
        AssertArtifact(result.ArtifactRecords, "Blazor runtime evidence pack", ProcessArtifactKind.Evidence);
        AssertArtifact(result.ArtifactRecords, "Project-structure result writeback summary", ProcessArtifactKind.Brief);
        AssertFinalizerSummaries(result.ExecutionRuns);
    }

    [Fact]
    public async Task Software_delivery_template_SB04_INV_001_completes_multi_team_governance_through_automation_dispatch() {
        const string validationLabel = "Multi-team software delivery automation validation";

        await using var application = await ProcessTemplateAutomationTestSupport.CreateProcessMockEnabledApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await ProcessTemplateAutomationTestSupport.CreateProjectAsync(
            projectsService,
            "Multi-team software delivery automation validation project",
            "Release");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Ship governed multi-team software change",
                "Release",
                "Coordinate implementation, peer review, QA, security, release approval, evidence writeback, and post-release learning.",
                null,
                ObjectSubtype: "epic"));

        var result = await ProcessTemplateAutomationTestSupport.ExecuteTemplateWithProcessMockAgentsAsync(
            scope.ServiceProvider,
            "software-delivery",
            projectId,
            validationLabel,
            $"{validationLabel} launch",
            "Validate the software-delivery representative template through production automation dispatch and multi-role governance.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["product-owner"] = ProcessMockAgentRoleKeys.ProductOwner,
                ["delivery-manager"] = ProcessMockAgentRoleKeys.ProductOwner,
                ["solution-architect"] = ProcessMockAgentRoleKeys.Architect,
                ["lead-engineer"] = ProcessMockAgentRoleKeys.Developer,
                ["qa-lead"] = ProcessMockAgentRoleKeys.Qa,
                ["security-reviewer"] = ProcessMockAgentRoleKeys.RepairDeveloper,
                ["release-manager"] = ProcessMockAgentRoleKeys.ReleaseManager
            },
            new ProcessProjectStructureContext {
                ProjectId = projectId,
                NodeId = "process-definition:software-delivery",
                NodeTitle = "Multi-team software delivery",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            },
            TimeSpan.FromSeconds(150));

        AssertStep(result.StepRuns, "Clarify .NET scope and app type boundary", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Run .NET architecture design and review subprocess", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Run .NET implementation slice subprocess", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Complete peer review and integration readiness", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Run QA validation and runtime or browser proof", ProcessStepRunStatus.Completed, "Quality accepted");
        AssertStep(result.StepRuns, "Perform security and data-handling review", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Record .NET run commands under process run node", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Capture and store .NET UI screenshots", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Approve first-pass release readiness", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Execute first-pass controlled release rollout", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Capture first-pass post-release learning", ProcessStepRunStatus.Completed);
        AssertStep(result.StepRuns, "Repair validation findings", ProcessStepRunStatus.Skipped);
        AssertStep(result.StepRuns, "Re-run QA validation and runtime or browser proof after repair", ProcessStepRunStatus.Skipped);
        AssertStep(result.StepRuns, "Escalate unresolved repair findings", ProcessStepRunStatus.Skipped);

        AssertArtifact(result.ArtifactRecords, "Scope boundary packet", ProcessArtifactKind.Brief);
        AssertArtifact(result.ArtifactRecords, "Implementation change set", ProcessArtifactKind.Deliverable);
        AssertArtifact(result.ArtifactRecords, "Regression evidence pack", ProcessArtifactKind.Evidence);
        AssertArtifact(result.ArtifactRecords, "Release approval record", ProcessArtifactKind.Decision);
        AssertFinalizerSummaries(result.ExecutionRuns);
    }

    [Fact]
    public async Task Blazor_app_delivery_template_runs_from_project_structure_context_with_artifacts_and_readback() {
        const string validationLabel = "Blazor process execution validation";
        const string managedArtifactRoot = "artifacts/software/blazor-e2e";

        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        var projectId = await CreateProjectAsync(projectsService);
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build governed Blazor app",
                "Implementation",
                "Build a Blazor WASM PWA with build/test/runtime/browser proof and project-structure writeback.",
                null,
                ObjectSubtype: "task"));
        var envelope = projectionService.GetProjectedEnvelope(
            "blazor-app-delivery",
            projectId,
            validationLabel);

        var importResult = await processesService.ImportAsync(envelope);
        Assert.True(importResult.IsSuccess, string.Join(" | ", importResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(importResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest {
            ProcessDefinitionId = importResult.Value,
            ProjectId = projectId,
            RunName = $"{validationLabel} run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate a software template from project-structure context.",
            ProjectStructureContext = new ProcessProjectStructureContext {
                ProjectId = projectId,
                NodeId = "process-definition:blazor-app-delivery",
                NodeTitle = "Blazor app delivery",
                ParentNodeId = workItem.Id,
                ParentNodeTitle = workItem.Title
            }
        });
        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

        await CompleteStepAsync(
            application,
            processesService,
            runResult.Value,
            "Resolve Blazor delivery contract",
            validationLabel,
            managedArtifactRoot,
            selectedBranchOutcomeTitle: ProcessCanvasBranching.DefaultRouteTitle);
        await CompleteStepAsync(
            application,
            processesService,
            runResult.Value,
            "Build Blazor application",
            validationLabel,
            managedArtifactRoot,
            selectedBranchOutcomeTitle: ProcessCanvasBranching.DefaultRouteTitle);
        await CompleteStepAsync(
            application,
            processesService,
            runResult.Value,
            "Validate Blazor runtime and browser evidence",
            validationLabel,
            managedArtifactRoot,
            selectedBranchOutcomeTitle: "Quality accepted");
        await CompleteStepAsync(
            application,
            processesService,
            runResult.Value,
            "Record Blazor results and evidence index",
            validationLabel,
            managedArtifactRoot);

        var runDetails = await processesService.GetRunDetailsAsync(runResult.Value);
        var activePathSteps = runDetails.StepRuns
            .Where(step => step.Title is not
                "Repair Blazor validation findings" and not
                "Revalidate Blazor repair" and not
                "Record repaired Blazor results and evidence index" and not
                "Escalate unresolved Blazor repair findings")
            .ToArray();

        Assert.All(activePathSteps, step => Assert.Equal(ProcessStepRunStatus.Completed, step.Status));
        Assert.Contains(runDetails.StepRuns, step =>
            step.Title == "Repair Blazor validation findings" &&
            step.Status == ProcessStepRunStatus.Skipped);
        Assert.Contains(runDetails.StepRuns, step =>
            step.Title == "Escalate unresolved Blazor repair findings" &&
            step.Status == ProcessStepRunStatus.Skipped);
        Assert.Contains(runDetails.Artifacts, artifact =>
            artifact.Title == "Blazor implementation change set" &&
            artifact.ArtifactKind == ProcessArtifactKind.Deliverable);
        Assert.Contains(runDetails.Artifacts, artifact =>
            artifact.Title == "Blazor runtime evidence pack" &&
            artifact.ArtifactKind == ProcessArtifactKind.Evidence);
        Assert.Contains(runDetails.Artifacts, artifact =>
            artifact.Title == "Project-structure result writeback summary" &&
            artifact.ManagedStoragePath == $"{managedArtifactRoot}/record-blazor-results-and-evidence-index-project-structure-result-writeback-summary.md");
        Assert.Contains(runDetails.Assignments, assignment =>
            assignment.RoleDisplayName == "Blazor implementation engineer" &&
            assignment.ExecutorKind == ProcessExecutorKindNames.AiAgent);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService) {
        var result = await projectsService.SaveAsync(new ProjectEditorModel {
            Name = "Blazor process execution validation project",
            Description = "Validates a software template through process services.",
            Objective = "Confirm Blazor template import, start, artifact recording, branch routing, and readback.",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task CompleteStepAsync(
        TestApplication application,
        ProcessesService processesService,
        Guid runId,
        string stepTitle,
        string validationLabel,
        string managedArtifactRoot,
        string? selectedBranchOutcomeTitle = null) {
        var runDetails = await processesService.GetRunDetailsAsync(runId);
        var step = Assert.Single(runDetails.StepRuns, item => item.Title == stepTitle);

        if (step.Status == ProcessStepRunStatus.Ready) {
            var startResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest {
                StepRunId = step.Id,
                TargetStatus = ProcessStepRunStatus.InProgress,
                Reason = $"{validationLabel} started {stepTitle}.",
                DecidedBy = "integration-test",
                SuppressAutomationDispatch = true
            });
            Assert.True(startResult.IsSuccess, string.Join(" | ", startResult.Errors.Select(error => error.Message)));

            runDetails = await processesService.GetRunDetailsAsync(runId);
            step = Assert.Single(runDetails.StepRuns, item => item.Id == step.Id);
        }

        foreach (var artifactOutput in step.ArtifactOutputs) {
            var managedStoragePath = $"{managedArtifactRoot}/{Slugify(stepTitle)}-{Slugify(artifactOutput.Title)}.md";
            await WriteWorkspaceArtifactAsync(
                application,
                managedStoragePath,
                BuildManagedArtifactContent(step, artifactOutput.Title, validationLabel));

            var artifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest {
                ProcessRunId = runId,
                StepRunId = step.Id,
                ArtifactExpectationId = artifactOutput.ArtifactExpectationId,
                ArtifactKind = ResolveArtifactKind(step, artifactOutput),
                Title = artifactOutput.Title,
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ProcessSensitivityLevel.Internal,
                ProvenanceSummary = $"Recorded by {validationLabel} for {stepTitle}.",
                AllowedFutureUsageSummary = "Reusable by downstream Blazor delivery validation steps.",
                ReviewSummary = "Deterministic software-process artifact recorded to satisfy the required handoff.",
                ManagedStoragePath = managedStoragePath
            });
            Assert.True(artifactResult.IsSuccess, string.Join(" | ", artifactResult.Errors.Select(error => error.Message)));
        }

        var selectedBranchOutcomeId = ResolveBranchOutcomeId(step, selectedBranchOutcomeTitle);

        var completionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest {
            StepRunId = step.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            SelectedBranchOutcomeId = selectedBranchOutcomeId,
            Reason = $"{validationLabel} completed {stepTitle} with required artifacts.",
            DecidedBy = "integration-test",
            SuppressAutomationDispatch = true
        });
        Assert.True(completionResult.IsSuccess, string.Join(" | ", completionResult.Errors.Select(error => error.Message)));
    }

    private static string BuildManagedArtifactContent(
        ProcessStepRunViewModel step,
        string artifactTitle,
        string validationLabel) {
        return $"""
            # {artifactTitle}

            Step title: {step.Title}
            Validation source: {validationLabel}.
            Evidence summary: The artifact records current-run Blazor process evidence, commands, browser proof placeholders, project-structure writeback references, and downstream handoff context.
            Outcome: Required software-process artifact contract is satisfied for deterministic integration validation.
            """;
    }

    private static ProcessArtifactKind ResolveArtifactKind(
        ProcessStepRunViewModel step,
        ProcessStepRunArtifactPortViewModel artifactOutput) {
        return step.ArtifactExpectations
            .Single(expectation => expectation.ArtifactExpectationId == artifactOutput.ArtifactExpectationId)
            .ArtifactKind;
    }

    private static Guid? ResolveBranchOutcomeId(
        ProcessStepRunViewModel step,
        string? selectedBranchOutcomeTitle) {
        if (string.IsNullOrWhiteSpace(selectedBranchOutcomeTitle)) {
            return null;
        }

        var selectedBranchOutcome = step.AvailableBranchOutcomes.SingleOrDefault(
            outcome => string.Equals(outcome.Title, selectedBranchOutcomeTitle, StringComparison.OrdinalIgnoreCase));
        Assert.True(
            selectedBranchOutcome is not null,
            $"Step '{step.Title}' did not expose branch outcome '{selectedBranchOutcomeTitle}'. Available outcomes: {FormatBranchOutcomes(step)}.");

        return selectedBranchOutcome.Id;
    }

    private static string FormatBranchOutcomes(ProcessStepRunViewModel step) {
        return step.AvailableBranchOutcomes.Count == 0
            ? "<none>"
            : string.Join(", ", step.AvailableBranchOutcomes.Select(outcome => $"{outcome.Title} [{outcome.Id:D}]"));
    }

    private static async Task WriteWorkspaceArtifactAsync(
        TestApplication application,
        string relativePath,
        string content) {
        var fullPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);
    }

    private static string Slugify(string value) {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')
            .ToArray();
        var collapsed = string.Join(
            "-",
            new string(chars)
                .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return string.IsNullOrWhiteSpace(collapsed)
            ? "artifact"
            : collapsed;
    }

    private static void AssertStep(
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        string title,
        ProcessStepRunStatus expectedStatus,
        string? expectedBranchTitle = null) {
        var step = Assert.Single(stepRuns, item => item.Title == title);
        Assert.Equal(expectedStatus, step.Status);
        if (!string.IsNullOrWhiteSpace(expectedBranchTitle)) {
            Assert.Equal(expectedBranchTitle, step.SelectedBranchOutcomeTitle);
        }
    }

    private static void AssertArtifact(
        IReadOnlyList<ProcessArtifactRecord> artifactRecords,
        string title,
        ProcessArtifactKind artifactKind) {
        Assert.Contains(
            artifactRecords,
            artifact => artifact.Title == title &&
                artifact.ArtifactKind == artifactKind &&
                !string.IsNullOrWhiteSpace(artifact.ManagedStoragePath));
    }

    private static void AssertFinalizerSummaries(IReadOnlyList<ExecutionRunRecord> executionRuns) {
        Assert.All(executionRuns, executionRun => {
            Assert.Contains("\"status\"", executionRun.ResultSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Completed", executionRun.ResultSummary, StringComparison.OrdinalIgnoreCase);
        });
    }
}
