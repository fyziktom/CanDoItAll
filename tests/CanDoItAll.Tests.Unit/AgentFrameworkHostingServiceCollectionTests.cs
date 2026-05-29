using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Hosting;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFrameworkHostingServiceCollectionTests
{
    [Fact]
    public void AddAgentFrameworkCore_builds_with_scope_validation()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-agent-framework-hosting-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var services = new ServiceCollection();
            services.AddAgentFrameworkCore(workspaceRoot);

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            using var scope = provider.CreateScope();

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
}
