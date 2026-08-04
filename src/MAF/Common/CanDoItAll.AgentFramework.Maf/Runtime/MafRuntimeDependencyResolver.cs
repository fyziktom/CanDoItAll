using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Tools.Documents;

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

        var gateway = services.GetService(typeof(IMafProviderRuntimeGateway)) as IMafProviderRuntimeGateway;
        var streamingDispatchGate = services.GetService(typeof(IMafProviderStreamingDispatchGate)) as IMafProviderStreamingDispatchGate;
        var imageAnalysisService = services.GetService(typeof(IAgentImageAnalysisService)) as IAgentImageAnalysisService;
        if (gateway is null || streamingDispatchGate is null || imageAnalysisService is null)
        {
            var missingServices = new[]
                {
                    (Service: nameof(IMafProviderRuntimeGateway), IsMissing: gateway is null),
                    (Service: nameof(IMafProviderStreamingDispatchGate), IsMissing: streamingDispatchGate is null),
                    (Service: nameof(IAgentImageAnalysisService), IsMissing: imageAnalysisService is null)
                }
                .Where(item => item.IsMissing)
                .Select(item => item.Service);
            throw new InvalidOperationException(
                $"The MAF provider runtime service graph is incomplete. Missing: {string.Join(", ", missingServices)}. Register AddMafProviderRuntimeServices().");
        }

        return new MafRuntimeProviderDependencies(
            gateway,
            streamingDispatchGate,
            imageAnalysisService);
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
            ?? new WorkspaceCommandExecutionService(
                workspaceRoot,
                new LocalWorkspaceProcessHost(),
                workspaceScope,
                ResolveLifecycleFactExtractors(services));
        var documentMarkdownConverter = services.GetService(typeof(IWorkspaceDocumentMarkdownConverter)) as IWorkspaceDocumentMarkdownConverter
            ?? new ManagedCodeMarkItDownDocumentMarkdownConverter();
        var imageOperationService = services.GetService(typeof(IWorkspaceImageOperationService)) as IWorkspaceImageOperationService
            ?? new WorkspaceImageOperationService(workspaceRoot, workspaceScope);
        var workspaceArtifactToolService = services.GetService(typeof(IWorkspaceArtifactToolService)) as IWorkspaceArtifactToolService
            ?? new WorkspaceArtifactToolService(
                workspaceRoot,
                workspaceCommandExecutionService,
                documentMarkdownConverter,
                workspaceScope,
                imageOperationService);

        return new MafWorkspaceRuntimeServices(
            workspaceFileService,
            workspaceCommandExecutionService,
            workspaceArtifactToolService);
    }

    private static IEnumerable<IWorkspaceCommandReceiptLifecycleFactExtractor> ResolveLifecycleFactExtractors(
        IServiceProvider services)
        => services.GetService(typeof(IEnumerable<IWorkspaceCommandReceiptLifecycleFactExtractor>)) is IEnumerable<IWorkspaceCommandReceiptLifecycleFactExtractor> extractors
            ? extractors
            : [];

}
