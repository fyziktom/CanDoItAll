using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessMockAgentRuntimeIntegrationTests
{
    [Fact]
    public async Task Process_mock_catalog_is_not_seeded_when_disabled()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessMockAgentCatalogService>();

        var context = await catalogService.EnsureCatalogAsync();

        Assert.Null(context);

        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var providers = await workspaceService.ListProvidersAsync();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);

        Assert.DoesNotContain(providers, ProcessMockAgentCatalog.IsProcessMockProvider);
        Assert.DoesNotContain(
            agents,
            agent => agent.Tags.Contains(ProcessMockAgentCatalog.AgentTag, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Process_mock_catalog_seeds_role_agents_when_enabled()
    {
        await using var application = await CreateEnabledApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessMockAgentCatalogService>();

        var context = await catalogService.EnsureCatalogAsync();

        Assert.NotNull(context);
        Assert.Equal(ProcessMockAgentCatalog.Roles.Count, context.AgentIdsByRoleKey.Count);

        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var provider = Assert.Single(
            await workspaceService.ListProvidersAsync(),
            ProcessMockAgentCatalog.IsProcessMockProvider);

        Assert.True(provider.IsEnabled);
        Assert.Equal(ProcessMockAgentCatalog.Model, provider.DefaultModel);

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        foreach (var role in ProcessMockAgentCatalog.Roles)
        {
            var roleTag = ProcessMockAgentCatalog.CreateRoleTag(role.RoleKey);
            var agent = Assert.Single(
                agents,
                item => item.ProviderProfileId == provider.Id &&
                        item.Tags.Contains(roleTag, StringComparer.OrdinalIgnoreCase));

            Assert.Equal(AgentLifecycleStatus.Active, agent.Status);
            Assert.Contains(agent.Tags, item => string.Equals(item, ProcessMockAgentCatalog.AgentTag, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(ProcessMockAgentCatalog.Model, agent.Model);
        }

        var technicalAgentBridge = scope.ServiceProvider.GetRequiredService<IAiTechnicalAgentBridge>();
        var partyIds = ProcessMockAgentCatalog.Roles.Select(item => item.PartyId).ToList();
        var staffingFacts = await technicalAgentBridge.GetStaffingFactsAsync(partyIds);

        Assert.Equal(ProcessMockAgentCatalog.Roles.Count, staffingFacts.Count);
        foreach (var role in ProcessMockAgentCatalog.Roles)
        {
            var fact = staffingFacts[role.PartyId];
            Assert.Equal(AiResourceBindingStatus.Bound, fact.BindingStatus);
            Assert.True(fact.TechnicalAgentId.HasValue);
            Assert.Equal(ProcessMockAgentCatalog.ProviderName, fact.ProviderName);
            Assert.Equal(ProcessMockAgentCatalog.Model, fact.DefaultModel);
        }
    }

    [Fact]
    public async Task Process_mock_launch_plan_selects_expected_calculator_role_agents_when_enabled()
    {
        await using var application = await CreateEnabledApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessMockAgentCatalogService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var context = await catalogService.EnsureCatalogAsync();
        Assert.NotNull(context);

        var projectId = await CreateProjectAsync(projectsService, "Process mock launch staffing proof");
        var fixture = ProcessCalculatorRepairDefinitionTestFixture.Create(projectId);
        var saveResult = await processesService.SaveAsync(fixture.Editor);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = "Process mock launch staffing proof",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration test process mock staffing validation.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var details = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(details);
        Assert.Equal(ProcessMockAgentCatalog.Roles.Count, details!.Roles.Count);

        var technicalAgentBridge = scope.ServiceProvider.GetRequiredService<IAiTechnicalAgentBridge>();
        var staffingFacts = await technicalAgentBridge.GetStaffingFactsAsync(ProcessMockAgentCatalog.Roles.Select(role => role.PartyId).ToList());

        foreach (var catalogRole in ProcessMockAgentCatalog.Roles)
        {
            var launchRole = Assert.Single(details.Roles, role => string.Equals(role.RoleKey, catalogRole.RoleKey, StringComparison.Ordinal));
            Assert.True(launchRole.SelectedCandidateId.HasValue);

            var selectedCandidate = Assert.Single(launchRole.Candidates, candidate => candidate.Id == launchRole.SelectedCandidateId.Value);
            var roleTag = ProcessMockAgentCatalog.CreateRoleTag(catalogRole.RoleKey);
            var fact = staffingFacts[catalogRole.PartyId];

            Assert.Equal(ProcessLaunchCandidateKind.AiResource, selectedCandidate.CandidateKind);
            Assert.Equal(catalogRole.PartyId, selectedCandidate.PartyId);
            Assert.Equal(catalogRole.AgentName, selectedCandidate.DisplayName);
            Assert.False(selectedCandidate.RequiresProvisioning);
            Assert.True(selectedCandidate.TechnicalAgentId.HasValue);
            Assert.Equal(fact.TechnicalAgentId, selectedCandidate.TechnicalAgentId);
            Assert.Equal(AiResourceBindingStatus.Bound, fact.BindingStatus);
            Assert.Equal(ProcessMockAgentCatalog.ProviderName, fact.ProviderName);
            Assert.Equal(ProcessMockAgentCatalog.Model, fact.DefaultModel);
            Assert.Contains(roleTag, fact.Tags, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(ProcessMockAgentCatalog.ProviderName, selectedCandidate.AvailabilitySummary, StringComparison.Ordinal);
            Assert.Contains(ProcessMockAgentCatalog.Model, selectedCandidate.AvailabilitySummary, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Process_mock_calculator_process_completes_end_to_end_through_durable_outbox_dispatch() {
        await using var application = await CreateEnabledApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessMockAgentCatalogService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var outboxService = scope.ServiceProvider.GetRequiredService<ProcessOutboxService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();

        var context = await catalogService.EnsureCatalogAsync();
        Assert.NotNull(context);

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Process mock calculator E2E {suffix}");
        var managerId = await CreateHumanManagerAsync(
            partyDirectoryService,
            $"Process Mock Launch Manager {suffix}",
            $"process.mock.manager.{suffix}@example.test");
        await SaveAssignmentAsync(projectPartyBridge, projectId, managerId, ProjectPartyAssignmentRole.Manager, "manager", true);

        var fixture = ProcessCalculatorRepairDefinitionTestFixture.Create(projectId);
        var saveResult = await processesService.SaveAsync(fixture.Editor);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));

        var publishResult = await processesService.PublishAsync(saveResult.Value);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            LaunchName = $"Process mock calculator E2E launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration test deterministic process mock calculator repair loop.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));

        var launchDetails = await processesService.GetLaunchPlanAsync(launchResult.Value);
        Assert.NotNull(launchDetails);
        Assert.Equal(ProcessMockAgentCatalog.Roles.Count, launchDetails!.Roles.Count);
        Assert.All(launchDetails.Roles, role => {
            Assert.True(role.SelectedCandidateId.HasValue);
            var selectedCandidate = Assert.Single(role.Candidates, candidate => candidate.Id == role.SelectedCandidateId.Value);
            var catalogRole = Assert.Single(ProcessMockAgentCatalog.Roles, item => string.Equals(item.RoleKey, role.RoleKey, StringComparison.Ordinal));

            Assert.Equal(ProcessLaunchCandidateKind.AiResource, selectedCandidate.CandidateKind);
            Assert.Equal(catalogRole.PartyId, selectedCandidate.PartyId);
            Assert.Equal(catalogRole.AgentName, selectedCandidate.DisplayName);
            Assert.False(selectedCandidate.RequiresProvisioning);
        });

        var submitResult = await processesService.SubmitLaunchPlanForApprovalAsync(launchResult.Value, "integration-tests");
        Assert.True(submitResult.IsSuccess, string.Join(" | ", submitResult.Errors.Select(error => error.Message)));

        var approveResult = await processesService.DecideLaunchPlanApprovalAsync(new ProcessLaunchApprovalDecisionRequest
        {
            LaunchPlanId = launchResult.Value,
            Status = ProcessLaunchApprovalStatus.Approved,
            ResolutionSummary = "Manager approved deterministic process mock calculator E2E execution.",
            DecidedBy = "integration-tests"
        });
        Assert.True(approveResult.IsSuccess, string.Join(" | ", approveResult.Errors.Select(error => error.Message)));

        var executeResult = await processesService.ExecuteLaunchPlanAsync(new ProcessLaunchExecutionRequest
        {
            LaunchPlanId = launchResult.Value,
            RequestedBy = "integration-tests"
        });
        Assert.True(executeResult.IsSuccess, string.Join(" | ", executeResult.Errors.Select(error => error.Message)));
        var runId = executeResult.Value;

        await DrainProcessOutboxUntilRunSettledAsync(
            outboxService,
            dbContextFactory,
            processesService,
            workspaceService,
            runId,
            TimeSpan.FromSeconds(45));

        var run = await processesService.GetRunAsync(runId);
        Assert.NotNull(run);
        Assert.Equal(ProcessRunStatus.Completed, run!.Status);

        var stepRuns = await processesService.ListStepRunsAsync(runId);
        Assert.Equal(
            [
                fixture.StepId(ProcessCalculatorRepairDefinitionTestFixture.StepKeys.Scope),
                fixture.StepId(ProcessCalculatorRepairDefinitionTestFixture.StepKeys.Architecture),
                fixture.StepId(ProcessCalculatorRepairDefinitionTestFixture.StepKeys.FirstImplementation),
                fixture.StepId(ProcessCalculatorRepairDefinitionTestFixture.StepKeys.QaFirstReview),
                fixture.StepId(ProcessCalculatorRepairDefinitionTestFixture.StepKeys.DirectReleaseNotes),
                fixture.StepId(ProcessCalculatorRepairDefinitionTestFixture.StepKeys.RepairImplementation),
                fixture.StepId(ProcessCalculatorRepairDefinitionTestFixture.StepKeys.QaRecheck),
                fixture.StepId(ProcessCalculatorRepairDefinitionTestFixture.StepKeys.ReleaseNotes)
            ],
            stepRuns.OrderBy(stepRun => stepRun.Sequence).Select(stepRun => stepRun.StepDefinitionId).ToArray());

        Assert.Equal(ProcessStepRunStatus.Completed, GetStepRunByKey(stepRuns, fixture, ProcessCalculatorRepairDefinitionTestFixture.StepKeys.Scope).Status);
        Assert.Equal(ProcessStepRunStatus.Completed, GetStepRunByKey(stepRuns, fixture, ProcessCalculatorRepairDefinitionTestFixture.StepKeys.Architecture).Status);
        Assert.Equal(ProcessStepRunStatus.Completed, GetStepRunByKey(stepRuns, fixture, ProcessCalculatorRepairDefinitionTestFixture.StepKeys.FirstImplementation).Status);

        var qaFirstReviewStep = GetStepRunByKey(stepRuns, fixture, ProcessCalculatorRepairDefinitionTestFixture.StepKeys.QaFirstReview);
        var directReleaseNotesStep = GetStepRunByKey(stepRuns, fixture, ProcessCalculatorRepairDefinitionTestFixture.StepKeys.DirectReleaseNotes);
        var repairImplementationStep = GetStepRunByKey(stepRuns, fixture, ProcessCalculatorRepairDefinitionTestFixture.StepKeys.RepairImplementation);
        var qaRecheckStep = GetStepRunByKey(stepRuns, fixture, ProcessCalculatorRepairDefinitionTestFixture.StepKeys.QaRecheck);
        var releaseNotesStep = GetStepRunByKey(stepRuns, fixture, ProcessCalculatorRepairDefinitionTestFixture.StepKeys.ReleaseNotes);

        Assert.Equal(ProcessStepRunStatus.Completed, qaFirstReviewStep.Status);
        Assert.Equal("Repairs required", qaFirstReviewStep.SelectedBranchOutcomeTitle);
        Assert.Equal(
            Assert.Single(qaFirstReviewStep.AvailableBranchOutcomes, item => item.Title == "Repairs required").Id,
            qaFirstReviewStep.SelectedBranchOutcomeId);
        Assert.Equal(ProcessStepRunStatus.Skipped, directReleaseNotesStep.Status);
        Assert.Equal(ProcessStepRunStatus.Completed, repairImplementationStep.Status);
        Assert.Equal(ProcessStepRunStatus.Completed, qaRecheckStep.Status);
        Assert.Equal("Approved", qaRecheckStep.SelectedBranchOutcomeTitle);
        Assert.Equal(
            Assert.Single(qaRecheckStep.AvailableBranchOutcomes, item => item.Title == "Approved").Id,
            qaRecheckStep.SelectedBranchOutcomeId);
        Assert.Equal(ProcessStepRunStatus.Completed, releaseNotesStep.Status);

        var artifactRecords = await ListRunArtifactRecordsAsync(dbContextFactory, runId);
        AssertExpectedArtifactRecord(
            artifactRecords,
            fixture,
            stepRuns,
            ProcessCalculatorRepairDefinitionTestFixture.StepKeys.Scope,
            ProcessCalculatorRepairDefinitionTestFixture.ArtifactTitles.Scope);
        AssertExpectedArtifactRecord(
            artifactRecords,
            fixture,
            stepRuns,
            ProcessCalculatorRepairDefinitionTestFixture.StepKeys.Architecture,
            ProcessCalculatorRepairDefinitionTestFixture.ArtifactTitles.Architecture);
        AssertExpectedArtifactRecord(
            artifactRecords,
            fixture,
            stepRuns,
            ProcessCalculatorRepairDefinitionTestFixture.StepKeys.FirstImplementation,
            ProcessCalculatorRepairDefinitionTestFixture.ArtifactTitles.FirstImplementation);
        AssertExpectedArtifactRecord(
            artifactRecords,
            fixture,
            stepRuns,
            ProcessCalculatorRepairDefinitionTestFixture.StepKeys.QaFirstReview,
            ProcessCalculatorRepairDefinitionTestFixture.ArtifactTitles.QaFirstReview);
        AssertExpectedArtifactRecord(
            artifactRecords,
            fixture,
            stepRuns,
            ProcessCalculatorRepairDefinitionTestFixture.StepKeys.RepairImplementation,
            ProcessCalculatorRepairDefinitionTestFixture.ArtifactTitles.RepairImplementation);
        AssertExpectedArtifactRecord(
            artifactRecords,
            fixture,
            stepRuns,
            ProcessCalculatorRepairDefinitionTestFixture.StepKeys.QaRecheck,
            ProcessCalculatorRepairDefinitionTestFixture.ArtifactTitles.QaRecheck);
        AssertExpectedArtifactRecord(
            artifactRecords,
            fixture,
            stepRuns,
            ProcessCalculatorRepairDefinitionTestFixture.StepKeys.ReleaseNotes,
            ProcessCalculatorRepairDefinitionTestFixture.ArtifactTitles.ReleaseNotes);
        Assert.DoesNotContain(
            artifactRecords,
            artifact => artifact.ArtifactExpectationId == fixture.ArtifactExpectationId(ProcessCalculatorRepairDefinitionTestFixture.StepKeys.DirectReleaseNotes) ||
                        string.Equals(artifact.Title, ProcessCalculatorRepairDefinitionTestFixture.ArtifactTitles.DirectReleaseNotes, StringComparison.Ordinal));

        var decisions = await processesService.ListDecisionRecordsAsync(runId);
        Assert.Contains(decisions, decision => decision.BranchOutcomeTitle == "Repairs required");
        Assert.Contains(decisions, decision => decision.BranchOutcomeTitle == "Approved");

        var executionRuns = (await workspaceService.ListExecutionRunsAsync(new ExecutionRunQuery(
                SourceKind: ProcessMockAgentCatalog.ProcessSourceKind,
                Take: 20,
                ProcessRunId: runId.ToString("D"))))
            .OrderBy(executionRun => executionRun.CreatedAtUtc)
            .ToList();
        Assert.Equal(7, executionRuns.Count);
        Assert.DoesNotContain(executionRuns, executionRun => string.Equals(executionRun.ProcessStepId, directReleaseNotesStep.Id.ToString("D"), StringComparison.OrdinalIgnoreCase));
        Assert.All(executionRuns, executionRun => {
            Assert.Equal(ProcessMockAgentCatalog.ProcessSourceKind, executionRun.SourceKind);
            Assert.Equal(runId.ToString("D"), executionRun.ProcessRunId);
            Assert.True(Guid.TryParse(executionRun.ProcessStepId, out var stepRunId));
            Assert.Contains(stepRuns, stepRun => stepRun.Id == stepRunId);
            Assert.Equal(ProcessMockAgentCatalog.ProviderName, executionRun.ProviderName);
            Assert.Equal(ProcessMockAgentCatalog.Model, executionRun.Model);
            Assert.Equal(ExecutionState.Completed, executionRun.State);
            Assert.Equal(RunOutcome.Succeeded, executionRun.Outcome);
            AssertProcessMockSessionState(executionRun);
        });

        var runOutboxRecords = await ListRunOutboxRecordsAsync(dbContextFactory, runId);
        Assert.NotEmpty(runOutboxRecords);
        Assert.DoesNotContain(runOutboxRecords, outbox => outbox.Status == ProcessOutboxRecordStatus.DeadLettered);
        Assert.All(runOutboxRecords, outbox => Assert.Equal(ProcessOutboxRecordStatus.Completed, outbox.Status));
    }

    [Fact]
    public async Task Process_mock_runtime_runs_deterministic_calculator_rejection_repair_and_approval()
    {
        await using var application = await CreateEnabledApplicationAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<ProcessMockAgentCatalogService>();
        await catalogService.EnsureCatalogAsync();

        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        var qaAgent = FindRoleAgent(agents, ProcessMockAgentRoleKeys.Qa);
        var repairAgent = FindRoleAgent(agents, ProcessMockAgentRoleKeys.RepairDeveloper);

        const string processRunId = "mock-run-001";

        var qaRejection = await workspaceService.ExecuteRunAsync(new ExecutionRunRequest(
            AgentId: qaAgent.Id,
            Prompt: "Run process mock QA first pass for the calculator implementation.",
            Context: CreateProcessContext("qa-first-pass", processRunId, "qa-first-pass")));
        Assert.Contains(ProcessMockAgentCatalog.BranchRepairsRequired, qaRejection.ResponseText, StringComparison.Ordinal);

        var rejectionDetail = await workspaceService.GetExecutionRunDetailAsync(qaRejection.ExecutionRunId);
        Assert.Contains(
            rejectionDetail.Artifacts,
            artifact => artifact.RelativePath.EndsWith("/04-qa-finding.md", StringComparison.OrdinalIgnoreCase));

        var repair = await workspaceService.ExecuteRunAsync(new ExecutionRunRequest(
            AgentId: repairAgent.Id,
            Prompt: "Run process mock repair developer step for the calculator implementation.",
            Context: CreateProcessContext("repair", processRunId, "repair")));
        Assert.Contains("PROCESS_STEP_OUTCOME", repair.ResponseText, StringComparison.Ordinal);

        var fileService = new WorkspaceFileService(workspaceFactory.GetWorkspaceRoot(), workspaceFactory.GetOrganizationScope());
        var repairedEngine = fileService.ReadTextFile("output/process-mock/mockrun001/CalculatorApp/CalculatorEngine.cs", 8000);
        Assert.True(repairedEngine.Succeeded, repairedEngine.Message);
        Assert.Contains("throw new DivideByZeroException", repairedEngine.Content, StringComparison.Ordinal);

        var qaApproval = await workspaceService.ExecuteRunAsync(new ExecutionRunRequest(
            AgentId: qaAgent.Id,
            Prompt: "Run process mock QA approval for the repaired calculator implementation.",
            Context: CreateProcessContext("qa-approval", processRunId, "qa-approval")));
        Assert.Contains(ProcessMockAgentCatalog.BranchApproved, qaApproval.ResponseText, StringComparison.Ordinal);

        var approvalDetail = await workspaceService.GetExecutionRunDetailAsync(qaApproval.ExecutionRunId);
        Assert.Contains(
            approvalDetail.Artifacts,
            artifact => artifact.RelativePath.EndsWith("/06-qa-approval.md", StringComparison.OrdinalIgnoreCase));
    }

    private static Task<TestApplication> CreateEnabledApplicationAsync()
    {
        return TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                [$"{ProcessMockAgentOptions.SectionName}:Enabled"] = "true"
            }
        });
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Validation"
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task<Guid> CreateHumanManagerAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        string email) {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Employee",
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task SaveAssignmentAsync(
        IProjectPartyIntegrationBridge projectPartyBridge,
        Guid projectId,
        Guid partyId,
        ProjectPartyAssignmentRole role,
        string nodeKey,
        bool isPrimary) {
        var result = await projectPartyBridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = partyId,
            Role = role,
            NodeKey = nodeKey,
            AllocationPercent = 100m,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            EndsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
            Notes = $"Assignment for {role}",
            IsPrimary = isPrimary,
            Source = "integration-tests"
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
    }

    private static AgentDefinition FindRoleAgent(
        IReadOnlyList<AgentDefinition> agents,
        string roleKey)
    {
        var roleTag = ProcessMockAgentCatalog.CreateRoleTag(roleKey);
        return Assert.Single(
            agents,
            item => item.Tags.Contains(roleTag, StringComparer.OrdinalIgnoreCase));
    }

    private static async Task DrainProcessOutboxUntilRunSettledAsync(
        ProcessOutboxService outboxService,
        IDbContextFactory<AppDbContext> dbContextFactory,
        ProcessesService processesService,
        IAgentFrameworkWorkspaceService workspaceService,
        Guid runId,
        TimeSpan timeout) {
        var deadline = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < deadline) {
            await outboxService.ProcessPendingAsync(20, TimeSpan.FromMinutes(1));

            var outboxRecords = await ListRunOutboxRecordsAsync(dbContextFactory, runId);
            var deadLetteredRecord = outboxRecords.FirstOrDefault(item => item.Status == ProcessOutboxRecordStatus.DeadLettered);
            if (deadLetteredRecord is not null) {
                Assert.Fail(
                    await BuildRunDiagnosticsAsync(
                        dbContextFactory,
                        processesService,
                        workspaceService,
                        runId,
                        $"Run outbox record {deadLetteredRecord.Id:D} dead-lettered. LastError={deadLetteredRecord.LastError}"));
            }

            var run = await processesService.GetRunAsync(runId);
            if (run?.Status == ProcessRunStatus.Completed) {
                return;
            }

            if (run?.Status == ProcessRunStatus.Failed) {
                Assert.Fail(
                    await BuildRunDiagnosticsAsync(
                        dbContextFactory,
                        processesService,
                        workspaceService,
                        runId,
                        $"Process mock run {runId:D} failed before completion."));
            }

            await Task.Delay(50);
        }

        Assert.Fail(
            await BuildRunDiagnosticsAsync(
                dbContextFactory,
                processesService,
                workspaceService,
                runId,
                $"Timed out waiting for process mock run {runId:D} to complete."));
    }

    private static ProcessStepRunViewModel GetStepRunByKey(
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        CalculatorRepairProcessDefinitionFixture fixture,
        string stepKey) {
        var stepDefinitionId = fixture.StepId(stepKey);
        return Assert.Single(stepRuns, stepRun => stepRun.StepDefinitionId == stepDefinitionId);
    }

    private static void AssertExpectedArtifactRecord(
        IReadOnlyList<ProcessArtifactRecord> artifactRecords,
        CalculatorRepairProcessDefinitionFixture fixture,
        IReadOnlyList<ProcessStepRunViewModel> stepRuns,
        string stepKey,
        string title) {
        var stepRun = GetStepRunByKey(stepRuns, fixture, stepKey);
        var artifactOutput = Assert.Single(
            stepRun.ArtifactOutputs,
            item => item.IsRequired &&
                    string.Equals(item.Title, title, StringComparison.Ordinal));
        Assert.Contains(
            artifactRecords,
            artifact => artifact.StepRunId == stepRun.Id &&
                        artifact.ArtifactExpectationId == artifactOutput.ArtifactExpectationId &&
                        string.Equals(artifact.Title, title, StringComparison.Ordinal));
    }

    private static void AssertProcessMockSessionState(ExecutionRunRecord executionRun) {
        using var sessionState = JsonDocument.Parse(executionRun.SerializedSessionStateJson ?? "{}");

        Assert.True(sessionState.RootElement.TryGetProperty("processMockAgent", out var processMockAgent));
        Assert.Equal(JsonValueKind.True, processMockAgent.ValueKind);
        Assert.True(sessionState.RootElement.TryGetProperty("roleKey", out var roleKeyProperty));
        var roleKey = roleKeyProperty.GetString();

        Assert.Contains(
            ProcessMockAgentCatalog.Roles,
            role => string.Equals(role.RoleKey, roleKey, StringComparison.Ordinal));
    }

    private static async Task<IReadOnlyList<ProcessOutboxRecord>> ListRunOutboxRecordsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid runId) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var records = await dbContext.Set<ProcessOutboxRecord>()
            .Where(item => item.ProcessRunId == runId)
            .ToListAsync();
        return records
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessArtifactRecord>> ListRunArtifactRecordsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid runId) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var records = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runId)
            .ToListAsync();
        return records
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToList();
    }

    private static async Task<string> BuildRunDiagnosticsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        ProcessesService processesService,
        IAgentFrameworkWorkspaceService workspaceService,
        Guid runId,
        string reason) {
        var builder = new StringBuilder()
            .AppendLine(reason);

        var run = await processesService.GetRunAsync(runId);
        builder.Append("Run status: ")
            .AppendLine(run?.Status.ToString() ?? "missing");

        var stepRuns = await processesService.ListStepRunsAsync(runId);
        builder.AppendLine("Steps:");
        foreach (var stepRun in stepRuns.OrderBy(stepRun => stepRun.Sequence)) {
            builder.Append("  ")
                .Append(stepRun.Sequence)
                .Append(": ")
                .Append(stepRun.Title)
                .Append(" / ")
                .Append(stepRun.Status)
                .Append(" / branch=")
                .Append(stepRun.SelectedBranchOutcomeTitle);
            if (!string.IsNullOrWhiteSpace(stepRun.DecisionSummary)) {
                builder.Append(" / decision=")
                    .Append(stepRun.DecisionSummary);
            }

            if (!string.IsNullOrWhiteSpace(stepRun.BlockedReason)) {
                builder.Append(" / blocked=")
                    .Append(stepRun.BlockedReason);
            }

            if (!string.IsNullOrWhiteSpace(stepRun.RefusalReason)) {
                builder.Append(" / refusal=")
                    .Append(stepRun.RefusalReason);
            }

            builder.AppendLine();
        }

        var outboxRecords = await ListRunOutboxRecordsAsync(dbContextFactory, runId);
        builder.AppendLine("Outbox:");
        foreach (var outbox in outboxRecords) {
            builder.Append("  ")
                .Append(outbox.CommandKey)
                .Append(" / ")
                .Append(outbox.Status)
                .Append(" / attempts=")
                .Append(outbox.AttemptCount)
                .Append(" / lastError=")
                .AppendLine(outbox.LastError);
        }

        var executionRuns = await workspaceService.ListExecutionRunsAsync(new ExecutionRunQuery(
            Take: 20,
            ProcessRunId: runId.ToString("D")));
        builder.AppendLine("Execution runs:");
        foreach (var executionRun in executionRuns.OrderBy(executionRun => executionRun.CreatedAtUtc)) {
            builder.Append("  ")
                .Append(executionRun.Title)
                .Append(" / ")
                .Append(executionRun.ProviderName)
                .Append(" / ")
                .Append(executionRun.Model)
                .Append(" / ")
                .Append(executionRun.State)
                .Append(" / ")
                .Append(executionRun.Outcome)
                .Append(" / step=")
                .AppendLine(executionRun.ProcessStepId);
        }

        return builder.ToString();
    }

    private static ExecutionInvocationContext CreateProcessContext(
        string sourceId,
        string processRunId,
        string processStepId)
    {
        return new ExecutionInvocationContext(
            SourceKind: ProcessMockAgentCatalog.ProcessSourceKind,
            SourceId: sourceId,
            CorrelationId: $"{processRunId}-{processStepId}",
            CausationId: processRunId,
            RequestedBy: "process-mock-tests",
            RequestedByKind: "test",
            MetadataJson: "{}",
            ProcessRunId: processRunId,
            ProcessStepId: processStepId);
    }
}
