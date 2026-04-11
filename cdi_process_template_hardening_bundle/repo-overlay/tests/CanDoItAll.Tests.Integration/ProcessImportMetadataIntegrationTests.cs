using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessImportMetadataIntegrationTests
{
    [Fact]
    public async Task ImportAsync_persists_source_format_and_warning_metadata()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var envelope = new ProcessImportExportEnvelope
        {
            SourceFormat = "CanDoItAll.ProcessTemplatePack/v2",
            Warnings =
            [
                "Projected from template pack.",
                "Detailed sidecar metadata remains in the file pack."
            ],
            Definition = BuildDefinition()
        };

        var importResult = await processesService.ImportAsync(envelope);

        Assert.True(importResult.IsSuccess);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var definition = await dbContext.Set<ProcessDefinition>()
            .SingleAsync(item => item.Id == importResult.Value);
        var version = await dbContext.Set<ProcessDefinitionVersion>()
            .Where(item => item.ProcessDefinitionId == definition.Id)
            .OrderByDescending(item => item.VersionNumber)
            .SingleAsync();

        Assert.Equal("CanDoItAll.ProcessTemplatePack/v2", version.ImportedFrom);
        Assert.Contains("Projected from template pack.", version.ImportWarnings, StringComparison.Ordinal);
        Assert.Contains("Detailed sidecar metadata remains in the file pack.", version.ImportWarnings, StringComparison.Ordinal);
    }

    private static ProcessDefinitionEditorModel BuildDefinition()
    {
        var roleId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            Name = "Imported metadata probe",
            Summary = "Verifies import metadata persistence.",
            ValueStatement = "Keep template import provenance visible after save.",
            OwnerName = "Morgan QA",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = roleId,
                    Key = "metadata-owner",
                    DisplayName = "Metadata owner",
                    Purpose = "Own import provenance checks.",
                    StaffingIntent = "Single accountable reviewer.",
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Metadata owner snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = stepId,
                    Key = "capture-metadata",
                    Title = "Capture import metadata",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Imported envelope.",
                    OutputContractSummary = "Stored metadata.",
                    EvidenceContractSummary = "Import metadata retained on the working version.",
                    DecisionRightsSummary = "Metadata owner can accept the import metadata.",
                    ExceptionPolicySummary = "Stop when provenance is not durable.",
                    TargetLeadHours = 1,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = roleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Rebind to the active metadata owner."
                        }
                    ]
                }
            ]
        };
    }
}
