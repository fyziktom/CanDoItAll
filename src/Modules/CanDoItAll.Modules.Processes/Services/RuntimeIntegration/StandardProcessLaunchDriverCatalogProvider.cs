using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Processes;


internal sealed class StandardProcessLaunchDriverCatalogProvider(
    IProcessStepExecutionDriver executionDriver,
    IProcessHostCapabilitySnapshotProvider hostCapabilitySnapshotProvider) : IProcessLaunchDriverCatalogProvider
{
    public async ValueTask<ProcessLaunchDriverCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await hostCapabilitySnapshotProvider.GetAsync(cancellationToken).ConfigureAwait(false);

        return new ProcessLaunchDriverCatalog(
            new ProcessDriverCatalog(StandardProcessAdapterDriverPackageFactory.CreateLayeredPackages(executionDriver)),
            executionDriver.Descriptor.Strategy.StrategyId,
            executionDriver.Descriptor.Adapter.CapabilityTags)
        {
            HostCapabilities = snapshot
        };
    }
}

internal sealed record StandardProcessAdapterCompositionRegistration(
    ProcessStepExecutionDriverDescriptor DriverDescriptor);

internal sealed class StandardProcessAdapterHostCapabilitySource(
    IEnumerable<StandardProcessAdapterCompositionRegistration> registrations) : IProcessHostCapabilitySource
{
    private static readonly IReadOnlySet<ProcessHostCapabilityId> OwnedCapabilities =
        new HashSet<ProcessHostCapabilityId> { ProcessHostCapabilityIds.ManagedProcessAdapter };
    private readonly IReadOnlyList<StandardProcessAdapterCompositionRegistration> registrations =
        (registrations ?? throw new ArgumentNullException(nameof(registrations))).ToArray();

    public ProcessHostCapabilitySourceId SourceId { get; } = new("standard-process-adapter");

    public IReadOnlySet<ProcessHostCapabilityId> DeclaredCapabilities => OwnedCapabilities;

    public ValueTask<IReadOnlyList<ProcessHostCapabilityFact>> ProbeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var available = registrations is [var registration] &&
                        registration.DriverDescriptor == AgentFrameworkProcessExecutionAdapter.DriverDescriptor;
        return ValueTask.FromResult<IReadOnlyList<ProcessHostCapabilityFact>>(
        [
            new ProcessHostCapabilityFact(
                ProcessHostCapabilityIds.ManagedProcessAdapter,
                available
                    ? ProcessHostCapabilityAvailability.Available
                    : ProcessHostCapabilityAvailability.Unavailable,
                available
                    ? ProcessHostCapabilityReason.Ready
                    : ProcessHostCapabilityReason.InvalidConfiguration,
                available
                    ? ProcessHostExecutionPort.ManagedProcessAdapter
                    : ProcessHostExecutionPort.None)
        ]);
    }
}
