using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessLaunchPlanDisplayProjectionIntegrationTests
{
    [Fact]
    public async Task Launch_plan_reads_project_generated_run_failure_as_effective_status()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
        var projectId = await CreateProjectAsync(projectsService, $"Launch Display Projection {suffix}");
        var managerId = await CreatePartyAsync(partyDirectoryService, $"Launch Projection Manager {suffix}");
        await SaveAssignmentAsync(projectPartyBridge, projectId, managerId, ProjectPartyAssignmentRole.Manager, true);

        var definition = BuildDefinition(projectId);
        var saveResult = await processesService.SaveAsync(definition);
        Assert.True(saveResult.IsSuccess, string.Join(" | ", saveResult.Errors.Select(error => error.Message)));
        var definitionId = saveResult.Value;

        var publishResult = await processesService.PublishAsync(definitionId);
        Assert.True(publishResult.IsSuccess, string.Join(" | ", publishResult.Errors.Select(error => error.Message)));

        var launchResult = await processesService.CreateLaunchPlanAsync(new ProcessLaunchCreateRequest
        {
            ProcessDefinitionId = definitionId,
            ProjectId = projectId,
            LaunchName = $"Projection launch {suffix}",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Integration test launch projection validation.",
            RequestedBy = "integration-tests"
        });
        Assert.True(launchResult.IsSuccess, string.Join(" | ", launchResult.Errors.Select(error => error.Message)));
        var launchPlanId = launchResult.Value;

        var submitResult = await processesService.SubmitLaunchPlanForApprovalAsync(launchPlanId, "integration-tests");
        Assert.True(submitResult.IsSuccess, string.Join(" | ", submitResult.Errors.Select(error => error.Message)));

        var approveResult = await processesService.DecideLaunchPlanApprovalAsync(new ProcessLaunchApprovalDecisionRequest
        {
            LaunchPlanId = launchPlanId,
            Status = ProcessLaunchApprovalStatus.Approved,
            ResolutionSummary = "Manager approved staffing for launch projection validation.",
            DecidedBy = "integration-tests"
        });
        Assert.True(approveResult.IsSuccess, string.Join(" | ", approveResult.Errors.Select(error => error.Message)));

        var executeResult = await processesService.ExecuteLaunchPlanAsync(new ProcessLaunchExecutionRequest
        {
            LaunchPlanId = launchPlanId,
            RequestedBy = "integration-tests"
        });
        Assert.True(executeResult.IsSuccess, string.Join(" | ", executeResult.Errors.Select(error => error.Message)));
        var runId = executeResult.Value;

        var executingDetails = await processesService.GetLaunchPlanAsync(launchPlanId);
        Assert.NotNull(executingDetails);
        Assert.Equal(ProcessLaunchPlanStatus.Executing, executingDetails!.Status);
        Assert.Equal("Run active", executingDetails.StatusBadgeText);
        Assert.Equal("info", executingDetails.StatusTone);
        Assert.Equal("Launch Executing", executingDetails.PlanningStatusBadgeText);

        var stepRun = Assert.Single(await processesService.ListStepRunsAsync(runId));
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = stepRun.Id,
            StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Started projection validation run.",
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        })).IsSuccess);

        var failedStepRun = Assert.Single(await processesService.ListStepRunsAsync(runId));
        Assert.True((await processesService.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = failedStepRun.Id,
            StepRunConcurrencyToken = failedStepRun.StepRunConcurrencyToken,
            TargetStatus = ProcessStepRunStatus.Failed,
            Reason = "Projection validation failed the generated runtime run on purpose.",
            DecidedBy = "integration-tests",
            SuppressAutomationDispatch = true
        })).IsSuccess);

        var plans = await processesService.ListLaunchPlansAsync(definitionId, projectId);
        var failedPlan = Assert.Single(plans, item => item.Id == launchPlanId);
        Assert.Equal(ProcessLaunchPlanStatus.Executing, failedPlan.Status);
        Assert.Equal("Run failed", failedPlan.StatusBadgeText);
        Assert.Equal("danger", failedPlan.StatusTone);
        Assert.Equal("Launch Executing", failedPlan.PlanningStatusBadgeText);
        Assert.Contains("run failed", failedPlan.StatusDetail, StringComparison.OrdinalIgnoreCase);

        var failedDetails = await processesService.GetLaunchPlanAsync(launchPlanId);
        Assert.NotNull(failedDetails);
        Assert.Equal(ProcessLaunchPlanStatus.Executing, failedDetails!.Status);
        Assert.Equal("Run failed", failedDetails.StatusBadgeText);
        Assert.Equal("danger", failedDetails.StatusTone);
        Assert.Equal("Launch Executing", failedDetails.PlanningStatusBadgeText);
        Assert.Contains("run failed", failedDetails.StatusDetail, StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessDefinitionEditorModel BuildDefinition(Guid projectId)
    {
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Launch projection process",
            Summary = "Validates launch-plan status projection from the generated runtime run.",
            ValueStatement = "Launch planning UI must surface generated runtime truth instead of stale planning state.",
            CustomerName = "Integration proof customer",
            OwnerName = "Integration proof owner",
            GovernancePolicySummary = "Launch planning records staffing and approval while runtime records execution truth.",
            ChangeSummary = "Launch plan display projection integration proof.",
            ConstitutionRuleSummary = "Do not let launch planning statuses hide runtime failure.",
            OperatingModeSummary = "Assisted execution.",
            SimulationReadinessSummary = "Safe for deterministic validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "launch-owner",
                    DisplayName = "Launch owner",
                    Purpose = "Own the generated runtime run.",
                    StaffingIntent = "Primary project manager.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    DefaultAllocationPercent = 100
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = stepId,
                    Key = "execute-launch",
                    Title = "Execute launch",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Ready launch plan.",
                    OutputContractSummary = "Generated runtime run outcome.",
                    EvidenceContractSummary = "Runtime outcome is captured.",
                    DecisionRightsSummary = "Launch owner executes the runtime step.",
                    ExceptionPolicySummary = "Surface runtime failure explicitly.",
                    TargetLeadHours = 1,
                    CanvasX = 160,
                    CanvasY = 160,
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

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
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
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests"
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task SaveAssignmentAsync(
        IProjectPartyIntegrationBridge projectPartyBridge,
        Guid projectId,
        Guid partyId,
        ProjectPartyAssignmentRole role,
        bool isPrimary)
    {
        var result = await projectPartyBridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = partyId,
            Role = role,
            AllocationPercent = 100m,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            EndsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
            Notes = $"Assignment for {role}",
            IsPrimary = isPrimary,
            Source = "integration-tests"
        });

        Assert.True(result.IsSuccess, string.Join(" | ", result.Errors.Select(error => error.Message)));
    }
}
