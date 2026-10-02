using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers.UI;
using CanDoItAll.AgentFramework.Providers.UiSandbox;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProviderProfilesUi;

public sealed class ProviderRendererTests {
    [Fact]
    public async Task Six_tabs_and_actual_children_share_one_context_and_immediate_draft() {
        using var context = Context();
        var view = new ProviderScenarioSession();
        var cut = context.Render<ProviderProfilesSurface>(p => p.Add(c => c.View, view));
        var editor = view.Editor;
        Assert.Equal(6, cut.FindAll("[role='tab']").Count);
        cut.Find("[data-testid='providers-name-input']").Input("Žluťoučký 東京 🧭");
        cut.Find("[data-testid='providers-model-input']").Input("raw-model");
        await Tab(cut, ProviderEditorSection.Runtime);
        cut.Find("[data-testid='providers-config-json']").Input("{\"extension\": [1,2]}");
        cut.Find("[data-testid='providers-notes']").Input("Unblurred notes");
        cut.Find("[data-testid='providers-suggested-models']").Input("one\ntwo,");
        foreach (var section in ProviderEditorSections.All) {
            await Tab(cut, section.Section);
            Assert.All(cut.FindComponents<EditForm>(), form => Assert.Same(editor.Context, form.Instance.EditContext));
            Assert.Same(editor, view.Editor);
        }
        Assert.Equal("Žluťoučký 東京 🧭", editor.Model.Name);
        Assert.Equal("raw-model", editor.Model.DefaultModel);
        Assert.Equal("Unblurred notes", editor.Model.Notes);
        Assert.Equal(["one", "two"], editor.Model.SuggestedModels);
        Assert.Equal("one\ntwo,", editor.SuggestedModelsText);
        Assert.Single(cut.FindComponents<ProviderModelPricingEditor>());
        Assert.Single(cut.FindComponents<ProviderThinkingSurface>());
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Invalid_json_and_numeric_text_survive_tabs_and_block_save() {
        using var context = Context();
        var view = new ProviderScenarioSession();
        var cut = context.Render<ProviderProfilesSurface>(p => p.Add(c => c.View, view));
        await Tab(cut, ProviderEditorSection.Prices);
        cut.Find("[data-testid='provider-pricing-input-0']").Input("1e-");
        Assert.Equal(1.25m, view.Editor.Model.ModelPrices[0].InputPerMillionTokensUsd);
        await Tab(cut, ProviderEditorSection.Runtime);
        cut.Find("[data-testid='providers-config-json']").Input("{unclosed");
        await Tab(cut, ProviderEditorSection.Thinking);
        await Tab(cut, ProviderEditorSection.Prices);
        Assert.Equal("1e-", cut.Find("[data-testid='provider-pricing-input-0']").GetAttribute("value"));
        await cut.InvokeAsync(() => Assert.False(view.Editor.Validate()));
        await cut.InvokeAsync(() => view.ExecuteAsync(new(Target(view), ProviderEditorAction.Save)));
        Assert.Equal(0, view.SaveCount);
        Assert.Equal("{unclosed", view.Editor.Model.ConfigurationJson);
        cut.Find("[data-testid='provider-pricing-input-0']").Input("0");
        await Tab(cut, ProviderEditorSection.Runtime);
        cut.Find("[data-testid='providers-config-json']").Input("{}");
        await cut.InvokeAsync(() => Assert.True(view.Editor.Validate()));
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Price_raw_state_follows_row_identity_through_removal_and_rename() {
        using var context = Context();
        var view = new ProviderScenarioSession();
        view.Editor.Model.ModelPrices.Add(new() { Model = "second", InputPerMillionTokensUsd = 8m });
        var second = view.Editor.Model.ModelPrices[1];
        var cut = context.Render<ProviderProfilesSurface>(p => p.Add(c => c.View, view));
        await Tab(cut, ProviderEditorSection.Prices);
        cut.Find("[data-testid='provider-pricing-input-1']").Input("invalid");
        await cut.Find("[data-testid='provider-pricing-row-0'] button").ClickAsync();
        Assert.Same(second, Assert.Single(view.Editor.Model.ModelPrices));
        Assert.Equal("invalid", cut.Find("[data-testid='provider-pricing-input-0']").GetAttribute("value"));
        cut.Find("[data-testid='provider-pricing-model-0']").Input("renamed-second");
        Assert.Equal("renamed-second", second.Model);
        Assert.Equal(8m, second.InputPerMillionTokensUsd);
        await cut.Find("[data-testid='provider-pricing-row-0'] button").ClickAsync();
        Assert.Empty(view.Editor.Context.GetValidationMessages());
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Every_rate_and_missing_value_remains_editable_before_blur() {
        using var context = Context();
        var view = new ProviderScenarioSession();
        var cut = context.Render<ProviderProfilesSurface>(p => p.Add(c => c.View, view));
        await Tab(cut, ProviderEditorSection.Prices);
        var price = view.Editor.Model.ModelPrices[0];
        (string Field, Func<decimal?> Value)[] rates = [
            ("input", () => price.InputPerMillionTokensUsd), ("cached", () => price.CachedInputPerMillionTokensUsd),
            ("cache-write", () => price.CacheWritePerMillionTokensUsd), ("output", () => price.OutputPerMillionTokensUsd),
            ("image-input", () => price.ImageInputPerMillionTokensUsd), ("cached-image-input", () => price.CachedImageInputPerMillionTokensUsd),
            ("long-input", () => price.LongContextInputPerMillionTokensUsd), ("long-cached", () => price.LongContextCachedInputPerMillionTokensUsd),
            ("long-cache-write", () => price.LongContextCacheWritePerMillionTokensUsd), ("long-output", () => price.LongContextOutputPerMillionTokensUsd)
        ];
        foreach (var (field, value) in rates) {
            cut.Find($"[data-testid='provider-pricing-{field}-0']").Input("12.345");
            Assert.Equal(12.345m, value());
            cut.Find($"[data-testid='provider-pricing-{field}-0']").Input(string.Empty);
            if (field is "input" or "cached" or "output") {
                await cut.InvokeAsync(() => Assert.False(view.Editor.Validate()));
                Assert.Equal(12.345m, value());
                cut.Find($"[data-testid='provider-pricing-{field}-0']").Input("0");
            } else {
                Assert.Null(value());
            }
        }
        cut.Find("[data-testid='provider-pricing-long-threshold-0']").Input("123456");
        Assert.Equal(123456, price.LongContextThresholdTokens);
        Assert.Contains("unpriced-model", cut.Find("[data-testid='provider-pricing-table']").TextContent);
        await context.DisposeRenderedComponentsAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Thinking_cancel_or_origin_change_cannot_mutate_configuration(bool changeOrigin) {
        using var context = Context();
        var view = new ProviderScenarioSession();
        var cut = context.Render<ProviderProfilesSurface>(p => p.Add(c => c.View, view));
        await Tab(cut, ProviderEditorSection.Thinking);
        cut.Find("[data-testid='provider-thinking-search']").Input("qwen3:8b");
        await cut.Find("button[aria-label='Edit thinking for qwen3:8b']").ClickAsync();
        var original = view.Editor.Model.ConfigurationJson;
        cut.Find("[data-testid='thinking-automatic']").Change(false);
        if (changeOrigin) {
            await cut.InvokeAsync(() => {
                view.Editor.Model.ConfigurationJson = "{}";
                view.Editor.Notify(nameof(view.Editor.Model.ConfigurationJson));
                view.Editor.Model.ConfigurationJson = original;
                view.Editor.Notify(nameof(view.Editor.Model.ConfigurationJson));
            });
            await cut.Find("[data-testid='thinking-apply']").ClickAsync();
            Assert.Contains("changed", cut.Find("[data-testid='thinking-edit-error']").TextContent);
        } else {
            await cut.FindAll("button").Single(button => button.TextContent.Trim() == "Cancel").ClickAsync();
        }
        Assert.Equal(original, view.Editor.Model.ConfigurationJson);
        Assert.Equal(original, view.ReadSaved(view.State.ProviderId!.Value).ConfigurationJson);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Thinking_apply_is_draft_only_and_preserves_unknown_extensions() {
        using var context = Context();
        var view = new ProviderScenarioSession();
        var original = view.ReadSaved(view.State.ProviderId!.Value).ConfigurationJson;
        var cut = context.Render<ProviderProfilesSurface>(p => p.Add(c => c.View, view));
        await Tab(cut, ProviderEditorSection.Thinking);
        cut.Find("[data-testid='provider-thinking-search']").Input("qwen3:8b");
        await cut.Find("button[aria-label='Edit thinking for qwen3:8b']").ClickAsync();
        cut.Find("[data-testid='thinking-automatic']").Change(false);
        cut.Find("[data-testid='thinking-supported']").Change(false);
        await cut.Find("[data-testid='thinking-apply']").ClickAsync();
        Assert.Equal(AgentThinkingEffortSupportStatus.Unsupported, Assert.Single(ProviderModelThinkingConfiguration.Read(view.Editor.Model.ConfigurationJson)).Status);
        Assert.Contains("customExtension", view.Editor.Model.ConfigurationJson);
        Assert.Equal(original, view.ReadSaved(view.State.ProviderId!.Value).ConfigurationJson);
        Assert.Equal(0, view.SaveCount);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Retired_input_and_action_cannot_modify_reacquired_target_or_second_editor() {
        using var context = Context();
        var view = new ProviderScenarioSession();
        var other = new ProviderScenarioSession();
        var cut = context.Render<ProviderProfilesSurface>(p => p.Add(c => c.View, view));
        var second = context.Render<ProviderProfilesSurface>(p => p.Add(c => c.View, other));
        var staleName = cut.FindComponents<TextBox>().Single(box => box.FindAll("[data-testid='providers-name-input']").Count == 1).Instance.ValueChanged;
        var target = Target(view);
        var id = view.State.ProviderId!.Value;
        await view.SelectAsync(view.Providers[1].Id);
        await view.SelectAsync(id);
        cut.Render();
        var expected = view.Editor.Model.Name;
        await cut.InvokeAsync(() => staleName.InvokeAsync("Late input"));
        await cut.InvokeAsync(() => view.ExecuteAsync(new(target, ProviderEditorAction.Save)));
        Assert.Equal(expected, view.Editor.Model.Name);
        Assert.Equal(expected, other.Editor.Model.Name);
        Assert.Equal(0, view.SaveCount);
        Assert.NotSame(view.Editor.Context, other.Editor.Context);
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Disposed_renderer_rejects_retained_input_and_thinking_callbacks() {
        using var context = Context();
        var view = new ProviderScenarioSession();
        var cut = context.Render<ProviderProfilesSurface>(p => p.Add(c => c.View, view));
        var name = view.Editor.Model.Name;
        var configuration = view.Editor.Model.ConfigurationJson;
        var input = cut.FindComponents<TextBox>().Single(box => box.FindAll("[data-testid='providers-name-input']").Count == 1).Instance.ValueChanged;
        await Tab(cut, ProviderEditorSection.Thinking);
        cut.Find("[data-testid='provider-thinking-search']").Input("qwen3:8b");
        await cut.Find("button[aria-label='Edit thinking for qwen3:8b']").ClickAsync();
        cut.Find("[data-testid='thinking-automatic']").Change(false);
        cut.Find("[data-testid='thinking-supported']").Change(false);
        var apply = cut.FindComponents<Button>().Single(button => button.FindAll("[data-testid='thinking-apply']").Count == 1).Instance.Click;
        await context.DisposeRenderedComponentsAsync();
        await cut.InvokeAsync(() => input.InvokeAsync("Disposed input"));
        await cut.InvokeAsync(() => apply.InvokeAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs()));
        Assert.Equal(name, view.Editor.Model.Name);
        Assert.Equal(configuration, view.Editor.Model.ConfigurationJson);
        Assert.Equal(0, view.SaveCount);
    }

    [Theory]
    [InlineData(ProviderScenario.Loading)]
    [InlineData(ProviderScenario.Empty)]
    [InlineData(ProviderScenario.Large)]
    [InlineData(ProviderScenario.Missing)]
    [InlineData(ProviderScenario.PartialSecrets)]
    [InlineData(ProviderScenario.ReadOnly)]
    [InlineData(ProviderScenario.Warning)]
    [InlineData(ProviderScenario.Unknown)]
    public async Task Fixture_states_render_real_surface_with_explicit_restrictions(ProviderScenario scenario) {
        using var context = Context();
        var view = new ProviderScenarioSession(scenario);
        var cut = context.Render<ProviderProfilesSurface>(p => p.Add(c => c.View, view));
        Assert.Single(cut.FindAll("[data-testid='agents-provider-profiles-panel']"));
        if (scenario == ProviderScenario.ReadOnly) {
            Assert.Empty(cut.FindAll("[data-testid='providers-save']"));
            Assert.Empty(cut.FindAll("button[aria-label^='Edit thinking for ' ]"));
            Assert.Contains("Shared qwen3:8b", cut.Markup);
        }
        if (scenario == ProviderScenario.PartialSecrets) {
            Assert.Single(cut.FindAll("[data-testid='providers-secret-warning']"));
            Assert.True(view.CanEdit);
        }
        if (scenario is ProviderScenario.Missing or ProviderScenario.Loading) {
            await cut.InvokeAsync(view.RefreshAsync);
            cut.Render();
            Assert.True(view.CanEdit);
        }
        if (scenario is ProviderScenario.Unknown or ProviderScenario.Warning) {
            Assert.True(view.WritesBlocked);
        }
        await context.DisposeRenderedComponentsAsync();
    }

    [Fact]
    public async Task Held_save_captures_all_fields_without_overwriting_later_typing() {
        var view = new ProviderScenarioSession(ProviderScenario.Held);
        var target = Target(view);
        view.Editor.Model.Name = "Captured";
        var save = view.ExecuteAsync(new(target, ProviderEditorAction.Save));
        Assert.True(view.IsBusy);
        view.Editor.Model.Name = "Later typing";
        view.Release();
        await save;
        Assert.Equal("Captured", view.ReadSaved(target.ProviderId!.Value).Name);
        Assert.Equal("Later typing", view.Editor.Model.Name);
        Assert.False(view.IsBusy);
    }

    private static ProviderEditorTarget Target(ProviderScenarioSession view) => new(view.State.ProviderId, view.Activation, view.Editor.Context);
    private static Task Tab(IRenderedComponent<ProviderProfilesSurface> cut, ProviderEditorSection section) =>
        cut.FindAll("[role='tab']")[ProviderEditorSections.IndexOf(section)].ClickAsync();
    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
