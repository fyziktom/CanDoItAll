using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class BusinessPlanProcessPostgresIntegrationTests
{
    private const string RepositoryRoot = @"C:\repositories\CanDoItAll";

    [Fact]
    public async Task Business_plan_template_includes_atomic_product_evidence_review()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        var envelope = projectionService.GetProjectedEnvelope(
            "business-plan-development",
            definitionName: "Business plan product evidence validation");
        Assert.NotNull(envelope.Definition);
        var definition = envelope.Definition!;

        var intake = Assert.Single(definition.Steps, step => step.Key == "strategy-intake");
        var productReview = Assert.Single(definition.Steps, step => step.Key == "product-evidence-review");
        var businessPlan = Assert.Single(definition.Steps, step => step.Key == "business-plan-draft");

        Assert.Equal(ProcessStepKind.Work, productReview.StepKind);
        Assert.Contains(productReview.Dependencies, dependency => dependency.DependsOnStepId == intake.Id);
        var productArtifact = Assert.Single(productReview.ArtifactExpectations, artifact => artifact.Title == "Product evidence assessment");
        Assert.Contains(businessPlan.Dependencies, dependency => dependency.DependsOnStepId == productReview.Id);
        Assert.Contains(businessPlan.ArtifactInputs, input => input.ArtifactExpectationId == productArtifact.Id);
    }

    [Fact]
    public async Task Business_plan_process_runs_with_business_artifacts_evidence_and_statuses()
    {
        const string validationLabel = "Business-analysis scenario validation";
        const string managedArtifactRoot = "artifacts/business/analysis-validation";

        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();

        var projectId = await CreateProjectAsync(projectsService);
        var envelope = projectionService.GetProjectedEnvelope(
            "business-plan-development",
            projectId,
            validationLabel);
        var definition = envelope.Definition ?? throw new InvalidOperationException("Business plan process definition was not projected.");

        Assert.DoesNotContain(definition.Steps, step => step.AllowedOperations.Contains(ProcessStepOperation.MutateProductTarget));
        Assert.All(definition.Steps, step =>
        {
            Assert.DoesNotContain("software", step.Title, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("developer", step.Title, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".net", step.Title, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("blazor", step.Title, StringComparison.OrdinalIgnoreCase);
        });

        var importResult = await processesService.ImportAsync(envelope);
        Assert.True(importResult.IsSuccess, string.Join(" | ", importResult.Errors.Select(error => error.Message)));
        var publishResult = await processesService.PublishAsync(importResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = importResult.Value,
            ProjectId = projectId,
            RunName = $"{validationLabel} run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate a non-software business-analysis process scenario."
        });
        Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

        await CompleteBusinessPlanRunAsync(
            application,
            processesService,
            runResult.Value,
            validationLabel,
            managedArtifactRoot);

        await AssertBusinessPlanRunCompletedAsync(
            application,
            processesService,
            runResult.Value,
            validationLabel,
            managedArtifactRoot);
    }

    [Fact]
    public async Task Business_plan_process_SB05_INV_001_completes_through_automation_dispatch_finalizer_and_readback()
    {
        const string validationLabel = "Business-analysis automation validation";

        await using var application = await ProcessTemplateAutomationTestSupport.CreateProcessMockEnabledApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var projectId = await ProcessTemplateAutomationTestSupport.CreateProjectAsync(
            projectsService,
            "Business-analysis automation validation project",
            "Planning");

        var result = await ProcessTemplateAutomationTestSupport.ExecuteTemplateWithProcessMockAgentsAsync(
            scope.ServiceProvider,
            "business-plan-development",
            projectId,
            validationLabel,
            $"{validationLabel} launch",
            "Validate the business-plan representative template through automation dispatch, finalizer completion, and business artifact readback.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["business-strategist"] = ProcessMockAgentRoleKeys.ProductOwner,
                ["financial-strategist"] = ProcessMockAgentRoleKeys.Developer,
                ["marketing-specialist"] = ProcessMockAgentRoleKeys.ReleaseManager
            },
            timeout: TimeSpan.FromSeconds(90));

        AssertAutomationStep(result.StepRuns, "Capture strategy intake", ProcessStepRunStatus.Completed);
        AssertAutomationStep(result.StepRuns, "Review product evidence", ProcessStepRunStatus.Completed);
        AssertAutomationStep(result.StepRuns, "Draft business plan", ProcessStepRunStatus.Completed);
        AssertAutomationStep(result.StepRuns, "Model financial assumptions", ProcessStepRunStatus.Completed);
        AssertAutomationStep(result.StepRuns, "Prepare marketing plan", ProcessStepRunStatus.Completed);
        AssertAutomationStep(result.StepRuns, "Review integrated business plan", ProcessStepRunStatus.Completed, "Approved");
        AssertAutomationStep(result.StepRuns, "Publish approved plan handoff", ProcessStepRunStatus.Completed);
        AssertAutomationStep(result.StepRuns, "Capture blocked-plan corrections", ProcessStepRunStatus.Skipped);

        Assert.DoesNotContain(result.StepRuns, step => step.Title.Contains("software", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.StepRuns, step => step.Title.Contains(".net", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.StepRuns, step => step.Title.Contains("blazor", StringComparison.OrdinalIgnoreCase));
        AssertAutomationArtifact(result.ArtifactRecords, "Business plan", ProcessArtifactKind.Deliverable);
        AssertAutomationArtifact(result.ArtifactRecords, "Financial model and sensitivity note", ProcessArtifactKind.Dataset);
        AssertAutomationArtifact(result.ArtifactRecords, "Go-to-market and experiment plan", ProcessArtifactKind.Deliverable);
        AssertAutomationArtifact(result.ArtifactRecords, "Integrated business plan review", ProcessArtifactKind.Decision);
        AssertAutomationFinalizerSummaries(result.ExecutionRuns);
    }

    [Fact]
    public async Task Business_plan_process_projects_and_runs_on_postgresql()
    {
        var availability = await PostgresTestAvailability.EnsureAvailableAsync(RepositoryRoot);
        Assert.True(availability.IsAvailable, availability.Message);
        Assert.False(string.IsNullOrWhiteSpace(availability.ConnectionString));

        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-business-plan-postgres");
        var databaseName = $"cditall_business_{Guid.NewGuid():N}"[..30];
        await CreateDatabaseAsync(availability.ConnectionString!, databaseName);

        try
        {
            var profile = testEnvironment.CreatePostgreSqlProfile(
                "business-plan-postgres",
                BuildDatabaseConnectionString(availability.ConnectionString!, databaseName));
            await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
            {
                TestEnvironment = testEnvironment,
                ActiveProfile = profile
            });
            await using var scope = application.Services.CreateAsyncScope();
            var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
            var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();
            var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();

            var agents = await workspaceFactory.GetOrganizationWorkspaceService().ListAgentsAsync(includeTemplates: false);
            Assert.Contains(agents, agent => string.Equals(agent.Name, "Business Strategist", StringComparison.Ordinal));
            Assert.Contains(agents, agent => string.Equals(agent.Name, "Financial Strategist", StringComparison.Ordinal));
            Assert.Contains(agents, agent => string.Equals(agent.Name, "Marketing Specialist", StringComparison.Ordinal));

            var projectId = await CreateProjectAsync(projectsService);
            var envelope = projectionService.GetProjectedEnvelope(
                "business-plan-development",
                projectId,
                "PostgreSQL business plan validation");
            var importResult = await processesService.ImportAsync(envelope);
            Assert.True(importResult.IsSuccess, string.Join(" | ", importResult.Errors.Select(error => error.Message)));
            var publishResult = await processesService.PublishAsync(importResult.Value);
            Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

            var runResult = await processesService.StartRunAsync(new ProcessRunStartRequest
            {
                ProcessDefinitionId = importResult.Value,
                ProjectId = projectId,
                RunName = "PostgreSQL business plan validation run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Validate non-code process template and specialist handoffs on PostgreSQL."
            });
            Assert.True(runResult.IsSuccess, string.Join(" | ", runResult.Errors.Select(error => error.Message)));

            const string validationLabel = "PostgreSQL business-plan validation";
            const string managedArtifactRoot = "artifacts/business/postgres-validation";

            await CompleteBusinessPlanRunAsync(
                application,
                processesService,
                runResult.Value,
                validationLabel,
                managedArtifactRoot);

            await AssertBusinessPlanRunCompletedAsync(
                application,
                processesService,
                runResult.Value,
                validationLabel,
                managedArtifactRoot);
        }
        finally
        {
            await DropDatabaseAsync(availability.ConnectionString!, databaseName);
        }
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = "Business plan PostgreSQL validation project",
            Description = "Validates the business-plan process template against PostgreSQL.",
            Objective = "Confirm strategy, finance, and marketing handoffs persist and progress.",
            CurrentPhase = "Planning"
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task CompleteBusinessPlanRunAsync(
        TestApplication application,
        ProcessesService processesService,
        Guid runId,
        string validationLabel,
        string managedArtifactRoot)
    {
        await CompleteStepAsync(application, processesService, runId, "strategy-intake", validationLabel, managedArtifactRoot);
        await CompleteStepAsync(application, processesService, runId, "product-evidence-review", validationLabel, managedArtifactRoot);
        await CompleteStepAsync(application, processesService, runId, "business-plan-draft", validationLabel, managedArtifactRoot);
        await CompleteStepAsync(application, processesService, runId, "financial-modeling", validationLabel, managedArtifactRoot);
        await CompleteStepAsync(application, processesService, runId, "marketing-plan", validationLabel, managedArtifactRoot);
        await CompleteStepAsync(
            application,
            processesService,
            runId,
            "integrated-review",
            validationLabel,
            managedArtifactRoot,
            selectedBranchOutcomeKey: "approved");
        await CompleteStepAsync(application, processesService, runId, "approved-execution-handoff", validationLabel, managedArtifactRoot);
    }

    private static async Task AssertBusinessPlanRunCompletedAsync(
        TestApplication application,
        ProcessesService processesService,
        Guid runId,
        string validationLabel,
        string managedArtifactRoot)
    {
        var runDetails = await processesService.GetRunDetailsAsync(runId);
        Assert.All(
            runDetails.StepRuns.Where(step => step.Title != "Capture blocked-plan corrections"),
            step => Assert.Equal(ProcessStepRunStatus.Completed, step.Status));
        Assert.Equal(
            ProcessStepRunStatus.Skipped,
            Assert.Single(runDetails.StepRuns, step => step.Title == "Capture blocked-plan corrections").Status);
        Assert.Equal(6, runDetails.Artifacts.Count);
        Assert.Contains(
            runDetails.Artifacts,
            artifact => artifact.Title == "Product evidence assessment" &&
                artifact.ArtifactKind == ProcessArtifactKind.Evidence);
        Assert.Contains(
            runDetails.Artifacts,
            artifact => artifact.Title == "Financial model and sensitivity note" &&
                artifact.ArtifactKind == ProcessArtifactKind.Dataset);
        Assert.Contains(
            runDetails.Artifacts,
            artifact => artifact.Title == "Go-to-market and experiment plan" &&
                artifact.ArtifactKind == ProcessArtifactKind.Deliverable);
        Assert.Contains(
            runDetails.Artifacts,
            artifact => artifact.Title == "Integrated business plan review" &&
                artifact.TrustStatus == ProcessArtifactTrustStatus.Approved);
        Assert.Contains(
            runDetails.Assignments,
            assignment => assignment.RoleDisplayName == "Business strategist" &&
                assignment.ExecutorKind == "AI agent");
        Assert.Contains(
            runDetails.Assignments,
            assignment => assignment.RoleDisplayName == "Financial strategist" &&
                assignment.ExecutorKind == "AI agent");
        Assert.Contains(
            runDetails.Assignments,
            assignment => assignment.RoleDisplayName == "Marketing specialist" &&
                assignment.ExecutorKind == "AI agent");

        var businessPlanArtifact = Assert.Single(runDetails.Artifacts, artifact => artifact.Title == "Business plan");
        Assert.Equal(ProcessArtifactKind.Deliverable, businessPlanArtifact.ArtifactKind);
        Assert.Equal($"{managedArtifactRoot}/business-plan-draft.md", businessPlanArtifact.ManagedStoragePath);

        var businessPlanContent = await ReadWorkspaceArtifactAsync(application, businessPlanArtifact.ManagedStoragePath);
        Assert.Contains(validationLabel, businessPlanContent, StringComparison.Ordinal);
        Assert.Contains("Evidence summary: The artifact records a concrete handoff", businessPlanContent, StringComparison.Ordinal);
        Assert.Contains("Outcome: Required artifact contract is satisfied", businessPlanContent, StringComparison.Ordinal);
    }

    private static async Task CompleteStepAsync(
        TestApplication application,
        ProcessesService processesService,
        Guid runId,
        string stepKey,
        string validationLabel,
        string managedArtifactRoot,
        string? selectedBranchOutcomeKey = null)
    {
        var runDetails = await processesService.GetRunDetailsAsync(runId);
        var step = Assert.Single(runDetails.StepRuns, item => item.Title == ToStepTitle(stepKey));

        if (step.Status == ProcessStepRunStatus.Ready)
        {
            var startResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
            {
                StepRunId = step.Id,
                TargetStatus = ProcessStepRunStatus.InProgress,
                Reason = $"{validationLabel} started this step.",
                DecidedBy = "integration-test",
                SuppressAutomationDispatch = true
            });
            Assert.True(startResult.IsSuccess, string.Join(" | ", startResult.Errors.Select(error => error.Message)));
            runDetails = await processesService.GetRunDetailsAsync(runId);
            step = Assert.Single(runDetails.StepRuns, item => item.Id == step.Id);
        }

        foreach (var artifactOutput in step.ArtifactOutputs)
        {
            var managedStoragePath = $"{managedArtifactRoot}/{stepKey}.md";
            await WriteWorkspaceArtifactAsync(
                application,
                managedStoragePath,
                BuildManagedArtifactContent(step, stepKey, artifactOutput.Title, validationLabel));

            var artifactResult = await processesService.RecordArtifactAsync(new ProcessArtifactRecordRequest
            {
                ProcessRunId = runId,
                StepRunId = step.Id,
                ArtifactExpectationId = artifactOutput.ArtifactExpectationId,
                ArtifactKind = ResolveArtifactKind(step, artifactOutput.Title),
                Title = artifactOutput.Title,
                TrustStatus = ResolveTrustStatus(step),
                SensitivityLevel = stepKey == "financial-modeling" || stepKey == "integrated-review"
                    ? ProcessSensitivityLevel.Confidential
                    : ProcessSensitivityLevel.Internal,
                ProvenanceSummary = $"Recorded by {validationLabel} for {stepKey}.",
                AllowedFutureUsageSummary = "Reusable by downstream business-plan validation steps.",
                ReviewSummary = "Atomic validation artifact recorded to satisfy the required handoff.",
                ManagedStoragePath = managedStoragePath
            });
            Assert.True(artifactResult.IsSuccess, string.Join(" | ", artifactResult.Errors.Select(error => error.Message)));
        }

        Guid? selectedBranchOutcomeId = null;
        if (!string.IsNullOrWhiteSpace(selectedBranchOutcomeKey))
        {
            var selectedBranchOutcomeTitle = ResolveBranchOutcomeTitle(selectedBranchOutcomeKey);
            selectedBranchOutcomeId = step.AvailableBranchOutcomes
                .Single(outcome => string.Equals(outcome.Title, selectedBranchOutcomeTitle, StringComparison.OrdinalIgnoreCase))
                .Id;
        }

        var completionResult = await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = step.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            SelectedBranchOutcomeId = selectedBranchOutcomeId,
            Reason = $"{validationLabel} completed this step with required artifacts.",
            DecidedBy = "integration-test",
            SuppressAutomationDispatch = true
        });
        Assert.True(completionResult.IsSuccess, string.Join(" | ", completionResult.Errors.Select(error => error.Message)));
    }

    private static string ToStepTitle(string stepKey)
    {
        return stepKey switch
        {
            "strategy-intake" => "Capture strategy intake",
            "product-evidence-review" => "Review product evidence",
            "business-plan-draft" => "Draft business plan",
            "financial-modeling" => "Model financial assumptions",
            "marketing-plan" => "Prepare marketing plan",
            "integrated-review" => "Review integrated business plan",
            "approved-execution-handoff" => "Publish approved plan handoff",
            "blocked-correction-plan" => "Capture blocked-plan corrections",
            _ => stepKey.Replace('-', ' ')
        };
    }

    private static string BuildManagedArtifactContent(
        ProcessStepRunViewModel step,
        string stepKey,
        string artifactTitle,
        string validationLabel)
    {
        return $"""
            # {artifactTitle}

            Step key: {stepKey}
            Step title: {step.Title}
            Validation source: {validationLabel}.
            Evidence summary: The artifact records a concrete handoff for the current process run, including the reviewed business-plan material, accountable step, and downstream reuse boundary.
            Outcome: Required artifact contract is satisfied for persistence and runtime completion validation.
            """;
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

    private static async Task<string> ReadWorkspaceArtifactAsync(
        TestApplication application,
        string relativePath)
    {
        var fullPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        return await File.ReadAllTextAsync(fullPath);
    }

    private static ProcessArtifactKind ResolveArtifactKind(ProcessStepRunViewModel step, string title)
    {
        return title switch
        {
            "Business strategy intake brief" => ProcessArtifactKind.Brief,
            "Product evidence assessment" => ProcessArtifactKind.Evidence,
            "Business plan" => ProcessArtifactKind.Deliverable,
            "Financial model and sensitivity note" => ProcessArtifactKind.Dataset,
            "Go-to-market and experiment plan" => ProcessArtifactKind.Deliverable,
            "Integrated business plan review" => ProcessArtifactKind.Decision,
            _ => step.StepKind == ProcessStepKind.Approval ? ProcessArtifactKind.Decision : ProcessArtifactKind.Evidence
        };
    }

    private static ProcessArtifactTrustStatus ResolveTrustStatus(ProcessStepRunViewModel step)
    {
        return step.StepKind == ProcessStepKind.Approval
            ? ProcessArtifactTrustStatus.Approved
            : ProcessArtifactTrustStatus.ReviewRequired;
    }

    private static string ResolveBranchOutcomeTitle(string outcomeKey)
    {
        return outcomeKey switch
        {
            "approved" => "Approved",
            _ => outcomeKey
        };
    }

    private static void AssertAutomationStep(
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        string title,
        ProcessStepRunStatus expectedStatus,
        string? expectedBranchTitle = null)
    {
        var step = Assert.Single(stepRuns, item => item.Title == title);
        Assert.Equal(expectedStatus, step.Status);
        if (!string.IsNullOrWhiteSpace(expectedBranchTitle))
        {
            Assert.Equal(expectedBranchTitle, step.SelectedBranchOutcomeTitle);
        }
    }

    private static void AssertAutomationArtifact(
        IReadOnlyList<ProcessArtifactRecord> artifactRecords,
        string title,
        ProcessArtifactKind artifactKind)
    {
        Assert.Contains(
            artifactRecords,
            artifact => artifact.Title == title &&
                artifact.ArtifactKind == artifactKind &&
                !string.IsNullOrWhiteSpace(artifact.ManagedStoragePath));
    }

    private static void AssertAutomationFinalizerSummaries(IReadOnlyList<ExecutionRunRecord> executionRuns)
    {
        Assert.All(executionRuns, executionRun =>
        {
            Assert.Contains("\"status\"", executionRun.ResultSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Completed", executionRun.ResultSummary, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string BuildDatabaseConnectionString(string connectionString, string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName,
            IncludeErrorDetail = true,
            Timeout = 5,
            CommandTimeout = 15
        };

        return builder.ConnectionString;
    }

    private static async Task CreateDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildAdminConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"create database \"{databaseName}\";";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(BuildAdminConnectionString(connectionString));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"drop database if exists \"{databaseName}\" with (force);";
        await command.ExecuteNonQueryAsync();
    }

    private static string BuildAdminConnectionString(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            builder.Database = "postgres";
        }

        builder.IncludeErrorDetail = true;
        builder.Timeout = 5;
        builder.CommandTimeout = 15;
        return builder.ConnectionString;
    }
}
