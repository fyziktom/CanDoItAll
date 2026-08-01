using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Support;

public sealed class TestHarnessOptions
{
    public CanDoItAllTestEnvironment? TestEnvironment { get; init; }

    public TestDatabaseProfile? ActiveProfile { get; init; }

    public TestSchemaBootstrapModules SchemaModules { get; init; } = TestSchemaBootstrapModules.Full;

    public IReadOnlyDictionary<string, string?>? ConfigurationOverrides { get; init; }

    public Action<IServiceCollection>? ConfigureServices { get; init; }
}
