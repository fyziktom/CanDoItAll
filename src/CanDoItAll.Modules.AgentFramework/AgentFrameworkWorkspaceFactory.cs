using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework.Hosting;
using Microsoft.Extensions.DependencyInjection;

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
    ICapabilityProofService capabilityProofService) : ICanDoItAllAgentWorkspaceFactory
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
        var commandExecutionService = new WorkspaceCommandExecutionService(workspaceRoot, processHost, scope);
        var artifactToolService = new WorkspaceArtifactToolService(workspaceRoot, commandExecutionService, scope);
        var runtime = new ScenarioHarnessAgentRuntime(
            new MafAgentRuntime(workspaceRoot, serviceProvider, scope),
            workspaceRoot,
            scope,
            fileService,
            commandExecutionService);
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
            providerDiagnosticsService,
            governanceBridge,
            new NullAgentExecutionEventSink(),
            checkpointBridge,
            processHost);

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
