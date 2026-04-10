using CanDoItAll.Mcp.Processes;
using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessesMcpIntegrationTests
{
    [Fact]
    public async Task ProcessesMcp_tools_list_seeded_definitions_and_run_detail()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var seedService = scope.ServiceProvider.GetRequiredService<ProcessDevelopmentSeedService>();
        var seedResult = await seedService.SeedBaselineAsync();

        Assert.True(seedResult.IsSuccess);
        Assert.NotNull(seedResult.Value);

        var tools = new ProcessesTools(
            new ProcessesCoordinator(application.Services.GetRequiredService<IServiceScopeFactory>()),
            NullLogger<ProcessesTools>.Instance);
        var seededRunId = seedResult.Value.SeededRunIds.First();

        var definitions = await ProcessesMcpIntegrationTestsAccessor.AssertOkAsync(
            tools.ProcessesDefinitionsListAsync());
        var primaryDefinition = Assert.Single(definitions, item => item.Id == seedResult.Value!.PrimaryDefinitionId);

        var runDetail = await ProcessesMcpIntegrationTestsAccessor.AssertOkAsync(
            tools.ProcessesRunDetailGetAsync(seededRunId));

        Assert.Equal(seededRunId, runDetail.Run.Id);
        Assert.Equal(primaryDefinition.Id, runDetail.Run.ProcessDefinitionId);
        Assert.NotEmpty(runDetail.StepRuns);
        Assert.NotEmpty(runDetail.WorkBriefs);
    }
}

internal static class ProcessesMcpIntegrationTestsAccessor
{
    public static async Task<T> AssertOkAsync<T>(Task<CanDoItAll.Mcp.Core.Contracts.McpToolEnvelope<T>> task)
    {
        var envelope = await task;
        Assert.True(envelope.Ok, envelope.Error?.Message ?? "Tool returned a failed envelope.");
        return envelope.Data!;
    }
}
