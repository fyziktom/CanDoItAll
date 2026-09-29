using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using CanDoItAll.Memory.Mock;
using CanDoItAll.Memory.UI;
using CanDoItAll.Modules.Memory.Pages;
using CanDoItAll.Modules.Memory.Services;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.Memory;

public sealed class MemoryWorkspaceOwnerTests {
    [Fact]
    public async Task PostgreSql_saved_destination_survives_failed_followup_without_renaming_original_or_replaying() {
        await using var app = await CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = services.GetRequiredService<IMemoryProviderManagementUiService>();
        await owner.SaveProviderAsync(Editor("provider.original"));
        using var workspace = new MemoryProvidersPageController(owner);
        await workspace.RefreshAsync();
        var store = services.GetRequiredService<ControlledProfiles>();
        workspace.Editor.InstanceId = "provider.destination";
        workspace.Editor.DisplayName = "Captured destination";
        var held = store.HoldWrite();
        var save = workspace.SaveProviderAsync();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        workspace.Editor.InstanceId = "provider.later";
        workspace.Editor.DisplayName = "Newer draft";
        store.FailList = true;
        held.SetResult();
        await save.WaitAsync(TimeSpan.FromSeconds(10));
        var receipt = Assert.Single(workspace.Submissions);
        Assert.Equal(MemoryEffectState.ReadbackWarning, receipt.State);
        Assert.Equal("provider.destination", Assert.Single(receipt.SavedProviders).Value);
        Assert.NotNull(await store.Inner.GetAsync(MemoryProviderInstanceId.Parse("provider.original")));
        Assert.Equal("Captured destination", (await store.Inner.GetAsync(MemoryProviderInstanceId.Parse("provider.destination")))!.DisplayName);
        await workspace.RefreshAsync();
        Assert.Equal(2, store.Writes);
        Assert.Equal("provider.later", workspace.Editor.InstanceId);
        Assert.Equal("Newer draft", workspace.Editor.DisplayName);
    }

    [Fact]
    public async Task Real_query_captures_input_before_guard_await_and_refresh_reads_only() {
        await using var app = await CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = services.GetRequiredService<IMemoryProviderManagementUiService>();
        await owner.SaveProviderAsync(Editor("provider.query"));
        var store = services.GetRequiredService<ControlledProfiles>();
        var editor = new MemoryQueryEditorModel { Query = "captured query", SourceModule = "module-original", SourceRecordId = "record-original", Citation = "citation-original" };
        var held = store.HoldGet();
        var query = owner.RunQueryAsync("provider.query", editor);
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        editor.Query = "changed query";
        editor.UseAsyncQuery = true;
        editor.SourceModule = "changed module";
        held.SetResult();
        var result = await query.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(MemoryProviderActionStatus.Completed, result.Status);
        Assert.Contains("captured query", result.ContextPack!.Summary, StringComparison.Ordinal);
        Assert.True(result.DriverDispatchAttempted);
        Assert.Equal(MemoryCapabilityIds.ContextQuerySync, result.Operation!.RequestedCapability);
        var snapshot = await owner.GetSnapshotAsync("provider.query");
        Assert.Equal(result.Operation.OperationId, Assert.Single(snapshot.Operations).OperationId);
        var missing = await owner.GetSnapshotAsync("provider.QUERY");
        Assert.Null(missing.SelectedProvider);
        Assert.NotEmpty(missing.Providers);
    }

    [Fact]
    public async Task Empty_query_is_refused_before_dispatch_and_a_corrected_query_can_run() {
        await using var app = await CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = services.GetRequiredService<IMemoryProviderManagementUiService>();
        await owner.SaveProviderAsync(Editor("provider.validation"));
        using var workspace = new MemoryProvidersPageController(owner);
        await workspace.RefreshAsync();
        workspace.QueryEditor.Query = " ";
        await workspace.RunQueryAsync();
        Assert.Equal(MemoryEffectState.Refused, Assert.Single(workspace.Submissions).State);
        Assert.Empty(await services.GetRequiredService<IMemoryOperationLedgerStore>().ListByProviderAsync(MemoryProviderInstanceId.Parse("provider.validation")));
        workspace.QueryEditor.Query = "corrected query";
        await workspace.RunQueryAsync();
        Assert.Equal(MemoryEffectState.Observed, workspace.Submissions[1].State);
        Assert.Equal(MemoryProviderActionStatus.Completed, workspace.QueryResult!.Status);
    }

    [Fact]
    public async Task Demo_second_write_fault_reports_confirmed_first_profile_and_read_only_review() {
        await using var app = await CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<ControlledProfiles>();
        store.FailSecondDemo = true;
        using var workspace = new MemoryProvidersPageController(services.GetRequiredService<IMemoryProviderManagementUiService>());
        await workspace.RefreshAsync();
        await workspace.AddDemoProvidersAsync();
        var receipt = Assert.Single(workspace.Submissions);
        Assert.Equal(MemoryEffectState.Unknown, receipt.State);
        Assert.Equal(MemoryDemoProviderIds.Business, Assert.Single(receipt.SavedProviders).Value);
        Assert.NotNull(await store.Inner.GetAsync(MemoryProviderInstanceId.Parse(MemoryDemoProviderIds.Business)));
        await workspace.AddDemoProvidersAsync();
        await workspace.ReviewAsync(receipt);
        Assert.Equal(1, store.Writes);
        Assert.Equal(MemoryEffectState.Reviewed, receipt.State);
        store.FailSecondDemo = false;
        await workspace.AddDemoProvidersAsync();
        Assert.Equal(2, store.Writes);
    }

    [Fact]
    public async Task Profile_generation_change_during_guard_refuses_dispatch_and_snapshot_publication() {
        await using var app = await CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = services.GetRequiredService<IMemoryProviderManagementUiService>();
        await owner.SaveProviderAsync(Editor("provider.origin"));
        var store = services.GetRequiredService<ControlledProfiles>();
        var held = store.HoldGet();
        var query = owner.RunQueryAsync("provider.origin", new());
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        services.GetRequiredService<MutableOrigin>().Revision++;
        held.SetResult();
        await Assert.ThrowsAsync<MemoryActionRefusedException>(() => query);
        Assert.Empty(await services.GetRequiredService<IMemoryOperationLedgerStore>().ListByProviderAsync(MemoryProviderInstanceId.Parse("provider.origin")));
        await Assert.ThrowsAsync<MemoryActionRefusedException>(() => owner.GetSnapshotAsync("provider.origin"));
    }

    [Theory]
    [InlineData(MemoryProviderDriverKind.Mock)]
    [InlineData(MemoryProviderDriverKind.Http)]
    [InlineData(MemoryProviderDriverKind.NativeRemote)]
    [InlineData(MemoryProviderDriverKind.Mcp)]
    public async Task Imported_unsupported_claims_are_refused_before_any_dispatch(MemoryProviderDriverKind driver) {
        await using var app = await CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var profile = services.GetRequiredService<MemoryProviderProfileEditorMapper>().ToProfile(Editor("provider.claims"));
        profile = profile with { DriverKind = driver, Manifest = profile.Manifest with { Capabilities = [
            new(MemoryCapabilityIds.IngestionSnapshot, "1", true), new(MemoryCapabilityIds.FeedbackImmediate, "1", true), new(MemoryCapabilityIds.EventsProviderPush, "1", true)] } };
        await services.GetRequiredService<ControlledProfiles>().Inner.UpsertAsync(profile, DateTimeOffset.UtcNow);
        var owner = services.GetRequiredService<IMemoryProviderManagementUiService>();
        await Assert.ThrowsAsync<MemoryActionRefusedException>(() => owner.EnqueueManualIngestionAsync(profile.InstanceId.Value, new()));
        await Assert.ThrowsAsync<MemoryActionRefusedException>(() => owner.SubmitFeedbackAsync(profile.InstanceId.Value, new()));
        await Assert.ThrowsAsync<MemoryActionRefusedException>(() => owner.AcknowledgeEventAsync(profile.InstanceId.Value, Guid.NewGuid().ToString("D"), true));
        await Assert.ThrowsAsync<MemoryActionRefusedException>(() => owner.CancelOperationAsync(Guid.NewGuid().ToString("D")));
        Assert.Empty(await services.GetRequiredService<IMemoryOperationLedgerStore>().ListByProviderAsync(profile.InstanceId));
        Assert.Empty(await services.GetRequiredService<IMemoryFeedbackLedgerStore>().ListByProviderAsync(profile.InstanceId));
        Assert.Empty(await services.GetRequiredService<IMemorySourceRequestLedgerStore>().ListByProviderAsync(profile.InstanceId));
    }

    [Fact]
    public async Task Failed_feedback_read_does_not_hide_other_real_ledger_regions_or_replace_draft() {
        await using var app = await CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var owner = services.GetRequiredService<IMemoryProviderManagementUiService>();
        await owner.SaveProviderAsync(Editor("provider.partial"));
        await owner.RunQueryAsync("provider.partial", new());
        using var workspace = new MemoryProvidersPageController(owner);
        await workspace.RefreshAsync();
        workspace.Editor.DisplayName = "dirty";
        services.GetRequiredService<ControlledFeedback>().FailRead = true;
        await workspace.RefreshAsync();
        Assert.Single(workspace.Snapshot!.Operations);
        Assert.Contains(MemoryReadRegion.Feedback, workspace.Snapshot.FailedRegions);
        Assert.Equal("dirty", workspace.Editor.DisplayName);
        Assert.DoesNotContain("PRIVATE-PROVIDER-ERROR", workspace.ErrorMessage!, StringComparison.Ordinal);
    }

    [Fact]
    public void Display_status_projection_is_exhaustive_and_preserves_public_numeric_values() {
        var map = typeof(MemoryProviderManagementUiService).Assembly.GetType("CanDoItAll.Modules.Memory.Services.MemoryProviderUiRecordMapper", throwOnError: true)!.GetMethod("ToUiStatus")!;
        foreach (var status in Enum.GetValues<MemoryOperationHandlerStatus>()) {
            var projected = Assert.IsType<MemoryProviderActionStatus>(map.Invoke(null, [status]));
            Assert.Equal((int)status, (int)projected);
            Assert.Equal(status.ToString(), projected.ToString());
        }
        Assert.IsType<ArgumentOutOfRangeException>(Assert.Throws<System.Reflection.TargetInvocationException>(() => map.Invoke(null, [(MemoryOperationHandlerStatus)int.MaxValue])).InnerException);
    }

    private static MemoryProviderProfileEditorModel Editor(string id) => new() { InstanceId = id, DisplayName = id, HealthState = MemoryProviderHealthState.Healthy };

    private static Task<TestApplication> CreateAsync() => TestApplication.CreateAsync(new TestHarnessOptions {
        ConfigureServices = services => {
            services.AddDeterministicMockMemoryProviderDriver();
            var origin = services.Single(d => d.ServiceType == typeof(ICanonicalRuntimeDatabase));
            services.Remove(origin);
            services.AddSingleton<MutableOrigin>(provider => new((ICanonicalRuntimeDatabase)(origin.ImplementationInstance ?? origin.ImplementationFactory?.Invoke(provider) ?? ActivatorUtilities.CreateInstance(provider, origin.ImplementationType!))));
            services.AddSingleton<ICanonicalRuntimeDatabase>(provider => provider.GetRequiredService<MutableOrigin>());
            services.AddScoped<EfMemoryProviderProfileStore>();
            services.AddScoped<ControlledProfiles>();
            services.Replace(ServiceDescriptor.Scoped<IMemoryProviderProfileStore>(provider => provider.GetRequiredService<ControlledProfiles>()));
            services.AddScoped<EfMemoryFeedbackLedgerStore>();
            services.AddScoped<ControlledFeedback>();
            services.Replace(ServiceDescriptor.Scoped<IMemoryFeedbackLedgerStore>(provider => provider.GetRequiredService<ControlledFeedback>()));
        }
    });

    private sealed class MutableOrigin(ICanonicalRuntimeDatabase inner) : ICanonicalRuntimeDatabase {
        public long Revision { get; set; }
        public ResolvedDatabaseProfile Profile => inner.Profile;
        public long Generation => inner.Generation + Revision;
    }

    private sealed class ControlledProfiles(EfMemoryProviderProfileStore inner) : IMemoryProviderProfileStore {
        public IMemoryProviderProfileStore Inner => inner;
        public TaskCompletionSource Entered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource? get;
        private TaskCompletionSource? write;
        public bool FailList { get; set; }
        public bool FailSecondDemo { get; set; }
        public int Writes { get; private set; }
        public TaskCompletionSource HoldWrite() => write = Reset();
        public TaskCompletionSource HoldGet() => get = Reset();
        private TaskCompletionSource Reset() {
            Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public async Task UpsertAsync(MemoryProviderProfile profile, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken = default) {
            if (write is { } held) {
                write = null;
                Entered.SetResult();
                await held.Task.WaitAsync(cancellationToken);
            }
            if (FailSecondDemo && profile.InstanceId.Value == MemoryDemoProviderIds.Programming) {
                throw new InvalidOperationException("Synthetic second demo write fault.");
            }
            await inner.UpsertAsync(profile, updatedAtUtc, cancellationToken);
            Writes++;
        }
        public async Task<MemoryProviderProfile?> GetAsync(MemoryProviderInstanceId providerId, CancellationToken cancellationToken = default) {
            if (get is { } held) {
                get = null;
                Entered.SetResult();
                await held.Task.WaitAsync(cancellationToken);
            }
            return await inner.GetAsync(providerId, cancellationToken);
        }
        public Task<IReadOnlyList<MemoryProviderProfile>> ListAsync(CancellationToken cancellationToken = default) {
            if (FailList) {
                FailList = false;
                throw new InvalidOperationException("Synthetic followup read fault.");
            }
            return inner.ListAsync(cancellationToken);
        }
    }

    private sealed class ControlledFeedback(EfMemoryFeedbackLedgerStore inner) : IMemoryFeedbackLedgerStore {
        public bool FailRead { get; set; }
        public Task SubmitAsync(MemoryFeedbackRecord record, CancellationToken cancellationToken = default) => inner.SubmitAsync(record, cancellationToken);
        public Task<IReadOnlyList<MemoryFeedbackRecord>> ListDueForDeliveryAsync(DateTimeOffset nowUtc, TimeSpan staleAfter, int take, CancellationToken cancellationToken = default) => inner.ListDueForDeliveryAsync(nowUtc, staleAfter, take, cancellationToken);
        public Task<IReadOnlyList<MemoryFeedbackRecord>> ListByProviderAsync(MemoryProviderInstanceId providerInstanceId, CancellationToken cancellationToken = default) => FailRead ? throw new InvalidOperationException("PRIVATE-PROVIDER-ERROR") : inner.ListByProviderAsync(providerInstanceId, cancellationToken);
        public Task<MemoryFeedbackRecord> TransitionAsync(MemoryFeedbackRecordId feedbackRecordId, MemoryLedgerStatus nextStatus, DateTimeOffset transitionedAtUtc, string reason, CancellationToken cancellationToken = default) => inner.TransitionAsync(feedbackRecordId, nextStatus, transitionedAtUtc, reason, cancellationToken);
        public Task<MemoryFeedbackRecord> DeferAsync(MemoryFeedbackRecordId feedbackRecordId, DateTimeOffset deferredAtUtc, bool incrementRetry, CancellationToken cancellationToken = default) => inner.DeferAsync(feedbackRecordId, deferredAtUtc, incrementRetry, cancellationToken);
    }
}
