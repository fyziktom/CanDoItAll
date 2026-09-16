using System.IO.Compression;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed partial class ProjectTransferHistoryPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Process_asset_history_transfer_retains_every_opaque_state_and_reexports_original_evidence(bool package) {
        await using var fixture = await TransferFixture.CreateAsync();
        var original = Enum.GetValues<AssetHistoryState>().Select(state => AssetHistory(fixture.SourceProfile.Profile.Id, state)).ToArray();
        original[1].ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
        await using (var source = await fixture.SourceContextAsync()) {
            source.AddRange(original);
            await source.SaveChangesAsync();
        }
        var evidence = original.OrderBy(row => row.IntentId).Select(WithoutProvenance).ToArray();
        var service = fixture.PackageService();
        if (package) {
            var exported = await service.ExportAllAsync(new() { PackagePath = Path.Combine(fixture.Environment.RootPath, "assets.cdaproj") });
            Assert.True(exported.IsSuccess, string.Join("; ", exported.Errors.Select(error => error.Message)));
            Assert.Equal(ProjectPackageManifest.CurrentProcessAssetHistoryVersion, exported.Value!.Manifest.ProcessAssetHistoryVersion);
            var imported = await service.ImportAllAsync(new() { PackagePath = exported.Value.PackagePath, TargetProfileId = fixture.TargetProfile.Profile.Id });
            Assert.True(imported.IsSuccess, string.Join("; ", imported.Errors.Select(error => error.Message)));
            Assert.Equal(original.Length, imported.Value!.RecordsImported);
        } else {
            var imported = await fixture.TransferAsync();
            Assert.True(imported.Success, imported.Message);
            Assert.Equal(original.Length, imported.RecordsCopied);
        }

        await fixture.RestartTargetAsync();
        var restored = (await fixture.LoadAsync(target: true)).ProcessAssetContributions.OrderBy(row => row.IntentId).ToArray();
        Assert.Equal(evidence, restored.Select(WithoutProvenance));
        Assert.All(restored, row => Assert.Equal(fixture.SourceProfile.Profile.Id, row.ImportedHistory!.SourceProfileId));
        Assert.Equal(original[1].ImportedHistory, restored.Single(row => row.IntentId == original[1].IntentId).ImportedHistory!.Previous);
        Assert.Contains(restored, row => row.MaterializedRequestJson.Length == 0 && row.ReceiptJson.Length == 0);
        Assert.Contains(restored, row => row.MaterializedRequestJson.Length > 0 && row.ReceiptJson.Length == 0);
        Assert.Contains(restored, row => row.ReceiptJson.Length > 0);
        await using (var target = await fixture.TargetContextAsync()) {
            Assert.Empty(await target.Set<Project>().ToArrayAsync());
            Assert.Empty(await target.Set<ProjectObjectRecord>().ToArrayAsync());
            Assert.Empty(await target.Set<ProjectWorkAssignmentRecord>().ToArrayAsync());
            Assert.Empty(await target.Set<CanDoItAll.Infrastructure.Storage.StoragePlacementIntentRecord>().ToArrayAsync());
        }

        var reexported = await service.ExportAllAsync(new() {
            SourceProfileId = fixture.TargetProfile.Profile.Id,
            PackagePath = Path.Combine(fixture.Environment.RootPath, "assets-reexport.cdaproj")
        });
        Assert.True(reexported.IsSuccess, string.Join("; ", reexported.Errors.Select(error => error.Message)));
        using var archive = ZipFile.OpenRead(reexported.Value!.PackagePath);
        await using var table = archive.GetEntry("tables/process-asset-contributions.json")!.Open();
        var carried = (await JsonSerializer.DeserializeAsync<ProjectProcessAssetContributionRecord[]>(table, PackageJson))!
            .OrderBy(row => row.IntentId).ToArray();
        Assert.Equal(restored.Select(row => JsonSerializer.Serialize(row)), carried.Select(row => JsonSerializer.Serialize(row)));
        var next = new ProjectTransferDataSet { ProcessAssetContributions = carried.ToList() };
        next.PrepareForTargetImport(fixture.TargetProfile.Profile.Id, Guid.NewGuid());
        Assert.Equal(evidence, next.ProcessAssetContributions.Select(WithoutProvenance));
        Assert.All(next.ProcessAssetContributions, row => Assert.Equal(restored.Single(old => old.IntentId == row.IntentId).ImportedHistory, row.ImportedHistory!.Previous));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ClearTargetAsync());
        await using var unchanged = await fixture.SourceContextAsync();
        Assert.Equal(original.OrderBy(row => row.IntentId).Select(row => JsonSerializer.Serialize(row)),
            (await unchanged.Set<ProjectProcessAssetContributionRecord>().AsNoTracking().OrderBy(row => row.IntentId).ToArrayAsync()).Select(row => JsonSerializer.Serialize(row)));
    }

    [Theory]
    [InlineData(AssetHistoryState.Prepared)]
    [InlineData(AssetHistoryState.Materialized)]
    [InlineData(AssetHistoryState.Committed)]
    public async Task Process_asset_target_evidence_blocks_replacement_even_without_a_surviving_project(AssetHistoryState state) {
        await using var fixture = await TransferFixture.CreateAsync();
        await using (var source = await fixture.SourceContextAsync()) {
            source.Add(new Project { Name = "Fresh project", CurrentPhase = "Execution" });
            await source.SaveChangesAsync();
        }
        var retained = AssetHistory(fixture.TargetProfile.Profile.Id, state);
        await using (var target = await fixture.TargetContextAsync()) {
            target.Add(retained);
            await target.SaveChangesAsync();
        }
        var service = fixture.PackageService();
        var exported = await service.ExportAllAsync(new() { PackagePath = Path.Combine(fixture.Environment.RootPath, "blocked-assets.cdaproj") });
        Assert.True(exported.IsSuccess);
        foreach (var importedHistory in new[] { false, true }) {
            await using (var target = await fixture.TargetContextAsync()) {
                var row = await target.Set<ProjectProcessAssetContributionRecord>().SingleAsync();
                row.ImportedHistory = importedHistory ? new(Guid.NewGuid(), Guid.NewGuid()) : null;
                await target.SaveChangesAsync();
                retained.ImportedHistory = row.ImportedHistory;
            }
            var guard = fixture.SourceServices.GetRequiredService<ProjectTransferTargetStateGuard>();
            await guard.RunLockedImportAsync(fixture.TargetProfile, async (session, token) => {
                Assert.Contains(await guard.FindLockedResiduesAsync(session, token), row =>
                    row.Area == ProjectTransferTargetStateArea.Workbench && row.Description == "retained Process asset contributions");
                return true;
            });
            var rows = await fixture.TransferAsync();
            Assert.False(rows.Success);
            Assert.Equal(0, rows.RecordsCopied);
            var package = await service.ImportAllAsync(new() { PackagePath = exported.Value!.PackagePath, TargetProfileId = fixture.TargetProfile.Profile.Id });
            Assert.False(package.IsSuccess);
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ClearTargetAsync());
            await using var unchanged = await fixture.TargetContextAsync();
            Assert.Empty(await unchanged.Set<Project>().ToArrayAsync());
            Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(await unchanged.Set<ProjectProcessAssetContributionRecord>().AsNoTracking().SingleAsync()));
        }
    }

    [Fact]
    public async Task A_late_Process_asset_history_save_failure_rolls_back_all_earlier_owner_saves() {
        await using var fixture = await TransferFixture.CreateAsync();
        var original = await SeedEvidenceAsync(fixture, includeProject: true);
        var retained = AssetHistory(fixture.SourceProfile.Profile.Id, AssetHistoryState.Materialized);
        await using (var source = await fixture.SourceContextAsync()) {
            source.Add(retained);
            await source.SaveChangesAsync();
        }
        var failure = new RejectAssetHistoryInsert();
        await Assert.ThrowsAsync<InjectedAssetHistoryFailure>(() => fixture.TransferAsync(failure));
        Assert.True(failure.Observed);
        Assert.Equal(0, (await fixture.LoadAsync(target: true)).Counts.Total);
        var unchanged = await fixture.LoadAsync(target: false);
        Assert.Equal(EvidenceSnapshot(original), EvidenceSnapshot(unchanged));
        Assert.Equal(JsonSerializer.Serialize(retained), JsonSerializer.Serialize(Assert.Single(unchanged.ProcessAssetContributions)));
    }

    private static ProjectProcessAssetContributionRecord AssetHistory(Guid profileId, AssetHistoryState state) {
        var plan = $"{{ \"state\":\"{state}\", \"sourceClaim\":\"original claim\", \"notes\":\"π and whitespace\" }}\n";
        var materialized = state == AssetHistoryState.Prepared ? string.Empty : " { \"sourceBytes\":\"dGVzdA==\" }\n";
        return new() {
            IntentId = Guid.NewGuid(), DatabaseProfileId = profileId, ProjectId = Guid.NewGuid(), ProjectLifetimeId = Guid.NewGuid(),
            SourceExecutionRunId = Guid.NewGuid(), NativeObjectId = Guid.NewGuid(), StorageIntentId = Guid.NewGuid(),
            PlanJson = plan, PlanFingerprint = ProjectProcessAssetPersistence.Hash(plan), MaterializedRequestJson = materialized,
            MaterializedFingerprint = materialized.Length == 0 ? string.Empty : ProjectProcessAssetPersistence.Hash(materialized),
            NodeJson = state == AssetHistoryState.Committed ? " { \"node\":\"deleted source node\" }\n" : string.Empty,
            ReceiptJson = state == AssetHistoryState.Committed ? " { \"receipt\":\"original exact receipt\" }\n" : string.Empty,
            PreparedAtUtc = Now
        };
    }

    public enum AssetHistoryState { Prepared, Materialized, Committed }
    private sealed class InjectedAssetHistoryFailure : Exception;

    private sealed class RejectAssetHistoryInsert : SaveChangesInterceptor {
        public bool Observed { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context is WorkbenchDbContext context && context.ChangeTracker.Entries<ProjectProcessAssetContributionRecord>()
                    .Any(entry => entry.State == EntityState.Added)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Observed = true;
                throw new InjectedAssetHistoryFailure();
            }
            return ValueTask.FromResult(result);
        }
    }
}
