using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Voice;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tools.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.DataProtection;

namespace CanDoItAll.AgentFramework.Hosting;

public static class AgentFrameworkServiceCollectionExtensions
{
    public static IServiceCollection AddAgentFrameworkCore(
        this IServiceCollection services,
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null,
        AgentExecutionActivityWorkspaceIdentity? activityWorkspaceIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var resolvedScope = workspaceScope ?? WorkspaceScopeDescriptor.Sandbox;
        var resolvedActivityWorkspaceIdentity =
            activityWorkspaceIdentity
            ?? AgentExecutionActivityWorkspaceIdentity.CreateHostLifetime(
                resolvedScope);
        if (resolvedActivityWorkspaceIdentity.WorkspaceScope != resolvedScope)
        {
            throw new ArgumentException(
                "The activity workspace identity must match the configured workspace scope.",
                nameof(activityWorkspaceIdentity));
        }

        services.AddLogging();
        services.AddDataProtection();
        services.TryAddSingleton<IPhysicalFileSystemPathPolicyFactory, PhysicalFileSystemPathPolicyFactory>();
        services.TryAddSingleton<IExternalTargetPathRegistryFactory, ExternalTargetPathRegistryFactory>();
        services.TryAddScoped<IExternalTargetPathRegistry, ExternalTargetPathRegistry>();
        services.TryAddSingleton(serviceProvider => new WorkspaceFileInspectionScopeFactory(
            normalizedWorkspaceRoot,
            resolvedScope,
            serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
            serviceProvider.GetRequiredService<IExternalTargetPathRegistryFactory>()));
        services.TryAddSingleton<ISandboxWorkspaceStore>(_ => new FileSandboxWorkspaceStore(normalizedWorkspaceRoot, resolvedScope));
        services.TryAddSingleton<ISandboxWorkspaceExecutionRunStore>(serviceProvider =>
            (ISandboxWorkspaceExecutionRunStore)serviceProvider.GetRequiredService<ISandboxWorkspaceStore>());
        services.TryAddSingleton<IAgentUsageTotalsQueryService, AgentUsageTotalsQueryService>();
        services.TryAddSingleton<IAgentPackageService>(_ => new ZipAgentPackageService(normalizedWorkspaceRoot, resolvedScope));
        services.TryAddSingleton<WorkspaceExecutableLocator>();
        services.TryAddScoped<IWorkspaceFileService>(serviceProvider => new WorkspaceFileService(
            normalizedWorkspaceRoot,
            serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
            resolvedScope,
            serviceProvider.GetRequiredService<IExternalTargetPathRegistry>()));
        services.TryAddScoped<IPluginWorkspaceFiles, PluginWorkspaceFiles>();
        services.TryAddScoped<IWorkspacePathResolutionService>(serviceProvider => new WorkspacePathResolutionService(
            normalizedWorkspaceRoot,
            serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
            resolvedScope,
            serviceProvider.GetRequiredService<IExternalTargetPathRegistry>()));
        services.TryAddSingleton<IWorkspaceProcessHost, LocalWorkspaceProcessHost>();
        services.TryAddSingleton<IWorkspaceLongRunningProcessHost>(serviceProvider =>
            serviceProvider.GetRequiredService<IWorkspaceProcessHost>() as IWorkspaceLongRunningProcessHost
            ?? throw new InvalidOperationException(
                "The configured workspace process host does not implement managed process sessions."));
        services.TryAddScoped<IWorkspaceCommandExecutionService>(serviceProvider => new WorkspaceCommandExecutionService(
            normalizedWorkspaceRoot,
            serviceProvider.GetRequiredService<IWorkspaceProcessHost>(),
            serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
            resolvedScope,
            serviceProvider.GetServices<IWorkspaceCommandReceiptLifecycleFactExtractor>(),
            serviceProvider.GetRequiredService<IExternalTargetPathRegistry>()));
        services.TryAddSingleton<IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory>(serviceProvider =>
            new WorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
                serviceProvider.GetServices<IWorkspaceCommandReceiptLifecycleFactExtractor>().ToList(),
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
                serviceProvider.GetRequiredService<IExternalTargetPathRegistryFactory>()));
        services.TryAddSingleton<IWorkspaceExecutionRunProcessLeaseCleaner>(serviceProvider =>
        {
            IPhysicalFileSystemPathPolicyFactory pathPolicyFactory =
                serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>();
            return new WorkspaceExecutionRunProcessLeaseCleaner(
                serviceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>(),
                new WorkspaceExecutionScope(
                    normalizedWorkspaceRoot,
                    resolvedScope,
                    resolvedActivityWorkspaceIdentity.DatabaseProfileId,
                    resolvedActivityWorkspaceIdentity.DatabaseProfileGeneration,
                    rootCaseSensitivity: pathPolicyFactory.Create(normalizedWorkspaceRoot).CaseSensitivity),
                serviceProvider.GetRequiredService<IWorkspaceExecutionRunProcessLeaseCleanupScopeFactory>());
        });
        services.TryAddSingleton<IWorkspaceDocumentMarkdownConverter, ManagedCodeMarkItDownDocumentMarkdownConverter>();
        services.TryAddScoped<IWorkspaceImageOperationService>(serviceProvider => new WorkspaceImageOperationService(
            normalizedWorkspaceRoot,
            serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
            resolvedScope,
            serviceProvider.GetRequiredService<IExternalTargetPathRegistry>()));
        services.TryAddScoped<IWorkspaceArtifactToolService>(serviceProvider => new WorkspaceArtifactToolService(
            normalizedWorkspaceRoot,
            serviceProvider.GetRequiredService<IWorkspaceCommandExecutionService>(),
            serviceProvider.GetRequiredService<IWorkspaceDocumentMarkdownConverter>(),
            serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
            resolvedScope,
            serviceProvider.GetRequiredService<IWorkspaceImageOperationService>(),
            serviceProvider.GetRequiredService<IExternalTargetPathRegistry>()));
        services.TryAddSingleton<IAgentProviderCredentialResolver, EnvironmentVariableAgentProviderCredentialResolver>();
        services.AddMafProviderRuntimeServices();
        services.TryAddSingleton<IProviderProfileService, ProviderProfileService>();
        services.TryAddSingleton<AgentReferenceDataInvalidationHub>();
        services.TryAddSingleton<IAgentReferenceDataCacheInvalidator>(serviceProvider =>
            serviceProvider.GetRequiredService<AgentReferenceDataInvalidationHub>());
        services.TryAddScoped<AgentReferenceDataCache>();
        services.TryAddScoped<IAgentReferenceDataProvider, WorkspaceBackedAgentReferenceDataProvider>();
        services.TryAddSingleton(AgentExecutionPreparationCachePolicy.Default);
        services.TryAddScoped<AgentExecutionPreparationCache>();
        services.TryAddScoped<IAgentExecutionPreparationCache>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    AgentExecutionPreparationCache>());
        services.TryAddSingleton<IAgentExecutionProfileGenerationSource>(
            new FixedAgentExecutionProfileGenerationSource(default));
        services.TryAddSingleton<WorkspaceBackedProviderProfileRegistry>(serviceProvider => new WorkspaceBackedProviderProfileRegistry(
            serviceProvider.GetRequiredService<ISandboxWorkspaceStore>(),
            serviceProvider.GetRequiredService<IProviderProfileService>()));
        services.TryAddSingleton<IProviderProfileRegistry>(serviceProvider =>
            serviceProvider.GetRequiredService<WorkspaceBackedProviderProfileRegistry>());
        services.TryAddSingleton<IProviderRuntimeProfileSource>(serviceProvider =>
            serviceProvider.GetRequiredService<WorkspaceBackedProviderProfileRegistry>());
        services.TryAddSingleton<IProviderRuntimeProfileSnapshotSource>(serviceProvider =>
            serviceProvider.GetRequiredService<WorkspaceBackedProviderProfileRegistry>());
        services.TryAddSingleton<IAgentExecutionCheckpointBridge>(serviceProvider => new WorkflowBackedAgentExecutionCheckpointBridge(
            serviceProvider.GetRequiredService<ISandboxWorkspaceStore>(),
            normalizedWorkspaceRoot,
            resolvedScope));
        services.TryAddSingleton<IAgentExecutionGovernanceBridge>(serviceProvider => new DurableAgentExecutionGovernanceBridge(
            serviceProvider.GetRequiredService<IAgentExecutionCheckpointBridge>()));
        services.TryAddSingleton<BufferedAgentExecutionEventSink>();
        services.TryAddSingleton<IAgentExecutionEventSink>(serviceProvider => serviceProvider.GetRequiredService<BufferedAgentExecutionEventSink>());
        services.TryAddSingleton<IAgentExecutionCancellationRegistry, AgentExecutionCancellationRegistry>();
        services.TryAddSingleton(resolvedActivityWorkspaceIdentity);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<
            PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>>(
            serviceProvider => new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                serviceProvider.GetRequiredService<TimeProvider>()));
        services.TryAddSingleton<AgentExecutionActivityCoordinator>();
        services.TryAddSingleton<IAgentExecutionActivityCoordinator>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    AgentExecutionActivityCoordinator>());
        services.TryAddSingleton<IAgentExecutionActivityReader>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    AgentExecutionActivityCoordinator>());
        services.AddAgentFrameworkA2AHosting();
        services.AddAgentFrameworkVoice();
        services.TryAddSingleton<IWorkspaceRuntimeServicesFactory>(serviceProvider => new WorkspaceRuntimeServicesFactory(
            serviceProvider.GetServices<IWorkspaceCommandReceiptLifecycleFactExtractor>().ToList(),
            serviceProvider.GetService<IWorkspaceDocumentMarkdownConverter>() ?? new ManagedCodeMarkItDownDocumentMarkdownConverter(),
            serviceProvider.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(),
            serviceProvider.GetRequiredService<IExternalTargetPathRegistryFactory>()));
        services.TryAddSingleton<MafAgentRuntime>(serviceProvider => new MafAgentRuntime(normalizedWorkspaceRoot, serviceProvider, resolvedScope));
        // SB18: the four narrow runtime ports resolve directly to the native MAF adapters exposed
        // by the same MafAgentRuntime composition (one adapter set per runtime scope). No broad
        // runtime interface or compatibility facade is constructed by production registrations.
        services.TryAddSingleton<IAgentExecutionRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<MafAgentRuntime>().ExecutionPort);
        services.TryAddSingleton<IAgentContinuationRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<MafAgentRuntime>().ContinuationPort);
        services.TryAddSingleton<IProviderDiagnosticsRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<MafAgentRuntime>().DiagnosticsPort);
        services.TryAddSingleton<IProviderModelAdministrationRuntime>(serviceProvider =>
            serviceProvider.GetRequiredService<MafAgentRuntime>().ModelAdministrationPort);
        services.TryAddSingleton<ICapabilityProofService, CapabilityProofService>();
        services.TryAddSingleton<IProviderDiagnosticsService>(serviceProvider => new ProviderDiagnosticsService(
            serviceProvider.GetRequiredService<IProviderDiagnosticsRuntime>(),
            serviceProvider.GetRequiredService<IProviderModelAdministrationRuntime>()));
        services.TryAddSingleton<ISpreadsheetDocumentService, ClosedXmlSpreadsheetDocumentService>();
        services.TryAddSingleton<IProjectStructureRuntimeGateway, UnavailableProjectStructureRuntimeGateway>();
        services.AddMafWorkflowAdapterServices(ServiceLifetime.Scoped);
        services.AddInMemoryWorkflowCatalogServices();
        services.AddInMemoryWorkflowRuntimeStores(normalizedWorkspaceRoot, resolvedScope);
        // AgentFrameworkWorkspaceService takes IEnumerable<IAgentExecutionProviderSelectionPolicy> /
        // IEnumerable<IAgentExecutionRunCriticalityPolicy>, so plain reflection-based activation below already
        // supplies every registered policy (for example the Modules.Processes registrations) without a separate
        // aggregator registration; an empty enumerable is the fail-open default for hosts that never load a
        // product module (governed provider-override/criticality hardening simply stays off).
        services.TryAddScoped<AgentFrameworkWorkspaceService>();
        services.TryAddScoped<IAgentFrameworkWorkspaceService>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    AgentFrameworkWorkspaceService>());
        services.TryAddScoped<IAgentFrameworkWorkspaceActivityExecutionService>(
            serviceProvider =>
                serviceProvider.GetRequiredService<
                    AgentFrameworkWorkspaceService>());

        return services;
    }

    public static IServiceCollection AddAgentFrameworkIntegrated(
        this IServiceCollection services,
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope = null,
        AgentExecutionActivityWorkspaceIdentity? activityWorkspaceIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        return services.AddAgentFrameworkCore(
            workspaceRoot,
            workspaceScope,
            activityWorkspaceIdentity);
    }
}
