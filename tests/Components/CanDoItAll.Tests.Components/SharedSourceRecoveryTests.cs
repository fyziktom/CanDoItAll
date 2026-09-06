using System.Reflection;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class SharedSourceRecoveryTests {
    [Fact]
    public async Task Source_create_has_stable_proposed_identity() {
        var service = CreateService(out var proxy);
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = Render(harness, proxy);
        await StartCreateAsync(cut);
        Assert.NotNull(proxy.Request!.Id);
        Assert.NotEqual(Guid.Empty, proxy.Request.Id);
        Assert.NotEmpty(cut.FindAll("[data-testid='shared-provider-source-unresolved']"));
        Assert.Equal(1, proxy.Writes);
        Assert.Contains("Add shared provider source", cut.Markup, StringComparison.Ordinal);
        Assert.False(cut.FindComponents<Button>().Single(button => button.Instance.Text == "Save source").Instance.IsBusy);
    }

    [Fact]
    public async Task Reopened_source_create_recovery_cannot_duplicate() {
        var service = CreateService(out var proxy);
        proxy.CommitBeforeFailure = true;
        var changes = new List<SharedProviderChange>();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = Render(harness, proxy, changes);
        await StartCreateAsync(cut);
        var candidate = proxy.Request!.Id;
        await cut.Find("[data-testid='shared-provider-connections-close']").ClickAsync();
        var reopened = Render(harness, proxy, changes);
        Assert.True(reopened.WaitForElement("[data-testid='shared-provider-source-add']").HasAttribute("disabled"));
        await reopened.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        Assert.Empty(reopened.FindAll("[data-testid='shared-provider-source-unresolved']"));
        Assert.False(reopened.Find("[data-testid='shared-provider-source-add']").HasAttribute("disabled"));
        Assert.Equal(candidate, Assert.Single(proxy.Sources).Source.Id);
        Assert.Equal(1, proxy.Writes);
        Assert.Single(changes, change => change.CommitState == SharedProviderCommitState.Committed);
    }

    [Fact]
    public async Task Failed_source_verification_remains_visibly_unresolved() {
        var service = CreateService(out var proxy);
        proxy.Verify = (_, _) => Task.FromException<SharedProviderSourceVerificationResult>(new IOException("Synthetic unavailable database."));
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = Render(harness, proxy);
        await StartCreateAsync(cut);
        await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        Assert.Contains("verification failed", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("[data-testid='shared-provider-source-unresolved']"));
        Assert.Equal(1, proxy.Writes);
        Assert.Empty(proxy.Sources);
    }

    [Fact]
    public async Task Classified_source_verification_clears_errors_and_unlocks() {
        var service = CreateService(out var proxy);
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = Render(harness, proxy);
        await StartCreateAsync(cut);
        var candidate = proxy.Request!.Id;
        await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        Assert.Equal(1, proxy.Writes);
        await cut.Find("[data-testid='shared-provider-source-retry-verified']").ClickAsync();
        Assert.Equal(2, proxy.Writes);
        Assert.Equal(candidate, proxy.Request.Id);
        Assert.Equal(candidate, Assert.Single(proxy.Sources).Source.Id);
        Assert.Empty(cut.FindAll("[data-testid='shared-provider-source-unresolved']"));
        Assert.DoesNotContain("unconfirmed", cut.Markup, StringComparison.Ordinal);
        Assert.False(cut.Find("[data-testid='shared-provider-source-add']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Source_verification_publishes_committed_change_once() {
        var service = CreateService(out var proxy);
        proxy.CommitBeforeFailure = true;
        var changes = new List<SharedProviderChange>();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = Render(harness, proxy, changes);
        await StartCreateAsync(cut);
        await cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        await cut.Find("[data-testid='shared-provider-source-refresh']").ClickAsync();
        await cut.Find("[data-testid='shared-provider-connections-close']").ClickAsync();
        var reopened = Render(harness, proxy, changes);
        reopened.WaitForElement("[data-testid='shared-provider-source-add']");
        Assert.Single(changes, change => change.CommitState == SharedProviderCommitState.Committed);
        Assert.Equal(1, proxy.Writes);
    }

    [Fact]
    public async Task Disposed_source_verification_emits_no_publication() {
        var service = CreateService(out var proxy);
        proxy.CommitBeforeFailure = true;
        var pending = new TaskCompletionSource<SharedProviderSourceVerificationResult>();
        CancellationToken received = default;
        SharedProviderSourceMutationAttempt? attempt = null;
        proxy.Verify = (request, token) => {
            attempt = request;
            received = token;
            return pending.Task;
        };
        var changes = new List<SharedProviderChange>();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var cut = Render(harness, proxy, changes);
        await StartCreateAsync(cut);
        var count = changes.Count;
        var verification = cut.Find("[data-testid='shared-provider-source-verify']").ClickAsync();
        cut.WaitForAssertion(() => Assert.NotNull(attempt));
        await cut.Find("[data-testid='shared-provider-connections-close']").ClickAsync();
        Assert.True(received.IsCancellationRequested);
        pending.SetResult(SharedProviderSourceVerification.Evaluate(attempt!, proxy.Sources));
        await verification;
        Assert.Equal(count, changes.Count);
        Assert.Equal(1, proxy.Writes);
    }

    [Fact]
    public async Task Shared_refresh_respects_unresolved_source_attempt() {
        var service = CreateService(out var proxy);
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton(service));
        var recovery = harness.Context.Services.GetRequiredService<SharedProviderRecovery>();
        recovery.BeginSource(new(Guid.NewGuid(), SharedProviderSourceMutationKind.Create));
        var cut = harness.Context.Render<SharedProviderRefreshButton>(p => p.Add(component => component.ProviderId, Guid.NewGuid()));
        Assert.True(cut.Find("[data-testid='shared-provider-refresh-capabilities']").HasAttribute("disabled"));
        Assert.Contains("Shared provider connections", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(0, proxy.Writes);
    }

    private static ISharedProviderManagementService CreateService(out SourceProxy proxy) {
        var service = DispatchProxy.Create<ISharedProviderManagementService, SourceProxy>();
        proxy = (SourceProxy)(object)service;
        return service;
    }

    private static IRenderedComponent<SharedProviderSourcesDialog> Render(ComponentTestHarness harness, SourceProxy proxy,
        List<SharedProviderChange>? changes = null) =>
        harness.Context.Render<SharedProviderSourcesDialog>(p => p
            .Add(component => component.Secrets, [new SecretListItem(proxy.SecretId, "Fixture credential", SecretKind.Token, "workspace", DateTimeOffset.UtcNow)])
            .Add(component => component.ProvidersChanged, (SharedProviderChangeDelivery delivery) => changes?.Add(delivery.Change)));

    private static async Task StartCreateAsync(IRenderedComponent<SharedProviderSourcesDialog> cut) {
        await cut.WaitForElement("[data-testid='shared-provider-source-add']").ClickAsync();
        cut.Find("[data-testid='shared-provider-source-name']").Change("Recovery source");
        cut.Find("[data-testid='shared-provider-source-uri']").Change("https://source.example.test/");
        await cut.Find("[data-testid='shared-provider-source-save']").ClickAsync();
    }

    public class SourceProxy : DispatchProxy {
        public Guid SecretId { get; } = Guid.NewGuid();
        public int Writes { get; private set; }
        public bool CommitBeforeFailure { get; set; }
        public SharedProviderSourceEditorRequest? Request { get; private set; }
        public IReadOnlyList<SharedProviderSourceManagementSnapshot> Sources { get; private set; } = [];
        public Func<SharedProviderSourceMutationAttempt, CancellationToken, Task<SharedProviderSourceVerificationResult>>? Verify { get; set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (method?.Name == nameof(ISharedProviderManagementService.ListSourcesAsync)) {
                return Task.FromResult(Sources);
            }
            if (method?.Name == nameof(ISharedProviderManagementService.VerifySourceAsync)) {
                var attempt = (SharedProviderSourceMutationAttempt)args![0]!;
                return Verify?.Invoke(attempt, (CancellationToken)args[^1]!) ??
                    Task.FromResult(SharedProviderSourceVerification.Evaluate(attempt, Sources));
            }
            if (method?.Name == nameof(ISharedProviderManagementService.SaveSourceAsync)) {
                Request = (SharedProviderSourceEditorRequest)args![0]!;
                Writes++;
                var token = Guid.NewGuid();
                if (CommitBeforeFailure || Writes > 1) {
                    Sources = [new(new(Request.Id!.Value, Request.Name, Request.BaseUri, Request.ApiTokenSecretId,
                        Request.IsEnabled, SharedProviderSourceNetworkPolicy.PublicOnly, SharedProviderSourceStatus.NeverSynchronized,
                        null, null, null, null, "", token), [])];
                }
                return Writes == 1
                    ? Task.FromException<SharedProviderSourceWriteResult>(new IOException("Synthetic lost source response."))
                    : Task.FromResult(new SharedProviderSourceWriteResult(Request.Id!.Value, token) {
                        Change = new(SharedProviderChangeKind.SourceConfiguration, [])
                    });
            }
            throw new InvalidOperationException("Unexpected source recovery call.");
        }
    }
}
