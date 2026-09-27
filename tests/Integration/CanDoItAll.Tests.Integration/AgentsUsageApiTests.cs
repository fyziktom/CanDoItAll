using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class AgentsUsageApiTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Endpoint_requires_both_dataset_permissions_before_reading_and_validates_strictly() {
        var agents = new Source(ProviderUsageWorkloadKind.Agent);
        var chats = new Source(ProviderUsageWorkloadKind.SimpleChat);
        await using var host = await HostAsync(agents, chats);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Client.GetAsync("/api/agents/usage")).StatusCode);
        SetToken(host, ApiAccessScopeNames.ReadAgents);
        foreach (var query in new[] { "", "?usageScope=both", "?usageScope=simple-chats" }) {
            Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/agents/usage" + query)).StatusCode);
        }
        Assert.Equal(0, agents.Reads);
        Assert.Equal(0, chats.Reads);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/api/agents/usage?usageScope=agents")).StatusCode);
        Assert.Equal(1, agents.Reads);
        Assert.Equal(0, chats.Reads);
        SetToken(host, ApiAccessScopeNames.ReadLlmChats);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.Client.GetAsync("/api/agents/usage?usageScope=simple-chats")).StatusCode);
        SetToken(host, ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.ReadLlmChats);
        foreach (var query in new[] { "usagePeriod=all", "usagePeriod=7D", "usagePeriod=", "usageScope=4", "usageScope=chats",
            "usagePeriod=7d&usagePeriod=1y", "usageScope=both&usageScope=both" }) {
            using var response = await host.Client.GetAsync("/api/agents/usage?" + query);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("agents.usage.invalid-query", await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(1, agents.Reads);
        Assert.Equal(0, chats.Reads);
    }

    [Fact]
    public async Task Defaults_periods_metadata_owner_parity_and_unavailable_sources_are_explicit() {
        var agents = new Source(ProviderUsageWorkloadKind.Agent);
        var chats = new Source(ProviderUsageWorkloadKind.SimpleChat);
        await using var host = await HostAsync(agents, chats);
        SetToken(host, ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.ReadLlmChats);
        var implicitDefault = await host.Client.GetFromJsonAsync<ProviderUsageSnapshot>("/api/agents/usage");
        var explicitDefault = await host.Client.GetFromJsonAsync<ProviderUsageSnapshot>("/api/agents/usage?usageScope=both&usagePeriod=7d");
        Assert.Equal(implicitDefault!.Query, explicitDefault!.Query);
        Assert.Equal(implicitDefault.GeneratedAtUtc, explicitDefault.GeneratedAtUtc);
        Assert.True(implicitDefault.IsComplete);
        Assert.Equal(0, implicitDefault.Totals.UsageObservationCount);
        foreach (var period in Enum.GetValues<ProviderUsagePeriod>()) {
            var actual = await host.Client.GetFromJsonAsync<ProviderUsageSnapshot>("/api/agents/usage?usageScope=both&usagePeriod=" + period.ToWireValue());
            await using var scope = host.App.Services.CreateAsyncScope();
            var owner = scope.ServiceProvider.GetRequiredService<ProviderUsageQueryService>();
            var expected = await owner.QueryWindowAsync(owner.Resolve(ProviderUsageWorkloadSelection.Both, period));
            Assert.Equal(expected.Query, actual!.Query);
            Assert.Equal(expected.Totals, actual.Totals);
            Assert.Equal(expected.GeneratedAtUtc, actual.GeneratedAtUtc);
            Assert.All(actual.Sources, source => Assert.Equal(Now, source.CoverageVerifiedAtUtc));
        }
        agents.State = ProviderUsageSourceState.Indexing;
        var partial = await host.Client.GetFromJsonAsync<ProviderUsageSnapshot>("/api/agents/usage");
        Assert.False(partial!.IsComplete);
        Assert.Contains(partial.Sources, source => source.State == ProviderUsageSourceState.Indexing);
        chats.State = ProviderUsageSourceState.Failed;
        var unavailable = await host.Client.GetFromJsonAsync<ProviderUsageSnapshot>("/api/agents/usage?usageScope=simple-chats");
        Assert.False(unavailable!.IsComplete);
        Assert.Equal(ProviderUsageSourceState.Failed, Assert.Single(unavailable.Sources).State);
    }

    [Fact]
    public async Task Request_abortion_reaches_bounded_source() {
        var agents = new Source(ProviderUsageWorkloadKind.Agent) { Hold = true };
        await using var host = await HostAsync(agents, new(ProviderUsageWorkloadKind.SimpleChat));
        SetToken(host, ApiAccessScopeNames.ReadAgents);
        using var cancellation = new CancellationTokenSource();
        var pending = host.Client.GetAsync("/api/agents/usage?usageScope=agents", cancellation.Token);
        await agents.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await agents.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Real_sources_keep_usage_in_the_resolved_profile_workspace() {
        await using var first = await ApiTestHost.CreateAsync(jwtEnabled: true);
        await using var second = await ApiTestHost.CreateAsync(jwtEnabled: true);
        await SeedAsync(first, 100);
        await SeedAsync(second, 200);
        foreach (var (host, tokens) in new[] { (first, 100L), (second, 200L), (first, 100L) }) {
            SetToken(host, ApiAccessScopeNames.ReadAgents);
            var result = await host.Client.GetFromJsonAsync<ProviderUsageSnapshot>("/api/agents/usage?usageScope=agents");
            Assert.True(result!.IsComplete);
            Assert.Equal(1, result.Totals.UsageObservationCount);
            Assert.Equal(tokens, result.Totals.Tokens.TotalTokens);
        }

        static async Task SeedAsync(ApiTestHost host, int tokens) {
            await using var services = host.App.Services.CreateAsyncScope();
            var store = services.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>();
            var agent = (await store.LoadCatalogAsync()).Agents.First();
            await store.SaveExecutionAsync(SandboxWorkspaceExecutionState.Empty with {
                ProviderUsageObservations = [new(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1), "Profile fixture", ProviderKind.OpenAi,
                    "model", ProviderTransportKind.Responses, ProviderUsageSourcePhases.AgentRuntime, ProviderUsageObservationStatus.Observed,
                    tokens, 0, 0, 0, tokens, 0) { AgentId = agent.Id }]
            });
            var profile = services.ServiceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>().ResolveCurrentProfile();
            var maintenance = new FileProviderUsageIndexMaintenance(host.ActiveProfile.WorkspaceRootPath,
                WorkspaceScopeDescriptor.Organization(profile.Profile.Id.ToString("N")));
            Assert.True((await maintenance.ProcessAsync()).Complete);
        }
    }

    private static Task<ApiTestHost> HostAsync(Source agents, Source chats) => ApiTestHost.CreateAsync(jwtEnabled: true,
        configureServices: services => {
            services.RemoveAll<IProviderUsageProjectionSource>();
            services.AddSingleton<IProviderUsageProjectionSource>(agents);
            services.AddSingleton<IProviderUsageProjectionSource>(chats);
            services.AddSingleton<TimeProvider>(new FixedClock());
        });

    private static void SetToken(ApiTestHost host, params string[] scopes) {
        var token = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() { Subject = "usage-window-fixture", Scopes = scopes.ToList() });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(token.TokenType, token.Token);
    }

    private sealed class FixedClock : TimeProvider {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Source(ProviderUsageWorkloadKind kind) : IBoundedProviderUsageProjectionSource {
        public string SourceName => kind.ToString();
        public ProviderUsageWorkloadKind WorkloadKind => kind;
        public ProviderUsageSourceState State { get; set; } = ProviderUsageSourceState.Complete;
        public int Reads { get; private set; }
        public bool Hold { get; init; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask<ProviderUsageSourceResult> ReadAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unbounded API read.");
        public async ValueTask<ProviderUsageSourceResult> ReadWindowAsync(ProviderUsageWindow window, CancellationToken cancellationToken = default) {
            Reads++;
            Started.TrySetResult();
            using var registration = cancellationToken.Register(() => Cancelled.TrySetResult());
            if (Hold) {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return new(SourceName, kind, State, [], DateTimeOffset.UnixEpoch,
                State == ProviderUsageSourceState.Complete ? null : new("fixture-unavailable", "Fixture unavailable")) {
                Window = window, CoverageVerifiedAtUtc = State == ProviderUsageSourceState.Complete ? Now : null
            };
        }
    }
}
