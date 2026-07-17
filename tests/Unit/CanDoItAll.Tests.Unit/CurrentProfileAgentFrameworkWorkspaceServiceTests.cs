using System.Collections.Concurrent;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Unit;

public sealed class CurrentProfileAgentFrameworkWorkspaceServiceTests
{
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

        return Assert.IsAssignableFrom<IAgentFrameworkWorkspaceService>(constructor.Invoke(
            [workspaceFactory, technicalAgentBridge, referenceDataCacheInvalidator, logger]));
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
