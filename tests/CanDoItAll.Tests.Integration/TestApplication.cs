using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration;

internal sealed class TestApplication : IAsyncDisposable
{
    private readonly bool _ownsTestEnvironment;

    private TestApplication(
        CanDoItAllTestEnvironment testEnvironment,
        bool ownsTestEnvironment,
        TestDatabaseProfile activeProfile,
        ServiceProvider services)
    {
        TestEnvironment = testEnvironment;
        _ownsTestEnvironment = ownsTestEnvironment;
        ActiveProfile = activeProfile;
        RootPath = testEnvironment.RootPath;
        Services = services;
    }

    public string RootPath { get; }

    public CanDoItAllTestEnvironment TestEnvironment { get; }

    public TestDatabaseProfile ActiveProfile { get; }

    public ServiceProvider Services { get; }

    public static async Task<TestApplication> CreateAsync(TestHarnessOptions? options = null)
    {
        if (options?.ActiveProfile is not null && options.TestEnvironment is null)
        {
            throw new InvalidOperationException("TestEnvironment must be supplied when ActiveProfile is provided.");
        }

        var ownsTestEnvironment = options?.TestEnvironment is null;
        var testEnvironment = options?.TestEnvironment ?? CanDoItAllTestEnvironment.Create("candoitall-tests");
        var activeProfile = options?.ActiveProfile ?? testEnvironment.CreatePostgreSqlProfile("primary");
        var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests",
            options?.SchemaModules ?? TestSchemaBootstrapModules.Full,
            options?.ConfigurationOverrides,
            options?.ConfigureServices);

        return new TestApplication(testEnvironment, ownsTestEnvironment, activeProfile, provider);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();

        if (_ownsTestEnvironment)
        {
            await TestEnvironment.DisposeAsync();
        }
    }
}
