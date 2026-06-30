using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.ScenarioSeeder;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = ScenarioSeederOptions.Parse(args, Directory.GetCurrentDirectory());
        var serviceProvider = await ScenarioSeederHost.BuildServiceProviderAsync(options);
        var scope = serviceProvider.CreateAsyncScope();

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("CanDoItAll.ScenarioSeeder");
        logger.LogInformation(
            "Seeding scenario {ScenarioName} into profile root {ProfileRoot}. Workspace {WorkspaceRoot}",
            options.ScenarioName,
            options.ProfileRootPath,
            options.WorkspaceRootPath);

        object result = !string.IsNullOrWhiteSpace(options.ActionName)
            ? await ExecuteActionAsync(scope.ServiceProvider, options)
            : options.ScenarioName switch
        {
            _ => await scope.ServiceProvider
                .GetRequiredService<AgentFrameworkIntegrationSimulationSeeder>()
                .SeedAsync()
        };

        Console.WriteLine(JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            }));
        await Console.Out.FlushAsync();

        return 0;
    }

    private static async Task<object> ExecuteActionAsync(
        IServiceProvider serviceProvider,
        ScenarioSeederOptions options)
    {
        await Task.CompletedTask;
        throw new InvalidOperationException($"Unknown scenario-seeder action '{options.ActionName}'.");
    }
}
