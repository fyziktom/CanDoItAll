using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessSubprocessIntegrationTests
{
    [Fact]
    public async Task Subprocess_step_creates_one_observable_child_run_and_mirrors_completion()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dispatchService = scope.ServiceProvider.GetRequiredService<IProcessRunAutomationDispatchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Subprocess runtime validation");
        var managerPartyId = await CreateAiManagerAsync(partyDirectoryService, "Integration subprocess manager");
        var childDefinitionId = await SaveAndPublishAsync(
            processesService,
            BuildChildDefinition(projectId));
        var parentDefinitionId = await SaveAndPublishAsync(
            processesService,
            BuildParentDefinition(projectId, childDefinitionId, managerPartyId));

        var runResult = await processesService.StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = parentDefinitionId,
                ProjectId = projectId,
                RunName = "Subprocess runtime validation run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Integration validation of subprocess orchestration."
            });

        Assert.True(runResult.IsSuccess, ToErrorMessage(runResult.Errors));
        var parentRunId = runResult.Value;
        var parentRun = await processesService.GetRunAsync(parentRunId);

        Assert.NotNull(parentRun);
        Assert.Equal(parentRunId, parentRun.RootRunId);
        Assert.Equal(0, parentRun.HierarchyDepth);
        Assert.Equal(managerPartyId, parentRun.ManagerAgentId);
        Assert.Equal("Integration subprocess manager", parentRun.ManagerAgentName);

        var parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        var intakeStep = Assert.Single(parentSteps, step => step.Title == "Capture parent intake");
        await CompleteStepAsync(processesService, parentRunId, intakeStep.Id);

        parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        var subprocessStep = Assert.Single(parentSteps, step => step.Title == "Run child validation subprocess");
        Assert.Equal(ProcessStepRunStatus.Ready, subprocessStep.Status);

        await dispatchService.DispatchAsync(parentRunId, subprocessStep.Id, "integration-subprocess-start");

        var firstSubprocessResult = await processesService.EnsureSubprocessRunForStepAsync(subprocessStep.Id);
        var secondSubprocessResult = await processesService.EnsureSubprocessRunForStepAsync(subprocessStep.Id);

        Assert.True(firstSubprocessResult.IsSuccess, ToErrorMessage(firstSubprocessResult.Errors));
        Assert.True(secondSubprocessResult.IsSuccess, ToErrorMessage(secondSubprocessResult.Errors));
        Assert.Equal(firstSubprocessResult.Value!.RunId, secondSubprocessResult.Value!.RunId);

        var childRunId = firstSubprocessResult.Value!.RunId;
        var childRun = await processesService.GetRunAsync(childRunId);

        Assert.NotNull(childRun);
        Assert.Equal(parentRunId, childRun.ParentRunId);
        Assert.Equal(subprocessStep.Id, childRun.ParentStepRunId);
        Assert.Equal(parentRunId, childRun.RootRunId);
        Assert.Equal(1, childRun.HierarchyDepth);
        Assert.Equal(managerPartyId, childRun.ManagerAgentId);
        Assert.Equal("Integration subprocess manager", childRun.ManagerAgentName);

        parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        subprocessStep = Assert.Single(parentSteps, step => step.Id == subprocessStep.Id);

        Assert.Equal(ProcessStepRunStatus.InProgress, subprocessStep.Status);
        Assert.NotNull(subprocessStep.SubprocessRun);
        Assert.Equal(childRunId, subprocessStep.SubprocessRun.RunId);
        Assert.Equal(0, subprocessStep.SubprocessRun.CompletedStepCount);
        Assert.Equal(2, subprocessStep.SubprocessRun.TotalStepCount);

        var childSteps = await processesService.ListStepRunsAsync(childRunId);
        await CompleteStepAsync(processesService, childRunId, Assert.Single(childSteps, step => step.Title == "Capture child intake").Id);

        parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        subprocessStep = Assert.Single(parentSteps, step => step.Title == "Run child validation subprocess");

        Assert.NotNull(subprocessStep.SubprocessRun);
        Assert.Equal(1, subprocessStep.SubprocessRun.CompletedStepCount);
        Assert.Equal(ProcessRunStatus.Active, subprocessStep.SubprocessRun.Status);

        childSteps = await processesService.ListStepRunsAsync(childRunId);
        await CompleteStepAsync(processesService, childRunId, Assert.Single(childSteps, step => step.Title == "Validate child result").Id);

        childRun = await processesService.GetRunAsync(childRunId);
        Assert.NotNull(childRun);
        Assert.Equal(ProcessRunStatus.Completed, childRun.Status);

        await dispatchService.DispatchAsync(parentRunId, subprocessStep.Id, "integration-subprocess-complete");

        parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        subprocessStep = Assert.Single(parentSteps, step => step.Title == "Run child validation subprocess");

        Assert.Equal(ProcessStepRunStatus.Completed, subprocessStep.Status);
        Assert.NotNull(subprocessStep.SubprocessRun);
        Assert.Equal(ProcessRunStatus.Completed, subprocessStep.SubprocessRun.Status);
        Assert.Equal(2, subprocessStep.SubprocessRun.CompletedStepCount);

        var directiveResult = await processesService.RecordManagerDirectiveAsync(
            new ProcessManagerDirectiveRequest
            {
                ProcessRunId = parentRunId,
                Directive = "Summarize subprocess blockers and request the narrowest unblock action.",
                InstructedBy = "integration-tests"
            });

        Assert.True(directiveResult.IsSuccess, ToErrorMessage(directiveResult.Errors));
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var directive = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .SingleAsync(entry =>
                entry.ProcessRunId == parentRunId &&
                entry.EventType == ProcessRuntimeEventTypes.ManagerDirectiveRecorded);

        Assert.Contains("Integration subprocess manager", directive.ReplayContextJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("subprocess blockers", directive.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Default_templates_import_nested_subprocess_references_in_order()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Default subprocess template validation");
        var setupDefinitionId = await ImportAndPublishTemplateAsync(
            processesService,
            projectionService,
            "dotnet-blazor-ssr-solution-setup",
            projectId);
        var sliceDefinitionId = await ImportAndPublishTemplateAsync(
            processesService,
            projectionService,
            "dotnet-development-slice",
            projectId);
        var softwareDeliveryDefinitionId = await ImportAndPublishTemplateAsync(
            processesService,
            projectionService,
            "software-delivery",
            projectId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var sliceSubprocessStep = await LoadPublishedStepAsync(dbContext, sliceDefinitionId, "prepare-solution-skeleton");
        var softwareDeliverySubprocessStep = await LoadPublishedStepAsync(dbContext, softwareDeliveryDefinitionId, "implementation");

        Assert.Equal(ProcessStepKind.Subprocess, sliceSubprocessStep.StepKind);
        Assert.Equal(setupDefinitionId, sliceSubprocessStep.SubprocessDefinitionId);
        Assert.Equal(".NET Blazor SSR solution setup subprocess", sliceSubprocessStep.SubprocessDefinitionSnapshotName);
        Assert.Equal(ProcessStepKind.Subprocess, softwareDeliverySubprocessStep.StepKind);
        Assert.Equal(sliceDefinitionId, softwareDeliverySubprocessStep.SubprocessDefinitionId);
        Assert.Equal(".NET implementation slice with atomic validation", softwareDeliverySubprocessStep.SubprocessDefinitionSnapshotName);
    }

    private static async Task<Guid> ImportAndPublishTemplateAsync(
        ProcessesService processesService,
        ProcessTemplateProjectionService projectionService,
        string processKey,
        Guid projectId)
    {
        var envelope = projectionService.GetProjectedEnvelope(processKey, projectId);
        var importResult = await processesService.ImportAsync(envelope);

        Assert.True(importResult.IsSuccess, ToErrorMessage(importResult.Errors));
        Assert.True((await processesService.PublishAsync(importResult.Value)).IsSuccess);
        return importResult.Value;
    }

    private static async Task<ProcessStepDefinition> LoadPublishedStepAsync(
        AppDbContext dbContext,
        Guid definitionId,
        string stepKey)
    {
        var definition = await dbContext.Set<ProcessDefinition>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == definitionId);

        Assert.NotNull(definition.ActivePublishedVersionId);
        return await dbContext.Set<ProcessStepDefinition>()
            .AsNoTracking()
            .SingleAsync(item =>
                item.ProcessDefinitionVersionId == definition.ActivePublishedVersionId!.Value &&
                item.Key == stepKey);
    }

    private static async Task<Guid> SaveAndPublishAsync(
        ProcessesService processesService,
        ProcessDefinitionEditorModel definition)
    {
        var saveResult = await processesService.SaveAsync(definition);

        Assert.True(saveResult.IsSuccess, ToErrorMessage(saveResult.Errors));
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);
        return saveResult.Value;
    }

    private static async Task CompleteStepAsync(
        ProcessesService processesService,
        Guid runId,
        Guid stepRunId)
    {
        var stepRun = Assert.Single(await processesService.ListStepRunsAsync(runId), step => step.Id == stepRunId);
        var startResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.InProgress,
                Reason = "Started by subprocess integration validation.",
                DecidedBy = "integration-tests",
                SuppressAutomationDispatch = true
            });

        Assert.True(startResult.IsSuccess, ToErrorMessage(startResult.Errors));
        stepRun = Assert.Single(await processesService.ListStepRunsAsync(runId), step => step.Id == stepRunId);

        var completeResult = await processesService.TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRun.Id,
                StepRunConcurrencyToken = stepRun.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.Completed,
                Reason = "Completed by subprocess integration validation.",
                DecidedBy = "integration-tests",
                SuppressAutomationDispatch = true
            });

        Assert.True(completeResult.IsSuccess, ToErrorMessage(completeResult.Errors));
    }

    private static ProcessDefinitionEditorModel BuildChildDefinition(Guid projectId)
    {
        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var validationStepId = Guid.NewGuid();

        return BuildBaseDefinition(
            projectId,
            "Child validation subprocess",
            "Runs a small two-step child workflow that can be observed from a parent process.",
            roleId,
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "child-intake",
                    Title = "Capture child intake",
                    StepKind = ProcessStepKind.Start,
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        CreateRoleAssignment(roleId)
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = validationStepId,
                    Key = "child-validation",
                    Title = "Validate child result",
                    StepKind = ProcessStepKind.Work,
                    TargetLeadHours = 1,
                    Dependencies =
                    [
                        CreateDependency(intakeStepId)
                    ],
                    RoleAssignments =
                    [
                        CreateRoleAssignment(roleId)
                    ]
                }
            ]);
    }

    private static ProcessDefinitionEditorModel BuildParentDefinition(
        Guid projectId,
        Guid childDefinitionId,
        Guid managerPartyId)
    {
        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var subprocessStepId = Guid.NewGuid();

        var definition = BuildBaseDefinition(
            projectId,
            "Parent process with subprocess step",
            "Runs a child workflow as an observable process step.",
            roleId,
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "parent-intake",
                    Title = "Capture parent intake",
                    StepKind = ProcessStepKind.Start,
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        CreateRoleAssignment(roleId)
                    ]
                },
                new ProcessStepEditorModel
                {
                    Id = subprocessStepId,
                    Key = "child-subprocess",
                    Title = "Run child validation subprocess",
                    StepKind = ProcessStepKind.Subprocess,
                    SubprocessDefinitionId = childDefinitionId,
                    SubprocessDefinitionSnapshotName = "Child validation subprocess",
                    TargetLeadHours = 2,
                    Dependencies =
                    [
                        CreateDependency(intakeStepId)
                    ],
                    RoleAssignments =
                    [
                        CreateRoleAssignment(roleId)
                    ]
                }
            ]);

        definition.ManagerAgentOverrideId = managerPartyId;
        definition.ManagerAgentOverrideName = "Integration subprocess manager";
        return definition;
    }

    private static ProcessDefinitionEditorModel BuildBaseDefinition(
        Guid projectId,
        string name,
        string summary,
        Guid roleId,
        List<ProcessStepEditorModel> steps)
    {
        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = name,
            Summary = summary,
            ValueStatement = "Keep subprocess execution visible without duplicating runtime state.",
            CustomerName = "Integration customer",
            OwnerName = "Integration owner",
            GovernancePolicySummary = "Parent and child runs keep separate durable records with explicit hierarchy links.",
            ChangeSummary = "Subprocess integration validation.",
            ConstitutionRuleSummary = "Do not hide subprocess state behind parent-only status.",
            OperatingModeSummary = "Assisted execution with manager oversight.",
            SimulationReadinessSummary = "Safe deterministic test definition.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "process-owner",
                    DisplayName = "Process owner",
                    Purpose = "Own the subprocess validation workflow.",
                    StaffingIntent = "Integration-owned process responsibility.",
                    PreferredExecutorKind = "person",
                    DefaultAllocationPercent = 100
                }
            ],
            Steps = steps
        };
    }

    private static ProcessStepRoleRequirementEditorModel CreateRoleAssignment(Guid roleId)
    {
        return new ProcessStepRoleRequirementEditorModel
        {
            Id = Guid.NewGuid(),
            RoleRequirementId = roleId,
            ResponsibilityKind = ProcessResponsibilityKind.Responsible
        };
    }

    private static ProcessStepDependencyEditorModel CreateDependency(Guid dependsOnStepId)
    {
        return new ProcessStepDependencyEditorModel
        {
            Id = Guid.NewGuid(),
            DependsOnStepId = dependsOnStepId
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

        Assert.True(result.IsSuccess, ToErrorMessage(result.Errors));
        return result.Value;
    }

    private static async Task<Guid> CreateAiManagerAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName)
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

        Assert.True(result.IsSuccess, ToErrorMessage(result.Errors));
        return result.Value;
    }

    private static string ToErrorMessage(IEnumerable<CanDoItAll.SharedKernel.Error> errors)
    {
        return string.Join(" | ", errors.Select(error => error.Message));
    }
}
