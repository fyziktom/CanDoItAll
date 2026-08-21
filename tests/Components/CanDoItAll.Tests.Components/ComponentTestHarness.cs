using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Web.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Components;

internal sealed class ComponentTestHarness : IAsyncDisposable
{
    private readonly bool _ownsTestEnvironment;

    private ComponentTestHarness(
        CanDoItAllTestEnvironment testEnvironment,
        bool ownsTestEnvironment,
        TestDatabaseProfile activeProfile,
        BunitContext context)
    {
        TestEnvironment = testEnvironment;
        _ownsTestEnvironment = ownsTestEnvironment;
        ActiveProfile = activeProfile;
        RootPath = testEnvironment.RootPath;
        Context = context;
    }

    public string RootPath { get; }

    public CanDoItAllTestEnvironment TestEnvironment { get; }

    public TestDatabaseProfile ActiveProfile { get; }

    public BunitContext Context { get; }

    public static async Task<ComponentTestHarness> CreateAsync(
        Action<IServiceCollection>? configureServices = null,
        TestHarnessOptions? options = null)
    {
        if (options?.ActiveProfile is not null && options.TestEnvironment is null)
        {
            throw new InvalidOperationException("TestEnvironment must be supplied when ActiveProfile is provided.");
        }

        var ownsTestEnvironment = options?.TestEnvironment is null;
        var testEnvironment = options?.TestEnvironment ?? CanDoItAllTestEnvironment.Create("candoitall-component-tests");
        var activeProfile = options?.ActiveProfile ?? testEnvironment.CreatePostgreSqlProfile("primary");

        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.AddAuthorization();
        var configuration = TestApplicationBootstrap.BuildConfiguration(activeProfile, options?.ConfigurationOverrides);

        TestApplicationBootstrap.ConfigureDefaultServices(
            context.Services,
            configuration,
            testEnvironment.CreateHostEnvironment("CanDoItAll.Tests.Components"));
        context.Services.AddAgentFrameworkUi();
        context.Services.AddScoped<TuningCoordinator>();
        context.Services.AddHttpClient<DevelopmentManagerClient>();
        configureServices?.Invoke(context.Services);

        if (string.IsNullOrWhiteSpace(configuration["Database:Provider"]) &&
            string.IsNullOrWhiteSpace(configuration["Database:ConnectionString"]))
        {
            await using var setupScope = context.Services.CreateAsyncScope();
            var profileService = setupScope.ServiceProvider.GetRequiredService<IDatabaseProfileService>();
            var saveResult = await profileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
                activeProfile,
                "PostgreSQL bootstrap"));
            if (saveResult.IsFailure)
            {
                throw new InvalidOperationException(string.Join(" ", saveResult.Errors.Select(error => error.Message)));
            }
        }

        await TestApplicationBootstrap.InitializeSchemaAsync(
            context.Services,
            options?.SchemaModules ?? TestSchemaBootstrapModules.Default);

        return new ComponentTestHarness(testEnvironment, ownsTestEnvironment, activeProfile, context);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();

        if (_ownsTestEnvironment)
        {
            await TestEnvironment.DisposeAsync();
        }
    }
}
