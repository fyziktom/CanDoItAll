using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.WorkflowAuthoring;

public sealed class WorkflowRouteFieldsTests {
    [Theory]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("invalid index")]
    public async Task Invalid_fanout_index_remains_visible_and_cannot_be_accepted_as_unspecified(string value) {
        await using var context = new BunitContext();
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        var state = new WorkflowRouteDraft { Kind = WorkflowRouteKind.FanOutSelector, FanOutTargetIndex = 2 };
        var cut = context.Render<WorkflowRouteFields>(p => p.Add(x => x.State, state));
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-route-fanout-index']").Change(value));
        Assert.Equal(value, state.FanOutIndexText);
        Assert.True(state.HasInvalidFanOutIndex);
        Assert.Equal(value, cut.Find("[data-testid='workflow-route-fanout-index']").GetAttribute("value"));
        Assert.Contains(WorkflowRouteDraft.InvalidIndexMessage, cut.Markup);
        await cut.InvokeAsync(() => cut.Find("[data-testid='workflow-route-fanout-index']").Change("3"));
        Assert.Equal(3, state.FanOutTargetIndex);
        Assert.False(state.HasInvalidFanOutIndex);
    }
}
