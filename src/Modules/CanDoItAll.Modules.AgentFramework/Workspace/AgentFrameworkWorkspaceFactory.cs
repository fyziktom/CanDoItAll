using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    IProviderRuntimeProfileSource providerRuntimeProfileSource,
    IAgentProviderCredentialResolver providerCredentialResolver,
    ICapabilityProofService capabilityProofService,
    IAgentExecutionActivityCoordinator activityCoordinator,
    IAgentExecutionPreparationCache executionPreparationCache,
    IAgentExecutionProfileGenerationSource executionProfileGenerationSource,
    IDatabaseSwitchNotificationService databaseSwitchNotificationService,
    ILogger<CanDoItAllAgentWorkspaceFactory> logger,
    IOptions<ProcessMockAgentOptions> processMockAgentOptions) :
    ICanDoItAllAgentWorkspaceFactory,
    IDisposable
{
    private readonly Lock workspaceServicesGate = new();
    private readonly Dictionary<
        AgentExecutionActivityWorkspaceIdentity,
        AgentFrameworkWorkspaceService> workspaceServices = [];
    private bool disposed;
    private bool subscribedToProfileChanges;

    public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
    {
        lock (workspaceServicesGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }

        return serviceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
    }

    public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var profile = databaseProfileRuntimeAccessor
            .ResolveCurrentProfile()
            .Profile;
        var profileGeneration =
            executionProfileGenerationSource.GetGeneration();
        var workspaceRoot = GetWorkspaceRoot();
        var confirmedProfile = databaseProfileRuntimeAccessor
            .ResolveCurrentProfile()
            .Profile;
        var confirmedProfileGeneration =
            executionProfileGenerationSource.GetGeneration();
        if (confirmedProfile.Id != profile.Id ||
            !string.Equals(
                confirmedProfile.Runtime.Fingerprint,
                profile.Runtime.Fingerprint,
                StringComparison.Ordinal) ||
            confirmedProfileGeneration != profileGeneration)
        {
            throw new InvalidOperationException(
                "The database profile changed while resolving the agent workspace.");
        }

        var workspaceIdentity = new AgentExecutionActivityWorkspaceIdentity(
            confirmedProfile.Id,
            scope,
            confirmedProfileGeneration);
        lock (workspaceServicesGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            EnsureProfileChangeSubscription();

            if (workspaceServices.TryGetValue(
                    workspaceIdentity,
                    out var existing))
            {
                return existing;
            }

            var workspaceService = CreateWorkspaceService(
                scope,
                workspaceIdentity,
                workspaceRoot);
            workspaceServices[workspaceIdentity] = workspaceService;
            return workspaceService;
        }
    }

    private AgentFrameworkWorkspaceService CreateWorkspaceService(
        WorkspaceScopeDescriptor scope,
        AgentExecutionActivityWorkspaceIdentity workspaceIdentity,
        string workspaceRoot)
    {
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
            serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AgentFrameworkWorkspaceService>>(),
            activityCoordinator,
            workspaceIdentity,
            executionPreparationCache,
            executionProfileGenerationSource,
            new WorkspaceExecutionRunProcessLeaseCleaner(
                store,
                commandExecutionService),
            providerProfileService,
            providerProfileRegistry,
            providerCredentialResolver,
            providerDiagnosticsService,
            governanceBridge,
            new NullAgentExecutionEventSink(),
            checkpointBridge,
            processHost,
            serviceProvider.GetRequiredService<IAgentExecutionCancellationRegistry>(),
            workspacePathResolutionService: new WorkspacePathResolutionService(workspaceRoot, scope),
            providerRuntimeProfileSource: providerRuntimeProfileSource);
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

    public void Dispose()
    {
        AgentFrameworkWorkspaceService[] ownedWorkspaceServices;
        lock (workspaceServicesGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (subscribedToProfileChanges)
            {
                databaseSwitchNotificationService.Changed -=
                    HandleDatabaseProfileChanged;
                subscribedToProfileChanges = false;
            }

            ownedWorkspaceServices = [.. workspaceServices.Values];
            workspaceServices.Clear();
        }

        DisposeWorkspaceServices(
            ownedWorkspaceServices,
            throwOnFailure: true);
    }

    private void EnsureProfileChangeSubscription()
    {
        if (subscribedToProfileChanges)
        {
            return;
        }

        databaseSwitchNotificationService.Changed +=
            HandleDatabaseProfileChanged;
        subscribedToProfileChanges = true;
    }

    private void HandleDatabaseProfileChanged(
        object? sender,
        DatabaseProfileChangedNotification notification)
    {
        AgentFrameworkWorkspaceService[] supersededWorkspaceServices;
        lock (workspaceServicesGate)
        {
            if (disposed)
            {
                return;
            }

            var supersededIdentities = workspaceServices.Keys
                .Where(identity =>
                    identity.DatabaseProfileId != notification.CurrentProfileId ||
                    identity.DatabaseProfileGeneration.Value !=
                        notification.Generation)
                .ToArray();
            supersededWorkspaceServices = supersededIdentities
                .Select(identity => workspaceServices[identity])
                .ToArray();
            foreach (var identity in supersededIdentities)
            {
                workspaceServices.Remove(identity);
            }
        }

        DisposeWorkspaceServices(
            supersededWorkspaceServices,
            throwOnFailure: false);
    }

    private void DisposeWorkspaceServices(
        IReadOnlyList<AgentFrameworkWorkspaceService> workspaceServicesToDispose,
        bool throwOnFailure)
    {
        List<Exception>? failures = null;
        foreach (var workspaceService in workspaceServicesToDispose)
        {
            try
            {
                workspaceService.Dispose();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
        {
            var aggregate = new AggregateException(
                "One or more agent workspace services failed to dispose.",
                failures);
            if (throwOnFailure)
            {
                throw aggregate;
            }

            logger.LogError(
                aggregate,
                "Failed to dispose {WorkspaceServiceCount} superseded agent workspace service(s) after a database profile change.",
                workspaceServicesToDispose.Count);
        }
    }
}
