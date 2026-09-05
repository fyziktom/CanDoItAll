using System.Net;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Editor = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using Entity = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;
using Kind = CanDoItAll.AgentFramework.Models.ProviderKind;

namespace CanDoItAll.Tests.Integration.SharedProviders;

public sealed class ProviderApiOutcomeIntegrationTests(ProviderApiOutcomeFixture fixture)
    : IClassFixture<ProviderApiOutcomeFixture> {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Api_known_commit_preserves_success_payload_and_declares_pending_reconciliation(bool delete) {
        var model = NewEditor();
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = delete ? await registry.SaveProviderAsync(model) : Guid.Empty;
        fixture.Observer.Fail = true;
        HttpResponseMessage response;
        try {
            response = delete
                ? await fixture.Host.Client.DeleteAsync($"/api/agents/providers/{id:D}")
                : await fixture.Host.Client.PostAsJsonAsync("/api/agents/providers", model);
        } finally {
            fixture.Observer.Fail = false;
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("committed-reconciliation-pending", Assert.Single(response.Headers.GetValues("CDA-Provider-Outcome")));
        if (!delete) {
            id = await response.Content.ReadFromJsonAsync<Guid>();
            Assert.NotEqual(Guid.Empty, id);
        } else {
            Assert.Contains("true", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        Assert.Equal(delete ? 0 : 1, await db.Set<Entity>().CountAsync(x => x.Name == model.Name));
        Assert.Equal(delete, await registry.GetProviderAsync(id) is null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publication_secondary_failure_or_owner_cancellation_preserves_permanent_committed_identity(bool cancelOwner) {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        using var owner = new CancellationTokenSource();
        var observer = new PublicationObserver(owner, cancelOwner);
        var application = ActivatorUtilities.CreateInstance<SharedProviderPublicationApplicationService>(scope.ServiceProvider,
            (IEnumerable<ISharedProviderPublicationCommitObserver>)[observer]);
        var management = ActivatorUtilities.CreateInstance<SharedProviderManagementService>(scope.ServiceProvider, application);
        if (cancelOwner) {
            var committed = await Assert.ThrowsAsync<SharedProviderCommittedException>(() =>
                management.SetPublicationAsync(id, SharedProviderPublicationAction.Publish, null, owner.Token));
            Assert.Equal([id], committed.Change.AffectedProviderProfileIds);
            Assert.Equal(SharedProviderCommitState.Committed, committed.Change.CommitState);
        } else {
            var result = await management.SetPublicationAsync(id, SharedProviderPublicationAction.Publish, null, owner.Token);
            Assert.True(result.Publication!.IsPublished);
            Assert.NotNull(result.Change!.Warning);
        }
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var row = Assert.Single(await db.Set<ProviderSharePublication>().Where(x => x.ProviderProfileId == id).ToListAsync());
        Assert.True(row.IsPublished);
        await Assert.ThrowsAsync<SharedProviderProfileDeletionBlockedException>(() => registry.DeleteProviderAsync(id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Local_health_diagnostic_persists_the_health_update_and_new_revision(bool healthy) {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var before = await registry.GetProviderEditorAsync(id);
        var diagnostics = new LocalDiagnostics(healthy);
        var runtime = ActivatorUtilities.CreateInstance<ProviderRuntimeAdministrationService>(scope.ServiceProvider, diagnostics);
        var result = await runtime.TestProviderAsync(id);
        Assert.Equal(healthy, result.Success);
        Assert.Equal(1, diagnostics.Calls);
        var after = await registry.GetProviderEditorAsync(id);
        Assert.NotEqual(before.ExpectedConcurrencyToken, after.ExpectedConcurrencyToken);
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var profile = await db.Set<Entity>().AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.NotNull(profile.LastHealthCheckAtUtc);
        Assert.Equal(healthy ? SharedProviderHealthState.Available : SharedProviderHealthState.Unavailable,
            SharedProviderPublicHealthMapper.Map(profile));
    }

    [Fact]
    public async Task Local_health_failure_before_persistence_is_retryable_and_does_not_change_revision() {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var before = await registry.GetProviderEditorAsync(id);
        var diagnostics = new LocalDiagnostics(true, fail: true);
        var runtime = ActivatorUtilities.CreateInstance<ProviderRuntimeAdministrationService>(scope.ServiceProvider, diagnostics);
        var commands = ActivatorUtilities.CreateInstance<ProviderEditorCommands>(scope.ServiceProvider, runtime);
        var outcome = await commands.CheckHealthAsync(id, sourceManaged: false, CancellationToken.None);
        Assert.Null(outcome.Persistence);
        Assert.False(outcome.Health!.Success);
        Assert.DoesNotContain("fixture-private-diagnostic-detail", outcome.Health.Summary, StringComparison.Ordinal);
        Assert.Equal(before.ExpectedConcurrencyToken, (await registry.GetProviderEditorAsync(id)).ExpectedConcurrencyToken);
        Assert.Equal(1, diagnostics.Calls);
    }

    [Fact]
    public async Task Api_local_health_diagnostic_failure_is_sanitized_and_does_not_persist() {
        await using var scope = fixture.Host.App.Services.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IProviderProfileRegistry>();
        var id = await registry.SaveProviderAsync(NewEditor());
        var before = await registry.GetProviderEditorAsync(id);
        fixture.Diagnostics.Fail = true;
        HttpResponseMessage response;
        try {
            response = await fixture.Host.Client.PostAsync($"/api/agents/providers/{id:D}/test", null);
        } finally {
            fixture.Diagnostics.Fail = false;
        }
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("agents.provider-diagnostic-unavailable", body, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-private-diagnostic-detail", body, StringComparison.Ordinal);
        Assert.Equal(before.ExpectedConcurrencyToken, (await registry.GetProviderEditorAsync(id)).ExpectedConcurrencyToken);
    }

    private static Editor NewEditor() => new() {
        Name = $"Provider outcome proof {Guid.NewGuid():N}",
        Kind = Kind.Ollama,
        Transport = ProviderTransportKind.ChatCompletions,
        BaseUrl = "http://127.0.0.1:11434",
        DefaultModel = "seam-model",
        SuggestedModels = ["seam-model"],
        IsEnabled = true
    };

    private sealed class PublicationObserver(CancellationTokenSource owner, bool cancel) : ISharedProviderPublicationCommitObserver {
        public Task PublicationChangedAsync(Guid providerProfileId, CancellationToken token = default) {
            if (cancel) {
                owner.Cancel();
                return Task.FromException(new OperationCanceledException(owner.Token));
            }
            return Task.FromException(new IOException("Synthetic post-commit publication observer failure."));
        }
    }

    internal sealed class LocalDiagnostics(bool healthy, bool fail = false) : IProviderDiagnosticsService {
        public bool Fail { get; set; } = fail;
        public int Calls { get; private set; }
        public Task<ProviderHealthResult> TestProviderAsync(CanDoItAll.AgentFramework.Models.ProviderProfile provider, CancellationToken token = default) {
            Calls++;
            return Fail
                ? Task.FromException<ProviderHealthResult>(new IOException("fixture-private-diagnostic-detail"))
                : Task.FromResult(new ProviderHealthResult(healthy, "Diagnostic details", ["seam-model"]));
        }
        public Task<ProviderTestChatResult> RunProviderTestChatAsync(CanDoItAll.AgentFramework.Models.ProviderProfile provider,
            ProviderTestChatRequest request, CancellationToken token = default) => throw new NotSupportedException();
        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(CanDoItAll.AgentFramework.Models.ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request, CancellationToken token = default) => throw new NotSupportedException();
    }
}

public sealed class ProviderApiOutcomeFixture : IAsyncLifetime {
    internal ApiTestHost Host { get; private set; } = null!;
    public FailingObserver Observer { get; } = new();
    internal ProviderApiOutcomeIntegrationTests.LocalDiagnostics Diagnostics { get; } = new(true);
    public async Task InitializeAsync() {
        Host = await ApiTestHost.CreateAsync(jwtEnabled: false,
            configureServices: services => {
                services.AddSingleton<IProviderProfileCommitObserver>(Observer);
                services.AddSingleton<IProviderDiagnosticsService>(Diagnostics);
            });
    }
    public Task DisposeAsync() => Host.DisposeAsync().AsTask();

    public sealed class FailingObserver : IProviderProfileCommitObserver {
        public bool Fail { get; set; }
        public Task ProviderSavedAsync(Guid id, CancellationToken token = default) =>
            Fail ? Task.FromException(new IOException("Synthetic post-commit observer failure.")) : Task.CompletedTask;
        public Task ProviderDeletedAsync(Guid id, CancellationToken token = default) => ProviderSavedAsync(id, token);
    }
}
