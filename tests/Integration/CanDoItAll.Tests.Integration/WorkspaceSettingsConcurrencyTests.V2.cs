using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed partial class WorkspaceSettingsConcurrencyTests {
    [Fact]
    public async Task V2_legacy_absence_and_empty_creation_are_explicit_without_seeding_a_policy() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var legacy = await owner.ReadSettingsSnapshotV2Async();
        Assert.Equal(WorkspaceSettingsHeadState.Existing, legacy.Expectation.State);
        Assert.Null(legacy.DecisionDataPolicy);
        await using (var seed = await CanonicalAsync(application)) {
            await seed.Set<WorkspaceSettings>().Where(row => row.IsCurrent).ExecuteUpdateAsync(update => update.SetProperty(row => row.IsCurrent, false));
        }
        var empty = await owner.ReadSettingsSnapshotV2Async();
        Assert.Equal(WorkspaceSettingsHeadState.Empty, empty.Expectation.State);
        Assert.Null(empty.DecisionDataPolicy);
        var created = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await owner.SaveSettingsV2Async(
            new(empty.Expectation, empty.Values, RootPolicy())));
        Assert.Equal(RootPolicy(), created.Snapshot.DecisionDataPolicy);
        Assert.Equal(created.Snapshot, await owner.ReadSettingsSnapshotV2Async());
        await using var read = await CanonicalAsync(application);
        Assert.Equal(2, await read.Set<WorkspaceSettings>().CountAsync());
        Assert.Null((await read.Set<WorkspaceSettings>().SingleAsync(row => row.Id == legacy.Expectation.Head!.Id)).DecisionDataPolicyJson);
        Assert.Equal(DecisionDataPolicyCodec.Serialize(RootPolicy()),
            (await read.Set<WorkspaceSettings>().SingleAsync(row => row.IsCurrent)).DecisionDataPolicyJson);
    }

    [Fact]
    public async Task V2_replaces_values_and_policy_atomically_and_policy_only_change_stales_both_contracts() {
        var probe = new MutationProbe();
        await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = probe.Configure });
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotV2Async();
        var submitted = new WorkspaceSettingsValues("  Configured  ", original.Values.DefaultProviderProfileId, " JSON ", "eur", "de-DE", "  Saved notes  ");
        probe.Armed = true;
        var accepted = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await owner.SaveSettingsV2Async(
            new(original.Expectation, submitted, RootPolicy())));
        Assert.Equal(new WorkspaceSettingsValues("Configured", submitted.DefaultProviderProfileId, "JSON", "EUR", "de-DE", "Saved notes"), accepted.Snapshot.Values);
        Assert.Equal(RootPolicy(), accepted.Snapshot.DecisionDataPolicy);
        Assert.Equal(accepted.Snapshot, await owner.ReadSettingsSnapshotV2Async());
        var update = Assert.Single(probe.Updates);
        var predicate = update.Sql[update.Sql.IndexOf("WHERE", StringComparison.Ordinal)..];
        Assert.Contains("\"ConcurrencyToken\"", predicate, StringComparison.Ordinal);
        Assert.Contains(original.Expectation.Head!.Revision, update.Identifiers);
        Assert.Contains("\"DecisionDataPolicyJson\"", update.Sql[..update.Sql.IndexOf("WHERE", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.Equal(1, probe.Commits);
        var changed = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await owner.SaveSettingsV2Async(
            new(accepted.Snapshot.Expectation, accepted.Snapshot.Values, RootPolicy(2))));
        Assert.Equal(accepted.Snapshot.Values, changed.Snapshot.Values);
        Assert.Equal(accepted.Snapshot.Expectation.Head!.Id, changed.Snapshot.Expectation.Head!.Id);
        Assert.NotEqual(accepted.Snapshot.Expectation.Head.Revision, changed.Snapshot.Expectation.Head.Revision);
        Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged,
            Assert.IsType<WorkspaceSettingsSaveOutcome.Rejected>(await owner.SaveSettingsAsync(
                new(accepted.Snapshot.Expectation, accepted.Snapshot.Values))).Reason);
        Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged,
            Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Rejected>(await owner.SaveSettingsV2Async(
                new(accepted.Snapshot.Expectation, accepted.Snapshot.Values, RootPolicy()))).Reason);
        Assert.Equal(changed.Snapshot, await owner.ReadSettingsSnapshotV2Async());
    }

    [Fact]
    public async Task V1_preserves_noncanonical_complete_policy_bytes_and_legacy_creation_remains_absent() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotAsync();
        var policy = " \n" + DecisionDataPolicyCodec.Serialize(RootPolicy()) + "\n ";
        await WritePolicyTextAsync(application, policy);
        var accepted = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await owner.SaveSettingsAsync(Command(original, "V1 edit")));
        Assert.Equal(policy, await ReadPolicyTextAsync(application));
        var current = await owner.ReadSettingsSnapshotV2Async();
        Assert.Equal(accepted.Snapshot.Expectation, current.Expectation);
        Assert.Equal(accepted.Snapshot.Values, current.Values);
        Assert.Equal(RootPolicy(), current.DecisionDataPolicy);
        await using (var seed = await CanonicalAsync(application)) {
            await seed.Set<WorkspaceSettings>().Where(row => row.IsCurrent).ExecuteUpdateAsync(update => update.SetProperty(row => row.IsCurrent, false));
        }
        var empty = await owner.ReadSettingsSnapshotAsync();
        var created = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await owner.SaveSettingsAsync(Command(empty, "Legacy creation")));
        Assert.Null(await ReadPolicyTextAsync(application));
        Assert.Null((await owner.ReadSettingsSnapshotV2Async()).DecisionDataPolicy);
        Assert.NotEqual(accepted.Snapshot.Expectation.Head!.Id, created.Snapshot.Expectation.Head!.Id);
    }

    [Fact]
    public async Task Corrupt_policy_reads_refuse_v1_preserves_bytes_and_explicit_original_v2_replacement_repairs() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        foreach (var corrupt in new[] { "not-json-sensitive-marker", "null", "{}", "{\"schemaVersion\":99}" }) {
            await WritePolicyTextAsync(application, corrupt);
            var original = await owner.ReadSettingsSnapshotAsync();
            var failure = await Assert.ThrowsAsync<WorkspaceSettingsPolicyUnavailableException>(() => owner.ReadSettingsSnapshotV2Async());
            Assert.DoesNotContain(corrupt, failure.ToString(), StringComparison.Ordinal);
            var legacy = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await owner.SaveSettingsAsync(Command(original, "Preserved corrupt policy")));
            Assert.Equal(corrupt, await ReadPolicyTextAsync(application));
            await Assert.ThrowsAsync<WorkspaceSettingsPolicyUnavailableException>(() => owner.ReadSettingsSnapshotV2Async());
            Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged,
                Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Rejected>(await owner.SaveSettingsV2Async(
                    new(original.Expectation, original.Values, RootPolicy()))).Reason);
            Assert.Equal(corrupt, await ReadPolicyTextAsync(application));
            var repaired = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await owner.SaveSettingsV2Async(
                new(legacy.Snapshot.Expectation, legacy.Snapshot.Values, RootPolicy())));
            Assert.Equal(repaired.Snapshot, await owner.ReadSettingsSnapshotV2Async());
            Assert.Equal(DecisionDataPolicyCodec.Serialize(RootPolicy()), await ReadPolicyTextAsync(application));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task V2_common_original_races_have_one_atomic_winner_with_v1_or_v2(bool empty, bool competingV1) {
        await using var application = await TestApplication.CreateAsync();
        if (empty) {
            await using var seed = await CanonicalAsync(application);
            await seed.Set<WorkspaceSettings>().Where(row => row.IsCurrent).ExecuteUpdateAsync(update => update.SetProperty(row => row.IsCurrent, false));
        }
        await using var firstScope = application.Services.CreateAsyncScope();
        await using var secondScope = application.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var second = secondScope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await first.ReadSettingsSnapshotV2Async();
        Assert.Equal(original, await second.ReadSettingsSnapshotV2Async());
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var firstValues = NamedValues(original.Values, "First contender");
        var secondValues = NamedValues(original.Values, "Second contender");
        var a = first.SaveSettingsV2Async(new(original.Expectation, firstValues, RootPolicy()), deadline.Token);
        if (competingV1) {
            var b = second.SaveSettingsAsync(new(original.Expectation, secondValues), deadline.Token);
            await Task.WhenAll(a, b);
            var current = await first.ReadSettingsSnapshotV2Async(deadline.Token);
            if (await a is WorkspaceSettingsSaveOutcomeV2.Accepted accepted) {
                Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged, Assert.IsType<WorkspaceSettingsSaveOutcome.Rejected>(await b).Reason);
                Assert.Equal(accepted.Snapshot, current);
            } else {
                Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged, Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Rejected>(await a).Reason);
                var acceptedV1 = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await b);
                Assert.Equal(acceptedV1.Snapshot.Expectation, current.Expectation);
                Assert.Equal(secondValues, current.Values);
                Assert.Null(current.DecisionDataPolicy);
            }
        } else {
            var b = second.SaveSettingsV2Async(new(original.Expectation, secondValues, RootPolicy(2)), deadline.Token);
            var outcomes = await Task.WhenAll(a, b);
            var accepted = Assert.Single(outcomes.OfType<WorkspaceSettingsSaveOutcomeV2.Accepted>());
            Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged, Assert.Single(outcomes.OfType<WorkspaceSettingsSaveOutcomeV2.Rejected>()).Reason);
            Assert.Equal(accepted.Snapshot, await first.ReadSettingsSnapshotV2Async(deadline.Token));
            Assert.Equal(accepted.Snapshot.Values == firstValues ? RootPolicy() : RootPolicy(2), accepted.Snapshot.DecisionDataPolicy);
        }
        await using var verify = await CanonicalAsync(application);
        Assert.Single(await verify.Set<WorkspaceSettings>().Where(row => row.IsCurrent).ToArrayAsync(deadline.Token));
    }

    [Fact]
    public async Task V2_original_namespace_cannot_repair_or_write_after_reactivation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotAsync();
        const string corrupt = "retained-corrupt-policy";
        await WritePolicyTextAsync(application, corrupt);
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        await application.Services.GetRequiredService<DatasetActivationCoordinator>().ReactivateAsync(profile, original.Expectation.Namespace, Guid.NewGuid());
        Assert.Equal(WorkspaceSettingsRejectionReason.DatasetChanged,
            Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Rejected>(await owner.SaveSettingsV2Async(
                new(original.Expectation, NamedValues(original.Values, "Retired draft"), RootPolicy()))).Reason);
        Assert.Equal(corrupt, await ReadPolicyTextAsync(application));
        var current = await owner.ReadSettingsSnapshotAsync();
        Assert.Equal(original.Expectation.Head, current.Expectation.Head);
        Assert.Equal(original.Values, current.Values);
        Assert.NotEqual(original.Expectation.Namespace, current.Expectation.Namespace);
        await Assert.ThrowsAsync<WorkspaceSettingsPolicyUnavailableException>(() => owner.ReadSettingsSnapshotV2Async());
    }

    [Fact]
    public async Task V2_commit_and_precommit_faults_keep_exact_knowledge_without_replay_or_partial_policy() {
        foreach (var fault in new[] { Fault.BeforeSaveAndCleanup, Fault.LostCommitAcknowledgement, Fault.CleanupAfterAcknowledgement }) {
            var probe = new MutationProbe(fault);
            var diagnostics = new V2PublicationFault();
            await using var application = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
                probe.Configure(services);
                services.AddSingleton<IActivityStream>(diagnostics);
                services.AddSingleton<ILogger<WorkspaceSettingsWriter>>(diagnostics);
            } });
            await using var scope = application.Services.CreateAsyncScope();
            var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
            var original = await owner.ReadSettingsSnapshotV2Async();
            var values = NamedValues(original.Values, "Atomic fault candidate");
            probe.Armed = true;
            diagnostics.Armed = fault == Fault.CleanupAfterAcknowledgement;
            if (fault == Fault.BeforeSaveAndCleanup) {
                var failure = await Assert.ThrowsAsync<IOException>(() => owner.SaveSettingsV2Async(new(original.Expectation, values, RootPolicy())));
                Assert.Equal("Owned Workspace precommit failure.", failure.Message);
                Assert.Equal(0, probe.Commits);
                Assert.Equal(original, await owner.ReadSettingsSnapshotV2Async());
                Assert.Null(await ReadPolicyTextAsync(application));
            } else {
                var outcome = await owner.SaveSettingsV2Async(new(original.Expectation, values, RootPolicy()));
                var current = await owner.ReadSettingsSnapshotV2Async();
                Assert.Equal(1, probe.Commits);
                Assert.Equal(values, current.Values);
                Assert.Equal(RootPolicy(), current.DecisionDataPolicy);
                Assert.Equal(DecisionDataPolicyCodec.Serialize(RootPolicy()), await ReadPolicyTextAsync(application));
                if (fault == Fault.LostCommitAcknowledgement) {
                    Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Unconfirmed>(outcome);
                } else {
                    var accepted = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(outcome);
                    Assert.Equal(current, accepted.Snapshot);
                    Assert.Equal(WorkspaceSettingsSecondaryDiagnostic.CleanupPending, accepted.Diagnostic);
                    Assert.True(diagnostics.ActivityFailures > 0);
                    Assert.True(diagnostics.LoggingFailures > 0);
                }
                Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged,
                    Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Rejected>(await owner.SaveSettingsV2Async(new(original.Expectation, values, RootPolicy()))).Reason);
                Assert.Equal(1, probe.Commits);
            }
        }
    }

    [Fact]
    public async Task Policy_column_size_constraint_preserves_legacy_null_and_counts_utf8_bytes() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var original = await owner.ReadSettingsSnapshotAsync();
        var accepted = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await owner.SaveSettingsV2Async(new(original.Expectation, original.Values, RootPolicy())));
        var before = await ReadPolicyTextAsync(application);
        foreach (var invalid in new[] { "", new string('x', DecisionDataPolicyCodec.MaximumUtf8Bytes + 1), new string('é', 513) }) {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => WritePolicyTextAsync(application, invalid));
            Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            Assert.Equal("CK_Workspace_Settings_DecisionDataPolicyJson", failure.ConstraintName);
            Assert.Equal(before, await ReadPolicyTextAsync(application));
            Assert.Equal(accepted.Snapshot, await owner.ReadSettingsSnapshotV2Async());
        }
        await WritePolicyTextAsync(application, new string('é', 512));
        Assert.Equal(1024, System.Text.Encoding.UTF8.GetByteCount((await ReadPolicyTextAsync(application))!));
        await Assert.ThrowsAsync<WorkspaceSettingsPolicyUnavailableException>(() => owner.ReadSettingsSnapshotV2Async());
        await WritePolicyTextAsync(application, null);
        Assert.Null((await owner.ReadSettingsSnapshotV2Async()).DecisionDataPolicy);
    }

    [Fact]
    public async Task Configured_policy_and_revision_survive_bootstrap_restart_and_do_not_cross_profiles() {
        await using var environment = CanDoItAllTestEnvironment.Create("workspace-v2-owner");
        var profile = environment.CreatePostgreSqlProfile("configured");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        WorkspaceSettingsSnapshotV2 accepted;
        string? encoded;
        await using (var original = await TestApplication.CreateAsync(options)) {
            await using var scope = original.Services.CreateAsyncScope();
            var owner = scope.ServiceProvider.GetRequiredService<WorkspaceService>();
            var observed = await owner.ReadSettingsSnapshotV2Async();
            accepted = Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Accepted>(await owner.SaveSettingsV2Async(
                new(observed.Expectation, NamedValues(observed.Values, "Retained configured workspace"), RootPolicy()))).Snapshot;
            encoded = await ReadPolicyTextAsync(original);
        }
        await using (var restarted = await TestApplication.CreateAsync(options)) {
            await using var scope = restarted.Services.CreateAsyncScope();
            Assert.Equal(accepted, await scope.ServiceProvider.GetRequiredService<WorkspaceService>().ReadSettingsSnapshotV2Async());
            Assert.Equal(encoded, await ReadPolicyTextAsync(restarted));
        }
        await using var other = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherScope = other.Services.CreateAsyncScope();
        var otherOwner = otherScope.ServiceProvider.GetRequiredService<WorkspaceService>();
        var otherOriginal = await otherOwner.ReadSettingsSnapshotV2Async();
        Assert.Null(otherOriginal.DecisionDataPolicy);
        Assert.NotEqual(accepted.Expectation.Namespace, otherOriginal.Expectation.Namespace);
        Assert.Equal(WorkspaceSettingsRejectionReason.DatasetChanged,
            Assert.IsType<WorkspaceSettingsSaveOutcomeV2.Rejected>(await otherOwner.SaveSettingsV2Async(
                new(accepted.Expectation, accepted.Values, RootPolicy(2)))).Reason);
        Assert.Equal(otherOriginal, await otherOwner.ReadSettingsSnapshotV2Async());
    }

    private static DecisionDataPolicy RootPolicy(int minutes = 1)
        => DecisionDataPolicy.PlanningDefault(TimeSpan.FromMinutes(minutes), TimeSpan.FromMinutes(minutes + 1));

    private static WorkspaceSettingsValues NamedValues(WorkspaceSettingsValues original, string name)
        => new(name, original.DefaultProviderProfileId, original.DefaultPromptOutputFormat, original.CurrencyCode, original.CurrencyCultureName, original.Notes);

    private static async Task WritePolicyTextAsync(TestApplication application, string? json) {
        await using var context = await CanonicalAsync(application);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Workspace_Settings" SET "DecisionDataPolicyJson" = {json} WHERE "IsCurrent"
            """);
    }

    private static async Task<string?> ReadPolicyTextAsync(TestApplication application) {
        await using var context = await CanonicalAsync(application);
        return await context.Set<WorkspaceSettings>().Where(row => row.IsCurrent).Select(row => row.DecisionDataPolicyJson).SingleAsync();
    }

    private sealed class V2PublicationFault : IActivityStream, ILogger<WorkspaceSettingsWriter> {
        internal bool Armed;
        internal int ActivityFailures;
        internal int LoggingFailures;

        public Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            if (Armed) {
                Interlocked.Increment(ref ActivityFailures);
                throw new IOException("Workspace v2 activity publication failed.");
            }
            return Task.CompletedTask;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (Armed) {
                Interlocked.Increment(ref LoggingFailures);
                throw new IOException("Workspace v2 logging failed.");
            }
        }
    }
}
