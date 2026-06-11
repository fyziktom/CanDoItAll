using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class BusinessPlanProcessPostgresIntegrationTests
{
    private const string RepositoryRoot = @"C:\repositories\CanDoItAll";
    private const string AutomationDispatchCommandKey = "dispatch-run-automation";

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
    public async Task Business_plan_process_SB05_INV_001_completes_on_postgresql_through_automation_dispatch_finalizer_and_readback()
    {
        const string validationLabel = "Business-analysis automation validation";

        var availability = await PostgresTestAvailability.EnsureAvailableAsync(RepositoryRoot);
        Assert.True(availability.IsAvailable, availability.Message);
        Assert.False(string.IsNullOrWhiteSpace(availability.ConnectionString));

        await using var testEnvironment = CanDoItAllTestEnvironment.Create("integration-business-plan-sb05-postgres");
        var profile = testEnvironment.CreatePostgreSqlProfile("business-plan-sb05");
        await using var application = await ProcessTemplateAutomationTestSupport.CreateProcessMockEnabledApplicationAsync(
            new TestHarnessOptions
            {
                TestEnvironment = testEnvironment,
                ActiveProfile = profile
            });
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await ProcessTemplateAutomationTestSupport.CreateProjectAsync(
            projectsService,
            "Business-analysis automation validation project",
            "Planning");
        var completedAutomationStepTitles = new[]
        {
            "Capture strategy intake",
            "Review product evidence",
            "Draft business plan",
            "Model financial assumptions",
            "Prepare marketing plan",
            "Review integrated business plan",
            "Publish approved plan handoff"
        };

        var result = await ProcessTemplateAutomationTestSupport.ExecuteTemplateWithProcessMockAgentsAsync(
            scope.ServiceProvider,
            "business-plan-development",
            projectId,
            validationLabel,
            $"{validationLabel} launch",
            "Validate the business-plan representative template through automation dispatch, finalizer completion, and business artifact readback.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["business-strategist"] = ProcessMockAgentRoleKeys.BusinessStrategist,
                ["financial-strategist"] = ProcessMockAgentRoleKeys.FinancialStrategist,
                ["marketing-specialist"] = ProcessMockAgentRoleKeys.MarketingSpecialist
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

        AssertBusinessAutomationDispatchReadback(result, completedAutomationStepTitles);
        AssertNoSoftwareDomainLeakage(result);
        AssertAutomationArtifact(result.ArtifactRecords, "Business strategy intake brief", ProcessArtifactKind.Brief, application.ActiveProfile.WorkspaceRootPath);
        AssertAutomationArtifact(result.ArtifactRecords, "Product evidence assessment", ProcessArtifactKind.Evidence, application.ActiveProfile.WorkspaceRootPath);
        AssertAutomationArtifact(result.ArtifactRecords, "Business plan", ProcessArtifactKind.Deliverable, application.ActiveProfile.WorkspaceRootPath);
        AssertAutomationArtifact(result.ArtifactRecords, "Financial model and sensitivity note", ProcessArtifactKind.Dataset, application.ActiveProfile.WorkspaceRootPath);
        AssertAutomationArtifact(result.ArtifactRecords, "Go-to-market and experiment plan", ProcessArtifactKind.Deliverable, application.ActiveProfile.WorkspaceRootPath);
        AssertAutomationArtifact(result.ArtifactRecords, "Integrated business plan review", ProcessArtifactKind.Decision, application.ActiveProfile.WorkspaceRootPath);
        await AssertPersistedBusinessAutomationReadbackAsync(dbContextFactory, result, projectId);
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

    private static void AssertBusinessAutomationDispatchReadback(
        ProcessTemplateAutomationRunResult result,
        IReadOnlyCollection<string> completedStepTitles)
    {
        Assert.NotEmpty(result.OutboxRecords);
        Assert.All(result.OutboxRecords, outbox =>
        {
            Assert.Equal(ProcessOutboxRecordStatus.Completed, outbox.Status);
        });
        Assert.Contains(
            result.OutboxRecords,
            outbox => string.Equals(outbox.CommandKey, AutomationDispatchCommandKey, StringComparison.Ordinal));

        var completedStepIds = result.StepRuns
            .Where(step => completedStepTitles.Contains(step.Title, StringComparer.Ordinal))
            .Select(step => step.Id.ToString("D"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(completedStepTitles.Count, completedStepIds.Count);
        Assert.Equal(completedStepIds.Count, result.ExecutionRuns.Count);

        Assert.All(result.ExecutionRuns, executionRun =>
        {
            Assert.Equal(ProcessMockAgentCatalog.ProcessSourceKind, executionRun.SourceKind);
            Assert.Equal(result.RunId.ToString("D"), executionRun.ProcessRunId);
            Assert.Contains(executionRun.ProcessStepId, completedStepIds);
            Assert.Equal(ProcessMockAgentCatalog.ProviderName, executionRun.ProviderName);
            Assert.Equal(ProcessMockAgentCatalog.Model, executionRun.Model);
            Assert.Equal(ExecutionState.Completed, executionRun.State);
            Assert.Equal(RunOutcome.Succeeded, executionRun.Outcome);
        });

        Assert.Contains(
            result.ExecutionRuns,
            executionRun => executionRun.SerializedSessionStateJson?.Contains(
                ProcessMockAgentRoleKeys.BusinessStrategist,
                StringComparison.Ordinal) == true);
        Assert.Contains(
            result.ExecutionRuns,
            executionRun => executionRun.SerializedSessionStateJson?.Contains(
                ProcessMockAgentRoleKeys.FinancialStrategist,
                StringComparison.Ordinal) == true);
        Assert.Contains(
            result.ExecutionRuns,
            executionRun => executionRun.SerializedSessionStateJson?.Contains(
                ProcessMockAgentRoleKeys.MarketingSpecialist,
                StringComparison.Ordinal) == true);
    }

    private static async Task AssertPersistedBusinessAutomationReadbackAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ProcessTemplateAutomationRunResult result,
        Guid projectId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var persistedRun = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .SingleAsync(run => run.Id == result.RunId);
        Assert.Equal(projectId, persistedRun.ProjectId);
        Assert.Equal(ProcessRunStatus.Completed, persistedRun.Status);

        var persistedOutboxRecords = await dbContext.Set<ProcessOutboxRecord>()
            .AsNoTracking()
            .Where(record => record.ProcessRunId == result.RunId)
            .OrderBy(record => record.CreatedAtUtc)
            .ToListAsync();
        Assert.NotEmpty(persistedOutboxRecords);
        Assert.All(persistedOutboxRecords, record =>
        {
            Assert.Equal(ProcessOutboxRecordStatus.Completed, record.Status);
        });
        Assert.Contains(
            persistedOutboxRecords,
            record => string.Equals(record.CommandKey, AutomationDispatchCommandKey, StringComparison.Ordinal));

        var persistedArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(artifact => artifact.ProcessRunId == result.RunId)
            .OrderBy(artifact => artifact.CreatedAtUtc)
            .ToListAsync();
        Assert.Equal(result.ArtifactRecords.Count, persistedArtifacts.Count);
        AssertBusinessArtifactTitles(persistedArtifacts);
        AssertNoSoftwareDomainLeakage(persistedArtifacts);
    }

    private static void AssertBusinessArtifactTitles(IReadOnlyCollection<ProcessArtifactRecord> artifactRecords)
    {
        Assert.Contains(artifactRecords, artifact => artifact.Title == "Business strategy intake brief");
        Assert.Contains(artifactRecords, artifact => artifact.Title == "Product evidence assessment");
        Assert.Contains(artifactRecords, artifact => artifact.Title == "Business plan");
        Assert.Contains(artifactRecords, artifact => artifact.Title == "Financial model and sensitivity note");
        Assert.Contains(artifactRecords, artifact => artifact.Title == "Go-to-market and experiment plan");
        Assert.Contains(artifactRecords, artifact => artifact.Title == "Integrated business plan review");
    }

    private static void AssertNoSoftwareDomainLeakage(ProcessTemplateAutomationRunResult result)
    {
        Assert.All(result.StepRuns, step =>
        {
            AssertNoSoftwareDomainLeakage(step.Title, $"step '{step.Title}' title");
            AssertNoSoftwareDomainLeakage(step.CurrentExecutorName, $"step '{step.Title}' executor");
            AssertNoSoftwareDomainLeakage(step.DecisionSummary, $"step '{step.Title}' decision summary");
        });
        AssertNoSoftwareDomainLeakage(result.ArtifactRecords);
        Assert.All(result.ExecutionRuns, executionRun =>
        {
            AssertNoSoftwareDomainLeakage(executionRun.Title, $"execution run '{executionRun.Id:D}' title");
            AssertNoSoftwareDomainLeakage(executionRun.ResultSummary, $"execution run '{executionRun.Id:D}' result summary");
            AssertNoSoftwareDomainLeakage(
                executionRun.SerializedSessionStateJson ?? string.Empty,
                $"execution run '{executionRun.Id:D}' session state");
        });
    }

    private static void AssertNoSoftwareDomainLeakage(IReadOnlyCollection<ProcessArtifactRecord> artifactRecords)
    {
        Assert.All(artifactRecords, artifact =>
        {
            AssertNoSoftwareDomainLeakage(artifact.Title, $"artifact '{artifact.Title}' title");
            AssertNoSoftwareDomainLeakage(artifact.ProvenanceSummary, $"artifact '{artifact.Title}' provenance");
            AssertNoSoftwareDomainLeakage(artifact.ReviewSummary, $"artifact '{artifact.Title}' review");
            AssertNoSoftwareDomainLeakage(artifact.ManagedStoragePath, $"artifact '{artifact.Title}' path");
        });
    }

    private static void AssertNoSoftwareDomainLeakage(string? value, string context)
    {
        var text = value ?? string.Empty;
        foreach (var term in new[] { "software", ".net", "blazor", "developer", "implementation", "release manager", "qa" })
        {
            Assert.False(
                text.Contains(term, StringComparison.OrdinalIgnoreCase),
                $"Unexpected software-domain term '{term}' in {context}: {text}");
        }
    }

    private static void AssertAutomationArtifact(
        IReadOnlyList<ProcessArtifactRecord> artifactRecords,
        string title,
        ProcessArtifactKind artifactKind,
        string? workspaceRootPath = null)
    {
        var matchingArtifacts = artifactRecords
            .Where(artifact => artifact.Title == title &&
                artifact.ArtifactKind == artifactKind &&
                !string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
            .ToList();
        Assert.NotEmpty(matchingArtifacts);
        if (string.IsNullOrWhiteSpace(workspaceRootPath))
        {
            return;
        }

        Assert.Contains(matchingArtifacts, artifact =>
        {
            var fullPath = Path.Combine(
                workspaceRootPath,
                artifact.ManagedStoragePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fullPath) && new FileInfo(fullPath).Length > 0;
        });
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
