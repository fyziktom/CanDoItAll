using Bunit;
using CanDoItAll.AgentFramework.CapabilityAuthoring.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.Extensions.DependencyInjection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Editing.UI;
using Microsoft.AspNetCore.Components.Forms;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests;

public sealed class CapabilityAuthoringFormTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rendered_comparison_preserves_pending_input_and_enables_apply_only_after_explicit_configuration_review(bool incomplete) {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(AuthoringScenario.ToolHttp);
        var current = await fixture.Operations.Load(fixture.CapabilityId!.Value, default);
        current.ExpectedFingerprint = "original-ui-fingerprint";
        var json = JsonNode.Parse(current.ConfigurationJson)!.AsObject();
        json["future"] = "private-extension-original";
        current.ConfigurationJson = json.ToJsonString();
        var writes = 0;
        var operations = fixture.Operations with {
            Load = (_, _) => Task.FromResult(CapabilityAuthoringSubmission.Capture(current).ToEditorModel()),
            Save = (submitted, _) => {
                writes++;
                return Task.FromResult<CapabilityAuthoringSaveOutcome>(new CapabilityAuthoringSaveOutcome.Accepted(submitted with { ExpectedFingerprint = "accepted-ui-fingerprint" }));
            }
        };
        var cut = context.Render<CapabilityAuthoringForm>(parameters => parameters.Add(component => component.Operations, operations)
            .Add(component => component.Mode, fixture.Mode).Add(component => component.CapabilityId, fixture.CapabilityId));
        var edit = cut.FindComponent<EditForm>().Instance.EditContext;
        cut.Find("[data-testid='agents-capability-details-name']").Input("Retained local name 東京");
        cut.Find("[data-testid='agents-capability-details-tags-input']").Input("unfinished tag");
        if (incomplete) {
            await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
            cut.Find("[data-testid='agents-capability-details-tool-http-timeout']").Input("unfinished timeout");
        }
        current.Description = "Peer description";
        current.ExpectedFingerprint = "peer-ui-fingerprint";
        json["future"] = "private-extension-peer";
        current.ConfigurationJson = json.ToJsonString();
        cut.Render(parameters => parameters.Add(component => component.ChangeSequence, 1));
        await cut.Find("[data-testid='capability-compare']").ClickAsync();
        var comparison = cut.Find("[data-testid='capability-comparison']");
        Assert.DoesNotContain("private-extension-original", comparison.TextContent);
        Assert.DoesNotContain("private-extension-peer", comparison.TextContent);
        Assert.True(cut.Find("[data-testid='capability-comparison-apply']").HasAttribute("disabled"));
        if (incomplete) {
            var keep = cut.Find("[data-testid='capability-comparison-keep-input']");
            var labelId = keep.GetAttribute("aria-labelledby");
            Assert.False(string.IsNullOrWhiteSpace(labelId));
            Assert.Equal("Keep my unfinished configuration locally", cut.Find($"[id='{labelId}']").TextContent.Trim());
            keep.Change(true);
        } else {
            var field = cut.FindComponent<CapabilityComparisonSurface>().FindComponent<MergeFieldView<CapabilityExecutionConfiguration>>();
            await field.FindAll("button").Single(button => button.TextContent.Trim() == "Use current").ClickAsync();
        }
        Assert.False(cut.Find("[data-testid='capability-comparison-apply']").HasAttribute("disabled"));
        await cut.Find("[data-testid='capability-comparison-apply']").ClickAsync();
        Assert.Same(edit, cut.FindComponent<EditForm>().Instance.EditContext);
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(0));
        Assert.Equal("unfinished tag", cut.Find("[data-testid='agents-capability-details-tags-input']").GetAttribute("value"));
        Assert.Equal("Peer description", ((CapabilityEditorModel)edit!.Model).Description);
        Assert.Equal(0, writes);
        await cut.Find("form").SubmitAsync();
        Assert.Equal(incomplete ? 0 : 1, writes);
        if (incomplete) {
            await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
            Assert.Equal("unfinished timeout", cut.Find("[data-testid='agents-capability-details-tool-http-timeout']").GetAttribute("value"));
            Assert.NotEmpty(edit.GetValidationMessages());
        }
        Assert.Equal(0, fixture.Setups);
    }

    [Fact]
    public async Task Setup_completion_updates_the_parent_and_later_input_invalidates_the_badge() {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(AuthoringScenario.ToolHttp);
        var cut = context.Render<CapabilityAuthoringForm>(p => p.Add(x => x.Operations, fixture.Operations)
            .Add(x => x.Mode, fixture.Mode).Add(x => x.CapabilityId, fixture.CapabilityId));
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
        await cut.Find("[data-testid='agents-capability-details-test-setup']").ClickAsync();
        cut.WaitForAssertion(() => Assert.Contains("Setup passed", cut.Markup, StringComparison.Ordinal));
        Assert.False(cut.Find("[data-testid='agents-capability-details-save']").HasAttribute("disabled"));
        cut.Find("[data-testid='agents-capability-details-tool-test-input']").Input("{\"later\":true}");
        Assert.DoesNotContain("Setup passed", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("previous diagnostic does not apply", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(1, fixture.Setups);
    }

    [Fact]
    public void Loaded_identity_uses_model_values() {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(AuthoringScenario.ToolHttp);
        var cut = context.Render<CapabilityAuthoringForm>(p => p.Add(x => x.Operations, fixture.Operations)
            .Add(x => x.Mode, fixture.Mode).Add(x => x.CapabilityId, fixture.CapabilityId));
        Assert.Equal("Fixture capability", cut.Find("[data-testid='agents-capability-details-name']").GetAttribute("value"));
        Assert.Equal("fixture", cut.Find("[data-testid='agents-capability-details-key']").GetAttribute("value"));
    }

    [Fact]
    public async Task Invalid_numeric_text_survives_tab_changes_and_blocks_save_before_blur() {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(AuthoringScenario.ToolHttp);
        var cut = context.Render<CapabilityAuthoringForm>(p => p.Add(x => x.Operations, fixture.Operations)
            .Add(x => x.Mode, fixture.Mode).Add(x => x.CapabilityId, fixture.CapabilityId));
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
        cut.Find("[data-testid='agents-capability-details-tool-http-timeout']").Input("invalid");
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(2));
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
        Assert.Equal("invalid", cut.Find("[data-testid='agents-capability-details-tool-http-timeout']").GetAttribute("value"));
        await cut.Find("form").SubmitAsync();
        Assert.Equal(0, fixture.Saves);
        Assert.Contains("Timeout", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
