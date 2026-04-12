using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessImportMetadataIntegrationTests
{
    [Fact]
    public async Task ImportAsync_persists_source_format_and_warnings_on_the_working_version()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projectsService, "Imported metadata project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId));

        Assert.True(saveResult.IsSuccess);

        var exportEnvelope = await processesService.ExportAsync(saveResult.Value);
        exportEnvelope.Definition.Id = null;
        exportEnvelope.Definition.WorkingVersionId = null;
        exportEnvelope.Definition.Name = "Imported metadata clone";
        exportEnvelope.SourceFormat = "integration-tests/process-import";
        exportEnvelope.Warnings =
        [
            "First warning",
            "Second warning"
        ];

        var importResult = await processesService.ImportAsync(exportEnvelope);

        Assert.True(importResult.IsSuccess);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var importedVersion = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == importResult.Value)
            .OrderByDescending(item => item.VersionNumber)
            .FirstAsync();

        Assert.Equal(exportEnvelope.SourceFormat, importedVersion.ImportedFrom);
        Assert.Equal(string.Join(Environment.NewLine, exportEnvelope.Warnings), importedVersion.ImportWarnings);
    }

    [Fact]
    public async Task ImportAsync_persists_source_format_when_the_envelope_has_no_warnings()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projectsService, "Imported source-only project");
        var saveResult = await processesService.SaveAsync(BuildDefinitionEditor(projectId));

        Assert.True(saveResult.IsSuccess);

        var exportEnvelope = await processesService.ExportAsync(saveResult.Value);
        exportEnvelope.Definition.Id = null;
        exportEnvelope.Definition.WorkingVersionId = null;
        exportEnvelope.Definition.Name = "Imported source-only clone";
        exportEnvelope.SourceFormat = "integration-tests/process-import/no-warnings";
        exportEnvelope.Warnings = [];

        var importResult = await processesService.ImportAsync(exportEnvelope);

        Assert.True(importResult.IsSuccess);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var importedVersion = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == importResult.Value)
            .OrderByDescending(item => item.VersionNumber)
            .FirstAsync();

        Assert.Equal(exportEnvelope.SourceFormat, importedVersion.ImportedFrom);
        Assert.Equal(string.Empty, importedVersion.ImportWarnings);
    }

    private static ProcessDefinitionEditorModel BuildDefinitionEditor(Guid projectId)
    {
        var roleId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Import metadata source definition",
            Summary = "Definition used to validate import metadata persistence.",
            ValueStatement = "Preserve import provenance on the working version.",
            CustomerName = "Internal",
            OwnerName = "Integration tests",
            InterfaceContractSummary = "Import metadata verification",
            GovernanceNotes = "Exercise import persistence.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "delivery-manager",
                    DisplayName = "Delivery manager",
                    PreferredExecutorKind = "person",
                    DefaultAllocationPercent = 100,
                    CanvasX = 120,
                    CanvasY = 120
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = Guid.NewGuid(),
                    Key = "intake",
                    Title = "Review intake",
                    StepKind = ProcessStepKind.Work,
                    TargetLeadHours = 2,
                    CanvasX = 320,
                    CanvasY = 120,
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

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
