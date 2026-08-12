using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Hosting;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

[Trait("Category", "UnixRuntimePortability")]
public sealed class AgentFrameworkHostingServiceCollectionTests
{
    [Fact]
    public async Task AddAgentFrameworkCore_scopes_external_target_authority_and_consumers()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-agent-framework-authority-{Guid.NewGuid():N}");
        var workspaceRoot = Path.Combine(testRoot, "workspace");
        var externalRoot = Path.Combine(testRoot, "external");
        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(externalRoot);

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddAgentFrameworkCore(workspaceRoot);

            AssertScoped<IExternalTargetPathRegistry>(services);
            AssertScoped<IWorkspaceFileService>(services);
            AssertScoped<IPluginWorkspaceFiles>(services);
            AssertScoped<IWorkspacePathResolutionService>(services);
            AssertScoped<IWorkspaceCommandExecutionService>(services);
            AssertScoped<IWorkspaceImageOperationService>(services);
            AssertScoped<IWorkspaceArtifactToolService>(services);

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            await using var firstScope = provider.CreateAsyncScope();
            await using var secondScope = provider.CreateAsyncScope();

            var firstRegistry = firstScope.ServiceProvider.GetRequiredService<IExternalTargetPathRegistry>();
            var sameScopeRegistry = firstScope.ServiceProvider.GetRequiredService<IExternalTargetPathRegistry>();
            var secondRegistry = secondScope.ServiceProvider.GetRequiredService<IExternalTargetPathRegistry>();

            Assert.Same(firstRegistry, sameScopeRegistry);
            Assert.NotSame(firstRegistry, secondRegistry);
            Assert.True(firstRegistry.TryCreateAlias(externalRoot, out var alias));
            Assert.Equal(
                ExternalTargetAliasResolutionKind.Resolved,
                firstRegistry.TryResolve(alias, out var resolvedRoot, out _));
            Assert.Equal(Path.GetFullPath(externalRoot), resolvedRoot);
            Assert.Equal(
                ExternalTargetAliasResolutionKind.Unbound,
                secondRegistry.TryResolve(alias, out _, out _));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddAgentFrameworkCore_builds_with_scope_validation()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-agent-framework-hosting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var services = new ServiceCollection();
            services.AddAgentFrameworkCore(workspaceRoot);

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            await using var scope = provider.CreateAsyncScope();

            Assert.IsType<MafWorkflowCompiler>(scope.ServiceProvider.GetRequiredService<IWorkflowMafCompiler>());
            Assert.IsType<CompositeWorkflowExecutorExecutionObserver>(
                scope.ServiceProvider.GetRequiredService<IWorkflowExecutorExecutionObserver>());
            var backendCatalog = scope.ServiceProvider.GetRequiredService<IWorkflowRuntimeBackendCatalog>();
            var inProcessBackend = backendCatalog.GetRequiredBackend(WorkflowRuntimeBackendKind.InProcess);
            Assert.True(inProcessBackend.IsRegistered);
            Assert.True(inProcessBackend.IsRunnable);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    private static void AssertScoped<TService>(IServiceCollection services)
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(TService));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public async Task AddAgentFrameworkCore_registers_real_activity_pipeline_and_explicit_workspace_identity()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-agent-framework-activity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);
        var workspaceScope = WorkspaceScopeDescriptor.Project("hosting-test");
        var workspaceIdentity = new AgentExecutionActivityWorkspaceIdentity(
            Guid.NewGuid(),
            workspaceScope,
            new DatabaseProfileGeneration(0));

        try
        {
            var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
            services.AddAgentFrameworkCore(
                workspaceRoot,
                workspaceScope,
                workspaceIdentity);

            await using var provider = services.BuildServiceProvider(
                new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true
                });
            await using var firstScope = provider.CreateAsyncScope();
            await using var secondScope = provider.CreateAsyncScope();

            var firstStream = firstScope.ServiceProvider.GetRequiredService<
                PartitionedSequencedStream<
                    AgentExecutionActivityStreamId,
                    AgentExecutionActivity>>();
            var secondStream = secondScope.ServiceProvider.GetRequiredService<
                PartitionedSequencedStream<
                    AgentExecutionActivityStreamId,
                    AgentExecutionActivity>>();
            var coordinator = firstScope.ServiceProvider
                .GetRequiredService<AgentExecutionActivityCoordinator>();
            var coordinatorContract = firstScope.ServiceProvider
                .GetRequiredService<IAgentExecutionActivityCoordinator>();
            var reader = secondScope.ServiceProvider
                .GetRequiredService<IAgentExecutionActivityReader>();
            var resolvedIdentity = firstScope.ServiceProvider
                .GetRequiredService<AgentExecutionActivityWorkspaceIdentity>();
            var workspaceService = firstScope.ServiceProvider
                .GetRequiredService<IAgentFrameworkWorkspaceService>();
            var activityExecutionService = firstScope.ServiceProvider
                .GetRequiredService<
                    IAgentFrameworkWorkspaceActivityExecutionService>();

            Assert.Same(firstStream, secondStream);
            Assert.Same(coordinator, coordinatorContract);
            Assert.Same(coordinator, reader);
            Assert.Same(workspaceIdentity, resolvedIdentity);
            Assert.IsType<AgentFrameworkWorkspaceService>(workspaceService);
            Assert.Same(workspaceService, activityExecutionService);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddAgentFrameworkCore_shares_reference_data_invalidation_across_scopes()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-agent-framework-hosting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var services = new ServiceCollection();
            services.AddAgentFrameworkCore(workspaceRoot);

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true
            });
            await using var firstScope = provider.CreateAsyncScope();
            await using var secondScope = provider.CreateAsyncScope();
            var firstCache = firstScope.ServiceProvider.GetRequiredService<AgentReferenceDataCache>();
            var secondCache = secondScope.ServiceProvider.GetRequiredService<AgentReferenceDataCache>();
            var firstInvalidator = firstScope.ServiceProvider.GetRequiredService<IAgentReferenceDataCacheInvalidator>();
            var secondInvalidator = secondScope.ServiceProvider.GetRequiredService<IAgentReferenceDataCacheInvalidator>();
            var firstLoadCount = 0;
            var secondLoadCount = 0;
            var request = new AgentReferenceDataRequest(AgentReferenceDataSections.Agents);
            var now = DateTimeOffset.UtcNow;
            var ttl = TimeSpan.FromMinutes(1);

            await firstCache.GetOrCreateAsync(request, now, ttl, LoadFirstAsync);
            await secondCache.GetOrCreateAsync(request, now, ttl, LoadSecondAsync);
            await firstCache.GetOrCreateAsync(request, now, ttl, LoadFirstAsync);
            await secondCache.GetOrCreateAsync(request, now, ttl, LoadSecondAsync);

            Assert.NotSame(firstCache, secondCache);
            Assert.Same(firstInvalidator, secondInvalidator);
            Assert.IsType<AgentReferenceDataInvalidationHub>(firstInvalidator);
            Assert.Equal(1, firstLoadCount);
            Assert.Equal(1, secondLoadCount);

            firstInvalidator.Invalidate();
            await firstCache.GetOrCreateAsync(request, now, ttl, LoadFirstAsync);
            await secondCache.GetOrCreateAsync(request, now, ttl, LoadSecondAsync);

            Assert.Equal(2, firstLoadCount);
            Assert.Equal(2, secondLoadCount);

            Task<AgentReferenceDataSnapshot> LoadFirstAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                firstLoadCount++;
                return Task.FromResult(CreateEmptyReferenceDataSnapshot());
            }

            Task<AgentReferenceDataSnapshot> LoadSecondAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                secondLoadCount++;
                return Task.FromResult(CreateEmptyReferenceDataSnapshot());
            }
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AddAgentFrameworkCore_catalog_service_rejects_unknown_executor()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-agent-framework-hosting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var services = new ServiceCollection();
            services.AddAgentFrameworkCore(workspaceRoot);

            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            await using var scope = provider.CreateAsyncScope();
            var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.SaveDefinitionAsync(
                new WorkflowDefinitionSaveRequest(
                    Id: null,
                    ExpectedVersionId: null,
                    "Unknown executor workflow",
                    "Should fail before runtime dispatch.",
                    WorkflowLifecycleStatus.Active,
                    CreateExecutorWorkflowGraph(new WorkflowExecutorId("missing.executor")),
                    WorkflowSettings.Default.DefaultRuntimePolicy)));

            Assert.Contains("missing.executor", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    private static WorkflowGraph CreateExecutorWorkflowGraph(WorkflowExecutorId executorId)
    {
        var start = new WorkflowNodeId("start");
        var tool = new WorkflowNodeId("tool");
        var end = new WorkflowNodeId("end");
        return new WorkflowGraph(
            start,
            [
                CreateNode(start, WorkflowNodeKind.Start, resultShape: WorkflowValueShape.Text),
                new(
                    tool,
                    WorkflowNodeKind.Executor,
                    "Tool",
                    [],
                    new WorkflowNodeSettings(
                        ComponentId: null,
                        AgentId: null,
                        SubworkflowId: null,
                        ExternalRequestKind: null,
                        Instructions: string.Empty,
                        InputShape: WorkflowValueShape.Text,
                        ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")) with
                    {
                        ExecutorId = executorId,
                        ExecutorSettingsJson = "{}",
                        ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
                    }),
                CreateNode(end, WorkflowNodeKind.End, inputShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
            ],
            [
                CreateEdge("start-tool", start, tool),
                CreateEdge("tool-end", tool, end)
            ]);
    }

    private static AgentReferenceDataSnapshot CreateEmptyReferenceDataSnapshot()
    {
        return new AgentReferenceDataSnapshot(
            AgentReferenceDataSections.Agents,
            [],
            [],
            new Dictionary<Guid, ProviderProfile>(),
            DateTimeOffset.UtcNow,
            TimeSpan.Zero);
    }

    private static WorkflowNode CreateNode(
        WorkflowNodeId id,
        WorkflowNodeKind kind,
        WorkflowValueShape? inputShape = null,
        WorkflowValueShape? resultShape = null)
        => new(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: inputShape,
                ResultShape: resultShape));

    private static WorkflowEdge CreateEdge(
        string id,
        WorkflowNodeId source,
        WorkflowNodeId target)
        => new(
            new WorkflowEdgeId(id),
            source,
            SourcePortId: null,
            target,
            TargetPortId: null,
            WorkflowEdgeKind.Direct,
            ConditionExpression: string.Empty)
        {
            Routing = WorkflowEdgeRouting.Always
        };
}
