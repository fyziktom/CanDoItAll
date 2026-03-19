using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ValidationServiceIntegrationTests
{
    [Fact]
    public async Task RunAsync_persists_findings_and_indexes_validation_result()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var validationService = scope.ServiceProvider.GetRequiredService<ValidationService>();
        var searchIndexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();

        var result = await validationService.RunAsync(new ValidationRunEditorModel
        {
            ValidationType = ValidationType.Architecture,
            ArtifactTitle = "Architecture review",
            ArtifactRoute = "/projects",
            SourceContent = "Architecture draft that talks about module seams but leaves implementation shallow."
        });

        Assert.True(result.IsSuccess);

        var run = await validationService.GetRunAsync(result.Value);
        Assert.NotEmpty(run.Findings);
        Assert.Contains(run.Findings, finding => finding.RuleCode == "missing-dependencies");

        var searchResults = await searchIndexService.SearchAsync("Architecture review");
        Assert.Contains(searchResults, item => item.Route.Contains("/validation?runId=", StringComparison.Ordinal));
    }
}
