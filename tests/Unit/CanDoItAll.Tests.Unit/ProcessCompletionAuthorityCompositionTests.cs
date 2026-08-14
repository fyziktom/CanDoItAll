using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.DependencyInjection;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class ProcessCompletionAuthorityCompositionTests
{
    [Fact]
    public async Task DefaultRuntimeComposition_ResolvesScopedProductCompletionAuthority()
    {
        await using var testEnvironment = CanDoItAllTestEnvironment.Create(
            "process-completion-authority-composition");
        var profile = testEnvironment.CreateInMemoryProfile("unit", "process-completion-authority");
        var configuration = TestApplicationBootstrap.BuildConfiguration(
            profile,
            new Dictionary<string, string?>
            {
                ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath
            });
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            testEnvironment.CreateHostEnvironment("CanDoItAll.Tests.Unit"));

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var firstScope = provider.CreateAsyncScope();
        var firstFactory =
            firstScope.ServiceProvider.GetRequiredService<WorkspaceFileInspectionScopeFactory>();

        Assert.NotNull(
            firstScope.ServiceProvider.GetRequiredService<ProcessProductCompletionPathGate>());
        Assert.Same(
            firstFactory,
            firstScope.ServiceProvider.GetRequiredService<WorkspaceFileInspectionScopeFactory>());

        await using var secondScope = provider.CreateAsyncScope();
        Assert.NotSame(
            firstFactory,
            secondScope.ServiceProvider.GetRequiredService<WorkspaceFileInspectionScopeFactory>());
    }
}
