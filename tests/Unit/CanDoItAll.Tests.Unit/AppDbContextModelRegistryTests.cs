using System.Reflection;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Infrastructure;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class AppDbContextModelRegistryTests
{
    [Fact]
    public void ConfigureAssemblies_filters_out_assemblies_without_entity_type_configurations()
    {
        using var modelRegistryScope = AppDbContextModelRegistry.UseIsolatedAssembliesForTesting();

        AppDbContextModelRegistry.ConfigureAssemblies([
            typeof(AppDbContextModelRegistryTests).Assembly,
            typeof(Project).Assembly
        ]);

        var assemblies = AppDbContextModelRegistry.Assemblies;

        Assert.DoesNotContain(typeof(AppDbContextModelRegistryTests).Assembly, assemblies);
        Assert.Contains(typeof(Project).Assembly, assemblies);
    }

    [Fact]
    public void ConfigureAssemblies_rejects_partial_type_loading_without_changing_the_registered_model() {
        using var modelRegistryScope = AppDbContextModelRegistry.UseIsolatedAssembliesForTesting();
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(Project).Assembly]);
        var previousAssemblies = AppDbContextModelRegistry.Assemblies;
        var previousModelKey = AppDbContextModelRegistry.ModelCacheKey;
        var availableConfiguration = typeof(Project).Assembly.GetTypes().First(type => type.GetInterfaces().Any(contract =>
            contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)));
        var loaderFailure = new TypeLoadException("Fixture dependency MissingModelDependency.Record could not be loaded.");
        var partialAssembly = new PartialModelAssembly(new ReflectionTypeLoadException([availableConfiguration, null], [loaderFailure]));

        var failure = Assert.Throws<InvalidOperationException>(() =>
            AppDbContextModelRegistry.ConfigureAssemblies([partialAssembly]));

        Assert.Contains(partialAssembly.FullName, failure.Message, StringComparison.Ordinal);
        Assert.Same(partialAssembly.Failure, failure.InnerException);
        Assert.Contains(loaderFailure.Message, failure.ToString(), StringComparison.Ordinal);
        Assert.Same(previousAssemblies, AppDbContextModelRegistry.Assemblies);
        Assert.Equal(previousModelKey, AppDbContextModelRegistry.ModelCacheKey);
    }

    private sealed class PartialModelAssembly(ReflectionTypeLoadException failure) : Assembly {
        public ReflectionTypeLoadException Failure { get; } = failure;

        public override string FullName => "PartialModelFixture";

        public override Type[] GetTypes() => throw Failure;
    }
}
