using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

internal static class AgentEditorCoreUiJourney {
    internal sealed record SavedCore(Guid AgentId, Guid ProviderId, string Model, string Instructions);

    internal static async Task<SavedCore> EditAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page, Guid agentId) {
        await page.SetViewportSizeAsync(1920, 1080);
        var baseline = await host.SeedAsync(async services => {
            var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var before = await workspace.GetAgentEditorAsync(agentId);
            var provider = (await workspace.ListProvidersAsync()).Single(item => item.Id == before.ProviderProfileId);
            var other = (await workspace.ListAgentsAsync(false)).First(item => item.Id != agentId);
            return (Before: before, Provider: provider, Other: other.Id, OtherJson: JsonSerializer.Serialize(await workspace.GetAgentEditorAsync(other.Id)));
        });
        await oracle.NavigateAsync($"{host.BaseUrl}/agents?tab=agents&agentId={agentId:D}");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
        var dialog = page.GetByTestId("agents-details-dialog").Last;
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-name")).ToHaveValueAsync(baseline.Before.Name);
        await Assertions.Expect(dialog.GetByRole(AriaRole.Tab)).ToHaveCountAsync(10);
        await dialog.GetByTestId("agents-catalog-name").FillAsync(" ");
        await dialog.GetByTestId("agents-catalog-name").PressAsync("Enter");
        await page.GetByText("Agent save failed", new() { Exact = true }).First.WaitForAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-name")).ToHaveValueAsync(" ");
        await host.SeedAsync(async services => {
            var current = await services.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentEditorAsync(agentId);
            Assert.Equal(baseline.Before.Name, current.Name);
            Assert.Equal(baseline.Before.ExpectedUpdatedAtUtc, current.ExpectedUpdatedAtUtc);
            return true;
        });
        await dialog.GetByTestId("agents-catalog-name").FillAsync("A2 saved Žluťoučký 東京");
        await dialog.GetByTestId("agents-catalog-role").FillAsync("Scoped file reviewer");
        await dialog.GetByTestId("agents-catalog-summary").FillAsync("Ten editor sections, real retained authority.");
        const string instructions = "A2 identity marker 東京. Use only the exact requested tools and scoped project. Respect approvals and denials. Never invent tool results.";
        await dialog.GetByTestId("agents-catalog-instructions").FillAsync(instructions);
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-core-identity.png") });
        await Tab("Runtime");
        await dialog.GetByTestId("agents-catalog-provider").SelectOptionAsync(new SelectOptionValue { Label = baseline.Provider.Name });
        await dialog.GetByTestId("agents-catalog-model-override").UncheckAsync();
        var choices = dialog.GetByTestId("agents-catalog-model-choice");
        var defaultValue = await choices.Locator("option").First.GetAttributeAsync("value");
        await choices.SelectOptionAsync(defaultValue!);
        await dialog.GetByTestId("agents-catalog-thinking-effort").SelectOptionAsync(new SelectOptionValue { Label = "High" });
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-auto-approval")).Not.ToBeCheckedAsync();
        await dialog.GetByTestId("agents-catalog-auto-approval").CheckAsync();
        var confirmation = page.GetByTestId("agents-auto-approval-confirmation").Last;
        await Assertions.Expect(confirmation).ToBeVisibleAsync();
        await confirmation.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-auto-approval")).Not.ToBeCheckedAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-thinking-effort-support")).ToContainTextAsync("supports configurable thinking effort");
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-core-runtime.png") });
        await Tab("Images");
        await dialog.GetByTestId("agents-catalog-image-generation-project-assets").CheckAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-image-generation-enabled")).Not.ToBeCheckedAsync();
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-core-images.png") });
        await Tab("Voice");
        await dialog.GetByTestId("agents-catalog-voice-enabled").CheckAsync();
        await dialog.GetByTestId("agents-catalog-voice-override").SelectOptionAsync("alloy");
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-save")).ToBeInViewportAsync(new() { Ratio = 1 });
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-core-voice.png") });
        await Tab("Memory");
        await dialog.GetByTestId("agents-catalog-memory-mode").SelectOptionAsync(nameof(AgentMemoryInvocationMode.ExplicitDirective));
        await dialog.GetByTestId("agents-catalog-crm-source-read").CheckAsync();
        var alias = dialog.GetByTestId("agents-catalog-memory-new-alias");
        await alias.FillAsync("unadded-東京");
        Assert.True(await alias.EvaluateAsync<bool>("element => element.form === null"));
        await alias.PressAsync("Enter");
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-a2-memory.png") });
        await Tab("Project Structure Access");
        await dialog.GetByTestId("agents-catalog-project-structure-load").ClickAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-project-structure-projects")).ToBeVisibleAsync();
        foreach (var projectId in baseline.Before.ProjectStructureAccess.AllowedProjectIds) {
            await Assertions.Expect(dialog.Locator($"input[data-project-id='{projectId:D}']")).ToBeCheckedAsync();
        }
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-project-structure-all")).Not.ToBeCheckedAsync();
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-a2-project-access.png") });
        await Tab("Workspace Tools");
        await dialog.GetByTestId("agents-catalog-workspace-scripts").CheckAsync();
        await page.GetByTestId("agents-workspace-scripts-confirmation-cancel").ClickAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-workspace-scripts")).Not.ToBeCheckedAsync();
        var root = dialog.GetByTestId("agents-catalog-workspace-external-roots-input");
        await root.FillAsync("unadded-relative-東京");
        Assert.True(await root.EvaluateAsync<bool>("element => element.form === null"));
        await root.PressAsync("Enter");
        await dialog.GetByTestId("agents-catalog-storage-selection-choose").ClickAsync();
        await Assertions.Expect(page.GetByTestId("agents-catalog-storage-selection-dialog-shell")).ToBeVisibleAsync();
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-a2-storage-picker.png") });
        await page.GetByTestId("agents-catalog-storage-selection-dialog-cancel").ClickAsync();
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-a2-workspace-tools.png") });
        await Tab("Secrets");
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-secrets")).ToBeVisibleAsync();
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-a2-secret-references.png") });
        await Tab("Process Access");
        await dialog.GetByTestId("agents-catalog-process-read").CheckAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-process-write")).Not.ToBeCheckedAsync();
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-a2-process-access.png") });
        await Tab("Capabilities");
        await dialog.GetByTestId("agents-details-capability-assignment-filter").SelectOptionAsync("Attached");
        await Assertions.Expect(dialog.GetByTestId("agents-details-capability-toggle")).ToHaveCountAsync(baseline.Before.SelectedCapabilityIds.Count);
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-a2-capabilities.png") });
        Assert.Equal(1, await dialog.Locator("form").CountAsync());
        await host.SeedAsync(async services => {
            var unchanged = await services.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentEditorAsync(agentId);
            Assert.Equal(baseline.Before.ExpectedUpdatedAtUtc, unchanged.ExpectedUpdatedAtUtc);
            Assert.Equal(baseline.Before.Name, unchanged.Name);
            return true;
        });
        await dialog.GetByTestId("agents-catalog-save").ClickAsync();
        await page.GetByText("Agent saved", new() { Exact = true }).First.WaitForAsync(new() { Timeout = 60_000 });
        var saved = await host.SeedAsync(async services => {
            var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            Assert.Equal(baseline.OtherJson, JsonSerializer.Serialize(await workspace.GetAgentEditorAsync(baseline.Other)));
            return await workspace.GetAgentEditorAsync(agentId);
        });
        Assert.Equal("A2 saved Žluťoučký 東京", saved.Name);
        Assert.Equal("Scoped file reviewer", saved.RoleTitle);
        Assert.Equal(instructions, saved.Instructions);
        Assert.Equal(baseline.Provider.Id, saved.ProviderProfileId);
        Assert.Empty(saved.Model);
        Assert.Equal(AgentReasoningEffortLevel.High, saved.ThinkingEffortOverride);
        Assert.True(saved.ImageGenerationAccess.CanStoreImagesAsProjectAssets);
        Assert.False(saved.ImageGenerationAccess.CanGenerateImages);
        Assert.Equal("alloy", saved.VoiceAccess.PreferredVoiceId);
        Assert.True(saved.VoiceAccess.CanUseVoiceMode);
        Assert.Equal(JsonSerializer.Serialize(baseline.Before.ProjectStructureAccess), JsonSerializer.Serialize(saved.ProjectStructureAccess));
        Assert.Equal(JsonSerializer.Serialize(baseline.Before.WorkspaceToolAccess), JsonSerializer.Serialize(saved.WorkspaceToolAccess));
        Assert.Equal(JsonSerializer.Serialize(baseline.Before.Permissions), JsonSerializer.Serialize(saved.Permissions));
        Assert.Equal(AgentMemoryInvocationMode.ExplicitDirective, saved.MemoryAccess.InvocationMode);
        Assert.Contains(MemorySourceScope.Crm, saved.MemoryAccess.AllowedSourceScopes);
        Assert.Equal(baseline.Before.MemoryAccess.ProviderBindings, saved.MemoryAccess.ProviderBindings);
        Assert.True(saved.ProcessAccess.CanRead);
        Assert.False(saved.ProcessAccess.CanWrite);
        Assert.False(saved.ProcessAccess.AllowAllDefinitions);
        Assert.Equal(baseline.Before.SelectedCapabilityIds, saved.SelectedCapabilityIds);
        Assert.Equal(baseline.Before.AllowedSecretReferences, saved.AllowedSecretReferences);
        Assert.NotEqual(baseline.Before.ExpectedUpdatedAtUtc, saved.ExpectedUpdatedAtUtc);
        await oracle.NavigateAsync($"{host.BaseUrl}/agents?tab=agents&agentId={agentId:D}");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-name")).ToHaveValueAsync(saved.Name);
        await Tab("Runtime");
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-thinking-effort").Locator("option:checked")).ToHaveTextAsync("High");
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-core-reopened-runtime.png") });
        await Tab("Memory");
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-memory-mode")).ToHaveValueAsync(nameof(AgentMemoryInvocationMode.ExplicitDirective));
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-crm-source-read")).ToBeCheckedAsync();
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-memory-new-alias")).ToHaveValueAsync(string.Empty);
        await Tab("Process Access");
        await Assertions.Expect(dialog.GetByTestId("agents-catalog-process-read")).ToBeCheckedAsync();
        return new(agentId, baseline.Provider.Id, baseline.Provider.DefaultModel, instructions);

        Task Tab(string label) => dialog.GetByRole(AriaRole.Tab, new() { Name = label, Exact = true }).ClickAsync();
    }

    internal static void AssertRequest(JsonElement input, SavedCore core) {
        Assert.Equal(core.Model, input.GetProperty("model").GetString());
        Assert.Equal("high", input.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains(Strings(input), text => text.Contains(core.Instructions, StringComparison.Ordinal));
    }

    internal static async Task DeleteCompletedFixtureAsync(LiveUiHost host, CrmHrBrowserOracle oracle, IPage page, Guid agentId) {
        var before = await host.SeedAsync(async services => {
            var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var history = await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId));
            Assert.NotEmpty(history);
            var siblings = (await workspace.ListAgentsAsync()).Where(agent => agent.Id != agentId).ToArray();
            return (History: history.Count, Siblings: JsonSerializer.Serialize(siblings));
        });
        await oracle.NavigateAsync($"{host.BaseUrl}/agents?tab=agents&agentId={agentId:D}");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
        await page.GetByTestId("agents-catalog-delete").ClickAsync();
        await page.GetByTestId("agents-catalog-delete-cancel").ClickAsync();
        await host.SeedAsync(async services => {
            var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            Assert.Equal(before.History, (await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId))).Count);
            return true;
        });
        await page.GetByTestId("agents-catalog-delete").ClickAsync();
        await Assertions.Expect(page.GetByTestId("agents-catalog-delete-confirmation-content")).ToBeInViewportAsync(new() { Ratio = 1 });
        await page.ScreenshotAsync(new() { Path = host.Artifact("agent-a2-delete-completed-history.png") });
        await page.GetByTestId("agents-catalog-delete-confirm").ClickAsync();
        await page.GetByText("Agent deleted", new() { Exact = true }).First.WaitForAsync();
        await host.SeedAsync(async services => {
            var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var remaining = await workspace.ListAgentsAsync();
            Assert.DoesNotContain(remaining, agent => agent.Id == agentId);
            Assert.Equal(before.Siblings, JsonSerializer.Serialize(remaining));
            Assert.Empty(await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(AgentId: agentId)));
            return true;
        });
    }

    private static IEnumerable<string> Strings(JsonElement input) => input.ValueKind switch {
        JsonValueKind.String => [input.GetString()!],
        JsonValueKind.Object => input.EnumerateObject().SelectMany(property => Strings(property.Value)),
        JsonValueKind.Array => input.EnumerateArray().SelectMany(Strings),
        _ => []
    };
}
