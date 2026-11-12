using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectCreationNamespaceTests {
    [Fact]
    public void Native_reservation_authority_requires_its_exact_original_namespace_and_receipt_retains_it() {
        var original = new DatasetNamespaceId(Guid.NewGuid());
        var record = new ProjectCreationReservationRecord { NamespaceId = original.Value };
        ProjectWriteAdmissionService.RequireReservationNamespace(record, original);
        record.NamespaceId = null;
        Assert.Throws<InvalidOperationException>(() => ProjectWriteAdmissionService.RequireReservationNamespace(record, original));
        record.NamespaceId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => ProjectWriteAdmissionService.RequireReservationNamespace(record, original));
        record.NamespaceId = original.Value;
        record.ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidOperationException>(() => ProjectWriteAdmissionService.RequireReservationNamespace(record, original));
        var admission = new ProjectWriteAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var receipt = new ProjectCreationReceipt(admission, original, null, "retained-fingerprint");
        Assert.Same(original, receipt.Namespace);
        Assert.Same(admission, receipt.Project);
        Assert.Throws<ArgumentNullException>(() => new ProjectCreationReceipt(admission, null!, null, "retained-fingerprint"));
    }

    [Fact]
    public void Transfer_preserves_original_or_legacy_absent_namespace_only_as_imported_history() {
        foreach (Guid? original in new Guid?[] { Guid.NewGuid(), null }) {
            var row = new ProjectTransferReservation {
                Id = Guid.NewGuid(), DatabaseProfileId = Guid.NewGuid(), NamespaceId = original,
                ProjectId = Guid.NewGuid(), LifetimeId = Guid.NewGuid(), RequesterId = Guid.NewGuid(),
                State = ProjectCreationReservationState.Consumed
            };
            var data = new ProjectTransferDataSet { CreationReservations = [row] };
            data.ValidateForImport();
            data.PrepareForTargetImport(Guid.NewGuid(), Guid.NewGuid());
            var restored = JsonSerializer.Deserialize<ProjectTransferReservation>(JsonSerializer.Serialize(row))!;
            Assert.Equal(original, restored.NamespaceId);
            Assert.Equal(row.DatabaseProfileId, restored.DatabaseProfileId);
            Assert.Equal(row.LifetimeId, restored.LifetimeId);
            Assert.Equal(ProjectCreationReservationState.Consumed, restored.State);
            Assert.NotNull(restored.ImportedHistory);
            Assert.Throws<InvalidOperationException>(() => RetainedEvidenceImport.RequireNative(restored.ImportedHistory));
            row.NamespaceId = Guid.Empty;
            Assert.Throws<InvalidDataException>(data.ValidateForImport);
        }
    }
}
