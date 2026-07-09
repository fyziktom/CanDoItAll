using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.AgentFramework;

public interface ICanDoItAllAgentWorkspaceFactory
{
    IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService();

    IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope);

    WorkspaceScopeDescriptor GetOrganizationScope();

    string GetWorkspaceRoot();
}

internal sealed class CanDoItAllAgentWorkspaceFactory(
    IServiceProvider serviceProvider,
    IWorkspacePathResolver workspacePathResolver,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IProviderProfileService providerProfileService,
    IProviderProfileRegistry providerProfileRegistry,
    IAgentProviderCredentialResolver providerCredentialResolver,
    ICapabilityProofService capabilityProofService,
    IOptions<ProcessMockAgentOptions> processMockAgentOptions) : ICanDoItAllAgentWorkspaceFactory
{
    private readonly Dictionary<string, IAgentFrameworkWorkspaceService> workspaceServices = new(StringComparer.Ordinal);

    public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
    {
        return serviceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
    }

    public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var key = scope.DisplayName;
        if (workspaceServices.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var workspaceRoot = GetWorkspaceRoot();
        var store = new FileSandboxWorkspaceStore(workspaceRoot, scope);
        var processHost = new LocalWorkspaceProcessHost();
        var fileService = new WorkspaceFileService(workspaceRoot, scope);
        var commandExecutionService = new WorkspaceCommandExecutionService(
            workspaceRoot,
            processHost,
            scope,
            serviceProvider.GetServices<IWorkspaceCommandReceiptLifecycleFactExtractor>());
        var mafRuntime = new MafAgentRuntime(workspaceRoot, serviceProvider, scope);
        var scenarioRuntime = new ScenarioHarnessAgentRuntime(
            mafRuntime,
            workspaceRoot,
            scope,
            fileService,
            commandExecutionService);
        var runtime = new ProcessMockAgentRuntime(
            scenarioRuntime,
            fileService,
            workspaceRoot,
            processMockAgentOptions);
        var checkpointBridge = new WorkflowBackedAgentExecutionCheckpointBridge(store, workspaceRoot, scope);
        var governanceBridge = new DurableAgentExecutionGovernanceBridge(checkpointBridge);
        var providerDiagnosticsService = new ProviderDiagnosticsService(runtime);
        var workspaceService = new AgentFrameworkWorkspaceService(
            store,
            new ZipAgentPackageService(workspaceRoot, scope),
            runtime,
            capabilityProofService,
            providerProfileService,
            providerProfileRegistry,
            providerCredentialResolver,
            providerDiagnosticsService,
            governanceBridge,
            new NullAgentExecutionEventSink(),
            checkpointBridge,
            processHost,
            serviceProvider.GetRequiredService<IAgentExecutionCancellationRegistry>(),
            workspacePathResolutionService: new WorkspacePathResolutionService(workspaceRoot, scope));

        workspaceServices[key] = workspaceService;
        return workspaceService;
    }

    public WorkspaceScopeDescriptor GetOrganizationScope()
    {
        return WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));
    }

    public string GetWorkspaceRoot()
    {
        return workspacePathResolver.ResolveWorkspaceRoot();
    }
}
