using Bunit;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.CapabilityAuthoring.UiSandbox;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;
using AccessKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests;

public sealed class CapabilitySetupAttributionTests {
    public enum EditSequence { SameRevision, DifferentRevision, ReturnToOriginal }

    [Theory]
    [InlineData(false, EditSequence.SameRevision)]
    [InlineData(false, EditSequence.DifferentRevision)]
    [InlineData(false, EditSequence.ReturnToOriginal)]
    [InlineData(true, EditSequence.SameRevision)]
    [InlineData(true, EditSequence.DifferentRevision)]
    [InlineData(true, EditSequence.ReturnToOriginal)]
    public async Task Unknown_current_attempt_cannot_relabel_previous_evidence_or_replay_setup(bool tool, EditSequence edits) {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(tool ? AuthoringScenario.ToolHttp : AuthoringScenario.McpLogical);
        var accepted = fixture.Read(fixture.CapabilityId!.Value);
        var calls = 0;
        var operations = WithSetup(fixture, () => ++calls == 1
            ? Task.FromResult(Result(true, "original"))
            : Task.FromException<McpSetupTestResult>(new InvalidOperationException("Controlled lost acknowledgement.")));
        var cut = await RenderAsync(context, fixture, operations);
        var session = Session(cut);
        await TestAsync(cut);
        Assert.Contains("original diagnostic", cut.Markup, StringComparison.Ordinal);
        if (!tool) {
            Assert.Contains("original_tool", cut.Markup, StringComparison.Ordinal);
        }
        if (edits != EditSequence.SameRevision) {
            await EditAsync(cut, "configuration-b");
            Assert.Empty(session.SetupDiagnostics);
            Assert.DoesNotContain("original diagnostic", cut.Markup, StringComparison.Ordinal);
        }
        if (edits == EditSequence.ReturnToOriginal) {
            await EditAsync(cut, "fixture-server");
        }

        await TestAsync(cut);
        Assert.True(session.SetupUnknown);
        Assert.Null(session.SetupSucceeded);
        Assert.DoesNotContain("original diagnostic", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("original_tool", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(session.SetupDiagnostics);
        Assert.Empty(session.SetupTools);
        Assert.Contains("outcome is unknown", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(session.TestSetupAsync);
        Assert.Equal(accepted, fixture.Read(fixture.CapabilityId.Value));
        await cut.InvokeAsync(() => cut.Find("form").SubmitAsync());
        Assert.Equal(2, calls);
        Assert.Equal(1, fixture.Saves);
        Assert.Equal(accepted.Id, session.AcceptedId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Known_unsuccessful_attempt_replaces_evidence_and_preserves_catalog_facts(bool tool) {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(tool ? AuthoringScenario.ToolHttp : AuthoringScenario.McpLogical);
        var accepted = fixture.Read(fixture.CapabilityId!.Value);
        var calls = 0;
        var cut = await RenderAsync(context, fixture, WithSetup(fixture, () => Task.FromResult(
            ++calls == 1 ? Result(true, "original") : Result(false, "current"))));
        await TestAsync(cut);
        await EditAsync(cut, "configuration-b");
        await TestAsync(cut);
        var session = Session(cut);
        Assert.False(session.SetupUnknown);
        Assert.False(session.SetupSucceeded);
        Assert.Equal("current diagnostic", Assert.Single(session.SetupDiagnostics).MaskedDetail);
        Assert.Contains("current diagnostic", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("original diagnostic", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("original_tool", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(accepted, fixture.Read(fixture.CapabilityId.Value));
        Assert.Equal(0, fixture.Saves);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Editing_while_acknowledgement_is_pending_hides_old_facts_and_keeps_unknown_guard(bool tool) {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(tool ? AuthoringScenario.ToolHttp : AuthoringScenario.McpLogical);
        var pending = new TaskCompletionSource<McpSetupTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var cut = await RenderAsync(context, fixture, WithSetup(fixture, () => ++calls == 1
            ? Task.FromResult(Result(true, "original")) : pending.Task));
        await TestAsync(cut);
        await EditAsync(cut, "configuration-b");
        var attempt = TestAsync(cut);
        var session = Session(cut);
        cut.WaitForAssertion(() => Assert.True(session.Busy));
        await EditAsync(cut, "configuration-c");
        pending.SetException(new InvalidOperationException("Controlled lost acknowledgement."));
        await attempt;
        Assert.True(session.SetupHistorical);
        Assert.True(session.SetupUnknown);
        Assert.Empty(session.SetupDiagnostics);
        Assert.Empty(session.SetupTools);
        Assert.DoesNotContain("original diagnostic", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(session.TestSetupAsync);
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Retired_attempt_cannot_publish_into_reopened_editor_or_another_editor(bool fault, bool close) {
        using var context = CreateContext();
        using var lifetime = new CancellationTokenSource();
        using var replacement = new CancellationTokenSource();
        var fixture = new CapabilityAuthoringScenario(AuthoringScenario.McpLogical);
        var pending = new TaskCompletionSource<McpSetupTestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var operations = WithSetup(fixture, () => ++calls == 1 ? pending.Task : Task.FromResult(Result(true, "independent")));
        var cut = await RenderAsync(context, fixture, operations, lifetime.Token);
        var old = Session(cut);
        var attempt = TestAsync(cut);
        cut.WaitForAssertion(() => Assert.True(old.Busy));
        var other = await RenderAsync(context, fixture, operations);
        await TestAsync(other);
        if (close) {
            await cut.InvokeAsync(() => cut.FindAll("button").Single(button => button.TextContent.Contains("Cancel", StringComparison.Ordinal)).ClickAsync());
        } else {
            lifetime.Cancel();
        }
        cut.Render(p => p.Add(x => x.OwnerCancellationToken, replacement.Token));
        await SelectConfigurationAsync(cut);
        var current = Session(cut);
        Assert.NotSame(old, current);
        if (fault) {
            pending.SetException(new InvalidOperationException("Retired acknowledgement."));
        } else {
            pending.SetResult(Result(true, "retired"));
        }
        await attempt;
        Assert.False(current.SetupUnknown);
        Assert.Null(current.SetupSucceeded);
        Assert.Empty(current.SetupDiagnostics);
        Assert.Empty(current.SetupTools);
        Assert.DoesNotContain("retired diagnostic", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("independent diagnostic", other.Markup, StringComparison.Ordinal);
        Assert.Contains("independent_tool", other.Markup, StringComparison.Ordinal);
        Assert.Equal(2, calls);
        Assert.Equal(0, fixture.Saves);
    }

    private static CapabilityAuthoringOperations WithSetup(CapabilityAuthoringScenario fixture, Func<Task<McpSetupTestResult>> attempt) => fixture.Operations with {
        TestMcp = (_, _) => attempt(),
        TestTool = async (_, _, _) => {
            var result = await attempt();
            return new(result.IsSuccess, new(AccessKind.Tool, new("fixture")), result.CorrelationId, result.Diagnostics);
        }
    };

    private static McpSetupTestResult Result(bool succeeded, string origin) => new(succeeded,
        new(AccessKind.McpServer, new("fixture")), new("fixture-server"), origin,
        succeeded ? [new(new($"{origin}_tool"), "Safe fixture tool")] : [], [],
        [new(CapabilityDiagnosticCategory.McpListTools, CapabilityValidationSeverity.Info,
            AccessKind.McpServer, new("fixture"), null, "configuration", null, null, null, null, null,
            origin, $"{origin} diagnostic", "Inspect the matching attempt.")], true);

    private static CapabilityAuthoringSession Session(IRenderedComponent<CapabilityAuthoringForm> cut)
        => cut.FindComponent<CapabilityConfigurationFields>().Instance.Session;

    private static Task TestAsync(IRenderedComponent<CapabilityAuthoringForm> cut)
        => cut.InvokeAsync(() => cut.Find("[data-testid='agents-capability-details-test-setup']").ClickAsync());

    private static Task EditAsync(IRenderedComponent<CapabilityAuthoringForm> cut, string value) => cut.InvokeAsync(() => {
        var selector = Session(cut).Draft.Model.Kind == CanDoItAll.AgentFramework.Models.CapabilityKind.Tool
            ? "[data-testid='agents-capability-details-tool-http-endpoint']"
            : "[data-testid='agents-capability-details-mcp-server']";
        var input = Session(cut).Draft.Model.Kind == CanDoItAll.AgentFramework.Models.CapabilityKind.Tool
            ? value == "fixture-server" ? "https://fixture.invalid/tool" : $"https://fixture.invalid/{value}"
            : value;
        cut.Find(selector).Input(input);
    });

    private static Task SelectConfigurationAsync(IRenderedComponent<CapabilityAuthoringForm> cut)
        => cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));

    private static async Task<IRenderedComponent<CapabilityAuthoringForm>> RenderAsync(BunitContext context,
        CapabilityAuthoringScenario fixture, CapabilityAuthoringOperations operations, CancellationToken token = default) {
        var cut = context.Render<CapabilityAuthoringForm>(p => p.Add(x => x.Operations, operations)
            .Add(x => x.Mode, fixture.Mode).Add(x => x.CapabilityId, fixture.CapabilityId)
            .Add(x => x.OwnerCancellationToken, token));
        await SelectConfigurationAsync(cut);
        return cut;
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
