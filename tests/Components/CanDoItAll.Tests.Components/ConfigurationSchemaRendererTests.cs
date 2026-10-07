using System.Reflection;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Configuration.UI;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Shell;

[Trait("Category", "HostPlatform")]
public sealed class ConfigurationSchemaRendererTests {
    [Fact]
    public async Task A_held_callback_keeps_its_original_state_when_a_new_editor_is_acquired() {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        var first = new ConfigurationState();
        var second = new ConfigurationState();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ConfigurationState? submitted = null;
        var schema = new ConfigurationSchema("1.0", [new("title", "Title", ConfigurationFieldType.Text, false, "")]);
        var cut = context.Render<ConfigurationSchemaRenderer>(p => p.Add(c => c.Schema, schema).Add(c => c.State, first)
            .Add(c => c.StateChanged, async state => {
                submitted = state;
                await completion.Task;
            }));
        var pending = cut.Find("input").InputAsync(new() { Value = "Žlutý 東京" });
        Assert.Same(first, submitted);
        cut.Render(p => p.Add(c => c.State, second));
        completion.SetResult();
        await pending;
        Assert.Equal("Žlutý 東京", first.GetText("title"));
        Assert.Equal(string.Empty, second.GetText("title"));
        Assert.Equal(string.Empty, cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Raw_validation_messages_are_encoded_and_empty_schema_is_explicit() {
        using var context = new BunitContext();
        context.Services.AddCanDoItAllBaseLib();
        var schema = new ConfigurationSchema("1.0", [new("title", "Title", ConfigurationFieldType.Text, false, "")]);
        var cut = context.Render<ConfigurationSchemaRenderer>(p => p.Add(c => c.Schema, schema).Add(c => c.State, new())
            .Add(c => c.Validation, new ConfigurationValidationResult([new("title", "<script>unsafe()</script> Žlutý")] )));
        Assert.Empty(cut.FindAll("script"));
        Assert.Equal("<script>unsafe()</script> Žlutý", cut.Find(".workflow-canvas-error").TextContent);
        cut.Render(p => p.Add(c => c.Schema, ConfigurationSchema.Empty()));
        Assert.Contains("No configuration fields are defined.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluated_leaf_and_sandbox_have_no_product_or_native_dependency() {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "CanDoItAll.slnx"))) {
            root = root.Parent;
        }
        Assert.NotNull(root);
        foreach (var project in new[] { "UI/CanDoItAll.Configuration.UI", "Sandboxes/CanDoItAll.Configuration.UiSandbox" }) {
            using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "src", project, "obj", "project.assets.json")));
            foreach (var library in assets.RootElement.GetProperty("libraries").EnumerateObject()) {
                var name = library.Name.Split('/')[0];
                Assert.Contains(name, new[] { "CanDoItAll.Configuration.UI", "CanDoItAll.SharedKernel", "CanDoItAll.Components.BaseLib", "CanDoItAll.Components.Common", "Microsoft.AspNetCore.App.Internal.Assets" });
            }
            foreach (var target in assets.RootElement.GetProperty("targets").EnumerateObject()) {
                foreach (var library in target.Value.EnumerateObject()) {
                    Assert.False(library.Value.TryGetProperty("native", out _));
                    if (library.Value.TryGetProperty("runtimeTargets", out var runtimeTargets)) {
                        Assert.DoesNotContain(runtimeTargets.EnumerateObject(), asset => asset.Value.GetProperty("assetType").GetString() == "native");
                    }
                }
            }
        }
        var properties = typeof(ConfigurationSchemaRenderer).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
        Assert.Equal(typeof(IReadOnlyList<ConfigurationSecretOption>), Assert.Single(properties, property => property.Name == "Secrets").PropertyType);
        Assert.DoesNotContain(typeof(ConfigurationSchemaRenderer).Assembly.GetReferencedAssemblies(), assembly => assembly.Name!.StartsWith("CanDoItAll.Modules.", StringComparison.Ordinal));
    }
}
