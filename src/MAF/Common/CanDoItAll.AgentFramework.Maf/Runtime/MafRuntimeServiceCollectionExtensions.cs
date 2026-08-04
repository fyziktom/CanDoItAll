using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.AgentFramework.Maf;

public static class MafRuntimeServiceCollectionExtensions
{
    public static IServiceCollection AddMafRuntimeArchitectureServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMafRuntimeDependencyResolver, MafRuntimeDependencyResolver>();
        services.TryAddSingleton<IMafProviderCredentialService, MafProviderCredentialService>();
        services.TryAddSingleton<IRuntimeToolProviderAccessFilter, RuntimeToolProviderAccessFilter>();
        services.TryAddSingleton<IRuntimeToolProviderComposer, RuntimeToolProviderComposer>();
        services.TryAddSingleton<IMafRuntimeCompositionMetrics, NoOpMafRuntimeCompositionMetrics>();
        services.TryAddTransient<IMafApprovalContinuationDriver, MafApprovalContinuationDriver>();
        services.TryAddSingleton<IMafRuntimeSessionPersistenceDriver, MafRuntimeSessionPersistenceDriver>();

        return services;
    }
}

internal interface IMafRuntimeCompositionMetrics
{
    void Record(MafRuntimeCompositionMeasurement measurement);
}

internal sealed class NoOpMafRuntimeCompositionMetrics : IMafRuntimeCompositionMetrics
{
    public static NoOpMafRuntimeCompositionMetrics Instance { get; } = new();

    public void Record(MafRuntimeCompositionMeasurement measurement)
    {
    }
}

internal sealed class InMemoryMafRuntimeCompositionMetrics : IMafRuntimeCompositionMetrics
{
    private readonly ConcurrentQueue<MafRuntimeCompositionMeasurement> measurements = new();

    public void Record(MafRuntimeCompositionMeasurement measurement)
    {
        measurements.Enqueue(measurement);
    }

    public IReadOnlyList<MafRuntimeCompositionMeasurement> Snapshot()
    {
        return measurements.ToArray();
    }
}
