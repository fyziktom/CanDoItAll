using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Operators.UI.Secrets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructureSecretReferenceTests {
    public enum ReferenceAccessBoundary { Allowed, BeforeSubmission, AfterSql, AfterCommit }

    [Theory]
    [InlineData(false, ReferenceAccessBoundary.Allowed)]
    [InlineData(false, ReferenceAccessBoundary.BeforeSubmission)]
    [InlineData(false, ReferenceAccessBoundary.AfterSql)]
    [InlineData(false, ReferenceAccessBoundary.AfterCommit)]
    [InlineData(true, ReferenceAccessBoundary.Allowed)]
    [InlineData(true, ReferenceAccessBoundary.BeforeSubmission)]
    [InlineData(true, ReferenceAccessBoundary.AfterSql)]
    [InlineData(true, ReferenceAccessBoundary.AfterCommit)]
    public async Task Secret_reference_uses_original_structure_grant_at_actual_native_commit(bool edit, ReferenceAccessBoundary boundary) {
        var access = new EditingUiTestAccess();
        var probe = new ReferenceAuthorityProbe(access, boundary);
        var owner = new OwnerGate();
        await using var harness = await HarnessAsync(owner, configure: services => ConfigureReferenceAccess(services, access, probe));
        var target = await SeedAsync(harness);
        var secret = await harness.Context.Services.GetRequiredService<SecretService>().SaveAsync(new() {
            Name = "Original authority reference", SecretValue = Guid.NewGuid().ToString("N")
        });
        Assert.True(secret.IsSuccess);
        var reference = edit ? await Workbench(harness).CreateObjectAsync(target.ProjectId,
            new(ProjectObjectType.SecretReference, "Original reference title", "Original note", "Original purpose", target.Node.Id,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() {
                    SecretReference = new() { SecretId = secret.Value, SecretNameSnapshot = "Original reference title", Purpose = "Original purpose" }
                }))) : null;
        var cut = Render(harness, target.ProjectId);
        if (edit) {
            await EditAsync(cut, reference!.Id);
        } else {
            await OpenAsync(cut, target.Node.Id);
            await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-secret-select']").ChangeAsync(new() { Value = secret.Value.ToString("D") }));
        }
        var state = State(cut);
        var visibleBefore = JsonSerializer.Serialize(cut.FindComponent<CanDoItAll.Components.CanvasLib.CanvasWorkbench>().Instance.Surface.Nodes);
        var before = await ReferenceRowsAsync(harness, target.ProjectId);
        await InputAsync(cut, "purpose", "Submitted exact reference purpose");
        probe.Project = target.ProjectId;
        probe.Armed = boundary is ReferenceAccessBoundary.AfterSql or ReferenceAccessBoundary.AfterCommit;
        access.Allowed = boundary != ReferenceAccessBoundary.BeforeSubmission;
        await UseAsync(cut);
        var committed = boundary is ReferenceAccessBoundary.Allowed or ReferenceAccessBoundary.AfterCommit;
        if (probe.Boundary is ReferenceAccessBoundary.AfterSql or ReferenceAccessBoundary.AfterCommit) {
            Assert.True(probe.Reached);
        }
        Assert.Equal(0, owner.Saves);
        Assert.Equal(0, owner.PlaintextReads);
        if (committed) {
            var saved = Assert.Single(await ReferencesAsync(harness, target.ProjectId));
            Assert.Equal("Submitted exact reference purpose", saved.Notes);
            Assert.Equal(edit ? reference!.Id : saved.Id, state.Receipt!.NodeId);
            Assert.Equal(boundary == ReferenceAccessBoundary.Allowed ? SecretReferencePhase.Observed : SecretReferencePhase.ReferenceCommitted, state.Receipt.Phase);
        } else {
            Assert.Equal(before, await ReferenceRowsAsync(harness, target.ProjectId));
            Assert.Equal(SecretReferencePhase.Refused, state.Receipt!.Phase);
        }
        if (boundary != ReferenceAccessBoundary.Allowed) {
            Assert.Equal(visibleBefore, JsonSerializer.Serialize(cut.FindComponent<CanDoItAll.Components.CanvasLib.CanvasWorkbench>().Instance.Surface.Nodes));
            Assert.Equal("Submitted exact reference purpose", state.Draft.Purpose);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Secret_vault_receipt_does_not_admit_work_after_structure_access_loss(bool afterVault) {
        var access = new EditingUiTestAccess();
        var owner = new OwnerGate();
        await using var harness = await HarnessAsync(owner, configure: services => services.Replace(ServiceDescriptor.Singleton<IEditingUiAccess>(access)));
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await OpenAsync(cut, target.Node.Id);
        await FillAsync(cut);
        var state = State(cut);
        var before = await ReferenceRowsAsync(harness, target.ProjectId);
        var reads = owner.MetadataReads;
        owner.AfterVaultSave = () => access.Allowed = false;
        access.Allowed = afterVault;
        await CreateAsync(cut);
        Assert.Equal(afterVault ? 1 : 0, owner.Saves);
        Assert.Equal(reads, owner.MetadataReads);
        Assert.Equal(before, await ReferenceRowsAsync(harness, target.ProjectId));
        Assert.Equal(afterVault ? SecretReferencePhase.VaultCommitted : SecretReferencePhase.Refused, state.Receipt!.Phase);
        if (afterVault) {
            Assert.Equal(owner.Accepted, state.Receipt.SecretId);
            Assert.Empty(state.Draft.Create.SecretValue);
        }
        Assert.Equal(0, owner.PlaintextReads);
    }

    [Fact]
    public async Task Secret_metadata_read_does_not_publish_after_original_structure_principal_changes() {
        var access = new EditingUiTestAccess();
        var owner = new OwnerGate();
        await using var harness = await HarnessAsync(owner, configure: services => services.Replace(ServiceDescriptor.Singleton<IEditingUiAccess>(access)));
        var target = await SeedAsync(harness);
        var secret = await harness.Context.Services.GetRequiredService<SecretService>().SaveAsync(new() {
            Name = "Withhold late private secret metadata", SecretValue = Guid.NewGuid().ToString("N")
        });
        Assert.True(secret.IsSuccess);
        var cut = Render(harness, target.ProjectId);
        owner.AfterMetadataRead = () => access.Principal = "replacement-operator-principal";
        await OpenAsync(cut, target.Node.Id);
        Assert.Empty(State(cut).Items);
        Assert.True(State(cut).IsUnavailable);
        Assert.DoesNotContain("Withhold late private secret metadata", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(0, owner.Saves);
    }

    private static void ConfigureReferenceAccess(IServiceCollection services, EditingUiTestAccess access, ReferenceAuthorityProbe probe) {
        services.Replace(ServiceDescriptor.Singleton<IEditingUiAccess>(access));
        services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider => new PooledDbContextFactory<WorkbenchDbContext>(
            new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>()).AddInterceptors(probe).Options));
    }

    private static async Task<string> ReferenceRowsAsync(ComponentTestHarness harness, Guid project) {
        await using var db = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var nodes = await db.Set<ProjectObjectRecord>().AsNoTracking().Where(x => x.ProjectId == project && !x.IsSystemManaged).OrderBy(x => x.NodeKey).ToListAsync();
        var links = await db.Set<ProjectObjectLinkRecord>().AsNoTracking().Where(x => x.ProjectId == project).OrderBy(x => x.Id).ToListAsync();
        return JsonSerializer.Serialize(new { Nodes = nodes, Links = links });
    }

    private sealed class ReferenceAuthorityProbe(EditingUiTestAccess access, ReferenceAccessBoundary boundary) : SaveChangesInterceptor, IDbTransactionInterceptor {
        public ReferenceAccessBoundary Boundary => boundary;
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        private DbContext? matched;

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is WorkbenchDbContext db && db.ChangeTracker.Entries<ProjectObjectRecord>()
                    .Any(x => x.Entity.ProjectId == Project && x.Entity.ObjectType == ProjectObjectType.SecretReference)) {
                Assert.NotNull(db.Database.CurrentTransaction);
                matched = db;
                if (boundary == ReferenceAccessBoundary.AfterSql) {
                    Revoke();
                }
            }
            return ValueTask.FromResult(result);
        }

        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (Armed && boundary == ReferenceAccessBoundary.AfterCommit && ReferenceEquals(matched, eventData.Context)) {
                Revoke();
            }
            return Task.CompletedTask;
        }

        private void Revoke() {
            Armed = false;
            Reached = true;
            access.Allowed = false;
        }
    }
}
