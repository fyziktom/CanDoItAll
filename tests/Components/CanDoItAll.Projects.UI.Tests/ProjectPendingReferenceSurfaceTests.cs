using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CanDoItAll.Tests.Components.ProjectsUi;

public sealed class ProjectPendingReferenceSurfaceTests {
    [Theory]
    [InlineData(ProjectPendingStage.Accepted, "does not undo")]
    [InlineData(ProjectPendingStage.Unconfirmed, "may already have committed")]
    [InlineData(ProjectPendingStage.Prepared, "deliberately destroys")]
    public async Task Disposal_requires_exact_explicit_confirmation_and_exports_only_reference_metadata(ProjectPendingStage stage, string explanation) {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var reference = new ProjectPendingReference(new(new(Guid.NewGuid()), new(long.MaxValue), Guid.NewGuid(), new("pc1:" + new string('b', 64))),
            new(27), stage, null, stage == ProjectPendingStage.Unconfirmed, true, 1234);
        ProjectPendingReference? selected = null;
        ProjectPendingReference? discarded = null;
        var cut = context.Render<ProjectPendingReferenceSurface>(parameters => parameters
            .Add(component => component.IsOpen, true).Add(component => component.Items, [reference])
            .Add(component => component.Select, value => selected = value).Add(component => component.Discard, value => discarded = value));
        Assert.Empty(cut.FindAll("[data-testid='project-reference-confirm']"));
        Assert.Contains("current context unavailable", cut.Markup);
        var export = cut.Find("[data-testid='project-reference-export']").GetAttribute("href")!;
        using var json = JsonDocument.Parse(Uri.UnescapeDataString(export[(export.IndexOf(',') + 1)..]));
        Assert.Equal(reference.Operation.Id, json.RootElement.GetProperty("operation").GetProperty("id").GetGuid());
        Assert.Equal(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture), json.RootElement.GetProperty("operation").GetProperty("epoch").GetString());
        Assert.False(json.RootElement.TryGetProperty("command", out _));
        Assert.False(json.RootElement.TryGetProperty("acceptedPayload", out _));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-reference-select']").ClickAsync(new()));
        Assert.Same(reference, selected);
        Assert.Null(discarded);
        cut.Render(parameters => parameters.Add(component => component.Selected, selected));
        Assert.Contains(explanation, cut.Find("[data-testid='project-reference-confirmation']").TextContent);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-reference-confirm']").ClickAsync(new()));
        Assert.Same(reference, discarded);
    }
}
