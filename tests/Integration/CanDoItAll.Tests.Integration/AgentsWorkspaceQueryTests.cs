using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentsWorkspaceQueryTests {
    [Fact]
    public async Task Real_registration_reads_overview_and_bound_resources() {
        await using var host = await AgentUiAdapterTestHost.CreateAsync();
        await using var scope = host.App.Services.CreateAsyncScope();
        var query = Assert.IsType<AgentsWorkspaceQuery>(scope.ServiceProvider.GetRequiredService<IAgentsWorkspaceQuery>());
        Assert.IsType<BoundAgentResourceQuery>(scope.ServiceProvider.GetRequiredService<IBoundAgentResourceQuery>());
        var header = await query.ReadHeaderAsync();
        var overview = await query.ReadOverviewAsync();
        var usage = await query.ReadUsageAsync(ProviderUsageWorkloadSelection.Both);
        var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var expected = await workspace.GetAgentOverviewAsync();
        Assert.Equal(expected.Totals, overview.Totals);
        Assert.Equal(ProviderUsageWorkloadSelection.Both, usage.Selection);
        Assert.Equal(HrAgentIdentity.AgentId, header.HrAgent?.Id);
        Assert.Contains(HrAgentIdentity.AgentId.ToString("D"), header.AvatarImageUrls.Keys);
        Assert.Equal(await scope.ServiceProvider.GetRequiredService<IBoundAgentResourceQuery>().CountAsync(), header.BoundResourceCount);
    }

    [Fact]
    public async Task Registered_header_preserves_real_hr_and_overview_when_bound_read_fails() {
        await using var host = await CreateDatabaseHost(services => services.AddSingleton<IBoundAgentResourceQuery>(new FailingBoundRead()));
        await using var scope = host.App.Services.CreateAsyncScope();
        var query = Assert.IsType<AgentsWorkspaceQuery>(scope.ServiceProvider.GetRequiredService<IAgentsWorkspaceQuery>());
        var header = await query.ReadHeaderAsync();
        var overview = await query.ReadOverviewAsync();
        Assert.Equal(HrAgentIdentity.AgentId, header.HrAgent?.Id);
        Assert.Contains(HrAgentIdentity.AgentId.ToString("D"), header.AvatarImageUrls.Keys);
        Assert.Null(header.BoundResourceCount);
        Assert.Equal(AgentsHeaderFailure.BoundResources, header.Failures);
        var expected = await scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentOverviewAsync();
        Assert.Equal(expected.Totals, overview.Totals);
    }

    [Fact]
    public async Task Registered_header_preserves_real_bound_count_when_agent_catalog_fails() {
        await using var host = await CreateDatabaseHost(services => DecorateWorkspace(services, failCatalog: true));
        await using var scope = host.App.Services.CreateAsyncScope();
        var query = scope.ServiceProvider.GetRequiredService<IAgentsWorkspaceQuery>();
        var header = await query.ReadHeaderAsync();
        var bound = Assert.IsType<BoundAgentResourceQuery>(scope.ServiceProvider.GetRequiredService<IBoundAgentResourceQuery>());
        Assert.Equal(await bound.CountAsync(), header.BoundResourceCount);
        Assert.Null(header.HrAgent);
        Assert.Equal(AgentsHeaderFailure.HrAgent | AgentsHeaderFailure.Avatars, header.Failures);
        var expected = await scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentOverviewAsync();
        Assert.Equal(expected.Totals, (await query.ReadOverviewAsync()).Totals);
    }

    [Fact]
    public async Task Registered_usage_and_header_do_not_require_successful_overview() {
        await using var host = await CreateDatabaseHost(services => DecorateWorkspace(services, failCatalog: false));
        await using var scope = host.App.Services.CreateAsyncScope();
        var query = scope.ServiceProvider.GetRequiredService<IAgentsWorkspaceQuery>();
        await Assert.ThrowsAsync<IOException>(() => query.ReadOverviewAsync());
        var header = await query.ReadHeaderAsync();
        var usage = await query.ReadUsageAsync(ProviderUsageWorkloadSelection.Both);
        Assert.Equal(HrAgentIdentity.AgentId, header.HrAgent?.Id);
        Assert.Equal(AgentsHeaderFailure.None, header.Failures);
        Assert.Equal(ProviderUsageWorkloadSelection.Both, usage.Selection);
        Assert.All(usage.Sources, source => Assert.NotEqual(ProviderUsageSourceState.Failed, source.State));
    }

    private static Task<ApiTestHost> CreateDatabaseHost(Action<IServiceCollection> configure)
        => ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: false, configureServices: services => {
            services.AddRazorComponents().AddInteractiveServerComponents();
            services.AddAgentFrameworkUi();
            configure(services);
        });

    private static void DecorateWorkspace(IServiceCollection services, bool failCatalog) {
        var factory = services.Last(x => x.ServiceType == typeof(IAgentFrameworkWorkspaceService)).ImplementationFactory!;
        services.AddScoped<IAgentFrameworkWorkspaceService>(provider => {
            var proxy = DispatchProxy.Create<IAgentFrameworkWorkspaceService, ReadFailureWorkspace>();
            var observer = (ReadFailureWorkspace)(object)proxy;
            observer.Inner = (IAgentFrameworkWorkspaceService)factory(provider);
            observer.FailCatalog = failCatalog;
            return proxy;
        });
    }

    public class ReadFailureWorkspace : DispatchProxy {
        public IAgentFrameworkWorkspaceService Inner { get; set; } = default!;
        public bool FailCatalog { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            if (FailCatalog && method!.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync)) {
                return Task.FromException<IReadOnlyList<AgentDefinition>>(new IOException("Private catalog test failure"));
            }
            if (!FailCatalog && method!.Name == nameof(IAgentFrameworkWorkspaceService.GetAgentOverviewAsync)) {
                return Task.FromException<AgentOverviewSnapshot>(new IOException("Private overview test failure"));
            }
            return method!.Invoke(Inner, args);
        }
    }

    private sealed class FailingBoundRead : IBoundAgentResourceQuery {
        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            => Task.FromException<int>(new IOException("Private bound test failure"));
    }
}
