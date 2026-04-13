using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessSchemaIntegrationTests
{
    [Fact]
    public async Task Process_schema_rejects_orphan_definition_child_rows()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Set<ProcessStepDependencyDefinition>().AddAsync(new ProcessStepDependencyDefinition
        {
            StepDefinitionId = Guid.NewGuid(),
            DependsOnStepId = Guid.NewGuid(),
            DisplayOrder = 0
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Process_schema_rejects_orphan_runtime_rows()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var reviewStepId = Guid.NewGuid();
        var projectId = await CreateProjectAsync(projectsService, "Process schema orphan runtime project");
        var saveResult = await processesService.SaveAsync(BuildLinearDefinitionEditor(projectId, roleId, intakeStepId, reviewStepId));

        Assert.True(saveResult.IsSuccess);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Set<ProcessStepRun>().AddAsync(new ProcessStepRun
        {
            ProcessRunId = Guid.NewGuid(),
            StepDefinitionId = intakeStepId,
            Sequence = 0,
            Title = "Broken runtime row",
            Status = ProcessStepRunStatus.Pending
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Process_schema_rejects_run_binding_to_a_foreign_definition_version()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process schema foreign version project");
        var firstRoleId = Guid.NewGuid();
        var secondRoleId = Guid.NewGuid();
        var firstDefinition = await processesService.SaveAsync(BuildLinearDefinitionEditor(
            projectId,
            firstRoleId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Foreign version source"));
        var secondDefinition = await processesService.SaveAsync(BuildLinearDefinitionEditor(
            projectId,
            secondRoleId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Foreign version target"));

        Assert.True(firstDefinition.IsSuccess);
        Assert.True(secondDefinition.IsSuccess);
        Assert.True((await processesService.PublishAsync(firstDefinition.Value)).IsSuccess);
        Assert.True((await processesService.PublishAsync(secondDefinition.Value)).IsSuccess);

        Guid publishedVersionId;
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            publishedVersionId = await dbContext.Set<ProcessDefinitionVersion>()
                .Where(item => item.ProcessDefinitionId == secondDefinition.Value && item.Status == ProcessVersionStatus.Published)
                .Select(item => item.Id)
                .SingleAsync();
        }

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        await verificationContext.Set<ProcessRun>().AddAsync(new ProcessRun
        {
            ProcessDefinitionId = firstDefinition.Value,
            ProcessDefinitionVersionId = publishedVersionId,
            ProjectId = projectId,
            Name = "Broken foreign version binding",
            Status = ProcessRunStatus.Draft,
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => verificationContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Process_schema_rejects_active_published_version_binding_to_a_foreign_definition_version()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process schema foreign active version project");
        var firstDefinition = await processesService.SaveAsync(BuildLinearDefinitionEditor(
            projectId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Foreign active version source"));
        var secondDefinition = await processesService.SaveAsync(BuildLinearDefinitionEditor(
            projectId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Foreign active version target"));

        Assert.True(firstDefinition.IsSuccess);
        Assert.True(secondDefinition.IsSuccess);
        Assert.True((await processesService.PublishAsync(firstDefinition.Value)).IsSuccess);
        Assert.True((await processesService.PublishAsync(secondDefinition.Value)).IsSuccess);

        Guid foreignPublishedVersionId;
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            foreignPublishedVersionId = await dbContext.Set<ProcessDefinitionVersion>()
                .Where(item => item.ProcessDefinitionId == secondDefinition.Value && item.Status == ProcessVersionStatus.Published)
                .Select(item => item.Id)
                .SingleAsync();
        }

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var definition = await verificationContext.Set<ProcessDefinition>()
            .SingleAsync(item => item.Id == firstDefinition.Value);
        definition.ActivePublishedVersionId = foreignPublishedVersionId;

        await Assert.ThrowsAsync<DbUpdateException>(() => verificationContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Process_schema_rejects_second_draft_version_for_the_same_definition()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process schema second draft project");
        var saveResult = await processesService.SaveAsync(BuildLinearDefinitionEditor(
            projectId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Second draft source"));

        Assert.True(saveResult.IsSuccess);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        await verificationContext.Set<ProcessDefinitionVersion>().AddAsync(
            BuildVersion(saveResult.Value, 99, ProcessVersionStatus.Draft));

        await Assert.ThrowsAsync<DbUpdateException>(() => verificationContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Process_schema_rejects_second_published_version_for_the_same_definition()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var projectId = await CreateProjectAsync(projectsService, "Process schema second published project");
        var saveResult = await processesService.SaveAsync(BuildLinearDefinitionEditor(
            projectId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Second published source"));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        await verificationContext.Set<ProcessDefinitionVersion>().AddAsync(
            BuildVersion(saveResult.Value, 99, ProcessVersionStatus.Published));

        await Assert.ThrowsAsync<DbUpdateException>(() => verificationContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Process_schema_rejects_duplicate_unconditional_dependencies()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var reviewStepId = Guid.NewGuid();
        var projectId = await CreateProjectAsync(projectsService, "Process schema unconditional dependency project");
        var saveResult = await processesService.SaveAsync(BuildLinearDefinitionEditor(projectId, roleId, intakeStepId, reviewStepId));

        Assert.True(saveResult.IsSuccess);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Set<ProcessStepDependencyDefinition>().AddAsync(new ProcessStepDependencyDefinition
        {
            StepDefinitionId = reviewStepId,
            DependsOnStepId = intakeStepId,
            DependsOnBranchOutcomeId = null,
            DisplayOrder = 99
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Process_schema_rejects_duplicate_conditional_dependencies()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var decisionStepId = Guid.NewGuid();
        var approvedOutcomeId = Guid.NewGuid();
        var routedStepId = Guid.NewGuid();
        var projectId = await CreateProjectAsync(projectsService, "Process schema conditional dependency project");
        var saveResult = await processesService.SaveAsync(BuildBranchingDefinitionEditor(
            projectId,
            roleId,
            intakeStepId,
            decisionStepId,
            approvedOutcomeId,
            routedStepId));

        Assert.True(saveResult.IsSuccess);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await dbContext.Set<ProcessStepDependencyDefinition>().AddAsync(new ProcessStepDependencyDefinition
        {
            StepDefinitionId = routedStepId,
            DependsOnStepId = decisionStepId,
            DependsOnBranchOutcomeId = approvedOutcomeId,
            DisplayOrder = 99
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveAsync_surfaces_duplicate_dependency_shape_conflicts_as_a_validation_error()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var roleId = Guid.NewGuid();
        var projectId = await CreateProjectAsync(projectsService, "Process schema duplicate dependency save project");
        var result = await processesService.SaveAsync(BuildLinearDefinitionEditor(
            projectId,
            roleId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Duplicate dependency shape",
            includeDuplicateDependency: true));

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "processes.dependency-unique-conflict");
    }

    [Fact]
    public async Task Process_schema_hardening_preserves_definition_delete_flow()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();

        var roleId = Guid.NewGuid();
        var intakeStepId = Guid.NewGuid();
        var reviewStepId = Guid.NewGuid();
        var projectId = await CreateProjectAsync(projectsService, "Process schema delete flow project");
        var saveResult = await processesService.SaveAsync(BuildLinearDefinitionEditor(projectId, roleId, intakeStepId, reviewStepId));

        Assert.True(saveResult.IsSuccess);
        Assert.True((await processesService.PublishAsync(saveResult.Value)).IsSuccess);
        Assert.True((await processesService.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = saveResult.Value,
            ProjectId = projectId,
            RunName = "Schema delete verification run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Verify delete behavior after FK hardening."
        })).IsSuccess);

        await processesService.DeleteAsync(saveResult.Value);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await dbContext.Set<ProcessDefinition>().AnyAsync(item => item.Id == saveResult.Value));
        Assert.False(await dbContext.Set<ProcessDefinitionVersion>().AnyAsync(item => item.ProcessDefinitionId == saveResult.Value));
        Assert.False(await dbContext.Set<ProcessStepDefinition>().AnyAsync(item => item.Id == intakeStepId || item.Id == reviewStepId));
        Assert.False(await dbContext.Set<ProcessStepDependencyDefinition>().AnyAsync(item => item.StepDefinitionId == reviewStepId));
        Assert.False(await dbContext.Set<ProcessRun>().AnyAsync(item => item.ProcessDefinitionId == saveResult.Value));
        Assert.False(await dbContext.Set<ProcessStepRun>().AnyAsync());
    }

    private static ProcessDefinitionEditorModel BuildLinearDefinitionEditor(
        Guid projectId,
        Guid roleId,
        Guid intakeStepId,
        Guid reviewStepId,
        string name = "Linear schema proof process",
        bool includeDuplicateDependency = false)
    {
        var dependencies = CreateDependencies((intakeStepId, null));
        if (includeDuplicateDependency)
        {
            dependencies.Add(new ProcessStepDependencyEditorModel
            {
                Id = Guid.NewGuid(),
                DependsOnStepId = intakeStepId
            });
        }

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = name,
            Summary = "Keeps schema proof minimal and explicit.",
            ValueStatement = "Schema invariants stay durable.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Durable schema rules are mandatory.",
            ChangeSummary = "Initial schema proof definition.",
            ConstitutionRuleSummary = "Dependencies must stay explicit and valid.",
            OperatingModeSummary = "Assisted execution for schema validation.",
            SimulationReadinessSummary = "Safe for integration verification.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "delivery-owner",
                    DisplayName = "Delivery owner",
                    Purpose = "Own the schema verification flow.",
                    StaffingIntent = "Primary process owner.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Schema proof owner."
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
                    CanvasX = 120,
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
                    Dependencies = dependencies,
                    CanvasX = 420,
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

    private static ProcessDefinitionEditorModel BuildBranchingDefinitionEditor(
        Guid projectId,
        Guid roleId,
        Guid intakeStepId,
        Guid decisionStepId,
        Guid approvedOutcomeId,
        Guid routedStepId)
    {
        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Branching schema proof process",
            Summary = "Keeps conditional dependency proof explicit.",
            ValueStatement = "Conditional routes must remain durable.",
            CustomerName = "Acme Customer",
            OwnerName = "Morgan Process Lead",
            GovernancePolicySummary = "Conditional branches require explicit outcomes.",
            ChangeSummary = "Initial branching schema proof definition.",
            ConstitutionRuleSummary = "Conditional routes must resolve through a declared branch outcome.",
            OperatingModeSummary = "Assisted execution for branching validation.",
            SimulationReadinessSummary = "Safe for integration verification.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "routing-owner",
                    DisplayName = "Routing owner",
                    Purpose = "Own branching decisions.",
                    StaffingIntent = "Primary decision owner.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Branching proof owner."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "capture-request",
                    Title = "Capture request",
                    StepKind = ProcessStepKind.Start,
                    TargetLeadHours = 1,
                    CanvasX = 120,
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
                    Id = decisionStepId,
                    Key = "route-request",
                    Title = "Route request",
                    StepKind = ProcessStepKind.Decision,
                    TargetLeadHours = 1,
                    Dependencies = CreateDependencies((intakeStepId, null)),
                    DecisionRoleRequirementId = roleId,
                    CanvasX = 420,
                    CanvasY = 180,
                    BranchOutcomes =
                    [
                        new ProcessStepBranchOutcomeEditorModel
                        {
                            Id = approvedOutcomeId,
                            Key = "approved",
                            Title = "Approved",
                            Description = "Route the request forward."
                        }
                    ],
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
                    Id = routedStepId,
                    Key = "complete-request",
                    Title = "Complete request",
                    StepKind = ProcessStepKind.Delivery,
                    TargetLeadHours = 2,
                    Dependencies = CreateDependencies((decisionStepId, approvedOutcomeId)),
                    CanvasX = 720,
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

    private static ProcessDefinitionVersion BuildVersion(
        Guid definitionId,
        int versionNumber,
        ProcessVersionStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProcessDefinitionVersion
        {
            ProcessDefinitionId = definitionId,
            VersionNumber = versionNumber,
            Status = status,
            ChangeSummary = $"Version {versionNumber} / {status}",
            GovernancePolicySummary = "Schema lifecycle proof",
            ConstitutionRuleSummary = "Schema lifecycle proof",
            OperatingModeSummary = "Schema lifecycle proof",
            SimulationReadinessSummary = "Schema lifecycle proof",
            ImportedFrom = string.Empty,
            ImportWarnings = string.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            PublishedAtUtc = status == ProcessVersionStatus.Published ? now : null,
            PublishedBy = status == ProcessVersionStatus.Published ? "integration-tests" : string.Empty
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
}
