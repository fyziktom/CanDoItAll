using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Playwright.Smoke;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class AgentAuthoringTeamNativeBrowserTests {
    [Fact]
    public async Task Team_UI_metadata_membership_concurrency_and_deletion_preserve_native_agent_authority() {
        await using var host = await LiveUiHost.StartAsync();
        var marker = "CA1-team-" + Guid.NewGuid().ToString("N");
        var first = await SaveAgentAsync(marker + " first");
        var second = await SaveAgentAsync(marker + " second");
        var neighbor = await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().SaveAgentTeamAsync(new() {
            Name = marker + " neighbor", AgentIds = [first]
        }));
        var before = await CaptureAgentsAsync();
        var page = await host.NewPageAsync();
        try {
            await page.GotoAsync(host.BaseUrl + "/agents?tab=agents");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.WaitForFunctionAsync("() => typeof databaseSwitchListeners !== 'undefined' && databaseSwitchListeners.size === 1");
            await Assertions.Expect(page.GetByTestId("agents-catalog-card").Filter(new() { HasTextString = marker + " first" })).ToBeVisibleAsync();
            await page.GetByTestId("agents-team-new").ClickAsync();
            await page.GetByTestId("agents-team-name").FillAsync(marker);
            await page.GetByTestId("agents-team-description").FillAsync("Grouping confers no new authority.");
            await page.GetByTestId("agents-team-choose-icon").ClickAsync();
            await page.GetByTestId("material-icon-picker-search").FillAsync("engineering");
            await Assertions.Expect(page.GetByTestId("material-icon-picker-option")).ToHaveCountAsync(1);
            await page.GetByTestId("material-icon-picker-option").PressAsync("Enter");
            await page.GetByTestId("material-icon-picker-confirm").ClickAsync();
            await page.GetByTestId("agents-team-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-team-details-dialog-content")).ToHaveCountAsync(0);
            var team = await host.SeedAsync(async services => Assert.Single(await services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentTeamsAsync(), item => item.Name == marker));
            Assert.Equal("engineering", team.Icon);
            Assert.Empty(team.AgentIds);
            await page.GetByTestId("agents-team-members").ClickAsync();
            await page.GetByTestId("agents-team-members-search").FillAsync(marker + " first");
            await Assertions.Expect(page.GetByTestId("agents-team-member-card")).ToHaveCountAsync(1);
            await page.GetByTestId("agents-team-member-card").PressAsync("Enter");
            await page.GetByTestId("agents-team-members-confirm").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-team-members-dialog")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("agents-catalog-card")).ToHaveCountAsync(1);
            Assert.Equal(new[] { first }, (await ReadTeamAsync(team.Id)).AgentIds);

            await page.GetByTestId("agents-team-edit").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-team-name")).ToHaveValueAsync(marker);
            await host.SeedAsync(async services => {
                await services.GetRequiredService<IAgentFrameworkWorkspaceService>().UpdateAgentTeamMembersAsync(team.Id, [first, second]);
                return true;
            });
            await page.GetByTestId("agents-team-name").FillAsync(marker + " renamed");
            await page.GetByTestId("agents-team-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-team-details-dialog-content")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("agents-catalog-card")).ToHaveCountAsync(2);
            var concurrent = await ReadTeamAsync(team.Id);
            Assert.Equal(marker + " renamed", concurrent.Name);
            Assert.Equal(new[] { first, second }.Order(), concurrent.AgentIds.Order());
            await page.ScreenshotAsync(new() { Path = host.Artifact("ca1-native-team-concurrent.png") });
            await page.GetByTestId("agents-team-edit").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-team-selected-icon")).ToContainTextAsync("engineering");
            await page.Keyboard.PressAsync("Escape");
            await page.GetByTestId("agents-team-delete").ClickAsync();
            await Assertions.Expect(page.GetByTestId("agents-team-tree-team").Filter(new() { HasTextString = marker + " renamed" })).ToHaveCountAsync(0);
            var remaining = await host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().ListAgentTeamsAsync());
            Assert.DoesNotContain(remaining, item => item.Id == team.Id);
            Assert.Equal(new[] { first }, Assert.Single(remaining, item => item.Id == neighbor).AgentIds);
            Assert.Equal(before, await CaptureAgentsAsync());
            await File.WriteAllTextAsync(host.Artifact("ca1-native-team.json"), JsonSerializer.Serialize(new {
                Team = team.Id, Neighbor = neighbor, Agents = new[] { first, second },
                ConcurrentMembershipPreserved = true, GroupOnlyDeleted = true, AgentDefinitionsUnchanged = true, Viewport = "1920x1080@1"
            }));
        } catch (Exception exception) {
            await File.WriteAllTextAsync(host.Artifact("ca1-native-team-failure.txt"), exception.ToString());
            await File.WriteAllTextAsync(host.Artifact("ca1-native-team-page.txt"), await page.Locator("body").InnerTextAsync());
            await page.ScreenshotAsync(new() { Path = host.Artifact("ca1-native-team-failure.png"), Timeout = 5_000 });
            await host.CaptureHostLogAsync();
            throw;
        }

        Task<Guid> SaveAgentAsync(string name) => host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().SaveAgentAsync(new() {
            Name = name, RoleTitle = "Owned membership proof", Instructions = "Preserve this instruction.", SelectedCapabilityIds = []
        }));
        Task<AgentTeamEditorModel> ReadTeamAsync(Guid id) => host.SeedAsync(services => services.GetRequiredService<IAgentFrameworkWorkspaceService>().GetAgentTeamEditorAsync(id));
        Task<string> CaptureAgentsAsync() => host.SeedAsync(async services => {
            var owner = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            return JsonSerializer.Serialize(new[] { await owner.GetAgentEditorAsync(first), await owner.GetAgentEditorAsync(second) });
        });
    }
}
