using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

/// <summary>
/// Integration test-host helper: production registrations serve the four narrow runtime ports
/// from the native MAF adapters directly (SB18 deleted the broad legacy runtime interface), so a
/// test that overrides runtime behavior with a fake must reroute the ports through that fake via
/// <see cref="FakeAgentRuntimePortAdapter"/> — the sanctioned test-side bridge for exactly this
/// purpose.
/// </summary>
internal static class AgentRuntimePortTestOverrides
{
    /// <summary>
    /// Replaces the four narrow runtime port registrations so they adapt whatever
    /// <see cref="IFakeAgentRuntime"/> the container resolves (typically a test fake registered by
    /// the caller) through one <see cref="FakeAgentRuntimePortAdapter"/> instance.
    /// </summary>
    public static IServiceCollection RouteRuntimePortsThroughAgentRuntime(this IServiceCollection services)
    {
        services.RemoveAll<FakeAgentRuntimePortAdapter>();
        services.RemoveAll<IAgentExecutionRuntime>();
        services.RemoveAll<IAgentContinuationRuntime>();
        services.RemoveAll<IProviderDiagnosticsRuntime>();
        services.RemoveAll<IProviderModelAdministrationRuntime>();
        services.AddSingleton(serviceProvider =>
            new FakeAgentRuntimePortAdapter(serviceProvider.GetRequiredService<IFakeAgentRuntime>()));
        services.AddSingleton<IAgentExecutionRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<FakeAgentRuntimePortAdapter>());
        services.AddSingleton<IAgentContinuationRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<FakeAgentRuntimePortAdapter>());
        services.AddSingleton<IProviderDiagnosticsRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<FakeAgentRuntimePortAdapter>());
        services.AddSingleton<IProviderModelAdministrationRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<FakeAgentRuntimePortAdapter>());
        return services;
    }
}
