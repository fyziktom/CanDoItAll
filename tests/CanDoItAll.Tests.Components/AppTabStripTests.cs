using Bunit;
using CanDoItAll.ComponentKit.Components;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components;

public sealed class AppTabStripTests : TestContext
{
    [Fact]
    public void Renders_state_indicators_and_invokes_actions()
    {
        var activated = string.Empty;
        var movedRight = string.Empty;
        var toggledSleep = string.Empty;

        var cut = RenderComponent<AppTabStrip>(parameters => parameters
            .Add(component => component.Items,
            [
                new WorkbenchTabState("projects", "Projects", "/projects", IsPinned: true, IsSleeping: true),
                new WorkbenchTabState("validation", "Validation", "/validation")
            ])
            .Add(component => component.ActiveTabId, "projects")
            .Add(component => component.Activate, EventCallback.Factory.Create<string>(this, value => activated = value))
            .Add(component => component.MoveRight, EventCallback.Factory.Create<string>(this, value => movedRight = value))
            .Add(component => component.ToggleSleep, EventCallback.Factory.Create<string>(this, value => toggledSleep = value)));

        Assert.Contains("pin", cut.Markup);
        Assert.Contains("zZ", cut.Markup);

        cut.Find("button[title='Move right']").Click();
        cut.Find("button[title='Sleep or wake tab']").Click();
        cut.Find("button").Click();

        Assert.Equal("projects", movedRight);
        Assert.Equal("projects", toggledSleep);
        Assert.Equal("projects", activated);
    }
}
