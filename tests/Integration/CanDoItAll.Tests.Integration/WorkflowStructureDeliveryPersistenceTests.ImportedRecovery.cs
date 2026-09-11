using System.Data.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Fact]
    public async Task Gateway_catch_cannot_credit_an_imported_native_receipt_after_a_commit_acknowledgement_failure() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var rollback = new CommitFault(afterCommit: false);
        var interrupted = Owner(fixture.Services, Factory(fixture.Services, new ObserveReceiptCommands(rollback), rollback));
        Assert.Same(rollback.Failure, await Assert.ThrowsAsync<ArgumentException>(() =>
            interrupted.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request)));
        Assert.NotNull(await fixture.Owner.FindPreparedWorkflowContributionAsync(fixture.Plan.Identity));
        Assert.Null(await fixture.Owner.FindWorkflowContributionAsync(fixture.Plan.Identity));

        var runStore = fixture.Services.GetRequiredService<IWorkflowRunStore>();
        var run = Assert.IsType<WorkflowRunSnapshot>(await runStore.GetRunAsync(fixture.Plan.Identity.Occurrence.RunId));
        await runStore.SaveRunAsync(run with { State = WorkflowRunState.Failed });
        var fault = new ImportReceiptAfterCommit(fixture.Factory, fixture.Plan.Identity);
        var owner = Owner(fixture.Services, Factory(fixture.Services, fault));
        var gateway = ActivatorUtilities.CreateInstance<WorkbenchProjectStructureRuntimeGateway>(fixture.Services, owner);

        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.ReconcileWorkflowOutputAsync(fixture.Plan.Identity));
        Assert.Equal("Imported execution evidence is historical. Prepare a new admission in this database profile.", rejected.Message);
        Assert.True(fault.SawCommittedNativeReceipt);
        Assert.Equal(1, fault.AcknowledgementFailures);
        var retained = Assert.IsType<ProjectWorkflowContributionResult>(await fixture.Owner.FindWorkflowContributionAsync(fixture.Plan.Identity));
        Assert.Equal(fault.Provenance, retained.ImportedHistory);
        Assert.NotNull(retained.Receipt);
        var output = Assert.IsType<WorkflowStructureOutput>(await OutputStore(fixture.Services).FindAsync(fixture.Plan.Identity));
        Assert.Equal(WorkflowStructureOutputState.Prepared, output.State);
        Assert.Null(output.Receipt);
        await using var read = await fixture.Factory.CreateDbContextAsync();
        Assert.Equal(1, await read.Set<ProjectObjectRecord>().CountAsync(row =>
            row.ProjectId == fixture.ProjectId && row.Title == fixture.Request.Title));
    }

    private sealed class ImportReceiptAfterCommit(OwnerFactory independent, WorkflowStructureOutputIdentity identity) : DbTransactionInterceptor {
        public RetainedEvidenceImport Provenance { get; } = new(Guid.NewGuid(), Guid.NewGuid());
        public bool SawCommittedNativeReceipt { get; private set; }
        public int AcknowledgementFailures { get; private set; }

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (AcknowledgementFailures != 0) {
                return;
            }
            await using var read = await independent.CreateDbContextAsync(cancellationToken);
            var receipt = await read.Set<ProjectWorkflowContributionRecord>().SingleOrDefaultAsync(row =>
                row.RunId == identity.Occurrence.RunId.Value && row.OccurrencePath == identity.Occurrence.Path &&
                row.Slot == identity.Slot && row.ReceiptJson != "", cancellationToken);
            if (receipt is null) {
                return;
            }
            SawCommittedNativeReceipt = await read.Set<ProjectObjectRecord>().AnyAsync(row =>
                row.Id == receipt.NativeObjectId, cancellationToken);
            receipt.ImportedHistory = Provenance;
            await read.SaveChangesAsync(cancellationToken);
            AcknowledgementFailures++;
            throw new ArgumentException("The native commit was acknowledged after its retained receipt became imported history.");
        }
    }
}
