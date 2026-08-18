using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public interface IAgentProviderFactory
{
    IReadOnlyList<ProviderCapabilityDescriptor> ListCapabilities(ProviderKind providerKind);

    bool Supports(ProviderKind providerKind, AgentProviderCapabilityKind capability);

    TDriver Resolve<TDriver>(ProviderKind providerKind)
        where TDriver : class, IAgentProviderDriver;

    bool TryResolve<TDriver>(ProviderKind providerKind, out TDriver driver)
        where TDriver : class, IAgentProviderDriver;

    ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query);
}

public sealed class AgentProviderDriverRegistryBuilder
{
    private readonly List<IAgentProviderDriver> drivers = [];

    public AgentProviderDriverRegistryBuilder AddDriver(IAgentProviderDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);

        if (driver.Capabilities.Count == 0)
        {
            throw new InvalidOperationException($"Provider driver '{driver.GetType().Name}' must declare at least one capability.");
        }

        drivers.Add(driver);
        return this;
    }

    public IAgentProviderFactory Build()
    {
        return new AgentProviderDriverRegistry(drivers);
    }
}

public sealed class AgentProviderDriverRegistry : IAgentProviderFactory
{
    private readonly IReadOnlyDictionary<ProviderCapabilityRegistrationKey, IAgentProviderDriver> driversByCapability;
    private readonly IReadOnlyDictionary<ProviderKind, IReadOnlyList<ProviderCapabilityDescriptor>> descriptorsByProvider;

    public AgentProviderDriverRegistry(IEnumerable<IAgentProviderDriver> drivers)
    {
        ArgumentNullException.ThrowIfNull(drivers);

        var driverList = drivers.ToList();
        driversByCapability = BuildCapabilityMap(driverList);
        descriptorsByProvider = BuildDescriptorMap(driverList);
    }

    public IReadOnlyList<ProviderCapabilityDescriptor> ListCapabilities(ProviderKind providerKind)
    {
        return descriptorsByProvider.TryGetValue(providerKind, out var descriptors)
            ? descriptors
            : [];
    }

    public bool Supports(ProviderKind providerKind, AgentProviderCapabilityKind capability)
    {
        return driversByCapability.ContainsKey(new ProviderCapabilityRegistrationKey(providerKind, capability));
    }

    public TDriver Resolve<TDriver>(ProviderKind providerKind)
        where TDriver : class, IAgentProviderDriver
    {
        var capability = ResolveCapabilityKind<TDriver>();
        if (!TryResolve<TDriver>(providerKind, out var driver))
        {
            throw new UnsupportedProviderCapabilityException(providerKind, capability);
        }

        return driver;
    }

    public bool TryResolve<TDriver>(ProviderKind providerKind, out TDriver driver)
        where TDriver : class, IAgentProviderDriver
    {
        var capability = ResolveCapabilityKind<TDriver>();
        var key = new ProviderCapabilityRegistrationKey(providerKind, capability);
        if (driversByCapability.TryGetValue(key, out var candidate) && candidate is TDriver typed)
        {
            driver = typed;
            return true;
        }

        driver = null!;
        return false;
    }

    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!driversByCapability.TryGetValue(new ProviderCapabilityRegistrationKey(query.Provider.Kind, query.Capability), out var driver))
        {
            throw new UnsupportedProviderCapabilityException(query.Provider.Kind, query.Capability);
        }

        return driver.GetDispatchLimits(query);
    }

    private static IReadOnlyDictionary<ProviderCapabilityRegistrationKey, IAgentProviderDriver> BuildCapabilityMap(
        IReadOnlyList<IAgentProviderDriver> drivers)
    {
        var map = new Dictionary<ProviderCapabilityRegistrationKey, IAgentProviderDriver>();
        foreach (var driver in drivers)
        {
            foreach (var capability in driver.Capabilities)
            {
                var key = new ProviderCapabilityRegistrationKey(driver.ProviderKind, capability);
                if (map.TryGetValue(key, out var existing))
                {
                    throw new DuplicateProviderCapabilityRegistrationException(driver.ProviderKind, capability, existing.GetType(), driver.GetType());
                }

                map.Add(key, driver);
            }
        }

        return map;
    }

    private static IReadOnlyDictionary<ProviderKind, IReadOnlyList<ProviderCapabilityDescriptor>> BuildDescriptorMap(
        IReadOnlyList<IAgentProviderDriver> drivers)
    {
        return drivers
            .GroupBy(driver => driver.ProviderKind)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProviderCapabilityDescriptor>)group
                    .SelectMany(driver => driver.Capabilities.Select(capability => CreateDescriptor(capability)))
                    .OrderBy(descriptor => descriptor.Capability)
                    .ToList());
    }

    private static ProviderCapabilityDescriptor CreateDescriptor(AgentProviderCapabilityKind capability)
    {
        return new ProviderCapabilityDescriptor(capability, ResolveOperations(capability));
    }

    private static IReadOnlySet<AgentProviderOperationKind> ResolveOperations(AgentProviderCapabilityKind capability)
    {
        return capability switch
        {
            AgentProviderCapabilityKind.ModelCatalog => new HashSet<AgentProviderOperationKind> { AgentProviderOperationKind.ListModels },
            AgentProviderCapabilityKind.ChatCompletion => new HashSet<AgentProviderOperationKind>
            {
                AgentProviderOperationKind.CompleteChat,
                AgentProviderOperationKind.AnalyzeImage
            },
            AgentProviderCapabilityKind.ImageGeneration => new HashSet<AgentProviderOperationKind>
            {
                AgentProviderOperationKind.GenerateImage,
                AgentProviderOperationKind.EditImage
            },
            AgentProviderCapabilityKind.SpeechToText => new HashSet<AgentProviderOperationKind> { AgentProviderOperationKind.TranscribeSpeech },
            AgentProviderCapabilityKind.TextToSpeech => new HashSet<AgentProviderOperationKind> { AgentProviderOperationKind.SynthesizeSpeech },
            AgentProviderCapabilityKind.Health => new HashSet<AgentProviderOperationKind> { AgentProviderOperationKind.TestHealth },
            AgentProviderCapabilityKind.ModelMaintenance => new HashSet<AgentProviderOperationKind> { AgentProviderOperationKind.CreateOrUpdateModel },
            _ => []
        };
    }

    private static AgentProviderCapabilityKind ResolveCapabilityKind<TDriver>()
        where TDriver : class, IAgentProviderDriver
    {
        var driverType = typeof(TDriver);
        if (driverType == typeof(IProviderHealthDriver))
        {
            return AgentProviderCapabilityKind.Health;
        }

        if (driverType == typeof(IProviderModelCatalogDriver))
        {
            return AgentProviderCapabilityKind.ModelCatalog;
        }

        if (driverType == typeof(IProviderChatCompletionDriver) ||
            driverType == typeof(IProviderStreamingChatCompletionDriver))
        {
            return AgentProviderCapabilityKind.ChatCompletion;
        }

        if (driverType == typeof(IProviderImageGenerationDriver))
        {
            return AgentProviderCapabilityKind.ImageGeneration;
        }

        if (driverType == typeof(IProviderSpeechToTextDriver))
        {
            return AgentProviderCapabilityKind.SpeechToText;
        }

        if (driverType == typeof(IProviderTextToSpeechDriver))
        {
            return AgentProviderCapabilityKind.TextToSpeech;
        }

        if (driverType == typeof(IProviderModelMaintenanceDriver))
        {
            return AgentProviderCapabilityKind.ModelMaintenance;
        }

        throw new InvalidOperationException($"Driver type '{driverType.Name}' is not a supported provider capability contract.");
    }

    private sealed record ProviderCapabilityRegistrationKey(
        ProviderKind ProviderKind,
        AgentProviderCapabilityKind Capability);
}
