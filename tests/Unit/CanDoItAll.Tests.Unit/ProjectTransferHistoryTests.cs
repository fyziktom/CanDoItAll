using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTransferHistoryTests {
    [Fact]
    public void Import_discards_live_authority_and_preserves_history_bytes_and_prior_provenance() {
        var project = new ProjectTransferProject { LifetimeId = Guid.NewGuid(), LegacyAgentAccessBindingEligible = true, Name = "Restored project", Slug = "restored", Description = "Original description", Objective = "Original objective" };
        var previous = new RetainedEvidenceImport(Guid.NewGuid(), Guid.NewGuid());
        var reservation = new ProjectTransferReservation {
            Id = Guid.NewGuid(), DatabaseProfileId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), LifetimeId = Guid.NewGuid(),
            RequesterId = Guid.NewGuid(), State = ProjectCreationReservationState.Reserved, ImportedHistory = previous
        };
        var contribution = new ProjectWorkflowContributionRecord { RunId = Guid.NewGuid(), ProjectId = project.Id,
            OccurrencePath = "original", Slot = 0, PlanJson = " {\"unknown\":1} ", RequestJson = "saved bytes", ReceiptJson = " receipt " };
        var admission = new ProjectWorkflowAdmissionRecord { IntentId = Guid.NewGuid(), RunId = Guid.NewGuid(),
            ProjectId = project.Id, NativeNodeId = Guid.NewGuid(), NodeId = "node:original", Sequence = 2,
            AdmissionJson = " original authority ", StatusJson = " original result ", Delivery = ProjectWorkflowDeliveryState.Pending };
        var data = new ProjectTransferDataSet { Projects = [project], CreationReservations = [reservation],
            WorkflowContributions = [contribution], WorkflowAdmissions = [admission] };
        var originalProject = JsonSerializer.Serialize(project);
        var sourceProfile = Guid.NewGuid();
        var transfer = Guid.NewGuid();

        data.PrepareForTargetImport(sourceProfile, transfer);

        var restored = Assert.Single(data.Projects);
        Assert.Equal(project.Id, restored.Id);
        Assert.NotEqual(project.LifetimeId, restored.LifetimeId);
        Assert.Equal(Guid.Empty, restored.LifetimeId);
        Assert.False(restored.LegacyAgentAccessBindingEligible);
        Assert.Equal(originalProject, JsonSerializer.Serialize(restored));
        Assert.Equal(ProjectCreationReservationState.Reserved, reservation.State);
        Assert.Equal(new(sourceProfile, transfer, previous), reservation.ImportedHistory);
        Assert.Equal(" {\"unknown\":1} ", contribution.PlanJson);
        Assert.Equal("saved bytes", contribution.RequestJson);
        Assert.Equal(" receipt ", contribution.ReceiptJson);
        Assert.Equal(" original authority ", admission.AdmissionJson);
        Assert.Equal(" original result ", admission.StatusJson);
        Assert.Equal(ProjectWorkflowDeliveryState.Pending, admission.Delivery);
        Assert.Equal(new(sourceProfile, transfer), admission.ImportedHistory);
        Assert.Throws<InvalidOperationException>(() => RetainedEvidenceImport.RequireNative(admission.ImportedHistory));
    }

    [Theory]
    [InlineData(ProjectPackageManifest.LegacyFormat)]
    [InlineData(ProjectPackageManifest.CurrentFormat)]
    public void Exact_legacy_and_history_package_manifests_remain_supported(string format) {
        ProjectPackageService.ValidateManifest(Manifest(format));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void V3_rejects_absent_disposition_or_missing_history_table(bool missingTable) {
        var manifest = Manifest(ProjectPackageManifest.CurrentFormat);
        if (missingTable) {
            manifest.Tables.RemoveAt(manifest.Tables.Count - 1);
        } else {
            manifest.HistoryDisposition = null;
        }
        Assert.Throws<InvalidDataException>(() => ProjectPackageService.ValidateManifest(manifest));
    }

    [Fact]
    public void Retained_deleted_project_history_does_not_require_live_projects_or_nodes() {
        var data = new ProjectTransferDataSet {
            Retirements = [new() { LifetimeId = Guid.NewGuid(), ProjectId = Guid.NewGuid() }],
            CreationReservations = [new() { Id = Guid.NewGuid(), DatabaseProfileId = Guid.NewGuid(), ProjectId = Guid.NewGuid(),
                LifetimeId = Guid.NewGuid(), RequesterId = Guid.NewGuid(), State = ProjectCreationReservationState.Cancelled }],
            WorkflowContributions = [new() { RunId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), OccurrencePath = "deleted", Slot = 0 }],
            WorkflowAdmissions = [new() { IntentId = Guid.NewGuid(), RunId = Guid.NewGuid(), ProjectId = Guid.NewGuid(),
                NativeNodeId = Guid.NewGuid(), NodeId = "deleted", Sequence = 1, Delivery = ProjectWorkflowDeliveryState.TargetDeleted }]
        };
        data.ValidateForImport();
        data.PrepareForPackageExport();
        Assert.Equal(4, data.Counts.Total);
        Assert.Empty(data.Projects);
    }

    [Fact]
    public void Retained_contribution_identity_cannot_be_duplicated_by_a_package() {
        var row = new ProjectWorkflowContributionRecord { RunId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), OccurrencePath = "original", Slot = 0 };
        var data = new ProjectTransferDataSet { WorkflowContributions = [row, row] };
        Assert.Throws<InvalidDataException>(data.ValidateForImport);
    }

    private static ProjectPackageManifest Manifest(string format) {
        string[] tables = ["Projects_Projects", "Projects_ProjectPhases", "Projects_ProjectOptionSelections", "Projects_ProjectHierarchyLinks",
            "Workbench_ProjectObjects", "Workbench_ProjectObjectLinks", "Workbench_ProjectProjectionLayouts", "Workbench_ProjectNodeBindings",
            "Workbench_ProjectNodeReferences", "Workbench_ProjectNodeLifecycleEvents", "Workbench_ProjectCrossModuleMutations", "Workbench_ViewStates"];
        if (format == ProjectPackageManifest.CurrentFormat) {
            tables = [.. tables, "Projects_ProjectRetirements", "Projects_ProjectCreationReservations", "Workbench_WorkflowContributionReceipts", "Workbench_WorkflowAdmissions"];
        }
        return new() { PackageId = Guid.NewGuid(), SourceProfileId = Guid.NewGuid(), Format = format,
            HistoryDisposition = format == ProjectPackageManifest.CurrentFormat ? ProjectPackageHistoryDisposition.PreserveAsHistory : null,
            Tables = tables.Select((table, index) => new ProjectPackageTableManifest { Name = table, FilePath = $"tables/{index}.json", Sha256 = new('A', 64) }).ToList() };
    }
}
