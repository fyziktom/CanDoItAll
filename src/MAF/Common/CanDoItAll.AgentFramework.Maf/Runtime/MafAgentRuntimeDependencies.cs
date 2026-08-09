using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CapabilityAccessPolicyEvaluatorContract = CanDoItAll.AgentFramework.Capabilities.Abstractions.ICapabilityAccessPolicyEvaluator;

namespace CanDoItAll.AgentFramework.Maf;

/// <summary>
/// Typed dependency bundle for <see cref="MafAgentRuntime"/> composition. All runtime collaborators are
/// constructor-injected through this record; <see cref="FromServices"/> is the only place that translates an
/// <see cref="IServiceProvider"/> into the typed graph, so composition roots stay the sole owners of service location.
/// </summary>
internal sealed record MafAgentRuntimeDependencies(
    IMafProviderRuntimeGateway ProviderRuntimeGateway,
    IMafProviderStreamingDispatchGate ProviderStreamingDispatchGate,
    IAgentImageAnalysisService ImageAnalysisService,
    IMafProviderCredentialService ProviderCredentialService,
    IMafProviderAgentFactory ProviderAgentFactory,
    IRuntimeToolProviderComposer RuntimeToolProviderComposer,
    IMafRuntimeCompositionMetrics CompositionMetrics,
    IWorkspaceRuntimeServicesFactory WorkspaceRuntimeServicesFactory,
    IPhysicalFileSystemPathPolicyFactory PhysicalPathPolicyFactory,
    ISpreadsheetDocumentService SpreadsheetDocumentService,
    IMafApprovalContinuationDriver ApprovalContinuationDriver,
    IMafRuntimeSessionPersistenceDriver SessionPersistenceDriver,
    MafRuntimeCapabilityDependencies CapabilityDependencies,
    IReadOnlyList<IAgentExecutionOutcomeRecoveryPolicy> ExecutionOutcomeRecoveryPolicies,
    AgentToolInvocationPolicyPipeline ToolInvocationPolicyPipeline)
{
    public static MafAgentRuntimeDependencies FromServices(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var providerRuntimeGateway = serviceProvider.GetService(typeof(IMafProviderRuntimeGateway)) as IMafProviderRuntimeGateway;
        var providerStreamingDispatchGate = serviceProvider.GetService(typeof(IMafProviderStreamingDispatchGate)) as IMafProviderStreamingDispatchGate;
        var imageAnalysisService = serviceProvider.GetService(typeof(IAgentImageAnalysisService)) as IAgentImageAnalysisService;
        var providerCredentialService = serviceProvider.GetService(typeof(IMafProviderCredentialService)) as IMafProviderCredentialService;
        var providerAgentFactory = serviceProvider.GetService(typeof(IMafProviderAgentFactory)) as IMafProviderAgentFactory;
        var workspaceRuntimeServicesFactory = serviceProvider.GetService(typeof(IWorkspaceRuntimeServicesFactory)) as IWorkspaceRuntimeServicesFactory;
        var physicalPathPolicyFactory = serviceProvider.GetService(typeof(IPhysicalFileSystemPathPolicyFactory)) as IPhysicalFileSystemPathPolicyFactory;
        var missingServices = new[]
            {
                (Service: nameof(IMafProviderRuntimeGateway), IsMissing: providerRuntimeGateway is null),
                (Service: nameof(IMafProviderStreamingDispatchGate), IsMissing: providerStreamingDispatchGate is null),
                (Service: nameof(IAgentImageAnalysisService), IsMissing: imageAnalysisService is null),
                (Service: nameof(IMafProviderCredentialService), IsMissing: providerCredentialService is null),
                (Service: nameof(IMafProviderAgentFactory), IsMissing: providerAgentFactory is null),
                (Service: nameof(IWorkspaceRuntimeServicesFactory), IsMissing: workspaceRuntimeServicesFactory is null),
                (Service: nameof(IPhysicalFileSystemPathPolicyFactory), IsMissing: physicalPathPolicyFactory is null)
            }
            .Where(item => item.IsMissing)
            .Select(item => item.Service)
            .ToList();
        if (missingServices.Count > 0)
        {
            var workspaceFactoryHint = missingServices.Contains(nameof(IWorkspaceRuntimeServicesFactory))
                ? " Register AddAgentFrameworkWorkspace/AddAgentFrameworkModule composition or pass an explicit IWorkspaceRuntimeServicesFactory."
                : string.Empty;
            throw new InvalidOperationException(
                $"The MAF provider runtime service graph is incomplete. Missing: {string.Join(", ", missingServices)}. Register AddMafProviderRuntimeServices().{workspaceFactoryHint}");
        }

        return new MafAgentRuntimeDependencies(
            providerRuntimeGateway!,
            providerStreamingDispatchGate!,
            imageAnalysisService!,
            providerCredentialService!,
            providerAgentFactory!,
            serviceProvider.GetService(typeof(IRuntimeToolProviderComposer)) as IRuntimeToolProviderComposer
                ?? CanDoItAll.AgentFramework.Maf.RuntimeToolProviderComposer.Default,
            serviceProvider.GetService(typeof(IMafRuntimeCompositionMetrics)) as IMafRuntimeCompositionMetrics
                ?? NoOpMafRuntimeCompositionMetrics.Instance,
            workspaceRuntimeServicesFactory!,
            physicalPathPolicyFactory!,
            serviceProvider.GetService(typeof(ISpreadsheetDocumentService)) as ISpreadsheetDocumentService
                ?? new ClosedXmlSpreadsheetDocumentService(),
            // Internal single-implementation drivers are constructed directly:
            // they are not composition seams, so a container can never swap in
            // a divergent graph silently.
            new MafApprovalContinuationDriver(),
            new MafRuntimeSessionPersistenceDriver(),
            MafRuntimeCapabilityDependencies.FromServices(serviceProvider),
            serviceProvider.GetServices<IAgentExecutionOutcomeRecoveryPolicy>().ToList(),
            new AgentToolInvocationPolicyPipeline(
                serviceProvider.GetService(typeof(IAgentToolInvocationPolicy)) as IAgentToolInvocationPolicy
                    ?? new DefaultAgentToolInvocationPolicy(),
                serviceProvider.GetServices<IToolInvocationPolicyContextContributor>().ToList()));
    }
}

/// <summary>
/// Typed dependencies consumed by <see cref="RuntimeCapabilityComposer"/> and the capability builders it owns.
/// </summary>
internal sealed record MafRuntimeCapabilityDependencies(
    IReadOnlyList<IAgentContextContributor> ContextContributors,
    IReadOnlyList<IAgentRuntimeToolProvider> RuntimeToolProviders,
    MafRuntimeStorageServices? StorageServices,
    IConfiguration? A2AConfiguration,
    ILoggerFactory LoggerFactory,
    CapabilityAccessPolicyEvaluatorContract CapabilityAccessPolicyEvaluator,
    IMcpClientFactory McpClientFactory,
    ISecretRuntimeResolver? SecretRuntimeResolver,
    IRegisteredCapabilityServiceSource RegisteredCapabilityServices)
{
    public static MafRuntimeCapabilityDependencies FromServices(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var catalogService = serviceProvider.GetService(typeof(IStorageCatalogService)) as IStorageCatalogService;
        var driverRegistry = serviceProvider.GetService(typeof(IStorageDriverRegistry)) as IStorageDriverRegistry;
        var browseDriverRegistry = serviceProvider.GetService(typeof(IStorageBrowseDriverRegistry)) as IStorageBrowseDriverRegistry;
        var storageServices = catalogService is not null && driverRegistry is not null
            ? new MafRuntimeStorageServices(catalogService, driverRegistry, browseDriverRegistry)
            : null;

        return new MafRuntimeCapabilityDependencies(
            serviceProvider.GetServices<IAgentContextContributor>().ToList(),
            serviceProvider.GetServices<IAgentRuntimeToolProvider>().ToList(),
            storageServices,
            serviceProvider.GetService(typeof(IConfiguration)) as IConfiguration,
            serviceProvider.GetService(typeof(ILoggerFactory)) as ILoggerFactory ?? NullLoggerFactory.Instance,
            serviceProvider.GetService(typeof(CapabilityAccessPolicyEvaluatorContract)) as CapabilityAccessPolicyEvaluatorContract
                ?? new CanDoItAll.AgentFramework.Capabilities.Access.CapabilityAccessPolicyEvaluator(),
            serviceProvider.GetService(typeof(IMcpClientFactory)) as IMcpClientFactory
                ?? new LocalStdioMcpClientFactory(),
            serviceProvider.GetService(typeof(ISecretRuntimeResolver)) as ISecretRuntimeResolver,
            new ServiceProviderRegisteredCapabilityServiceSource(
                serviceProvider,
                serviceProvider.GetServices<RegisteredCapabilityServiceDescriptor>()
                    .Select(descriptor => descriptor.ServiceType)
                    .ToHashSet()));
    }
}

/// <summary>
/// Storage services that back the runtime storage tools. The bundle is present only when both the catalog service
/// and the driver registry are available; the browse registry stays optional.
/// </summary>
internal sealed record MafRuntimeStorageServices(
    IStorageCatalogService CatalogService,
    IStorageDriverRegistry DriverRegistry,
    IStorageBrowseDriverRegistry? BrowseDriverRegistry);

/// <summary>
/// Resolves data-driven registered skill/plugin service types declared in capability configuration JSON.
/// </summary>
internal interface IRegisteredCapabilityServiceSource
{
    object? Resolve(Type serviceType);
}

/// <summary>
/// The one sanctioned bridge from capability configuration JSON service-type names to DI-registered instances.
/// It lives in this composition file so runtime code depends on the typed
/// <see cref="IRegisteredCapabilityServiceSource"/> contract instead of <see cref="IServiceProvider"/>.
/// </summary>
internal sealed class ServiceProviderRegisteredCapabilityServiceSource(
    IServiceProvider serviceProvider,
    IReadOnlySet<Type> approvedServiceTypes) : IRegisteredCapabilityServiceSource
{
    public object? Resolve(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        // Capability configuration can only reach service types a module
        // explicitly approved via RegisteredCapabilityServiceDescriptor; an
        // arbitrary configured type name fails closed instead of resolving
        // whatever happens to be registered in the container.
        if (!approvedServiceTypes.Contains(serviceType))
        {
            throw new InvalidOperationException(
                $"Service type '{serviceType.FullName}' is not an approved registered capability service. Register a RegisteredCapabilityServiceDescriptor for it in the owning module.");
        }

        return serviceProvider.GetService(serviceType);
    }
}

/// <summary>
/// Explicit approval of one service type for data-driven capability
/// configuration. Modules register a descriptor for every plugin/skill service
/// type their capability configurations may reference; anything else fails
/// closed at composition time.
/// </summary>
public sealed record RegisteredCapabilityServiceDescriptor(Type ServiceType)
{
    public Type ServiceType { get; } = ServiceType ?? throw new ArgumentNullException(nameof(ServiceType));
}
