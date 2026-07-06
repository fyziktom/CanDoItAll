using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal interface IMafRuntimeDependencyResolver
{
    MafRuntimeProviderDependencies ResolveProviderDependencies(IServiceProvider services);

    MafWorkspaceRuntimeServices ResolveWorkspaceServices(
        IServiceProvider services,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope);
}

internal sealed class MafRuntimeDependencyResolver : IMafRuntimeDependencyResolver
{
    public static IMafRuntimeDependencyResolver Default { get; } = new MafRuntimeDependencyResolver();

    public static IMafRuntimeDependencyResolver Resolve(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.GetService(typeof(IMafRuntimeDependencyResolver)) is IMafRuntimeDependencyResolver resolver
            ? resolver
            : Default;
    }

    public MafRuntimeProviderDependencies ResolveProviderDependencies(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var gateway = services.GetService(typeof(IMafProviderRuntimeGateway)) is IMafProviderRuntimeGateway resolvedGateway
            ? resolvedGateway
            : MafProviderRuntimeGateway.CreateFallback(services);
        var streamingDispatchGate = services.GetService(typeof(IMafProviderStreamingDispatchGate)) is IMafProviderStreamingDispatchGate resolvedGate
            ? resolvedGate
            : CreateFallbackProviderStreamingDispatchGate(services);

        return new MafRuntimeProviderDependencies(gateway, streamingDispatchGate);
    }

    public MafWorkspaceRuntimeServices ResolveWorkspaceServices(
        IServiceProvider services,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        var workspaceFileService = services.GetService(typeof(IWorkspaceFileService)) as IWorkspaceFileService
            ?? new WorkspaceFileService(workspaceRoot, workspaceScope);
        var workspaceCommandExecutionService = services.GetService(typeof(IWorkspaceCommandExecutionService)) as IWorkspaceCommandExecutionService
            ?? new WorkspaceCommandExecutionService(workspaceRoot, new LocalWorkspaceProcessHost(), workspaceScope);
        var workspaceArtifactToolService = services.GetService(typeof(IWorkspaceArtifactToolService)) as IWorkspaceArtifactToolService
            ?? new WorkspaceArtifactToolService(workspaceRoot, workspaceCommandExecutionService, workspaceScope);

        return new MafWorkspaceRuntimeServices(
            workspaceFileService,
            workspaceCommandExecutionService,
            workspaceArtifactToolService);
    }

    private static IMafProviderStreamingDispatchGate CreateFallbackProviderStreamingDispatchGate(IServiceProvider services)
    {
        var providerFactory = services.GetService(typeof(IAgentProviderFactory)) is IAgentProviderFactory resolvedFactory
            ? resolvedFactory
            : MafProviderRuntimeServiceCollectionExtensions.CreateDefaultProviderFactory(services);
        var dispatchLaneGate = services.GetService(typeof(IProviderDispatchLaneGate)) is IProviderDispatchLaneGate resolvedGate
            ? resolvedGate
            : new ProviderDispatchLaneGate(providerFactory);

        return new MafProviderStreamingDispatchGate(dispatchLaneGate);
    }
}
