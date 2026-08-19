using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// One owned, scope-bound bundle of the workspace services used by a single
/// execution. Every member was constructed for exactly <see cref="Scope"/>;
/// runtime code receives this bundle instead of looking up competing services
/// from a root container or constructing fallbacks. Stateless services
/// (document converters, spreadsheet codecs) are not part of the bundle —
/// only services whose behavior is bound to the workspace root and scope
/// identity belong here. The bundle owns disposal of the services it created.
/// </summary>
public sealed class WorkspaceRuntimeServices : IAsyncDisposable
{
    private readonly IReadOnlyList<object> ownedServices;
    private bool isDisposed;

    public WorkspaceRuntimeServices(
        WorkspaceExecutionScope scope,
        IWorkspaceFileService fileService,
        IWorkspaceCommandExecutionService commandExecutionService,
        IWorkspaceArtifactToolService artifactToolService,
        IWorkspaceImageOperationService imageOperationService,
        IWorkspaceProcessHost processHost,
        IExternalTargetPathRegistry externalTargetPathRegistry,
        IReadOnlyList<object>? ownedServices = null)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        FileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
        CommandExecutionService = commandExecutionService ?? throw new ArgumentNullException(nameof(commandExecutionService));
        ArtifactToolService = artifactToolService ?? throw new ArgumentNullException(nameof(artifactToolService));
        ImageOperationService = imageOperationService ?? throw new ArgumentNullException(nameof(imageOperationService));
        ProcessHost = processHost ?? throw new ArgumentNullException(nameof(processHost));
        ExternalTargetPathRegistry = externalTargetPathRegistry ?? throw new ArgumentNullException(nameof(externalTargetPathRegistry));
        this.ownedServices = ownedServices ?? [];
    }

    public WorkspaceExecutionScope Scope { get; }

    public IWorkspaceFileService FileService { get; }

    public IWorkspaceCommandExecutionService CommandExecutionService { get; }

    public IWorkspaceArtifactToolService ArtifactToolService { get; }

    public IWorkspaceImageOperationService ImageOperationService { get; }

    /// <summary>
    /// The single process host for this bundle. Every consumer of the bundle
    /// (command execution, process leases, workspace service) uses exactly
    /// this instance; the bundle owns its disposal.
    /// </summary>
    public IWorkspaceProcessHost ProcessHost { get; }

    public IExternalTargetPathRegistry ExternalTargetPathRegistry { get; }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        List<Exception>? failures = null;
        foreach (var owned in ownedServices)
        {
            try
            {
                switch (owned)
                {
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is { Count: > 0 })
        {
            throw new AggregateException(
                "One or more scope-bound workspace services failed to dispose.",
                failures);
        }
    }
}

/// <summary>
/// Creates the scope-bound workspace service bundle for one execution. The
/// implementation composes typed dependencies; it returns typed services and
/// never leaks an <see cref="IServiceProvider"/>. A missing required
/// dependency fails at factory construction, not during a tool call.
/// </summary>
public interface IWorkspaceRuntimeServicesFactory
{
    WorkspaceRuntimeServices Create(WorkspaceExecutionScope scope);
}

/// <summary>
/// Raised when the workspace runtime composition is incomplete or when
/// scope-bound services with different identities would be mixed inside one
/// execution.
/// </summary>
public sealed class WorkspaceRuntimeCompositionException(string message)
    : InvalidOperationException(message);

/// <summary>
/// Default typed implementation: builds every scope-bound service from the
/// execution scope with constructor injection only. Receipt lifecycle fact
/// extractors and the stateless document converter are supplied at factory
/// construction (from module composition); no fallback path constructs
/// services with a different scope identity.
/// </summary>
public sealed class WorkspaceRuntimeServicesFactory(
    IReadOnlyList<IWorkspaceCommandReceiptLifecycleFactExtractor> lifecycleFactExtractors,
    IWorkspaceDocumentMarkdownConverter documentMarkdownConverter,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    IExternalTargetPathRegistryFactory externalTargetPathRegistryFactory)
    : IWorkspaceRuntimeServicesFactory
{
    public WorkspaceRuntimeServices Create(WorkspaceExecutionScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var externalTargetRegistry = externalTargetPathRegistryFactory.Create(scope.ExternalTargetRootBindings);
        var fileService = new WorkspaceFileService(
            scope.WorkspaceRoot,
            physicalPathPolicyFactory,
            scope.Scope,
            externalTargetRegistry);
        var processHost = new LocalWorkspaceProcessHost();
        var commandExecutionService = new WorkspaceCommandExecutionService(
            scope.WorkspaceRoot,
            processHost,
            physicalPathPolicyFactory,
            scope.Scope,
            lifecycleFactExtractors,
            externalTargetRegistry);
        var imageOperationService = new WorkspaceImageOperationService(
            scope.WorkspaceRoot,
            physicalPathPolicyFactory,
            scope.Scope,
            externalTargetRegistry);
        var artifactToolService = new WorkspaceArtifactToolService(
            scope.WorkspaceRoot,
            commandExecutionService,
            documentMarkdownConverter,
            physicalPathPolicyFactory,
            scope.Scope,
            imageOperationService,
            externalTargetRegistry);
        return new WorkspaceRuntimeServices(
            scope,
            fileService,
            commandExecutionService,
            artifactToolService,
            imageOperationService,
            processHost,
            externalTargetRegistry,
            ownedServices:
            [
                fileService,
                commandExecutionService,
                processHost,
                artifactToolService,
                imageOperationService
            ]);
    }
}
