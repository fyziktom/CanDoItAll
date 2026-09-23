using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

internal static class AgentUiAdapterTestHost {
    public static Task<ApiTestHost> CreateAsync(Action<IServiceCollection>? configureServices = null)
        => ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true, configureServices: services => {
            services.AddRazorComponents().AddInteractiveServerComponents();
            services.AddAgentFrameworkUi();
            configureServices?.Invoke(services);
        });
}
