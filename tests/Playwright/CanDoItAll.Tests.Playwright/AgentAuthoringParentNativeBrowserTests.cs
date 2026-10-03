using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Playwright.Smoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentAuthoringParentNativeBrowserTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_nested_wizard_keeps_existing_whole_draft_save_and_new_agent_staging(bool existing) {
        await using var host = await LiveUiHost.StartAsync();
        var marker = "CA1-parent-" + Guid.NewGuid().ToString("N");
        var agentId = existing ? await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().SaveAgentAsync(new() {
            Name = marker, Instructions = "Original", SelectedCapabilityIds = []
        })) : (Guid?)null;
        var page = await host.NewPageAsync();
        try {
            await page.GotoAsync(host.BaseUrl + "/agents?tab=agents" + (agentId.HasValue ? "&agentId=" + agentId : string.Empty));
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
            if (!existing) {
                await Assertions.Expect(page.GetByTestId("agents-catalog-card").First).ToBeVisibleAsync();
                await page.GetByTestId("agents-catalog-new").ClickAsync();
            }
            var editor = page.GetByTestId("agents-details-dialog").Last;
            await editor.GetByTestId("agents-catalog-name").FillAsync(marker);
            await editor.GetByTestId("agents-catalog-summary").FillAsync("Unsaved whole-agent summary");
            await editor.GetByTestId("agents-catalog-instructions").FillAsync("Unsaved instructions Ω");
            await editor.GetByRole(AriaRole.Tab, new() { Name = "Memory", Exact = true }).ClickAsync();
            await editor.GetByTestId("agents-catalog-memory-mode").SelectOptionAsync(nameof(AgentMemoryInvocationMode.ExplicitDirective));
            await editor.GetByRole(AriaRole.Tab, new() { Name = "Capabilities", Exact = true }).ClickAsync();
            await editor.GetByTestId("agents-details-new-skill").ClickAsync();
            await page.GetByTestId("agents-capability-setup-name").FillAsync(marker + " skill");
            await page.GetByTestId("agents-capability-setup-next").ClickAsync();
            await page.GetByTestId("agents-capability-setup-skill-mode").SelectOptionAsync("Inline");
            await page.GetByTestId("agents-capability-setup-inline-instructions").FillAsync("Read-only owned skill instructions.");
            await page.GetByTestId("agents-capability-setup-next").ClickAsync();
            await page.GetByTestId("agents-capability-setup-create").ClickAsync();
            await Assertions.Expect(page.GetByTestId("capability-authoring-form")).ToHaveCountAsync(0);
            await Assertions.Expect(editor.GetByTestId("agents-details-new-skill")).ToBeEnabledAsync();
            var capability = await host.SeedAsync(async services => Assert.Single(await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListCapabilitiesAsync(), item => item.Name == marker + " skill"));
            if (!existing) {
                await Assertions.Expect(editor.GetByTestId("agents-created-capability-result")).ToContainTextAsync("Capability created and staged");
                Assert.DoesNotContain(await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(false)), item => item.Name == marker);
                await editor.GetByTestId("agents-catalog-save").ClickAsync();
                await Assertions.Expect(page.GetByText("Agent saved", new() { Exact = true })).ToBeVisibleAsync();
                agentId = await host.SeedAsync(async services => Assert.Single(await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentsAsync(false), item => item.Name == marker).Id);
            }
            var saved = await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentEditorAsync(agentId));
            Assert.Equal("Unsaved whole-agent summary", saved.Summary);
            Assert.Equal("Unsaved instructions Ω", saved.Instructions);
            Assert.Equal(AgentMemoryInvocationMode.ExplicitDirective, saved.MemoryAccess.InvocationMode);
            Assert.Contains(capability.Id, saved.SelectedCapabilityIds);
            Assert.Single(await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListCapabilitiesAsync()), item => item.Id == capability.Id);
            await page.ScreenshotAsync(new() { Path = host.Artifact("ca1-parent-" + existing + ".png") });
        } catch (Exception exception) {
            await File.WriteAllTextAsync(host.Artifact("ca1-parent-failure.txt"), exception.ToString());
            await File.WriteAllTextAsync(host.Artifact("ca1-parent-page.txt"), await page.Locator("body").InnerTextAsync());
            await host.CaptureHostLogAsync();
            throw;
        }
    }
}
