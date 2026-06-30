using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class AppDbContextModelRegistryTests
{
    [Fact]
    public void ConfigureAssemblies_filters_out_assemblies_without_entity_type_configurations()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([
            typeof(AppDbContextModelRegistryTests).Assembly,
            typeof(Project).Assembly
        ]);

        var assemblies = AppDbContextModelRegistry.Assemblies;

        Assert.DoesNotContain(typeof(AppDbContextModelRegistryTests).Assembly, assemblies);
        Assert.Contains(typeof(Project).Assembly, assemblies);
    }
}
