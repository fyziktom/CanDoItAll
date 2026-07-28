using System.Collections.Concurrent;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Unit;

public sealed class CurrentProfileAgentFrameworkWorkspaceServiceTests
{
    [Theory]
    [InlineData(DirectoryProjectionMutation.SaveAgent)]
    [InlineData(DirectoryProjectionMutation.DeleteAgent)]
    [InlineData(DirectoryProjectionMutation.CloneAgent)]
    [InlineData(DirectoryProjectionMutation.ConvertToTemplate)]
    [InlineData(DirectoryProjectionMutation.ImportAgent)]
    [InlineData(DirectoryProjectionMutation.SaveProvider)]
    [InlineData(DirectoryProjectionMutation.DeleteProvider)]
    [InlineData(DirectoryProjectionMutation.CreateOrUpdateProviderModel)]
    [InlineData(DirectoryProjectionMutation.GrantProjectStructureAccess)]
    [InlineData(DirectoryProjectionMutation.RevokeProjectStructureAccess)]
    public async Task Directory_projection_mutation_invalidates_reference_data_on_both_sides_of_successful_synchronization(
        DirectoryProjectionMutation mutation)
    {
        var calls = new List<ProjectionRefreshCall>();
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, RecordingWorkspaceServiceProxy>();
        var workspaceProxy = (RecordingWorkspaceServiceProxy)(object)workspace;
        workspaceProxy.Calls = calls;
        var bridge = DispatchProxy.Create<IAiTechnicalAgentBridge, RecordingTechnicalAgentBridgeProxy>();
        var bridgeProxy = (RecordingTechnicalAgentBridgeProxy)(object)bridge;
        bridgeProxy.Calls = calls;
        var invalidator = new RecordingReferenceDataCacheInvalidator(calls);
        var services = new ServiceCollection();
        services.AddLogging();
        using var serviceProvider = services.BuildServiceProvider();
        var service = CreateCurrentProfileService(
            new StubWorkspaceFactory(workspace),
            bridge,
            invalidator,
            serviceProvider);

        await ExecuteDirectoryProjectionMutationAsync(service, mutation);

        Assert.Equal(
            [
                ProjectionRefreshCall.WorkspaceMutation,
                ProjectionRefreshCall.ReferenceDataInvalidation,
                ProjectionRefreshCall.DirectoryProjectionSynchronization,
                ProjectionRefreshCall.ReferenceDataInvalidation
            ],
            calls);
    }

    [Theory]
    [InlineData(ProjectAccessMutation.Grant)]
    [InlineData(ProjectAccessMutation.Revoke)]
    public async Task Project_access_mutation_succeeds_after_catalog_commit_when_secondary_projections_fail(
        ProjectAccessMutation mutation)
    {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceServiceProxy>();
        var workspaceProxy = (WorkspaceServiceProxy)(object)workspace;
        var bridge = DispatchProxy.Create<IAiTechnicalAgentBridge, TechnicalAgentBridgeProxy>();
        var bridgeProxy = (TechnicalAgentBridgeProxy)(object)bridge;
        var invalidator = new FailingReferenceDataCacheInvalidator();
        var loggerProvider = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        using var serviceProvider = services.BuildServiceProvider();
        var service = CreateCurrentProfileService(
            new StubWorkspaceFactory(workspace),
            bridge,
            invalidator,
            serviceProvider);
        var agentId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        if (mutation == ProjectAccessMutation.Grant)
        {
            await service.GrantAgentProjectStructureAccessAsync(agentId, projectId);
        }
        else
        {
            await service.RevokeAgentProjectStructureAccessAsync(agentId, projectId);
        }

        Assert.Equal(mutation == ProjectAccessMutation.Grant ? 1 : 0, workspaceProxy.GrantCallCount);
        Assert.Equal(mutation == ProjectAccessMutation.Revoke ? 1 : 0, workspaceProxy.RevokeCallCount);
        Assert.Equal(1, invalidator.InvalidationCallCount);
        Assert.Equal(1, bridgeProxy.SynchronizationCallCount);
        Assert.Contains(
            loggerProvider.Messages,
            message => message.Contains("reference-data cache invalidation failed", StringComparison.Ordinal));
        Assert.Contains(
            loggerProvider.Messages,
            message => message.Contains("agent-directory projection synchronization failed", StringComparison.Ordinal));
    }

    private static IAgentFrameworkWorkspaceService CreateCurrentProfileService(
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IAiTechnicalAgentBridge technicalAgentBridge,
        IAgentReferenceDataCacheInvalidator referenceDataCacheInvalidator,
        IServiceProvider serviceProvider)
    {
        var implementationType = typeof(AgentFrameworkModuleServiceCollectionExtensions).Assembly.GetType(
            "CanDoItAll.Modules.AgentFramework.CurrentProfileAgentFrameworkWorkspaceService",
            throwOnError: true)!;
        var loggerType = typeof(ILogger<>).MakeGenericType(implementationType);
        var logger = serviceProvider.GetRequiredService(loggerType);
        var constructor = Assert.Single(implementationType.GetConstructors());
        var timeProvider = TimeProvider.System;
        var coordinator = new AgentExecutionActivityCoordinator(
            new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                timeProvider),
            timeProvider);

        return Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(constructor.Invoke(
            [
                workspaceFactory,
                technicalAgentBridge,
                referenceDataCacheInvalidator,
                new StubDatabaseProfileRuntimeAccessor(),
                new DatabaseSwitchNotificationService(),
                coordinator,
                new FixedAgentExecutionProfileGenerationSource(
                    new DatabaseProfileGeneration(0)),
                logger
            ]));
    }

    private static async Task ExecuteDirectoryProjectionMutationAsync(
        IAgentFrameworkWorkspaceService service,
        DirectoryProjectionMutation mutation)
    {
        var entityId = Guid.NewGuid();

        switch (mutation)
        {
            case DirectoryProjectionMutation.SaveAgent:
                await service.SaveAgentAsync(new AgentEditorModel());
                break;
            case DirectoryProjectionMutation.DeleteAgent:
                await service.DeleteAgentAsync(entityId);
                break;
            case DirectoryProjectionMutation.CloneAgent:
                await service.CloneAgentAsync(entityId, "Clone");
                break;
            case DirectoryProjectionMutation.ConvertToTemplate:
                await service.ConvertToTemplateAsync(entityId, "template-key");
                break;
            case DirectoryProjectionMutation.ImportAgent:
                await service.ImportAgentAsync("agent-package");
                break;
            case DirectoryProjectionMutation.SaveProvider:
                await service.SaveProviderAsync(new ProviderProfileEditorModel());
                break;
            case DirectoryProjectionMutation.DeleteProvider:
                await service.DeleteProviderAsync(entityId);
                break;
            case DirectoryProjectionMutation.CreateOrUpdateProviderModel:
                await service.CreateOrUpdateProviderModelAsync(
                    entityId,
                    new ProviderModelMaintenanceEditorRequest(
                        "base-model",
                        "target-model",
                        "system-prompt",
                        4096));
                break;
            case DirectoryProjectionMutation.GrantProjectStructureAccess:
                await service.GrantAgentProjectStructureAccessAsync(entityId, Guid.NewGuid());
                break;
            case DirectoryProjectionMutation.RevokeProjectStructureAccess:
                await service.RevokeAgentProjectStructureAccessAsync(entityId, Guid.NewGuid());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
    }

    public enum DirectoryProjectionMutation
    {
        SaveAgent,
        DeleteAgent,
        CloneAgent,
        ConvertToTemplate,
        ImportAgent,
        SaveProvider,
        DeleteProvider,
        CreateOrUpdateProviderModel,
        GrantProjectStructureAccess,
        RevokeProjectStructureAccess
    }

    private enum ProjectionRefreshCall
    {
        WorkspaceMutation,
        ReferenceDataInvalidation,
        DirectoryProjectionSynchronization
    }

    public enum ProjectAccessMutation
    {
        Grant,
        Revoke
    }

    private sealed class StubWorkspaceFactory(IAgentFrameworkWorkspaceService service)
        : ICanDoItAllAgentWorkspaceFactory
    {
        private readonly WorkspaceScopeDescriptor scope = WorkspaceScopeDescriptor.Organization("test");

        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => service;

        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor requestedScope) => service;

        public WorkspaceScopeDescriptor GetOrganizationScope() => scope;

        public string GetWorkspaceRoot() => string.Empty;
    }

    private class RecordingWorkspaceServiceProxy : DispatchProxy
    {
        public List<ProjectionRefreshCall> Calls { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            Calls.Add(ProjectionRefreshCall.WorkspaceMutation);

            return targetMethod.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync) or
                nameof(IAgentFrameworkWorkspaceService.CloneAgentAsync) or
                nameof(IAgentFrameworkWorkspaceService.ConvertToTemplateAsync) or
                nameof(IAgentFrameworkWorkspaceService.ImportAgentAsync) or
                nameof(IAgentFrameworkWorkspaceService.SaveProviderAsync)
                    => Task.FromResult(Guid.NewGuid()),
                nameof(IAgentFrameworkWorkspaceService.DeleteAgentAsync) or
                nameof(IAgentFrameworkWorkspaceService.DeleteProviderAsync) or
                nameof(IAgentFrameworkWorkspaceService.GrantAgentProjectStructureAccessAsync) or
                nameof(IAgentFrameworkWorkspaceService.RevokeAgentProjectStructureAccessAsync)
                    => Task.CompletedTask,
                nameof(IAgentFrameworkWorkspaceService.CreateOrUpdateProviderModelAsync)
                    => Task.FromResult(new ProviderModelMaintenanceEditorResult(
                        "target-model",
                        "base-model",
                        "system-prompt",
                        4096,
                        "modelfile",
                        "ok")),
                _ => throw new NotSupportedException($"Unexpected workspace call '{targetMethod.Name}'.")
            };
        }
    }

    private class WorkspaceServiceProxy : DispatchProxy
    {
        public int GrantCallCount { get; private set; }

        public int RevokeCallCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.GrantAgentProjectStructureAccessAsync))
            {
                GrantCallCount++;
                return Task.CompletedTask;
            }

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.RevokeAgentProjectStructureAccessAsync))
            {
                RevokeCallCount++;
                return Task.CompletedTask;
            }

            throw new NotSupportedException($"Unexpected workspace call '{targetMethod.Name}'.");
        }
    }

    private class RecordingTechnicalAgentBridgeProxy : DispatchProxy
    {
        public List<ProjectionRefreshCall> Calls { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            if (targetMethod.Name == nameof(IAiTechnicalAgentBridge.SynchronizeDirectoryProjectionAsync))
            {
                Calls.Add(ProjectionRefreshCall.DirectoryProjectionSynchronization);
                return Task.CompletedTask;
            }

            throw new NotSupportedException($"Unexpected bridge call '{targetMethod.Name}'.");
        }
    }

    private class TechnicalAgentBridgeProxy : DispatchProxy
    {
        public int SynchronizationCallCount { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            if (targetMethod.Name == nameof(IAiTechnicalAgentBridge.SynchronizeDirectoryProjectionAsync))
            {
                SynchronizationCallCount++;
                throw new InvalidOperationException("Directory projection failure.");
            }

            throw new NotSupportedException($"Unexpected bridge call '{targetMethod.Name}'.");
        }
    }

    private sealed class RecordingReferenceDataCacheInvalidator(List<ProjectionRefreshCall> calls)
        : IAgentReferenceDataCacheInvalidator
    {
        public event EventHandler? Invalidated
        {
            add
            {
            }
            remove
            {
            }
        }

        public void Invalidate()
        {
            calls.Add(ProjectionRefreshCall.ReferenceDataInvalidation);
        }
    }

    private sealed class FailingReferenceDataCacheInvalidator : IAgentReferenceDataCacheInvalidator
    {
        public event EventHandler? Invalidated
        {
            add
            {
            }
            remove
            {
            }
        }

        public int InvalidationCallCount { get; private set; }

        public void Invalidate()
        {
            InvalidationCallCount++;
            throw new InvalidOperationException("Reference-data invalidation failure.");
        }
    }

    private sealed class StubDatabaseProfileRuntimeAccessor : IDatabaseProfileRuntimeAccessor
    {
        private readonly ResolvedDatabaseProfile profile = new(
            new DatabaseProfileRecord
            {
                Id = Guid.NewGuid(),
                DisplayName = "Test",
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "not-used");

        public ResolvedDatabaseProfile ResolveCurrentProfile() => profile;

        public ResolvedDatabaseProfile ResolveProfile(Guid profileId) => profile;
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => EmptyScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            public static EmptyScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
