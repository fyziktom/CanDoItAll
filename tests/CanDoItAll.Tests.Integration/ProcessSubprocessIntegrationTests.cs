using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
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

        var parentRoleId = await LoadPublishedRoleIdAsync(dbContextFactory, parentDefinitionId, "process-owner");
        await ResolveRunAssignmentAsync(
            processesService,
            parentRunId,
            parentRoleId,
            managerPartyId,
            "Integration subprocess manager");

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

        await using (var staleStatusDbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var staleChildRun = await staleStatusDbContext.Set<ProcessRun>()
                .SingleAsync(item => item.Id == childRunId);
            staleChildRun.Status = ProcessRunStatus.Active;
            staleChildRun.CompletedAtUtc = null;
            await staleStatusDbContext.SaveChangesAsync();
        }

        await dispatchService.DispatchAsync(parentRunId, subprocessStep.Id, "integration-subprocess-complete");

        childRun = await processesService.GetRunAsync(childRunId);
        Assert.NotNull(childRun);
        Assert.Equal(ProcessRunStatus.Completed, childRun.Status);

        parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        subprocessStep = Assert.Single(parentSteps, step => step.Title == "Run child validation subprocess");

        Assert.Equal(ProcessStepRunStatus.Completed, subprocessStep.Status);
        Assert.NotNull(subprocessStep.SubprocessRun);
        Assert.Equal(ProcessRunStatus.Completed, subprocessStep.SubprocessRun.Status);
        Assert.Equal(2, subprocessStep.SubprocessRun.CompletedStepCount);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var projectedSubprocessArtifact = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .SingleAsync(item =>
                item.ProcessRunId == parentRunId &&
                item.StepRunId == subprocessStep.Id &&
                item.Title == "Child subprocess completion evidence");

        Assert.StartsWith($"subprocess-run:{childRunId:D}:artifact:", projectedSubprocessArtifact.ExternalReferenceKey);
        Assert.Contains(childRunId.ToString("D"), projectedSubprocessArtifact.ProvenanceSummary, StringComparison.Ordinal);

        var directiveResult = await processesService.RecordManagerDirectiveAsync(
            new ProcessManagerDirectiveRequest
            {
                ProcessRunId = parentRunId,
                Directive = "Summarize subprocess blockers and request the narrowest unblock action.",
                InstructedBy = "integration-tests"
            });

        Assert.True(directiveResult.IsSuccess, ToErrorMessage(directiveResult.Errors));
        var directive = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .SingleAsync(entry =>
                entry.ProcessRunId == parentRunId &&
                entry.EventType == ProcessRuntimeEventTypes.ManagerDirectiveRecorded);

        Assert.Contains("Integration subprocess manager", directive.ReplayContextJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("subprocess blockers", directive.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Subprocess_step_blocks_when_child_run_has_only_capability_gap_steps()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dispatchService = scope.ServiceProvider.GetRequiredService<IProcessRunAutomationDispatchService>();

        var projectId = await CreateProjectAsync(projectsService, "Subprocess capability gap validation");
        var managerPartyId = await CreateAiManagerAsync(partyDirectoryService, "Capability gap subprocess manager");
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
                RunName = "Subprocess capability gap validation run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Integration validation of subprocess capability-gap escalation."
            });

        Assert.True(runResult.IsSuccess, ToErrorMessage(runResult.Errors));
        var parentRunId = runResult.Value;
        var parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        await CompleteStepAsync(
            processesService,
            parentRunId,
            Assert.Single(parentSteps, step => step.Title == "Capture parent intake").Id);

        parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        var subprocessStep = Assert.Single(parentSteps, step => step.Title == "Run child validation subprocess");
        await dispatchService.DispatchAsync(parentRunId, subprocessStep.Id, "integration-subprocess-capability-gap");

        parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        subprocessStep = Assert.Single(parentSteps, step => step.Title == "Run child validation subprocess");
        var parentRun = await processesService.GetRunAsync(parentRunId);

        Assert.Equal(ProcessStepRunStatus.Blocked, subprocessStep.Status);
        Assert.Equal(ProcessRunStatus.Blocked, parentRun!.Status);
        Assert.NotNull(subprocessStep.SubprocessRun);
        Assert.Equal(ProcessRunStatus.Active, subprocessStep.SubprocessRun.Status);
        Assert.Contains("capability gaps", subprocessStep.BlockedReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Resolve the subprocess role assignments", subprocessStep.BlockedReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Subprocess_run_inherits_parent_role_bindings_by_role_template()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dispatchService = scope.ServiceProvider.GetRequiredService<IProcessRunAutomationDispatchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Subprocess assignment inheritance validation");
        var managerPartyId = await CreateAiManagerAsync(partyDirectoryService, "Parent delivery manager");
        var engineerPartyId = await CreateAiManagerAsync(partyDirectoryService, "Parent lead engineer");
        var childDefinition = BuildChildDefinitionWithInheritedRoles(projectId);
        var childDefinitionId = await SaveAndPublishAsync(processesService, childDefinition.Definition);
        var parentDefinition = BuildParentDefinitionWithInheritedRoles(projectId, childDefinitionId);
        var parentDefinitionId = await SaveAndPublishAsync(processesService, parentDefinition.Definition);

        var runResult = await processesService.StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = parentDefinitionId,
                ProjectId = projectId,
                RunName = "Subprocess assignment inheritance run",
                OperatingMode = ProcessOperatingMode.Development,
                TriggerReason = "Integration validation of subprocess assignment inheritance."
            });

        Assert.True(runResult.IsSuccess, ToErrorMessage(runResult.Errors));
        var parentRunId = runResult.Value;
        await ResolveRunAssignmentAsync(
            processesService,
            parentRunId,
            parentDefinition.ManagerRoleId,
            managerPartyId,
            "Parent delivery manager");
        await ResolveRunAssignmentAsync(
            processesService,
            parentRunId,
            parentDefinition.LeadEngineerRoleId,
            engineerPartyId,
            "Parent lead engineer");

        var parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        await CompleteStepAsync(
            processesService,
            parentRunId,
            Assert.Single(parentSteps, step => step.Title == "Capture inherited parent intake").Id);

        parentSteps = await processesService.ListStepRunsAsync(parentRunId);
        var subprocessStep = Assert.Single(parentSteps, step => step.Title == "Run inherited assignment subprocess");
        await dispatchService.DispatchAsync(parentRunId, subprocessStep.Id, "integration-subprocess-inheritance");

        var subprocessResult = await processesService.EnsureSubprocessRunForStepAsync(subprocessStep.Id);

        Assert.True(subprocessResult.IsSuccess, ToErrorMessage(subprocessResult.Errors));
        var childRunId = subprocessResult.Value!.RunId;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var childAssignments = await dbContext.Set<ProcessRunAssignment>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == childRunId)
            .ToListAsync();

        var childManagerAssignment = Assert.Single(childAssignments, item => item.RoleRequirementId == childDefinition.ManagerRoleId);
        var childEngineerAssignment = Assert.Single(childAssignments, item => item.RoleRequirementId == childDefinition.EngineerRoleId);

        Assert.Equal(managerPartyId, childManagerAssignment.PartyId);
        Assert.Equal("Parent delivery manager", childManagerAssignment.DisplayName);
        Assert.False(childManagerAssignment.IsCapabilityGap);
        Assert.Contains("Inherited subprocess role binding", childManagerAssignment.BindingReason, StringComparison.Ordinal);
        Assert.Contains("matching role template", childManagerAssignment.BindingReason, StringComparison.Ordinal);

        Assert.Equal(engineerPartyId, childEngineerAssignment.PartyId);
        Assert.Equal("Parent lead engineer", childEngineerAssignment.DisplayName);
        Assert.False(childEngineerAssignment.IsCapabilityGap);
        Assert.Contains("Inherited subprocess role binding", childEngineerAssignment.BindingReason, StringComparison.Ordinal);
        Assert.Contains("matching role template", childEngineerAssignment.BindingReason, StringComparison.Ordinal);

        var childSteps = await processesService.ListStepRunsAsync(childRunId);
        var childStep = Assert.Single(childSteps, step => step.Title == "Capture inherited child work");
        var persistedChildStep = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == childStep.Id);

        Assert.Equal(ProcessStepRunStatus.Ready, childStep.Status);
        Assert.Equal("Parent lead engineer", childStep.CurrentExecutorName);
        Assert.Equal(engineerPartyId, persistedChildStep.CurrentExecutorPartyId);
    }

    [Fact]
    public async Task Default_templates_import_nested_subprocess_references_and_generic_software_delivery_implementation()
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
            "dotnet-solution-setup",
            projectId);
        var duplicateSetupImport = await processesService.ImportAsync(
            projectionService.GetProjectedEnvelope(
                "dotnet-solution-setup",
                projectId,
                ".NET solution setup subprocess"));

        Assert.True(duplicateSetupImport.IsSuccess, ToErrorMessage(duplicateSetupImport.Errors));
        Assert.True((await processesService.PublishAsync(duplicateSetupImport.Value)).IsSuccess);

        var featureImplementationDefinitionId = await ImportAndPublishTemplateAsync(
            processesService,
            projectionService,
            "dotnet-feature-function-implementation",
            projectId);
        var duplicateFeatureImplementationImport = await processesService.ImportAsync(
            projectionService.GetProjectedEnvelope(
                "dotnet-feature-function-implementation",
                projectId,
                ".NET feature/function implementation subprocess"));

        Assert.True(duplicateFeatureImplementationImport.IsSuccess, ToErrorMessage(duplicateFeatureImplementationImport.Errors));
        Assert.True((await processesService.PublishAsync(duplicateFeatureImplementationImport.Value)).IsSuccess);

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
        var sliceFeatureSubprocessStep = await LoadPublishedStepAsync(dbContext, sliceDefinitionId, "implement-code-change");
        var softwareDeliveryFeatureIntakeStep = await LoadPublishedStepAsync(dbContext, softwareDeliveryDefinitionId, "feature-intake");
        var softwareDeliveryImplementationStep = await LoadPublishedStepAsync(dbContext, softwareDeliveryDefinitionId, "implementation");
        var softwareDeliveryQaStep = await LoadPublishedStepAsync(dbContext, softwareDeliveryDefinitionId, "qa-validation");
        var softwareDeliveryReleaseApprovalStep = await LoadPublishedStepAsync(dbContext, softwareDeliveryDefinitionId, "release-approval");
        var featureIntakeArtifact = await dbContext.Set<ProcessArtifactExpectation>()
            .AsNoTracking()
            .SingleAsync(item =>
                item.StepDefinitionId == softwareDeliveryFeatureIntakeStep.Id &&
                item.Title == "Scope boundary packet");
        var qaEvidenceArtifact = await dbContext.Set<ProcessArtifactExpectation>()
            .AsNoTracking()
            .SingleAsync(item =>
                item.StepDefinitionId == softwareDeliveryQaStep.Id &&
                item.Title == "Regression evidence pack");

        Assert.Equal(ProcessStepKind.Subprocess, sliceSubprocessStep.StepKind);
        Assert.Equal(setupDefinitionId, sliceSubprocessStep.SubprocessDefinitionId);
        Assert.Equal(".NET solution setup subprocess", sliceSubprocessStep.SubprocessDefinitionSnapshotName);
        Assert.Equal(ProcessStepKind.Subprocess, sliceFeatureSubprocessStep.StepKind);
        Assert.Equal(featureImplementationDefinitionId, sliceFeatureSubprocessStep.SubprocessDefinitionId);
        Assert.Equal(".NET feature/function implementation subprocess", sliceFeatureSubprocessStep.SubprocessDefinitionSnapshotName);
        Assert.Equal(ProcessStepKind.Work, softwareDeliveryImplementationStep.StepKind);
        Assert.Null(softwareDeliveryImplementationStep.SubprocessDefinitionId);
        Assert.Equal(string.Empty, softwareDeliveryImplementationStep.SubprocessDefinitionSnapshotName);
        Assert.Contains(
            "project-structure",
            softwareDeliveryImplementationStep.Notes,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "output location",
            softwareDeliveryImplementationStep.Notes,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "must not be downgraded",
            softwareDeliveryFeatureIntakeStep.Notes,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "without downgrading",
            featureIntakeArtifact.ValidationRequirementSummary,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "shipped entrypoint/runtime consistency",
            softwareDeliveryQaStep.OutputContractSummary,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "referenced-runtime inspection",
            softwareDeliveryQaStep.EvidenceContractSummary,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "stale/unreferenced artifact findings",
            qaEvidenceArtifact.ValidationRequirementSummary,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "shipped entrypoint and referenced runtime",
            softwareDeliveryReleaseApprovalStep.InputContractSummary,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Default_template_synchronization_preserves_existing_definition_identity()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Default subprocess template sync validation");
        await ImportAndPublishTemplateAsync(
            processesService,
            projectionService,
            "dotnet-solution-setup",
            projectId);
        var featureDefinitionId = await ImportAndPublishTemplateAsync(
            processesService,
            projectionService,
            "dotnet-feature-function-implementation",
            projectId);
        var oldSliceEnvelope = projectionService.GetProjectedEnvelope("dotnet-development-slice", projectId);
        var oldImplementationStep = oldSliceEnvelope.Definition.Steps
            .Single(step => step.Key == "implement-code-change");

        oldImplementationStep.Title = "Implement bounded code change";
        oldImplementationStep.StepKind = ProcessStepKind.Work;
        oldImplementationStep.SubprocessDefinitionId = null;
        oldImplementationStep.SubprocessDefinitionSnapshotName = string.Empty;

        var importResult = await processesService.ImportAsync(oldSliceEnvelope);
        Assert.True(importResult.IsSuccess, ToErrorMessage(importResult.Errors));
        Assert.True((await processesService.PublishAsync(importResult.Value)).IsSuccess);

        var synchronizeResult = await processesService.SynchronizeImportedDefinitionAsync(
            importResult.Value,
            projectionService.GetProjectedEnvelope("dotnet-development-slice", projectId));

        Assert.True(synchronizeResult.IsSuccess, ToErrorMessage(synchronizeResult.Errors));
        Assert.True(synchronizeResult.Value);
        Assert.True((await processesService.PublishAsync(importResult.Value)).IsSuccess);

        var definitions = await processesService.ListDefinitionsAsync(projectId);
        Assert.Single(definitions, definition => definition.Name == ".NET implementation slice with atomic validation");

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var implementationStep = await LoadPublishedStepAsync(dbContext, importResult.Value, "implement-code-change");

        Assert.Equal(ProcessStepKind.Subprocess, implementationStep.StepKind);
        Assert.Equal(featureDefinitionId, implementationStep.SubprocessDefinitionId);
        Assert.Equal(".NET feature/function implementation subprocess", implementationStep.SubprocessDefinitionSnapshotName);
    }

    [Fact]
    public async Task Default_ai_delivery_template_keeps_governance_roles_agent_executable()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "AI delivery role binding validation");
        var definitionId = await ImportAndPublishTemplateAsync(
            processesService,
            projectionService,
            "ai-assisted-change-delivery",
            projectId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var definition = await dbContext.Set<ProcessDefinition>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == definitionId);

        Assert.NotNull(definition.ActivePublishedVersionId);
        var roles = await dbContext.Set<ProcessRoleRequirement>()
            .AsNoTracking()
            .Where(item => item.ProcessDefinitionVersionId == definition.ActivePublishedVersionId!.Value)
            .Where(item => new[]
            {
                "product-owner",
                "solution-architect",
                "ai-safety-reviewer",
                "model-risk-approver",
                "ai-evaluation-lead",
                "qa-lead",
                "security-reviewer",
                "release-approver"
            }.Contains(item.Key))
            .ToListAsync();

        Assert.Equal(8, roles.Count);
        Assert.All(
            roles,
            role =>
            {
                Assert.Equal(ProcessExecutorKindNames.AiAgent, role.PreferredExecutorKind);
                Assert.True(role.AllowsFallback, role.Key);
            });
    }

    [Fact]
    public async Task Dotnet_solution_setup_template_routes_console_scaffold_to_general_dotnet_agent()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var projectionService = scope.ServiceProvider.GetRequiredService<ProcessTemplateProjectionService>();
        var workbenchService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projectsService, "Generic .NET setup staffing validation");
        var workItem = await workbenchService.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Build todo summary console app",
                "Implementation",
                "Product root: C:\\programovani\\candoitall-test-dotnet-console. Architecture: .NET console application. Solution name: TodoSummary. App project: src/TodoSummary.Console. Test project: tests/TodoSummary.Tests. Exact interface: stdin and --text only. No web UI, no browser UI, no database, no background service.",
                null,
                ObjectSubtype: "task"));
        var definitionId = await ImportAndPublishTemplateAsync(
            processesService,
            projectionService,
            "dotnet-solution-setup",
            projectId);

        var runResult = await processesService.StartRunAsync(
            new ProcessRunStartRequest
            {
                ProcessDefinitionId = definitionId,
                ProjectId = projectId,
                RunName = "Generic .NET setup staffing validation run",
                OperatingMode = ProcessOperatingMode.AssistedExecution,
                TriggerReason = "Validate that generic setup staffing follows the selected console mindmap.",
                ProjectStructureContext = new ProcessProjectStructureContext
                {
                    ProjectId = projectId,
                    NodeId = "process-node",
                    NodeTitle = "Generic setup process",
                    ParentNodeId = workItem.Id,
                    ParentNodeTitle = workItem.Title
                }
            });

        Assert.True(runResult.IsSuccess, ToErrorMessage(runResult.Errors));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var engineerAssignment = await (
            from assignment in dbContext.Set<ProcessRunAssignment>().AsNoTracking()
            join role in dbContext.Set<ProcessRoleRequirement>().AsNoTracking()
                on assignment.RoleRequirementId equals role.Id
            where assignment.ProcessRunId == runResult.Value &&
                  role.DisplayName == "Generic .NET scaffold engineer"
            select assignment)
            .SingleAsync();

        Assert.Equal(".NET Application Developer", engineerAssignment.DisplayName);
        Assert.NotEqual("Blazor Application Developer", engineerAssignment.DisplayName);
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

    private static async Task<Guid> LoadPublishedRoleIdAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid definitionId,
        string roleKey)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var definition = await dbContext.Set<ProcessDefinition>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == definitionId);

        Assert.NotNull(definition.ActivePublishedVersionId);
        return await dbContext.Set<ProcessRoleRequirement>()
            .AsNoTracking()
            .Where(item => item.ProcessDefinitionVersionId == definition.ActivePublishedVersionId!.Value)
            .Where(item => item.Key == roleKey)
            .Select(item => item.Id)
            .SingleAsync();
    }

    private static async Task ResolveRunAssignmentAsync(
        ProcessesService processesService,
        Guid runId,
        Guid roleId,
        Guid partyId,
        string displayName)
    {
        var result = await processesService.ResolveAssignmentAsync(
            new ProcessAssignmentResolutionRequest
            {
                ProcessRunId = runId,
                RoleRequirementId = roleId,
                PartyId = partyId,
                DisplayName = displayName,
                ExecutorKind = "AI agent",
                BindingReason = "Approved parent run assignment for subprocess inheritance validation.",
                AllowsDirectMessaging = true
            });

        Assert.True(result.IsSuccess, ToErrorMessage(result.Errors));
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
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            Title = "Child subprocess completion evidence",
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            IsRequired = true,
                            TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired,
                            SensitivityLevel = ProcessSensitivityLevel.Internal,
                            AllowedFutureUsageSummary = "Parent process may use this projection to continue after the child subprocess completes.",
                            ValidationRequirementSummary = "Must point at the completed child subprocess run instead of duplicating child runtime state."
                        }
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

    private static (
        ProcessDefinitionEditorModel Definition,
        Guid ManagerRoleId,
        Guid EngineerRoleId) BuildChildDefinitionWithInheritedRoles(Guid projectId)
    {
        var managerRoleId = Guid.NewGuid();
        var engineerRoleId = Guid.NewGuid();
        var childStepId = Guid.NewGuid();

        return (
            new ProcessDefinitionEditorModel
            {
                ProjectId = projectId,
                Name = "Child subprocess with inherited assignments",
                Summary = "Validates that subprocess runs snapshot compatible parent role bindings.",
                ValueStatement = "Subprocesses inherit compatible runtime role bindings without sharing mutable assignment state.",
                CustomerName = "Integration customer",
                OwnerName = "Integration owner",
                GovernancePolicySummary = "Child run owns its durable assignment records after creation.",
                ChangeSummary = "Subprocess assignment inheritance validation.",
                ConstitutionRuleSummary = "Do not leave subprocess agent roles unassigned when parent roles already have compatible bindings.",
                OperatingModeSummary = "Development mode with parent-run assignment inheritance.",
                SimulationReadinessSummary = "Safe deterministic test definition.",
                Roles =
                [
                    CreateTemplateRole(
                        managerRoleId,
                        "delivery-manager",
                        "Child delivery manager",
                        "process-role-template/delivery-manager",
                        "Delivery manager / template-pack v1",
                        ProjectPartyAssignmentRole.Manager),
                    CreateTemplateRole(
                        engineerRoleId,
                        "software-engineer",
                        "Child implementation engineer",
                        "process-role-template/software-engineer",
                        "Software engineer / template-pack v1",
                        null)
                ],
                Steps =
                [
                    new ProcessStepEditorModel
                    {
                        Id = childStepId,
                        Key = "child-work",
                        Title = "Capture inherited child work",
                        StepKind = ProcessStepKind.Start,
                        TargetLeadHours = 1,
                        RoleAssignments =
                        [
                            CreateRoleAssignment(engineerRoleId)
                        ]
                    }
                ]
            },
            managerRoleId,
            engineerRoleId);
    }

    private static (
        ProcessDefinitionEditorModel Definition,
        Guid ManagerRoleId,
        Guid LeadEngineerRoleId) BuildParentDefinitionWithInheritedRoles(
        Guid projectId,
        Guid childDefinitionId)
    {
        var managerRoleId = Guid.NewGuid();
        var leadEngineerRoleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var subprocessStepId = Guid.NewGuid();

        return (
            new ProcessDefinitionEditorModel
            {
                ProjectId = projectId,
                Name = "Parent process with inherited subprocess assignments",
                Summary = "Runs a subprocess that should inherit compatible parent role bindings.",
                ValueStatement = "Parent launch bindings become child-run snapshots by stable role template identity.",
                CustomerName = "Integration customer",
                OwnerName = "Integration owner",
                GovernancePolicySummary = "Parent and child runs retain separate assignment tables.",
                ChangeSummary = "Subprocess assignment inheritance validation.",
                ConstitutionRuleSummary = "Subprocess dispatch must not depend on mutable parent assignment lookup.",
                OperatingModeSummary = "Development mode with explicit parent assignments.",
                SimulationReadinessSummary = "Safe deterministic test definition.",
                Roles =
                [
                    CreateTemplateRole(
                        managerRoleId,
                        "delivery-manager",
                        "Parent delivery manager",
                        "process-role-template/delivery-manager",
                        "Delivery manager / template-pack v1",
                        ProjectPartyAssignmentRole.Manager),
                    CreateTemplateRole(
                        leadEngineerRoleId,
                        "lead-engineer",
                        "Parent lead engineer",
                        "process-role-template/software-engineer",
                        "Software engineer / template-pack v1",
                        ProjectPartyAssignmentRole.TeamMember)
                ],
                Steps =
                [
                    new ProcessStepEditorModel
                    {
                        Id = intakeStepId,
                        Key = "parent-intake",
                        Title = "Capture inherited parent intake",
                        StepKind = ProcessStepKind.Start,
                        TargetLeadHours = 1,
                        RoleAssignments =
                        [
                            CreateRoleAssignment(managerRoleId)
                        ]
                    },
                    new ProcessStepEditorModel
                    {
                        Id = subprocessStepId,
                        Key = "inherited-subprocess",
                        Title = "Run inherited assignment subprocess",
                        StepKind = ProcessStepKind.Subprocess,
                        SubprocessDefinitionId = childDefinitionId,
                        SubprocessDefinitionSnapshotName = "Child subprocess with inherited assignments",
                        TargetLeadHours = 1,
                        Dependencies =
                        [
                            CreateDependency(intakeStepId)
                        ],
                        RoleAssignments =
                        [
                            CreateRoleAssignment(leadEngineerRoleId)
                        ]
                    }
                ]
            },
            managerRoleId,
            leadEngineerRoleId);
    }

    private static ProcessRoleEditorModel CreateTemplateRole(
        Guid roleId,
        string key,
        string displayName,
        string templateSourceKey,
        string templateSnapshotName,
        ProjectPartyAssignmentRole? preferredProjectAssignmentRole)
    {
        return new ProcessRoleEditorModel
        {
            Id = roleId,
            Key = key,
            DisplayName = displayName,
            Purpose = $"Own {displayName} responsibilities.",
            StaffingIntent = $"Use the bound {displayName} assignment.",
            PreferredExecutorKind = "agent",
            PreferredProjectAssignmentRole = preferredProjectAssignmentRole,
            DefaultAllocationPercent = 100,
            RoleTemplateSourceKey = templateSourceKey,
            RoleTemplateSnapshotName = templateSnapshotName,
            SnapshotSummary = $"{displayName} template summary."
        };
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
