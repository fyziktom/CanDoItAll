using CanDoItAll.Modules.Processes;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessObservationIntentResolverTests
{
    [Fact]
    public async Task ResolveAsync_requires_definition_or_run_for_focused_details()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IProcessObservationIntentResolver>();

        var plan = await resolver.ResolveAsync(new ProcessObservationIntent(
            ProjectId: null,
            ProcessDefinitionId: null,
            ProcessRunId: null,
            StepRunId: null,
            FocusKind: ProcessObservationFocusKind.QualityReview));

        Assert.Equal(ProcessObservationIntentResolutionStatus.Ambiguous, plan.Status);
        Assert.Empty(plan.DialogDescriptors);
    }
}
