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
        var seeder = scope.ServiceProvider.GetRequiredService<AgentFrameworkIntegrationSimulationSeeder>();

        logger.LogInformation(
            "Seeding simulation into profile root {ProfileRoot}. Database {DatabasePath}",
            options.ProfileRootPath,
            options.DatabasePath);

        var result = await seeder.SeedAsync();
        Console.WriteLine(JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            }));

        return 0;
    }
}
