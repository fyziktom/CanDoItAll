using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.ScenarioSeeder;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = ScenarioSeederOptions.Parse(args, Directory.GetCurrentDirectory());
        await using var serviceProvider = await ScenarioSeederHost.BuildServiceProviderAsync(options);
        await using var scope = serviceProvider.CreateAsyncScope();

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("CanDoItAll.ScenarioSeeder");
        logger.LogInformation(
            "Seeding scenario {ScenarioName} into profile root {ProfileRoot}. Database {DatabasePath}",
            options.ScenarioName,
            options.ProfileRootPath,
            options.DatabasePath);

        object result = options.ScenarioName switch
        {
            ScenarioSeederOptions.AgentShowcaseCalculatorScenario => await scope.ServiceProvider
                .GetRequiredService<AgentShowcaseCalculatorSeeder>()
                .SeedAsync(),
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

        return 0;
    }
}
