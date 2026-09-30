using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ConfigurationWorkflowBrowserTests(PlaywrightAppFixture fixture) {
    [Fact]
    public async Task Production_workflow_saves_generic_and_real_trusted_renderer_settings_without_execution() {
        await using var services = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile, "Configuration.Browser",
            TestSchemaBootstrapModules.Full, new Dictionary<string, string?> {
                [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
            });
        await using var scope = services.CreateAsyncScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>();
        var generic = BuiltInWorkflowExecutorDescriptors.ProjectStructure;
        var trusted = BuiltInWorkflowExecutorDescriptors.ImageGeneration;
        var definition = await catalog.SaveDefinitionAsync(new(null, null, "Configuration boundary browser draft", "Saved only; never executed.",
            WorkflowLifecycleStatus.Draft,
            new(new("start"), [Node("start", WorkflowNodeKind.Start), Executor("generic", generic), Executor("trusted", trusted), Node("end", WorkflowNodeKind.End)],
                [Edge("start", "generic"), Edge("generic", "trusted"), Edge("trusted", "end")]),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)));
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 1600, Height = 1000 } });
        var page = await context.NewPageAsync();
        var errors = new List<string>();
        page.PageError += (_, message) => errors.Add(message);
        page.Console += (_, message) => {
            if (message.Type == "error") {
                errors.Add(message.Text);
            }
        };
        await page.GotoAsync($"{fixture.BaseUrl}/agents/workflows?workflowId={definition.Id.Value:D}");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForFunctionAsync("() => typeof databaseSwitchStorageListener === 'function'");
        await page.GetByTestId("workflows-tab-editor").ClickAsync();
        await page.GetByTestId("workflow-canvas-editor").WaitForAsync();
        if (!await page.GetByTestId("workflow-canvas-selection-window").IsVisibleAsync()) {
            await page.GetByTestId("workflow-canvas-toggle-selection").ClickAsync();
        }
        await OpenNode("generic");
        await page.GetByTestId("workflow-canvas-node-modal-settings-title").FillAsync("Žlutý document 東京");
        await page.GetByTestId("workflow-canvas-node-modal-settings-content").FillAsync("Exact safe draft content — never executed.");
        await page.GetByTestId("workflow-canvas-node-modal-close").ClickAsync();
        await OpenNode("trusted");
        var prompt = page.GetByTestId("workflow-canvas-node-modal-settings-prompt");
        await prompt.FillAsync("Configuration-only image prompt Žlutý 東京");
        await Assertions.Expect(page.GetByTestId("workflow-canvas-node-modal-settings-providerProfileId")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("[data-settings-renderer-resolution]")).ToHaveCountAsync(0);
        var evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "configuration-ui");
        Directory.CreateDirectory(evidence);
        await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "production-trusted-fields.png"), FullPage = true });
        await page.GetByTestId("workflow-canvas-node-modal-close").ClickAsync();
        await page.GetByTestId("workflow-canvas-save").ClickAsync();
        WorkflowDefinition? stored = null;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (stored is null || stored.VersionId == definition.VersionId) {
            await Task.Delay(100, deadline.Token);
            stored = (await catalog.GetDefinitionAsync(definition.Id, null, deadline.Token))?.Definition;
        }
        Assert.Equal(definition.Id, stored.Id);
        var savedGeneric = Assert.Single(stored.Graph.Nodes, node => node.Id.Value == "generic");
        var savedTrusted = Assert.Single(stored.Graph.Nodes, node => node.Id.Value == "trusted");
        using var genericJson = JsonDocument.Parse(savedGeneric.Settings.ExecutorSettingsJson!);
        using var trustedJson = JsonDocument.Parse(savedTrusted.Settings.ExecutorSettingsJson!);
        Assert.Equal("Žlutý document 東京", genericJson.RootElement.GetProperty("title").GetString());
        Assert.Equal("Exact safe draft content — never executed.", genericJson.RootElement.GetProperty("content").GetString());
        Assert.Equal("Configuration-only image prompt Žlutý 東京", trustedJson.RootElement.GetProperty("prompt").GetString());
        Assert.Equal(generic.Id, savedGeneric.Settings.ExecutorId);
        Assert.Equal(trusted.Id, savedTrusted.Settings.ExecutorId);
        await page.ReloadAsync();
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForFunctionAsync("() => typeof databaseSwitchStorageListener === 'function'");
        await page.GetByTestId("workflows-tab-editor").ClickAsync();
        await page.GetByTestId("workflow-canvas-editor").WaitForAsync();
        if (!await page.GetByTestId("workflow-canvas-selection-window").IsVisibleAsync()) {
            await page.GetByTestId("workflow-canvas-toggle-selection").ClickAsync();
        }
        await OpenNode("trusted");
        await Assertions.Expect(prompt).ToHaveValueAsync("Configuration-only image prompt Žlutý 東京");
        await File.WriteAllTextAsync(Path.Combine(evidence, "production-definition-proof.json"), JsonSerializer.Serialize(new {
            WorkflowId = stored.Id.Value, OriginalVersion = definition.VersionId.Value, SavedVersion = stored.VersionId.Value,
            Profile = fixture.OwnedDatabaseProfile.ProfileKey, GenericExecutor = generic.Id.Value, TrustedExecutor = trusted.Id.Value,
            ExactSettingsReadBack = true, Reopened = true, ExecutionRequested = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Empty(errors);
        async Task OpenNode(string name) {
            await page.GetByTestId("workflow-canvas-select-node").Filter(new() { HasText = name }).ClickAsync();
            await page.GetByTestId("workflow-canvas-open-selected-node-details").ClickAsync();
            await page.GetByTestId("workflow-canvas-node-details-modal").WaitForAsync();
        }
    }
    private static WorkflowNode Node(string id, WorkflowNodeKind kind) => new(new(id), kind, id, [],
        new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text));
    private static WorkflowNode Executor(string id, WorkflowExecutorDescriptor descriptor) => Node(id, WorkflowNodeKind.Executor) with {
        Settings = new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text) {
            ExecutorId = descriptor.Id, ExecutorSettingsJson = descriptor.DefaultSettingsJson, ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
        }
    };
    private static WorkflowEdge Edge(string from, string to) => new(new(from + "-" + to), new(from), null, new(to), null, WorkflowEdgeKind.Direct, string.Empty) {
        Routing = WorkflowEdgeRouting.Always
    };
}
