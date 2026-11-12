using CanDoItAll.Composition;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed partial class WorkspaceSettingsMigrationTests {
    [Fact]
    [Trait("Category", "LiveProcess")]
    [Trait("Category", "HostPlatform")]
    public Task Whole_archive_preserves_configured_policy_bytes_and_requires_explicit_target_reactivation()
        => AssertPolicyArchiveAsync(configured: true);

    [Fact]
    [Trait("Category", "LiveProcess")]
    [Trait("Category", "HostPlatform")]
    public Task Legacy_whole_archive_upgrade_keeps_all_values_and_revisions_with_explicit_policy_absence()
        => AssertPolicyArchiveAsync(configured: false);

    private static async Task AssertPolicyArchiveAsync(bool configured) {
        var source = await Fixture.CreatePolicyPredecessorAsync();
        Fixture? target = null;
        string? archive = null;
        Exception? primary = null;
        try {
            target = await Fixture.CreateEmptyAsync();
            PolicyRowIds ids;
            await using (var seed = source.Canonical()) {
                ids = await SeedPolicyPredecessorAsync(seed);
                seed.Add(new SearchDocument { SourceType = SearchDocument.ResourceSourceType, SourceKey = Guid.NewGuid().ToString("D"),
                    Category = "resource", Title = "Unrelated retained archive sentinel", Summary = "Exact untouched summary",
                    Body = "Retained sentinel 漢字 <raw>", Route = "/resources", UpdatedAtUtc = SavedAt });
                await seed.SaveChangesAsync();
                if (configured) {
                    await UpgradePolicyAsync(seed);
                }
            }
            var originalBinding = await source.SeedHistoricalPolicyActivationAsync();
            if (configured) {
                var writer = source.PolicyWriter();
                var original = await writer.ReadV2Async();
                var accepted = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await writer.ReplaceV2Async(
                    new(original.Expectation, original.Values, CompletePolicy)));
                Assert.Equal(accepted.Snapshot, await writer.ReadV2Async());
                await using var retained = source.Canonical();
                var noncanonical = " \n" + DecisionDataPolicyCodec.Serialize(CompletePolicy) + "\n ";
                await SetPolicyTextAsync(retained, ids.Current, noncanonical);
                await SetPolicyTextAsync(retained, ids.Inactive, noncanonical);
            }
            string[] originalPayload;
            string[] originalHistory;
            string[] originalSchema;
            string[] sentinel;
            string[] originalLifecycle;
            PolicySavedHead saved;
            await using (var before = source.Canonical()) {
                originalPayload = configured ? await PolicyFullPayloadsAsync(before) : await PolicyPredecessorPayloadsAsync(before);
                originalHistory = await PolicyHistoryAsync(before);
                originalSchema = await PolicySchemaAsync(before);
                sentinel = await PolicySentinelAsync(before);
                originalLifecycle = await PolicyLifecycleAsync(before);
                saved = await before.Database.SqlQueryRaw<PolicySavedHead>("""
                    SELECT "Id", "ConcurrencyToken" AS "Revision", "WorkspaceName", "DefaultProviderProfileId",
                        "DefaultPromptOutputFormat", "CurrencyCode", "CurrencyCultureName", "Notes"
                    FROM "Workspace_Settings" WHERE "IsCurrent"
                    """).SingleAsync();
            }
            var oldCommand = new ReplaceWorkspaceSettingsV2Command(
                new(originalBinding.Namespace, WorkspaceSettingsHeadState.Existing, new(saved.Id, saved.Revision)), saved.Values(), CompletePolicy);
            archive = Path.Combine(source.Environment.RootPath, "workspace-policy.dump");
            await PostgreSqlProtocolRestoreTests.RunClientAsync("pg_dump", source.ConnectionString, "--format=custom", "--file", archive);
            await PostgreSqlProtocolRestoreTests.RunClientAsync("pg_restore", target.ConnectionString,
                "--exit-on-error", "--single-transaction", archive);
            await using (var restored = target.Canonical()) {
                Assert.Equal(originalPayload, configured ? await PolicyFullPayloadsAsync(restored) : await PolicyPredecessorPayloadsAsync(restored));
                Assert.Equal(originalHistory, await PolicyHistoryAsync(restored));
                Assert.Equal(originalSchema, await PolicySchemaAsync(restored));
                Assert.Equal(sentinel, await PolicySentinelAsync(restored));
                Assert.Equal(originalLifecycle, await PolicyLifecycleAsync(restored));
                await UpgradePolicyAsync(restored);
                Assert.Equal(originalPayload, configured ? await PolicyFullPayloadsAsync(restored) : await PolicyPredecessorPayloadsAsync(restored));
                if (!configured) {
                    Assert.All(await restored.Set<WorkspaceSettings>().AsNoTracking().ToArrayAsync(), row => Assert.Null(row.DecisionDataPolicyJson));
                }
                var installed = await PolicyFullPayloadsAsync(restored);
                await UpgradePolicyAsync(restored);
                Assert.Equal(installed, await PolicyFullPayloadsAsync(restored));
                Assert.False(restored.Database.HasPendingModelChanges());
                await restored.Database.MigrateAsync();
                Assert.Equal(installed, await PolicyFullPayloadsAsync(restored));
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => target.PolicyCoordinator.EnsureReadyAsync(target.Profile));
            await using (var unchanged = target.Canonical()) {
                Assert.Equal(originalLifecycle, await PolicyLifecycleAsync(unchanged));
            }
            var replacement = await target.PolicyCoordinator.ReactivateAsync(target.Profile, originalBinding.Namespace, Guid.NewGuid());
            Assert.NotEqual(originalBinding.Namespace, replacement.Namespace);
            Assert.Equal(replacement, await target.PolicyCoordinator.ResumeAsync(target.Profile));
            Assert.Equal(replacement, await target.PolicyCoordinator.EnsureReadyAsync(target.Profile));
            var targetWriter = target.PolicyWriter();
            var observed = await targetWriter.ReadV2Async();
            Assert.Equal(replacement.Namespace, observed.Expectation.Namespace);
            Assert.Equal(oldCommand.Expected.Head, observed.Expectation.Head);
            Assert.Equal(oldCommand.Values, observed.Values);
            Assert.Equal(configured ? CompletePolicy : null, observed.DecisionDataPolicy);
            Assert.Equal(WorkspaceSettingsRejectionReason.DatasetChanged,
                Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Rejected>(await targetWriter.ReplaceV2Async(oldCommand)).Reason);
            await using (var final = target.Canonical()) {
                Assert.Equal(originalPayload, configured ? await PolicyFullPayloadsAsync(final) : await PolicyPredecessorPayloadsAsync(final));
                Assert.Equal(sentinel, await PolicySentinelAsync(final));
                Assert.Equal(observed, await targetWriter.ReadV2Async());
            }
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            var cleanup = new List<Exception>();
            if (archive is not null) {
                try {
                    File.Delete(archive);
                } catch (Exception failure) {
                    cleanup.Add(failure);
                }
            }
            try {
                await TestFixtureCleanup.DisposeAsync(target, source);
            } catch (Exception failure) {
                cleanup.Add(failure);
            }
            if (cleanup.Count != 0) {
                if (primary is not null) {
                    cleanup.Insert(0, primary);
                }
                throw new AggregateException("Workspace policy archive assertion or owned cleanup failed.", cleanup);
            }
        }
    }

    private static Task<string[]> PolicySentinelAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(retained)::text AS "Value" FROM "Infrastructure_SearchDocuments" retained ORDER BY retained."Id"
        """).ToArrayAsync();
    private static Task<string[]> PolicyLifecycleAsync(AppDbContext context) => context.Database.SqlQueryRaw<string>("""
        SELECT to_jsonb(retained)::text AS "Value" FROM "Infrastructure_DatasetLifecycle" retained ORDER BY retained."Id"
        """).ToArrayAsync();

    private sealed record PolicySavedHead(Guid Id, Guid Revision, string WorkspaceName, Guid? DefaultProviderProfileId,
        string DefaultPromptOutputFormat, string CurrencyCode, string CurrencyCultureName, string Notes) {
        internal WorkspaceSettingsValues Values() => new(WorkspaceName, DefaultProviderProfileId, DefaultPromptOutputFormat,
            CurrencyCode, CurrencyCultureName, Notes);
    }

    private sealed partial class Fixture {
        private DatasetActivationService PolicyActivation {
            get {
                var policies = new PhysicalFileSystemPathPolicyFactory();
                var writer = new DurableFileWriter(policies);
                var paths = new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions { RootPath = Environment.ControlPlaneRootPath }),
                    Environment.CreateHostEnvironment(nameof(WorkspaceSettingsMigrationTests)), writer);
                return new(paths, writer, policies);
            }
        }

        internal DatasetActivationCoordinator PolicyCoordinator => new(PolicyActivation,
            [ProjectFiniteOperationPool.Create(new()), WorkbenchTaskPlanFiniteOperations.Create(new())]);

        internal async Task<DatasetActivationBinding> SeedHistoricalPolicyActivationAsync() {
            await using var context = Canonical();
            return await HistoricalDatasetActivation.SeedSharedRowsAsync(context, PolicyActivation, Profile.Profile.Id, SavedAt,
                ProjectFiniteOperationPool.Pool, WorkbenchTaskPlanFiniteOperations.Pool);
        }

        internal WorkspaceSettingsWriter PolicyWriter() => new(new PolicyOwnerFactory(this), new PolicyCanonicalDatabase(Profile),
            PolicyActivation, new SystemClock(), new CurrencyDisplayState(Options.Create(new CurrencyOptions())),
            new NullActivityStream(), NullLogger<WorkspaceSettingsWriter>.Instance);
    }

    private sealed class PolicyOwnerFactory(Fixture fixture) : IDbContextFactory<WorkspaceSettingsDbContext> {
        public WorkspaceSettingsDbContext CreateDbContext() => fixture.Owner();
        public Task<WorkspaceSettingsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class PolicyCanonicalDatabase(ResolvedDatabaseProfile profile) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile => profile;
        public long Generation => 1;
    }
}
