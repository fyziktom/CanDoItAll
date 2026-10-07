using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.CapabilityAuthoring.UiSandbox;
using CanDoItAll.Components.BaseLib;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI.Tests;

public sealed class CapabilityModeTests {
    [Theory]
    [InlineData(AuthoringScenario.McpStdio)]
    [InlineData(AuthoringScenario.McpHttp)]
    [InlineData(AuthoringScenario.McpSse)]
    [InlineData(AuthoringScenario.McpLogical)]
    [InlineData(AuthoringScenario.SkillFile)]
    [InlineData(AuthoringScenario.SkillInline)]
    [InlineData(AuthoringScenario.SkillRegistered)]
    [InlineData(AuthoringScenario.ToolProcess)]
    [InlineData(AuthoringScenario.ToolHttp)]
    [InlineData(AuthoringScenario.RawPlugin)]
    [InlineData(AuthoringScenario.BuiltInTool)]
    public async Task Details_round_trip_all_three_tabs_without_implicit_setup(AuthoringScenario scenario) {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(scenario);
        var cut = context.Render<CapabilityAuthoringForm>(p => p.Add(x => x.Operations, fixture.Operations)
            .Add(x => x.Mode, fixture.Mode).Add(x => x.CapabilityId, fixture.CapabilityId));
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(1));
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(2));
        Assert.NotEmpty(cut.Find("[data-testid='agents-capability-details-raw-json']").TextContent);
        await cut.InvokeAsync(() => cut.FindComponent<Tabs>().Instance.SelectedIndexChanged.InvokeAsync(0));
        if (scenario == AuthoringScenario.BuiltInTool) {
            Assert.True(cut.Find("[data-testid='agents-capability-details-name']").HasAttribute("disabled"));
        }
        await cut.Find("form").SubmitAsync();
        Assert.Equal(1, fixture.Saves);
        Assert.Equal(1, fixture.DefinitionCount);
        Assert.Equal(fixture.CapabilityId, fixture.LastSavedId);
        Assert.Equal(0, fixture.Setups);
    }

    [Theory]
    [InlineData(AuthoringScenario.NewMcp, "stdio")]
    [InlineData(AuthoringScenario.NewMcp, "http")]
    [InlineData(AuthoringScenario.NewMcp, "sse")]
    [InlineData(AuthoringScenario.NewMcp, "logical")]
    [InlineData(AuthoringScenario.NewSkill, "FilePath")]
    [InlineData(AuthoringScenario.NewSkill, "Inline")]
    [InlineData(AuthoringScenario.NewSkill, "Upload")]
    [InlineData(AuthoringScenario.NewSkill, "Registered")]
    [InlineData(AuthoringScenario.NewTool, "externalHttp")]
    [InlineData(AuthoringScenario.NewTool, "externalProcess")]
    public async Task Wizard_creates_once_after_all_steps_with_the_selected_configuration(AuthoringScenario scenario, string mode) {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(scenario);
        Guid? accepted = null;
        var cut = context.Render<CapabilityAuthoringForm>(p => p.Add(x => x.Operations, fixture.Operations)
            .Add(x => x.Mode, fixture.Mode).Add(x => x.InitialKind, fixture.InitialKind).Add(x => x.Completed, id => accepted = id));
        Input("name", "Authored definition");
        await cut.Find("form").SubmitAsync();
        Assert.Equal(0, fixture.Saves);
        switch (scenario) {
            case AuthoringScenario.NewMcp:
                cut.Find("[data-testid='agents-capability-setup-mcp-transport']").Change(mode);
                Input("mcp-server", "fixture-server");
                Input("mcp-command", "fixture-helper");
                Input("mcp-endpoint", "https://fixture.invalid/mcp");
                Input("mcp-tools", "fixture");
                break;
            case AuthoringScenario.NewSkill:
                cut.Find("[data-testid='agents-capability-setup-skill-mode']").Change(mode);
                if (mode == "FilePath") {
                    Input("skill-root", "skills/fixture");
                } else if (mode == "Registered") {
                    Input("skill-registered-type", "Fixture.Skill");
                } else {
                    if (mode == "Upload") {
                        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("Instructions Ω", "SKILL.md"));
                        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid='agents-capability-setup-next']").HasAttribute("disabled")));
                    } else {
                        Input("inline-instructions", "Instructions Ω");
                    }
                    Input("inline-name", "fixture");
                    Input("inline-resources", "[{\"name\":\"guide.txt\",\"content\":\"Resource Ω\"}]");
                }
                break;
            case AuthoringScenario.NewTool:
                cut.Find("[data-testid='agents-capability-setup-tool-kind']").Change(mode);
                if (mode == "externalHttp") {
                    Input("tool-http-endpoint", "https://fixture.invalid/tool");
                } else {
                    Input("tool-process-command", "fixture-helper");
                    Input("tool-process-allowed-executables", "fixture-helper");
                }
                break;
        }
        await cut.Find("[data-testid='agents-capability-setup-next']").ClickAsync();
        Assert.NotEmpty(cut.FindAll("[data-testid='agents-capability-setup-create']"));
        await cut.Find("form").SubmitAsync();
        Assert.NotNull(accepted);
        Assert.Equal(1, fixture.Saves);
        Assert.Equal(1, fixture.DefinitionCount);
        Assert.Equal(0, fixture.Setups);
        using var json = JsonDocument.Parse(fixture.Read(accepted.Value).ConfigurationJson);
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
        if (mode is "Inline" or "Upload") {
            Assert.Equal("Resource Ω", json.RootElement.GetProperty("inlineSkill").GetProperty("resources")[0].GetProperty("content").GetString());
        }
        void Input(string field, string value) => cut.Find($"[data-testid='agents-capability-setup-{field}']").Input(value);
    }

    [Fact]
    public async Task Parent_failure_after_create_returns_the_same_saved_identity_without_another_write() {
        using var context = CreateContext();
        var fixture = new CapabilityAuthoringScenario(AuthoringScenario.McpLogical);
        var completions = new List<Guid>();
        var cut = context.Render<CapabilityAuthoringForm>(p => p.Add(x => x.Operations, fixture.Operations)
            .Add(x => x.Mode, fixture.Mode).Add(x => x.CapabilityId, fixture.CapabilityId)
            .Add(x => x.Completed, id => {
                completions.Add(id);
                throw new IOException("Controlled parent read failure.");
            }));
        await cut.Find("form").SubmitAsync();
        Assert.Contains("parent could not refresh", cut.Markup, StringComparison.Ordinal);
        await cut.Find("[data-testid='capability-use-saved']").ClickAsync();
        Assert.Equal(new[] { fixture.CapabilityId!.Value, fixture.CapabilityId.Value }, completions);
        Assert.Equal(1, fixture.Saves);
    }

    private static BunitContext CreateContext() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
